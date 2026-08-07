using Random = UnityEngine.Random;
using UnityEngine;
using System;

/// <summary>
/// Renders expanding screen-space ripple rings and maps them to Penrose tiles.
/// </summary>
[EffectSyncSettings(typeof(RippleSyncSettingsAsset))]
public class Ripple : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored minimum per-frame drop-spawn threshold for the unchanged Standalone look.</summary>
    private const float StandaloneIntensityMin = 0.01f;

    /// <summary>Authored maximum per-frame drop-spawn threshold for the unchanged Standalone look.</summary>
    private const float StandaloneIntensityMax = 0.02f;

    /// <summary>Authored minimum pre-division drop velocity for the unchanged Standalone look.</summary>
    private const float StandaloneVelocityMin = 0.01f;

    /// <summary>Authored maximum pre-division drop velocity for the unchanged Standalone look.</summary>
    private const float StandaloneVelocityMax = 0.9f;

    /// <summary>Authored divisor applied to each randomly rolled drop velocity.</summary>
    private const float StandaloneVelocityDivisor = 2000f;

    /// <summary>Authored divisor that maps screen-space distance into ripple phase.</summary>
    private const float StandaloneDistanceDivisor = 20f;

    /// <summary>Authored palette phase offset for the unchanged Standalone look.</summary>
    private const float StandalonePaletteOffset = 0.5f;

    /// <summary>Authored fixed hue shift for the unchanged Standalone look.</summary>
    private const float StandaloneHueShift = 0.2f;

    // Sync Defaults

    /// <summary>Authored maximum Waveform-driven hue shift in Synced Mode.</summary>
    private const float SyncHueShiftMax = 0.2f;

    /// <summary>Ripple can pulse a Fill and land a Drop, and suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Ripple's Standalone Defaults.</summary>
    public static RippleStandaloneSettings StandaloneSettings => new RippleStandaloneSettings(
        new FloatRange(StandaloneIntensityMin, StandaloneIntensityMax),
        new FloatRange(StandaloneVelocityMin, StandaloneVelocityMax),
        StandaloneVelocityDivisor,
        StandaloneDistanceDivisor,
        StandalonePaletteOffset,
        StandaloneHueShift);

    /// <summary>Resolves a fresh copy of Ripple's file-local Sync Defaults.</summary>
    public static RippleSyncSettings SyncDefaults => new RippleSyncSettings
    {
        HueShiftMax = SyncHueShiftMax,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private RippleStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private RippleSyncSettings SyncSettings { get; set; } = SyncDefaults;

    private Color startColor;
    private Color endColor;
    private Drop[] drops;
    private Vector2 screen;
    private float intensity;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Drops {drops.Length}";
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        drops = new Drop[0];
    }

    /// <summary>
    /// Resolves Effect Settings and initializes per-activation random state before drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Ripple),
            SyncDefaults);
        waveform = waveforms.Random();
        intensity = Random.Range(standaloneSettings.Intensity.Min, standaloneSettings.Intensity.Max);
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
        // Beat pulse scales ripple brightness while drop radius/progression remains independent.
        // Standalone reads the file-local hue shift so a Sync Settings edit can never move the
        // Standalone look; Waveform.Lerp returns its second argument when no live clock is sampled.
        float hueShift = waveform.Lerp(
            0f,
            beatManager.IsSynced ? SyncSettings.HueShiftMax : standaloneSettings.HueShift);
        if (Random.value < intensity)
        {
            Array.Resize(ref drops, drops.Length + 1);
            drops[drops.Length - 1] = new Drop(standaloneSettings);
        }
        buffer.Fade();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                screen.x = x;
                screen.y = y;
                var idx = x + (y * width);
                var sum = 0f;
                for (int i = 0; i < drops.Length; i++)
                {
                    drops[i].Update(effectDelta);
                    var d = Vector2.Distance(screen, drops[i].Position);
                    sum += (drops[i].radius - (d / standaloneSettings.DistanceDivisor)).Clamp01();
                }
                sum += standaloneSettings.PaletteOffset;
                sum %= 1f;
                screenBuffer[idx] = APalette.read(sum + hueShift, true);
            }
        }

        ConvertScreenBuffer(ref screenBuffer, in buffer);
    }

    /// <summary>
    /// Expanding screen-space ripple source.
    /// </summary>
    public class Drop
    {
        private Vector2 position;
        private float velocity;
        public float radius = 0f;

        /// <summary>
        /// Creates a ripple drop at a random screen position using the activation's Standalone Settings.
        /// </summary>
        public Drop(RippleStandaloneSettings standaloneSettings)
        {
            velocity = Random.Range(standaloneSettings.Velocity.Min, standaloneSettings.Velocity.Max) /
                standaloneSettings.VelocityDivisor;
            position = new Vector2(Random.Range(0, width), Random.Range(0, height));
        }

        public Vector2 Position => position;
        public float Radius => radius;

        /// <summary>
        /// Expands the ripple radius and respawns when it grows past the screen.
        /// </summary>
        public void Update(float deltaTime)
        {
            radius += deltaTime * velocity;
        }
    }
}

/// <summary>The resolved Standalone Settings that preserve Ripple's authored no-music look.</summary>
public sealed class RippleStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from Ripple's file-local defaults.</summary>
    public RippleStandaloneSettings(
        FloatRange intensity,
        FloatRange velocity,
        float velocityDivisor,
        float distanceDivisor,
        float paletteOffset,
        float hueShift)
    {
        Intensity = intensity;
        Velocity = velocity;
        VelocityDivisor = velocityDivisor;
        DistanceDivisor = distanceDivisor;
        PaletteOffset = paletteOffset;
        HueShift = hueShift;
    }

    /// <summary>Per-activation range for the per-frame drop-spawn threshold.</summary>
    public FloatRange Intensity;

    /// <summary>Per-drop pre-division velocity range.</summary>
    public FloatRange Velocity;

    /// <summary>Divisor applied to each randomly rolled drop velocity.</summary>
    public float VelocityDivisor;

    /// <summary>Divisor that maps screen-space distance into ripple phase.</summary>
    public float DistanceDivisor;

    /// <summary>Palette phase offset applied before wrapping the ripple sum.</summary>
    public float PaletteOffset;

    /// <summary>Fixed hue shift applied to every palette read in Standalone Mode.</summary>
    public float HueShift;
}

/// <summary>The saved-or-default musical-response settings used by Ripple in Synced Mode.</summary>
[Serializable]
public sealed class RippleSyncSettings
{
    /// <summary>Maximum hue shift reached at the Waveform peak in Synced Mode.</summary>
    [Range(0f, 1f)] public float HueShiftMax;

    /// <summary>Copies every Ripple Sync Setting from another value.</summary>
    public void CopyFrom(RippleSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        HueShiftMax = source.HueShiftMax;
    }
}
