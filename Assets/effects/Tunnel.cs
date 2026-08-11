using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a direct tile-space tunnel from Tile radius, Tile-index phase, and time.
/// </summary>
/// <remarks>
/// FILL: the tunnel rushes (scroll accelerates) and compresses its radial bands as the Fill builds,
/// both driven by <see cref="BeatManager.Fill"/> Build.
/// DROP: <see cref="BeatManager.Drop"/> Decay drives a hard reverse warp plus deep radial-band
/// compression over two bars.
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
    /// Authored minimum radial phase scale for the unchanged Standalone look. It carries the former
    /// <c>0.01</c> radial mix through the former <c>0.03</c> Tile-center scale; smaller values spread
    /// the rings wider.
    /// </summary>
    private const float StandaloneRadialPhaseScaleMin = 0.0003f;

    /// <summary>
    /// Authored maximum radial phase scale for the unchanged Standalone look. It carries the former
    /// <c>0.2</c> radial mix through the former <c>0.03</c> Tile-center scale.
    /// </summary>
    private const float StandaloneRadialPhaseScaleMax = 0.006f;

    // Sync Defaults

    /// <summary>Authored minimum per-Tile index phase step for the current Synced look.</summary>
    private const float SyncTileIndexPhaseStepMin = 0.0004f;

    /// <summary>Authored maximum per-Tile index phase step for the current Synced look.</summary>
    private const float SyncTileIndexPhaseStepMax = 0.003f;

    /// <summary>Authored minimum scroll speed for the current Synced look.</summary>
    private const float SyncScrollSpeedMin = 0.1f;

    /// <summary>Authored maximum scroll speed for the current Synced look.</summary>
    private const float SyncScrollSpeedMax = 1f;

    /// <summary>Authored minimum radial phase scale for the current Synced look.</summary>
    private const float SyncRadialPhaseScaleMin = 0.0003f;

    /// <summary>Authored maximum radial phase scale for the current Synced look.</summary>
    private const float SyncRadialPhaseScaleMax = 0.006f;

    /// <summary>Authored first Waveform energy admitted by Tunnel in Synced Mode.</summary>
    private const Energy SyncWaveformEnergyOne = Energy.Low;

    /// <summary>Authored second Waveform energy admitted by Tunnel in Synced Mode.</summary>
    private const Energy SyncWaveformEnergyTwo = Energy.Mid;

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

    /// <summary>
    /// Authored floor of the Waveform-driven brightness pulse: higher values make a shallower pulse,
    /// and one disables the brightness pulse. Tune while watching the live readout.
    /// </summary>
    private const float SyncBeatBrightnessFloor = 0.75f;

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

    /// <summary>The tunnel intensifies its scroll and ring compression for a Fill, and slams a reverse
    /// warp for a Drop; its driving motion suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

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
        RadialPhaseScale = new FloatRange(
            StandaloneRadialPhaseScaleMin,
            StandaloneRadialPhaseScaleMax),
    };

    /// <summary>Resolves a fresh copy of Tunnel's file-local Sync Defaults.</summary>
    public static TunnelSyncSettings SyncDefaults => new()
    {
        TileIndexPhaseStep = new FloatRange(SyncTileIndexPhaseStepMin, SyncTileIndexPhaseStepMax),
        ScrollSpeed = new FloatRange(SyncScrollSpeedMin, SyncScrollSpeedMax),
        RadialPhaseScale = new FloatRange(SyncRadialPhaseScaleMin, SyncRadialPhaseScaleMax),
        WaveformEnergyOne = SyncWaveformEnergyOne,
        WaveformEnergyTwo = SyncWaveformEnergyTwo,
        FillScrollRateMultiplier = SyncFillScrollRateMultiplier,
        FillRingCompression = SyncFillRingCompression,
        BeatBrightnessFloor = SyncBeatBrightnessFloor,
        DropBars = SyncDropBars,
        DropReverseScrollRateMultiplier = SyncDropReverseScrollRateMultiplier,
        DropRingCompression = SyncDropRingCompression,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private TunnelStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private TunnelSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Current randomly rolled per-Tile index phase step.</summary>
    private float tileIndexPhaseStep;

    /// <summary>Current randomly rolled base scroll speed.</summary>
    private float scrollSpeed;

    /// <summary>Current randomly rolled radial phase scale.</summary>
    private float radialPhaseScale;

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

    /// <summary>Resolves both saved settings surfaces and initializes the per-activation Roll.</summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Tunnel),
            StandaloneDefaults);
        Reroll();
        fillEnv = 0f;
        fillScroll = 0f;
        dropEnv = 0f;
        dropScroll = 0f;
        buffer.Clear();
    }

    /// <summary>
    /// Resolves Sync Settings, selects the active mode's Roll ranges, then re-rolls Tile-index phase,
    /// scroll speed, radial phase, and Waveform in the original random order.
    /// </summary>
    private void Reroll()
    {
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Tunnel),
            SyncDefaults);

        bool isSynced = beatManager.IsSynced;
        FloatRange tileIndexPhaseStepRange = isSynced
            ? SyncSettings.TileIndexPhaseStep
            : standaloneSettings.TileIndexPhaseStep;
        FloatRange scrollSpeedRange = isSynced
            ? SyncSettings.ScrollSpeed
            : standaloneSettings.ScrollSpeed;
        FloatRange radialPhaseScaleRange = isSynced
            ? SyncSettings.RadialPhaseScale
            : standaloneSettings.RadialPhaseScale;

        tileIndexPhaseStep = Random.Range(tileIndexPhaseStepRange.Min, tileIndexPhaseStepRange.Max);
        scrollSpeed = Random.Range(scrollSpeedRange.Min, scrollSpeedRange.Max);
        radialPhaseScale = Random.Range(radialPhaseScaleRange.Min, radialPhaseScaleRange.Max);
        waveform = waveforms.Random(SyncSettings.WaveformEnergyOne, SyncSettings.WaveformEnergyTwo);
    }

    /// <summary>Re-rolls the tunnel when the musical Grid returns to one.</summary>
    protected override void OnNewGrid()
    {
        Reroll();
    }

    /// <summary>Reserved deactivation hook. Controller does not currently call this.</summary>
    public override void OnEnd() { }

    /// <summary>Returns the current rolls and live musical envelopes for the Controller debug display.</summary>
    public override string DebugText()
    {
        return $"Tile index phase step: {tileIndexPhaseStep}\n" +
        $"Scroll speed: {scrollSpeed}\n" +
        $"Radial phase scale: {radialPhaseScale}\n" +
        (fillEnv > 0.01f ? $"FILL {fillEnv:0.00}\n" : "") +
        (dropEnv > 0.01f ? $"DROP {dropEnv:0.00}\n" : "");
    }

    /// <summary>
    /// Reads Fill Build and integrates its configured extra scroll rate. Integrating the rush
    /// preserves tunnel phase; scaling absolute <c>effectTime</c> would make the bands jump when a
    /// Fill starts or ends.
    /// </summary>
    private void UpdateFillEnvelope()
    {
        fillEnv = beatManager.Fill.In.Build();
        fillScroll = Mathf.Repeat(
            fillScroll + (scrollSpeed * SyncSettings.FillScrollRateMultiplier * fillEnv * effectDelta),
            1f);
    }

    /// <summary>
    /// Reads Drop Decay and integrates reverse scroll. The reverse phase intentionally opposes the
    /// Fill rush, so the Drop reads as an inward warp instead of a stronger version of the build.
    /// </summary>
    private void UpdateDropSlam()
    {
        dropEnv = beatManager.Drop.In.Decay(SyncSettings.DropBars * 4);
        dropScroll = Mathf.Repeat(
            dropScroll - (scrollSpeed * SyncSettings.DropReverseScrollRateMultiplier * dropEnv * effectDelta),
            1f);
    }

    /// <summary>Renders one frame of radial tunnel bands directly into the Buffer.</summary>
    public override void Draw()
    {
        // The Waveform scales tunnel brightness without changing the tunnel phase.
        float beatBrightness = waveform.Lerp(SyncSettings.BeatBrightnessFloor, 1f);
        UpdateFillEnvelope();
        UpdateDropSlam();

        float ringCompression = 1f +
            (SyncSettings.FillRingCompression * fillEnv) +
            (SyncSettings.DropRingCompression * dropEnv);

        for (int i = 0; i < Penrose.Total; i++)
        {
            float radialPhase = tiles[i].radius * radialPhaseScale * ringCompression;
            float phase = (i * tileIndexPhaseStep + (effectTime * scrollSpeed) + fillScroll +
                dropScroll + radialPhase) % 1f;
            buffer[i] = Color.HSVToRGB(phase, 1f, 1f) * beatBrightness;
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

    /// <summary>Per-activation range scaling Tile radius into the tunnel phase.</summary>
    public FloatRange RadialPhaseScale;

    /// <summary>Copies every Tunnel Standalone Setting, including range endpoints and Rails.</summary>
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
        RadialPhaseScale = new FloatRange(
            source.RadialPhaseScale.Min,
            source.RadialPhaseScale.Max,
            source.RadialPhaseScale.LowRail,
            source.RadialPhaseScale.HighRail);
    }
}

