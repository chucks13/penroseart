using Random = UnityEngine.Random;
using UnityEngine;
using System;

/// <summary>
/// Renders expanding screen-space ripple rings, samples an Effect-conditioned copy of the shared
/// animated palette, and maps the result to Penrose tiles.
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
    /// Authored greatest flat fraction of the Standalone screen before Ripple spawns. One waits
    /// for a completely flat screen; a lower value spawns earlier, while the screen is merely
    /// mostly flat. Any value costs exactly one wavefront per flat episode, because the fresh
    /// wavefront counts as pending motion and clears the flat check on its next frame.
    /// </summary>
    private const float StandaloneMaxFlatScreenFraction = 0.9f;

    /// <summary>Authored palette phase offset for the unchanged Standalone look.</summary>
    private const float StandalonePaletteOffset = 0.5f;

    /// <summary>
    /// Standalone palette conditioning for clean separation between Wavefronts. Starting from the
    /// Angles calibration with the hue-spread reference tightened at the wall, it moves dark-heavy
    /// palettes into one useful luminance band, repairs black and near-black entries with
    /// neighbouring hue, collapses near-duplicates, and redistributes distinct colour movement
    /// across the cyclic table. Tune on the wall.
    /// </summary>
    private static PaletteConditioning StandalonePaletteConditioning => new()
    {
        TargetLuminance = 0.4f,
        MinimumLuminance = 0.12f,
        LuminanceEqualization = 0.85f,
        HueSpreadReference = 0.325f,
        MaximumLuminanceScale = 4f,
        DarkLuminanceThreshold = 0.03f,
        DuplicateThreshold = 0.08f,
        HueRedistribution = 1f,
    };

    /// <summary>Authored fixed hue shift for the unchanged Standalone look.</summary>
    private const float StandaloneHueShift = 0.2f;

    // Sync Defaults

    /// <summary>
    /// Authored minimum mean wavefront spawns per unit-velocity screen crossing in Synced Mode.
    /// At Mid Energy and 125 BPM, the eight-beat crossing lasts 3.84 seconds, so 2.3 targets about
    /// 0.6 spawns per second at the wall's ordinary tempo.
    /// </summary>
    /// <remarks>
    /// See Energy and Data Surface in <c>CONTEXT.md</c>, and the <c>/rave/onair/bpm</c> and
    /// <c>/rave/onair/energy_state</c> lanes in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    private const float SyncSpawnsPerCrossingMin = 2.3f;

    /// <summary>
    /// Authored maximum mean wavefront spawns per unit-velocity screen crossing in Synced Mode.
    /// At Mid Energy and 125 BPM, the eight-beat crossing lasts 3.84 seconds, so 4.6 targets about
    /// 1.2 spawns per second at the wall's ordinary tempo.
    /// </summary>
    /// <remarks>
    /// See Energy and Data Surface in <c>CONTEXT.md</c>, and the <c>/rave/onair/bpm</c> and
    /// <c>/rave/onair/energy_state</c> lanes in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    private const float SyncSpawnsPerCrossingMax = 4.6f;

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
    /// Authored greatest flat fraction of the Synced screen before Ripple spawns. One waits for a
    /// completely flat screen; a lower value spawns earlier, while the screen is merely mostly
    /// flat. Any value costs exactly one wavefront per flat episode, because the fresh wavefront
    /// counts as pending motion and clears the flat check on its next frame.
    /// </summary>
    private const float SyncMaxFlatScreenFraction = 0.8f;

    /// <summary>Authored palette phase offset for the current Synced look.</summary>
    private const float SyncPaletteOffset = 0.5f;

    /// <summary>
    /// Sync palette conditioning, independently authored so live tuning in one mode cannot drift
    /// the other. It starts at the same dark-heavy-palette repair calibration as Standalone, keeping
    /// Wavefront colour separation readable while the Beat Pulse wiggle and hue drift move through
    /// the conditioned cyclic table. Tune on the wall.
    /// </summary>
    private static PaletteConditioning SyncPaletteConditioning => new()
    {
        TargetLuminance = 0.4f,
        MinimumLuminance = 0.12f,
        LuminanceEqualization = 0.85f,
        HueSpreadReference = 0.325f,
        MaximumLuminanceScale = 4f,
        DarkLuminanceThreshold = 0.03f,
        DuplicateThreshold = 0.08f,
        HueRedistribution = 1f,
    };

    /// <summary>Authored Levels form whose Low band supplies the Synced presence gate.</summary>
    private const LevelsForm SyncLowLevelsForm = LevelsForm.Smoothed;

    /// <summary>
    /// Authored selected-form Low presence threshold for the Beat Pulse palette shift in Synced Mode.
    /// </summary>
    /// <remarks>
    /// See Levels and Beat Pulse in <c>CONTEXT.md:189-191,267-269</c> and their wire lanes in
    /// <c>docs/osc-client-contract.md:355-400</c>.
    /// </remarks>
    private const float SyncLowPresenceThreshold = 0.4f;

    /// <summary>Authored amplitude of the Beat Pulse hue wiggle in Synced Mode.</summary>
    /// <remarks>
    /// See Beat Pulse in <c>CONTEXT.md:267-269</c> and <c>docs/osc-client-contract.md:355-382</c>.
    /// </remarks>
    private const float SyncHueWiggleAmplitude = 0.4f;

    /// <summary>
    /// Authored depth of the Beat Pulse hue rotation while a Fill is active in Synced Mode: the
    /// fraction of the half-wheel journey to the complementary color reached at each pulse peak.
    /// </summary>
    /// <remarks>
    /// See Fill and Beat Pulse in <c>CONTEXT.md</c>, and the <c>/rave/onair/fill_state</c> and
    /// <c>/rave/onair/beat_pulse</c> lanes in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    private const float SyncFillComplementDepth = 1f;

    /// <summary>
    /// Authored number of beats from the Drop landing that spawn extra wavefronts in Synced Mode.
    /// </summary>
    /// <remarks>
    /// See Drop and Beat Counting in <c>CONTEXT.md</c>, and the
    /// <c>/rave/onair/drop_state</c> lane in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    private const int SyncDropSpawnBeats = 8;

    /// <summary>Authored number of extra wavefronts spawned on each selected Drop beat.</summary>
    /// <remarks>
    /// See Drop and Beat Counting in <c>CONTEXT.md</c>, and the
    /// <c>/rave/onair/drop_state</c> lane in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    private const int SyncDropSpawnsPerBeat = 1;

    /// <summary>
    /// Authored hue-wheel cycles per second advanced while the selected Levels form's Low is at or
    /// below its presence threshold in Synced Mode.
    /// </summary>
    /// <remarks>
    /// See Levels and Standalone Mode / Synced Mode in <c>CONTEXT.md:189-191,227-235</c>.
    /// </remarks>
    private const float SyncHueDriftRate = 0.05f;

    /// <summary>
    /// Ripple handles Fill with a transient Beat Pulse hue rotation toward complementary colors,
    /// handles the opening Drop beats with extra wavefront spawns, and suits Mid/High Energy
    /// sections.
    /// </summary>
    /// <remarks>
    /// See Repertoire, Fill, Drop, Beat Pulse, and Energy in <c>CONTEXT.md</c>, and the
    /// <c>/rave/onair/fill_state</c>, <c>/rave/onair/drop_state</c>,
    /// <c>/rave/onair/beat_pulse</c>, and <c>/rave/onair/energy_state</c> lanes in
    /// <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill |
        Repertoire.HandlesDrop |
        Repertoire.EnergyMid |
        Repertoire.EnergyHigh;

    /// <summary>
    /// Resolves a fresh copy of Ripple's file-local Standalone Defaults, including Effect-local
    /// palette conditioning.
    /// </summary>
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
        PaletteConditioning = StandalonePaletteConditioning,
        HueShift = StandaloneHueShift,
    };

    /// <summary>
    /// Resolves a fresh copy of Ripple's file-local Sync Defaults, including independently saved
    /// Effect-local palette conditioning.
    /// </summary>
    public static RippleSyncSettings SyncDefaults => new()
    {
        SpawnsPerCrossing = new FloatRange(
            SyncSpawnsPerCrossingMin,
            SyncSpawnsPerCrossingMax),
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
        PaletteConditioning = SyncPaletteConditioning,
        LowLevelsForm = SyncLowLevelsForm,
        LowPresenceThreshold = SyncLowPresenceThreshold,
        HueWiggleAmplitude = SyncHueWiggleAmplitude,
        FillComplementDepth = SyncFillComplementDepth,
        DropSpawnBeats = SyncDropSpawnBeats,
        DropSpawnsPerBeat = SyncDropSpawnsPerBeat,
        HueDriftRate = SyncHueDriftRate,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private RippleStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private RippleSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>
    /// Ripple's Effect-local conditioned endpoint cache. It follows shared palette revisions and live
    /// conditioning controls while preserving the animated cross-fade without steady-frame allocation.
    /// </summary>
    private readonly ConditionedPaletteCache conditionedPalette = new();

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

    /// <summary>Standalone per-frame wavefront spawn chance chosen by the activation Roll.</summary>
    private float standaloneSpawnChance;

    /// <summary>Synced mean wavefront spawns per screen crossing chosen by the activation Roll.</summary>
    private float syncedSpawnsPerCrossing;

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
    /// Previous active Drop beats-remaining value retained for Ripple's consumer-local beat Edge.
    /// </summary>
    /// <remarks>
    /// See Drop, Data Surface, and Edge in <c>CONTEXT.md</c>, and the
    /// <c>/rave/onair/drop_state</c> lane in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    private int? previousDropBeatsRemaining;

    /// <summary>Number of Drop beat Edges observed during the current active Drop.</summary>
    /// <remarks>
    /// See Drop and Edge in <c>CONTEXT.md</c>, and the <c>/rave/onair/drop_state</c> lane in
    /// <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    private int dropSpawnBeatCount;

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
                ? lowPresent ? pulse * SyncSettings.HueWiggleAmplitude : 0f
                : standaloneSettings.HueShift,
            1f);
        previousPulse = pulse;
        previousIsSynced = isSynced;
        previousDropBeatsRemaining = null;
        dropSpawnBeatCount = 0;
        FloatRange activeSpawnRange = isSynced
            ? SyncSettings.SpawnsPerCrossing
            : standaloneSettings.SpawnChance;
        float activeSpawnValue = Random.Range(activeSpawnRange.Min, activeSpawnRange.Max);
        float spawnRangePosition = Mathf.InverseLerp(
            activeSpawnRange.Min,
            activeSpawnRange.Max,
            activeSpawnValue);

        // One activation Roll drives both mode-owned quantities at the same range position. The
        // active mode keeps the direct Random result, while the other value is ready if IsSynced
        // changes mid-activation without consuming a second random number.
        if (isSynced)
        {
            syncedSpawnsPerCrossing = activeSpawnValue;
            standaloneSpawnChance = Mathf.Lerp(
                standaloneSettings.SpawnChance.Min,
                standaloneSettings.SpawnChance.Max,
                spawnRangePosition);
        }
        else
        {
            standaloneSpawnChance = activeSpawnValue;
            syncedSpawnsPerCrossing = Mathf.Lerp(
                SyncSettings.SpawnsPerCrossing.Min,
                SyncSettings.SpawnsPerCrossing.Max,
                spawnRangePosition);
        }
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer through the active mode's live palette
    /// conditioning.
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
        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        conditionedPalette.Refresh(APalette, paletteConditioning);
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
                ? SyncSettings.HueWiggleAmplitude * pulseDelta
                : SyncSettings.HueDriftRate * effectDelta;
            huePhase = Mathf.Repeat(huePhase + hueAdvance, 1f);
        }
        previousPulse = pulse;
        previousIsSynced = isSynced;

        // Fill rotates each rendered hue around the wheel by the raw Beat Pulse — pulse x depth x
        // half the wheel — so every color travels the rim to its complement and back at its own
        // saturation and brightness, never crossing the gray middle a straight crossfade passes
        // through. The rotation never enters huePhase, so it vanishes without disturbing the wiggle
        // or drift when Active ends. See Fill and Beat Pulse in CONTEXT.md and the
        // /rave/onair/fill_state and /rave/onair/beat_pulse lanes in docs/osc-client-contract.md.
        bool fillActive = isSynced && beatManager.Fill.Active;
        float fillHueRotation = fillActive
            ? pulse * SyncSettings.FillComplementDepth * 0.5f
            : 0f;
        float clockAdvance = clockRate * effectDelta;
        float spawnChance = isSynced
            ? syncedSpawnsPerCrossing * clockAdvance / screenCrossingRadius
            : standaloneSpawnChance;

        // Both the Synced spawn chance and wavefront lifetime now advance in shared-clock units,
        // so changing the clock pace changes their real-time speed without changing ring density.
        bool spawnWavefront = Random.value < spawnChance;
        if (spawnWavefront)
        {
            SpawnWavefront(velocityRange);
        }
        clock += clockAdvance;
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
                bool wavefrontPending = false;
                for (int i = 0; i < wavefronts.Length; i++)
                {
                    var d = Vector2.Distance(screen, wavefronts[i].Position);
                    var contribution =
                        (wavefronts[i].RadiusAt(clock) - (d / distanceDivisor)).Clamp01();
                    wavefrontPending |= contribution < 1f;
                    sum += contribution;
                }

                if (!wavefrontPending)
                {
                    flatPixels++;
                }

                sum += paletteOffset;
                sum %= 1f;
                Color color = conditionedPalette.ReadCyclic(sum + huePhase, doblend: true);
                screenBuffer[idx] = fillActive
                    ? color.Delta(fillHueRotation)
                    : color;
            }
        }

        ConvertScreenBuffer(ref screenBuffer, in buffer);

        // A wavefront contributes exactly 1 everywhere its ring has already passed, and the wrap
        // above discards whole numbers, so a pixel that every ring has passed renders the same
        // color as every other such pixel. Any contribution below 1 means a ring is crossing the
        // pixel now or has not reached it yet, and both are motion the pixel is about to show, so
        // neither counts as flat. Counting a ring that has not arrived is also what keeps this to
        // one spawn: the wavefront below contributes 0 everywhere on its first frame, which clears
        // the flat count outright at any MaxFlatScreenFraction instead of leaving the check armed
        // until the new ring grows wide enough. Once enough of the screen is flat the wall reads as
        // one color, so Ripple spawns a wavefront to put motion back onto it. This runs in both
        // modes and stays independent of the paced random spawn roll above.
        if (flatPixels >= maxFlatScreenFraction * width * height)
        {
            SpawnWavefront(velocityRange);
        }

        // Comparing the active Drop's beats-remaining Data Surface read with Ripple's retained
        // prior value creates the consumer-local Edge for each new Drop beat. When Active begins,
        // its first value is the landing beat, so the local count begins there and rests as soon as
        // Active ends. Drop wavefronts are added after the unchanged paced roll and flat-spawn
        // decision, leaving both existing paths in force. See Drop, Data Surface, and Edge in
        // CONTEXT.md, and /rave/onair/drop_state in docs/osc-client-contract.md.
        bool dropActive = isSynced && beatManager.Drop.Active;
        int? dropBeatsRemaining = dropActive
            ? beatManager.Drop.BeatsRemaining.Value
            : null;
        bool newDropBeat = dropBeatsRemaining != previousDropBeatsRemaining;
        if (!dropActive)
        {
            dropSpawnBeatCount = 0;
        }
        else if (newDropBeat)
        {
            if (dropSpawnBeatCount < SyncSettings.DropSpawnBeats)
            {
                for (int i = 0; i < SyncSettings.DropSpawnsPerBeat; i++)
                {
                    SpawnWavefront(velocityRange);
                }
            }

            dropSpawnBeatCount++;
        }
        previousDropBeatsRemaining = dropBeatsRemaining;
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
    /// Live Effect-local palette conditioning for Standalone Mode, independently saved so tuning it
    /// cannot drift the Synced look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>
    /// Hue shift the persistent palette position is seeded with when a Standalone activation
    /// begins. Standalone then holds that position rather than re-reading this value, so a Play
    /// Mode edit here reaches the wall on the next activation rather than on the next frame.
    /// </summary>
    public float HueShift;

    /// <summary>
    /// Copies every Ripple Standalone Setting, including palette conditioning, range endpoints, and
    /// Rails.
    /// </summary>
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
        PaletteConditioning = source.PaletteConditioning;
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
    /// <summary>
    /// Per-activation range for mean wavefront spawns during one unit-velocity screen crossing.
    /// </summary>
    public FloatRange SpawnsPerCrossing;

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

    /// <summary>
    /// Live Effect-local palette conditioning for Synced Mode, independently saved so tuning it
    /// cannot drift the Standalone look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Levels form whose Low band supplies the Beat Pulse presence gate.</summary>
    public LevelsForm LowLevelsForm;

    /// <summary>Selected-form Low threshold above which the Beat Pulse shifts the palette in Synced Mode.</summary>
    [Range(0f, 1f)] public float LowPresenceThreshold;

    /// <summary>
    /// Amplitude of the Beat Pulse hue wiggle in Synced Mode: the palette displacement, in
    /// hue-wheel cycles, reached at each pulse peak.
    /// </summary>
    [Range(0f, 1f)] public float HueWiggleAmplitude;

    /// <summary>
    /// Depth of the raw Beat Pulse hue rotation while a Fill is active in Synced Mode: the
    /// fraction of the half-wheel journey to the complementary color reached at each pulse peak.
    /// </summary>
    /// <remarks>
    /// See Fill and Beat Pulse in <c>CONTEXT.md</c>, and the <c>/rave/onair/fill_state</c> and
    /// <c>/rave/onair/beat_pulse</c> lanes in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    [Range(0f, 1f)] public float FillComplementDepth;

    /// <summary>Number of beats from the Drop landing that spawn extra wavefronts.</summary>
    /// <remarks>
    /// See Drop and Beat Counting in <c>CONTEXT.md</c>, and the
    /// <c>/rave/onair/drop_state</c> lane in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    public int DropSpawnBeats;

    /// <summary>Number of extra wavefronts spawned on each selected Drop beat.</summary>
    /// <remarks>
    /// See Drop and Beat Counting in <c>CONTEXT.md</c>, and the
    /// <c>/rave/onair/drop_state</c> lane in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    public int DropSpawnsPerBeat;

    /// <summary>
    /// Hue-wheel cycles advanced per second while the selected Levels form's Low is at or below the
    /// presence threshold in Synced Mode.
    /// </summary>
    [Min(0f)] public float HueDriftRate;

    /// <summary>
    /// Copies every Ripple Sync Setting, including palette conditioning, range endpoints, and Rails.
    /// </summary>
    public void CopyFrom(RippleSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        SpawnsPerCrossing = CopyRange(source.SpawnsPerCrossing);
        Velocity = CopyRange(source.Velocity);
        LowCrossingBeats = source.LowCrossingBeats;
        MidCrossingBeats = source.MidCrossingBeats;
        HighCrossingBeats = source.HighCrossingBeats;
        CrossingTimeSeconds = CopyRange(source.CrossingTimeSeconds);
        DistanceDivisor = source.DistanceDivisor;
        MaxFlatScreenFraction = source.MaxFlatScreenFraction;
        PaletteOffset = source.PaletteOffset;
        PaletteConditioning = source.PaletteConditioning;
        LowLevelsForm = source.LowLevelsForm;
        LowPresenceThreshold = source.LowPresenceThreshold;
        HueWiggleAmplitude = source.HueWiggleAmplitude;
        FillComplementDepth = source.FillComplementDepth;
        DropSpawnBeats = source.DropSpawnBeats;
        DropSpawnsPerBeat = source.DropSpawnsPerBeat;
        HueDriftRate = source.HueDriftRate;
    }

    /// <summary>Copies one FloatRange so saved Settings cannot mutate authored Defaults through aliasing.</summary>
    private static FloatRange CopyRange(FloatRange source)
    {
        return new FloatRange(source.Min, source.Max, source.LowRail, source.HighRail);
    }
}
