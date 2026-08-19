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

    /// <summary>Authored minimum per-frame spawn chance for the unchanged Standalone look.</summary>
    private const float StandaloneSpawnChanceMin = 0.01f;

    /// <summary>Authored maximum per-frame spawn chance for the unchanged Standalone look.</summary>
    private const float StandaloneSpawnChanceMax = 0.02f;

    /// <summary>Authored minimum wavefront clock multiplier for the Standalone look.</summary>
    private const float StandaloneVelocityMin = 0.75f;

    /// <summary>Authored maximum wavefront clock multiplier for the Standalone look.</summary>
    private const float StandaloneVelocityMax = 1.25f;

    /// <summary>Authored Standalone clock advance in ripple-radius units per second.</summary>
    private const float StandaloneClockRate = 0.4f;

    /// <summary>Authored divisor that maps screen-space distance into ripple phase.</summary>
    private const float StandaloneDistanceDivisor = 20f;

    /// <summary>
    /// Authored greatest flat fraction of the Standalone screen before Ripple spawns. One means
    /// only a completely flat screen spawns, which costs exactly one wavefront per flat episode.
    /// A lower value spawns while the screen is merely mostly flat, which refills an empty screen
    /// faster but can spawn on several consecutive frames before the new ring covers enough.
    /// </summary>
    private const float StandaloneMaxFlatScreenFraction = 1f;

    /// <summary>Authored palette phase offset for the unchanged Standalone look.</summary>
    private const float StandalonePaletteOffset = 0.5f;

    /// <summary>Authored fixed hue shift for the unchanged Standalone look.</summary>
    private const float StandaloneHueShift = 0.2f;

    // Sync Defaults

    /// <summary>Authored minimum per-frame spawn chance for the current Synced look.</summary>
    private const float SyncSpawnChanceMin = 0.01f;

    /// <summary>Authored maximum per-frame spawn chance for the current Synced look.</summary>
    private const float SyncSpawnChanceMax = 0.02f;

    /// <summary>Authored minimum wavefront clock multiplier for the Synced look.</summary>
    private const float SyncVelocityMin = 0.75f;

    /// <summary>Authored maximum wavefront clock multiplier for the Synced look.</summary>
    private const float SyncVelocityMax = 1.25f;

    /// <summary>Authored beats for a unit-velocity wavefront to cross the screen at Low Energy.</summary>
    private const float SyncLowCrossingBeats = 16f;

    /// <summary>Authored beats for a unit-velocity wavefront to cross the screen at Mid Energy.</summary>
    private const float SyncMidCrossingBeats = 8f;

    /// <summary>Authored beats for a unit-velocity wavefront to cross the screen at High Energy.</summary>
    private const float SyncHighCrossingBeats = 4f;

    /// <summary>Authored minimum real-time screen crossing for the tempo-and-Energy pace.</summary>
    private const float SyncCrossingTimeMinSeconds = 2f;

    /// <summary>Authored maximum real-time screen crossing for the tempo-and-Energy pace.</summary>
    private const float SyncCrossingTimeMaxSeconds = 8f;

    /// <summary>Authored divisor that maps screen-space distance into ripple phase in Synced Mode.</summary>
    private const float SyncDistanceDivisor = 20f;

    /// <summary>
    /// Authored greatest flat fraction of the Synced screen before Ripple spawns. One means only a
    /// completely flat screen spawns, which costs exactly one wavefront per flat episode. A lower
    /// value spawns while the screen is merely mostly flat, which refills an empty screen faster
    /// but can spawn on several consecutive frames before the new ring covers enough.
    /// </summary>
    private const float SyncMaxFlatScreenFraction = 1f;

    /// <summary>Authored palette phase offset for the current Synced look.</summary>
    private const float SyncPaletteOffset = 0.5f;

    /// <summary>Authored Levels form whose Low band supplies the Synced presence gate.</summary>
    private const LevelsForm SyncLowLevelsForm = LevelsForm.Smoothed;

    /// <summary>
    /// Authored selected-form Low presence threshold for the Beat Pulse palette shift in Synced Mode.
    /// </summary>
    /// <remarks>
    /// See Levels and Beat Pulse in <c>CONTEXT.md:189-191,267-269</c> and their wire lanes in
    /// <c>docs/osc-client-contract.md:355-400</c>.
    /// </remarks>
    private const float SyncLowPresenceThreshold = 0.375f;

    /// <summary>Authored maximum Beat Pulse-driven hue shift in Synced Mode.</summary>
    /// <remarks>
    /// See Beat Pulse in <c>CONTEXT.md:267-269</c> and <c>docs/osc-client-contract.md:355-382</c>.
    /// </remarks>
    private const float SyncHueShiftMax = 0.2f;

    /// <summary>
    /// Authored hue-wheel cycles per second advanced while the selected Levels form's Low is at or
    /// below its presence threshold in Synced Mode.
    /// </summary>
    /// <remarks>
    /// See Levels and Standalone Mode / Synced Mode in <c>CONTEXT.md:189-191,227-235</c>.
    /// </remarks>
    private const float SyncHueDriftRate = 0.05f;

    /// <summary>
    /// Ripple's Mid/High Energy suitability character; see the Repertoire and Energy entries in
    /// <c>CONTEXT.md</c>.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh copy of Ripple's file-local Standalone Defaults.</summary>
    public static RippleStandaloneSettings StandaloneDefaults => new()
    {
        SpawnChance = new FloatRange(
            StandaloneSpawnChanceMin,
            StandaloneSpawnChanceMax),
        Velocity = new FloatRange(StandaloneVelocityMin, StandaloneVelocityMax),
        ClockRate = StandaloneClockRate,
        DistanceDivisor = StandaloneDistanceDivisor,
        MaxFlatScreenFraction = StandaloneMaxFlatScreenFraction,
        PaletteOffset = StandalonePaletteOffset,
        HueShift = StandaloneHueShift,
    };

    /// <summary>Resolves a fresh copy of Ripple's file-local Sync Defaults.</summary>
    public static RippleSyncSettings SyncDefaults => new()
    {
        SpawnChance = new FloatRange(SyncSpawnChanceMin, SyncSpawnChanceMax),
        Velocity = new FloatRange(SyncVelocityMin, SyncVelocityMax),
        LowCrossingBeats = SyncLowCrossingBeats,
        MidCrossingBeats = SyncMidCrossingBeats,
        HighCrossingBeats = SyncHighCrossingBeats,
        CrossingTimeSeconds = new FloatRange(
            SyncCrossingTimeMinSeconds,
            SyncCrossingTimeMaxSeconds),
        DistanceDivisor = SyncDistanceDivisor,
        MaxFlatScreenFraction = SyncMaxFlatScreenFraction,
        PaletteOffset = SyncPaletteOffset,
        LowLevelsForm = SyncLowLevelsForm,
        LowPresenceThreshold = SyncLowPresenceThreshold,
        HueShiftMax = SyncHueShiftMax,
        HueDriftRate = SyncHueDriftRate,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private RippleStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private RippleSyncSettings SyncSettings { get; set; } = SyncDefaults;

    private Color startColor;
    private Color endColor;
    private Wavefront[] wavefronts;
    private Vector2 screen;

    /// <summary>The greatest possible distance between two pixels in Ripple's fixed screen buffer.</summary>
    private float maxScreenDistance;

    /// <summary>The single motion clock shared by every active wavefront.</summary>
    private float clock;

    /// <summary>
    /// Current clock advance per second. It remains an unconstrained value so later choreography
    /// can freeze it at zero or make it negative without changing the clock model.
    /// </summary>
    private float clockRate;

    /// <summary>Current per-frame chance of spawning a new wavefront, rolled from the active mode's range.</summary>
    private float spawnChance;

    /// <summary>
    /// Persistent wrapped palette hue position, initialized from the rendered offset at activation
    /// and then moved without being replaced when its driver changes.
    /// </summary>
    private float huePhase;

    /// <summary>
    /// Previous frame's Beat Pulse read, advanced every frame so reopening the Low gate applies only
    /// the current frame's pulse movement.
    /// </summary>
    /// <remarks>
    /// See Beat Pulse in <c>CONTEXT.md:268-270</c> and
    /// <c>docs/osc-client-contract.md:356-382</c>.
    /// </remarks>
    private float previousPulse;

    /// <summary>
    /// Whether the previous frame was in Synced Mode, retained so entering it cannot apply pulse
    /// movement accumulated while Standalone held the hue position.
    /// </summary>
    /// <remarks>See Standalone Mode / Synced Mode in <c>CONTEXT.md:227-235</c>.</remarks>
    private bool previousIsSynced;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Wavefronts {wavefronts.Length}";
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        wavefronts = new Wavefront[0];
        maxScreenDistance = new Vector2(width - 1, height - 1).magnitude;
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
        bool isSynced = beatManager.IsSynced;
        float pulse = beatManager.Pulses.Beat;
        bool lowPresent = isSynced &&
            beatManager.Levels.Select(SyncSettings.LowLevelsForm).Low >
            SyncSettings.LowPresenceThreshold;
        huePhase = Mathf.Repeat(
            isSynced
                ? lowPresent ? pulse * SyncSettings.HueShiftMax : 0f
                : standaloneSettings.HueShift,
            1f);
        previousPulse = pulse;
        previousIsSynced = isSynced;
        FloatRange spawnChanceRange = isSynced
            ? SyncSettings.SpawnChance
            : standaloneSettings.SpawnChance;
        spawnChance = Random.Range(spawnChanceRange.Min, spawnChanceRange.Max);
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
        float distanceDivisor = isSynced
            ? SyncSettings.DistanceDivisor
            : standaloneSettings.DistanceDivisor;
        float paletteOffset = isSynced
            ? SyncSettings.PaletteOffset
            : standaloneSettings.PaletteOffset;
        float maxFlatScreenFraction = isSynced
            ? SyncSettings.MaxFlatScreenFraction
            : standaloneSettings.MaxFlatScreenFraction;
        float screenCrossingRadius = maxScreenDistance / distanceDivisor;
        float saturationRadius = 1f + screenCrossingRadius;
        clockRate = isSynced
            ? ResolveSyncedClockRate(screenCrossingRadius)
            : standaloneSettings.ClockRate;

        // Synced Mode uses the selected Levels form's Low only as a strict presence gate. Above it,
        // the full Beat Pulse's frame delta moves the persistent hue position; below it, the authored
        // drift moves that same position. Standalone holds it. Advancing previousPulse in every mode
        // and gate state keeps every crossing a velocity change instead of a position jump. See
        // CONTEXT.md:189-191,227-235,268-270 and the beat-pulse and levels wire lanes in
        // docs/osc-client-contract.md:356-400.
        float pulse = beatManager.Pulses.Beat;
        if (isSynced)
        {
            float pulseDelta = previousIsSynced ? pulse - previousPulse : 0f;
            bool lowPresent = beatManager.Levels.Select(SyncSettings.LowLevelsForm).Low >
                SyncSettings.LowPresenceThreshold;
            float hueAdvance = lowPresent
                ? SyncSettings.HueShiftMax * pulseDelta
                : SyncSettings.HueDriftRate * effectDelta;
            huePhase = Mathf.Repeat(huePhase + hueAdvance, 1f);
        }
        previousPulse = pulse;
        previousIsSynced = isSynced;
        bool spawnWavefront = Random.value < spawnChance;
        if (spawnWavefront)
        {
            SpawnWavefront(velocityRange);
        }
        clock += clockRate * effectDelta;
        RetireWavefronts(saturationRadius);
        buffer.Fade();

        int flatPixels = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                screen.x = x;
                screen.y = y;
                var idx = x + (y * width);
                var sum = 0f;
                bool hasGradient = false;
                for (int i = 0; i < wavefronts.Length; i++)
                {
                    var d = Vector2.Distance(screen, wavefronts[i].Position);
                    var contribution =
                        (wavefronts[i].RadiusAt(clock) - (d / distanceDivisor)).Clamp01();
                    hasGradient |= contribution > 0f && contribution < 1f;
                    sum += contribution;
                }

                if (!hasGradient)
                {
                    flatPixels++;
                }

                sum += paletteOffset;
                sum %= 1f;
                screenBuffer[idx] = APalette.read(sum + huePhase, true);
            }
        }

        ConvertScreenBuffer(ref screenBuffer, in buffer);

        // A wavefront contributes exactly 1 everywhere its ring has already passed, and the wrap
        // above discards whole numbers, so a pixel reached only by passed rings renders the same
        // color as every other such pixel. Once enough of the screen is in that state the wall
        // reads as one flat color, so Ripple spawns a wavefront to put a gradient back onto it.
        // This runs in both modes; only where Synced Mode places its spawns may change later.
        if (flatPixels >= maxFlatScreenFraction * width * height)
        {
            SpawnWavefront(velocityRange);
        }
    }

    /// <summary>
    /// Converts the Energy-selected musical crossing pace into Ripple clock units per second.
    /// </summary>
    /// <param name="screenCrossingRadius">Radius a unit-velocity wavefront travels across the screen.</param>
    /// <returns>The positive base clock rate before later choreography steers it.</returns>
    /// <remarks>
    /// Energy and the Data Surface are defined in <c>CONTEXT.md</c>. Tempo and Energy arrive through
    /// <c>/rave/onair/bpm</c> and <c>/rave/onair/energy_state</c> in
    /// <c>docs/osc-client-contract.md</c>. The real-time range applies only here, leaving
    /// <see cref="clockRate"/> free to reach zero or become negative afterward.
    /// </remarks>
    private float ResolveSyncedClockRate(float screenCrossingRadius)
    {
        float crossingBeats = beatManager.Energy.Level switch
        {
            Energy.Low => SyncSettings.LowCrossingBeats,
            Energy.Mid => SyncSettings.MidCrossingBeats,
            Energy.High => SyncSettings.HighCrossingBeats,
            _ => SyncSettings.MidCrossingBeats,
        };
        float crossingSeconds = Mathf.Clamp(
            crossingBeats * 60f / beatManager.Timing.Bpm.Value,
            SyncSettings.CrossingTimeSeconds.Min,
            SyncSettings.CrossingTimeSeconds.Max);
        return screenCrossingRadius / crossingSeconds;
    }

    /// <summary>Adds one wavefront born at the current shared clock value.</summary>
    /// <param name="velocityRange">Active mode's authored wavefront clock-multiplier range.</param>
    private void SpawnWavefront(FloatRange velocityRange)
    {
        Array.Resize(ref wavefronts, wavefronts.Length + 1);
        wavefronts[^1] = new Wavefront(velocityRange, clock);
    }

    /// <summary>
    /// Retires wavefronts once saturation makes their modulo-wrapped contribution invisible, or
    /// once reverse clock motion collapses their radius back to zero.
    /// </summary>
    /// <param name="saturationRadius">Radius whose contribution is exactly one at every screen pixel.</param>
    private void RetireWavefronts(float saturationRadius)
    {
        int retainedCount = 0;
        for (int i = 0; i < wavefronts.Length; i++)
        {
            Wavefront wavefront = wavefronts[i];
            float radius = wavefront.RadiusAt(clock);
            bool saturated = radius >= saturationRadius;
            bool collapsedWhileReversing = clockRate < 0f && radius <= 0f;
            if (saturated || collapsedWhileReversing)
            {
                continue;
            }

            wavefronts[retainedCount++] = wavefront;
        }

        if (retainedCount != wavefronts.Length)
        {
            Array.Resize(ref wavefronts, retainedCount);
        }
    }

    /// <summary>
    /// Expanding screen-space ripple source.
    /// </summary>
    public class Wavefront
    {
        /// <summary>The fixed screen-space origin of this wavefront.</summary>
        private readonly Vector2 position;

        /// <summary>The wavefront's fixed multiplier over shared-clock movement.</summary>
        private readonly float velocity;

        /// <summary>The shared clock value captured when this wavefront spawned.</summary>
        private readonly float birthClock;

        /// <summary>
        /// Creates a wavefront at a random screen position using the active mode's velocity Settings.
        /// </summary>
        /// <param name="velocityRange">Active mode's authored clock-multiplier range.</param>
        /// <param name="birthClock">Shared clock value at this wavefront's birth.</param>
        public Wavefront(FloatRange velocityRange, float birthClock)
        {
            velocity = Random.Range(velocityRange.Min, velocityRange.Max);
            position = new Vector2(Random.Range(0, width), Random.Range(0, height));
            this.birthClock = birthClock;
        }

        /// <summary>This wavefront's fixed screen-space origin.</summary>
        public Vector2 Position => position;

        /// <summary>Calculates radius from shared-clock distance since this wavefront's birth.</summary>
        /// <param name="currentClock">Ripple's current shared clock value.</param>
        /// <returns>The signed radius at <paramref name="currentClock"/>.</returns>
        public float RadiusAt(float currentClock) => velocity * (currentClock - birthClock);
    }
}

