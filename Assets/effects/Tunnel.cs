using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a direct tile-space tunnel from Tile radius, Tile-index phase, and mode-specific cycle
/// phase, using that cyclic coordinate to sample an Effect-conditioned copy of the shared animated
/// palette.
/// </summary>
/// <remarks>
/// FILL: the tunnel rushes (scroll accelerates) and compresses its radial bands as the Fill builds,
/// both driven by <see cref="BeatManager.Fill"/> Build.
/// DROP: <see cref="BeatManager.Drop"/> Decay drives a hard reverse warp plus deep radial-band
/// compression over two bars.
/// SYNC: current Energy selects an authored Duration, and the served Duration pulse supplies the
/// deliberately pumping colour-cycle phase.
/// </remarks>
[EffectSyncSettings(typeof(TunnelSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(TunnelStandaloneSettingsAsset))]
public class Tunnel : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored minimum per-Tile index phase step for the unchanged Standalone look.</summary>
    private const float StandaloneTileIndexPhaseStepMin = 0.0004f;

    /// <summary>Authored maximum per-Tile index phase step for the unchanged Standalone look.</summary>
    private const float StandaloneTileIndexPhaseStepMax = 0.003f;

    /// <summary>Authored minimum scroll speed for the unchanged Standalone look.</summary>
    private const float StandaloneScrollSpeedMin = 0.1f;

    /// <summary>Authored maximum scroll speed for the unchanged Standalone look.</summary>
    private const float StandaloneScrollSpeedMax = 1f;

    /// <summary>
    /// Authored minimum radial mix for the unchanged Standalone look; smaller values spread the
    /// rings wider.
    /// </summary>
    private const float StandaloneRadialMixMin = 0.01f;

    /// <summary>Authored maximum radial mix for the unchanged Standalone look.</summary>
    private const float StandaloneRadialMixMax = 0.2f;

    /// <summary>
    /// Authored Tile-center scale for the unchanged Standalone look. The radial phase term is Tile
    /// radius times this scale times the radial mix, so both settings tune the ring spacing.
    /// </summary>
    private const float StandaloneCenterScale = 0.03f;

    /// <summary>
    /// Authored Standalone palette conditioning, calibrated to pass the classic six-stop rainbow
    /// through unchanged. Its mean relative luminance is 0.5, so that target produces unit lift;
    /// equalization and redistribution stay neutral; the 0.001 dark-repair threshold remains below
    /// pure blue's approximately 0.072 luminance ceiling. Tune on the wall.
    /// </summary>
    private static PaletteConditioning StandalonePaletteConditioning => new()
    {
        TargetLuminance = 0.5f,
        MinimumLuminance = 0f,
        LuminanceEqualization = 0f,
        HueSpreadReference = 1f,
        MaximumLuminanceScale = 1f,
        DarkLuminanceThreshold = 0.001f,
        DuplicateThreshold = 0f,
        HueRedistribution = 0f,
    };

    // Sync Defaults

    /// <summary>Authored minimum per-Tile index phase step for the current Synced look.</summary>
    private const float SyncTileIndexPhaseStepMin = 0.0004f;

    /// <summary>Authored maximum per-Tile index phase step for the current Synced look.</summary>
    private const float SyncTileIndexPhaseStepMax = 0.003f;

    /// <summary>Authored Duration of one full colour cycle at Low Energy.</summary>
    private const Duration SyncLowCycleDuration = Duration.Whole;

    /// <summary>Authored Duration of one full colour cycle at Mid Energy.</summary>
    private const Duration SyncMidCycleDuration = Duration.Half;

    /// <summary>Authored Duration of one full colour cycle at High Energy.</summary>
    private const Duration SyncHighCycleDuration = Duration.Quarter;

    /// <summary>Authored minimum radial mix for the current Synced look.</summary>
    private const float SyncRadialMixMin = 0.01f;

    /// <summary>Authored maximum radial mix for the current Synced look.</summary>
    private const float SyncRadialMixMax = 0.2f;

    /// <summary>Authored Tile-center scale for the current Synced look.</summary>
    private const float SyncCenterScale = 0.03f;

    /// <summary>
    /// Authored Sync palette conditioning, independently authored from Standalone so live tuning in
    /// either mode cannot drift the other. It begins at the same rainbow pass-through calibration:
    /// unit lift at mean luminance 0.5, neutral equalization and redistribution, and dark repair below
    /// pure blue's approximately 0.072 luminance ceiling. Tune on the wall.
    /// </summary>
    private static PaletteConditioning SyncPaletteConditioning => new()
    {
        TargetLuminance = 0.5f,
        MinimumLuminance = 0f,
        LuminanceEqualization = 0f,
        HueSpreadReference = 1f,
        MaximumLuminanceScale = 1f,
        DarkLuminanceThreshold = 0.001f,
        DuplicateThreshold = 0f,
        HueRedistribution = 0f,
    };

    /// <summary>
    /// Authored extra scroll-rate multiple at full Fill: the color scroll rushes this much faster at
    /// the build's peak. Tune while watching the Fill readout.
    /// </summary>
    private const float SyncFillScrollRateMultiplier = 5f;

    /// <summary>
    /// Authored extra ring-compression multiple at full Fill: the radial bands tighten this much at
    /// the build's peak. Tune while watching the Fill readout.
    /// </summary>
    private const float SyncFillRingCompression = 3f;

    /// <summary>Authored bars over which the Drop warp falls linearly from full to nothing.</summary>
    private const int SyncDropBars = 2;

    /// <summary>
    /// Authored reverse scroll-rate multiple at the Drop's peak. It exceeds
    /// <see cref="SyncFillScrollRateMultiplier"/> so the Drop out-slams a Fill; tune while watching
    /// the Drop readout.
    /// </summary>
    private const float SyncDropReverseScrollRateMultiplier = 10f;

    /// <summary>
    /// Authored extra ring-compression multiple at the Drop's peak, stacked on any Fill compression.
    /// Tune while watching the Drop readout.
    /// </summary>
    private const float SyncDropRingCompression = 6f;

    /// <summary>
    /// The tunnel handles Fill and Drop, and its authored whole-, half-, and quarter-note cycle
    /// cadences make its motion suit Low-, Mid-, and High-energy sections respectively.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill |
        Repertoire.HandlesDrop |
        Repertoire.EnergyLow |
        Repertoire.EnergyMid |
        Repertoire.EnergyHigh;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate Tunnel's authored
    /// Standalone Defaults.
    /// </summary>
    public static TunnelStandaloneSettings StandaloneDefaults => new()
    {
        TileIndexPhaseStep = new FloatRange(
            StandaloneTileIndexPhaseStepMin,
            StandaloneTileIndexPhaseStepMax),
        ScrollSpeed = new FloatRange(StandaloneScrollSpeedMin, StandaloneScrollSpeedMax),
        RadialMix = new FloatRange(StandaloneRadialMixMin, StandaloneRadialMixMax),
        CenterScale = StandaloneCenterScale,
        PaletteConditioning = StandalonePaletteConditioning,
    };

    /// <summary>Resolves a fresh copy of Tunnel's file-local Sync Defaults.</summary>
    public static TunnelSyncSettings SyncDefaults => new()
    {
        TileIndexPhaseStep = new FloatRange(SyncTileIndexPhaseStepMin, SyncTileIndexPhaseStepMax),
        LowCycleDuration = SyncLowCycleDuration,
        MidCycleDuration = SyncMidCycleDuration,
        HighCycleDuration = SyncHighCycleDuration,
        RadialMix = new FloatRange(SyncRadialMixMin, SyncRadialMixMax),
        CenterScale = SyncCenterScale,
        PaletteConditioning = SyncPaletteConditioning,
        FillScrollRateMultiplier = SyncFillScrollRateMultiplier,
        FillRingCompression = SyncFillRingCompression,
        DropBars = SyncDropBars,
        DropReverseScrollRateMultiplier = SyncDropReverseScrollRateMultiplier,
        DropRingCompression = SyncDropRingCompression,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private TunnelStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private TunnelSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>
    /// Tunnel's Effect-local conditioned endpoint cache. It follows shared palette revisions and live
    /// conditioning controls while preserving the animated cross-fade without steady-frame allocation.
    /// </summary>
    private readonly ConditionedPaletteCache conditionedPalette = new();

    /// <summary>Current randomly rolled per-Tile index phase step.</summary>
    private float tileIndexPhaseStep;

    /// <summary>Current Standalone ScrollSpeed Roll, unused by the Synced colour cycle.</summary>
    private float scrollSpeed;

    /// <summary>Most recently sampled Synced colour-cycle phase.</summary>
    private float previousSyncedCyclePhase;

    /// <summary>Duration used for the most recently sampled Synced colour-cycle phase.</summary>
    private Duration previousSyncedCycleDuration;

    /// <summary>Whether a prior phase from the current Synced Duration is available for differencing.</summary>
    private bool hasPreviousSyncedCyclePhase;

    /// <summary>Current randomly rolled radial mix.</summary>
    private float radialMix;

    /// <summary>Fill Build amount driving the scroll rush and ring compression.</summary>
    private float fillEnv;

    /// <summary>
    /// Integrated extra scroll phase from the Fill rush, kept in [0,1). Integrating the rate avoids
    /// the phase jump that scaling absolute <c>effectTime</c> would cause.
    /// </summary>
    private float fillScroll;

    /// <summary>Drop Decay amount driving the reverse warp and ring-compression punch.</summary>
    private float dropEnv;

    /// <summary>
    /// Integrated reverse scroll phase from the Drop warp, kept in [0,1). Like
    /// <see cref="fillScroll"/>, it pulls the phase the other way.
    /// </summary>
    private float dropScroll;

    /// <summary>
    /// Resolves both saved settings surfaces, initializes the per-activation Roll, and declares that
    /// Tunnel drives no Waveform response: its musical reading is the Energy-selected cycle Duration
    /// alone, so a second envelope on the same frame would only compete with it.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Tunnel),
            StandaloneDefaults);
        ResolveSyncSettings();
        Reroll();
        waveform = waveforms.None;
        fillEnv = 0f;
        fillScroll = 0f;
        dropEnv = 0f;
        dropScroll = 0f;
        hasPreviousSyncedCyclePhase = false;
        buffer.Clear();
    }

    /// <summary>
    /// Re-reads the saved Sync Settings so a live wall edit reaches the next frame. It is separate
    /// from <see cref="Reroll"/> because Synced Mode keeps one Roll for the whole activation while
    /// still following every settings change.
    /// </summary>
    private void ResolveSyncSettings()
    {
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Tunnel),
            SyncDefaults);
    }

    /// <summary>
    /// Selects the active mode's Roll ranges and re-rolls Tile-index phase, the Standalone scroll
    /// speed, and radial phase in the original random order. Keeping the Standalone ScrollSpeed Roll
    /// in its original slot preserves the locked Standalone look while Synced Mode takes its cycle
    /// cadence from a Duration. Synced Mode rolls once per activation, so the Grid no longer calls
    /// this; <see cref="OnNewGrid"/> owns that decision.
    /// </summary>
    private void Reroll()
    {
        bool isSynced = beatManager.IsSynced;
        FloatRange tileIndexPhaseStepRange = isSynced
            ? SyncSettings.TileIndexPhaseStep
            : standaloneSettings.TileIndexPhaseStep;
        FloatRange scrollSpeedRange = standaloneSettings.ScrollSpeed;
        FloatRange radialMixRange = isSynced
            ? SyncSettings.RadialMix
            : standaloneSettings.RadialMix;

        tileIndexPhaseStep = Random.Range(tileIndexPhaseStepRange.Min, tileIndexPhaseStepRange.Max);
        scrollSpeed = Random.Range(scrollSpeedRange.Min, scrollSpeedRange.Max);
        radialMix = Random.Range(radialMixRange.Min, radialMixRange.Max);
    }

    /// <summary>
    /// Re-resolves Sync Settings on every Grid so live wall tuning keeps taking effect, then turns
    /// the Grid over: Synced Mode changes the shared palette, Standalone Mode re-rolls as it always
    /// has. A Synced tunnel holds one Roll for the whole activation — its cadence already follows the
    /// music through Energy and Duration, so a Grid re-roll would only interrupt that reading, and
    /// colour is what turns over on the Grid instead.
    /// </summary>
    protected override void OnNewGrid()
    {
        ResolveSyncSettings();
        if (beatManager.IsSynced)
        {
            APalette.Change();
        }
        else
        {
            Reroll();
        }
    }

    /// <summary>Reserved deactivation hook. Controller does not currently call this.</summary>
    public override void OnEnd() { }

    /// <summary>Returns the current rolls, cycle cadence, and live musical envelopes for the Controller debug display.</summary>
    public override string DebugText()
    {
        return $"Tile index phase step: {tileIndexPhaseStep}\n" +
        (beatManager.IsSynced
            ? $"Cycle duration: {CurrentCycleDuration()}\n"
            : $"Scroll speed: {scrollSpeed}\n") +
        $"Radial mix: {radialMix}\n" +
        (fillEnv > 0.01f ? $"FILL {fillEnv:0.00}\n" : "") +
        (dropEnv > 0.01f ? $"DROP {dropEnv:0.00}\n" : "");
    }

    /// <summary>
    /// Selects the authored cycle Duration for the Data Surface's current Energy. An unavailable
    /// Energy uses the Low setting, keeping the Synced tunnel at its calmest authored cadence.
    /// </summary>
    /// <returns>The Sync Setting selected for the current Energy tier.</returns>
    private Duration CurrentCycleDuration()
    {
        return beatManager.Energy.Level switch
        {
            Energy.High => SyncSettings.HighCycleDuration,
            Energy.Mid => SyncSettings.MidCycleDuration,
            _ => SyncSettings.LowCycleDuration,
        };
    }

    /// <summary>
    /// Samples the served Duration pulse as an increasing colour-cycle phase and reports its forward
    /// advance. A Duration change starts a fresh sample so the mapping discontinuity is not multiplied
    /// into the Fill or Drop response.
    /// </summary>
    /// <param name="duration">The authored Duration selected for the current Energy tier.</param>
    /// <param name="cyclePhaseAdvance">Forward phase movement since the previous comparable sample.</param>
    /// <returns>The increasing zero-to-one phase served by the selected Duration pulse.</returns>
    private float SampleSyncedCyclePhase(Duration duration, out float cyclePhaseAdvance)
    {
        float cyclePhase = 1f - beatManager.Pulses.Every(duration);
        cyclePhaseAdvance = hasPreviousSyncedCyclePhase && duration == previousSyncedCycleDuration
            ? Mathf.Repeat(cyclePhase - previousSyncedCyclePhase, 1f)
            : 0f;

        previousSyncedCyclePhase = cyclePhase;
        previousSyncedCycleDuration = duration;
        hasPreviousSyncedCyclePhase = true;
        return cyclePhase;
    }

    /// <summary>
    /// Reads Fill Build and integrates its configured extra multiple of the current colour-cycle
    /// advance. Integrating the rush preserves tunnel phase; scaling absolute <c>effectTime</c> would
    /// make the bands jump when a Fill starts or ends. Following the served phase advance keeps the
    /// Fill proportional through both stalls and surges of the pumping motion.
    /// </summary>
    /// <param name="cyclePhaseAdvance">Forward movement of the served colour-cycle phase this frame.</param>
    private void UpdateFillEnvelope(float cyclePhaseAdvance)
    {
        fillEnv = beatManager.Fill.In.Build();
        fillScroll = Mathf.Repeat(
            fillScroll + (cyclePhaseAdvance * SyncSettings.FillScrollRateMultiplier * fillEnv),
            1f);
    }

    /// <summary>
    /// Reads Drop Decay and integrates a reverse multiple of the current colour-cycle advance. The
    /// reverse phase intentionally opposes the Fill rush, so the Drop reads as an inward warp instead
    /// of a stronger version of the build, while retaining the pumping cycle's instantaneous rate.
    /// </summary>
    /// <param name="cyclePhaseAdvance">Forward movement of the served colour-cycle phase this frame.</param>
    private void UpdateDropSlam(float cyclePhaseAdvance)
    {
        dropEnv = beatManager.Drop.In.Decay(SyncSettings.DropBars * 4);
        dropScroll = Mathf.Repeat(
            dropScroll - (cyclePhaseAdvance * SyncSettings.DropReverseScrollRateMultiplier * dropEnv),
            1f);
    }

    /// <summary>Renders one frame of radial tunnel bands directly into the Buffer.</summary>
    public override void Draw()
    {
        bool isSynced = beatManager.IsSynced;
        float cyclePhase;
        float cyclePhaseAdvance;
        if (isSynced)
        {
            Duration cycleDuration = CurrentCycleDuration();
            cyclePhase = SampleSyncedCyclePhase(cycleDuration, out cyclePhaseAdvance);
        }
        else
        {
            cyclePhase = effectTime * scrollSpeed;
            cyclePhaseAdvance = 0f;
            hasPreviousSyncedCyclePhase = false;
        }

        UpdateFillEnvelope(cyclePhaseAdvance);
        UpdateDropSlam(cyclePhaseAdvance);

        float ringCompression = 1f +
            (SyncSettings.FillRingCompression * fillEnv) +
            (SyncSettings.DropRingCompression * dropEnv);

        float centerScale = isSynced
            ? SyncSettings.CenterScale
            : standaloneSettings.CenterScale;
        float radialPhaseScale = centerScale * radialMix * ringCompression;

        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        conditionedPalette.Refresh(APalette, paletteConditioning);

        for (int i = 0; i < Penrose.Total; i++)
        {
            float radialPhase = tiles[i].radius * radialPhaseScale;
            float phase = (i * tileIndexPhaseStep + cyclePhase + fillScroll + dropScroll +
                radialPhase) % 1f;
            buffer[i] = conditionedPalette.ReadCyclic(phase, doblend: true);
        }
    }
}

