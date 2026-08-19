using Random = UnityEngine.Random;
using UnityEngine;
using System;

/// <summary>
/// Renders expanding screen-space ripple rings and maps them to Penrose tiles.
/// </summary>
[EffectSyncSettings(typeof(RippleSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(RippleStandaloneSettingsAsset))]
public class Ripple : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored minimum per-frame drop-spawn chance for the unchanged Standalone look.</summary>
    private const float StandaloneDropSpawnChanceMin = 0.01f;

    /// <summary>Authored maximum per-frame drop-spawn chance for the unchanged Standalone look.</summary>
    private const float StandaloneDropSpawnChanceMax = 0.02f;

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

    /// <summary>Authored minimum per-frame drop-spawn chance for the current Synced look.</summary>
    private const float SyncDropSpawnChanceMin = 0.01f;

    /// <summary>Authored maximum per-frame drop-spawn chance for the current Synced look.</summary>
    private const float SyncDropSpawnChanceMax = 0.02f;

    /// <summary>Authored minimum pre-division drop velocity for the current Synced look.</summary>
    private const float SyncVelocityMin = 0.01f;

    /// <summary>Authored maximum pre-division drop velocity for the current Synced look.</summary>
    private const float SyncVelocityMax = 0.9f;

    /// <summary>Authored divisor applied to each randomly rolled drop velocity in Synced Mode.</summary>
    private const float SyncVelocityDivisor = 2000f;

    /// <summary>Authored divisor that maps screen-space distance into ripple phase in Synced Mode.</summary>
    private const float SyncDistanceDivisor = 20f;

    /// <summary>Authored palette phase offset for the current Synced look.</summary>
    private const float SyncPaletteOffset = 0.5f;

    /// <summary>
    /// Authored default Normalized Low presence threshold for the Beat Pulse palette shift in Synced Mode.
    /// </summary>
    /// <remarks>
    /// See Levels and Beat Pulse in <c>CONTEXT.md:189-191,267-269</c> and their wire lanes in
    /// <c>docs/osc-client-contract.md:355-400</c>.
    /// </remarks>
    private const float SyncLowPresenceThreshold = 0.35f;

    /// <summary>Authored maximum Beat Pulse-driven hue shift in Synced Mode.</summary>
    /// <remarks>
    /// See Beat Pulse in <c>CONTEXT.md:267-269</c> and <c>docs/osc-client-contract.md:355-382</c>.
    /// </remarks>
    private const float SyncHueShiftMax = 0.2f;

    /// <summary>
    /// Ripple's Mid/High Energy suitability character; see the Repertoire and Energy entries in
    /// <c>CONTEXT.md</c>.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh copy of Ripple's file-local Standalone Defaults.</summary>
    public static RippleStandaloneSettings StandaloneDefaults => new()
    {
        DropSpawnChance = new FloatRange(
            StandaloneDropSpawnChanceMin,
            StandaloneDropSpawnChanceMax),
        Velocity = new FloatRange(StandaloneVelocityMin, StandaloneVelocityMax),
        VelocityDivisor = StandaloneVelocityDivisor,
        DistanceDivisor = StandaloneDistanceDivisor,
        PaletteOffset = StandalonePaletteOffset,
        HueShift = StandaloneHueShift,
    };

    /// <summary>Resolves a fresh copy of Ripple's file-local Sync Defaults.</summary>
    public static RippleSyncSettings SyncDefaults => new()
    {
        DropSpawnChance = new FloatRange(SyncDropSpawnChanceMin, SyncDropSpawnChanceMax),
        Velocity = new FloatRange(SyncVelocityMin, SyncVelocityMax),
        VelocityDivisor = SyncVelocityDivisor,
        DistanceDivisor = SyncDistanceDivisor,
        PaletteOffset = SyncPaletteOffset,
        LowPresenceThreshold = SyncLowPresenceThreshold,
        HueShiftMax = SyncHueShiftMax,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private RippleStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private RippleSyncSettings SyncSettings { get; set; } = SyncDefaults;

    private Color startColor;
    private Color endColor;
    private Drop[] drops;
    private Vector2 screen;

    /// <summary>Current per-frame chance of spawning a new drop, rolled from the active mode's range.</summary>
    private float dropSpawnChance;

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
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Ripple),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Ripple),
            SyncDefaults);
        waveform = waveforms.Random();
        FloatRange dropSpawnChanceRange = beatManager.IsSynced
            ? SyncSettings.DropSpawnChance
            : standaloneSettings.DropSpawnChance;
        dropSpawnChance = Random.Range(dropSpawnChanceRange.Min, dropSpawnChanceRange.Max);
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
        bool isSynced = beatManager.IsSynced;
        FloatRange velocityRange = isSynced ? SyncSettings.Velocity : standaloneSettings.Velocity;
        float velocityDivisor = isSynced
            ? SyncSettings.VelocityDivisor
            : standaloneSettings.VelocityDivisor;
        float distanceDivisor = isSynced
            ? SyncSettings.DistanceDivisor
            : standaloneSettings.DistanceDivisor;
        float paletteOffset = isSynced
            ? SyncSettings.PaletteOffset
            : standaloneSettings.PaletteOffset;

        // Synced Mode uses Normalized Low only as a strict presence gate; above it, the full Beat
        // Pulse shifts palette hue while drop radius/progression remains independent. Standalone
        // keeps its existing Waveform path. See CONTEXT.md:189-191,267-269 and the beat-pulse and
        // levels wire lanes in docs/osc-client-contract.md:355-400.
        float hueShift = isSynced
            ? beatManager.Levels.Normalized.Low > SyncSettings.LowPresenceThreshold
                ? beatManager.Pulses.Beat * SyncSettings.HueShiftMax
                : 0f
            : waveform.Lerp(0f, standaloneSettings.HueShift);
        if (Random.value < dropSpawnChance)
        {
            Array.Resize(ref drops, drops.Length + 1);
            drops[drops.Length - 1] = new Drop(velocityRange, velocityDivisor);
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
                    sum += (drops[i].radius - (d / distanceDivisor)).Clamp01();
                }
                sum += paletteOffset;
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
        /// Creates a ripple drop at a random screen position using the active mode's velocity Settings.
        /// </summary>
        public Drop(FloatRange velocityRange, float velocityDivisor)
        {
            velocity = Random.Range(velocityRange.Min, velocityRange.Max) / velocityDivisor;
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

/// <summary>
/// The serializable value shape shared by Ripple's Standalone Defaults and Settings; the in-file
/// Defaults preserve the authored no-music look.
/// </summary>
[Serializable]
public sealed class RippleStandaloneSettings
{
    /// <summary>Per-activation range for the per-frame chance of spawning a new drop.</summary>
    public FloatRange DropSpawnChance;

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

    /// <summary>Copies every Ripple Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(RippleStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        DropSpawnChance = CopyRange(source.DropSpawnChance);
        Velocity = CopyRange(source.Velocity);
        VelocityDivisor = source.VelocityDivisor;
        DistanceDivisor = source.DistanceDivisor;
        PaletteOffset = source.PaletteOffset;
        HueShift = source.HueShift;
    }

    /// <summary>Copies one FloatRange so saved Settings cannot mutate authored Defaults through aliasing.</summary>
    private static FloatRange CopyRange(FloatRange source)
    {
        return new FloatRange(source.Min, source.Max, source.LowRail, source.HighRail);
    }
}

