using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class Controller : Singleton<Controller> {

  [Header("Switching")]
  public float effectTime = 10f;

  public float transitionTime = 2f;
  public int currentEffect;

  [Header("Settings")]
  public Noise.Settings[] noiseSettings;

  public ColorSparkle.Settings[] sparkleSettings;
  public Nibbler.Settings[] nibblerSettings;
  public Pulse.Settings[] pulseSettings;
  public NoiseTunnel.Settings[] noiseTunnelSettings;
  public Dance dance;

  [Header("GUI")]
  public TextMeshProUGUI effectText;

  public TextMeshProUGUI debugText;

  [HideInInspector]
  public EffectBase[] effects;

  [HideInInspector]
  public Penrose penrose;

  [HideInInspector]
  public Timer timer;

  [HideInInspector]
  public Transition transition;

  private bool inTransition;

  private void SetupEffects() {
    effects = new EffectBase[EffectFactory.EffectCount];
    string names = string.Empty;
    for(int i = 0; i < effects.Length; i++) {
      effects[i] = EffectFactory.CreateEffect(EffectFactory.EffectTypes[i]);
      effects[i].Init();
      names += i != effects.Length - 1 ? $"{effects[i].Name}, " : $"{effects[i].Name}";
    }

    effects[currentEffect].LoadSettings();

    Debug.Log($"{names}");
  }

  // Use this for initialization
  void Start() {

    Application.targetFrameRate = 60;

    penrose = GameObject.FindObjectOfType<Penrose>();

    //geometry = new Tiles();
    SetupEffects();

    transition = new IndexWipe();
    transition.Init();

    dance = new Dance();
    dance.Init();

    timer            =  new Timer(effectTime, false);
    timer.onFinished += OnTimerFinished;

    effectText.text = effects[currentEffect].GetType().ToString();

  }

  private int GetNewEffectIndex() {
    var i = Random.Range(0, effects.Length);
    return (i == transition.A) ? GetNewEffectIndex() : i;
  }

  private void OnTimerFinished() {
    if(inTransition) {
      inTransition  = !inTransition;
      currentEffect = transition.B;
      timer.Set(effectTime);
      timer.Reset();
      effectText.text = effects[currentEffect].Name;
      return;
    }

    inTransition = !inTransition;
    transition.A = currentEffect;
    transition.V = 0f;
    transition.B = GetNewEffectIndex();

    effects[transition.B].LoadSettings();

    timer.Set(transitionTime);
    timer.Reset();

    currentEffect = -1;
  }

  // Update is called once per frame
  void Update() {
    timer.Update(Time.deltaTime);
    if(Input.GetKeyDown("space")) dance.MarkBeat();
    dance.Update();

    if(inTransition) {
      transition.V = timer.Value;
      transition.Draw();
      penrose.buffer = (Color[])transition.buffer.Clone();
    } else {
      effects[currentEffect].Draw();
      penrose.buffer = (Color[])effects[currentEffect].buffer.Clone();
    }
    
  }

}