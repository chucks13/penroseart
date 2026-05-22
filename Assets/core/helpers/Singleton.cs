using UnityEngine;

/// <summary>
/// Unity MonoBehaviour singleton helper used by scene-hosted runtime components such as Controller.
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{

    private static T _instance;
    public static bool HasInstance => _instance != null;

    public static T Instance
    {
        get
        {
            if (HasInstance) return _instance;

            Debug.LogFormat("Creating a new {0}!", typeof(T).Name);
            var go = new GameObject(typeof(T).Name, typeof(T)) { name = typeof(T).Name };
            go.AddComponent<T>();
            return _instance;
        }
    }

    /// <summary>
    /// Registers this component as the singleton instance and keeps its GameObject alive across scene loads.
    /// </summary>
    protected virtual void Awake()
    {
        _instance = this as T;
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