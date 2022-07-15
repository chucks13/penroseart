using UnityEngine;

public class RGBFade : TransitionBase {

  public override void OnStart() { buffer.Clear(); }
  public override void OnEnd() {  }

  public override void Draw() {
   
    controller.effects[A].Draw();
    controller.effects[B].Draw();

    for(int i = 0; i < buffer.Length; i++) {
      var DR = Mathf.Clamp01(D * 3);
      var VR = 1f - DR;
      var DG = Mathf.Clamp01(Mathf.Clamp01(D - 0.333f) * 3);
      var VG = 1f - DG;
      var DB = Mathf.Clamp01(Mathf.Clamp01(D - 0.666f) * 3);
      var VB = 1f - DB;
      buffer[i].r = controller.effects[A].buffer[i].r * DR + controller.effects[B].buffer[i].r * VR;
      buffer[i].g = controller.effects[A].buffer[i].g * DG + controller.effects[B].buffer[i].g * VG;
      buffer[i].b = controller.effects[A].buffer[i].b * DB + controller.effects[B].buffer[i].b * VB;
    }

  }

}