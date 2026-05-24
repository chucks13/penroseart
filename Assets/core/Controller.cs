
#define ENABLE_SERIAL

using System;
using System.Collections;
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
    public BlenderBase[] blenders;

    /// <summary>
    /// Optional active external-source blender selected by telnet/debug paths.
    /// When set, incoming PixelReceiver data is blended with the native buffer.
    /// </summary>
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

    [Header("Nova Testing Technique")]
    [Tooltip("If true, immediately stops transitions and locks playback to the named effect below.")]
    public bool forceEffect = false;

    [Tooltip("If not empty, matches the effect name substring used by the live force override.")]
    public string forceEffectName = "";

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

    /// <summary>Seconds for each effect-to-effect transition.</summary>
    public float transitionTime = 2f;

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

    /// <summary>UI label showing effect debug text, OSC text, FPS, and serial state.</summary>
    public TextMeshProUGUI debugText;

    /// <summary>UI label listing local IPv4 addresses.</summary>
    public TextMeshProUGUI myIPText;
    //  public TextMeshProUGUI myBrightnessText;

    /// <summary>Runtime catalog of top-level effects created by <see cref="SetupEffects"/>.</summary>
    [HideInInspector]
    public EffectBase[] effects;

    /// <summary>Runtime catalog of transitions created by <see cref="SetupTransitions"/>.</summary>
    [HideInInspector]
    public TransitionBase[] transitions;

    /// <summary>Scene Penrose model and preview mesh component.</summary>
    [HideInInspector]
    public Penrose penrose;

    /// <summary>Effect/transition phase timer. Its finish event drives <see cref="OnTimerFinished"/>.</summary>
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
    /// Draws a random entry from the top half of a deck, shifts the deck up,
    /// and moves the drawn card to the bottom to reduce immediate repeats.
    /// </summary>
    private int pullCard(int[] deck)
    {
        int length = deck.Length;
        int idx = Random.Range(0, length / 2);
        int result = deck[idx];
        for (int i = idx; i < length - 1; i++)
            deck[i] = deck[i + 1];
        deck[length - 1] = result;
        return result;
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
            sendACN(universe++, udpFrameBuffer, ptr1, 510);
        }
        sendACN(universe, udpFrameBuffer, ptr1, 5400 - ptr1);
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
            transitions[i].Init();
        }
        transitionDeck = initDeck(transitions.Length);

        // Transitions are started only when the controller enters a real
        // effect-to-effect transition. Startup should only build the catalog.
        Debug.Log($"Transitions ({transitions.Length}):\n{FormatCatalog(factory.Names)}");
    }
    /// <summary>
    /// Builds the external-source blender catalog. Blenders have no Init or OnStart lifecycle hook.
    /// </summary>
    private void SetupBlenders()
    {
        var factory = new Factory<BlenderBase>();

        blenders = new BlenderBase[factory.Count];
        for (int i = 0; i < blenders.Length; i++)
        {
            blenders[i] = factory.Create(factory.Types[i]);
        }

        // BlenderBase has no Init/OnStart contract. Concrete blenders are
        // ready after construction; transition startup remains transition-only.
        Debug.Log($"Blenders ({blenders.Length}):\n{FormatCatalog(factory.Names)}");

    }

    /// <summary>
    /// Cancels transition playback and immediately starts the effect at the requested catalog index.
    /// </summary>
    public void JumpToEffect(int i, float time)
    {
        if (i < 0) return;
        if (i >= effects.Length) return;
        EffectBase.APalette.Change();
        //select the new effect
        inTransition = false;
        currentEffect = i;
        effects[currentEffect].RandomizeTime();
        effects[currentEffect].OnStart();
        timer.Set(time);
        timer.Reset();
        effectText.text = effects[currentEffect].Name;
        // turn on the button
    }

    /// <summary>
    /// Resolves the live force-effect override to an effect catalog index by case-insensitive substring match.
    /// </summary>
    private bool TryGetForcedEffectIndex(out int effectIndex)
    {
        effectIndex = -1;
        if (!forceEffect || string.IsNullOrWhiteSpace(forceEffectName) || effects == null)
            return false;

        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i].Name.IndexOf(forceEffectName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                effectIndex = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Applies the live force-effect override during Update, cancelling transitions when needed.
    /// </summary>
    private void ApplyForceEffectOverride()
    {
        if (!forceEffect)
            return;

        if (!TryGetForcedEffectIndex(out int forcedEffectIndex))
            return;

        if (inTransition || currentEffect != forcedEffectIndex)
        {
            // Inspector/keyboard force is a live override: cancel transitions and
            // enter the requested effect immediately instead of waiting for the deck.
            JumpToEffect(forcedEffectIndex, effectTime);
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

        if (currentEffect >= effects.Length)
        {
            oms.Add(makemessage("/1/reset", 1f - (float)brightness / 255f));

        }
        if (om.address == "/ping")
        {
            oms.Add(makemessage("/1/vscroll1", 1f - (float)brightness / 255f));
            // update the current effect button
            // stream these one at a time for the button matrix
            if (currentEffect >= 0)
            {
                osc.Send(makemessage("/1/push" + (pingIndex + 1), (pingIndex == currentEffect) ? 1f : 0f));
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

        if (request.isNetworkError || request.isHttpError)
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
        penrose = GameObject.FindObjectOfType<Penrose>();
        penrose.Init();

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
        drum.Init();
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

        // Timer drives effect/transition phase changes. Update() advances it;
        // OnTimerFinished() is the state-machine callback.
        timer = new Timer(effectTime, false);
        timer.onFinished += OnTimerFinished;

        effectText.text = effects[currentEffect].GetType().ToString();
        StartCoroutine(Fps());
#if ENABLE_TELNET
        // Optional debug command server. Disabled in normal builds.
        server = new TelnetServer();
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
    /// Selects the next effect index, using forceEffectName when active or the rotating effect deck otherwise.
    /// </summary>
    private int GetNewEffectIndex()
    {
        // Keep random deck selection as the default, but let the live testing
        // override choose future targets while forceEffect remains enabled.
        if (TryGetForcedEffectIndex(out int forcedEffectIndex))
            return forcedEffectIndex;

        return pullCard(effectDeck);
    }

    /// <summary>
    /// Timer state-machine callback. Alternates between effect playback and transition playback unless forceEffect is active.
    /// </summary>
    private void OnTimerFinished()
    {
        // Force mode owns the state machine. If an operator/dev has requested a
        // specific effect, timer expiry should not transition away from it.
        if (TryGetForcedEffectIndex(out int forcedEffectIndex))
        {
            if (inTransition || currentEffect != forcedEffectIndex)
                JumpToEffect(forcedEffectIndex, effectTime);
            else
                timer.Reset();

            return;
        }

        if (inTransition)
        {
            // Transition phase finished: promote the transition target to the
            // active effect, return to normal effect playback, and draw the next
            // transition card for the following phase change.
            inTransition = !inTransition;
            currentEffect = transitions[currentTransition].B;
            timer.Set(effectTime);
            timer.Reset();
            effectText.text = effects[currentEffect].Name;
            currentTransition = pullCard(transitionDeck);
            //           if (randomTransition)
            //               currentTransition = Random.Range(0, transitions.Length);
            return;
        }

        // Effect phase finished: configure the selected transition to blend from
        // the current effect to the next effect, start the destination effect so
        // it can render during the transition, then switch Update() into
        // transition drawing mode.
        inTransition = !inTransition;

        TransitionBase transition = transitions[currentTransition];
        transition.RandomizeTime();
        transition.V = 0f;
        transition.B = GetNewEffectIndex();
        transition.A = currentEffect;
        transition.OnStart();
        EffectBase.APalette.Change();

        effects[transition.B].RandomizeTime();
        effects[transition.B].OnStart();

        timer.Set(transitionTime);
        timer.Reset();

        currentEffect = -1;

        effectText.text = transition.Name;
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
        timer.Update(effectDelta);
#if ENABLE_TELNET
        server.Service();                   // service pending telnet commands
#endif
        // 2. Palette controls. Return reloads palette definitions; Update()
        // advances the shared animated palette used by most effects.
        if (Input.GetKeyDown(KeyCode.Return))
        {
            EffectBase.APalette = new AnimPalette(); // reload the palettes
        }
        EffectBase.APalette.Update();

        // 3. Local keyboard/debug input. Escape toggles live force mode; A-W
        // jump directly to effect indexes; X switches the keyboard bank.
        // Nova Technique: Escape key toggles the testing override on/off
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            forceEffect = !forceEffect;
            Debug.Log($"[Nova] Testing override: {forceEffect}");
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

        ApplyForceEffectOverride();

        // 4. Drum test keys and global rhythm update.
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
        beatManager.Update();
        if (raveOsc != null)
            raveOsc.ApplyTo(beatManager);
        // 5. Main visual generation. Either draw the special NYE overlay, the
        // active transition, or the active effect into penrose.buffer.
        if (NYE)
        {
            Color[] buffer = penrose.buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (Random.Range(0, 16) == 0) ? Color.white : Color.black;
            }
        }
        else
        {
            if (inTransition)
            {
                // Transition mode: update transition progress, advance both
                // participating effects, and let the transition fill its buffer.
                transitions[currentTransition].V = timer.Value;
                transitions[currentTransition].UpdateTime();

                int indexA = transitions[currentTransition].A;
                int indexB = transitions[currentTransition].B;

                effects[indexA].UpdateTime();
                if (indexA != indexB)
                    effects[indexB].UpdateTime();

                transitions[currentTransition].Draw();
                penrose.buffer = (Color[])transitions[currentTransition].buffer.Clone();

                debugText.text = transitions[currentTransition].DebugText();

            }
            else
            {
                // Effect mode: advance and draw the single active effect.
                effects[currentEffect].UpdateTime();
                effects[currentEffect].Draw();
                penrose.buffer = (Color[])effects[currentEffect].buffer.Clone();

                debugText.text = effects[currentEffect].DebugText();
            }
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

        // 7. Debug text: normal effect/transition debug plus FPS/keyboard bank,
        // temporarily replaced by recent OSC text when OSC traffic arrives.
        debugText.text += $"\nFPS: {fps},KB{keyboardBase}";
        if (OSCtimer > 0)
        {
#if ENABLE_SERIAL
            // Clear serial debug info if OSC text is active to prevent clutter
            if (serial != null) debugText.text = debugText.text.Replace(serial.GetDebugInfo(), "");
#endif
            debugText.text = OSCtext;
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
            debugText.text = "dummy source";
            doblend = true;
        }
        else 
#endif
        if (readPixel.Update())
        {
            blendBuffer = (Color[])readPixel.buffer.Clone();
            debugText.text = "Pixel source";
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
        if (serial != null) debugText.text += serial.GetDebugInfo();
#endif
    }
}
