using System;
using UnityEngine;
using Random = UnityEngine.Random;

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
/// 1. Copy this file and <c>EmptyEffectSyncSettingsAsset.cs</c>.
/// 2. Rename the files and <see cref="EmptyEffect"/> class to the new effect name.
/// 3. Rename <see cref="EmptyEffectStandaloneSettings"/>, <see cref="EmptyEffectSyncSettings"/>,
///    and <see cref="EmptyEffectSyncSettingsAsset"/> for the new effect. Update the copied asset's
///    <c>CreateAssetMenu</c> file and menu names and the <c>[EffectSyncSettings(typeof(...))]</c> attribute.
/// 4. Fill in the copied Effect Settings scaffold with the new Effect's authored values.
/// 5. Remove the <c>[RuntimeCatalogIgnore]</c> attribute from the copy.
/// 6. Delete the <c>EXAMPLE</c> members the effect does not need and implement <see cref="Draw"/>.
/// 7. Enter Play Mode or run a compile/import check so Unity generates both <c>.meta</c> files.
/// 8. Create the Sync Settings asset from the Tuning Window's Effects tab — compiling alone
///    does not create it (see <c>docs/effect-authoring.md</c>).
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
/// Keep and fill in the Effect Settings scaffold. Every <c>EXAMPLE</c> member is
/// only an illustration of how an Effect may consume that scaffold and is safe to delete.
/// </remarks>
[EffectSyncSettings(typeof(EmptyEffectSyncSettingsAsset))]
[RuntimeCatalogIgnore]
public class EmptyEffect : EffectBase
{
    // Standalone Defaults

    /// <summary>
    /// EXAMPLE — an authored value that fixes this Effect's look in Standalone Mode.
    /// Replace it with this Effect's own Standalone Defaults, including every randomization range.
    /// </summary>
    private const float StandaloneBrightness = 1f;

    /// <summary>Authored minimum speed for the EXAMPLE Standalone randomization range.</summary>
    private const float StandaloneSpeedMin = 0.5f;

    /// <summary>Authored maximum speed for the EXAMPLE Standalone randomization range.</summary>
    private const float StandaloneSpeedMax = 1.5f;

    // Sync Defaults

    /// <summary>
    /// EXAMPLE — an authored starting value for this Effect's musical response in Synced Mode.
    /// Replace it with this Effect's own Sync Defaults, which seed and restore its saved Sync Settings.
    /// </summary>
    private const float SyncBrightnessFloor = 0.35f;

    /// <summary>Authored multiplier for the EXAMPLE Fill response in Synced Mode.</summary>
    private const float SyncFillResponseStrength = 1f;

    /// <summary>Resolves a fresh immutable-by-convention copy of EmptyEffect's Standalone Defaults.</summary>
    public static EmptyEffectStandaloneSettings StandaloneSettings => new EmptyEffectStandaloneSettings(
        new FloatRange(StandaloneSpeedMin, StandaloneSpeedMax),
        StandaloneBrightness);

    /// <summary>Resolves a fresh copy of EmptyEffect's file-local Sync Defaults.</summary>
    public static EmptyEffectSyncSettings SyncDefaults => new EmptyEffectSyncSettings
    {
        BrightnessFloor = SyncBrightnessFloor,
        FillResponseStrength = SyncFillResponseStrength,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private EmptyEffectStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private EmptyEffectSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The current speed rolled from the EXAMPLE Standalone randomization range.</summary>
    private float speed;

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

        // Structure — how this Effect reaches its saved Sync Settings. Resolve consumes
        // no UnityEngine.Random, so it never disturbs an Effect's roll order.
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(EmptyEffect),
            SyncDefaults);

        // EXAMPLE — a per-activation roll from a Standalone randomization range. Delete if unused.
        speed = Random.Range(standaloneSettings.Speed.Min, standaloneSettings.Speed.Max);

        // EXAMPLE — delete if this effect does not use a Waveform.
        waveform = waveforms.Random();
    }

    /// <summary>
    /// EXAMPLE — re-acquires a Waveform when the shared 16-beat Grid wraps. Some Effects
    /// re-roll their whole look here and some keep their values; the choice belongs to
    /// the Effect. Delete or reshape this override freely.
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
        // EXAMPLE — the second endpoint is the no-placement fallback, so it is the Standalone constant.
        // The first endpoint already reads the resolved Sync Settings;
        // making the second endpoint musical takes the dual-homed IsSynced selection the authoring guide describes.
        float brightness = waveform.Lerp(SyncSettings.BrightnessFloor, standaloneSettings.Brightness);

        // EXAMPLE — wire facts and direct derived values live together in shallow groups.
        float gridProgress = beatManager.Grid.Progress ?? 0f;
        float fillBuild = beatManager.Fill.In.Build() * SyncSettings.FillResponseStrength;
        bool dropActive = beatManager.Drop.Active;
        Energy? energy = beatManager.Energy.Level;

        // EXAMPLE — audio bands and their color mappings begin at beatManager.Levels.
        _ = (brightness, speed, gridProgress, fillBuild, dropActive, energy);

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

/// <summary>The Standalone Settings scaffold copied and filled in for a new Effect.</summary>
public sealed class EmptyEffectStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from EmptyEffect's file-local defaults.</summary>
    public EmptyEffectStandaloneSettings(FloatRange speed, float brightness)
    {
        Speed = speed;
        Brightness = brightness;
    }

    /// <summary>Per-activation speed range used by the EXAMPLE randomization.</summary>
    public FloatRange Speed;

    /// <summary>Fixed Standalone brightness used by the EXAMPLE Waveform fallback.</summary>
    public float Brightness;
}

/// <summary>The serializable Sync Settings scaffold copied and filled in for a new Effect.</summary>
[Serializable]
public sealed class EmptyEffectSyncSettings
{
    /// <summary>Floor of the EXAMPLE Waveform-driven brightness response.</summary>
    [Range(0f, 1f)] public float BrightnessFloor;

    /// <summary>Multiplier applied to the EXAMPLE Fill response.</summary>
    [Min(0f)] public float FillResponseStrength;

    /// <summary>Copies every EmptyEffect Sync Setting from another value.</summary>
    public void CopyFrom(EmptyEffectSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BrightnessFloor = source.BrightnessFloor;
        FillResponseStrength = source.FillResponseStrength;
    }
}
