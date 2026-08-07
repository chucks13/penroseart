using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a direct tile-space tunnel from radius, density, and time.
/// </summary>
/// <remarks>
/// FILL: the tunnel rushes (scroll accelerates) and zooms (radial bands tighten) as the Fill builds,
/// both driven by <see cref="BeatManager.Fill"/> Build.
/// DROP: <see cref="BeatManager.Drop"/> Decay drives a hard reverse warp plus a deep zoom over two bars.
/// </remarks>
[EffectSyncSettings(typeof(TunnelSyncSettingsAsset))]
public class Tunnel : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored minimum band density for the unchanged Standalone look.</summary>
    private const float StandaloneDensityMin = 0.0004f;

    /// <summary>Authored maximum band density for the unchanged Standalone look.</summary>
    private const float StandaloneDensityMax = 0.003f;

    /// <summary>Authored minimum scroll speed for the unchanged Standalone look.</summary>
    private const float StandaloneSpeedMin = 0.1f;

    /// <summary>Authored maximum scroll speed for the unchanged Standalone look.</summary>
    private const float StandaloneSpeedMax = 1f;

    /// <summary>Authored minimum radial mix for the unchanged Standalone look.</summary>
    private const float StandaloneMixMin = 0.01f;

    /// <summary>Authored maximum radial mix for the unchanged Standalone look.</summary>
    private const float StandaloneMixMax = 0.2f;

    /// <summary>Authored tile-center scale for the unchanged Standalone look.</summary>
    private const float StandaloneCenterScale = 0.03f;

    // Sync Defaults

    /// <summary>Authored first Waveform energy admitted by Tunnel in Synced Mode.</summary>
    private const Energy SyncWaveformEnergyOne = Energy.Low;

    /// <summary>Authored second Waveform energy admitted by Tunnel in Synced Mode.</summary>
    private const Energy SyncWaveformEnergyTwo = Energy.Mid;

    /// <summary>Authored extra scroll-rate multiple at full Fill.</summary>
    private const float SyncFillRush = 5f;

    /// <summary>Authored extra ring-compression multiple at full Fill.</summary>
    private const float SyncFillZoom = 3f;

    /// <summary>Authored floor of the Waveform-driven brightness pulse.</summary>
    private const float SyncBeatBrightnessFloor = 0.75f;

    /// <summary>Authored Drop decay length in bars.</summary>
    private const int SyncDropBars = 2;

    /// <summary>Authored reverse scroll-rate multiple at the Drop's peak.</summary>
    private const float SyncDropRush = 10f;

    /// <summary>Authored extra ring-compression multiple at the Drop's peak.</summary>
    private const float SyncDropZoom = 6f;

    /// <summary>The tunnel intensifies its rush/zoom motion for a Fill, and slams a reverse warp for a Drop;
    /// its driving motion suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Tunnel's Standalone Defaults.</summary>
    public static TunnelStandaloneSettings StandaloneSettings => new TunnelStandaloneSettings(
        new FloatRange(StandaloneDensityMin, StandaloneDensityMax),
        new FloatRange(StandaloneSpeedMin, StandaloneSpeedMax),
        new FloatRange(StandaloneMixMin, StandaloneMixMax),
        StandaloneCenterScale);

    /// <summary>Resolves a fresh copy of Tunnel's file-local Sync Defaults.</summary>
    public static TunnelSyncSettings SyncDefaults => new TunnelSyncSettings
    {
        WaveformEnergyOne = SyncWaveformEnergyOne,
        WaveformEnergyTwo = SyncWaveformEnergyTwo,
        FillRush = SyncFillRush,
        FillZoom = SyncFillZoom,
        BeatBrightnessFloor = SyncBeatBrightnessFloor,
        DropBars = SyncDropBars,
        DropRush = SyncDropRush,
        DropZoom = SyncDropZoom,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private TunnelStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private TunnelSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Current randomly rolled band density.</summary>
    private float density;

    /// <summary>Current randomly rolled base scroll speed.</summary>
    private float speed;

    /// <summary>Current randomly rolled radial mix.</summary>
    private float mix;

    /// <summary>Fill Build amount driving rush and zoom.</summary>
    private float fillEnv;

    /// <summary>Integrated extra scroll phase from the Fill rush, kept in [0,1).</summary>
    private float fillScroll;

    /// <summary>Drop Decay amount driving the reverse warp and zoom punch.</summary>
    private float dropEnv;

    /// <summary>Integrated reverse scroll phase from the Drop warp, kept in [0,1).</summary>
    private float dropScroll;

    /// <summary>Initializes the fixed Standalone Settings and per-activation random state.</summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        Reroll();
        fillEnv = 0f;
        fillScroll = 0f;
        dropEnv = 0f;
        dropScroll = 0f;
        buffer.Clear();
    }

    /// <summary>
    /// Resolves Sync Settings, then re-rolls density, speed, mix, and Waveform in the original order.
    /// </summary>
    private void Reroll()
    {
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Tunnel),
            SyncDefaults);
        density = Random.Range(standaloneSettings.Density.Min, standaloneSettings.Density.Max);
        speed = Random.Range(standaloneSettings.Speed.Min, standaloneSettings.Speed.Max);
        mix = Random.Range(standaloneSettings.Mix.Min, standaloneSettings.Mix.Max);
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
        return $"Density: {density}\n" +
        $"Speed: {speed}\n" +
        $"Mix: {mix}\n" +
        (fillEnv > 0.01f ? $"FILL {fillEnv:0.00}\n" : "") +
        (dropEnv > 0.01f ? $"DROP {dropEnv:0.00}\n" : "");
    }

    /// <summary>Reads Fill Build and integrates its configured extra scroll rate without jumping phase.</summary>
    private void UpdateFillEnvelope()
    {
        fillEnv = beatManager.Fill.In.Build();
        fillScroll = Mathf.Repeat(
            fillScroll + (speed * SyncSettings.FillRush * fillEnv * effectDelta),
            1f);
    }

    /// <summary>Reads Drop Decay and integrates its configured reverse scroll without jumping phase.</summary>
    private void UpdateDropSlam()
    {
        dropEnv = beatManager.Drop.In.Decay(SyncSettings.DropBars * 4);
        dropScroll = Mathf.Repeat(
            dropScroll - (speed * SyncSettings.DropRush * dropEnv * effectDelta),
            1f);
    }

    /// <summary>Renders one frame of radial tunnel bands directly into the tile buffer.</summary>
    public override void Draw()
    {
        // Beat pulse scales tunnel brightness without changing the tunnel phase.
        float beatBrightness = waveform.Lerp(SyncSettings.BeatBrightnessFloor, 1f);
        UpdateFillEnvelope();
        UpdateDropSlam();

        float zoom = 1f + (SyncSettings.FillZoom * fillEnv) + (SyncSettings.DropZoom * dropEnv);

        for (int i = 0; i < Penrose.Total; i++)
        {
            float x = Mathf.Abs(tiles[i].center.x * standaloneSettings.CenterScale);
            float y = Mathf.Abs(tiles[i].center.y * standaloneSettings.CenterScale);
            float distance = Mathf.Sqrt((x * x) + (y * y));
            float phase = (i * density + (effectTime * speed) + fillScroll + dropScroll + (distance * mix * zoom)) % 1f;
            buffer[i] = Color.HSVToRGB(phase, 1f, 1f) * beatBrightness;
        }
    }
}

