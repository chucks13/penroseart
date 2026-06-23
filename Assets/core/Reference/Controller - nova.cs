
#if false

// thenova version is commented out because it is incompatible with this code base
// if is only included for reference to do upgrades

// LED https://www.aliexpress.com/item/32734333207.html?spm=a2g0s.9042311.0.0.27254c4d9KHEYb
#define USE_MINI

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Text;
//using System.Diagnostics;
//using UDebug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;
//  gameObjectToHide.GetComponent<Renderer>().enabled = false;

using System.Net.NetworkInformation;
using UnityEngine.Experimental.U2D;



public class Controller : Singleton<Controller>
{
    [Tooltip("Drag the SerialController GameObject here")]
#if UNITY_STANDALONE_WIN 
        public SerialController serial;
#endif
    [Header("Effects")]


    public float TurntableAngle = 0f;
    public double TurntablePeriod = 0f;
    public TouchHandler touchHandler = new TouchHandler();
    public int TouchSensors = 0;
    Stopwatch stopwatch = null;
    public EffectBase[] effects;
    public int[] remap;

    public float debug1;
    public float debug2;
    public float debug3;

    private string keyPressed;

    public bool NYE = false;
    private float secondsAccululator = 0f;
    public float onMinute = 1700;
    public float offMinute = 200;
    public InputField destIP;
    public InputField onTime;
    public InputField offTime;
    public Toggle onToggle;
    public bool displayOn = true;
    [Header("UDP")]
    public string IP;
    public int brightness;
    int port = 5568;      // default e131 port 8888;/
    private static int localPort;


    // public SerialOut serial;

    IPEndPoint remoteEndPoint;
    UdpClient client;

    [Header("Effect Switching")]
    //   public int startEffect;

    EffectBase currentEffect;
    EffectBase toEffect;
    private int keyboardBase = 0;

    public float effectTime = 10f;

    [Header("Transition Switching")]
    public bool randomTransition = true;

    public float transitionTime = 2f;

    [Header("Settings")]

    public float timescale = 1f;
    public float framerate = 1f / 60f;

    public string paletteSource;
    public string jsonSource;

    public ACNHandler readACN;

    [Header("GUI")]
    public TextMeshProUGUI effectText;
    public TextMeshProUGUI debugText;
    public TextMeshProUGUI myIPText;
    //  public TextMeshProUGUI myBrightnessText;

    [Header("Effects")]

    [HideInInspector]
    private int[] sortedEffects;

    [HideInInspector]
    public TransitionBase[] transitions;
    private int[] sortedTransitions;
    public PenBase[] pens;
    private int[] sortedPens;

    TransitionBase currentTransition;
    [HideInInspector]
    public Display Display;

    [HideInInspector]
    //    public Timer timer;
    public float timeElapsed;
    // public float timeScale=1;
    private OSCReader osc;
    private int OSCtimer;
    private String OSCtext;

    private bool inTransition;

    private float fps;
    private float lastCount;

    private Color[] currentBuffer = new Color[752];
    private Color[] LastBuffer = new Color[752];


    private float tween = 0f;

    void OnEnable()
    {
#if UNITY_STANDALONE_WIN
                if (serial != null)
                    serial.OnLineReceived += HandleSerialLine;
#endif
    }

    void OnDisable()
    {
        Screen.sleepTimeout = SleepTimeout.SystemSetting;

#if UNITY_STANDALONE_WIN
                if (serial != null)
                    serial.OnLineReceived -= HandleSerialLine;
#endif
    }

    void OnDestroy()
    {
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
    }

    void HandleSerialLine(string line)
    {
        //        Debug.Log($"Serial : {line}");
        var parts = line.Split('=');
        if (parts.Length == 2)
        {
            if (parts[0] == "home")
            {
                if (stopwatch == null)
                {
                    stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Debug.Log("Start stopwatch");
                    return;
                }
                TurntablePeriod = stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();
                Debug.Log("Restart stopwatch");
            }
            if (parts[0] == "touch")
            {
                if (int.TryParse(parts[1], out int result))
                {
                    TouchSensors = result;
                }
            }
        }
    }



