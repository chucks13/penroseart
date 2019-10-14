public class Fade : TransitionBase {

  public override void Draw() {
   
    controller.effects[A].Draw();
    controller.effects[B].Draw();


    for(int i = 0; i < buffer.Length; i++) {
      var colorA = controller.effects[A].buffer[i] * D;
      var colorB = controller.effects[B].buffer[i] * V;
      buffer[i] = colorA + colorB;
    }

  }

}