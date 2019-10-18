using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class Controller : Singleton<Controller> {

  [Header("Effect Switching")]
  public int currentEffect;

  public float effectTime = 10f;

  [Header("Transition Switching")]
  public int currentTransition;

  public float transitionTime = 2f;

  [Header("Settings")]
  public Noise.Settings[] noiseSettings;

  public ColorSparkle.Settings[] sparkleSettings;
  public Nibbler.Settings[] nibblerSettings;
  public Pulse.Settings[] pulseSettings;
  public NoiseTunnel.Settings[] noiseTunnelSettings;
  public Example2DEffect.Settings[] example2dEffectSettings;
  public Dance dance;

  [Header("GUI")]
  public TextMeshProUGUI effectText;

  public TextMeshProUGUI debugText;

  [HideInInspector]
  public EffectBase[] effects;

  [HideInInspector]
  public TransitionBase[] transitions;

  [HideInInspector]
  public Penrose penrose;

  [HideInInspector]
  public Timer timer;

  private bool inTransition;

  private void SetupEffects() {
    var factory = new Factory<EffectBase>();

    effects = new EffectBase[factory.Count];
    for(int i = 0; i < effects.Length; i++) {
      effects[i] = factory.Create(factory.Types[i]);
      effects[i].Init();
    }

    effects[currentEffect].OnStart();

    Debug.Log($"Effects: {string.Join(", ", factory.Names)}");
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

  // Use this for initialization
  void Start() {
    Application.targetFrameRate = 60;

    penrose = GameObject.FindObjectOfType<Penrose>();
    penrose.Init();

    SetupEffects();
    SetupTransitions();
    
    dance = new Dance();
    dance.Init();

    timer            =  new Timer(effectTime, false);
    timer.onFinished += OnTimerFinished;

    effectText.text = effects[currentEffect].GetType().ToString();
  }

  private int GetNewEffectIndex() {
    var i = Random.Range(0, effects.Length);
    return (i == transitions[currentTransition].A) ? GetNewEffectIndex() : i;
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
    transitions[currentTransition].A = currentEffect;
    transitions[currentTransition].V = 0f;
    transitions[currentTransition].B = GetNewEffectIndex();

    effects[transitions[currentTransition].B].OnStart();

    timer.Set(transitionTime);
    timer.Reset();

    currentEffect = -1;

    effectText.text = transitions[currentTransition].Name;
  }

  // Update is called once per frame
  void Update() {
    timer.Update(Time.deltaTime);
    if(Input.GetKeyDown("space")) dance.MarkBeat();
    dance.Update();

    if(inTransition) {
      transitions[currentTransition].V = timer.Value;
      transitions[currentTransition].Draw();
      penrose.buffer = (Color[])transitions[currentTransition].buffer.Clone();

      debugText.text = transitions[currentTransition].DebugText();

    } else {
      effects[currentEffect].Draw();
      penrose.buffer = (Color[])effects[currentEffect].buffer.Clone();

      debugText.text = effects[currentEffect].DebugText();
    }

    penrose.Send();
  }

}