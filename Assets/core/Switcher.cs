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
        string.Empty);

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
        string stageName)
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
    }
}

/// <summary>
/// Mechanical stage switcher for Penrose performers.
/// The Switcher owns only the in-flight effect/transition state and renders it;
/// the Director decides what to start and when to complete it.
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

    /// <summary>Currently active effect index, or -1 while a transition owns the frame.</summary>
    public int CurrentEffectIndex => isTransitioning ? -1 : currentEffectIndex;

    /// <summary>Active transition index while transitioning; otherwise the next/last transition index.</summary>
    public int CurrentTransitionIndex => currentTransitionIndex;

    /// <summary>The destination effect while transitioning, otherwise the currently active effect.</summary>
    public int TransitionTargetEffectIndex => isTransitioning ? transitions[currentTransitionIndex].B : currentEffectIndex;

    /// <summary>Display name for the effect or transition currently on stage.</summary>
    public string CurrentName => isTransitioning ? transitions[currentTransitionIndex].Name : effects[currentEffectIndex].Name;

    /// <summary>Current read-only mechanical stage snapshot for runtime HUDs and inspector diagnostics.</summary>
    public SwitcherStatus Status => BuildStatus();

    public Switcher(Controller controller, EffectBase[] effects, TransitionBase[] transitions)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
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
        Trace($"SWITCHER_INIT current={FormatEffect(effectIndex)} nextTransition={FormatTransition(transitionIndex)}");
    }

    /// <summary>Immediately puts an effect on stage, cancelling any in-flight transition.</summary>
    public void ShowNow(int effectIndex)
    {
        ValidateEffectIndex(effectIndex);

        EffectBase.APalette.Change();
        isTransitioning = false;
        currentEffectIndex = effectIndex;
        StartEffect(effectIndex);
        Trace($"SWITCHER_SHOW_NOW current={FormatEffect(effectIndex)}");
    }

    /// <summary>
    /// Starts a transition from the current effect to the target effect.
    /// Progress and completion remain owned by the Director.
    /// </summary>
    public void StartTransition(int targetEffectIndex, int transitionIndex)
    {
        ValidateEffectIndex(currentEffectIndex);
        ValidateEffectIndex(targetEffectIndex);
        ValidateTransitionIndex(transitionIndex);

        var sourceEffectIndex = currentEffectIndex;
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
        isTransitioning = true;
        Trace($"SWITCHER_START transition={FormatTransition(transitionIndex)} source={FormatEffect(sourceEffectIndex)} target={FormatEffect(targetEffectIndex)} A={transition.A} B={transition.B}");
    }

    /// <summary>Promotes the transition destination to the active effect.</summary>
    public void CompleteTransition()
    {
        if (!isTransitioning)
        {
            throw new InvalidOperationException("Cannot complete a transition when the Switcher is not transitioning.");
        }

        var transition = transitions[currentTransitionIndex];
        var completedTransitionIndex = currentTransitionIndex;
        var sourceEffectIndex = transition.A;
        var targetEffectIndex = transition.B;
        currentEffectIndex = targetEffectIndex;
        isTransitioning = false;
        Trace($"SWITCHER_COMPLETE transition={FormatTransition(completedTransitionIndex)} source={FormatEffect(sourceEffectIndex)} current={FormatEffect(currentEffectIndex)} targetWas={targetEffectIndex}");
    }

    /// <summary>
    /// Renders the active effect or transition into a cloned 900-tile buffer.
    /// The Director supplies transition progress because it owns timing.
    /// </summary>
    public Color[] Render(float transitionProgress, out string debugText)
    {
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
                transition.Name);
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
            currentTransitionIndex,
            TransitionName(currentTransitionIndex),
            currentName);
    }

    private string EffectName(int effectIndex)
    {
        return effectIndex >= 0 && effectIndex < effects.Length ? effects[effectIndex].Name : string.Empty;
    }

    private string TransitionName(int transitionIndex)
    {
        return transitionIndex >= 0 && transitionIndex < transitions.Length ? transitions[transitionIndex].Name : string.Empty;
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
