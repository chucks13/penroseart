using UnityEngine;
public class IndexWipe : TransitionBase
{

  public override void OnStart() { buffer.Clear(); }

  public override void OnEnd() { }

  public override void Draw()
  {
    controller.effects[A].Draw();
    controller.effects[B].Draw();
    Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V);

  }

  private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2)
  {
    var total = (int)(Penrose.Total * V2);

    for (int i = total; i < Penrose.Total; i++)
    {
      dest[i] = src1[i];
    }

    for (int j = 0; j < total; j++)
    {
      dest[j] = src2[j];
    }
  }
  public override void Blend(Color[] dest, Color[] src1, Color[] src2)
  {
    //        OnInit();
    float V2 = 0.5f;
    if (settings.Length > 0)
      V2 = settings[0];
    Draw2(dest, src1, src2, V2);
  }
  public override string Usage()
  {
    return "[ratio]";
  }



}