/// <summary>
/// The serializable value shape shared by Ripple's Standalone Defaults and Settings; the in-file
/// Defaults preserve the authored no-music look.
/// </summary>
[Serializable]
public sealed class RippleStandaloneSettings
{
    /// <summary>Per-activation range for the per-frame chance of spawning a new wavefront.</summary>
    public FloatRange SpawnChance;

    /// <summary>Per-wavefront multiplier range over shared-clock movement.</summary>
    public FloatRange Velocity;

    /// <summary>Standalone shared-clock advance in ripple-radius units per second.</summary>
    public float ClockRate;

    /// <summary>Divisor that maps screen-space distance into ripple phase.</summary>
    public float DistanceDivisor;

    /// <summary>
    /// Greatest fraction of the screen that may render flat before Ripple spawns a wavefront.
    /// </summary>
    [Range(0f, 1f)] public float MaxFlatScreenFraction;

    /// <summary>Palette phase offset applied before wrapping the ripple sum.</summary>
    public float PaletteOffset;

    /// <summary>
    /// Hue shift the persistent palette position is seeded with when a Standalone activation
    /// begins. Standalone then holds that position rather than re-reading this value, so a Play
    /// Mode edit here reaches the wall on the next activation rather than on the next frame.
    /// </summary>
    public float HueShift;

