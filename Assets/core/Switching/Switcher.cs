using System;
using UnityEngine;

/// <summary>
/// Read-only snapshot of the Mechanical Switcher's current stage state for HUDs and inspectors.
/// </summary>
public readonly struct SwitcherStatus
{
    public static SwitcherStatus NotReady { get; } = new SwitcherStatus(
        false,
        -1,
        string.Empty,
        -1,
        string.Empty,
        -1,
        string.Empty,
        -1,
        string.Empty,
        string.Empty,
        0f);

    public readonly bool Ready;
    public readonly int CurrentEffectIndex;
    public readonly string CurrentEffectName;
    public readonly int SourceEffectIndex;
    public readonly string SourceEffectName;
    public readonly int TargetEffectIndex;
    public readonly string TargetEffectName;
    public readonly int CurrentTransitionIndex;
    public readonly string CurrentTransitionName;
    public readonly string StageName;
    public readonly float TransitionProgress;

    public SwitcherStatus(
        bool ready,
        int currentEffectIndex,
        string currentEffectName,
        int sourceEffectIndex,
        string sourceEffectName,
        int targetEffectIndex,
        string targetEffectName,
        int currentTransitionIndex,
        string currentTransitionName,
        string stageName,
        float transitionProgress)
    {
        Ready = ready;
        CurrentEffectIndex = currentEffectIndex;
        CurrentEffectName = currentEffectName;
        SourceEffectIndex = sourceEffectIndex;
        SourceEffectName = sourceEffectName;
        TargetEffectIndex = targetEffectIndex;
        TargetEffectName = targetEffectName;
        CurrentTransitionIndex = currentTransitionIndex;
        CurrentTransitionName = currentTransitionName;
        StageName = stageName;
        TransitionProgress = Mathf.Clamp01(transitionProgress);
    }
}

/// <summary>
/// Timing context for a started A-to-B Transition.
/// </summary>
public readonly struct TransitionStartTiming
{
    private readonly bool useBeatDuration;
    private readonly float secondsPerBeat;

    private TransitionStartTiming(float startTime, bool useBeatDuration, float secondsPerBeat)
    {
        if (useBeatDuration && secondsPerBeat <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(secondsPerBeat), secondsPerBeat, "Seconds per beat must be positive.");
        }

        StartTime = startTime;
        this.useBeatDuration = useBeatDuration;
        this.secondsPerBeat = secondsPerBeat;
    }

    /// <summary>Unity time when the transition should be considered started.</summary>
    public float StartTime { get; }

    /// <summary>Creates timing for beat-denominated Synced Mode execution.</summary>
    public static TransitionStartTiming FromBeatClock(float startTime, float secondsPerBeat)
    {
        return new TransitionStartTiming(startTime, true, secondsPerBeat);
    }

    /// <summary>Creates timing for Standalone Mode execution using the transition's default duration.</summary>
    public static TransitionStartTiming FromDefaultDuration(float startTime)
    {
        return new TransitionStartTiming(startTime, false, 0f);
    }

    /// <summary>Resolves the active execution duration from the selected transition's repertoire.</summary>
    public float DurationSeconds(TransitionRepertoire repertoire)
    {
        var durationSeconds = useBeatDuration
            ? repertoire.DurationBeats * secondsPerBeat
            : repertoire.DefaultDurationSeconds;
        if (durationSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(repertoire), durationSeconds, "Transition duration cannot be negative.");
        }

        return durationSeconds;
    }
}

/// <summary>
/// Beat-domain cue direction cast into the Mechanical Switcher by the Director.
/// </summary>
public readonly struct SwitcherCueDirection
{
    public readonly int CueMarkBeat;
    public readonly int TargetEffectIndex;
    public readonly int TransitionIndex;
    public readonly TransitionRepertoire TransitionRepertoire;

    public SwitcherCueDirection(
        int cueMarkBeat,
        int targetEffectIndex,
        int transitionIndex,
        TransitionRepertoire transitionRepertoire)
    {
        CueMarkBeat = cueMarkBeat;
        TargetEffectIndex = targetEffectIndex;
        TransitionIndex = transitionIndex;
        TransitionRepertoire = transitionRepertoire;
    }
}

