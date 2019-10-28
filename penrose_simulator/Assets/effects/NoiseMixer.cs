using UnityEngine;

public class NoiseMixer : EffectBase {

  private EffectBase[] effects;
  private Factory<EffectBase> factory;
  private Color border;

  public override string DebugText() {
    var debugText = string.Empty;
    for(var i = 0; i < 2; i++) {
      debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
    }

    return debugText;
  }

  public override void Init() {
    base.Init();
    factory = new Factory<EffectBase>();
  }

  private EffectBase GetRandomEffect() {
    var effect = factory.Create(factory.Types[Random.Range(0, factory.Count)]);
    return effect.Name == Name ? GetRandomEffect() : effect;
  }

  public override void OnStart() {
    effects = new EffectBase[2];

    var debugText = string.Empty;
    for(var i = 0; i < 2; i++) {
      effects[i] = GetRandomEffect();
      effects[i].Init();
      effects[i].OnStart();
      debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
      border    =  Color.HSVToRGB(Random.value, 1, 1);
    }

    controller.debugText.text = debugText;
  }

  public override void OnEnd() { }

  public override void Draw() {
    for(int i = 0; i < 2; i++) {
      effects[i].Draw();
    }

    for(int i = 0; i < buffer.Length; i++) {
      float scale = 0.07f; //(1.0f + (controller.dance.decay * 0.25f)) * setting.scale;
      float x     = tiles[i].center.x * scale;
      float y     = tiles[i].center.y * scale;
      float z     = controller.dance.fixedTime; // * setting.speed;

      float n = Perlin.Noise(x, y, z);
      if(n > 0.1)
        buffer[i] = effects[0].buffer[i];
      else if(n > -0.1)
        buffer[i] = border;
      else
        buffer[i] = effects[1].buffer[i];
    }
  }

}