    /// <summary>Copies every Ripple Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(RippleStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        SpawnChance = CopyRange(source.SpawnChance);
        Velocity = CopyRange(source.Velocity);
        ClockRate = source.ClockRate;
        DistanceDivisor = source.DistanceDivisor;
        MaxFlatScreenFraction = source.MaxFlatScreenFraction;
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
    /// <summary>Per-activation range for the per-frame chance of spawning a new wavefront.</summary>
    public FloatRange SpawnChance;

    /// <summary>Per-wavefront multiplier range over shared-clock movement.</summary>
    public FloatRange Velocity;

    /// <summary>Beats for a unit-velocity wavefront to cross the screen at Low Energy.</summary>
    public float LowCrossingBeats;

    /// <summary>Beats for a unit-velocity wavefront to cross the screen at Mid Energy.</summary>
    public float MidCrossingBeats;

    /// <summary>Beats for a unit-velocity wavefront to cross the screen at High Energy.</summary>
    public float HighCrossingBeats;

    /// <summary>
    /// Minimum and maximum real seconds for the tempo-and-Energy crossing calculation. This range
    /// does not constrain the final clock rate.
    /// </summary>
    public FloatRange CrossingTimeSeconds;

    /// <summary>Divisor that maps screen-space distance into ripple phase.</summary>
    public float DistanceDivisor;

