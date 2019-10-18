
public class IndexWipe : TransitionBase {

  public override void OnStart() { buffer.Clear(); }

  public override void OnEnd() { }

  public override void Draw() {
    controller.effects[A].Draw();
    controller.effects[B].Draw();

    var total = (int)(Penrose.Total * V);

    for(int i = total; i < Penrose.Total; i++) {
      buffer[i] = controller.effects[A].buffer[i];
    }

    for(int j = 0; j < total; j++) {
      buffer[j] = controller.effects[B].buffer[j];
    }
  }

}