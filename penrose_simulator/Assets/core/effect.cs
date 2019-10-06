using UnityEngine;

[System.Serializable]
public abstract class Effect {

  [HideInInspector]
  public Color[] buffer;

  protected Controller controller;

  protected virtual void Fade(int index) {
    buffer[index] *= 0.98f;
  }

  protected void FadeAll() {
    for(var i = 0; i < buffer.Length; i++)
      Fade(i);
  }

  protected virtual void Clear(int index) {
    buffer[index] = Color.black;
  }

  protected void ClearAll() {
    for(var i = 0; i < buffer.Length; i++) {
      Clear(i);
    }
  }

  public virtual void Init() {
    controller = Controller.Instance;
    buffer = new Color[600];
  }
  public abstract void Draw();

  public abstract void LoadSettings();
  
}