    private IEnumerator Fps()
    {
        while (true)
        {
            fps = Time.frameCount - lastCount;
            lastCount = Time.frameCount;
            yield return new WaitForSeconds(1f);
        }
    }

    private void setupRemap()
    {
        int[] sectionremap = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        remap = new int[752];
        for (int i = 0; i < 752; i++)
        {
            int section = i / 45;
            int reverse = 44 - (i % 45);
            remap[i] = 751 - i;// (sectionremap[section] * 45) + reverse;
        }
    }
    private void SetupEffects()
    {
        var factory = new Factory<EffectBase>();

        effects = new EffectBase[factory.Count];
        sortedEffects = new int[factory.Count];
        for (int i = 0; i < effects.Length; i++)
        {
            effects[i] = factory.Create(factory.Types[i]);
            effects[i].Init();
            sortedEffects[i] = i;
        }
        Debug.Log($"Effects: {string.Join(", ", factory.Names)}");
    }

    private void SetupTransitions()
    {
        var factory = new Factory<TransitionBase>();

        transitions = new TransitionBase[factory.Count];
        sortedTransitions = new int[factory.Count];
        for (int i = 0; i < transitions.Length; i++)
        {
            transitions[i] = factory.Create(factory.Types[i]);
            transitions[i].Init();
            sortedTransitions[i] = i;
        }
        Debug.Log($"Transitions: {string.Join(", ", factory.Names)}");
    }

