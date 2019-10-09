﻿using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class EffectController : Singleton<EffectController> {

  public enum EffectTypes {
    Nibbler,
    Sparkle,
    Noise,
    Pulse
  }

  public float effectTime = 10;
  public int currentEffect;
  
  public Noise.Settings[] noiseSettings;
  public ColorSparkle.Settings[] sparkleSettings;
  public Nibbler.Settings[] nibblerSettings;
  public Pulse.Settings[] pulseSettings;

  public TextMeshProUGUI effectText;
  public TextMeshProUGUI debugText;

    [HideInInspector]
  public Effect[] effects;



  [HideInInspector]
  public Tiles geometry;

  private float timeLeft;
  private Penrose penrose;
  public Dance dance;
  

  // Use this for initialization
  void Start() {
    penrose  = GameObject.FindObjectOfType<Penrose>();

    effects = new Effect[4];
    effects[0] = new Nibbler();
    effects[1] = new ColorSparkle();
    effects[2] = new Noise();
    effects[3] = new Pulse();

    foreach(var effect in effects) {
      effect.Init();
      effect.LoadSettings();
    }

    geometry = new Tiles();
    dance = new Dance();
        dance.Init();
    timeLeft = 0f;

    effectText.text = ((EffectTypes)currentEffect).ToString();
  }

    void EffectUpdate()
    {
        // if we are out of time
        if (timeLeft <= 0)
        {
            // reset the timer
            timeLeft = effectTime;
            // pick a different effect
            currentEffect += Random.Range(1, effects.Length);
            currentEffect %= effects.Length;
            effects[currentEffect].LoadSettings();
            effectText.text = ((EffectTypes)currentEffect).ToString();
        }

        // update the effect
        effects[currentEffect].Draw();

        // copy the effect buffer into the grid object
        penrose.buffer = (Color[])effects[currentEffect].buffer.Clone();
        timeLeft -= Time.deltaTime;
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown("space"))
            dance.markBeat();
        dance.Update();
        EffectUpdate();
    }
}