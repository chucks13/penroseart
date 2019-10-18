using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IndexWipe : Transition {

  private int idx;
  private int last;

  public override void Init() {
    base.Init();
    idx = Mathf.CeilToInt(Penrose.Total / controller.transitionTime / Application.targetFrameRate) + 1;
  }

  public override void Draw() {

    ClearAll();

    controller.effects[A].Draw();
    controller.effects[B].Draw();

    var total = (int)(Penrose.Total * V);

    
    for(int i = total; i < Penrose.Total; i++) {
      buffer[i] = controller.effects[A].buffer[i];
    }

    for(int j = 0; j < total; j++) {
      buffer[j] = controller.effects[B].buffer[j];
    }

    
    controller.effectText.text =
      $"{controller.effects[A].Name} ({Delta:0.00}) => {controller.effects[B].Name} ({V:0.00}) ({total}, {Penrose.Total - total})";

  }
  public override void LoadSettings() {  }
}
