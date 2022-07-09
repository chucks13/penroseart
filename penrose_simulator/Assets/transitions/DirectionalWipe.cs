
public class DirectionalWipe : TransitionBase {

  public override void OnStart() { buffer.Clear(); }

  public override void OnEnd() { }

  public override void Draw() {
    controller.effects[A].Draw();
    controller.effects[B].Draw();
    var width = (int)controller.penrose.Bounds.size.x.Round();
    var height = (int)controller.penrose.Bounds.size.y.Round();

    for(int i = 0; i < buffer.Length; i++) {
      if ((controller.penrose.tiles[i].position.x + width / 2) * 1f >= width * V)
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