/// <summary>
/// The serializable value shape shared by Tunnel's fully populated Standalone Defaults and saved
/// Standalone Settings; Unity may create an empty instance before serialized values are applied.
/// </summary>
[Serializable]
public sealed class TunnelStandaloneSettings
{
    /// <summary>Per-activation range for the phase step applied between consecutive Tile indexes.</summary>
    public FloatRange TileIndexPhaseStep;

    /// <summary>Per-activation base scroll-speed range.</summary>
    public FloatRange ScrollSpeed;

    /// <summary>Per-activation range mixing scaled Tile radius into the tunnel phase.</summary>
    public FloatRange RadialMix;

    /// <summary>Scale applied to the Tile-center distance that the radial mix reads.</summary>
    [Min(0f)] public float CenterScale;

    /// <summary>
    /// Live effect-local palette conditioning for Standalone Mode, independently saved so tuning it
    /// cannot drift the Synced look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>
    /// Copies every Tunnel Standalone Setting, including palette conditioning, range endpoints, and
    /// Rails.
    /// </summary>
    public void CopyFrom(TunnelStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TileIndexPhaseStep = new FloatRange(
            source.TileIndexPhaseStep.Min,
            source.TileIndexPhaseStep.Max,
            source.TileIndexPhaseStep.LowRail,
            source.TileIndexPhaseStep.HighRail);
        ScrollSpeed = new FloatRange(
            source.ScrollSpeed.Min,
            source.ScrollSpeed.Max,
            source.ScrollSpeed.LowRail,
            source.ScrollSpeed.HighRail);
        RadialMix = new FloatRange(
            source.RadialMix.Min,
            source.RadialMix.Max,
            source.RadialMix.LowRail,
            source.RadialMix.HighRail);
        CenterScale = source.CenterScale;
        PaletteConditioning = source.PaletteConditioning;
    }
}