/// <summary>The non-editable Standalone Settings that reproduce Tunnel's authored no-music look.</summary>
public sealed class TunnelStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from Tunnel's file-local defaults.</summary>
    public TunnelStandaloneSettings(FloatRange density, FloatRange speed, FloatRange mix, float centerScale)
    {
        Density = density;
        Speed = speed;
        Mix = mix;
        CenterScale = centerScale;
    }

    /// <summary>Per-activation band-density range.</summary>
    public FloatRange Density;

    /// <summary>Per-activation base scroll-speed range.</summary>
    public FloatRange Speed;

    /// <summary>Per-activation radial-mix range.</summary>
    public FloatRange Mix;

    /// <summary>Scale from tile-center coordinates into radial tunnel space.</summary>
    public float CenterScale;
}

/// <summary>Editable music-response values saved as Tunnel's Sync Settings.</summary>
[Serializable]
public sealed class TunnelSyncSettings
{
    /// <summary>First Waveform energy admitted when Tunnel rolls its musical response.</summary>
    public Energy WaveformEnergyOne;

    /// <summary>Second Waveform energy admitted when Tunnel rolls its musical response.</summary>
    public Energy WaveformEnergyTwo;

    /// <summary>Extra scroll-rate multiple at full Fill.</summary>
    [Min(0f)] public float FillRush;

    /// <summary>Extra ring-compression multiple at full Fill.</summary>
    [Min(0f)] public float FillZoom;

    /// <summary>Floor of the Waveform-driven brightness pulse.</summary>
    [Range(0f, 1f)] public float BeatBrightnessFloor;

    /// <summary>Drop decay length in bars.</summary>
    [Min(1)] public int DropBars;

    /// <summary>Reverse scroll-rate multiple at the Drop's peak.</summary>
    [Min(0f)] public float DropRush;

    /// <summary>Extra ring-compression multiple at the Drop's peak.</summary>
    [Min(0f)] public float DropZoom;

    /// <summary>Copies every Tunnel Sync Setting from another value.</summary>
    public void CopyFrom(TunnelSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        WaveformEnergyOne = source.WaveformEnergyOne;
        WaveformEnergyTwo = source.WaveformEnergyTwo;
        FillRush = source.FillRush;
        FillZoom = source.FillZoom;
        BeatBrightnessFloor = source.BeatBrightnessFloor;
        DropBars = source.DropBars;
        DropRush = source.DropRush;
        DropZoom = source.DropZoom;
    }
}
