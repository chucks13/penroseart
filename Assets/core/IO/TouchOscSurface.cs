// TouchOSC control-surface transport built on RaveSystem.Osc: decodes operator intents inbound and
// encodes surface feedback outbound, so no Penrose runtime code handles OSC wire format directly.

using System;
using System.Collections.Generic;
using System.Net;
using RaveSystem.Osc;
using UnityEngine;

/// <summary>The kind of operator intent carried by a <see cref="TouchOscCommand"/>.</summary>
public enum TouchOscCommandKind
{
    /// <summary>Master brightness fader moved. <see cref="TouchOscCommand.Value"/> is the raw 0..1 fader position.</summary>
    Brightness,

    /// <summary>The NYE toggle was pressed.</summary>
    ToggleNye,

    /// <summary>A grid cell was pressed. <see cref="TouchOscCommand.Button"/> is the 0-based cell index.</summary>
    PressButton,

    /// <summary>Effect-period fader moved. <see cref="TouchOscCommand.Value"/> is the raw 0..1 fader position.</summary>
    EffectPeriod,

    /// <summary>The reset button was pressed: release the held effect and let the deck rotate again.</summary>
    ReleaseHold,
}

/// <summary>
/// One decoded TouchOSC operator intent. Queued on the socket thread and applied on Unity's main thread.
/// </summary>
/// <remarks>
/// Commands are events, not state: repeated presses must neither collapse into one another nor reorder,
/// which is why the surface queues them instead of exposing a last-value-wins snapshot the way
/// <see cref="RaveOscReceiver"/> does for the RaveSystem beat wire.
/// </remarks>
public readonly struct TouchOscCommand
{
    /// <summary>Which intent this command carries.</summary>
    public readonly TouchOscCommandKind Kind;

    /// <summary>The raw fader position for <see cref="TouchOscCommandKind.Brightness"/> and <see cref="TouchOscCommandKind.EffectPeriod"/>; otherwise zero.</summary>
    public readonly float Value;

    /// <summary>The 0-based grid cell for <see cref="TouchOscCommandKind.PressButton"/>; otherwise -1.</summary>
    public readonly int Button;

    /// <summary>The wire text for the runtime HUD's transient "last OSC message" line.</summary>
    public readonly string Text;

    /// <summary>Creates a command. Use <see cref="Fader"/> or <see cref="Press"/> rather than calling this directly.</summary>
    private TouchOscCommand(TouchOscCommandKind kind, float value, int button, string text)
    {
        Kind = kind;
        Value = value;
        Button = button;
        Text = text;
    }

    /// <summary>Creates a fader/toggle command carrying a raw wire value.</summary>
    public static TouchOscCommand Fader(TouchOscCommandKind kind, float value, string text) =>
        new TouchOscCommand(kind, value, -1, text);

    /// <summary>Creates a grid press command for the given 0-based cell.</summary>
    public static TouchOscCommand Press(int button, string text) =>
        new TouchOscCommand(TouchOscCommandKind.PressButton, 0f, button, text);
}

/// <summary>
/// Unity component owning the TouchOSC control surface: listens on UDP 6969, decodes page-1 messages into
/// <see cref="TouchOscCommand"/> intents for the main thread, and sends surface feedback back to the
/// operator's tablet.
/// </summary>
/// <remarks>
/// This is the sole owner of TouchOSC's address vocabulary. Callers speak in intents and feedback
/// ("light cell 4", "brightness is 0.3"), never in OSC addresses.
/// <para>
/// Threading: <see cref="OscUdpSocket"/> raises packets on a thread-pool worker, so handlers only enqueue.
/// <see cref="TryDequeue"/>, the queue-feedback methods, and <see cref="FlushReplies"/> are main-thread only.
/// </para>
/// </remarks>
public sealed class TouchOscSurface : MonoBehaviour
{
    /// <summary>UDP port the TouchOSC tablet sends to.</summary>
    private const int ListenPort = 6969;

    /// <summary>UDP port the TouchOSC tablet listens on for feedback.</summary>
    private const int ReplyPort = 6161;

    /// <summary>The number of named effect controls exposed by the TouchOSC surface.</summary>
    private const int PushButtonCount = 27;

