using UnityEngine;

/// <summary>
/// Copyable starter template for a new PenroseArt effect.
/// </summary>
/// <remarks>
/// This class is intentionally excluded from the runtime effect catalog by
/// <see cref="RuntimeCatalogIgnoreAttribute"/>. To create a real effect:
///
/// 1. Copy this file.
/// 2. Rename the file and class to the new effect name.
/// 3. Remove the <c>[RuntimeCatalogIgnore]</c> attribute from the copy.
/// 4. Delete the <c>EXAMPLE</c> members below and implement <see cref="Draw"/>.
///
/// Effect lifecycle:
/// - <see cref="Init"/> is called once after reflection creates the effect.
///   Use it for reusable setup, cached geometry, lookup tables, and buffer setup.
/// - <see cref="OnStart"/> is called every time the effect becomes active.
///   Use it for per-run randomization and activation state.
/// - <see cref="Draw"/> is called every frame while the effect is active.
///   Write exactly <c>Penrose.Total</c> colors into <see cref="EffectBase.buffer"/>.
/// - <see cref="OnEnd"/> exists for future cleanup hooks, but Controller does
///   not currently call it.
///
/// This template also collects small, copy-paste <c>EXAMPLE</c>s of the live musical
/// data an effect can read through <see cref="EffectBase.beatManager"/>. Each is
/// marked <c>EXAMPLE</c> and is safe to delete. The first one reads the 16-beat Grid.
/// </remarks>
[RuntimeCatalogIgnore]
public class EmptyEffect : EffectBase
{
    /// <summary>
    /// Optional one-time setup. The base implementation connects this effect to
    /// Controller, Penrose geometry, tile data, and the 900-color output buffer.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Optional activation setup. The base implementation resets beat behavior and arms the new-Grid edge;
    /// add per-run randomization here in real effects.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
    }

    /// <summary>
    /// Text appended to the on-screen debug display while this effect is active.
    /// </summary>
    public override string DebugText() => "Empty effect template";

    /// <summary>
    /// Render one frame. Real effects should replace this with their visual
    /// algorithm and fill every slot in <see cref="EffectBase.buffer"/>.
    /// </summary>
    public override void Draw()
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = Color.black;
    }

    /// <summary>
    /// EXAMPLE override of <see cref="EffectBase.OnNewGrid"/>: the base edge-detects the downbeat of each new
    /// 16-beat Grid (Count wraps 16 → 1) on a Locked lock and calls this once. Here it picks a fresh rhythmic
    /// Waveform variant; delete it when you write your own effect.
    /// </summary>
    /// <remarks>
    /// The base hook hides the Grid bookkeeping — you just react. The live <see cref="BeatManager.Grid"/> is a
    /// nullable <see cref="GridInfo"/> you can still read directly in <see cref="Draw"/> for more than the
    /// downbeat edge:
    /// <list type="bullet">
    /// <item><description><see cref="GridInfo.Progress"/> — position 0..1 through the 16 beats, e.g.
    ///   <c>var sweep = beatManager.Grid?.Progress ?? 0f;</c> for something that sweeps and resets each Grid.</description></item>
    /// <item><description><see cref="GridInfo.Confidence"/> — how much to trust the lock; the base hook already
    ///   gates on Locked, so this fires only on a Grid the Director trusts.</description></item>
    /// </list>
    /// A null Grid means the wall is off the grid (Standalone Mode, or the beat clock dropped out), so the hook
    /// simply never fires there and resumes cleanly when the grid returns.
    /// </remarks>
    protected override void OnNewGrid()
    {
        // beatVariant feeds the base helpers BeatBrightness()/BeatTime(), so re-rolling it changes this
        // effect's rhythmic "personality" for the next 16 beats.
        beatVariant = beatManager.GetRandomVariant();
    }

    /// <summary>
    /// Reserved for future deactivation cleanup. Controller does not currently
    /// call this method.
    /// </summary>
    public override void OnEnd()
    {
    }
}
