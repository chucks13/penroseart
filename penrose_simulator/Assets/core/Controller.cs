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

using System.Net.NetworkInformation;

public class Controller : Singleton<Controller> {

    [Header("UDP")]
    public string IP;
    public byte brightness;
    int port=5568;      // default e131 port
    private static int localPort;
    IPEndPoint remoteEndPoint;
    UdpClient client;
    public CameraReader cameraOverlay;

    [Header("Effect Switching")]
    public int startEffect;

  private int currentEffect;

  public float effectTime = 10f;

  [Header("Transition Switching")]
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

    public Dance dance;
  public drums drum;
  public ACNHandler readACN;

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

  private bool inTransition;

  private float fps;
  private float lastCount;
  private int pingIndex;
 
  private IEnumerator Fps() {
    while(true) {
      fps = Time.frameCount - lastCount;
      lastCount = Time.frameCount;
      yield return new WaitForSeconds(1f);
    }
  }

  private void SetupEffects() {
    var factory = new Factory<EffectBase>();

    effects = new EffectBase[factory.Count];
    for(int i = 0; i < effects.Length; i++) {
      effects[i] = factory.Create(factory.Types[i]);
      effects[i].Init();
      effects[i].sortIndex = Random.Range(0, 10000);
      effects[i].initialIndex = i;
    }
    pingIndex = 0;

    Debug.Log($"Effects: {string.Join(", ", factory.Names)}");

    effects[startEffect].sortIndex = -1;
    ReSortEffectsArray();
    currentEffect = 0;

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
    byte sequence=0;



    public string GetLocalIPv4()
    {
        string addresses = "";
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                 addresses+= "\n"+ ip.ToString();
            }
        }
        return addresses;
    }


    private void setupUDP()
    {
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), port);
        client = new UdpClient();
        // set up template
        g = Guid.NewGuid();
        byte[] uuid = g.ToByteArray();
        for (int i = 0; i < 16; i++)
            acnheader[i + 22] = uuid[i];
    }

    private async Task sendACN(int universe,byte[] data,int start,int length)
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

    private void sendUDPFrame(Color[] data)
    {
        byte[] frame = new byte[1800*3];          // 900 bytes plus 4 header per packet
        int ptr2;
        int ptr1;
        // build uf the frame data
        ptr2 = 0;
        int[] wires = penrose.JsonRawData.wires;
        int size = wires.Length;
        for (ptr1=0;ptr1< size; ptr1++)
        {
            int ptr3 = wires[ptr1]/2;
            frame[ptr2++] = (byte)(data[ptr3].r * brightness);
            frame[ptr2++] = (byte)(data[ptr3].b * brightness);
            frame[ptr2++] = (byte)(data[ptr3].g * brightness);
        }
        // send the packets
        int universe = 1;
        for (ptr1 = 0; ptr1 < (5400-510); ptr1 += 510)
        {
            sendACN(universe++, frame, ptr1, 510);
        }
        sendACN(universe, frame, ptr1, 5400 - ptr1);
        acnheader[111] = sequence++;
    }

    private void SetupTransitions() {
    var factory = new Factory<TransitionBase>();

    transitions = new TransitionBase[factory.Count];
    for(int i = 0; i < transitions.Length; i++) {
      transitions[i] = factory.Create(factory.Types[i]);
      transitions[i].Init();
    }

    Debug.Log($"Transitions: {string.Join(", ", factory.Names)}");
  }

    public void JumpToEffect(int i)
    {
        if (i < 0) return;
        if (i >= effects.Length) return;
        EffectBase.APalette.Change();
        //select the new effect
        inTransition = false;
        currentEffect = i;
        effects[currentEffect].OnStart();
        timer.Set(effectTime);
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
        if(om.address.StartsWith("/1/push"))
        {
            if(om.GetInt(0)==1)
            {
                int button = int.Parse(om.address.Substring(7)) - 1;
                for (int i = 0; i < effects.Length; i++)
                {
                    if (effects[i].initialIndex == button)
                    {
                        JumpToEffect(i);
                        oms.Add(makemessage(om.address, 1f));
                        break;
                    }
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
            if(currentEffect>=0)
            {
                osc.Send(makemessage("/1/push" + (pingIndex + 1), (pingIndex == effects[currentEffect].initialIndex) ? 1f : 0f));
                pingIndex++;
                pingIndex %= effects.Length;
            }
        }

    }


    public void OscHandler(OscMessage om)
    {
        if(om.address=="/beat")
            dance.MarkBeat();

        ArrayList oms = new ArrayList();        // make a list of replies
        OSCpage1(om,oms);
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

    public void OSCping()
    {
        ArrayList oms = new ArrayList();        // make a list of replies
        OscMessage om = makemessage("/ping", 0);
        OSCpage1(om, oms);
        cameraOverlay.OSCHandler(om, oms);
        drum.OSCHandler(om,oms);
        if (oms.Count > 0)                      // send any replies
            osc.Send(oms);
    }
    // Use this for initialization
    void Start() {
    Application.targetFrameRate = 60;
    OSCtimer = 0;

    penrose = GameObject.FindObjectOfType<Penrose>();
    penrose.Init();
    


        myIPText.text = GetLocalIPv4();

    SetupEffects();
    SetupTransitions();
        try
        {
            setupUDP();

        }
        catch (Exception e)
        {

            Debug.Log($"Failed to setup UDP: {e.Message}");
        }

    osc = gameObject.AddComponent(typeof(OSCReader)) as OSCReader;
    osc.SetAllMessageHandler(OscHandler);
    dance = new Dance();
    dance.Init();
    drum = new drums();
    drum.Init();
    readACN = new ACNHandler();
    readACN.Init();

    cameraOverlay = new CameraReader();
    cameraOverlay.Init((int)penrose.Bounds.size.x,(int) penrose.Bounds.size.y, Penrose.Total);

    timer =  new Timer(effectTime, false);
    timer.onFinished += OnTimerFinished;

    effectText.text = effects[currentEffect].GetType().ToString();
    StartCoroutine(Fps());
  }

 
  private int GetNewEffectIndex() {
    effects[currentEffect].sortIndex = 100000;
    ReSortEffectsArray();
    currentEffect = effects.Length - 1;
    return Random.Range(0, effects.Length / 2);
  }

  private void ReSortEffectsArray() {
    // sort effects array
    
    Array.Sort(effects, (a, b) => a.sortIndex.CompareTo(b.sortIndex));
    var names = new string[effects.Length];
    for(int i = 0; i < effects.Length; i++) {
      effects[i].sortIndex = i;
      names[i] = effects[i].Name;
    }
    Debug.Log($"{string.Join(", ", names)}");
  }

  private void OnTimerFinished() {
    if(inTransition) {
      inTransition  = !inTransition;
      currentEffect = transitions[currentTransition].B;
      timer.Set(effectTime);
      timer.Reset();
      effectText.text = effects[currentEffect].Name;
      currentTransition = Random.Range(0, transitions.Length);
      return;
    }

    inTransition                     = !inTransition;
   
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

    // Update is called once per frame
    void Update() {
        timer.Update(Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.Return))
        {
            EffectBase.APalette= new AnimPalette(); // reload the palettes
        }
        if (Input.GetKeyDown("space")) 
        dance.MarkBeat();
        EffectBase.APalette.Update();
        dance.Update();

        if (Input.anyKey)
        {
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
            {
                if (Input.GetKeyDown(k))
                {
                    int button = k - KeyCode.A;
                    for (int i = 0; i < effects.Length; i++)
                    {
                        if (effects[i].initialIndex == button)
                        {
                            JumpToEffect(i);
                            break;
                        }
                    }
                    break;
                }
            }
        }
        // test drums
        if (Input.GetKeyDown("1")) drum.hit(0, 1f);
        if (Input.GetKeyDown("2")) drum.hit(1, 1f);
        if (Input.GetKeyDown("3")) drum.hit(2, 1f);
        if (Input.GetKeyDown("4")) drum.hit(3, 1f);
        if (Input.GetKeyDown("5")) drum.hit(4, 1f);
        drum.Update();
        if ( readACN.Update())
        {
            penrose.buffer = (Color[])readACN.buffer.Clone();
             debugText.text = "ACN source";
        }
        else 
        { 
            if (inTransition) {
                transitions[currentTransition].V = timer.Value;
                transitions[currentTransition].Draw();
                penrose.buffer = (Color[])transitions[currentTransition].buffer.Clone();

                debugText.text = transitions[currentTransition].DebugText();

            } else {
                effects[currentEffect].Draw();
                penrose.buffer = (Color[])effects[currentEffect].buffer.Clone();

                debugText.text = effects[currentEffect].DebugText();
            }
            drum.Draw(penrose.buffer);
        }

        cameraOverlay.Draw(penrose.buffer);

        debugText.text += $"\nFPS: {fps}";
        if(OSCtimer>0)
        {
            debugText.text = OSCtext;
            OSCtimer--;
        }
        sendUDPFrame(penrose.buffer);
 
        penrose.UpdateModelColors();
        OSCping();
    }
}