/// <summary>The serializable value shape shared by Tunnel's Sync Defaults and Sync Settings.</summary>
[Serializable]
public sealed class TunnelSyncSettings
{
    /// <summary>Per-Roll range for the phase step applied between consecutive Tile indexes.</summary>
    public FloatRange TileIndexPhaseStep;

    /// <summary>Per-Roll base scroll-speed range.</summary>
    public FloatRange ScrollSpeed;

    /// <summary>Per-Roll range scaling Tile radius into the tunnel phase.</summary>
    public FloatRange RadialPhaseScale;

    /// <summary>First Waveform energy admitted when Tunnel rolls its musical response.</summary>
    public Energy WaveformEnergyOne;

    /// <summary>Second Waveform energy admitted when Tunnel rolls its musical response.</summary>
    public Energy WaveformEnergyTwo;

    /// <summary>Extra color-scroll rate multiple at full Fill.</summary>
    [Min(0f)] public float FillScrollRateMultiplier;

    /// <summary>Extra ring-compression multiple at full Fill.</summary>
    [Min(0f)] public float FillRingCompression;

    /// <summary>Floor of the Waveform-driven brightness pulse.</summary>
    [Range(0f, 1f)] public float BeatBrightnessFloor;

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
        ScrollSpeed = new FloatRange(
            source.ScrollSpeed.Min,
            source.ScrollSpeed.Max,
            source.ScrollSpeed.LowRail,
            source.ScrollSpeed.HighRail);
        RadialPhaseScale = new FloatRange(
            source.RadialPhaseScale.Min,
            source.RadialPhaseScale.Max,
            source.RadialPhaseScale.LowRail,
            source.RadialPhaseScale.HighRail);
        WaveformEnergyOne = source.WaveformEnergyOne;
        WaveformEnergyTwo = source.WaveformEnergyTwo;
        FillScrollRateMultiplier = source.FillScrollRateMultiplier;
        FillRingCompression = source.FillRingCompression;
        BeatBrightnessFloor = source.BeatBrightnessFloor;
        DropBars = source.DropBars;
        DropReverseScrollRateMultiplier = source.DropReverseScrollRateMultiplier;
        DropRingCompression = source.DropRingCompression;
    }
}
