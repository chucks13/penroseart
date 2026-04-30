#define PREP_CAPTURE 


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
using System.Drawing;

// git connection test 4/29/2026

public class Controller : Singleton<Controller>
{

    public int[] effectDeck;
    public int[] transitionDeck;
    public BlenderBase[] blenders;

    public BlenderBase ActiveBlender = null;
    public TransitionBase ActiveTransitionBlender = null;
    public TelnetServer server;

    public Color[] blendBuffer = new Color[Penrose.Total];

    public bool NYE = false;

    public bool dummyActive = false;
    private float secondsAccululator = 0f;
    public float FilterScale = 0.03f;
    public float FilterTimer = 0f;
    public float onMinute = 1700;
    public float offMinute = 200;
    public InputField destIP;
    public InputField onTime;
    public InputField offTime;
    public Toggle onToggle;
    public bool FilterMode = false;
    public bool displayOn = true;
    [Header("UDP")]
    public string IP;
    public byte brightness;
    int port = 5568;      // default e131 port
    private static int localPort;
    IPEndPoint remoteEndPoint;
    UdpClient client;
    public CameraReader cameraOverlay;
    public bool useCamera;

    [Header("Effect Switching")]
    public int startEffect;

    private int currentEffect;
    private int keyboardBase = 0;

    public float effectTime = 10f;

    [Header("Transition Switching")]
    public bool randomTransition = true;
    public int currentTransition;

    public float transitionTime = 2f;

    [Header("Settings")]
    public Noise.Settings[] noiseSettings;


    public TileShapes.Settings[] tileShapesSettings;
    public Angles.Settings[] anglesSettings;
    public ColorSparkle.Settings[] sparkleSettings;
    public Nibbler.Settings[] nibblerSettings;
    public Panels.Settings[] panelsSettings;
    public Pulse.Settings[] pulseSettings;
    public Ripple.Settings[] rippleSettings;
    public NoiseTunnel.Settings[] noiseTunnelSettings;
    public RainbowBars.Settings[] rainbowBarsSettings;
    public Waterfall.Settings[] waterfallSettings;
    public Julia.Settings[] juliaSettings;
    public Flock.Settings[] flockSettings;
    public MetaBalls.Settings[] metaBallsSettings;
    public drums.Settings[] drumsSettings;
    public Tunnel.Settings[] tunnelSettings;
    public Vortex.Settings[] vortexSettings;
    public string paletteSource;
    public string jsonSource;

    public Dance dance;
    public drums drum;
    public PixelReceiver readPixel;

    [Header("GUI")]
    public TextMeshProUGUI effectText;
    public TextMeshProUGUI debugText;
    public TextMeshProUGUI myIPText;
    //  public TextMeshProUGUI myBrightnessText;


    [HideInInspector]
    public EffectBase[] effects;

    [HideInInspector]
    public TransitionBase[] transitions;

    [HideInInspector]
    public Penrose penrose;

    [HideInInspector]
    public Timer timer;

    private OSCReader osc;
    private int OSCtimer;
    private String OSCtext;

    private byte[] udpFrameBuffer;
    private bool inTransition;

    private float fps;
    private float lastCount;
    private int pingIndex;

    private IEnumerator Fps()
    {
        while (true)
        {
            fps = Time.frameCount - lastCount;
            lastCount = Time.frameCount;
            yield return new WaitForSeconds(1f);
        }
    }

    private int[] initDeck(int count)
    {
        int[] deck = new int[count];
        for (int i = 0; i < count; i++)
            deck[i] = i;
        return deck;
    }
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

        Debug.Log($"Effects: {string.Join(", ", factory.Names)}");

        //    effects[startEffect].sortIndex = -1;
        //    ReSortEffectsArray();
        currentEffect = pullCard(effectDeck);

