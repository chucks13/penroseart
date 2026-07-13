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
    /// Optional activation setup. The base performs no musical acquisition or response; concrete
    /// effects own those decisions and may add per-run randomization here.
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
    /// Reserved for future deactivation cleanup. Controller does not currently
    /// call this method.
    /// </summary>
    public override void OnEnd()
    {
    }
}