    // Deep enough that ordinary bursts (a fader sweep is ~60 messages/second) never touch it. Reaching the
    // cap means the main thread has stalled, so the oldest intents are dropped first: an operator's most
    // recent action matters more than a stale one.
    private const int MaxQueuedCommands = 256;

    // A full refresh bundles one message per grid cell (~28 bytes each), so 8 KB covers a grid far
    // larger than the current one.
    private const int SendBufferSize = 8192;

    private readonly object queueLock = new object();
    private readonly Queue<TouchOscCommand> commands = new Queue<TouchOscCommand>();
    private readonly List<PendingReply> pendingReplies = new List<PendingReply>();
    private readonly byte[] sendBuffer = new byte[SendBufferSize];

    private OscDispatcher dispatcher;
    private OscUdpSocket socket;
    private OscUdpSender sender;

    // Feedback is broadcast, never narrowed to one peer. Any number of surfaces can drive the wall at
    // once, and none of them is "the" surface: a surface that has never transmitted still receives, and
    // a surface that joins late catches up on the next heartbeat. Unicast would be the more reliable
    // single delivery -- Wi-Fi acks and retries it where it does neither for a broadcast frame -- but a
    // heartbeat retransmits by repetition, so a dropped frame costs one interval of staleness instead of
    // a wrong lamp until the next change.
    /// <summary>Seconds between full-state restatements. Sends between them carry only what changed.</summary>
    [Tooltip("Seconds between full-state broadcasts to every control surface. Lower is more responsive to changes made outside the surface; the whole state is under a kilobyte.")]
    [SerializeField]
    private float heartbeatSeconds = 0.5f;

    /// <summary>Floor on <see cref="heartbeatSeconds"/>, so a mistyped Inspector value cannot flood the link.</summary>
    private const float MinHeartbeatSeconds = 0.05f;

    private float nextHeartbeatTime;

    private readonly object errorLock = new object();
    private Exception pendingError;
    private bool hasPendingError;
    private int droppedCommands;
    private bool loggedSendFailure;

    /// <summary>The number of named effect cells the surface exposes.</summary>
    public static int GridCellCount => PushButtonCount;

    private void Awake()
    {
        StartListening();
    }

    private void OnDestroy()
    {
        StopListening();
    }

    private void Update()
    {
        ReportDecodeFailure();
        ReportDroppedCommands();
    }

    /// <summary>
    /// Reports whether this frame owes every control surface a full restatement of the wall's state.
    /// </summary>
    /// <remarks>
    /// Feedback is otherwise edge-driven, which leaves a surface wrong until the next change if a
    /// datagram is lost, and leaves a surface that joined late wrong indefinitely. The heartbeat is the
    /// corrector: send-on-change keeps presses instant, and this keeps everyone converging on the truth.
    /// </remarks>
    /// <returns><c>true</c> when the heartbeat interval has elapsed, including on the first call.</returns>
    public bool ConsumeFullStateDue()
    {
        if (Time.unscaledTime < nextHeartbeatTime)
        {
            return false;
        }

        nextHeartbeatTime = Time.unscaledTime + Mathf.Max(MinHeartbeatSeconds, heartbeatSeconds);
        return true;
    }

    /// <summary>
    /// Takes the next queued operator intent. Call in a loop from the main thread until it returns false.
    /// </summary>
    /// <param name="command">The dequeued intent when this returns true.</param>
    /// <returns><c>true</c> when an intent was dequeued; <c>false</c> when the queue is empty.</returns>
    public bool TryDequeue(out TouchOscCommand command)
    {
        lock (queueLock)
        {
            if (commands.Count == 0)
            {
                command = default;
                return false;
            }

            command = commands.Dequeue();
            return true;
        }
    }

    /// <summary>Queues the lit state of the given 0-based grid cell.</summary>
    public void QueueButtonState(int button, bool lit) => QueueReply(PushAddress(button), lit ? 1f : 0f);

    /// <summary>Queues feedback moving the brightness fader to <paramref name="faderValue"/> (0..1).</summary>
    public void QueueBrightness(float faderValue) => QueueReply("/1/vscroll1", faderValue);

    /// <summary>Queues feedback moving the effect-period fader to <paramref name="faderValue"/> (0..1).</summary>
    public void QueueEffectPeriod(float faderValue) => QueueReply("/1/hscroll1", faderValue);