    /// <summary>
    /// Greatest fraction of the screen that may render flat before Ripple spawns a wavefront.
    /// </summary>
    [Range(0f, 1f)] public float MaxFlatScreenFraction;

    /// <summary>Palette phase offset applied before wrapping the ripple sum.</summary>
    public float PaletteOffset;

    /// <summary>Levels form whose Low band supplies the Beat Pulse presence gate.</summary>
    public LevelsForm LowLevelsForm;

    /// <summary>Selected-form Low threshold above which the Beat Pulse shifts the palette in Synced Mode.</summary>
    [Range(0f, 1f)] public float LowPresenceThreshold;

    /// <summary>Maximum hue shift reached at the Beat Pulse peak in Synced Mode.</summary>
    [Range(0f, 1f)] public float HueShiftMax;

    /// <summary>
    /// Hue-wheel cycles advanced per second while the selected Levels form's Low is at or below the
    /// presence threshold in Synced Mode.
    /// </summary>
    [Min(0f)] public float HueDriftRate;

    /// <summary>Copies every Ripple Sync Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(RippleSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        SpawnChance = CopyRange(source.SpawnChance);
        Velocity = CopyRange(source.Velocity);
        LowCrossingBeats = source.LowCrossingBeats;
        MidCrossingBeats = source.MidCrossingBeats;
        HighCrossingBeats = source.HighCrossingBeats;
        CrossingTimeSeconds = CopyRange(source.CrossingTimeSeconds);
        DistanceDivisor = source.DistanceDivisor;
        MaxFlatScreenFraction = source.MaxFlatScreenFraction;
        PaletteOffset = source.PaletteOffset;
        LowLevelsForm = source.LowLevelsForm;
        LowPresenceThreshold = source.LowPresenceThreshold;
        HueShiftMax = source.HueShiftMax;
        HueDriftRate = source.HueDriftRate;
    }

    /// <summary>Copies one FloatRange so saved Settings cannot mutate authored Defaults through aliasing.</summary>
    private static FloatRange CopyRange(FloatRange source)
    {
        return new FloatRange(source.Min, source.Max, source.LowRail, source.HighRail);
    }
}
