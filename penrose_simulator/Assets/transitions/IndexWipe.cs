using System.Collections;
using System.Collections.Generic;
using UnityEngine;
<<<<<<< HEAD

public class IndexWipe : Transition {

  private int idx;
  private int last;

=======

public class IndexWipe : TransitionBase {

  private int idx;
  private int last;

>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter
  public override void Init() {
    base.Init();
    idx = Mathf.CeilToInt(Penrose.Total / controller.transitionTime / Application.targetFrameRate) + 1;
  }

  public override void Draw() {

<<<<<<< HEAD
    ClearAll();
=======
    //ClearAll();
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter

    controller.effects[A].Draw();
    controller.effects[B].Draw();

    var total = (int)(Penrose.Total * V);

    
    for(int i = total; i < Penrose.Total; i++) {
      buffer[i] = controller.effects[A].buffer[i];
    }

    for(int j = 0; j < total; j++) {
      buffer[j] = controller.effects[B].buffer[j];
    }

<<<<<<< HEAD
    
    controller.effectText.text =
      $"{controller.effects[A].Name} ({Delta:0.00}) => {controller.effects[B].Name} ({V:0.00}) ({total}, {Penrose.Total - total})";

  }
  public override void LoadSettings() {  }
=======
  }
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter
}