/// <summary>
/// Plain beat-clock facts the Switcher needs to time a cast cue against the wall clock.
/// </summary>
public readonly struct SwitcherClockSnapshot
{
    public readonly int CurrentBeat;
    public readonly float BeatFraction;
    public readonly float SecondsPerBeat;
    public readonly float NowSeconds;

    public SwitcherClockSnapshot(int currentBeat, float beatFraction, float secondsPerBeat, float nowSeconds)
    {
        if (secondsPerBeat <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(secondsPerBeat), secondsPerBeat, "Seconds per beat must be positive.");
        }

        CurrentBeat = currentBeat;
        BeatFraction = Mathf.Clamp01(beatFraction);
        SecondsPerBeat = secondsPerBeat;
        NowSeconds = nowSeconds;
    }
}

/// <summary>
/// Mechanical stage switcher for Penrose performers. Its contract is one sentence: take a cast cue and
/// execute it at its beats, then promote the destination on completion (ADR-0019). The Switcher renders
/// in-flight effect/transition progress and owns Runway/impact/tail timing; it holds no loaded-cue lifecycle,
/// no Lock Point, and no accept/reject verdict — a cast fires unconditionally.
/// </summary>
[Serializable]
public sealed class Switcher
{
    private readonly Controller controller;
    private readonly EffectBase[] effects;
    private readonly TransitionBase[] transitions;

    private int currentEffectIndex = -1;
    private int currentTransitionIndex = -1;
    private bool isTransitioning;
    private float transitionStartTime;
    private float transitionDurationSeconds = 1f;
    private float transitionProgress;

    /// <summary>Currently active effect index, or -1 while a transition owns the frame.</summary>
    public int CurrentEffectIndex => isTransitioning ? -1 : currentEffectIndex;

    /// <summary>Active transition index while a transition owns the frame; otherwise -1.</summary>
    public int CurrentTransitionIndex => isTransitioning ? currentTransitionIndex : -1;

    /// <summary>The destination effect while transitioning, otherwise the currently active effect.</summary>
    public int TransitionTargetEffectIndex => isTransitioning ? transitions[currentTransitionIndex].B : currentEffectIndex;

    /// <summary>Display name for the effect or transition currently on stage.</summary>
    public string CurrentName => isTransitioning ? transitions[currentTransitionIndex].Name : effects[currentEffectIndex].Name;

    /// <summary>Current read-only mechanical stage snapshot for runtime HUDs and inspector diagnostics.</summary>
    public SwitcherStatus Status => BuildStatus();

    public Switcher(Controller controller, EffectBase[] effects, TransitionBase[] transitions)
    {
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        this.controller = controller;
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        this.transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
    }

    /// <summary>
    /// Seeds the Switcher from Controller startup state. The effect is assumed to have already run OnStart().
    /// </summary>
    public void SetInitialEffect(int effectIndex, int transitionIndex)
    {
        ValidateEffectIndex(effectIndex);
        ValidateTransitionIndex(transitionIndex);

        currentEffectIndex = effectIndex;
        currentTransitionIndex = transitionIndex;
        isTransitioning = false;
        transitionProgress = 0f;
        Trace(() => $"SWITCHER_INIT current={FormatEffect(effectIndex)} nextTransition={FormatTransition(transitionIndex)}");
    }

    /// <summary>Immediately puts an effect on stage, cancelling any in-flight transition.</summary>
    public void ShowNow(int effectIndex)
    {
        ValidateEffectIndex(effectIndex);

        EffectBase.APalette.Change();
        isTransitioning = false;
        transitionProgress = 0f;
        currentEffectIndex = effectIndex;
        StartEffect(effectIndex);
        Trace(() => $"SWITCHER_SHOW_NOW current={FormatEffect(effectIndex)}");
    }

