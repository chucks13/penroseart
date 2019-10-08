using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EffectFactory {

  public static EffectBase CreateEffect(Controller.EffectTypes t) {
    switch(t) {
      case Controller.EffectTypes.Nibbler: return new Nibbler();
      case Controller.EffectTypes.Sparkle: return new ColorSparkle();
      case Controller.EffectTypes.Noise:   return new Noise();
      case Controller.EffectTypes.Pulse:   return new Pulse();
      default:                             throw new ArgumentOutOfRangeException(nameof(t), t, null);
    }
  }
}