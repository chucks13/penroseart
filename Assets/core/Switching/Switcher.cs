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
/// Beat-domain cue direction inserted into the Mechanical Switcher by the Director.
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
/// Plain beat-clock facts the Switcher needs to lock and execute an inserted cue.
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
/// Read-only snapshot of the Switcher-held cue lifecycle.
/// </summary>
public readonly struct SwitcherCueStatus
{
    public static SwitcherCueStatus Empty { get; } = new SwitcherCueStatus(false, false, -1, -1, -1, -1, -1, -1, 0, 0);

    public readonly bool HasCue;
    public readonly bool IsLocked;
    public readonly int CueMarkBeat;
    public readonly int TargetEffectIndex;
    public readonly int TransitionIndex;
    public readonly int LockPointBeat;
    public readonly int StartBeat;
    public readonly int CompleteBeat;
    public readonly int RunwayBeats;
    public readonly int TailBeats;

    public SwitcherCueStatus(
        bool hasCue,
        bool isLocked,
        int cueMarkBeat,
        int targetEffectIndex,
        int transitionIndex,
        int lockPointBeat,
        int startBeat,
        int completeBeat,
        int runwayBeats,
        int tailBeats)
    {
        HasCue = hasCue;
        IsLocked = isLocked;
        CueMarkBeat = cueMarkBeat;
        TargetEffectIndex = targetEffectIndex;
        TransitionIndex = transitionIndex;
        LockPointBeat = lockPointBeat;
        StartBeat = startBeat;
        CompleteBeat = completeBeat;
        RunwayBeats = runwayBeats;
        TailBeats = tailBeats;
    }

    public bool CanUpdate => HasCue && !IsLocked;
}

/// <summary>
/// Mechanical stage switcher for Penrose performers.
/// The Switcher owns Loaded Cue scheduling plus in-flight effect/transition execution,
/// renders progress, and promotes B on completion.
/// </summary>
[Serializable]
public sealed class Switcher
{
    private readonly Controller controller;
    private readonly EffectBase[] effects;
    private readonly TransitionBase[] transitions;
    private readonly CueLog cueLog;

    private int currentEffectIndex = -1;
    private int currentTransitionIndex = -1;
    private bool isTransitioning;
    private float transitionStartTime;
    private float transitionDurationSeconds = 1f;
    private float transitionProgress;
    private bool hasLoadedCue;
    private SwitcherCueDirection loadedCue;
    private int loadedCueStartBeat;
    private int loadedCueCompleteBeat;
    private int loadedCueLockPointBeat;
    private bool loadedCueLocked;
    private float loadedCueStartTime;
    private float loadedCueSecondsPerBeat;

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

    /// <summary>Current read-only Loaded Cue lifecycle snapshot.</summary>
    public SwitcherCueStatus LoadedCueStatus => BuildLoadedCueStatus();

    public Switcher(Controller controller, EffectBase[] effects, TransitionBase[] transitions, CueLog cueLog = null)
    {
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        this.controller = controller;
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        this.transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        this.cueLog = cueLog;
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
        ClearLoadedCue();
        Trace($"SWITCHER_INIT current={FormatEffect(effectIndex)} nextTransition={FormatTransition(transitionIndex)}");
    }

    /// <summary>Immediately puts an effect on stage, cancelling any in-flight transition.</summary>
    public void ShowNow(int effectIndex)
    {
        ValidateEffectIndex(effectIndex);

        EffectBase.APalette.Change();
        isTransitioning = false;
        transitionProgress = 0f;
        ClearLoadedCue();
        currentEffectIndex = effectIndex;
        StartEffect(effectIndex);
        Trace($"SWITCHER_SHOW_NOW current={FormatEffect(effectIndex)}");
    }

    /// <summary>
    /// Inserts or updates one beat-domain cue direction for fire-and-forget Switcher execution and
    /// answers whether the offer was accepted. A cue must arrive strictly before its Lock Point (one
    /// beat before the Runway start); an offer at or past the Lock Point, or any differing offer once
    /// the loaded cue has locked, is rejected and leaves the loaded cue and stage untouched. Callers
    /// therefore never mirror or guess commitment state — the Switcher alone owns it.
    /// </summary>
    /// <returns><c>true</c> when the cue is loaded; <c>false</c> when the offer is rejected.</returns>
    public bool UpsertLoadedCue(SwitcherCueDirection cue, SwitcherClockSnapshot clock)
    {
        ValidateEffectIndex(cue.TargetEffectIndex);
        ValidateTransitionIndex(cue.TransitionIndex);

        StartLoadedCueIfDue(clock.NowSeconds);

        if (hasLoadedCue)
        {
            LatchLockAtBeat(clock.CurrentBeat);
            if (loadedCueLocked)
            {
                if (!SameCue(cue, loadedCue))
                {
                    Trace($"SWITCHER_IGNORE_LOCKED_CUE cueMark={cue.CueMarkBeat} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.TargetEffectIndex)} lockedCueMark={loadedCue.CueMarkBeat}");
                }

                return false;
            }
        }

        if (!CanCommitCue(cue.CueMarkBeat, cue.TransitionRepertoire, clock.CurrentBeat))
        {
            Trace($"SWITCHER_REJECT_LATE_CUE beat={clock.CurrentBeat} cueMark={cue.CueMarkBeat} lock={LockPointBeatFor(cue.CueMarkBeat, cue.TransitionRepertoire)} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.TargetEffectIndex)}");
            return false;
        }

        LoadCue(cue, clock);
        return true;
    }

