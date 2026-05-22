using System;

/// <summary>
/// Plain C# countdown/progress timer used by Controller for effect and transition phases.
/// </summary>
public class Timer
{

    private float seconds;
    private float timeLeft;
    private bool autoReset;

    /// <summary>Callback invoked when the timer reaches zero.</summary>
    public Action onFinished;

    /// <summary>Configured timer duration in seconds.</summary>
    public float Seconds => seconds;

    /// <summary>Normalized progress from 0 at reset to 1 at expiry.</summary>
    public float Value => 1f - (timeLeft / seconds);

    /// <summary>
    /// Creates a timer with a target duration and optional automatic reset after firing.
    /// </summary>
    public Timer(float seconds, bool autoReset = true)
    {
        this.autoReset = autoReset;
        Set(seconds);
        if (!autoReset) timeLeft = seconds;
    }

    /// <summary>
    /// Changes the timer duration. Auto-reset timers restart immediately.
    /// </summary>
    public void Set(float timeInSeconds)
    {
        seconds = timeInSeconds;
        if (autoReset) Reset();
    }

    /// <summary>
    /// Restores the remaining time to the configured duration.
    /// </summary>
    public void Reset()
    {
        timeLeft = seconds;
    }

    /// <summary>
    /// Advances the timer and invokes <see cref="onFinished"/> on expiry.
    /// </summary>
    public void Update(float deltaTime)
    {
        timeLeft -= deltaTime;

        if (!(timeLeft < 0f)) return;

        onFinished?.Invoke();
        if (autoReset) Reset();
    }

}
