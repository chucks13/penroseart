using System;
using System.Collections;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class Controller : Singleton<Controller> {

  [Header("Effect Switching")]
  public int startEffect;

  private int currentEffect;

  public float effectTime = 10f;

  [Header("Transition Switching")]
  public int currentTransition;

  public float transitionTime = 2f;

  [Header("Settings")]
  public Noise.Settings[] noiseSettings;

  public ColorSparkle.Settings[] sparkleSettings;
  public Nibbler.Settings[] nibblerSettings;
  public Pulse.Settings[] pulseSettings;
  public Ripple.Settings[] rippleSettings;
  public NoiseTunnel.Settings[] noiseTunnelSettings;
  public Plasma.Settings[] plasmaSettings;
  public Julia.Settings[] juliaSettings;
  public Flock.Settings[] flockSettings;
  public MetaBalls.Settings[] metaBallsSettings;
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

  private float fps;
  private float lastCount;

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
    }

    Debug.Log($"Effects: {string.Join(", ", factory.Names)}");

    effects[startEffect].sortIndex = -1;
    ReSortEffectsArray();
    currentEffect = 0;

    effects[currentEffect].OnStart();

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

    debugText.text += $"\nFPS: {fps}";

    penrose.Send();
  }

}