    /// <summary>
    /// Whether a cue for this Cue Mark and transition can still commit on this beat: strictly before its
    /// Lock Point. Runway/tail/lock arithmetic is the Switcher's alone; callers ask, they do not compute.
    /// </summary>
    public static bool CanCommitCue(int cueMarkBeat, TransitionRepertoire repertoire, int beat)
    {
        return beat < LockPointBeatFor(cueMarkBeat, repertoire);
    }

    /// <summary>
    /// Projects the beat-domain window a cue for this Cue Mark and transition would occupy. Runway/tail/lock
    /// arithmetic is the Switcher's alone, so this stays private; the loaded cue's window is published for
    /// diagnostics through <see cref="SwitcherCueStatus"/>, not by projecting arbitrary candidates.
    /// </summary>
    private static void ProjectCueWindow(
        int cueMarkBeat,
        TransitionRepertoire repertoire,
        out int startBeat,
        out int lockPointBeat,
        out int completeBeat)
    {
        lockPointBeat = LockPointBeatFor(cueMarkBeat, repertoire);
        startBeat = lockPointBeat + 1;
        completeBeat = cueMarkBeat + repertoire.TailBeats;
    }

    private static int LockPointBeatFor(int cueMarkBeat, TransitionRepertoire repertoire)
    {
        return cueMarkBeat - repertoire.RunwayBeats - 1;
    }

    /// <summary>
    /// Whether the loaded cue has reached its Lock Point and can no longer be changed. A one-way latch:
    /// once the Switcher's own clock (an offer's beat or the render clock's wall time) reaches the Lock
    /// Point the cue rides until it starts, clears, or is aborted — a later backstep cannot reopen it.
    /// </summary>
    private bool IsLoadedCueLocked => loadedCueLocked;

    /// <summary>Wall time of the Lock Point beat: one beat before the Runway Start Time the loaded cue carries.</summary>
    private float LoadedCueLockTime => loadedCueStartTime - loadedCueSecondsPerBeat;

    /// <summary>Latches the lock once a beat at or past the Lock Point is observed; never unlatches.</summary>
    private void LatchLockAtBeat(int beat)
    {
        if (hasLoadedCue && !loadedCueLocked && beat >= loadedCueLockPointBeat)
        {
            loadedCueLocked = true;
            NotifyLocked(CueLockVia.Beat);
        }
    }

    /// <summary>Latches the lock once the render clock's wall time reaches the Lock Point; never unlatches.</summary>
    private void LatchLockAtTime(float nowSeconds)
    {
        if (hasLoadedCue && !loadedCueLocked && nowSeconds >= LoadedCueLockTime)
        {
            loadedCueLocked = true;
            NotifyLocked(CueLockVia.Render);
        }
    }

    /// <summary>
    /// Raises the minimal lock notification the Cue Log sink joins with its remembered display context.
    /// Fires exactly once per loaded cue — the false→true latch guard above admits only the first crossing —
    /// and carries no Phrase or name context, which the Switcher does not hold.
    /// </summary>
    private void NotifyLocked(CueLockVia via)
    {
        cueLog?.CueLocked(loadedCue.CueMarkBeat, loadedCueLockPointBeat, via);
    }

    /// <summary>
    /// Starts or replaces a transition from the current stage destination to the target effect.
    /// The Switcher owns progress and completion after this call; if another transition is still
    /// rendering, the previous destination becomes the source for this new last-command-wins move.
    /// </summary>
    public void StartTransition(int targetEffectIndex, int transitionIndex, TransitionStartTiming timing)
    {
        ValidateTransitionIndex(transitionIndex);
        ClearLoadedCue();
        StartTransition(targetEffectIndex, transitionIndex, timing, Time.time, transitions[transitionIndex].Repertoire);
    }

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
        var message = $"transition={FormatTransition(transitionIndex)} source={FormatEffect(sourceEffectIndex)} target={FormatEffect(targetEffectIndex)} A={transition.A} B={transition.B} durationSeconds={transitionDurationSeconds:0.###} progress={transitionProgress:0.###}";
        Trace($"SWITCHER_START {message}");
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
        StartLoadedCueIfDue(nowSeconds);

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

    private void LoadCue(SwitcherCueDirection cue, SwitcherClockSnapshot clock)
    {
        ProjectCueWindow(
            cue.CueMarkBeat,
            cue.TransitionRepertoire,
            out loadedCueStartBeat,
            out loadedCueLockPointBeat,
            out loadedCueCompleteBeat);
        loadedCue = cue;
        loadedCueStartTime = TimeAtBeat(clock, loadedCueStartBeat);
        loadedCueSecondsPerBeat = clock.SecondsPerBeat;
        loadedCueLocked = false;
        hasLoadedCue = true;
        Trace($"SWITCHER_LOAD_CUE cueMark={cue.CueMarkBeat} lock={loadedCueLockPointBeat} start={loadedCueStartBeat} startTime={loadedCueStartTime:0.###} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.TargetEffectIndex)}");
    }

