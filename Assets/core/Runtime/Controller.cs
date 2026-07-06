﻿
#define ENABLE_SERIAL

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Text;
//  gameObjectToHide.GetComponent<Renderer>().enabled = false;

using System.Net.NetworkInformation;

// git connection test 4/29/2026

/// <summary>
/// Main Unity-hosted runtime hub for PenroseArt. Owns catalogs, timing, input routing, output routing, overlays, and preview updates.
/// </summary>
/// <remarks>
/// Most visual runtime objects are plain C# classes, not MonoBehaviours. Controller is responsible for creating them and calling their lifecycle hooks.
/// </remarks>
public class Controller : Singleton<Controller>
{
    // ---------------------------------------------------------------------
    // Runtime catalogs and selection decks
    // ---------------------------------------------------------------------

    /// <summary>
    /// Rotating deck of indexes into <see cref="effects"/>. A card is drawn
    /// from the top half and moved to the bottom to reduce immediate repeats.
    /// </summary>
    [HideInInspector]
    public int[] effectDeck;

    /// <summary>
    /// Rotating deck of indexes into <see cref="transitions"/> using the same
    /// anti-repeat draw behavior as <see cref="effectDeck"/>.
    /// </summary>
    [HideInInspector]
    public int[] transitionDeck;

    /// <summary>
    /// External pixel-source blenders discovered by <see cref="Factory{T}"/>.
    /// </summary>
    [NonSerialized]
    public BlenderBase[] blenders;

    /// <summary>
    /// Optional active external-source blender selected by telnet/debug paths.
    /// When set, incoming PixelReceiver data is blended with the native buffer.
    /// </summary>
    [NonSerialized]
    public BlenderBase ActiveBlender = null;

    /// <summary>
    /// Optional transition reused as an external-source blender. This is separate
    /// from normal effect-to-effect transition playback.
    /// </summary>
    public TransitionBase ActiveTransitionBlender = null;

#if ENABLE_TELNET
    /// <summary>Optional telnet command server, compiled only with ENABLE_TELNET.</summary>
    public TelnetServer server;
#endif

    /// <summary>
    /// Scratch buffer for external pixel-source data before it is blended or
    /// copied into <see cref="penrose"/>.<see cref="Penrose.buffer"/>.
    /// </summary>
    [HideInInspector]
    public Color[] blendBuffer = new Color[Penrose.Total];

    /// <summary>
    /// New Year's Eve overlay mode: replaces the effect pipeline with random
    /// sparse white pixels on black.
    /// </summary>
    public bool NYE = false;

#if PREP_CAPTURE
    /// <summary>PREP_CAPTURE dummy input toggle for local blend-source testing.</summary>
    public bool dummyActive = false;
#endif

    // ---------------------------------------------------------------------
    // Display scheduling, brightness, and legacy filter controls
    // ---------------------------------------------------------------------

    /// <summary>Accumulator used by PREP_CAPTURE display scheduling.</summary>
    private float secondsAccululator = 0f;

    /// <summary>Hue-window scale used by <see cref="applyFilter"/>.</summary>
    public float FilterScale = 0.03f;

    /// <summary>Seconds remaining for the optional filter mode.</summary>
    public float FilterTimer = 0f;

    /// <summary>Wall-clock HHMM minute at which PREP_CAPTURE display scheduling turns output on.</summary>
    public float onMinute = 1700;

    /// <summary>Wall-clock HHMM minute at which PREP_CAPTURE display scheduling turns output off.</summary>
    public float offMinute = 200;

    /// <summary>UI input field for the UDP/E1.31 destination IP.</summary>
    public InputField destIP;

    /// <summary>UI input field for <see cref="onMinute"/>.</summary>
    public InputField onTime;

    /// <summary>UI input field for <see cref="offMinute"/>.</summary>
    public InputField offTime;

    /// <summary>UI toggle for <see cref="displayOn"/>.</summary>
    public Toggle onToggle;

    /// <summary>Whether <see cref="applyFilter"/> should hue-clamp the current frame.</summary>
    public bool FilterMode = false;

    /// <summary>Master output gate. When false, output brightness is forced to zero.</summary>
    public bool displayOn = true;

    // ---------------------------------------------------------------------
    // Hardware output state
    // ---------------------------------------------------------------------

    [Header("UDP")]
    /// <summary>Destination IP for the legacy UDP/E1.31 output path.</summary>
    public string IP;

    /// <summary>Master output brightness multiplier, 0-255.</summary>
    public byte brightness;

    /// <summary>Default E1.31/ACN UDP port.</summary>
    int port = 5568;

    /// <summary>Unused/static local port placeholder retained by the UDP setup code.</summary>
    private static int localPort;

    /// <summary>Destination endpoint for the legacy UDP/E1.31 output path.</summary>
    IPEndPoint remoteEndPoint;

    /// <summary>UDP client used by E1.31 output and PREP_CAPTURE pixel feedback.</summary>
    UdpClient client;

#if ENABLE_SERIAL
    /// <summary>
    /// Physical LED output buffer after expanding 900 logical Penrose tiles
    /// through the 1800-entry wire map.
    /// </summary>
    private Color[] serialOutputBuffer = new Color[1800];

    /// <summary>USB serial transport manager for S2 Mini / ESP32 boards.</summary>
    private SerialOut serial;
#endif

    /// <summary>Optional webcam overlay source, created only when <see cref="useCamera"/> is true.</summary>
    public CameraReader cameraOverlay;

    // ---------------------------------------------------------------------
    // Effect forcing and playback state
    // ---------------------------------------------------------------------

    [Header("Effect Lock")]
    /// <summary>
    /// The held effect, as a single source of truth for whether the wall rotates or stays put.
    /// <para>
    /// <c>-1</c> is the <b>Random</b> sentinel: the deck rotates normally through the Director,
    /// and this is the default so a fresh scene behaves exactly like the old unlocked state. Any value
    /// <c>&gt;= 0</c> is an index into <see cref="effects"/> that is <b>held</b>: the timer state machine and
    /// the deck never switch away from it until this is set back to <c>-1</c>.
    /// </para>
    /// <para>
    /// Replaces the former <c>forceEffect</c> (bool) + <c>forceEffectName</c> (string prefix) pair: the bool is
    /// now simply "is this <c>&gt;= 0</c>", and the target is the value itself. The inspector renders this as a
    /// catalog dropdown via <c>EffectSelectorDrawer</c> (row 0 = Random); pressing <c>Escape</c> resets it to
    /// <c>-1</c>. The blank <c>EmptyEffect</c> template is never selectable because <see cref="Factory{T}"/>
    /// excludes its <c>[RuntimeCatalogIgnore]</c> type from <see cref="effects"/> entirely.
    /// </para>
    /// </summary>
    [Tooltip("Random (-1) lets the deck rotate normally. Any other choice holds that effect and stops the " +
             "random switching until you pick Random again. Escape also resets this to Random.")]
    [EffectSelector]
    public int heldEffect = -1;

    /// <summary>Whether to create and draw the optional camera overlay.</summary>
    public bool useCamera;

    [Header("Effect Switching")]
    /// <summary>
    /// Serialized legacy field for an initial effect index. Current startup uses
    /// the effect deck / force override instead, so this is not actively read.
    /// </summary>
    public int startEffect;

