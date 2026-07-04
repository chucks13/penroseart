using UnityEngine;

/// <summary>
/// Unity MonoBehaviour singleton helper used by scene-hosted runtime components such as Controller.
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    /// <summary>Returns true when a scene-owned singleton instance has registered itself.</summary>
    public static bool HasInstance => _instance != null;

    /// <summary>Returns the registered singleton instance without creating a GameObject or component.</summary>
    public static bool TryGetInstance(out T instance)
    {
        instance = _instance;
        return instance != null;
    }

    public static T Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            throw new System.InvalidOperationException(
                $"{typeof(T).Name} has no registered scene instance. " +
                $"Add it to the scene before reading Singleton<{typeof(T).Name}>.Instance.");
        }
    }

    /// <summary>
    /// Registers this scene-owned component as the singleton instance and keeps its GameObject alive across scene loads.
    /// </summary>
    protected virtual void Awake()
    {
        var component = this as T;
        if (component == null)
        {
            Debug.LogError($"{GetType().Name} must inherit Singleton<{GetType().Name}> with its own component type.", this);
            return;
        }

        if (_instance != null && _instance != component)
        {
            Debug.LogError($"Duplicate {typeof(T).Name} singleton in scene. Keeping the first registered instance.", this);
            return;
        }

        _instance = component;
        DontDestroyOnLoad(_instance);
    }

    /// <summary>
    /// Editor reset hook that renames the GameObject to the singleton type name.
    /// </summary>
    protected virtual void Reset()
    {
        transform.position = Vector3.zero;
        gameObject.name = typeof(T).Name;
    }

}