/// <summary>The serializable value shape shared by Tunnel's Sync Defaults and Sync Settings.</summary>
[Serializable]
public sealed class TunnelSyncSettings
{
    /// <summary>Per-Roll range for the phase step applied between consecutive Tile indexes.</summary>
    public FloatRange TileIndexPhaseStep;

    /// <summary>Duration of one full colour cycle at Low Energy.</summary>
    public Duration LowCycleDuration;

    /// <summary>Duration of one full colour cycle at Mid Energy.</summary>
    public Duration MidCycleDuration;

    /// <summary>Duration of one full colour cycle at High Energy.</summary>
    public Duration HighCycleDuration;

    /// <summary>Per-Roll range mixing scaled Tile radius into the tunnel phase.</summary>
    public FloatRange RadialMix;

    /// <summary>Scale applied to the Tile-center distance that the radial mix reads.</summary>
    [Min(0f)] public float CenterScale;

    /// <summary>
    /// Live effect-local palette conditioning for Synced Mode, independently saved so tuning it
    /// cannot drift the Standalone look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Extra color-scroll rate multiple at full Fill.</summary>
    [Min(0f)] public float FillScrollRateMultiplier;

    /// <summary>Extra ring-compression multiple at full Fill.</summary>
    [Min(0f)] public float FillRingCompression;

