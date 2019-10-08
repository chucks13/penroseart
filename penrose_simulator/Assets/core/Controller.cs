using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class Controller : Singleton<Controller> {

  public enum EffectTypes {
    Nibbler,
    Sparkle,
    Noise,
    Pulse
  }

  [Header("Switching")]
  public float effectTime = 10;
  public int currentEffect;

  [Header("Settings")]
  public Noise.Settings[] noiseSettings;
  public ColorSparkle.Settings[] sparkleSettings;
  public Nibbler.Settings[] nibblerSettings;
  public Pulse.Settings[] pulseSettings;
  public Dance dance;

  [Header("GUI")]
  public TextMeshProUGUI effectText;
  public TextMeshProUGUI debugText;

  [HideInInspector]
  public EffectBase[] effects;

  [HideInInspector]
  public Tiles geometry;

  private float timeLeft;
  private Penrose penrose;

  private void SetupEffects() {
    effects    = new EffectBase[5];
    effects[0] = EffectFactory.CreateEffect(EffectTypes.Nibbler);
    effects[1] = EffectFactory.CreateEffect(EffectTypes.Sparkle);
    effects[2] = EffectFactory.CreateEffect(EffectTypes.Noise);
    effects[3] = EffectFactory.CreateEffect(EffectTypes.Pulse);
    effects[4] = new RandomEffectsMixer();

    foreach(var effect in effects) {
      effect.Init();
    }

    effects[currentEffect].LoadSettings();
  }

  // Use this for initialization
  void Start() {
    penrose = GameObject.FindObjectOfType<Penrose>();

    SetupEffects();

    geometry = new Tiles();
    dance    = new Dance();
    dance.Init();

    timeLeft = effectTime;

    effectText.text = effects[currentEffect].GetType().ToString();
  }

  void EffectUpdate() {
    // if we are out of time
    if(timeLeft <= 0) {
      // reset the timer
      timeLeft = effectTime;
      // pick a different effect
      currentEffect += Random.Range(1, effects.Length);
      currentEffect %= effects.Length;
      effects[currentEffect].LoadSettings();
      effectText.text = effects[currentEffect].GetType().ToString();
    }

    // update the effect
    effects[currentEffect].Draw();

    // copy the effect buffer into the grid object
    penrose.buffer =  (Color[])effects[currentEffect].buffer.Clone();
    timeLeft       -= Time.deltaTime;
  }

  // Update is called once per frame
  void Update() {
    if(Input.GetKeyDown("space")) dance.MarkBeat();
    dance.Update();
    EffectUpdate();
  }
}