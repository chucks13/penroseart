using UnityEngine;

public class RandomEffectsMixer : EffectBase {

  private EffectBase[] effects;
  private int total;
  private float percent;

  public override void Draw() {

    for(int i = 0; i < total; i++) {
      effects[i].Draw();
    }

    for(int i = 0; i < buffer.Length; i++) {

<<<<<<< HEAD
=======
  public override void Draw() {

    for(int i = 0; i < total; i++) {
      effects[i].Draw();
    }

    for(int i = 0; i < buffer.Length; i++) {

>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter
      float r = 0f, g = 0f, b = 0f;
      for(int j = 0; j < total; j++) {
        r += effects[j].buffer[i].r * percent;
        g += effects[j].buffer[i].g * percent;
        b += effects[j].buffer[i].b * percent;
      }

      buffer[i] = new Color(r , g , b , 1f);
    }
  }
<<<<<<< HEAD

  private EffectBase GetRandomEffect() {
    var effect = EffectFactory.CreateEffect(EffectFactory.EffectTypes[Random.Range(0,EffectFactory.EffectCount)]);
    if(effect.Name == Name) effect = GetRandomEffect();
    return effect;
  }

  public override void LoadSettings() {
    effects = new EffectBase[Random.Range(2, 4)];
    total = effects.Length;

    var debugText = string.Empty;
    for(var i = 0; i < total; i++) {
      effects[i] = GetRandomEffect();
      effects[i].Init();
      effects[i].LoadSettings();
      debugText += (i < total - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
    }

    controller.debugText.text = debugText;
    percent = (1f / total) * 2f;

  }

  
=======
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter

  private EffectBase GetRandomEffect() {
    var effect = EffectFactory.Create(EffectFactory.Types[Random.Range(0,EffectFactory.Count)]);
    if(effect.Name == Name) effect = GetRandomEffect();
    return effect;
  }

  public override void LoadSettings() {
    effects = new EffectBase[Random.Range(2, 4)];
    total = effects.Length;

    var debugText = string.Empty;
    for(var i = 0; i < total; i++) {
      effects[i] = GetRandomEffect();
      effects[i].Init();
      effects[i].LoadSettings();
      debugText += (i < total - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
    }

    controller.debugText.text = debugText;
    percent = (1f / total) * 2f;

  }

  

}