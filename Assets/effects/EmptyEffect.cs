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
    /// EXAMPLE state for <see cref="RerollVariantOnNewGrid"/>: the Grid Count seen last frame, so the
    /// wrap into a new 16-beat Grid can be detected. 0 means "off the grid last frame"; a live count is 1..16.
    /// </summary>
    private int lastGridCount;

    /// <summary>
    /// Optional one-time setup. The base implementation connects this effect to
    /// Controller, Penrose geometry, tile data, and the 900-color output buffer.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Optional activation setup. The base implementation resets beat behavior;
    /// add per-run randomization here in real effects.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();

        // EXAMPLE: seed the Grid tracker from the current Grid so the reroll below does not fire on the
        // first active frame. `Grid` is null when the wall is not on the grid, so `?? 0` reads "no grid".
        lastGridCount = beatManager.Grid?.Count ?? 0;
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
        // EXAMPLE (delete with the example members when you write your own effect): drive this effect's
        // rhythmic personality from the live 16-beat Grid.
        RerollVariantOnNewGrid();

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = Color.black;
    }

    /// <summary>
    /// EXAMPLE use of <see cref="BeatManager.Grid"/>: pick a fresh rhythmic Waveform variant at the start
    /// of every 16-beat Grid — i.e. each time the Grid Count wraps 16 → 1 onto a new Grid.
    /// </summary>
    /// <remarks>
    /// <see cref="BeatManager.Grid"/> is a nullable <see cref="GridInfo"/>: null means the wall is not on
    /// the grid right now (Standalone Mode, or the beat clock dropped out), so the reroll simply never fires
    /// there and resumes cleanly when the grid returns. The same struct also carries:
    /// <list type="bullet">
    /// <item><description><see cref="GridInfo.Progress"/> — position 0..1 through the 16 beats, e.g.
    ///   <c>var sweep = beatManager.Grid?.Progress ?? 0f;</c> for something that sweeps and resets each Grid.</description></item>
    /// <item><description><see cref="GridInfo.Confidence"/> — how much to trust the lock. All three values
    ///   (Locked / Coasting / Contradicted) are on-grid readings, so many effects ignore it; this example
    ///   uses it to only re-roll on a Grid the Director actually trusts.</description></item>
    /// </list>
    /// </remarks>
    private void RerollVariantOnNewGrid()
    {
        var grid = beatManager.Grid;

        // Count is 1..16 while on the grid, 0 here when off the grid.
        var count = grid?.Count ?? 0;

        // Fire once on the wrap to count 1 (the downbeat of a new Grid), not every frame it stays 1 — and
        // only when the lock is trusted (Locked), so a Coasting / Contradicted reading does not re-roll.
        if (count == 1 && lastGridCount != 1 && grid?.Confidence == GridSyncState.Locked)
        {
            // beatVariant feeds the base helpers BeatBrightness()/BeatTime(), so re-rolling it changes this
            // effect's rhythmic "personality" for the next 16 beats.
            beatVariant = beatManager.GetRandomVariant();
        }

        lastGridCount = count;
    }

    /// <summary>
    /// Reserved for future deactivation cleanup. Controller does not currently
    /// call this method.
    /// </summary>
    public override void OnEnd()
    {
    }
}
