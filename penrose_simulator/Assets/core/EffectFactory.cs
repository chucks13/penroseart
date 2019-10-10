using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class EffectFactory {

  private static Type[] effectTypes;
  private static string[] effectNames;

  public static int EffectCount => GetEffectTypes().Length;
  public static Type[] EffectTypes => GetEffectTypes();

  public static string[] EffectNames => GetEffectNames();

  private static string[] GetEffectNames() {
    return effectNames ?? (effectNames = EffectTypes.Select(t => t.ToString()).ToArray());
  }

  private static Type[] GetEffectTypes() {
    return effectTypes ?? (effectTypes = Assembly.GetAssembly(typeof(EffectBase)).GetTypes()
                                                 .Where(
                                                   myType => myType.IsClass && !myType.IsAbstract &&
                                                             myType.IsSubclassOf(typeof(EffectBase)) && !myType.IsSubclassOf(typeof(Transition))
                                                 ).ToArray());
  }

  public static EffectBase CreateEffect(Type t) {
    return Activator.CreateInstance(t) as EffectBase;
  }

}