    /// <summary>Drop decay length in bars.</summary>
    [Min(1)] public int DropBars;

    /// <summary>Reverse color-scroll rate multiple at the Drop's peak.</summary>
    [Min(0f)] public float DropReverseScrollRateMultiplier;

    /// <summary>Extra ring-compression multiple at the Drop's peak.</summary>
    [Min(0f)] public float DropRingCompression;

    /// <summary>Copies every Tunnel Sync Setting from another value.</summary>
    public void CopyFrom(TunnelSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TileIndexPhaseStep = new FloatRange(
            source.TileIndexPhaseStep.Min,
            source.TileIndexPhaseStep.Max,
            source.TileIndexPhaseStep.LowRail,
            source.TileIndexPhaseStep.HighRail);
        LowCycleDuration = source.LowCycleDuration;
        MidCycleDuration = source.MidCycleDuration;
        HighCycleDuration = source.HighCycleDuration;
        RadialMix = new FloatRange(
            source.RadialMix.Min,
            source.RadialMix.Max,
            source.RadialMix.LowRail,
            source.RadialMix.HighRail);
        CenterScale = source.CenterScale;
        PaletteConditioning = source.PaletteConditioning;
        FillScrollRateMultiplier = source.FillScrollRateMultiplier;
        FillRingCompression = source.FillRingCompression;
        DropBars = source.DropBars;
        DropReverseScrollRateMultiplier = source.DropReverseScrollRateMultiplier;
        DropRingCompression = source.DropRingCompression;
    }
}
