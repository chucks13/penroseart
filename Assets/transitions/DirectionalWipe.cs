using UnityEngine;
using Random = UnityEngine.Random;
public class DirectionalWipe : TransitionBase
{

  float angle;
  float diagonalsize;
  public override void OnStart()
  {
    buffer.Clear();
    angle = Random.value * Mathf.PI * 2f;
    var width = (int)controller.penrose.Bounds.size.x.Round();
    var height = (int)controller.penrose.Bounds.size.y.Round();
    Vector2 diagonal = new Vector2(width, height);
    diagonalsize = diagonal.magnitude;
  }

  public override void OnEnd() { }

  public static Vector2 rotate(Vector2 v, float delta)
  {
    return new Vector2(
        v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
        v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
    );
  }

  public override void Draw()
  {
    controller.effects[A].Draw();
    controller.effects[B].Draw();
    Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, angle);
  }

  private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, float Angle2)
  {

    for (int i = 0; i < buffer.Length; i++)
    {
      Vector2 point = rotate(controller.penrose.tiles[i].position, Angle2);

      if ((point.x + diagonalsize / 2) * 1f >= diagonalsize * V2)
      {
        dest[i] = src1[i];
      }
      else
      {
        dest[i] = src2[i];
      }
    }
  }

  public override void Blend(Color[] dest, Color[] src1, Color[] src2)
  {
    float V2 = 0.5f;
    float Angle2 = 0;
    if (settings.Length > 0)
      V2 = settings[0];
    if (settings.Length > 1)
      Angle2 = settings[1];
    Draw2(dest, src1, src2, V2, Angle2);
  }
  public override string Usage()
  {
    return "[ratio] [angle (radians)]";
  }


}