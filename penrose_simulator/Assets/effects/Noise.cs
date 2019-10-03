using UnityEngine;
using Random = UnityEngine.Random;

public class Noise : Effect {


  private Settings setting;

  //private float scale;
  //private float speed;
  //private float amplifier;
  //private float colorDelta;
  private float timeScale;

  public override void Init() {
    base.Init();
    setting = new Settings();

    //scale = Random.Range(0.05f, 0.2f);
    //speed = Random.Range(0.1f, 1.5f);
    //amplifier  = Random.Range(1, 5);
    //colorDelta = Random.value;
    //
    //Debug.Log($"Scale: {scale}, Speed: {speed}, Amplifier: {amplifier}, ColorDelta: {colorDelta}");
  }
  public override void Draw() {
    TileData[] tiles = controller.geometry.tileData;
    for(int i = 0; i < buffer.Length; i++) {
      float n = Perlin.Noise(tiles[i].center.x * setting.scale, tiles[i].center.y * setting.scale, Time.fixedTime * setting.speed);
      n *= setting.amplifier;

      int v = (int)n;
      if((v & 1) == 0)
        buffer[i] = Color.HSVToRGB((n + setting.colorDelta) % 1, 1, 1);
      else
        buffer[i] = Color.black;
    }
  }

  public override void LoadSettings() {
    if(controller.noiseSettings.Length > 0) {
      setting = controller.noiseSettings[Random.Range(0, controller.noiseSettings.Length)];
    } else {
      setting.Randomize();
    }

    controller.debugText.text = $"Scale: {setting.scale}\nSpeed: {setting.speed}\nAmp: {setting.amplifier}\nDelta: {setting.colorDelta}";
  }

  [System.Serializable]
  public class Settings {
    public float scale;
    public float speed;
    public float amplifier;
    public float colorDelta;

    public void Randomize() {
      scale      = Random.Range(0.05f, 0.2f);
      speed      = Random.Range(0.1f, 1.5f);
      amplifier  = Random.Range(1, 5);
      colorDelta = Random.value;
    }
  }

}