    private void SetupPens()
    {
        var factory = new Factory<PenBase>();

        pens = new PenBase[factory.Count];
        sortedPens = new int[factory.Count];
        for (int i = 0; i < pens.Length; i++)
        {
            pens[i] = factory.Create(factory.Types[i]);
            pens[i].Init();
            sortedPens[i] = i;
        }
        Debug.Log($"Pens: {string.Join(", ", factory.Names)}");
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

    int tabeffect = 0;

    void jumptotab(int n)
    {
        if (n < effects.Length)
            JumpToEffect(n);
    }

    public void nextEffect()
    {
        tabeffect++;
        tabeffect %= effects.Length;
        JumpToEffect(tabeffect);

    }
    public void prevEffect()
    {
        tabeffect += effects.Length;
        tabeffect--;
        tabeffect %= effects.Length;
        JumpToEffect(tabeffect);

    }
    public void sameEffect()
    {
        JumpToEffect(tabeffect);
    }

    public void randEffect()
    {
        JumpToEffect(Random.Range(0, effects.Length));
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


    private async Task sendcustom(int universe, byte[] data, int start, int length)
    {
        int fullLength = 4 + length;

        byte[] sending = new byte[fullLength];
        // set the universe umber
        sending[2] = (byte)(universe);
        // copy the data
        for (int i = 0; i < length; i++)
            sending[i + 4] = data[i + start];
        await client.SendAsync(sending, fullLength, remoteEndPoint);
    }

    private int udpuniverse;
    private int udpidx;
    private byte[] udppack = new byte[512];


    
    private void addbytetoudp(byte n)
    {
        udppack[udpidx] = n;
        udpidx++;
#if USE_MINI
        int packetsize=510;
#else
        int packetsize=512;
#endif        

        if (udpidx == packetsize)
        {
            sendACN(udpuniverse++, udppack, 0, udpidx);        // send universer
            udpidx = 0;
        }
    }

    private void sendUDPFrame(Color[] data)
    {

        udpuniverse = 1;
        udpidx = 0;
#if USE_MINI
    int[,] wires=Display.MiniWires;

#else
    int[,] wires=Display.HexWires;
#endif
        int rows = Display.MiniWires.GetLength(0);    
        int columns = Display.MiniWires.GetLength(1); 
        byte level = (byte)brightness;
        if (!displayOn)
            level = 0;
        for (int output = 0; output < rows; output++)
        {
            for (int pixel = 0; pixel < columns; pixel++)
            {
                int n = wires[output, pixel];
                if(n>752)
                    continue;
                Color c;
                if (n < data.Length)
                    c = data[n];
                else
                    c = Color.black;            // fill the unused pixels with black
                //              if (i == 3)     // used to discover output wires
                //                c = Color.green;
                // 2 LEDs per pixel
#if USE_MINI
                addbytetoudp((byte)(Math.Max(c.r, 0f) * level));
                addbytetoudp((byte)(Math.Max(c.g, 0f) * level));
                addbytetoudp((byte)(Math.Max(c.b, 0f) * level));
#else                
                addbytetoudp((byte)(Math.Max(c.r, 0f) * level));
                addbytetoudp((byte)(Math.Max(c.g, 0f) * level));
                addbytetoudp((byte)(Math.Max(c.b, 0f) * level));

                addbytetoudp((byte)(Math.Max(c.r, 0f) * level));
                addbytetoudp((byte)(Math.Max(c.g, 0f) * level));
                addbytetoudp((byte)(Math.Max(c.b, 0f) * level));
#endif                
            }
        }
        sendACN(udpuniverse++, udppack, 0, udpidx);        // send the remainer
        acnheader[111] = sequence++;
    }
    public void JumpToEffect(int i)
    {
        if (i < 0) return;
        if (i >= effects.Length) return;
        //    EffectBase.APalette.Change();
        //select the new effect
        inTransition = false;
        currentEffect = effects[i];
        for (int x = 0; x < effects.Length; x++)
            effects[x].activeity = 0;
        currentEffect.OnStart();
        currentEffect.activeity = 3;
        timeElapsed = 0f;
        effectText.text = currentEffect.Name;
        // turn on the button
    }

    public int FindEffect(string name)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i].Name == name)
                return i;
        }
        return -1;

    }
    public void OSCpage1(OscMessage om, ArrayList oms)
    {
        if (om.address == "/1/vscroll1")       // brightness
        {
            brightness = (int)Mathf.Lerp(255f, 0f, om.GetFloat(0));
        }
        if (om.address.StartsWith("/1/nav1"))
        {
            if (om.GetInt(0) == 1)
                NYE = !NYE;
        }


    }


    public void OscHandler(OscMessage om)
    {

        ArrayList oms = new ArrayList();        // make a list of replies
        OSCpage1(om, oms);
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
    private void bury(int item, int[] list)
    {
        int moving = list[item];
        for (int i = item; i < list.Length - 1; i++)
            list[i] = list[i + 1];
        list[list.Length - 1] = moving;
    }
    public EffectBase GetRandomEffect(bool allowMixers = true)
    {
        int item = 0;
        int Idx = 0;
        EffectBase effect = null;

        bool testing = false;       // set to true to test
        if (allowMixers && testing)                // testing
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] is distortionFilter)     // specify effect to test
                {
                    effect = effects[i];
                    effect.activeity = 3;
                    effect.OnStart();
                    return effect;
                }
            }

        }


        for (int i = 0; i < effects.Length; i++)
        {
            int j = effects.Length / 2; // preferred
            if (i > j)                  // we went past thatS
                j = i;
            item = Random.Range(0, j);
            Idx = sortedEffects[item];
            // place select item at the end of the list
            EffectBase maybe = effects[Idx];
            if ((!allowMixers) && maybe is Mixer)
                continue;
            effect = maybe;

            bury(item, sortedEffects);
            break;
        }
        if (effect == null)
        {
            Debug.Log("Failsafe");
            // default some non mixedr
            for (int i = 0; i < effects.Length; i++)
            {
                if (!(effects[i] is Mixer))
                {
                    effect = effects[i];
                    break;
                }
            }
        }

        effect.activeity = 3;
        effect.OnStart();
        return effect;
    }

    public TransitionBase GetRandomTransition()
    {
        /*            
                                for (int i = 0; i < transitions.Length; i++)
                                {
                                    if (transitions[i] is PentTransition)
                                        return transitions[i];
                                }
        */
        int item = Random.Range(0, transitions.Length / 2);
        int Idx = sortedTransitions[item];
        // place select item at the end of the list
        bury(item, sortedTransitions);
        TransitionBase transition = transitions[Idx];
        return transition;
    }
    public PenBase GetRandomPen()
    {
        /*
        for (int i = 0; i < pens.Length; i++)
        {
            if (pens[i] is PenDotted)
            {
                pens[i].Start();
                return pens[i];
            }
        }
        */
        int item = Random.Range(0, (pens.Length + 1) / 2);
        int Idx = sortedPens[item];
        //        Idx = 3;
        // place select item at the end of the list
        bury(item, sortedPens);
        PenBase pen = pens[Idx];
        pen.Start();
        return pen;
    }

    private void checkTime()
    {
        // handle state machine
        if (inTransition)
        {
            if (timeElapsed >= transitionTime)
            {
                currentEffect = toEffect;
                inTransition = false;
                timeElapsed = 0f;
            }
        }
        else
        {
            if (timeElapsed >= effectTime)
            {
                toEffect = GetRandomEffect();
                currentTransition = GetRandomTransition();
                currentTransition.A = currentEffect;
                currentTransition.B = toEffect;
                currentTransition.OnStart();
                timeElapsed = 0f;
                inTransition = true;
            }
        }

        // styart stop time
        /*
        secondsAccululator += Time.deltaTime;
        if (secondsAccululator > 1f)
        {
            secondsAccululator %= 1f;

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
            json += "\"displayOn\":\"" + displayOn + "\",";
            json += "\"brightness\":\"" + brightness + "\",";
            json += "\"onMinute\":\"" + onMinute + "\",";
            json += "\"offMinute\":\"" + offMinute + "\"";
            json += "}";
            StartCoroutine(PostRequest(url, json));
        }
        */
    }

    void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        Application.targetFrameRate = 60;
        OSCtimer = 0;

        Display = GameObject.FindObjectOfType<Display>();
        Display.Init();

        myIPText.text = GetLocalIPv4();
        onTime.text = onMinute.ToString();
        offTime.text = offMinute.ToString();
        destIP.text = IP;
        onToggle.isOn = displayOn;

        onTime.onEndEdit.AddListener(onTimeEndEditCallback);
        offTime.onEndEdit.AddListener(offTimeEndEditCallback);
        destIP.onEndEdit.AddListener(destIPEndEditCallback);
        onToggle.onValueChanged.AddListener(displayOnChange);

        setupRemap();
        SetupEffects();
        SetupTransitions();
        SetupPens();
        timeElapsed = 0f;
        inTransition = false;
        currentEffect = GetRandomEffect();
        setIP(IP);

        osc = gameObject.AddComponent(typeof(OSCReader)) as OSCReader;
        osc.SetAllMessageHandler(OscHandler);
        readACN = new ACNHandler();
        readACN.Init();

        StartCoroutine(Fps());
        //     serial=new SerialOut();
        //    serial.Init(1000000);
    }


    void UpdateKeypress()
    {
        // Check if any key is pressed
        if (Input.anyKeyDown)
        {
            // Get the pressed key as a string
            keyPressed = GetPressedKeyAsString();
            // Check which key was pressed
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            brightness += 17;
            if (brightness > 255)
                brightness = 255;
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            brightness -= 17;
            if (brightness < 0)
                brightness = 0;

        }
        if (Input.GetKeyUp(KeyCode.O))      // different for hand controller
        {
            displayOn = !displayOn;
            onToggle.isOn = displayOn;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            nextEffect();
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            prevEffect();
        }
        /*
        if (Input.GetKeyDown(KeyCode.H) && (snakeRibbon.demostate == 0))
        {
            if (timescale == 1f)
                timescale = 0.25f;
            else
                timescale = 1f;

        }
*/
    }
    /*
       8bitzero 2
    JoystickButton0-7
         K        M 
       C            H
      E F          I G        
       D    N  O    J        
    */


    string GetPressedKeyAsString()
    {
        // Loop through all possible keys
        foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
        {
            // Check if the current key is pressed
            if (Input.GetKeyDown(keyCode))
            {
                // Convert the key code to string and return it
                return keyCode.ToString();
            }
        }

        // Return an empty string if no key is pressed
        return "";
    }   // Update is called once per frame

    void updateTurntable()
    {
        double elapsed = 0;
        if (stopwatch != null)
        {
            elapsed = stopwatch.Elapsed.TotalSeconds;
            //            Debug.Log($"Elapsed : {elapsed}");
        }

        if (TurntablePeriod > 0)
        {
            //            Debug.Log($"TurntablePeriod : {TurntablePeriod}");
            TurntableAngle = (float)(elapsed / TurntablePeriod);
            TurntableAngle *= 360f;             // reverse spin for still image
            Display.transform.rotation = Quaternion.Euler(0f, (float)TurntableAngle, 0f);
        }
        else
            Display.transform.Rotate(0f, -0.1f, 0f, Space.World);     // slow spin

    }
    void Update()
    {
        UpdateKeypress();
        updateTurntable();

        //        Display.transform.Rotate(0f, 0.1f, 0f, Space.World);   

        if (Input.GetKey(KeyCode.LeftArrow))
            Display.transform.Rotate(0f, 0.2f, 0f, Space.World);
        //        if (Input.GetKey(KeyCode.RightArrow))


        if (Input.GetKey(KeyCode.UpArrow))
            Display.transform.Rotate(1f, 0f, 0f, Space.World);
        if (Input.GetKey(KeyCode.DownArrow))
            Display.transform.Rotate(-1f, 0f, 0f, Space.World);
        timeElapsed += Time.deltaTime;
        checkTime();
        //       timer.Update(Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.Return))
        {
            EffectBase.APalette = new AnimPalette(); // reload the palettes
        }
        EffectBase.APalette.Update();

        if (Input.GetMouseButtonDown(0))
        {
            //            JumpToEffect(Random.Range(0,effects.Length));
        }

        if (Input.GetKeyDown(KeyCode.X)) keyboardBase = 1 - keyboardBase;       // toggle base
        /*
        if (NYE)
        {
            Color[] buffer = Display.buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (Random.Range(0, 16) == 0) ? Color.white : Color.black;
            }
        }
        else
        */
        //---------------------------------------------------------------------
        //     timescale = 1f / 20f;
        framerate = 1f / 60f;
        Display.buffer = (Color[])currentBuffer.Clone();
        tween += timescale;  //  framerate * timescale;
        if (tween >= 1f)
        {
            LastBuffer = (Color[])currentBuffer.Clone();
            {
                for (int i = 0; i < effects.Length; i++)
                    if (effects[i].activeity > 0)
                        effects[i].activeity--;
                if (inTransition)
                {
                    effectText.text = currentTransition.GetType().ToString();
                    currentTransition.A.Draw();
                    if (currentTransition.A != currentTransition.B)
                        currentTransition.B.Draw();
                    currentTransition.A.activeity = 3;
                    currentTransition.B.activeity = 3;

                    currentTransition.V = timeElapsed / transitionTime;
                    currentTransition.Draw();
                    currentBuffer = (Color[])currentTransition.buffer.Clone();

                    debugText.text = currentTransition.DebugText();

                }
                else
                {
                    effectText.text = currentEffect.GetType().ToString();
                    currentEffect.Draw();
                    currentEffect.activeity = 3;
                    currentBuffer = (Color[])currentEffect.buffer.Clone();

                }
            }
            tween = 0f;
            //            if (tween > framerate)
            //                tween = framerate;
        }
        //        tween = 0;

        // add touch layer
        touchHandler.Draw(currentBuffer);

        for (int i = 0; i < currentBuffer.Length; i++)
            Display.buffer[i] = currentBuffer[i];// Color.Lerp(LastBuffer[i], currentBuffer[i], tween);
        //---------------------------------------------------------------------
        debugText.text = currentEffect.DebugText();
        debugText.text += $"\nFPS: {fps},KB{keyboardBase}";

        debugText.text += "\n" + keyPressed + "\n";
        for (int i = 0; i < effects.Length; i++)
            debugText.text += (effects[i].activeity > 0) ? (effects[i] is Mixer ? "*" : "o") : "-";


        if (OSCtimer > 0)
        {
            debugText.text = OSCtext;
            OSCtimer--;
        }
        sendUDPFrame(Display.buffer);
        //       serial.send(Display.buffer,brightness);

        Display.UpdateModelColors();
        /*
        if (Input.anyKey)
        {
            for (KeyCode k = KeyCode.A; k < KeyCode.X; k++)
            {
                if (Input.GetKeyDown(k))
                {
                    int button = k - KeyCode.A;
                    int i = button + (keyboardBase * 23);
                    JumpToEffect(i);
                }
            }
        }
        */
    }
}

#endif