    private static float TimeAtBeat(SwitcherClockSnapshot clock, int beat)
    {
        var beatsUntil = beat - clock.CurrentBeat - clock.BeatFraction;
        return clock.NowSeconds + (beatsUntil * clock.SecondsPerBeat);
    }

    private void StartLoadedCueIfDue(float nowSeconds)
    {
        if (!hasLoadedCue)
        {
            return;
        }

        LatchLockAtTime(nowSeconds);
        if (nowSeconds >= loadedCueStartTime)
        {
            StartLoadedCue(nowSeconds);
        }
    }

    private void StartLoadedCue(float nowSeconds)
    {
        var cue = loadedCue;
        var startBeat = loadedCueStartBeat;
        var startTime = loadedCueStartTime;
        var secondsPerBeat = loadedCueSecondsPerBeat;
        var elapsedBeats = Mathf.Max(0f, (nowSeconds - startTime) / secondsPerBeat);
        ClearLoadedCue();
        StartTransition(
            cue.TargetEffectIndex,
            cue.TransitionIndex,
            TransitionStartTiming.FromBeatClock(startTime, secondsPerBeat),
            nowSeconds,
            cue.TransitionRepertoire);
        Trace($"SWITCHER_START_CUE now={nowSeconds:0.###} elapsedBeats={elapsedBeats:0.###} start={startBeat} cueMark={cue.CueMarkBeat} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.TargetEffectIndex)}");
    }

    /// <summary>
    /// Discards the Switcher-held Loaded Cue, even one already locked. The Director calls this when the
    /// clock drops and the mode boundary crosses into Standalone: a beat-domain cue carries a Unity-time
    /// start and would otherwise fire from Unity time into a dead clock (ADR-0007).
    /// </summary>
    /// <remarks>
    /// A fire-and-forget command on the Director → Switcher seam, not lifecycle observation: idempotent,
    /// safe to call every Standalone frame, and a no-op when no cue is loaded.
    /// </remarks>
    public void AbortLoadedCue()
    {
        if (!hasLoadedCue)
        {
            return;
        }

        Trace($"SWITCHER_ABORT_CUE cueMark={loadedCue.CueMarkBeat} locked={IsLoadedCueLocked} transition={FormatTransition(loadedCue.TransitionIndex)} target={FormatEffect(loadedCue.TargetEffectIndex)}");
        ClearLoadedCue();
    }

    private void ClearLoadedCue()
    {
        hasLoadedCue = false;
        loadedCue = default;
        loadedCueStartBeat = -1;
        loadedCueCompleteBeat = -1;
        loadedCueLockPointBeat = -1;
        loadedCueLocked = false;
        loadedCueStartTime = 0f;
        loadedCueSecondsPerBeat = 0f;
    }

    private SwitcherCueStatus BuildLoadedCueStatus()
    {
        return hasLoadedCue
            ? new SwitcherCueStatus(
                true,
                IsLoadedCueLocked,
                loadedCue.CueMarkBeat,
                loadedCue.TargetEffectIndex,
                loadedCue.TransitionIndex,
                loadedCueLockPointBeat,
                loadedCueStartBeat,
                loadedCueCompleteBeat,
                loadedCue.TransitionRepertoire.RunwayBeats,
                loadedCue.TransitionRepertoire.TailBeats)
            : SwitcherCueStatus.Empty;
    }

    private static bool SameCue(SwitcherCueDirection left, SwitcherCueDirection right)
    {
        return left.CueMarkBeat == right.CueMarkBeat
            && left.TargetEffectIndex == right.TargetEffectIndex
            && left.TransitionIndex == right.TransitionIndex
            && SameRepertoire(left.TransitionRepertoire, right.TransitionRepertoire);
    }

    private static bool SameRepertoire(TransitionRepertoire left, TransitionRepertoire right)
    {
        return left.Tags == right.Tags
            && left.RunwayBeats == right.RunwayBeats
            && left.TailBeats == right.TailBeats
            && left.Shape == right.Shape
            && left.Intensity == right.Intensity
            && Mathf.Approximately(left.DefaultDurationSeconds, right.DefaultDurationSeconds);
    }

    private void CompleteTransition()
    {
        var transition = transitions[currentTransitionIndex];
        var completedTransitionIndex = currentTransitionIndex;
        var sourceEffectIndex = transition.A;
        var targetEffectIndex = transition.B;
        currentEffectIndex = targetEffectIndex;
        isTransitioning = false;
        transitionProgress = 0f;
        var message = $"transition={FormatTransition(completedTransitionIndex)} source={FormatEffect(sourceEffectIndex)} current={FormatEffect(currentEffectIndex)} targetWas={targetEffectIndex}";
        Trace($"SWITCHER_COMPLETE {message}");
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

    private void Trace(string message)
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
