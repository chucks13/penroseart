using System;
using UnityEngine;

/// <summary>
/// Mechanical stage switcher for Penrose performers.
/// The Switcher owns only the in-flight effect/transition state and renders it;
/// the Director decides what to start and when to complete it.
/// </summary>
[Serializable]
public sealed class Switcher
{
    private readonly EffectBase[] effects;
    private readonly TransitionBase[] transitions;

    private int currentEffectIndex = -1;
    private int currentTransitionIndex = -1;
    private bool isTransitioning;

    /// <summary>Whether a transition is currently being rendered instead of a single effect.</summary>
    public bool IsTransitioning => isTransitioning;

    /// <summary>Currently active effect index, or -1 while a transition owns the frame.</summary>
    public int CurrentEffectIndex => isTransitioning ? -1 : currentEffectIndex;

    /// <summary>Active transition index while transitioning; otherwise the next/last transition index.</summary>
    public int CurrentTransitionIndex => currentTransitionIndex;

    /// <summary>The destination effect while transitioning, otherwise the currently active effect.</summary>
    public int TransitionTargetEffectIndex => isTransitioning ? transitions[currentTransitionIndex].B : currentEffectIndex;

    /// <summary>Display name for the effect or transition currently on stage.</summary>
    public string CurrentName => isTransitioning ? transitions[currentTransitionIndex].Name : effects[currentEffectIndex].Name;

    /// <summary>Repertoire advertised by the active effect, or None while a transition owns the frame.</summary>
    public Repertoire CurrentEffectRepertoire => CurrentEffectIndex >= 0 ? effects[CurrentEffectIndex].Repertoire : Repertoire.None;

    public Switcher(Controller controller, EffectBase[] effects, TransitionBase[] transitions)
    {
        _ = controller ?? throw new ArgumentNullException(nameof(controller));
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
    }

    /// <summary>Immediately puts an effect on stage, cancelling any in-flight transition.</summary>
    public void ShowNow(int effectIndex)
    {
        ValidateEffectIndex(effectIndex);

        EffectBase.APalette.Change();
        isTransitioning = false;
        currentEffectIndex = effectIndex;
        StartEffect(effectIndex);
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

        var transition = transitions[transitionIndex];
        transition.RandomizeTime();
        transition.V = 0f;
        transition.A = currentEffectIndex;
        transition.B = targetEffectIndex;
        transition.OnStart();

        EffectBase.APalette.Change();
        StartEffect(targetEffectIndex);

        currentTransitionIndex = transitionIndex;
        currentEffectIndex = -1;
        isTransitioning = true;
    }

    /// <summary>Promotes the transition destination to the active effect.</summary>
    public void CompleteTransition()
    {
        if (!isTransitioning)
        {
            throw new InvalidOperationException("Cannot complete a transition when the Switcher is not transitioning.");
        }

        currentEffectIndex = transitions[currentTransitionIndex].B;
        isTransitioning = false;
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