/// <summary>
/// The serializable value shape shared by Ripple's Sync Defaults and Sync Settings for its
/// musical response in Synced Mode.
/// </summary>
[Serializable]
public sealed class RippleSyncSettings
{
    /// <summary>Per-activation range for the per-frame chance of spawning a new drop.</summary>
    public FloatRange DropSpawnChance;

    /// <summary>Per-drop pre-division velocity range.</summary>
    public FloatRange Velocity;

    /// <summary>Divisor applied to each randomly rolled drop velocity.</summary>
    public float VelocityDivisor;

    /// <summary>Divisor that maps screen-space distance into ripple phase.</summary>
    public float DistanceDivisor;

    /// <summary>Palette phase offset applied before wrapping the ripple sum.</summary>
    public float PaletteOffset;

    /// <summary>Normalized Low threshold above which the Beat Pulse shifts the palette in Synced Mode.</summary>
    [Range(0f, 1f)] public float LowPresenceThreshold;

    /// <summary>Maximum hue shift reached at the Beat Pulse peak in Synced Mode.</summary>
    [Range(0f, 1f)] public float HueShiftMax;

    /// <summary>Copies every Ripple Sync Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(RippleSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        DropSpawnChance = CopyRange(source.DropSpawnChance);
        Velocity = CopyRange(source.Velocity);
        VelocityDivisor = source.VelocityDivisor;
        DistanceDivisor = source.DistanceDivisor;
        PaletteOffset = source.PaletteOffset;
        LowPresenceThreshold = source.LowPresenceThreshold;
        HueShiftMax = source.HueShiftMax;
    }

    /// <summary>Copies one FloatRange so saved Settings cannot mutate authored Defaults through aliasing.</summary>
    private static FloatRange CopyRange(FloatRange source)
    {
        return new FloatRange(source.Min, source.Max, source.LowRail, source.HighRail);
    }
}
