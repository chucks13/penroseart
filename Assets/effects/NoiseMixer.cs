using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Combines two internally owned child Effects using a Perlin noise mask and colored border band.
/// </summary>
[EffectSyncSettings(typeof(NoiseMixerSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(NoiseMixerStandaloneSettingsAsset))]
public class NoiseMixer : MixerBase
{
    // Standalone Defaults

    /// <summary>Authored border saturation for the unchanged Standalone look.</summary>
    private const float StandaloneBorderSaturation = 1f;

    /// <summary>Authored border value for the unchanged Standalone look.</summary>
    private const float StandaloneBorderValue = 1f;

    /// <summary>Authored minimum distortion mode included in the unchanged Standalone roll.</summary>
    private const int StandaloneDistortionModeMin = 0;

    /// <summary>Authored exclusive maximum distortion mode included in the unchanged Standalone roll.</summary>
    private const int StandaloneDistortionModeMaxExclusive = 2;

    /// <summary>Authored scale from tile centers into Perlin noise space for the unchanged Standalone look.</summary>
    private const float StandaloneNoiseScale = 0.07f;

    /// <summary>Authored base half-width of the noise-mask border for the unchanged Standalone look.</summary>
    private const float StandaloneBaseMaskWidth = 0.1f;

    /// <summary>
    /// Authored half-width reached at a Waveform peak and used as the no-placement fallback in Standalone Mode.
    /// </summary>
    private const float StandalonePeakMaskWidth = 0.25f;

    // Sync Defaults

    /// <summary>Authored border saturation used by NoiseMixer in Synced Mode.</summary>
    private const float SyncBorderSaturation = 1f;

    /// <summary>Authored border value used by NoiseMixer in Synced Mode.</summary>
    private const float SyncBorderValue = 1f;

    /// <summary>Authored minimum distortion mode included in a Synced Mode roll.</summary>
    private const int SyncDistortionModeMin = 0;

    /// <summary>Authored exclusive maximum distortion mode included in a Synced Mode roll.</summary>
    private const int SyncDistortionModeMaxExclusive = 2;

    /// <summary>Authored scale from tile centers into Perlin noise space in Synced Mode.</summary>
    private const float SyncNoiseScale = 0.07f;

    /// <summary>Authored base half-width of the noise-mask border in Synced Mode.</summary>
    private const float SyncBaseMaskWidth = 0.1f;

    /// <summary>Authored half-width reached by the noise-mask border at a Waveform peak.</summary>
    private const float SyncPeakMaskWidth = 0.25f;

    /// <summary>Authored extra Perlin sample-time offset at a full Waveform envelope.</summary>
    private const float SyncRhythmTimeOffset = 0.5f;

    /// <summary>The mask slows its drift over the authored eight beats leading into a Drop.</summary>
    private const int SyncDropSlowdownBeats = 8;

    /// <summary>NoiseMixer's blended noise suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh copy of NoiseMixer's file-local Standalone Defaults.</summary>
    public static NoiseMixerStandaloneSettings StandaloneDefaults => new NoiseMixerStandaloneSettings
    {
        BorderSaturation = StandaloneBorderSaturation,
        BorderValue = StandaloneBorderValue,
        DistortionMode = new IntRange(
            StandaloneDistortionModeMin,
            StandaloneDistortionModeMaxExclusive),
        NoiseScale = StandaloneNoiseScale,
        MaskWidth = new FloatRange(StandaloneBaseMaskWidth, StandalonePeakMaskWidth),
    };

    /// <summary>Resolves a fresh copy of NoiseMixer's file-local Sync Defaults.</summary>
    public static NoiseMixerSyncSettings SyncDefaults => new NoiseMixerSyncSettings
    {
        BorderSaturation = SyncBorderSaturation,
        BorderValue = SyncBorderValue,
        DistortionMode = new IntRange(SyncDistortionModeMin, SyncDistortionModeMaxExclusive),
        NoiseScale = SyncNoiseScale,
        MaskWidth = new FloatRange(SyncBaseMaskWidth, SyncPeakMaskWidth),
        RhythmTimeOffset = SyncRhythmTimeOffset,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private NoiseMixerStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private NoiseMixerSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The mask slows its drift using the live Sync Settings before a Drop.</summary>
    protected override int DropSlowdownBeats => SyncSettings.DropSlowdownBeats;

    /// <summary>The two child Effects privately owned and combined by this Mixer.</summary>
    private EffectBase[] effects;

    /// <summary>The border hue rolled during the current activation.</summary>
    private float borderHue;

    /// <summary>
    /// Current distortion mode: zero offsets noise sample time; one modulates the border width.
    /// </summary>
    private int distortionMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        return debugText;
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Resolves settings and initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(NoiseMixer),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(NoiseMixer),
            SyncDefaults);

        waveform = waveforms.Random();
        effects = new EffectBase[2];

        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].RandomizeTime();
            effects[i].Init();
            effects[i].OnStart();
            // NoiseMixer owns the rhythmic shape of the composite, so child Waveforms are suppressed.
            effects[i].waveform = waveforms.None;
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
            borderHue = Random.value;
        }

        IntRange distortionModes = beatManager.IsSynced
            ? SyncSettings.DistortionMode
            : standaloneSettings.DistortionMode;
        distortionMode = Random.Range(distortionModes.MinInclusive, distortionModes.MaxExclusive);

        controller.debugText.text = debugText;
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        for (int i = 0; i < 2; i++)
        {
            effects[i].UpdateTime();
            // Reassert suppression after UpdateTime because the child may acquire a new Waveform on a Grid wrap.
            effects[i].waveform = waveforms.None;
            effects[i].Draw();
        }

        float rhythm = waveform.Envelope;
        bool isSynced = beatManager.IsSynced;
        FloatRange maskWidth = isSynced
            ? SyncSettings.MaskWidth
            : standaloneSettings.MaskWidth;

        float sampleTime = effectTime + (SyncSettings.RhythmTimeOffset * rhythm);
        float width = maskWidth.Min;
        if (distortionMode == 1)
        {
            width = waveform.Lerp(maskWidth.Min, maskWidth.Max);
        }

        float scale = isSynced
            ? SyncSettings.NoiseScale
            : standaloneSettings.NoiseScale;
        float borderSaturation = isSynced
            ? SyncSettings.BorderSaturation
            : standaloneSettings.BorderSaturation;
        float borderValue = isSynced
            ? SyncSettings.BorderValue
            : standaloneSettings.BorderValue;
        Color border = Color.HSVToRGB(borderHue, borderSaturation, borderValue);

        for (int i = 0; i < buffer.Length; i++)
        {
            float x = tiles[i].center.x * scale;
            float y = tiles[i].center.y * scale;
            float z = (distortionMode == 0) ? sampleTime : effectTime; // use local mixer time

            float n = Perlin.Noise(x, y, z);
            if (n > width)
            {
                buffer[i] = effects[0].buffer[i];
            }
            else if (n > -width)
            {
                buffer[i] = border;
            }
            else
            {
                buffer[i] = effects[1].buffer[i];
            }
        }
    }
}

