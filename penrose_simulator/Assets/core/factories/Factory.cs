using System;
using System.Linq;
using System.Reflection;

public class Factory<T> where T : class {

  private Type[] types;
  private string[] names;

  public int Count => GetTypes().Length;
  public Type[] Types => GetTypes();

  public string[] Names => GetNames();

  public Factory() { names = GetNames(); }

  private string[] GetNames() { return names ?? (names = Types.Select(t => t.ToString()).ToArray()); }

  private Type[] GetTypes() {
    return types ?? (types = Assembly.GetAssembly(typeof(T)).GetTypes().Where(
             myType => myType.IsClass && !myType.IsAbstract &&
                       myType.IsSubclassOf(typeof(T)) 
                       //&& !myType.IsSubclassOf(typeof(EffectBase))
           ).ToArray());
  }

  public T Create(Type t) { return Activator.CreateInstance(t) as T; }

}