    /// <summary>
    /// Casts one beat-domain cue for fire-and-forget execution, unconditionally. The cue's Transition
    /// starts on its Runway beat (<c>Cue Mark − Runway</c>), its Impact Point lands on the Cue Mark beat,
    /// and its Tail completes after. There is no loaded cue, no Lock Point, and no verdict: a cast arriving
    /// at Runway start runs the full Runway; a late cast still fires, beginning already underway so its
    /// Impact still lands on the mark — a compressed Runway. The Switcher never holds the cue to wait: the
    /// Runway begins now, or is already past, never in the future. Decide-at-cast callers read the current
    /// sheet and cast at the last responsible moment; the Switcher trusts the cast and executes it.
    /// </summary>
    /// <param name="cue">The impact-beat cue: target effect, transition, and that transition's repertoire.</param>
    /// <param name="clock">The canonical beat clock the Runway start is timed against.</param>
    public void Cast(SwitcherCueDirection cue, SwitcherClockSnapshot clock)
    {
        ValidateEffectIndex(cue.TargetEffectIndex);
        ValidateTransitionIndex(cue.TransitionIndex);

        var repertoire = cue.TransitionRepertoire;
        var runwayStartBeat = cue.CueMarkBeat - repertoire.RunwayBeats;
        // Never later than now: an on-time cast starts on its Runway beat; a late cast starts already
        // underway (Runway compressed); the cue is never parked to wait for a future beat.
        var runwayStartTime = Mathf.Min(TimeAtBeat(clock, runwayStartBeat), clock.NowSeconds);

        StartTransition(
            cue.TargetEffectIndex,
            cue.TransitionIndex,
            TransitionStartTiming.FromBeatClock(runwayStartTime, clock.SecondsPerBeat),
            clock.NowSeconds,
            repertoire);
        Trace(() => $"SWITCHER_CAST cueMark={cue.CueMarkBeat} runwayStart={runwayStartBeat} startTime={runwayStartTime:0.###} now={clock.NowSeconds:0.###} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.TargetEffectIndex)}");
    }

    /// <summary>
    /// Starts or replaces a transition from the current stage destination to the target effect.
    /// The Switcher owns progress and completion after this call; if another transition is still
    /// rendering, the previous destination becomes the source for this new last-command-wins move.
    /// </summary>
    public void StartTransition(int targetEffectIndex, int transitionIndex, TransitionStartTiming timing)
    {
        ValidateTransitionIndex(transitionIndex);
        StartTransition(targetEffectIndex, transitionIndex, timing, Time.time, transitions[transitionIndex].Repertoire);
    }

    /// <summary>Starts one fully resolved transition and defers its diagnostic display reads.</summary>
    private void StartTransition(
        int targetEffectIndex,
        int transitionIndex,
        TransitionStartTiming timing,
        float progressNowSeconds,
        TransitionRepertoire repertoire)
    {
        var sourceEffectIndex = isTransitioning ? transitions[currentTransitionIndex].B : currentEffectIndex;
        ValidateEffectIndex(sourceEffectIndex);
        ValidateEffectIndex(targetEffectIndex);
        ValidateTransitionIndex(transitionIndex);

        var transition = transitions[transitionIndex];
        transition.RandomizeTime();
        transition.V = 0f;
        transition.A = sourceEffectIndex;
        transition.B = targetEffectIndex;
        transition.OnStart();

        EffectBase.APalette.Change();
        StartEffect(targetEffectIndex);

        currentTransitionIndex = transitionIndex;
        currentEffectIndex = -1;
        transitionStartTime = timing.StartTime;
        transitionDurationSeconds = timing.DurationSeconds(repertoire);
        transitionProgress = ProgressAt(progressNowSeconds);
        isTransitioning = true;
        Trace(() => $"SWITCHER_START transition={FormatTransition(transitionIndex)} source={FormatEffect(sourceEffectIndex)} target={FormatEffect(targetEffectIndex)} A={transition.A} B={transition.B} durationSeconds={transitionDurationSeconds:0.###} progress={transitionProgress:0.###}");
        if (transitionDurationSeconds == 0f)
        {
            CompleteTransition();
        }
    }

