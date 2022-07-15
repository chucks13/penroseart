using UnityEngine;
using Random = UnityEngine.Random;
public class DirectionalWipe : TransitionBase {

    float angle;
    float diagonalsize;
  public override void OnStart() { 
        buffer.Clear();
        angle= Random.value*Mathf.PI * 2f;
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

    public override void Draw() {
    controller.effects[A].Draw();
    controller.effects[B].Draw();

    for(int i = 0; i < buffer.Length; i++) {
      Vector2 point = rotate(controller.penrose.tiles[i].position,angle);

      if ((point.x + diagonalsize / 2) * 1f >= diagonalsize * V)
      {
        buffer[i] = controller.effects[A].buffer[i];
      }
      else
      {
        buffer[i] = controller.effects[B].buffer[i];
      }
    }
  }

}