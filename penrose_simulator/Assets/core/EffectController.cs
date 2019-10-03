using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class EffectController : Singleton<EffectController> {

  public enum EffectTypes {
    Nibbler,
    Sparkle,
    Noise
  }

  public float effectTime = 10;
  public int currentEffect;
  
  public Noise.Settings[] noiseSettings;
  public ColorSparkle.Settings[] sparkleSettings;
  public Nibbler.Settings[] nibblerSettings;

  [HideInInspector]
  public Effect[] effects;
  
  [HideInInspector]
  public Tiles geometry;

  private float timeLeft;
  private Penrose penrose;

  // Use this for initialization
  void Start() {
    penrose  = GameObject.FindObjectOfType<Penrose>();
    
    effects = new Effect[3];
    effects[0] = new Nibbler();
    effects[1] = new ColorSparkle();
    effects[2] = new Noise();

    foreach(var effect in effects) {
      effect.Init();
      effect.LoadSettings();
    }

    geometry = new Tiles();
  }

  // Update is called once per frame
  void Update() {
    // if we are out of time
    if(timeLeft <= 0) {
      // reset the timer
      timeLeft = effectTime;
      // pick a different effect
      currentEffect += Random.Range(1, effects.Length);
      currentEffect %= effects.Length;
      effects[currentEffect].LoadSettings();
    }

    // update the effect
    effects[currentEffect].Draw();

    // copy the effect buffer into the grid object
    penrose.buffer = (Color[]) effects[currentEffect].buffer.Clone();
    timeLeft -= Time.deltaTime;
  }
}