    /// <summary>
    /// Renders the active effect or transition at the supplied Unity time into a cloned 900-tile buffer.
    /// </summary>
    public Color[] RenderAtTime(float nowSeconds, out string debugText)
    {
        if (isTransitioning)
        {
            transitionProgress = ProgressAt(nowSeconds);
            if (transitionProgress >= 1f)
            {
                CompleteTransition();
            }
        }

        if (isTransitioning)
        {
            var transition = transitions[currentTransitionIndex];
            transition.V = transitionProgress;
            transition.UpdateTime();

            var indexA = transition.A;
            var indexB = transition.B;

            effects[indexA].UpdateTime();
            if (indexA != indexB)
            {
                effects[indexB].UpdateTime();
            }

            transition.Draw();
            debugText = transition.DebugText();
            return (Color[])transition.buffer.Clone();
        }

        ValidateEffectIndex(currentEffectIndex);
        var effect = effects[currentEffectIndex];
        effect.UpdateTime();
        effect.Draw();
        debugText = effect.DebugText();
        return (Color[])effect.buffer.Clone();
    }

    private static float TimeAtBeat(SwitcherClockSnapshot clock, int beat)
    {
        var beatsUntil = beat - clock.CurrentBeat - clock.BeatFraction;
        return clock.NowSeconds + (beatsUntil * clock.SecondsPerBeat);
    }

    /// <summary>Promotes the transition target and emits the deferred completion trace.</summary>
    private void CompleteTransition()
    {
        var transition = transitions[currentTransitionIndex];
        var completedTransitionIndex = currentTransitionIndex;
        var sourceEffectIndex = transition.A;
        var targetEffectIndex = transition.B;
        currentEffectIndex = targetEffectIndex;
        isTransitioning = false;
        transitionProgress = 0f;
        Trace(() => $"SWITCHER_COMPLETE transition={FormatTransition(completedTransitionIndex)} source={FormatEffect(sourceEffectIndex)} current={FormatEffect(currentEffectIndex)} targetWas={targetEffectIndex}");
    }

    private float ProgressAt(float now)
    {
        return transitionDurationSeconds == 0f
            ? 1f
            : Mathf.Clamp01((now - transitionStartTime) / transitionDurationSeconds);
    }

    private SwitcherStatus BuildStatus()
    {
        if (effects == null || transitions == null)
        {
            return SwitcherStatus.NotReady;
        }

        if (isTransitioning)
        {
            var transition = transitions[currentTransitionIndex];
            var sourceName = EffectName(transition.A);
            var targetName = EffectName(transition.B);
            return new SwitcherStatus(
                true,
                -1,
                string.Empty,
                transition.A,
                sourceName,
                transition.B,
                targetName,
                currentTransitionIndex,
                transition.Name,
                transition.Name,
                transitionProgress);
        }

        var currentName = EffectName(currentEffectIndex);
        return new SwitcherStatus(
            currentEffectIndex >= 0,
            currentEffectIndex,
            currentName,
            currentEffectIndex,
            currentName,
            currentEffectIndex,
            currentName,
            -1,
            string.Empty,
            currentName,
            0f);
    }

    private string EffectName(int effectIndex)
    {
        return effectIndex >= 0 && effectIndex < effects.Length ? effects[effectIndex].Name : string.Empty;
    }

    private string FormatEffect(int effectIndex)
    {
        return effectIndex >= 0 && effectIndex < effects.Length ? $"{effectIndex}:{effects[effectIndex].Name}" : $"{effectIndex}:<none>";
    }

    private string FormatTransition(int transitionIndex)
    {
        return transitionIndex >= 0 && transitionIndex < transitions.Length ? $"{transitionIndex}:{transitions[transitionIndex].Name}" : $"{transitionIndex}:<none>";
    }

    /// <summary>Writes a Switcher trace whose display values are resolved only when tracing is enabled.</summary>
    private void Trace(Func<string> message)
    {
        controller.LogDirectorSwitching(message);
    }

    private void StartEffect(int effectIndex)
    {
        var effect = effects[effectIndex];
        effect.RandomizeTime();
        effect.OnStart();
    }

    private void ValidateEffectIndex(int effectIndex)
    {
        if (effectIndex < 0 || effectIndex >= effects.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(effectIndex), effectIndex, "Effect index is outside the runtime catalog.");
        }
    }

    private void ValidateTransitionIndex(int transitionIndex)
    {
        if (transitionIndex < 0 || transitionIndex >= transitions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(transitionIndex), transitionIndex, "Transition index is outside the runtime catalog.");
        }
    }
}
