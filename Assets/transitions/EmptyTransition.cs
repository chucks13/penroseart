// Copyable starter for a new PenroseArt A-to-B transition. TEMPLATE is never defined, so Unity compiles none of this file.
#if TEMPLATE
using UnityEngine;

/// <summary>
/// Copyable starter template for a new PenroseArt effect-to-effect transition.
/// </summary>
/// <remarks>
/// To create a real transition:
///
/// 1. Copy this file.
/// 2. Rename the file and class to the new transition name.
/// 3. Delete the <c>#if TEMPLATE</c> and <c>#endif</c> lines, and rewrite the top comment to say what the copy does.
/// 4. Implement <see cref="Draw"/>.
///
/// The copy compiles, joins the catalog, and runs as it stands as a plain crossfade. A transition
/// needs no settings: without an override of <see cref="TransitionBase.BuildCodeDefaults"/> it uses
/// the default Transition Repertoire (see <c>docs/effect-authoring.md</c>).
///
/// A transition draws source Effect <see cref="TransitionBase.A"/> and destination Effect
/// <see cref="TransitionBase.B"/>, then blends from A to B as <see cref="TransitionBase.V"/> rises
/// from 0 to 1; <see cref="TransitionBase.D"/> is the remaining A weight. Runway beats happen before
/// the Transition's Impact Point, Tail beats happen after it, and their sum is the transition duration.
/// The Switcher's private timing aligns that Impact Point with the Director's Cue Mark.
///
/// Live musical values come from <see cref="TransitionBase.beatManager"/>, and Waveforms from
/// <see cref="TransitionBase.waveforms"/>.
/// </remarks>
public class EmptyTransition : TransitionBase
{
    /// <summary>Per-activation setup; this template has none.</summary>
    public override void OnStart()
    {
    }

    /// <summary>Draws both Effects and writes a linear A-to-B crossfade into the output buffer.</summary>
    public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();

        Color[] source = controller.effects[A].buffer;
        Color[] destination = controller.effects[B].buffer;

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = (source[i] * D) + (destination[i] * V);
    }

    /// <summary>Reserved for deactivation cleanup. Controller does not currently call this method.</summary>
    public override void OnEnd()
    {
    }
}
#endif
