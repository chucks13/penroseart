<<<<<<< HEAD
﻿public class Fade : Transition {
=======
﻿public class Fade : TransitionBase {
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter

  public override void Draw() {
   
    controller.effects[A].Draw();
    controller.effects[B].Draw();


    for(int i = 0; i < buffer.Length; i++) {
      var colorA = controller.effects[A].buffer[i] * Delta;
      var colorB = controller.effects[B].buffer[i] * V;
      buffer[i] = colorA + colorB;
    }

    controller.effectText.text =
      $"{controller.effects[A].Name} ({Delta:0.00}) => {controller.effects[B].Name} ({V:0.00})";
  }

  public override void LoadSettings() { }

}