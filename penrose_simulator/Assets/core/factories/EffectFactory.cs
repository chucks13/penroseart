using System;
using System.Linq;
using System.Reflection;

public static class EffectFactory {

  private static Type[] effectTypes;
  private static string[] effectNames;

  public static int Count => GetEffectTypes().Length;
  public static Type[] Types => GetEffectTypes();

  public static string[] Names => GetEffectNames();

  private static string[] GetEffectNames() { return effectNames ?? (effectNames = Types.Select(t => t.ToString()).ToArray()); }

  private static Type[] GetEffectTypes() {
    return effectTypes ?? (effectTypes = Assembly.GetAssembly(typeof(EffectBase)).GetTypes().Where(
             myType => myType.IsClass && !myType.IsAbstract &&
                       myType.IsSubclassOf(typeof(EffectBase))).ToArray());
  }

  public static EffectBase Create(Type t) { return Activator.CreateInstance(t) as EffectBase; }

}