/// <summary>Editable values saved as NoiseMixer's Standalone Settings.</summary>
[Serializable]
public sealed class NoiseMixerStandaloneSettings
{
    /// <summary>Border saturation used in Standalone Mode.</summary>
    public float BorderSaturation;

    /// <summary>Border value used in Standalone Mode.</summary>
    public float BorderValue;

    /// <summary>Per-Roll range selecting the distortion mode; the upper endpoint is exclusive.</summary>
    public IntRange DistortionMode;

    /// <summary>Scale from tile-center coordinates into Perlin noise space in Standalone Mode.</summary>
    public float NoiseScale;

    /// <summary>Noise-mask border half-width from the trough base to the Waveform peak; the peak endpoint doubles as the no-placement fallback, which fixes the Standalone width.</summary>
    public FloatRange MaskWidth;

    /// <summary>Copies every NoiseMixer Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(NoiseMixerStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BorderSaturation = source.BorderSaturation;
        BorderValue = source.BorderValue;
        DistortionMode = new IntRange(
            source.DistortionMode.MinInclusive,
            source.DistortionMode.MaxExclusive,
            source.DistortionMode.LowRail,
            source.DistortionMode.HighRail);
        NoiseScale = source.NoiseScale;
        MaskWidth = new FloatRange(
            source.MaskWidth.Min,
            source.MaskWidth.Max,
            source.MaskWidth.LowRail,
            source.MaskWidth.HighRail);
    }
}

/// <summary>Editable music-response values saved as NoiseMixer's Sync Settings.</summary>
[Serializable]
public sealed class NoiseMixerSyncSettings
{
    /// <summary>Border saturation used in Synced Mode.</summary>
    [Range(0f, 1f)] public float BorderSaturation;

    /// <summary>Border value used in Synced Mode.</summary>
    [Range(0f, 1f)] public float BorderValue;

    /// <summary>Per-Roll range selecting the distortion mode; the upper endpoint is exclusive.</summary>
    public IntRange DistortionMode;

    /// <summary>Scale from tile-center coordinates into Perlin noise space in Synced Mode.</summary>
    [Min(0f)] public float NoiseScale;

    /// <summary>Noise-mask border half-width from the trough base to the Waveform peak; the peak endpoint doubles as the no-placement fallback, which fixes the Standalone width.</summary>
    public FloatRange MaskWidth;

    /// <summary>Extra Perlin sample-time offset at a full Waveform envelope.</summary>
    [Min(0f)] public float RhythmTimeOffset;

    /// <summary>Approach window in beats over which the mask slows before a Drop.</summary>
    [Min(0)] public int DropSlowdownBeats;

    /// <summary>Copies every NoiseMixer Sync Setting from another value.</summary>
    public void CopyFrom(NoiseMixerSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BorderSaturation = source.BorderSaturation;
        BorderValue = source.BorderValue;
        DistortionMode = new IntRange(
            source.DistortionMode.MinInclusive,
            source.DistortionMode.MaxExclusive,
            source.DistortionMode.LowRail,
            source.DistortionMode.HighRail);
        NoiseScale = source.NoiseScale;
        MaskWidth = new FloatRange(
            source.MaskWidth.Min,
            source.MaskWidth.Max,
            source.MaskWidth.LowRail,
            source.MaskWidth.HighRail);
        RhythmTimeOffset = source.RhythmTimeOffset;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