    /// <summary>Queues the reset lamp, lit while an effect is held and the deck is frozen.</summary>
    public void QueueReset(bool held) => QueueReply("/1/reset", held ? 1f : 0f);

    /// <summary>
    /// Sends everything queued since the last flush as one datagram, then clears the queue. A single reply
    /// goes out as a plain message; two or more are wrapped in an immediate OSC bundle.
    /// </summary>
    public void FlushReplies()
    {
        if (pendingReplies.Count == 0)
        {
            return;
        }

        try
        {
            Send(EncodeReplies());
        }
        finally
        {
            pendingReplies.Clear();
        }
    }

    /// <summary>Encodes the pending replies into <see cref="sendBuffer"/> and returns the byte count.</summary>
    private int EncodeReplies()
    {
        if (pendingReplies.Count == 1)
        {
            return WriteMessage(sendBuffer, pendingReplies[0].Address, pendingReplies[0].Value);
        }

        var bundle = new OscBundleWriter(sendBuffer, OscTimeTag.Immediately);
        for (int i = 0; i < pendingReplies.Count; i++)
        {
            Span<byte> element = bundle.BeginElement();
            bundle.EndElement(WriteMessage(element, pendingReplies[i].Address, pendingReplies[i].Value));
        }

        return bundle.Finish();
    }

    /// <summary>Writes one single-float OSC message into <paramref name="destination"/> and returns its byte count.</summary>
    private static int WriteMessage(Span<byte> destination, string address, float value)
    {
        var writer = new OscWriter(destination);
        writer.WriteAddress(address);
        writer.WriteFloat32(value);
        return writer.Finish();
    }

    /// <summary>Sends the first <paramref name="length"/> bytes of <see cref="sendBuffer"/>, logging the first failure only.</summary>
    private void Send(int length)
    {
        if (sender == null)
        {
            return;
        }

        try
        {
            sender.Send(new ReadOnlySpan<byte>(sendBuffer, 0, length));
        }
        catch (Exception ex)
        {
            // Feedback runs every frame, so a persistent transport fault would otherwise flood the log.
            if (!loggedSendFailure)
            {
                loggedSendFailure = true;
                Debug.LogWarning($"[TouchOscSurface] Failed to send feedback to {sender.RemoteEndPoint}; suppressing further send warnings until the peer changes. {ex}");
            }
        }
    }

    private void QueueReply(string address, float value)
    {
        pendingReplies.Add(new PendingReply(address, value));
    }

    /// <summary>Builds the grid cell address for a 0-based <paramref name="button"/> index (the wire is 1-based).</summary>
    private static string PushAddress(int button) => "/1/push" + (button + 1);

    private void StartListening()
    {
        if (socket != null)
        {
            return;
        }

        dispatcher = new OscDispatcher();
        RegisterPageOne();

        // 255.255.255.255 is the limited broadcast address, which reaches the local link without knowing
        // the subnet, so no address is configured anywhere and every surface is reached the same way.
        sender = new OscUdpSender(new IPEndPoint(IPAddress.Broadcast, ReplyPort), broadcast: true);
        socket = new OscUdpSocket(new IPEndPoint(IPAddress.Any, ListenPort));
        socket.PacketReceived += OnPacketReceived;
        socket.Start();
        Debug.Log($"[TouchOscSurface] Listening for TouchOSC on UDP {ListenPort}; broadcasting feedback to port {ReplyPort}.");
    }

    private void StopListening()
    {
        if (socket != null)
        {
            socket.PacketReceived -= OnPacketReceived;
            socket.Dispose();
            socket = null;
        }

        sender?.Dispose();
        sender = null;
        dispatcher?.Dispose();
        dispatcher = null;
    }

