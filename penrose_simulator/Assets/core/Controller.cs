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
  public Example2DEffect.Settings[] example2dEffectSettings;
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

<<<<<<< HEAD
  [HideInInspector]
  public Transition transition;
=======
  //[HideInInspector]
  //public Transition transition;
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter

  private bool inTransition;

  private void SetupEffects() {
    effects = new EffectBase[EffectFactory.EffectCount];
    for(int i = 0; i < effects.Length; i++) {
      effects[i] = EffectFactory.CreateEffect(EffectFactory.EffectTypes[i]);
      effects[i].Init();
    }

    effects[currentEffect].LoadSettings();
<<<<<<< HEAD
=======

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
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter
  }

  // Use this for initialization
  void Start() {

    Application.targetFrameRate = 60;

    penrose = GameObject.FindObjectOfType<Penrose>();

    //geometry = new Tiles();
    SetupEffects();
<<<<<<< HEAD

    transition = new IndexWipe();
    transition.Init();
=======
    SetupTransitions();

    //transition = new IndexWipe();
    //transition.Init();
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter

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

<<<<<<< HEAD
    effects[transition.B].LoadSettings();
=======
    effects[transitions[currentTransition].B].LoadSettings();
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter

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
<<<<<<< HEAD
      transition.V = timer.Value;
      transition.Draw();
      penrose.buffer = (Color[])transition.buffer.Clone();
=======
      transitions[currentTransition].V = timer.Value;
      transitions[currentTransition].Draw();
      penrose.buffer = (Color[])transitions[currentTransition].buffer.Clone();

      var aName = effects[transitions[currentTransition].A].Name;
      var bName = effects[transitions[currentTransition].B].Name;
      var delta = transitions[currentTransition].D;
      var value = transitions[currentTransition].V;

      debugText.text = $"{aName} ({delta:0.00}) => {bName} ({value:0.00})";

>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter
    } else {
      effects[currentEffect].Draw();
      penrose.buffer = (Color[])effects[currentEffect].buffer.Clone();
    }
    
  }

}