        effects[currentEffect].OnStart();

    }

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

    Guid g;
    byte sequence = 0;



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
        transitions[currentTransition].OnStart();

        Debug.Log($"Transitions: {string.Join(", ", factory.Names)}");
    }
    private void SetupBlenders()
    {
        var factory = new Factory<BlenderBase>();

        blenders = new BlenderBase[factory.Count];
        for (int i = 0; i < blenders.Length; i++)
        {
            blenders[i] = factory.Create(factory.Types[i]);
        }
        transitions[currentTransition].OnStart();

        Debug.Log($"Blenders: {string.Join(", ", factory.Names)}");

    }

    public void JumpToEffect(int i, float time)
    {
        if (i < 0) return;
        if (i >= effects.Length) return;
        EffectBase.APalette.Change();
        //select the new effect
        inTransition = false;
        currentEffect = i;
        effects[currentEffect].OnStart();
        timer.Set(time);
        timer.Reset();
        effectText.text = effects[currentEffect].Name;
        // turn on the button
    }
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


    public void OscHandler(OscMessage om)
    {
        if (om.address.StartsWith("/rhythm/beat"))
            dance.MarkBeat();
        if (om.address == "/beat")
            dance.MarkBeat();

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
    public OscMessage makemessage(string address, float value)
    {
        OscMessage message = new OscMessage();
        message.address = address;
        message.values.Add(value);
        return message;
    }

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

    private void onTimeEndEditCallback(string input) { onMinute = int.Parse(input); }
    private void offTimeEndEditCallback(string input) { offMinute = int.Parse(input); }
    private void destIPEndEditCallback(string input)
    {
        IP = input;
        setIP(IP);
    }
    private void displayOnChange(bool isOn) { displayOn = isOn; }

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

    private void checkTime()
    {
        secondsAccululator += Time.deltaTime;
        FilterTimer -= Time.deltaTime;
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
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        OSCtimer = 0;

        penrose = GameObject.FindObjectOfType<Penrose>();
        penrose.Init();

        myIPText.text = GetLocalIPv4();
        onTime.text = onMinute.ToString();
        offTime.text = offMinute.ToString();
        destIP.text = IP;
        onToggle.isOn = displayOn;

        onTime.onEndEdit.AddListener(onTimeEndEditCallback);
        offTime.onEndEdit.AddListener(offTimeEndEditCallback);
        destIP.onEndEdit.AddListener(destIPEndEditCallback);
        onToggle.onValueChanged.AddListener(displayOnChange);

        SetupEffects();
        SetupTransitions();
        SetupBlenders();
        setIP(IP);

        osc = gameObject.AddComponent(typeof(OSCReader)) as OSCReader;
        osc.SetAllMessageHandler(OscHandler);
        dance = new Dance();
        dance.Init();
        drum = new drums();
        drum.Init();
        readPixel = new PixelReceiver();
        readPixel.Init();

        if (useCamera)
        {
            cameraOverlay = new CameraReader();
            cameraOverlay.Init((int)penrose.Bounds.size.x, (int)penrose.Bounds.size.y, Penrose.Total);
        }

        timer = new Timer(effectTime, false);
        timer.onFinished += OnTimerFinished;

        effectText.text = effects[currentEffect].GetType().ToString();
        StartCoroutine(Fps());
        server = new TelnetServer();
        server.Start();     // start telnet server

    }


    private int GetNewEffectIndex()
    {
        return pullCard(effectDeck);
    }

    private void OnTimerFinished()
    {
        if (inTransition)
        {
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

        inTransition = !inTransition;

        transitions[currentTransition].OnStart();
        transitions[currentTransition].V = 0f;
        transitions[currentTransition].B = GetNewEffectIndex();
        transitions[currentTransition].A = currentEffect;
        EffectBase.APalette.Change();

        effects[transitions[currentTransition].B].OnStart();

        timer.Set(transitionTime);
        timer.Reset();

        currentEffect = -1;

        effectText.text = transitions[currentTransition].Name;
    }

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

    void makeDummySignal(Color[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            Penrose.TileData t = penrose.Tiles[i];
            float y = t.center.y / 30f;
            y += Time.fixedTime;
            y %= 1f;
            {
                Color c = y > 0.5f ? Color.magenta : Color.black;
                buffer[i] = c;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        checkTime();
        timer.Update(Time.deltaTime);
        server.Service();                   // service pending telnet commands
        if (Input.GetKeyDown(KeyCode.Return))
        {
            EffectBase.APalette = new AnimPalette(); // reload the palettes
        }
        if (Input.GetKeyDown("space"))
            dance.MarkBeat();
        EffectBase.APalette.Update();
        dance.Update();

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
                transitions[currentTransition].V = timer.Value;
                transitions[currentTransition].Draw();
                penrose.buffer = (Color[])transitions[currentTransition].buffer.Clone();

                debugText.text = transitions[currentTransition].DebugText();

            }
            else
            {
                effects[currentEffect].Draw();
                penrose.buffer = (Color[])effects[currentEffect].buffer.Clone();

                debugText.text = effects[currentEffect].DebugText();
            }
            if (FilterMode)
                applyFilter(penrose.buffer);
            drum.Draw(penrose.buffer);
        }

        if (useCamera)
            cameraOverlay.Draw(penrose.buffer);

        debugText.text += $"\nFPS: {fps},KB{keyboardBase}";
        if (OSCtimer > 0)
        {
            debugText.text = OSCtext;
            OSCtimer--;
        }

        bool doblend = false;
        if (dummyActive)
        {
            makeDummySignal(blendBuffer);
            debugText.text = "dummy source";
            doblend = true;
        }
        else if (readPixel.Update())
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
                ActiveTransitionBlender.Blend(penrose.buffer, penrose.buffer, blendBuffer);
            }
            else
                penrose.buffer = (Color[])blendBuffer.Clone();


        }

        sendUDPFrame(penrose.buffer);

        penrose.UpdateModelColors();
        OSCping();
    }
}
