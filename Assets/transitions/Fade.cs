using UnityEngine;
public class Fade : TransitionBase
{

  public override void OnStart() { buffer.Clear(); }
  public override void OnEnd() { }

  public override void Draw()
  {

    controller.effects[A].Draw();
    controller.effects[B].Draw();
    Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, D);

  }

  private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, float D2)
  {

    for (int i = 0; i < buffer.Length; i++)
    {
      var colorA = src1[i] * D2;
      var colorB = src2[i] * V2;
      dest[i] = colorA + colorB;
    }
  }
  public override void Blend(Color[] dest, Color[] src1, Color[] src2)
  {
    float V2 = 0.5f;
    if (settings.Length > 0)
      V2 = settings[0];
    Draw2(dest, src1, src2, V2, 1f - V2);
  }
  public override string Usage()
  {
    return "[ratio]";
  }


}