    /// <summary>
    /// Index into <see cref="effects"/> for the currently playing effect. Set to
    /// -1 while an effect-to-effect transition is active.
    /// </summary>
    private int currentEffect;

    /// <summary>
    /// The beat variant (Waveform Pool index) of the effect currently on screen, or <c>-1</c> when no single
    /// effect owns the frame (startup gap or mid-transition, where <see cref="currentEffect"/> is -1).
    /// </summary>
    /// <remarks>
    /// Exposes the otherwise-private on-screen variant to the Waveform Pool selector in the BeatManager dashboard so
    /// it can show what the wall is actually pulsing to in "Auto" mode (the read-back), and so locking a specific
    /// Waveform can retarget the live effect's pulse immediately instead of waiting for the next effect to start.
    /// This is an editor/inspector affordance; the runtime selection model is unchanged.
    /// </remarks>
    public int CurrentBeatVariant
    {
        get
        {
            int index = switcher != null ? switcher.CurrentEffectIndex : currentEffect;
            return (effects != null && index >= 0 && index < effects.Length) ? effects[index].beatVariant : -1;
        }
        set
        {
            int index = switcher != null ? switcher.CurrentEffectIndex : currentEffect;
            if (effects != null && index >= 0 && index < effects.Length) effects[index].beatVariant = value;
        }
    }

    /// <summary>Read-only Director sequencing snapshot for inspectors and status displays.</summary>
    public DirectorStatus DirectorStatus => director != null ? director.Status : DirectorStatus.NotReady;

    /// <summary>Read-only Mechanical Switcher stage snapshot for inspectors and status displays.</summary>
    public SwitcherStatus SwitcherStatus => switcher != null ? switcher.Status : SwitcherStatus.NotReady;

    /// <summary>Read-only Switcher-held Loaded Cue snapshot for inspectors and status displays.</summary>
    public SwitcherCueStatus SwitcherLoadedCueStatus => switcher != null ? switcher.LoadedCueStatus : SwitcherCueStatus.Empty;

    /// <summary>Latest render-pipeline debug text captured before HUD filtering.</summary>
    public string LastRenderDebugText => lastRenderDebugText;

    /// <summary>Latest compact top-line HUD text shown on screen.</summary>
    public string LastRuntimeHudLine => lastRuntimeHudLine;

    /// <summary>Latest compact detail HUD text shown on screen.</summary>
    public string LastRuntimeDetailLine => lastRuntimeDetailLine;

    /// <summary>Keyboard bank: A-W select effects 0-22 or 23-45 depending on this value.</summary>
    private int keyboardBase = 0;

    /// <summary>Seconds to play an effect before starting the next transition.</summary>
    public float effectTime = 10f;

    [Header("Transition Switching")]
    /// <summary>
    /// Serialized legacy toggle from the older random-transition path. Current
    /// code always uses <see cref="transitionDeck"/> selection after startup.
    /// </summary>
    public bool randomTransition = true;

    /// <summary>
    /// Index into <see cref="transitions"/> for the active or next transition.
    /// The scene-serialized value controls the first transition before deck
    /// selection takes over.
    /// </summary>
    public int currentTransition;

    /// <summary>
    /// Legacy serialized transition duration retained for scene compatibility.
    /// Active Standalone Mode transition timing now comes from each transition's repertoire.
    /// </summary>
    public float transitionTime = 4f;

    // ---------------------------------------------------------------------
    // Scene-provided source data and helper systems
    // ---------------------------------------------------------------------

    /// <summary>Serialized palette definitions consumed by <see cref="AnimPalette"/>.</summary>
    public string paletteSource;

    /// <summary>Serialized Penrose geometry/wiring JSON consumed by <see cref="Penrose"/>.</summary>
    public string jsonSource;

    /// <summary>Drum/ring overlay system drawn after the main effect/transition.</summary>
    public drums drum;

    /// <summary>External UDP pixel-source receiver used for optional blending/replacement.</summary>
    public PixelReceiver readPixel;

    /// <summary>Global beat clock and beat-reactive helper system.</summary>
    public BeatManager beatManager = new BeatManager();

    // ---------------------------------------------------------------------
    // UI and scene references
    // ---------------------------------------------------------------------

    [Header("GUI")]
    /// <summary>UI label showing the active effect or transition name.</summary>
    public TextMeshProUGUI effectText;

    /// <summary>UI label showing compact runtime status plus optional verbose debug details.</summary>
    public TextMeshProUGUI debugText;

    [Header("Runtime HUD")]
    /// <summary>Whether the runtime HUD overlay should show compact runtime status text.</summary>
    public bool showRuntimeHud = true;

    /// <summary>When true, the bottom HUD includes effect/transition debug text and transport details.</summary>
    public bool showVerboseRuntimeHud = false;

    /// <summary>Alpha applied to the compact HUD text so it does not dominate the wall preview.</summary>
    [Range(0.15f, 1f)]
    public float runtimeHudAlpha = 0.78f;

    /// <summary>Whether Controller should resize the legacy scene text into a compact non-blocking HUD at startup.</summary>
    public bool configureCompactHudLayout = true;

    [Header("Director Debug Logging")]
    /// <summary>Writes tagged Director/Switcher sequencing diagnostics to the Unity log for post-run debugging.</summary>
    public bool logDirectorSwitching = false;

    /// <summary>UI label listing local IPv4 addresses.</summary>
    public TextMeshProUGUI myIPText;
    //  public TextMeshProUGUI myBrightnessText;

    /// <summary>Runtime catalog of top-level effects created by <see cref="SetupEffects"/>.</summary>
    [HideInInspector]
    public EffectBase[] effects;

    /// <summary>
    /// Temporary live-tuning aid: a per-effect override of an effect's code-declared <see cref="Repertoire"/>,
    /// keyed by effect catalog <see cref="EffectBase.Name"/> (never index — reflection catalog order is fragile).
    /// Presence of a matching entry replaces the declared affinity; remove the entry to restore it. Expected
    /// to be ripped out once real per-effect support lands.
    /// </summary>
    [Serializable]
    public struct EffectRepertoireOverride
    {
        /// <summary>Catalog name (<see cref="EffectBase.Name"/>) of the effect this override targets.</summary>
        public string effectName;

        /// <summary>Affinity flags advertised in place of the effect's declared <see cref="Repertoire"/>.</summary>
        public Repertoire flags;
    }

    /// <summary>
    /// Temporary live-tuning overrides of effect affinities, applied by <see cref="EffectiveRepertoire"/>.
    /// Empty by default (no behavior change); entries are added/edited at runtime via the Controller inspector.
    /// </summary>
    public List<EffectRepertoireOverride> effectRepertoireOverrides = new List<EffectRepertoireOverride>();

    /// <summary>
    /// The affinity the Director should treat <paramref name="effectIndex"/> as advertising: an override entry
    /// whose <see cref="EffectRepertoireOverride.effectName"/> matches the effect's catalog
    /// <see cref="EffectBase.Name"/> wins; otherwise the effect's code-declared <see cref="EffectBase.Repertoire"/>.
    /// Invalid index → <see cref="Repertoire.None"/>.
    /// </summary>
    public Repertoire EffectiveRepertoire(int effectIndex)
    {
        if (effects == null || effectIndex < 0 || effectIndex >= effects.Length || effects[effectIndex] == null)
        {
            return Repertoire.None;
        }

        var effect = effects[effectIndex];
        if (effectRepertoireOverrides != null)
        {
            foreach (var over in effectRepertoireOverrides)
            {
                if (over.effectName == effect.Name)
                {
                    return over.flags;
                }
            }
        }

        return effect.Repertoire;
    }