    /// <summary>Registers every page-1 address the control surface sends.</summary>
    private void RegisterPageOne()
    {
        dispatcher.Register("/1/vscroll1", (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) =>
            EnqueueFader(TouchOscCommandKind.Brightness, "/1/vscroll1", ref reader));

        dispatcher.Register("/1/hscroll1", (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) =>
            EnqueueFader(TouchOscCommandKind.EffectPeriod, "/1/hscroll1", ref reader));

        dispatcher.Register("/1/nav1", (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) =>
            EnqueuePressGated(TouchOscCommandKind.ToggleNye, "/1/nav1", -1, ref reader));

        dispatcher.Register("/1/reset", (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) =>
            EnqueuePressGated(TouchOscCommandKind.ReleaseHold, "/1/reset", -1, ref reader));

        for (int i = 0; i < PushButtonCount; i++)
        {
            int button = i;
            string address = PushAddress(i);
            dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) =>
                EnqueuePressGated(TouchOscCommandKind.PressButton, address, button, ref reader));
        }
    }

    /// <summary>Queues a fader move carrying the raw wire value.</summary>
    private void EnqueueFader(TouchOscCommandKind kind, string address, ref OscReader reader)
    {
        if (!TryReadNumber(ref reader, out float value))
        {
            return;
        }

        Enqueue(TouchOscCommand.Fader(kind, value, $"{address} {value}"));
    }

    /// <summary>
    /// Queues a press intent, but only on the press edge: the surface sends 1 on press and 0 on release,
    /// and only the press acts.
    /// </summary>
    private void EnqueuePressGated(TouchOscCommandKind kind, string address, int button, ref OscReader reader)
    {
        if (!TryReadNumber(ref reader, out float value) || (int)value != 1)
        {
            return;
        }

        string text = $"{address} 1";
        Enqueue(kind == TouchOscCommandKind.PressButton
            ? TouchOscCommand.Press(button, text)
            : TouchOscCommand.Fader(kind, 1f, text));
    }

    /// <summary>
    /// Reads the message's first argument as a number, accepting either int32 or float32.
    /// </summary>
    /// <remarks>
    /// TouchOSC sends buttons and faders as float32 but other surfaces send int32 for the same controls, so
    /// the surface accepts both rather than rejecting a message on its type tag.
    /// </remarks>
    /// <returns><c>true</c> when a numeric argument was read; <c>false</c> for a missing or non-numeric argument.</returns>
    private static bool TryReadNumber(ref OscReader reader, out float value)
    {
        value = 0f;
        if (!reader.MoveNext())
        {
            return false;
        }

        switch (reader.CurrentTag)
        {
            case OscToken.F32:
                value = reader.ReadFloat32();
                return true;
            case OscToken.I32:
                value = reader.ReadInt32();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Appends a decoded intent, dropping the oldest when the queue is full. Runs on the socket thread.</summary>
    private void Enqueue(TouchOscCommand command)
    {
        lock (queueLock)
        {
            while (commands.Count >= MaxQueuedCommands)
            {
                commands.Dequeue();
                droppedCommands++;
            }

            commands.Enqueue(command);
        }
    }

    /// <summary>Decodes one inbound packet on the socket thread. The sender's address is not recorded:
    /// feedback goes to every surface on the link, so there is nobody to single out.</summary>
    private void OnPacketReceived(ReadOnlySpan<byte> packet, System.Net.SocketAddress from)
    {
        try
        {
            dispatcher.Dispatch(packet);
        }
        catch (Exception ex)
        {
            lock (errorLock)
            {
                pendingError = ex;
                hasPendingError = true;
            }
        }
    }

    /// <summary>Surfaces the most recent socket-thread decode failure on the main thread.</summary>
    private void ReportDecodeFailure()
    {
        Exception error = null;
        lock (errorLock)
        {
            if (hasPendingError)
            {
                error = pendingError;
                pendingError = null;
                hasPendingError = false;
            }
        }

        if (error != null)
        {
            Debug.LogWarning($"[TouchOscSurface] Failed to decode a TouchOSC packet: {error}");
        }
    }

    /// <summary>Surfaces queue overflow, which means the main thread stalled long enough to lose operator input.</summary>
    private void ReportDroppedCommands()
    {
        int dropped;
        lock (queueLock)
        {
            dropped = droppedCommands;
            droppedCommands = 0;
        }

        if (dropped > 0)
        {
            Debug.LogWarning($"[TouchOscSurface] Dropped {dropped} queued command(s): the main thread fell more than {MaxQueuedCommands} messages behind.");
        }
    }

    /// <summary>One queued outbound single-float message.</summary>
    private readonly struct PendingReply
    {
        /// <summary>The OSC address to send to.</summary>
        public readonly string Address;

        /// <summary>The single float argument.</summary>
        public readonly float Value;

        /// <summary>Creates a pending reply.</summary>
        public PendingReply(string address, float value)
        {
            Address = address;
            Value = value;
        }
    }
}
