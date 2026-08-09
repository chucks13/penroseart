using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Combines two internally owned child Effects using a Perlin noise mask and colored border band.
/// </summary>
[EffectSyncSettings(typeof(NoiseMixerSyncSettingsAsset))]
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

    /// <summary>Resolves a fresh immutable-by-convention copy of NoiseMixer's Standalone Defaults.</summary>
    public static NoiseMixerStandaloneSettings StandaloneSettings => new NoiseMixerStandaloneSettings
    {
        BorderSaturation = StandaloneBorderSaturation,
        BorderValue = StandaloneBorderValue,
        DistortionModeMin = StandaloneDistortionModeMin,
        DistortionModeMaxExclusive = StandaloneDistortionModeMaxExclusive,
        NoiseScale = StandaloneNoiseScale,
        BaseMaskWidth = StandaloneBaseMaskWidth,
        PeakMaskWidth = StandalonePeakMaskWidth,
    };

    /// <summary>Resolves a fresh copy of NoiseMixer's file-local Sync Defaults.</summary>
    public static NoiseMixerSyncSettings SyncDefaults => new NoiseMixerSyncSettings
    {
        BorderSaturation = SyncBorderSaturation,
        BorderValue = SyncBorderValue,
        DistortionModeMin = SyncDistortionModeMin,
        DistortionModeMaxExclusive = SyncDistortionModeMaxExclusive,
        NoiseScale = SyncNoiseScale,
        BaseMaskWidth = SyncBaseMaskWidth,
        PeakMaskWidth = SyncPeakMaskWidth,
        RhythmTimeOffset = SyncRhythmTimeOffset,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private NoiseMixerStandaloneSettings standaloneSettings = StandaloneSettings;

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
        standaloneSettings = StandaloneSettings;
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

        int distortionModeMin = beatManager.IsSynced
            ? SyncSettings.DistortionModeMin
            : standaloneSettings.DistortionModeMin;
        int distortionModeMaxExclusive = beatManager.IsSynced
            ? SyncSettings.DistortionModeMaxExclusive
            : standaloneSettings.DistortionModeMaxExclusive;
        distortionMode = Random.Range(distortionModeMin, distortionModeMaxExclusive);

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
        float baseMaskWidth = isSynced
            ? SyncSettings.BaseMaskWidth
            : standaloneSettings.BaseMaskWidth;
        float peakMaskWidth = isSynced
            ? SyncSettings.PeakMaskWidth
            : standaloneSettings.PeakMaskWidth;

        float sampleTime = effectTime + (SyncSettings.RhythmTimeOffset * rhythm);
        float width = baseMaskWidth;
        if (distortionMode == 1)
        {
            width = waveform.Lerp(baseMaskWidth, peakMaskWidth);
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

/// <summary>The non-editable Standalone Settings that reproduce NoiseMixer's authored no-music look.</summary>
public sealed class NoiseMixerStandaloneSettings
{
    /// <summary>Border saturation used in Standalone Mode.</summary>
    public float BorderSaturation;

    /// <summary>Border value used in Standalone Mode.</summary>
    public float BorderValue;

    /// <summary>Inclusive lower endpoint supplied to the Standalone distortion-mode roll.</summary>
    public int DistortionModeMin;

    /// <summary>Exclusive upper endpoint supplied to the Standalone distortion-mode roll.</summary>
    public int DistortionModeMaxExclusive;

    /// <summary>Scale from tile-center coordinates into Perlin noise space in Standalone Mode.</summary>
    public float NoiseScale;

    /// <summary>Base half-width of the noise-mask border in Standalone Mode.</summary>
    public float BaseMaskWidth;

    /// <summary>Half-width used by the no-placement Waveform fallback in Standalone Mode.</summary>
    public float PeakMaskWidth;
}

/// <summary>Editable music-response values saved as NoiseMixer's Sync Settings.</summary>
[Serializable]
public sealed class NoiseMixerSyncSettings
{
    /// <summary>Border saturation used in Synced Mode.</summary>
    [Range(0f, 1f)] public float BorderSaturation;

    /// <summary>Border value used in Synced Mode.</summary>
    [Range(0f, 1f)] public float BorderValue;

    /// <summary>Inclusive lower endpoint supplied to a Synced Mode distortion-mode roll.</summary>
    [Min(0)] public int DistortionModeMin;

    /// <summary>Exclusive upper endpoint supplied to a Synced Mode distortion-mode roll.</summary>
    [Min(1)] public int DistortionModeMaxExclusive;

    /// <summary>Scale from tile-center coordinates into Perlin noise space in Synced Mode.</summary>
    [Min(0f)] public float NoiseScale;

    /// <summary>Base half-width of the noise-mask border in Synced Mode.</summary>
    [Min(0f)] public float BaseMaskWidth;

    /// <summary>Half-width reached by the noise-mask border at a Waveform peak.</summary>
    [Min(0f)] public float PeakMaskWidth;

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
        DistortionModeMin = source.DistortionModeMin;
        DistortionModeMaxExclusive = source.DistortionModeMaxExclusive;
        NoiseScale = source.NoiseScale;
        BaseMaskWidth = source.BaseMaskWidth;
        PeakMaskWidth = source.PeakMaskWidth;
        RhythmTimeOffset = source.RhythmTimeOffset;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