    /// <summary>Runtime catalog of transitions created by <see cref="SetupTransitions"/>.</summary>
    [HideInInspector]
    public TransitionBase[] transitions;

    /// <summary>Mechanical renderer/swapper for the currently staged effect or transition.</summary>
    [HideInInspector]
    public Switcher switcher;

    /// <summary>Decision layer that owns sequencing cadence and stage-directed cues.</summary>
    [HideInInspector]
    public Director director;

    /// <summary>Scene Penrose model and preview mesh component.</summary>
    [HideInInspector]
    public Penrose penrose;

    /// <summary>Effect/transition phase timer. Its finish event drives <see cref="Director.OnTimerFinished"/>.</summary>
    [NonSerialized]
    [HideInInspector]
    public Timer timer;

    // ---------------------------------------------------------------------
    // OSC, frame timing, and private frame state
    // ---------------------------------------------------------------------

    /// <summary>Legacy TouchOSC reader component added to the Controller GameObject.</summary>
    private OSCReader osc;

    /// <summary>RaveSystem OSC receiver backed by the new RaveSystem.Osc stack.</summary>
    private RaveOscReceiver raveOsc;

    /// <summary>Frames remaining before the latest OSC debug text is cleared.</summary>
    private int OSCtimer;

    /// <summary>Latest OSC message text shown temporarily in the debug label.</summary>
    private String OSCtext;

    /// <summary>Latest effect/transition debug text emitted by the render pipeline.</summary>
    private string lastRenderDebugText = string.Empty;

    /// <summary>Latest compact top-line HUD text shown on screen.</summary>
    private string lastRuntimeHudLine = string.Empty;

    /// <summary>Latest compact detail HUD text shown on screen.</summary>
    private string lastRuntimeDetailLine = string.Empty;

    /// <summary>Reusable byte buffer for legacy UDP/E1.31 frame packets.</summary>
    private byte[] udpFrameBuffer;

    /// <summary>Whether the controller is currently drawing a transition instead of a single effect.</summary>
    private bool inTransition;

#if PREP_CAPTURE
    /// <summary>Local time accumulator for PREP_CAPTURE dummy signal generation.</summary>
    public float diagnosticTime;
#endif

    /// <summary>Last frame delta time stored for effects/helpers that read Controller state directly.</summary>
    public float effectDelta;

    /// <summary>Approximate frames per second, sampled once per second by <see cref="Fps"/>.</summary>
    private float fps;

    /// <summary>Last sampled Time.frameCount used to calculate <see cref="fps"/>.</summary>
    private float lastCount;

    /// <summary>Current effect-button index used to stream OSC button state over repeated pings.</summary>
    private int pingIndex;

