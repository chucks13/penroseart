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

/// <summary>
/// Reflection-backed runtime catalog builder for effects, transitions, and blenders.
/// </summary>
/// <typeparam name="T">Base class used to select concrete catalog entries.</typeparam>
public class Factory<T> where T : class
{

    private Type[] types;
    private string[] names;

    /// <summary>Number of concrete, non-ignored catalog types.</summary>
    public int Count => GetTypes().Length;
    /// <summary>Sorted concrete, non-ignored catalog types.</summary>
    public Type[] Types => GetTypes();

    /// <summary>Display names for the sorted catalog types.</summary>
    public string[] Names => GetNames();

    public Factory() { names = GetNames(); }

    private string[] GetNames() { return names ?? (names = Types.Select(t => t.ToString()).ToArray()); }

    private Type[] GetTypes()
    {
        return types ?? (types = Assembly.GetAssembly(typeof(T)).GetTypes().Where(
                 myType => myType.IsClass && !myType.IsAbstract &&
                           myType.IsSubclassOf(typeof(T)) &&
                           !myType.IsDefined(typeof(RuntimeCatalogIgnoreAttribute), false)
               ).OrderBy(myType => myType.FullName, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Creates a catalog instance. Catalog entries must have parameterless constructors.
    /// </summary>
    public T Create(Type t) { return Activator.CreateInstance(t) as T; }

}