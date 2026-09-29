// Copyable starter for a new PenroseArt effect. TEMPLATE is never defined, so Unity compiles none of this file.
#if TEMPLATE
using UnityEngine;

/// <summary>
/// Copyable starter template for a new PenroseArt effect.
/// </summary>
/// <remarks>
/// To create a real effect:
///
/// 1. Copy this file.
/// 2. Rename the file and class to the new effect name.
/// 3. Delete the <c>#if TEMPLATE</c> and <c>#endif</c> lines, and rewrite the top comment to say what the copy does.
/// 4. Implement <see cref="Draw"/>.
///
/// The copy compiles, joins the catalog, and runs as it stands; it draws black until <see cref="Draw"/>
/// does more. An effect needs no settings. Standalone Settings and Sync Settings are added when the
/// effect is ready for them (see <c>docs/effect-authoring.md</c>).
///
/// Lifecycle:
/// - <see cref="EffectBase.Init"/> runs once after reflection creates the effect. Override it for
///   reusable setup such as cached geometry and lookup tables, and call <c>base.Init()</c> first.
/// - <see cref="EffectBase.OnStart"/> runs every time the effect becomes active. Override it for
///   per-run randomization, and call <c>base.OnStart()</c> first.
/// - <see cref="Draw"/> runs every frame while the effect is active. It writes exactly
///   <c>Penrose.Total</c> colors into <see cref="EffectBase.buffer"/>.
///
/// Live musical values come from <see cref="EffectBase.beatManager"/>, and Waveforms from
/// <see cref="EffectBase.waveforms"/>.
/// </remarks>
public class EmptyEffect : EffectBase
{
    /// <summary>Text appended to the on-screen debug display while this effect is active.</summary>
    public override string DebugText() => "";

    /// <summary>Renders one frame by filling every tile with black.</summary>
    public override void Draw()
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = Color.black;
    }

    /// <summary>Reserved for deactivation cleanup. Controller does not currently call this method.</summary>
    public override void OnEnd()
    {
    }
}
#endif
