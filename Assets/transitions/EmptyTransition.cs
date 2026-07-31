// Copyable, catalog-ignored starter for authoring PenroseArt A-to-B transitions.
using UnityEngine;

/// <summary>
/// Copyable starter template for a new PenroseArt effect-to-effect transition.
/// </summary>
/// <remarks>
/// Authoring orientation:
/// - Effects and Transitions receive live read-only musical values through <see cref="EffectBase.beatManager"/> or <see cref="TransitionBase.beatManager"/>.
/// - They receive Waveform acquisition tools as <see cref="EffectBase.waveforms"/> or <see cref="TransitionBase.waveforms"/>.
/// - <see cref="EffectBase.waveform"/> is neutral public artistic configuration that an owning Performer may replace.
/// - Transitions declare only the public artistic configuration they actually use.
/// - Base classes acquire and respond to nothing automatically; the concrete Performer owns every example decision below.
///
/// This class is intentionally excluded from the runtime transition catalog by
/// <see cref="RuntimeCatalogIgnoreAttribute"/>. To create a real transition:
///
/// 1. Copy this file.
/// 2. Rename the file and class to the new transition name.
/// 3. Remove the <c>[RuntimeCatalogIgnore]</c> attribute from the copy.
/// 4. Delete or replace the <c>EXAMPLE</c> members below.
///
/// A normal transition draws source Effect <see cref="TransitionBase.A"/> and destination Effect
/// <see cref="TransitionBase.B"/>, then blends from A to B as <see cref="TransitionBase.V"/> rises
/// from 0 to 1; <see cref="TransitionBase.D"/> is the remaining A weight. Runway beats happen before
/// the Transition's Impact Point, Tail beats happen after it, and their sum is the transition duration.
/// The Switcher's private timing aligns that Impact Point with the Director's Cue Mark.
/// Every <c>EXAMPLE</c> below is local to this transition and safe to delete.
/// </remarks>
[RuntimeCatalogIgnore]
public class EmptyTransition : TransitionBase
{
    private int? previousGridBeat;

    /// <summary>
    /// EXAMPLE — public artistic configuration used only by this Transition. An owner may replace it.
    /// </summary>
    [System.NonSerialized]
    public Waveform waveform;

    /// <summary>
    /// EXAMPLE — declares a four-beat Runway ending at the Impact Point and no Tail afterward.
    /// </summary>
    protected override TransitionSettings BuildCodeDefaults()
    {
        return new TransitionSettings
        {
            RunwayBeats = 4,
            TailBeats = 0,
            Shape = TransitionShape.Blend,
            Intensity = TransitionIntensity.Subtle,
            DefaultDurationSeconds = 4f,
        };
    }

    /// <summary>
    /// EXAMPLE — explicitly acquires the Waveform this transition uses for the current activation.
    /// </summary>
    public override void OnStart()
    {
        waveform = waveforms.Random();
        previousGridBeat = beatManager.Grid.Beat;
    }

    /// <summary>
    /// Draws both Effects and writes a Waveform-shaped linear A-to-B crossfade into the output buffer.
    /// </summary>
    public override void Draw()
    {
        // EXAMPLE — this transition detects the grid boundary from its own prior observation.
        var gridBeat = beatManager.Grid.Beat;
        if (gridBeat == 1 && previousGridBeat is { } previous && previous != 1)
            waveform = waveforms.Random();
        previousGridBeat = gridBeat;

        controller.effects[A].Draw();
        controller.effects[B].Draw();

        // EXAMPLE — no live placement returns 1, preserving the plain progress crossfade.
        float blend = Mathf.Clamp01(V * waveform.Lerp(0.85f, 1f));
        float remaining = 1f - blend;
        Color[] source = controller.effects[A].buffer;
        Color[] destination = controller.effects[B].buffer;

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = (source[i] * remaining) + (destination[i] * blend);
    }

    /// <summary>
    /// Reserved for future deactivation cleanup. Controller does not currently call this method.
    /// </summary>
    public override void OnEnd()
    {
    }
}
