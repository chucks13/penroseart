using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {

  private static T _instance;
  public static bool HasInstance => _instance != null;

  public static T Instance {
    get {
      if(HasInstance) return _instance;

      Debug.LogFormat("Creating a new {0}!", typeof(T).Name);
      var go = new GameObject(typeof(T).Name, typeof(T)) {name = typeof(T).Name};
      go.AddComponent<T>();
      return _instance;
    }
  }

  protected virtual void Awake() {
    _instance = this as T;
    DontDestroyOnLoad(_instance);
  }

  protected virtual void Reset() {
    transform.position = Vector3.zero;
    gameObject.name    = typeof(T).Name;
  }

}