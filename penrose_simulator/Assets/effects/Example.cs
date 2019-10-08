using Random = UnityEngine.Random;
using UnityEngine;

public class Example : EffectBase {

  // by using a settings data class you can change settings on the fly
  private Settings setting;
  
  // Init is run once when program starts
  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  // LoadSettings is run every time the effect is chosen by the EffectController
  public override void LoadSettings() { }

  // Draw is called every frame
  public override void Draw() { }

  // Settings class for the effect
  // don't forget to add in the EffectController as well as it's name to EffectTypes (also in the controller)
  [System.Serializable]
  public class Settings {
    
    // Should have a randomize method
    public void Randomize() { }
  }
}