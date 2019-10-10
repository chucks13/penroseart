public class Fade : Transition {

  public override void Draw() {
    if(A == null || B == null) return;

    for(int i = 0; i < buffer.Length; i++) {
      buffer[i] = A.buffer[i] * (1 - V) + (B.buffer[i] * V);
    }
  }

  public override void LoadSettings() { }

}