    /// <summary>
    /// Samples frame count once per second for the debug display.
    /// </summary>
    private IEnumerator Fps()
    {
        while (true)
        {
            fps = Time.frameCount - lastCount;
            lastCount = Time.frameCount;
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// Creates an ordered deck of catalog indexes from 0 to count - 1.
    /// </summary>
    private int[] initDeck(int count)
    {
        int[] deck = new int[count];
        for (int i = 0; i < count; i++)
            deck[i] = i;
        return deck;
    }

    /// <summary>
    /// Formats catalog names with stable numeric indexes for startup logs.
    /// </summary>
    private static string FormatCatalog(string[] names)
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < names.Length; i++)
        {
            if (i > 0)
                builder.AppendLine();

            builder.Append(i.ToString("00"));
            builder.Append(" ");
            builder.Append(names[i]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds the top-level effect catalog, initializes every effect once,
    /// creates the effect deck, and starts the first selected effect.
    /// </summary>
    private void SetupEffects()
    {
        var factory = new Factory<EffectBase>();

        effects = new EffectBase[factory.Count];
        for (int i = 0; i < effects.Length; i++)
        {
            effects[i] = factory.Create(factory.Types[i]);
            effects[i].BindController(this);
            effects[i].Init();
            //        effects[i].sortIndex = Random.Range(0, 10000);
            //        effects[i].initialIndex = i;
        }
        effectDeck = initDeck(effects.Length);
        pingIndex = 0;

        Debug.Log($"Effects ({effects.Length}):\n{FormatCatalog(factory.Names)}");

        //    effects[startEffect].sortIndex = -1;
        //    ReSortEffectsArray();
        currentEffect = GetNewEffectIndex();
        effects[currentEffect].RandomizeTime();

        effects[currentEffect].OnStart();

    }

    /// <summary>
    /// E1.31/ACN packet template used by the legacy UDP output path.
    /// Runtime fields such as packet lengths, sender UUID, sequence, universe,
    /// and payload size are patched before each universe is sent.
    /// </summary>
    byte[] acnheader = {
        // root layer
    0x00,0x10,     // (0-1) preamble size
    0x00,0x00,     // (2-4) post amble size
    0x41,0x53,0x43,0x2d,0x45,0x31,0x2e,0x31,0x37,0x00,0x00,0x00, // (4-15) ACN packet identifier
    0x00,0x00,     // (16-17)flags and length (to be filled in)     0x72,0x30,  560     +110
    0x00,0x00,0x00,0x04,    // (18-21) vector
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,  // (22-37) senders unique id (to be filled in)
    // framing layer
    0x00,0x00,     // (38-39) flags and length (to be filled in)     0x72,0x1a,   538       +88
    0x00,0x00,0x00,0x02,    // (40-43) vector
    0x31,0x39,0x32,0x2e,0x31,0x36,0x38,0x2e,0x31,0x2e,0x32,0x35,0x33,0x00,0x00,0x00,    // (44-107) source name
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,     // source name
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,     // source name
    0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,     // source name
    0x64,       // (108) priority
    0x00,0x00,  // (109-110) sync address
    0x00,       // (111) sequence number  (to be filled in)
    0x00,       // (112) options
    0x00,0x00,  // (113-114) universe number  (to be filled in)
    // DMP layer
    0x00,0x00,   // (115-116) flags and length  (to be filled in)   0x71,0xcd,  461  +11
    0x02,       // (117) vector
    0xa1,       // (118) address type and data type
    0x00,0x00,  // (119-120) first property address
    0x00,0x01,  // (121-122) address increment
    0x00,0x00,  // (123-124) payoad size (channel count+1) (to be filled in)  0x01,0xc3,    451 +1
    0x00,       // (125)  DMX slot 0
    };

    /// <summary>Sender UUID written into the ACN packet template during UDP setup.</summary>
    Guid g;

    /// <summary>Legacy UDP/E1.31 frame sequence byte, incremented after each frame.</summary>
    byte sequence = 0;



    /// <summary>
    /// Returns the IPv4 addresses visible on this host for display/debug output.
    /// </summary>
    public string GetLocalIPv4()
    {
        string addresses = "";
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                addresses += "\n" + ip.ToString();
            }
        }
        return addresses;
    }


    /// <summary>
    /// Prepares the legacy UDP/E1.31 output endpoint and fills the ACN sender UUID.
    /// </summary>
    private void setupUDP(string address)
    {

        remoteEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
        client = new UdpClient();
        // set up template
        g = Guid.NewGuid();
        byte[] uuid = g.ToByteArray();
        for (int i = 0; i < 16; i++)
            acnheader[i + 22] = uuid[i];
    }

    /// <summary>
    /// Sends one E1.31/ACN universe payload from the packed UDP frame buffer.
    /// </summary>
    private async Task sendACN(int universe, byte[] data, int start, int length)
    {
        int fullLength = acnheader.Length + length;

        byte[] sending = new byte[fullLength];
        // copy the header
        for (int i = 0; i < acnheader.Length; i++)
            sending[i] = acnheader[i];
        // patch some length values
        int length16 = 0x7000 + 110 + length;
        sending[16] = (byte)(length16 >> 8);
        sending[17] = (byte)(length16 & 255);

        int length38 = 0x7000 + 88 + length;
        sending[38] = (byte)(length38 >> 8);
        sending[39] = (byte)(length38 & 255);

        int length115 = 0x7000 + 11 + length;
        sending[115] = (byte)(length115 >> 8);
        sending[116] = (byte)(length115 & 255);

        int length123 = 1 + length;
        sending[123] = (byte)(length123 >> 8);
        sending[124] = (byte)(length123 & 255);
        // set the universe umber
        sending[113] = (byte)(universe >> 8);
        sending[114] = (byte)(universe & 255);
        // copy the data
        for (int i = 0; i < length; i++)
            sending[i + acnheader.Length] = data[i + start];

        await client.SendAsync(sending, fullLength, remoteEndPoint);
    }

    /// <summary>
    /// Observes a fire-and-forget E1.31 send: the render loop must not block on UDP, but a faulted
    /// send must surface as an error instead of dying as an unobserved task.
    /// </summary>
    private static void ObserveAcnSend(Task sendTask)
    {
        sendTask.ContinueWith(
            t => Debug.LogError($"E1.31 ACN send failed: {t.Exception?.GetBaseException().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Sends the current 900-tile frame to the local PREP_CAPTURE pixel feedback port in chunked RGB packets.
    /// </summary>
    private void SendPixelData(Color[] data, byte seq)
    {
        const ushort headerSize = 6;
        // 1464 is the largest multiple of 3 <= (1472 - 6)
        const ushort maxData = 1464;

        // Pre-allocate or use a class-level buffer
        byte[] packet = new byte[maxData + headerSize];

        int pixelsTotal = data.Length;
        int pixelsProcessed = 0;
        ushort byteOffset = 0;

        while (pixelsProcessed < pixelsTotal)
        {
            // How many pixels can we fit in this chunk?
            int pixelsRemaining = pixelsTotal - pixelsProcessed;
            int pixelsInThisChunk = Math.Min(pixelsRemaining, maxData / 3);

            ushort thisDataSize = (ushort)(pixelsInThisChunk * 3);
            ushort thisPacketSize = (ushort)(thisDataSize + headerSize);

            // Header
            packet[0] = (byte)(thisPacketSize >> 8);
            packet[1] = (byte)(thisPacketSize & 0xFF);
            packet[2] = 0x00; // Context: RGB
            packet[3] = seq;
            packet[4] = (byte)(byteOffset >> 8);
            packet[5] = (byte)(byteOffset & 0xFF);

            // Pack the pixels
            for (int i = 0; i < pixelsInThisChunk; i++)
            {
                Color32 c = data[pixelsProcessed + i];
                int writeIdx = headerSize + (i * 3);
                packet[writeIdx] = c.r;
                packet[writeIdx + 1] = c.g;
                packet[writeIdx + 2] = c.b;
            }

            client.Send(packet, thisPacketSize, "127.0.0.1", 7777);

            // Advance based on what we actually sent
            pixelsProcessed += pixelsInThisChunk;
            byteOffset += thisDataSize;
        }
    }
    /// <summary>
    /// Legacy output path: expands 900 logical tile colors through the 1800-entry wire map and sends E1.31/ACN UDP packets.
    /// </summary>
    private void sendUDPFrame(Color[] data)
    {
#if PREP_CAPTURE
        if(readPixel.timeout==0)
            SendPixelData(data, sequence);
#endif
        if (udpFrameBuffer == null) udpFrameBuffer = new byte[1800 * 3];
        int ptr2;
        int ptr1;
        // build uf the frame data
        ptr2 = 0;
        int[] wires = penrose.JsonRawData.wires;
        int size = wires.Length;
        byte level = brightness;
        if (!displayOn)
            level = 0;


        for (ptr1 = 0; ptr1 < size; ptr1++)
        {
            int ptr3 = wires[ptr1] / 2;
            udpFrameBuffer[ptr2++] = (byte)(data[ptr3].r * level);
            udpFrameBuffer[ptr2++] = (byte)(data[ptr3].b * level);
            udpFrameBuffer[ptr2++] = (byte)(data[ptr3].g * level);
        }
        // send the packets
        int universe = 1;
        for (ptr1 = 0; ptr1 < (5400 - 510); ptr1 += 510)
        {
            ObserveAcnSend(sendACN(universe++, udpFrameBuffer, ptr1, 510));
        }
        ObserveAcnSend(sendACN(universe, udpFrameBuffer, ptr1, 5400 - ptr1));
        acnheader[111] = sequence++;
    }

#if ENABLE_SERIAL
    /// <summary>
    /// Active hardware output path: expands 900 logical tile colors through the 1800-entry wire map and sends them over SerialOut.
    /// </summary>
    private void sendSerialFrame(Color[] data)
    {
        byte level = brightness;
        if (!displayOn) level = 0;

        // Map the 900 animation tiles to the 1800 physical LEDs
        int[] wires = penrose.JsonRawData.wires;
        if (serialOutputBuffer.Length != wires.Length)
        {
            serialOutputBuffer = new Color[wires.Length];
        }

        for (int i = 0; i < wires.Length; i++)
        {
            // Map physical LED 'i' to simulation tile 'wires[i]/2'
            serialOutputBuffer[i] = data[wires[i] / 2];
        }

        serial.send(serialOutputBuffer, level);
    }
#endif

    /// <summary>
    /// Builds and initializes the transition catalog. Transitions are activated later when the state machine enters transition playback.
    /// </summary>
    private void SetupTransitions()
    {
        var factory = new Factory<TransitionBase>();

        transitions = new TransitionBase[factory.Count];
        for (int i = 0; i < transitions.Length; i++)
        {
            transitions[i] = factory.Create(factory.Types[i]);
            transitions[i].BindController(this);
            transitions[i].Init();
        }
        transitionDeck = initDeck(transitions.Length);

        // Transitions are started only when the controller enters a real
        // effect-to-effect transition. Startup should only build the catalog.
        Debug.Log($"Transitions ({transitions.Length}):\n{FormatCatalog(factory.Names)}");
    }
    /// <summary>
    /// Builds the external-source blender catalog and runs each blender's one-time Init.
    /// </summary>
    private void SetupBlenders()
    {
        var factory = new Factory<BlenderBase>();

        blenders = new BlenderBase[factory.Count];
        for (int i = 0; i < blenders.Length; i++)
        {
            blenders[i] = factory.Create(factory.Types[i]);
            blenders[i].BindController(this);
            blenders[i].Init();
        }

        // Blenders have an Init contract (Controller/beatManager binding) but still no OnStart:
        // they are ready after Init, and transition startup remains transition-only.
        Debug.Log($"Blenders ({blenders.Length}):\n{FormatCatalog(factory.Names)}");

    }

    /// <summary>
    /// Cancels transition playback and immediately starts the effect at the requested catalog index.
    /// </summary>
    public void JumpToEffect(int i, float time)
    {
        if (i < 0) return;
        if (i >= effects.Length) return;

        if (director != null)
        {
            director.ShowNow(i, time);
            return;
        }

        // Startup/fallback path before Director/Switcher are initialized.
        EffectBase.APalette.Change();
        inTransition = false;
        currentEffect = i;
        effects[currentEffect].RandomizeTime();
        effects[currentEffect].OnStart();
        timer.Set(time);
        timer.Reset();
        effectText.text = effects[currentEffect].Name;
    }


    /// <summary>
    /// Resolves <see cref="heldEffect"/> to a playable catalog index, or reports that the wall is free to rotate.
    /// </summary>
    /// <param name="effectIndex">The held effect's index in <see cref="effects"/> when the method returns true.</param>
    /// <returns>
    /// <c>true</c> when an effect is held (<see cref="heldEffect"/> is a valid index into <see cref="effects"/>);
    /// <c>false</c> for the <c>-1</c> Random sentinel — and also defensively for an out-of-range or pre-setup value,
    /// so a stale/garbage index degrades to normal rotation instead of throwing.
    /// </returns>
    internal bool TryGetHeldEffectIndex(out int effectIndex)
    {
        effectIndex = -1;

        // -1 is the Random sentinel; anything outside the catalog is treated the same way (rotate, do not throw).
        if (heldEffect < 0 || effects == null || heldEffect >= effects.Length)
            return false;

        effectIndex = heldEffect;
        return true;
    }

    /// <summary>
    /// Per-frame hook (called from <see cref="Update"/>) that keeps the wall on the held effect while one is held.
    /// </summary>
    /// <remarks>
    /// No-op for the <c>-1</c> Random sentinel. When an effect is held but the wall is mid-transition or showing a
    /// different effect — e.g. because the held effect was just changed in the dropdown, or the deck had rotated
    /// elsewhere before the lock — this cancels any transition and enters the held effect immediately rather than
    /// waiting for the next timer tick.
    /// </remarks>
    private void ApplyHeldEffect()
    {
        if (director != null)
        {
            director.ApplyHold();
            return;
        }

        if (!TryGetHeldEffectIndex(out int heldEffectIndex))
            return;

        if (inTransition || currentEffect != heldEffectIndex)
        {
            JumpToEffect(heldEffectIndex, effectTime);
        }
    }
    /// <summary>
    /// Handles page-1 OSC controls for brightness, effect jumps, and runtime UI feedback.
    /// </summary>
    public void OSCpage1(OscMessage om, ArrayList oms)
    {
        if (om.address == "/1/vscroll1")       // brightness
        {
            brightness = (byte)Mathf.Lerp(255f, 0f, om.GetFloat(0));
        }
        if (om.address.StartsWith("/1/nav1"))
        {
            if (om.GetInt(0) == 1)
                NYE = !NYE;
        }
        if (om.address.StartsWith("/1/push"))
        {
            if (om.GetInt(0) == 1)
            {
                int button = int.Parse(om.address.Substring(7)) - 1;
                if (button == 23)
                {
                    keyboardBase = 1 - keyboardBase;
                }
                else
                {
                    int i = (button + keyboardBase);
                    JumpToEffect(i, effectTime);
                    oms.Add(makemessage(om.address, 1f));
                }
            }

        }
        if (om.address == "/1/hscroll1")       // period
        {
            float position = om.GetFloat(0);
            if (position == 1f) effectTime = 60 * 60;
            if (position < 0.87f) effectTime = 2 * 60;
            if (position < 0.62f) effectTime = 10;
            if (position < 0.37f) effectTime = 5;
            if (position < 0.12f) effectTime = 1;
        }

        int currentEffectIndex = switcher != null ? switcher.CurrentEffectIndex : currentEffect;
        if (currentEffectIndex >= effects.Length)
        {
            oms.Add(makemessage("/1/reset", 1f - (float)brightness / 255f));

        }
        if (om.address == "/ping")
        {
            oms.Add(makemessage("/1/vscroll1", 1f - (float)brightness / 255f));
            // update the current effect button
            // stream these one at a time for the button matrix
            if (currentEffectIndex >= 0)
            {
                osc.Send(makemessage("/1/push" + (pingIndex + 1), (pingIndex == currentEffectIndex) ? 1f : 0f));
                pingIndex++;
                pingIndex %= effects.Length;
            }
        }

    }


    /// <summary>
    /// Root OSC router called by OSCReader for every received message.
    /// </summary>
    public void OscHandler(OscMessage om)
    {
        if (om.address == "/beat")
        { }

        ArrayList oms = new ArrayList();        // make a list of replies
        OSCpage1(om, oms);
        if (useCamera)
            cameraOverlay.OSCHandler(om, oms);
        drum.OSCHandler(om, oms);
        OSCtext = om.ToString();
        OSCtimer = 20;
        if (oms.Count > 0)                      // send any replies
            osc.Send(oms);
    }
    /// <summary>
    /// Creates a single-float OSC message for replies to the control surface.
    /// </summary>
    public OscMessage makemessage(string address, float value)
    {
        OscMessage message = new OscMessage();
        message.address = address;
        message.values.Add(value);
        return message;
    }

    /// <summary>
    /// Applies a new legacy UDP/E1.31 destination address, reporting parse/setup failures to the Unity log.
    /// </summary>
    private void setIP(string address)
    {
        try
        {
            setupUDP(address);

        }
        catch (Exception e)
        {

            Debug.Log($"Failed to setup UDP: {e.Message}");
        }
    }

    /// <summary>Updates the scheduled display-on minute from the UI field.</summary>
    private void onTimeEndEditCallback(string input) { onMinute = int.Parse(input); }
    /// <summary>Updates the scheduled display-off minute from the UI field.</summary>
    private void offTimeEndEditCallback(string input) { offMinute = int.Parse(input); }
    /// <summary>Updates the legacy UDP/E1.31 destination IP from the UI field.</summary>
    private void destIPEndEditCallback(string input)
    {
        IP = input;
        setIP(IP);
    }
    /// <summary>Updates the master display gate from the UI toggle.</summary>
    private void displayOnChange(bool isOn) { displayOn = isOn; }

    /// <summary>
    /// Sends periodic OSC state updates back to the control surface.
    /// </summary>
    public void OSCping()
    {
        ArrayList oms = new ArrayList();        // make a list of replies
        OscMessage om = makemessage("/ping", 0);
        OSCpage1(om, oms);
        if (useCamera)
            cameraOverlay.OSCHandler(om, oms);
        drum.OSCHandler(om, oms);
        if (oms.Count > 0)                      // send any replies
            osc.Send(oms);
    }

    /// <summary>
    /// Parses the small JSON-like response used by the PREP_CAPTURE wall status endpoint.
    /// </summary>
    void parseResponce(string data)
    {
        String vname = "";
        int mode = 0;
        int value = 0;
        for (var i = 0; i < data.Length; i++)
        {
            char c = data[i];
            switch (c)
            {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    value *= 10;
                    value += c - '0';
                    break;
                case '"':
                    if (mode == 0)
                    {
                        vname = "";
                        mode = 1;
                    }
                    else
                    {
                        mode = 0;
                        value = 0;
                    }
                    break;
                default:
                    if (mode == 1)
                        vname += c;
                    break;
                case '}':
                case ',':
                    if (vname == "FilterTimer") FilterTimer = value;
                    if (vname == "FilterMode") FilterMode = (value != 0) ? true : false;
                    if (vname == "displayOn") displayOn = (value != 0) ? true : false;
                    if (vname == "brightness") brightness = (byte)value;
                    if (vname == "onMinute") onMinute = value;
                    if (vname == "offMinute") offMinute = value;
                    break;
            }
        }
    }
    /// <summary>
    /// Posts PREP_CAPTURE wall state to the remote status endpoint and applies the returned state.
    /// </summary>
    IEnumerator PostRequest(string url, string json)
    {
        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            //            Debug.Log(request.error);
        }
        else
        {
            parseResponce(request.downloadHandler.text);
        }
    }

    /// <summary>
    /// Updates display/filter timers and, when PREP_CAPTURE is enabled, synchronizes wall state with the remote status endpoint.
    /// </summary>
    private void checkTime()
    {
        secondsAccululator += Time.deltaTime;
        FilterTimer -= Time.deltaTime;

#if PREP_CAPTURE
        if (secondsAccululator > 1f)
        {
            secondsAccululator %= 1f;
            if (FilterTimer < 0f)
                FilterTimer = 0f;
            FilterMode = (FilterTimer > 0);

            System.DateTime currentTime = System.DateTime.Now;
            int minute = (currentTime.Hour * 100) + currentTime.Minute;
            if (minute == onMinute)
            {
                displayOn = true;
                onToggle.isOn = displayOn;
            }
            if (minute == offMinute)
            {
                displayOn = false;
                onToggle.isOn = displayOn;
            }

            string url = "http://chucks.cust.he.net/wall/fromwall.php";
            string json = "{";
            json += "\"FilterTimer\":\"" + ((int)FilterTimer) + "\",";
            json += "\"FilterMode\":\"" + FilterMode + "\",";
            json += "\"displayOn\":\"" + displayOn + "\",";
            json += "\"brightness\":\"" + brightness + "\",";
            json += "\"onMinute\":\"" + onMinute + "\",";
            json += "\"offMinute\":\"" + offMinute + "\"";
            json += "}";
            StartCoroutine(PostRequest(url, json));
        }
#endif
    }

    private void ConfigureRuntimeHud()
    {
        if (!configureCompactHudLayout)
        {
            return;
        }

        ConfigureHudText(effectText, true);
        ConfigureHudText(debugText, false);
    }

    private void ConfigureHudText(TextMeshProUGUI text, bool topLine)
    {
        if (text == null)
        {
            return;
        }

        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.textWrappingMode = topLine ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.fontSize = topLine ? 34f : 30f;
        text.alignment = topLine ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.BottomLeft;
        text.color = WithAlpha(Color.white, runtimeHudAlpha);

        var rect = text.rectTransform;
        if (topLine)
        {
            rect.anchorMin = new Vector2(0.15f, 1f);
            rect.anchorMax = new Vector2(0.85f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -72f);
            rect.offsetMax = new Vector2(0f, -12f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(24f, 18f);
            rect.offsetMax = new Vector2(-24f, 150f);
        }
    }

    private void UpdateRuntimeHud(string renderDebugText, string transientMessage = null)
    {
        lastRenderDebugText = renderDebugText ?? string.Empty;

        if (!showRuntimeHud)
        {
            lastRuntimeHudLine = string.Empty;
            lastRuntimeDetailLine = string.Empty;
            if (effectText != null) effectText.text = string.Empty;
            if (debugText != null) debugText.text = string.Empty;
            return;
        }

        var directorStatus = DirectorStatus;
        var switcherStatus = SwitcherStatus;
        lastRuntimeHudLine = BuildRuntimeHudLine(directorStatus, switcherStatus);
        lastRuntimeDetailLine = BuildRuntimeDetailLine(directorStatus, switcherStatus, transientMessage);

        if (effectText != null)
        {
            effectText.color = WithAlpha(Color.white, runtimeHudAlpha);
            effectText.text = lastRuntimeHudLine;
        }

        if (debugText != null)
        {
            debugText.color = WithAlpha(Color.white, Mathf.Clamp01(runtimeHudAlpha * 0.82f));
            debugText.text = showVerboseRuntimeHud
                ? BuildVerboseRuntimeDetail(lastRuntimeDetailLine, lastRenderDebugText)
                : lastRuntimeDetailLine;
        }
    }

    private string BuildRuntimeHudLine(DirectorStatus directorStatus, SwitcherStatus switcherStatus)
    {
        var stage = FormatStageName(switcherStatus);
        if (directorStatus.Mode == DirectorMode.Hold)
        {
            return $"{stage} · HOLD";
        }

        if (directorStatus.Mode == DirectorMode.Standalone)
        {
            return $"{stage} · STANDALONE";
        }

        if (directorStatus.Mode == DirectorMode.Synced)
        {
            return $"{stage} · SYNC";
        }

        return "Starting Director…";
    }

    private string BuildRuntimeDetailLine(DirectorStatus directorStatus, SwitcherStatus switcherStatus, string transientMessage)
    {
        if (!string.IsNullOrWhiteSpace(transientMessage))
        {
            return SanitizeHudLine(transientMessage);
        }

        var builder = new StringBuilder();
        AppendIfNotEmpty(builder, FormatNextMove(directorStatus));
        if (directorStatus.IsSyncedMode)
        {
            AppendIfNotEmpty(builder, FormatGridState());
            AppendIfNotEmpty(builder, FormatTimingSource(directorStatus));
            AppendIfNotEmpty(builder, FormatGridPosition());
            AppendIfNotEmpty(builder, FormatLanding(directorStatus));
            AppendIfNotEmpty(builder, FormatDropCue());
            AppendIfNotEmpty(builder, directorStatus.BeatsUntilCadenceReady > 0
                ? $"cadence +{directorStatus.BeatsUntilCadenceReady}b"
                : "cadence ready");
        }
        else if (directorStatus.Mode == DirectorMode.Standalone)
        {
            AppendIfNotEmpty(builder, FormatDecision(directorStatus.Decision));
        }
        else if (directorStatus.Mode == DirectorMode.Hold)
        {
            AppendIfNotEmpty(builder, "Director suspended");
        }

        if (switcherStatus.Ready && switcherStatus.CurrentEffectIndex < 0)
        {
            AppendIfNotEmpty(builder, $"transition {Mathf.RoundToInt(Mathf.Clamp01(switcherStatus.TransitionProgress) * 100f)}%");
        }

        AppendIfNotEmpty(builder, $"FPS {fps:0}");
        AppendIfNotEmpty(builder, $"KB{keyboardBase}");
        return builder.ToString();
    }

    private string BuildVerboseRuntimeDetail(string detailLine, string renderDebugText)
    {
        var builder = new StringBuilder(detailLine ?? string.Empty);
        AppendIfNotEmpty(builder, SanitizeHudLine(renderDebugText));
        return builder.ToString();
    }

    private string FormatStageName(SwitcherStatus status)
    {
        if (!status.Ready)
        {
            return "Starting";
        }

        if (status.CurrentEffectIndex < 0)
        {
            return $"{status.SourceEffectName} → {status.TargetEffectName} {Mathf.RoundToInt(Mathf.Clamp01(status.TransitionProgress) * 100f)}%";
        }

        return status.StageName;
    }

    private static string FormatNextMove(DirectorStatus status)
    {
        if (status.NextEffectIndex < 0 && status.NextTransitionIndex < 0)
        {
            return string.Empty;
        }

        var nextEffect = status.NextEffectIndex >= 0 ? status.NextEffectName : "—";
        var nextTransition = status.NextTransitionIndex >= 0 ? status.NextTransitionName : "—";
        return $"next {nextEffect} via {nextTransition}";
    }

    private static string FormatTimingSource(DirectorStatus status)
    {
        switch (status.TimingSource)
        {
            case TimingFrameSource.CueMark:
                return "cue-mark";
            case TimingFrameSource.TrackPhaseBoundary:
                return "track-phase-boundary";
            default:
                return "unlocked";
        }
    }

    /// <summary>Wire-fed Grid state for the HUD, annotated when the current Phrase is irregular.</summary>
    private string FormatGridState()
    {
        if (!(beatManager != null && beatManager.Grid is { } grid))
        {
            return "unlocked";
        }

        return beatManager.Phrase?.irregular == true
            ? $"{grid.State} (irregular)"
            : grid.State.ToString();
    }

    private string FormatGridPosition()
    {
        return beatManager != null && beatManager.Grid is { } grid ? $"{grid.Beat}/16" : "no grid";
    }

    private static string FormatLanding(DirectorStatus status)
    {
        return status.BeatsUntilLanding >= 0 ? $"landing +{status.BeatsUntilLanding}b" : FormatDecision(status.Decision);
    }

    private static string FormatDecision(DirectorDecision decision)
    {
        switch (decision)
        {
            case DirectorDecision.StandaloneTimer:
                return "timer";
            case DirectorDecision.WaitingForGrid:
                return "waiting grid";
            case DirectorDecision.WaitingForRunway:
                return "waiting runway";
            case DirectorDecision.WaitingForCadence:
                return "waiting cadence";
            case DirectorDecision.CueingTransition:
                return "cueing";
            case DirectorDecision.Hold:
                return "hold";
            default:
                return "not ready";
        }
    }

    private string FormatDropCue()
    {
        var drop = beatManager.Drop;
        if (drop is { inProgress: true })
        {
            return "Drop now";
        }

        if (drop is { beatsUntilStart: { } beatsUntilStart })
        {
            return $"Drop +{beatsUntilStart}b";
        }

        return string.Empty;
    }

    private static string SanitizeHudLine(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static void AppendIfNotEmpty(StringBuilder builder, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(" · ");
        }

        builder.Append(value);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    /// <summary>Writes a tagged sequencing diagnostic line to the Unity log when enabled.</summary>
    public void LogDirectorSwitching(string message)
    {
        if (!logDirectorSwitching)
        {
            return;
        }

        Debug.Log($"[DEBUG-DIR16] frame={Time.frameCount} t={Time.time:0.000} {message}");
    }

    /// <summary>
    /// Unity startup hook. Initializes the scene model, runtime catalogs, input systems, overlays, timer, and active hardware output.
    /// </summary>
    void Start()
    {
        // Unity/application setup.
        Application.targetFrameRate = 60;
        OSCtimer = 0;

        // The Penrose scene object owns JSON-derived geometry, tile metadata,
        // bounds, mesh generation, and the preview color buffer. Effects must
        // not be initialized until this completes because EffectBase.Init()
        // caches penrose.Tiles.
        penrose = FindAnyObjectByType<Penrose>();
        penrose.Init(jsonSource);
        EffectBase.LoadPalette(paletteSource);

        // Seed the on-screen configuration controls from serialized fields.
        myIPText.text = GetLocalIPv4();
        onTime.text = onMinute.ToString();
        offTime.text = offMinute.ToString();
        destIP.text = IP;
        onToggle.isOn = displayOn;

        // UI callbacks update the serialized/runtime fields directly. The IP
        // callback also rebuilds the legacy UDP endpoint.
        onTime.onEndEdit.AddListener(onTimeEndEditCallback);
        offTime.onEndEdit.AddListener(offTimeEndEditCallback);
        destIP.onEndEdit.AddListener(destIPEndEditCallback);
        onToggle.onValueChanged.AddListener(displayOnChange);

        // Build runtime catalogs. These are plain C# objects, not scene objects.
        // Effects are also started here because the first frame needs an active
        // currentEffect before the timer state machine begins.
        SetupEffects();
        SetupTransitions();
        SetupBlenders();
        setIP(IP);

        // Input/overlay helpers. OSCReader is a MonoBehaviour added to this
        // GameObject; drums and PixelReceiver are plain C# objects with their
        // own UDP listeners.
        osc = gameObject.AddComponent(typeof(OSCReader)) as OSCReader;
        osc.SetAllMessageHandler(OscHandler);
        raveOsc = gameObject.AddComponent<RaveOscReceiver>();
        drum = new drums();
        drum.RandomizeTime();
        drum.Init(this);
        readPixel = new PixelReceiver();
        readPixel.Init();

        // Optional camera overlay. It depends on Penrose bounds and writes into
        // the same 900-tile buffer as the main effect pipeline.
        if (useCamera)
        {
            cameraOverlay = new CameraReader();
            cameraOverlay.RandomizeTime();
            cameraOverlay.Init((int)penrose.Bounds.size.x, (int)penrose.Bounds.size.y, Penrose.Total);
        }

        // Director owns sequencing cadence; Switcher owns only the mechanical in-flight stage state.
        timer = new Timer(effectTime, false);
        switcher = new Switcher(this, effects, transitions);
        switcher.SetInitialEffect(currentEffect, currentTransition);
        director = new Director(this, switcher, timer, effectDeck, transitionDeck, currentTransition);
        timer.onFinished += director.OnTimerFinished;

        ConfigureRuntimeHud();
        UpdateRuntimeHud(string.Empty);
        StartCoroutine(Fps());
#if ENABLE_TELNET
        // Optional debug command server. Disabled in normal builds.
        server = new TelnetServer { controller = this };
        server.Start();     // start telnet server
#endif
#if ENABLE_SERIAL
        // Active hardware output path for desktop controller builds.
        serial = new SerialOut();
        // 2,000,000 baud is required for 900 pixels @ 60fps (~1.6Mbps raw data)
        serial.Init(2000000);
        Debug.Log("[Controller] Serial Output Enabled.");
#endif

    }


    /// <summary>
    /// Picks the next effect: the held effect when one is held, otherwise the next card off the rotating deck.
    /// </summary>
    private int GetNewEffectIndex()
    {
        // A held effect (heldEffect >= 0) always wins; the -1 Random sentinel falls through to deck rotation,
        // which is the default behavior.
        if (TryGetHeldEffectIndex(out int heldEffectIndex))
            return heldEffectIndex;

        return Deck.PullRandom(effectDeck, _ => true, (minInclusive, maxExclusive) => Random.Range(minInclusive, maxExclusive));
    }

    /// <summary>
    /// Applies the optional hue-clamping display filter in-place to the current frame buffer.
    /// </summary>
    void applyFilter(Color[] buffer)
    {
        Color32 c = new Color32(0x5a, 0x2d, 0x81, 255);
        Color.RGBToHSV(c, out float khue, out float ksat, out float kbri);
        for (int i = 0; i < buffer.Length; i++)
        {
            Color.RGBToHSV(buffer[i], out float hue, out float sat, out float bri);
            /*
            if(bri>0.1f)
            {
                if (bri < 0.5f)
                    bri = 0.5f;
            }
            */
            hue = khue + (hue * FilterScale * 2) - FilterScale;
            buffer[i] = Color.HSVToRGB(hue % 1f, sat, bri);
        }

    }

#if PREP_CAPTURE
    /// <summary>
    /// Generates a PREP_CAPTURE dummy external source pattern for blender testing.
    /// </summary>
    void makeDummySignal(Color[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            Penrose.TileData t = penrose.Tiles[i];
            float y = t.center.y / 30f;
            y += diagnosticTime;
            y %= 1f;
            {
                Color c = y > 0.5f ? Color.magenta : Color.black;
                buffer[i] = c;
            }
        }
    }
#endif

    /// <summary>
    /// Unity frame loop. Advances timing, handles input, draws effects/transitions, applies overlays/blending, outputs hardware frames, and updates the preview mesh.
    /// </summary>
    void Update()
    {
        // 1. Advance clocks and service optional command systems.
        checkTime();
        effectDelta = Time.deltaTime;
#if PREP_CAPTURE
        diagnosticTime += effectDelta;
#endif
        if (raveOsc != null)
            raveOsc.ApplyTo(beatManager);
        beatManager.Update();

        // Director reads the freshly-applied beat state: live OSC drives Synced Mode,
        // while no live source leaves Standalone Mode on its independent timer path.
        director.Tick(effectDelta);
#if ENABLE_TELNET
        server.Service();                   // service pending telnet commands
#endif
        // 2. Palette controls. Return reloads palette definitions; Update()
        // advances the shared animated palette used by most effects.
        if (Input.GetKeyDown(KeyCode.Return))
        {
            EffectBase.LoadPalette(paletteSource); // reload the palettes
        }
        EffectBase.APalette.Update();

        // 3. Local keyboard/debug input. Escape releases any held effect back to
        // Random; A-W jump directly to effect indexes; X switches the keyboard bank.
        // Escape is the quick "let it rotate again" key: it sets heldEffect back to
        // the -1 Random sentinel, which the inspector dropdown mirrors as row 0.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            heldEffect = -1;
            Debug.Log("[Controller] Effect lock released — back to Random (deck rotation).");
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            int target = switcher.Status.TargetEffectIndex;
            JumpToEffect(target, effectTime);
            Debug.Log($"[Controller] Restarted effect: {SwitcherStatus.StageName}");
        }

        if (Input.anyKey)
        {
            for (KeyCode k = KeyCode.A; k < KeyCode.X; k++)
            {
                if (Input.GetKeyDown(k))
                {
                    int button = k - KeyCode.A;
                    int i = (button + (keyboardBase * 23));
                    JumpToEffect(i, effectTime);
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.X)) keyboardBase = 1 - keyboardBase;       // toggle base

        ApplyHeldEffect();

        // 4. Drum test keys and overlay update.
        // test drums
        if (Input.GetKeyDown("1")) drum.hit(0, 1f);
        if (Input.GetKeyDown("2")) drum.hit(1, 1f);
        if (Input.GetKeyDown("3")) drum.hit(2, 1f);
        if (Input.GetKeyDown("4")) drum.hit(3, 1f);
        if (Input.GetKeyDown("5")) drum.hit(4, 1f);
        if (Input.GetKeyDown("6")) drum.ring(1, 1f);
        if (Input.GetKeyDown("7")) drum.ring(2, 1f);
        if (Input.GetKeyDown("8")) drum.ring(3, 1f);
        if (Input.GetKeyDown("9")) drum.ring(4, 1f);
        if (Input.GetKeyDown("0")) drum.ring(5, 1f);
        drum.Update();

        // 5. Main visual generation. Either draw the special NYE overlay, the
        // active transition, or the active effect into penrose.buffer.
        string renderDebugText;
        if (NYE)
        {
            Color[] buffer = penrose.buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (Random.Range(0, 16) == 0) ? Color.white : Color.black;
            }

            renderDebugText = "NYE overlay";
        }
        else
        {
            penrose.buffer = switcher.RenderAtTime(Time.time, out renderDebugText);
            if (FilterMode)
                applyFilter(penrose.buffer);
            drum.Draw(penrose.buffer);
        }

        // 6. Optional camera overlay modifies the already-rendered Penrose buffer.
        if (useCamera)
        {
            cameraOverlay.UpdateTime();
            cameraOverlay.Draw(penrose.buffer);
        }

        // 7. Transient debug notices can temporarily occupy the compact bottom HUD.
        string transientHudMessage = null;
        if (OSCtimer > 0)
        {
            transientHudMessage = OSCtext;
            OSCtimer--;
        }

        // 8. External pixel source. PREP_CAPTURE can synthesize a dummy source;
        // otherwise PixelReceiver supplies incoming UDP RGB frames. If no
        // blender is active, external pixels replace the native buffer.
        bool doblend = false;
#if PREP_CAPTURE
        if (dummyActive)
        {
            makeDummySignal(blendBuffer);
            transientHudMessage = "dummy source";
            doblend = true;
        }
        else 
#endif
        if (readPixel.Update())
        {
            blendBuffer = (Color[])readPixel.buffer.Clone();
            transientHudMessage = "Pixel source";
            doblend = true;
        }

        if (doblend)
        {
            if (ActiveBlender != null)
            {
                ActiveBlender.Blend(penrose.buffer, penrose.buffer, blendBuffer);
            }
            else if (ActiveTransitionBlender != null)
            {
                // Transitions can also be selected as external-source blenders.
                // Keep their time moving so Blend() implementations that use
                // effectTime, such as NoiseTransition, behave like Draw().
                ActiveTransitionBlender.UpdateTime();
                ActiveTransitionBlender.Blend(penrose.buffer, penrose.buffer, blendBuffer);
            }
            else
                penrose.buffer = (Color[])blendBuffer.Clone();
        }

        // 9. Hardware output. Serial is the active compiled path when
        // ENABLE_SERIAL is defined; otherwise this falls back to legacy UDP/E1.31.
#if ENABLE_SERIAL
        sendSerialFrame(penrose.buffer);
#else
        sendUDPFrame(penrose.buffer);
#endif

        // 10. Local Unity preview and outbound OSC control-surface state.
        penrose.UpdateModelColors();
        OSCping();

#if ENABLE_SERIAL
        if (serial != null) renderDebugText += serial.GetDebugInfo();
#endif
        UpdateRuntimeHud(renderDebugText, transientHudMessage);
    }
}
