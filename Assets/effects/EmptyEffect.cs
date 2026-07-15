using UnityEngine;

/// <summary>
/// Copyable starter template for a new PenroseArt effect.
/// </summary>
/// <remarks>
/// Authoring orientation:
/// - Effects and Transitions receive live read-only musical values through <see cref="EffectBase.beatManager"/> or <see cref="TransitionBase.beatManager"/>.
/// - They receive Waveform acquisition tools as <see cref="EffectBase.waveforms"/> or <see cref="TransitionBase.waveforms"/>.
/// - <see cref="EffectBase.waveform"/> is neutral public artistic configuration that an owning Performer may replace.
/// - Transitions declare only the public artistic configuration they actually use.
/// - Base classes acquire and respond to nothing automatically; the concrete Performer owns every example decision below.
///
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
/// Every <c>EXAMPLE</c> below is local to this effect and safe to delete.
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

        // EXAMPLE — delete if this effect does not use a Waveform.
        waveform = waveforms.Random();
    }

    /// <summary>
    /// EXAMPLE — explicitly chooses a new Waveform when the shared 16-beat Grid wraps.
    /// Delete this override when the effect should keep its current value.
    /// </summary>
    protected override void OnNewGrid()
    {
        waveform = waveforms.Random();
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
        // EXAMPLE — the second endpoint is the no-placement fallback.
        float brightness = waveform.Lerp(0.35f, 1f);

        // EXAMPLE — wire facts and direct derived values live together in shallow groups.
        float gridProgress = beatManager.Grid.Progress ?? 0f;
        float fillBuild = beatManager.Fill.Build();
        bool dropActive = beatManager.Drop.Active;
        Energy? energy = beatManager.Energy.Level;

        // EXAMPLE — audio bands and their color mappings begin at beatManager.Levels.
        _ = (brightness, gridProgress, fillBuild, dropActive, energy);

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
