using System;
using System.Linq;
using System.Reflection;

/// <summary>
/// Marks a concrete runtime type as documentation/template code that should not
/// appear in reflection-built effect, transition, or blender catalogs.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RuntimeCatalogIgnoreAttribute : Attribute
{
}

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
                       myType.IsSubclassOf(typeof(T)) &&
                       !myType.IsDefined(typeof(RuntimeCatalogIgnoreAttribute), false)
           ).OrderBy(myType => myType.FullName, StringComparer.Ordinal).ToArray());
  }

  public T Create(Type t) { return Activator.CreateInstance(t) as T; }

}