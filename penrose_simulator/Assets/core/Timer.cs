using System;

public class Timer {

  private float seconds;
  private float timeLeft;
  private bool autoReset;

  public Action onFinished;

  public float Seconds => seconds;

  public float Value => 1f - (timeLeft / seconds);

  public Timer(float seconds, bool autoReset = true) {
    this.autoReset = autoReset;
    Set(seconds);
    if(!autoReset) timeLeft = seconds;
  }

  public void Set(float timeInSeconds) {
    seconds = timeInSeconds;
    if (autoReset) Reset();
  }

  public void Reset() {
    timeLeft = seconds;
  }

  public void Update(float deltaTime) {
    timeLeft -= deltaTime;

    if(!(timeLeft < 0f)) return;

    onFinished?.Invoke();
    if(autoReset) Reset();
  }

}
