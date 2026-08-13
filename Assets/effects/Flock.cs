// Advanced reference effect: music interpretation, event choreography, boid simulation, and tile rendering.

using System;
using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Simulates a flock of moving boids in wall space and projects each boid onto the nearest Penrose tile.
/// </summary>
/// <remarks>
/// <para>
/// This is the advanced reference for a direct <see cref="EffectBase"/> implementation. The effect keeps
/// artistic policy in one module: it interprets musical inputs, composes those inputs into steering forces,
/// advances the simulation, and writes the resulting colors into <see cref="EffectBase.buffer"/>.
/// </para>
/// <para>The musical signals have deliberately separate jobs:</para>
/// <list type="bullet">
/// <item><description>Smoothed Low/Mid/High levels set the flock's broad quiet-to-active travel speed.</description></item>
/// <item><description>Energy sets the phrase pace, tightening Low and loosening High around neutral Mid.</description></item>
/// <item><description>Normalized Mid strengthens schooling and bends the shared course.</description></item>
/// <item><description>Normalized spectral centroid increases separation and independent wander.</description></item>
/// <item><description>A four-bar <see cref="Routine"/> changes trail lifetime and palette treatment.</description></item>
/// <item><description>Fill and Drop structure temporarily overrides ordinary steering with visible choreography.</description></item>
/// </list>
/// <para>
/// Standalone mode has explicit steady values because no musical Grid or level data is available there.
/// Trails intentionally preserve the previous frame, and the live animated palette is sampled every frame
/// so palette transitions remain visible after the effect starts.
/// </para>
/// <para>
/// The nested <see cref="Boid"/> simulation performs an O(n²) neighbor scan. That simple implementation is
/// intentional for the fixed flock of 80 boids: it keeps the wrap-aware flocking rules local and readable.
/// </para>
/// </remarks>
[EffectSyncSettings(typeof(FlockSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(FlockStandaloneSettingsAsset))]
public class Flock : EffectBase
{
    // Standalone Defaults

    // Baseline flocking

    /// <summary>Baseline weight of steering toward neighbors' travel direction.</summary>
    private const float StandaloneBaseAlignmentWeight = 0.75f;

    /// <summary>Baseline weight of steering toward the local flock center.</summary>
    private const float StandaloneBaseCohesionWeight = 1f;

    /// <summary>Baseline weight of steering away from nearby boids.</summary>
    private const float StandaloneBaseSeparationWeight = 1.25f;

    // Broad movement

    /// <summary>Ordinary speed limit at complete quiet, relative to each boid's authored maximum.</summary>
    private const float StandaloneQuietSpeedMultiplier = 0.1f;

    /// <summary>Ordinary speed limit at full activity, relative to each boid's authored maximum.</summary>
    private const float StandaloneActiveSpeedMultiplier = 1.5f;

    /// <summary>Standalone movement posture that restores approximately the authored one-times speed.</summary>
    private const float StandaloneMovementActivity = 0.65f;

    // Routine-driven color and trails

    /// <summary>Standalone Routine posture that preserves full palette color and visible trails without a Grid.</summary>
    private const float StandaloneRoutineEnvelope = 1f;

    /// <summary>Standalone trail half-life at the bottom of the Routine envelope.</summary>
    private const float StandaloneTrailHalfLifeMin = 0.03f;

    /// <summary>Standalone trail half-life at the top of the Routine envelope.</summary>
    private const float StandaloneTrailHalfLifeMax = 0.25f;

    /// <summary>Maximum coordinated hue shift applied by the Routine.</summary>
    private const float StandaloneRoutineHueShift = 0.2f;

    /// <summary>
    /// Share of authored saturation retained without a Grid. Full, because
    /// <see cref="StandaloneRoutineEnvelope"/> holds the Routine at the top of its range, where no
    /// saturation is taken away. Lower the envelope and this floor becomes the value it lands on.
    /// </summary>
    private const float StandaloneRoutineSaturationFloor = 1f;

    /// <summary>
    /// Share of authored value retained without a Grid. Full, for the same reason as
    /// <see cref="StandaloneRoutineSaturationFloor"/>.
    /// </summary>
    private const float StandaloneRoutineValueFloor = 1f;

    // Continuous wander

    /// <summary>Minimum share of a boid's steering force devoted to continuous wander.</summary>
    private const float StandaloneWanderStrengthMin = 0.2f;

    /// <summary>Maximum share of a boid's steering force devoted to continuous wander.</summary>
    private const float StandaloneWanderStrengthMax = 0.45f;

    /// <summary>Slowest wander phase rate selected for a Grid, in radians per second.</summary>
    private const float StandaloneWanderTurnRateMin = 0.5f;

    /// <summary>Fastest wander phase rate selected for a Grid, in radians per second.</summary>
    private const float StandaloneWanderTurnRateMax = 1.1f;

    /// <summary>Maximum heading offset used by a boid's continuous wander.</summary>
    private const float StandaloneWanderHeadingRadians = 0.7853982f;

    /// <summary>Minimum per-boid multiplier that prevents wander phases from moving in lockstep.</summary>
    private const float StandaloneWanderFrequencyMin = 0.8f;

    /// <summary>Maximum per-boid multiplier that prevents wander phases from moving in lockstep.</summary>
    private const float StandaloneWanderFrequencyMax = 1.2f;

    /// <summary>Share of ordinary wander strength retained during complete quiet.</summary>
    private const float StandaloneQuietWanderMultiplier = 0.15f;

    /// <summary>Share of ordinary wander turn rate retained during complete quiet.</summary>
    private const float StandaloneQuietWanderTurnMultiplier = 0.5f;

    // Sync Defaults

    // Broad level-driven movement

    /// <summary>Share of broad movement activity contributed by the smoothed Low band.</summary>
    private const float SyncMovementLowWeight = 0.7f;

    /// <summary>Share of broad movement activity contributed by the smoothed Mid band.</summary>
    private const float SyncMovementMidWeight = 0.25f;

    /// <summary>Share of broad movement activity contributed by the smoothed High band.</summary>
    private const float SyncMovementHighWeight = 0.05f;

    /// <summary>Weighted level below which the flock adopts its quiet posture.</summary>
    private const float SyncQuietActivityThreshold = 0.1f;

    /// <summary>Weighted level at which the flock reaches its active posture.</summary>
    private const float SyncActiveActivityThreshold = 0.5f;

    /// <summary>Ordinary speed limit at complete quiet, relative to each boid's authored maximum.</summary>
    private const float SyncQuietSpeedMultiplier = 0.1f;

    /// <summary>Ordinary speed limit at full activity, relative to each boid's authored maximum.</summary>
    private const float SyncActiveSpeedMultiplier = 1.5f;

    /// <summary>Phrase-pace multiplier at Low Energy, slowing and tightening the flock body.</summary>
    private const float SyncLowEnergyPaceMultiplier = 0.75f;

    /// <summary>Phrase-pace multiplier at High Energy, speeding and loosening the flock body.</summary>
    private const float SyncHighEnergyPaceMultiplier = 1.25f;

    // Normalized schooling and spectral detail

    /// <summary>Minimum normalized Mid level below which no schooling maneuver is added.</summary>
    private const float SyncMidManeuverLevelMin = 0.2f;

    /// <summary>Maximum normalized Mid level that produces the full schooling maneuver.</summary>
    private const float SyncMidManeuverLevelMax = 0.65f;

    /// <summary>Alignment lift on a strong normalized Mid-band maneuver.</summary>
    private const float SyncMidAlignmentLift = 0.75f;

    /// <summary>Cohesion lift on a strong normalized Mid-band maneuver.</summary>
    private const float SyncMidCohesionLift = 0.5f;

    /// <summary>Shared course-bending force applied by a strong normalized Mid-band maneuver.</summary>
    private const float SyncCollectiveTurnStrength = 0.75f;

    /// <summary>Minimum normalized spectral centroid below which no extra agitation is added.</summary>
    private const float SyncSpectralCentroidMin = 0.25f;

    /// <summary>Maximum normalized spectral centroid that produces full extra agitation.</summary>
    private const float SyncSpectralCentroidMax = 0.65f;

    /// <summary>Separation lift when the normalized spectrum leans toward high-frequency detail.</summary>
    private const float SyncSpectralSeparationLift = 1f;

    // Routine-driven color and trails

    /// <summary>Trail half-life in seconds at the bottom of the Routine envelope.</summary>
    private const float SyncTrailHalfLifeMin = 0.03f;

    /// <summary>Trail half-life in seconds at the top of the Routine envelope.</summary>
    private const float SyncTrailHalfLifeMax = 0.25f;

    /// <summary>Maximum coordinated hue shift applied by the Routine.</summary>
    private const float SyncRoutineHueShift = 0.2f;

    /// <summary>Share of authored saturation retained at the bottom of the Routine.</summary>
    private const float SyncRoutineSaturationFloor = 0.7f;

    /// <summary>Share of authored value retained at the bottom of the Routine.</summary>
    private const float SyncRoutineValueFloor = 0.7f;

    // Fill choreography

    /// <summary>The common Fill length used as the unboosted response baseline.</summary>
    private const float SyncTypicalFillBeats = 4f;

    /// <summary>The shortest supported Fill duration used to cap short-window compensation.</summary>
    private const float SyncMinimumFillBeats = 1f;

    /// <summary>Base tangential velocity added when a typical Fill begins; shorter Fills receive a duration boost.</summary>
    private const float SyncFillOnsetImpulse = 6f;

    /// <summary>Strength of continuous Fill steering around wall center.</summary>
    private const float SyncFillOrbitSteering = 4f;

    /// <summary>Share of Fill orbit retained at the end of the Drop gather, creating a tightening spiral.</summary>
    private const float SyncFillOrbitAtFullGather = 0.5f;

    /// <summary>Alignment lift at full Fill drive.</summary>
    private const float SyncFillAlignmentLift = 0.35f;

    /// <summary>Separation lift that lets a Fill-only orbit widen.</summary>
    private const float SyncFillSeparationLift = 0.5f;

    // Drop spiral, gathering, and aftermath

    /// <summary>Soft center-seeking steering that bends a Fill orbit as soon as its Drop is announced.</summary>
    private const float SyncDropSpiralSteering = 0.75f;

    /// <summary>Number of beats before a Drop over which the flock finishes gathering at wall center.</summary>
    private const int SyncDropGatherBeats = 8;

    /// <summary>Strength of center-seeking steering at the end of the pre-Drop gather.</summary>
    private const float SyncDropGatherSteering = 3f;

    /// <summary>Share of ordinary separation removed at the end of the pre-Drop gather.</summary>
    private const float SyncDropGatherSeparationSuppression = 0.9f;

    /// <summary>Number of Drop beats over which the torn-apart aftermath fades from the landing.</summary>
    private const int SyncDropAftermathBeats = 16;

    /// <summary>Trail half-life in seconds at the Drop landing before the bloom fades.</summary>
    private const float SyncDropTrailHalfLife = 1.5f;

    /// <summary>Initial Drop burst speed relative to each boid's ordinary maximum speed.</summary>
    private const float SyncDropBurstSpeedMultiplier = 1.6f;

    /// <summary>Share of pre-Drop velocity retained in the radial burst so a Fill can leave angular momentum.</summary>
    private const float SyncDropVelocityCarry = 0.35f;

    /// <summary>Strength of outward steering during the Drop aftermath.</summary>
    private const float SyncDropOutwardSteering = 1.5f;

    /// <summary>Maximum speed lift at the start of the Drop aftermath.</summary>
    private const float SyncDropSpeedLift = 0.75f;

    /// <summary>Share of cohesion removed at the start of the Drop aftermath.</summary>
    private const float SyncDropCohesionSuppression = 0.9f;

    /// <summary>Separation lift at the start of the Drop aftermath.</summary>
    private const float SyncDropSeparationLift = 1.25f;

    // Continuous wander response

    /// <summary>Share of ordinary wander strength retained during complete quiet.</summary>
    private const float SyncQuietWanderMultiplier = 0.15f;

    /// <summary>Share of ordinary wander turn rate retained during complete quiet.</summary>
    private const float SyncQuietWanderTurnMultiplier = 0.5f;

    /// <summary>Additional wander strength at full spectral agitation.</summary>
    private const float SyncSpectralWanderStrengthLift = 1.5f;

    /// <summary>Additional wander turn rate at full spectral agitation.</summary>
    private const float SyncSpectralWanderTurnLift = 1f;

    // Both-mode flocking and wander

    /// <summary>Synced weight of steering toward neighbors' travel direction.</summary>
    private const float SyncBaseAlignmentWeight = 0.75f;

    /// <summary>Synced weight of steering toward the local flock center.</summary>
    private const float SyncBaseCohesionWeight = 1f;

    /// <summary>Synced weight of steering away from nearby boids.</summary>
    private const float SyncBaseSeparationWeight = 1.25f;

    /// <summary>Minimum Synced share of a boid's steering force devoted to continuous wander.</summary>
    private const float SyncWanderStrengthMin = 0.2f;

    /// <summary>Maximum Synced share of a boid's steering force devoted to continuous wander.</summary>
    private const float SyncWanderStrengthMax = 0.45f;

    /// <summary>Slowest Synced wander phase rate selected for a Grid, in radians per second.</summary>
    private const float SyncWanderTurnRateMin = 0.5f;

    /// <summary>Fastest Synced wander phase rate selected for a Grid, in radians per second.</summary>
    private const float SyncWanderTurnRateMax = 1.1f;

    /// <summary>Maximum Synced heading offset used by a boid's continuous wander.</summary>
    private const float SyncWanderHeadingRadians = 0.7853982f;

    /// <summary>Minimum Synced per-boid multiplier that prevents wander phases from moving in lockstep.</summary>
    private const float SyncWanderFrequencyMin = 0.8f;

    /// <summary>Maximum Synced per-boid multiplier that prevents wander phases from moving in lockstep.</summary>
    private const float SyncWanderFrequencyMax = 1.2f;

    // Runtime mechanism constants

    /// <summary>Number of boids simulated by the effect.</summary>
    private const int BoidCount = 80;

    // Effect identity and author-facing switches

    /// <summary>
    /// Advertises that the effect handles Fill and Drop structure and suits any energy level.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>
    /// Master switch for Routine-controlled trails. Set to <see langword="false"/> to render only current boid positions.
    /// </summary>
    private static readonly bool TrailsEnabled = true;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate Flock's authored
    /// Standalone Defaults.
    /// </summary>
    public static FlockStandaloneSettings StandaloneDefaults => new()
    {
        BaseAlignmentWeight = StandaloneBaseAlignmentWeight,
        BaseCohesionWeight = StandaloneBaseCohesionWeight,
        BaseSeparationWeight = StandaloneBaseSeparationWeight,
        MovementActivity = StandaloneMovementActivity,
        SpeedMultiplier = new FloatRange(
            StandaloneQuietSpeedMultiplier,
            StandaloneActiveSpeedMultiplier),
        RoutineEnvelope = StandaloneRoutineEnvelope,
        TrailHalfLife = new FloatRange(
            StandaloneTrailHalfLifeMin,
            StandaloneTrailHalfLifeMax),
        RoutineHueShift = StandaloneRoutineHueShift,
        RoutineSaturationFloor = StandaloneRoutineSaturationFloor,
        RoutineValueFloor = StandaloneRoutineValueFloor,
        WanderStrength = new FloatRange(StandaloneWanderStrengthMin, StandaloneWanderStrengthMax),
        WanderTurnRate = new FloatRange(StandaloneWanderTurnRateMin, StandaloneWanderTurnRateMax),
        WanderHeadingRadians = StandaloneWanderHeadingRadians,
        WanderFrequency = new FloatRange(StandaloneWanderFrequencyMin, StandaloneWanderFrequencyMax),
        QuietWanderMultiplier = StandaloneQuietWanderMultiplier,
        QuietWanderTurnMultiplier = StandaloneQuietWanderTurnMultiplier,
    };

    /// <summary>Resolves a fresh copy of Flock's file-local Sync Defaults.</summary>
    public static FlockSyncSettings SyncDefaults => new()
    {
        BaseAlignmentWeight = SyncBaseAlignmentWeight,
        BaseCohesionWeight = SyncBaseCohesionWeight,
        BaseSeparationWeight = SyncBaseSeparationWeight,
        MovementLowWeight = SyncMovementLowWeight,
        MovementMidWeight = SyncMovementMidWeight,
        MovementHighWeight = SyncMovementHighWeight,
        MovementLevel = new FloatRange(
            SyncQuietActivityThreshold,
            SyncActiveActivityThreshold),
        SpeedMultiplier = new FloatRange(
            SyncQuietSpeedMultiplier,
            SyncActiveSpeedMultiplier),
        EnergyPaceMultiplier = new FloatRange(
            SyncLowEnergyPaceMultiplier,
            SyncHighEnergyPaceMultiplier),
        MidManeuverLevel = new FloatRange(
            SyncMidManeuverLevelMin,
            SyncMidManeuverLevelMax),
        MidAlignmentLift = SyncMidAlignmentLift,
        MidCohesionLift = SyncMidCohesionLift,
        CollectiveTurnStrength = SyncCollectiveTurnStrength,
        SpectralCentroid = new FloatRange(
            SyncSpectralCentroidMin,
            SyncSpectralCentroidMax),
        SpectralSeparationLift = SyncSpectralSeparationLift,
        TrailHalfLife = new FloatRange(SyncTrailHalfLifeMin, SyncTrailHalfLifeMax),
        RoutineHueShift = SyncRoutineHueShift,
        RoutineSaturationFloor = SyncRoutineSaturationFloor,
        RoutineValueFloor = SyncRoutineValueFloor,
        TypicalFillBeats = SyncTypicalFillBeats,
        MinimumFillBeats = SyncMinimumFillBeats,
        FillOnsetImpulse = SyncFillOnsetImpulse,
        FillOrbitSteering = SyncFillOrbitSteering,
        FillOrbitAtFullGather = SyncFillOrbitAtFullGather,
        FillAlignmentLift = SyncFillAlignmentLift,
        FillSeparationLift = SyncFillSeparationLift,
        DropSpiralSteering = SyncDropSpiralSteering,
        DropGatherBeats = SyncDropGatherBeats,
        DropGatherSteering = SyncDropGatherSteering,
        DropGatherSeparationSuppression = SyncDropGatherSeparationSuppression,
        DropAftermathBeats = SyncDropAftermathBeats,
        DropTrailHalfLife = SyncDropTrailHalfLife,
        DropBurstSpeedMultiplier = SyncDropBurstSpeedMultiplier,
        DropVelocityCarry = SyncDropVelocityCarry,
        DropOutwardSteering = SyncDropOutwardSteering,
        DropSpeedLift = SyncDropSpeedLift,
        DropCohesionSuppression = SyncDropCohesionSuppression,
        DropSeparationLift = SyncDropSeparationLift,
        QuietWanderMultiplier = SyncQuietWanderMultiplier,
        QuietWanderTurnMultiplier = SyncQuietWanderTurnMultiplier,
        SpectralWanderStrengthLift = SyncSpectralWanderStrengthLift,
        SpectralWanderTurnLift = SyncSpectralWanderTurnLift,
        WanderStrength = new FloatRange(SyncWanderStrengthMin, SyncWanderStrengthMax),
        WanderTurnRate = new FloatRange(SyncWanderTurnRateMin, SyncWanderTurnRateMax),
        WanderHeadingRadians = SyncWanderHeadingRadians,
        WanderFrequency = new FloatRange(SyncWanderFrequencyMin, SyncWanderFrequencyMax),
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private FlockStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private FlockSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Whether live musical placement currently selects Sync Settings.</summary>
    private bool IsSynced => beatManager.IsSynced;

    // Activation and per-frame state

    /// <summary>The boids projected onto the Penrose wall.</summary>
    private Boid[] flock;

    /// <summary>Four-bar trail and color choreography selected from the current track Energy.</summary>
    private Routine routine;

    /// <summary>Current Grid's gentle continuous wander force.</summary>
    private float wanderStrength = (StandaloneWanderStrengthMin + StandaloneWanderStrengthMax) * 0.5f;

    /// <summary>Current Grid's wander phase rate.</summary>
    private float wanderTurnRate = (StandaloneWanderTurnRateMin + StandaloneWanderTurnRateMax) * 0.5f;

    /// <summary>Low-dominant quiet-to-active posture derived from smoothed levels.</summary>
    private float movementActivity;

    /// <summary>Current Energy's phrase-pace multiplier, neutral in Standalone Mode.</summary>
    private float energyPaceMultiplier = 1f;

    /// <summary>Activity-gated schooling maneuver derived from normalized Mid.</summary>
    private float midManeuver;

    /// <summary>Activity-gated separation and wander drive derived from normalized spectral centroid.</summary>
    private float spectralAgitation;

    /// <summary>Current Routine envelope used only for trail and color treatment.</summary>
    private float routineEnvelope;

    /// <summary>Center of the Penrose bounds used by Fill orbit and Drop gather/burst steering.</summary>
    private Vector2 wallCenter;

    /// <summary>Clockwise or counter-clockwise Fill orbit direction selected for the current Grid.</summary>
    private float fillOrbitDirection;

    /// <summary>Alternating left or right collective course bend used for the current Grid.</summary>
    private float courseTurnDirection;

    /// <summary>Duration-aware Fill orbit drive, allowed above one for very short Fills.</summary>
    private float fillOrbitDrive;

    /// <summary>Early inward bend while a live Fill announces an upcoming Drop.</summary>
    private float dropSpiral;

    /// <summary>Smooth zero-to-one progress through the configured final pre-Drop gather.</summary>
    private float dropGather;

    /// <summary>Landing-relative one-to-zero aftermath envelope across at least one nominal Grid.</summary>
    private float dropAftermath;

    /// <summary>Previous Fill state retained locally to identify its onset.</summary>
    private bool previousFillActive;

    /// <summary>Previous Drop state retained locally to identify its onset.</summary>
    private bool previousDropActive;

    // Effect lifecycle

    /// <summary>
    /// Initializes one activation, including its Grid character, boids, event edges, and persistent frame buffer.
    /// </summary>
    /// <remarks>
    /// Random calls intentionally remain in this order. Changing their order changes the visual seed produced by
    /// the same Unity random state even when every range remains identical.
    /// </remarks>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Flock),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Flock),
            SyncDefaults);

        // Resolve consumes no UnityEngine.Random, so the original roll sequence begins unchanged here.
        RerollRoutine();
        RerollWander();
        fillOrbitDirection = RollDirection();
        courseTurnDirection = RollDirection();

        Vector2 min = penrose.Bounds.min;
        Vector2 max = penrose.Bounds.max;
        wallCenter = penrose.Bounds.center;

        ResetMusicalState();
        CreateBoids(min, max);
        buffer.Clear();
    }

    /// <summary>
    /// Selects a fresh Routine and wander character on each Grid while preventing one long shared heading.
    /// </summary>
    /// <remarks>
    /// Boid positions and velocities are deliberately preserved. The new Grid changes character rather than
    /// restarting the simulation.
    /// </remarks>
    protected override void OnNewGrid()
    {
        RerollRoutine();
        RerollWander();
        fillOrbitDirection = RollDirection();
        courseTurnDirection = courseTurnDirection < 0f ? 1f : -1f;
    }

    /// <summary>
    /// Renders one frame by reading music, updating event choreography, preparing trails, and advancing every boid.
    /// </summary>
    public override void Draw()
    {
        ReadMusicalInputs();
        UpdateMusicalMotion();
        PrepareFrameBuffer();
        UpdateAndRenderBoids();
    }

    /// <summary>
    /// Returns the live musical interpretation values used to tune the effect on the wall.
    /// </summary>
    public override string DebugText() =>
        $"ACT {movementActivity:0.00} PACE {energyPaceMultiplier:0.00} MID {midManeuver:0.00} DETAIL {spectralAgitation:0.00}\n" +
        $"ROUTINE {routineEnvelope:0.00} WANDER {wanderStrength:0.00}/{wanderTurnRate:0.00}\n" +
        $"FILL {fillOrbitDrive:0.00}\nSPIRAL {dropSpiral:0.00} GATHER {dropGather:0.00}\nDROP {dropAftermath:0.00}";

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this method.
    /// </summary>
    public override void OnEnd()
    {
    }

    // Activation and Grid character

    /// <summary>
    /// Composes and shuffles one Grid's Routine from the current track Energy.
    /// </summary>
    /// <remarks>
    /// Low uses three Low slots and one Mid; Mid uses three Mid and one Low; High uses one High,
    /// two Mid, and one Low. Missing Energy uses the Mid recipe so Standalone Mode starts from a
    /// balanced artistic state even though its live envelope later uses an explicit steady posture.
    /// </remarks>
    private void RerollRoutine()
    {
        Energy energy = beatManager.Energy.Level ?? Energy.Mid;
        Energy[] energyRecipe = energy switch
        {
            Energy.Low => new[] { Energy.Low, Energy.Low, Energy.Low, Energy.Mid },
            Energy.Mid => new[] { Energy.Mid, Energy.Mid, Energy.Mid, Energy.Low },
            Energy.High => new[] { Energy.High, Energy.Mid, Energy.Mid, Energy.Low },
            _ => throw new System.ArgumentOutOfRangeException(nameof(energy), energy, "Unsupported Energy level."),
        };

        // Fisher-Yates changes the four-bar order without changing the recipe's exact Energy counts.
        for (int i = energyRecipe.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (energyRecipe[i], energyRecipe[swapIndex]) = (energyRecipe[swapIndex], energyRecipe[i]);
        }

        routine = Routine.Of(
            waveforms.Random(energyRecipe[0]),
            waveforms.Random(energyRecipe[1]),
            waveforms.Random(energyRecipe[2]),
            waveforms.Random(energyRecipe[3]));
    }

    /// <summary>Selects the gentle wander strength and turn rate used for the current Grid.</summary>
    private void RerollWander()
    {
        FloatRange wanderStrengthRange = IsSynced
            ? SyncSettings.WanderStrength
            : standaloneSettings.WanderStrength;
        FloatRange wanderTurnRateRange = IsSynced
            ? SyncSettings.WanderTurnRate
            : standaloneSettings.WanderTurnRate;
        wanderStrength = Random.Range(wanderStrengthRange.Min, wanderStrengthRange.Max);
        wanderTurnRate = Random.Range(wanderTurnRateRange.Min, wanderTurnRateRange.Max);
    }

    /// <summary>Randomly chooses one of the two directions around the wall center.</summary>
    /// <returns>Negative one for clockwise motion or positive one for counterclockwise motion.</returns>
    private static float RollDirection() => Random.value < 0.5f ? -1f : 1f;

    /// <summary>Resets values that are sampled or edge-detected independently for each activation.</summary>
    private void ResetMusicalState()
    {
        movementActivity = 0f;
        energyPaceMultiplier = 1f;
        midManeuver = 0f;
        spectralAgitation = 0f;
        routineEnvelope = 0f;
        fillOrbitDrive = 0f;
        dropSpiral = 0f;
        dropGather = 0f;
        dropAftermath = 0f;

        // Starting during an active event joins its sustained motion without inventing a false onset impulse.
        previousFillActive = beatManager.Fill.Active;
        previousDropActive = beatManager.Drop.Active;
    }

    /// <summary>Creates the fixed-size flock and gives every boid access to the completed shared array.</summary>
    /// <param name="min">Minimum wrapped wall-space bounds.</param>
    /// <param name="max">Maximum wrapped wall-space bounds.</param>
    private void CreateBoids(Vector2 min, Vector2 max)
    {
        flock = new Boid[BoidCount];
        FloatRange wanderFrequencyRange = IsSynced
            ? SyncSettings.WanderFrequency
            : standaloneSettings.WanderFrequency;
        for (int i = 0; i < BoidCount; i++)
        {
            flock[i] = new Boid(min, max, this, wanderFrequencyRange)
            {
                boids = flock,
            };
        }
    }

    // Per-frame musical interpretation and rendering

    /// <summary>Reads each musical source once and assigns it only to the visual behavior it was chosen to control.</summary>
    private void ReadMusicalInputs()
    {
        routineEnvelope = GetRoutineEnvelope(routine.Envelope);

        LevelBands smoothed = beatManager.Levels.Smoothed;
        LevelBands normalized = beatManager.Levels.Normalized;
        movementActivity = GetMovementActivity(
            smoothed.Low,
            smoothed.Mid,
            smoothed.High);
        energyPaceMultiplier = GetEnergyPaceMultiplier(beatManager.Energy.Level);

        midManeuver = GetMidManeuver(normalized.Mid, movementActivity);
        spectralAgitation = GetSpectralAgitation(normalized.Centroid, movementActivity);
    }

    /// <summary>Updates duration-aware Fill motion, Drop spiral/gather motion, and local onset impulses.</summary>
    private void UpdateMusicalMotion()
    {
        UpdateFillMotion();
        UpdateDropMotion();
    }

    /// <summary>
    /// Swells Fill orbit motion through the Fill's In Stock Envelope and adds one tangential
    /// impulse at onset, unless the active Drop owns its full In span.
    /// </summary>
    private void UpdateFillMotion()
    {
        var fill = beatManager.Fill;
        var drop = beatManager.Drop;
        bool fillActive = fill.Active;
        float fillBuild = fill.In.Build();
        fillOrbitDrive = drop.Active
            ? 0f
            : GetFillOrbitDrive(fillBuild, fill.LengthBeats);
        dropSpiral = drop.BeatsUntil.HasValue
            ? fillBuild
            : 0f;

        if (!drop.Active && fillActive && !previousFillActive)
        {
            float impulse = SyncSettings.FillOnsetImpulse * GetFillDurationBoost(fill.LengthBeats);
            for (int i = 0; i < flock.Length; i++)
            {
                flock[i].ApplyTangentialImpulse(wallCenter, fillOrbitDirection, impulse);
            }
        }

        previousFillActive = fillActive;
    }

    /// <summary>
    /// Builds the final zero-to-one gathering amount from the Drop's Before Stock Envelope,
    /// applies the landing burst, and samples the sixteen-beat In aftermath.
    /// </summary>
    private void UpdateDropMotion()
    {
        var drop = beatManager.Drop;
        bool dropActive = drop.Active;
        dropGather = drop.Before.Build(SyncSettings.DropGatherBeats);

        if (dropActive && !previousDropActive)
        {
            for (int i = 0; i < flock.Length; i++)
            {
                Boid boid = flock[i];
                boid.ApplyRadialImpulse(
                    wallCenter,
                    boid.maxSpeed * SyncSettings.DropBurstSpeedMultiplier,
                    SyncSettings.DropVelocityCarry);
            }
        }

        previousDropActive = dropActive;
        dropAftermath = drop.In.Decay(SyncSettings.DropAftermathBeats);
    }

    /// <summary>
    /// Fades the previous frame by Routine half-life, blooms that half-life at the Drop landing,
    /// or clears the frame when trails are disabled.
    /// </summary>
    private void PrepareFrameBuffer()
    {
        if (TrailsEnabled)
        {
            FloatRange trailHalfLife = IsSynced
                ? SyncSettings.TrailHalfLife
                : standaloneSettings.TrailHalfLife;
            float halfLife = Mathf.Clamp01(routineEnvelope).Lerp(
                trailHalfLife.Min,
                trailHalfLife.Max);
            halfLife = Mathf.Lerp(
                halfLife,
                SyncSettings.DropTrailHalfLife,
                dropAftermath);
            buffer.Fade(GetTrailRetention(halfLife, effectDelta));
        }
        else
        {
            buffer.Clear();
        }
    }

    /// <summary>Advances boids sequentially and projects their live palette colors onto nearest exact-center Penrose tiles.</summary>
    /// <remarks>
    /// Sequential updates are intentional existing behavior: later boids observe positions already advanced for
    /// earlier boids in the same frame. Converting this loop to multiple passes would subtly change the motion.
    /// </remarks>
    private void UpdateAndRenderBoids()
    {
        for (int i = 0; i < flock.Length; i++)
        {
            Boid boid = flock[i];
            boid.Update(effectDelta);

            // Sample the animated palette live; caching this color would miss palette transitions after OnStart.
            Color paletteColor = APalette.read((float)i / BoidCount, true);
            buffer[penrose.GetNearestTileIndex(boid.position)] =
                ApplyRoutineColor(paletteColor, routineEnvelope);
        }
    }

    // Musical and geometry mappings

    /// <summary>Returns low-dominant broad activity, or an active default when musical levels are unavailable.</summary>
    /// <param name="low">Smoothed Low-band level in <c>[0..1]</c>.</param>
    /// <param name="mid">Smoothed Mid-band level in <c>[0..1]</c>.</param>
    /// <param name="high">Smoothed High-band level in <c>[0..1]</c>.</param>
    /// <remarks>Mode selection reads whether live musical placement and levels are available.</remarks>
    /// <returns>Calibrated quiet-to-active movement in <c>[0..1]</c>.</returns>
    private float GetMovementActivity(float low, float mid, float high)
    {
        if (!IsSynced)
        {
            return standaloneSettings.MovementActivity;
        }

        float weighted = (Mathf.Clamp01(low) * SyncSettings.MovementLowWeight)
            + (Mathf.Clamp01(mid) * SyncSettings.MovementMidWeight)
            + (Mathf.Clamp01(high) * SyncSettings.MovementHighWeight);
        return weighted.Remap(
            SyncSettings.MovementLevel.Min,
            SyncSettings.MovementLevel.Max,
            0f,
            1f,
            clamp: true);
    }

    /// <summary>Returns the live Routine envelope, or its steady visual posture in Standalone Mode.</summary>
    /// <param name="envelope">Current Routine envelope in <c>[0..1]</c>.</param>
    /// <remarks>Mode selection reads whether a musical Grid is available to place the Routine.</remarks>
    /// <returns>The clamped live envelope, or the authored Standalone posture.</returns>
    private float GetRoutineEnvelope(float envelope)
    {
        return IsSynced
            ? Mathf.Clamp01(envelope)
            : standaloneSettings.RoutineEnvelope;
    }

    /// <summary>Composes Levels-driven activity with Energy phrase pace for the ordinary speed limit.</summary>
    /// <param name="activity">Calibrated broad movement activity.</param>
    /// <returns>Multiplier applied to each boid's authored maximum speed.</returns>
    private float GetMovementSpeedMultiplier(float activity)
    {
        FloatRange speedMultiplier = IsSynced
            ? SyncSettings.SpeedMultiplier
            : standaloneSettings.SpeedMultiplier;
        return Mathf.Clamp01(activity).Lerp(speedMultiplier.Min, speedMultiplier.Max)
            * energyPaceMultiplier;
    }

    /// <summary>Maps the current Energy level to the authored Low-to-High phrase pace.</summary>
    /// <param name="energy">Current track-relative Energy, or null while that classification rests.</param>
    /// <returns>
    /// The Low endpoint, the midpoint for Mid or unavailable Energy, or the High endpoint; one in
    /// Standalone Mode so the authored Standalone body remains unchanged.
    /// </returns>
    private float GetEnergyPaceMultiplier(Energy? energy)
    {
        if (!IsSynced)
        {
            return 1f;
        }

        return (energy ?? Energy.Mid) switch
        {
            Energy.Low => SyncSettings.EnergyPaceMultiplier.Min,
            Energy.Mid => Mathf.Lerp(
                SyncSettings.EnergyPaceMultiplier.Min,
                SyncSettings.EnergyPaceMultiplier.Max,
                0.5f),
            Energy.High => SyncSettings.EnergyPaceMultiplier.Max,
            _ => throw new ArgumentOutOfRangeException(nameof(energy), energy, "Unsupported Energy level."),
        };
    }

    /// <summary>Returns an activity-gated schooling maneuver from normalized Mid.</summary>
    /// <param name="normalizedMid">Normalized Mid-band level.</param>
    /// <param name="activity">Broad activity gate that prevents maneuvering during silence.</param>
    /// <returns>Schooling maneuver strength in <c>[0..1]</c>.</returns>
    private float GetMidManeuver(float normalizedMid, float activity)
    {
        float maneuver = normalizedMid.Remap(
            SyncSettings.MidManeuverLevel.Min,
            SyncSettings.MidManeuverLevel.Max,
            0f,
            1f,
            clamp: true);
        return maneuver * Mathf.Clamp01(activity);
    }

    /// <summary>Returns activity-gated separation and wander drive from normalized spectral centroid.</summary>
    /// <param name="normalizedCentroid">Normalized spectral centroid.</param>
    /// <param name="activity">Broad activity gate that prevents agitation during silence.</param>
    /// <returns>Spectral agitation in <c>[0..1]</c>.</returns>
    private float GetSpectralAgitation(float normalizedCentroid, float activity)
    {
        float agitation = normalizedCentroid.Remap(
            SyncSettings.SpectralCentroid.Min,
            SyncSettings.SpectralCentroid.Max,
            0f,
            1f,
            clamp: true);
        return agitation * Mathf.Clamp01(activity);
    }

    /// <summary>Maps an explicit trail half-life to frame-rate-independent retention for one elapsed step.</summary>
    /// <param name="halfLife">Seconds over which the retained buffer contribution halves.</param>
    /// <param name="deltaTime">Elapsed seconds represented by this fade step.</param>
    /// <returns>Per-step buffer retention in <c>[0..1]</c>.</returns>
    public static float GetTrailRetention(float halfLife, float deltaTime)
    {
        // Exponential decay gives an identical visual lifetime across different frame rates.
        return Mathf.Pow(0.5f, Mathf.Max(0f, deltaTime) / halfLife);
    }

    /// <summary>Applies the Routine's coordinated hue, saturation, and value treatment to one palette color.</summary>
    /// <param name="color">Live animated palette color.</param>
    /// <param name="envelope">Routine envelope controlling treatment strength.</param>
    /// <returns>The treated color with the original alpha preserved.</returns>
    private Color ApplyRoutineColor(Color color, float envelope)
    {
        float amount = Mathf.Clamp01(envelope);
        float hueShift = IsSynced
            ? SyncSettings.RoutineHueShift
            : standaloneSettings.RoutineHueShift;
        float saturationFloor = IsSynced
            ? SyncSettings.RoutineSaturationFloor
            : standaloneSettings.RoutineSaturationFloor;
        float valueFloor = IsSynced
            ? SyncSettings.RoutineValueFloor
            : standaloneSettings.RoutineValueFloor;
        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        float shiftedHue = (hue + (hueShift * amount)) % 1f;
        float shiftedSaturation = saturation * Mathf.Lerp(saturationFloor, 1f, amount);
        float shiftedValue = value * Mathf.Lerp(valueFloor, 1f, amount);
        Color shifted = Color.HSVToRGB(shiftedHue, shiftedSaturation, shiftedValue);
        shifted.a = color.a;
        return shifted;
    }

    /// <summary>Rotates a boid's current travel direction by a local wander heading offset.</summary>
    /// <param name="velocity">Current travel vector; zero falls back to world-space right.</param>
    /// <param name="headingOffsetRadians">Signed local rotation in radians.</param>
    /// <returns>A normalized direction relative to current travel.</returns>
    public static Vector2 GetWanderDirection(Vector2 velocity, float headingOffsetRadians)
    {
        Vector2 forward = velocity.sqrMagnitude > Mathf.Epsilon ? velocity.normalized : Vector2.right;
        float sin = Mathf.Sin(headingOffsetRadians);
        float cos = Mathf.Cos(headingOffsetRadians);
        return new Vector2(
            (forward.x * cos) - (forward.y * sin),
            (forward.x * sin) + (forward.y * cos));
    }

    /// <summary>Returns a left or right quarter-turn relative to the flock's current travel direction.</summary>
    /// <param name="velocity">Current travel vector.</param>
    /// <param name="turnDirection">Negative turns left; zero or positive turns right.</param>
    /// <returns>A normalized relative quarter-turn direction.</returns>
    public static Vector2 GetCollectiveTurnDirection(Vector2 velocity, float turnDirection)
    {
        float signedQuarterTurn = turnDirection < 0f ? -Mathf.PI * 0.5f : Mathf.PI * 0.5f;
        return GetWanderDirection(velocity, signedQuarterTurn);
    }

    /// <summary>Returns the shortest offset from one point to another across wrapped rectangular bounds.</summary>
    /// <param name="from">Origin position inside the wrapped bounds.</param>
    /// <param name="to">Target position inside the wrapped bounds.</param>
    /// <param name="boundsSize">Width and height of the wrapped space.</param>
    /// <returns>The shortest visible offset from <paramref name="from"/> to <paramref name="to"/>.</returns>
    public static Vector2 GetWrappedOffset(Vector2 from, Vector2 to, Vector2 boundsSize)
    {
        Vector2 offset = to - from;
        if (boundsSize.x > Mathf.Epsilon && Mathf.Abs(offset.x) > boundsSize.x * 0.5f)
        {
            offset.x -= Mathf.Sign(offset.x) * boundsSize.x;
        }

        if (boundsSize.y > Mathf.Epsilon && Mathf.Abs(offset.y) > boundsSize.y * 0.5f)
        {
            offset.y -= Mathf.Sign(offset.y) * boundsSize.y;
        }

        return offset;
    }

    /// <summary>Returns the shared choreography and onset boost for Fill windows shorter than the typical duration.</summary>
    /// <param name="lengthBeats">Known Fill length, or <see langword="null"/> when unavailable.</param>
    /// <returns>Configured duration compensation for a short Fill.</returns>
    private float GetFillDurationBoost(int? lengthBeats)
    {
        float duration = lengthBeats is > 0
            ? Mathf.Clamp(
                lengthBeats.Value,
                SyncSettings.MinimumFillBeats,
                SyncSettings.TypicalFillBeats)
            : SyncSettings.TypicalFillBeats;
        return Mathf.Sqrt(SyncSettings.TypicalFillBeats / duration);
    }

    /// <summary>Builds the duration-aware orbit through an active Fill's In Stock Envelope.</summary>
    /// <param name="fillBuild">Continuous zero-to-one build through the active Fill.</param>
    /// <param name="lengthBeats">Known Fill length used for short-window compensation.</param>
    /// <returns>Fill orbit drive; short Fills may intentionally return more than 1.</returns>
    private float GetFillOrbitDrive(float fillBuild, int? lengthBeats)
    {
        return fillBuild * GetFillDurationBoost(lengthBeats);
    }

    // Individual boid simulation

    /// <summary>One moving particle in wall space before projection onto the nearest Penrose tile.</summary>
    /// <remarks>
    /// Each boid computes three classic flocking forces—alignment, cohesion, and separation—using wrapped
    /// neighbor offsets. It then layers musical steering in a fixed priority order before integrating velocity.
    /// </remarks>
    public class Boid
    {
        /// <summary>Current position in wrapped wall coordinates.</summary>
        public Vector2 position;

        /// <summary>Current wall-space travel velocity.</summary>
        public Vector2 velocity;

        /// <summary>Total steering accumulated for the current simulation step.</summary>
        public Vector2 acceleration;

        /// <summary>Maximum neighbor distance considered by flocking, in wall-space units.</summary>
        public float perception = 3f;

        /// <summary>Authored maximum ordinary speed in wall-space units per second.</summary>
        public float maxSpeed = 20f;

        /// <summary>Maximum magnitude of one steering force.</summary>
        public float maxForce = 0.25f;

        /// <summary>Shared flock inspected for nearby neighbors.</summary>
        public Boid[] boids;

        /// <summary>Owning effect that supplies live musical and Grid character state.</summary>
        public Flock parent;

        /// <summary>Minimum wrapped wall-space bounds.</summary>
        private readonly Vector2 min;

        /// <summary>Maximum wrapped wall-space bounds.</summary>
        private readonly Vector2 max;

        /// <summary>Steering toward neighbors' average velocity.</summary>
        private Vector2 alignmentSteering;

        /// <summary>Steering toward the local wrapped neighbor center.</summary>
        private Vector2 cohesionSteering;

        /// <summary>Steering away from nearby neighbors.</summary>
        private Vector2 separationSteering;

        /// <summary>Current position within this boid's independent wander cycle.</summary>
        private float wanderPhase;

        /// <summary>Per-boid phase multiplier that keeps the flock from steering in lockstep.</summary>
        private readonly float wanderFrequency;

        /// <summary>Creates one boid with random position, velocity, and independent wander phase.</summary>
        /// <param name="min">Minimum wrapped wall-space bounds.</param>
        /// <param name="max">Maximum wrapped wall-space bounds.</param>
        /// <param name="parent">Owning Flock effect.</param>
        /// <param name="wanderFrequencyRange">Endpoints for this boid's one-time wander-frequency roll,
        /// resolved by the caller from the mode-selected settings surface.</param>
        public Boid(Vector2 min, Vector2 max, Flock parent, FloatRange wanderFrequencyRange)
        {
            this.min = min;
            this.max = max;
            this.parent = parent;
            velocity = new Vector2(Random.Range(-maxSpeed, maxSpeed), Random.Range(-maxSpeed, maxSpeed));
            position = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
            wanderPhase = Random.Range(0f, Mathf.PI * 2f);
            wanderFrequency = Random.Range(wanderFrequencyRange.Min, wanderFrequencyRange.Max);
        }

        /// <summary>Adds an immediate clockwise or counter-clockwise velocity impulse around a center point.</summary>
        /// <param name="center">Center of rotation.</param>
        /// <param name="direction">Negative for clockwise, zero or positive for counter-clockwise.</param>
        /// <param name="magnitude">Velocity magnitude added along the tangent.</param>
        public void ApplyTangentialImpulse(Vector2 center, float direction, float magnitude)
        {
            Vector2 radial = position - center;
            if (radial.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 tangent = new(-radial.y, radial.x);
            float sign = direction < 0f ? -1f : 1f;
            velocity += tangent.normalized * (magnitude * sign);
        }

        /// <summary>Launches the boid away from a center point while retaining a share of its existing momentum.</summary>
        /// <param name="center">Center from which the boid launches.</param>
        /// <param name="magnitude">Outward velocity magnitude.</param>
        /// <param name="velocityCarry">Share of existing velocity retained in <c>[0..1]</c>.</param>
        public void ApplyRadialImpulse(Vector2 center, float magnitude, float velocityCarry)
        {
            Vector2 radial = position - center;
            if (radial.sqrMagnitude <= Mathf.Epsilon)
            {
                radial = velocity.sqrMagnitude > Mathf.Epsilon ? velocity : Vector2.right;
            }

            velocity = (velocity * Mathf.Clamp01(velocityCarry)) + (radial.normalized * magnitude);
        }

        /// <summary>Advances position, neighbor flocking, musical steering, velocity, and wrapping for one frame.</summary>
        /// <param name="deltaTime">Elapsed effect time in seconds.</param>
        public void Update(float deltaTime)
        {
            if (boids == null)
            {
                return;
            }

            position += deltaTime * velocity;

            float speedLimit = GetActiveSpeedLimit();
            MeasureFlockSteering(speedLimit);
            acceleration = GetWeightedFlockingAcceleration();
            AddMusicalSteering(deltaTime, speedLimit);

            velocity += acceleration;
            velocity = Vector2.ClampMagnitude(velocity, speedLimit);
            WrapAtBounds();
        }

        /// <summary>Returns the ordinary speed limit lifted as needed by Fill, gathering, or Drop aftermath.</summary>
        /// <returns>Maximum velocity magnitude for the current frame.</returns>
        private float GetActiveSpeedLimit()
        {
            float ordinarySpeedLimit = maxSpeed * parent.GetMovementSpeedMultiplier(parent.movementActivity);
            float eventDrive = Mathf.Clamp01(Mathf.Max(parent.fillOrbitDrive, parent.dropGather));
            float speedLimit = Mathf.Lerp(
                ordinarySpeedLimit,
                Mathf.Max(ordinarySpeedLimit, maxSpeed),
                eventDrive);
            return Mathf.Lerp(
                speedLimit,
                maxSpeed * (1f + parent.SyncSettings.DropSpeedLift),
                parent.dropAftermath);
        }

        /// <summary>Combines classic flocking forces after applying live musical weight changes.</summary>
        /// <returns>Weighted alignment, cohesion, and separation acceleration.</returns>
        private Vector2 GetWeightedFlockingAcceleration()
        {
            float fillSpread = parent.fillOrbitDrive
                * (1f - Mathf.Max(parent.dropSpiral, parent.dropGather));
            float baseAlignmentWeight = parent.IsSynced
                ? parent.SyncSettings.BaseAlignmentWeight
                : parent.standaloneSettings.BaseAlignmentWeight;
            float baseCohesionWeight = parent.IsSynced
                ? parent.SyncSettings.BaseCohesionWeight
                : parent.standaloneSettings.BaseCohesionWeight;
            float baseSeparationWeight = parent.IsSynced
                ? parent.SyncSettings.BaseSeparationWeight
                : parent.standaloneSettings.BaseSeparationWeight;
            float alignmentWeight = baseAlignmentWeight
                * (1f + (parent.SyncSettings.FillAlignmentLift * parent.fillOrbitDrive)
                    + (parent.SyncSettings.MidAlignmentLift * parent.midManeuver));
            float cohesionWeight = baseCohesionWeight
                * (1f + (parent.SyncSettings.MidCohesionLift * parent.midManeuver))
                * (1f - (parent.SyncSettings.DropCohesionSuppression * parent.dropAftermath));
            float separationWeight = baseSeparationWeight
                * parent.energyPaceMultiplier
                * (1f + (parent.SyncSettings.FillSeparationLift * fillSpread)
                    + (parent.SyncSettings.SpectralSeparationLift * parent.spectralAgitation)
                    + (parent.SyncSettings.DropSeparationLift * parent.dropAftermath))
                * (1f - (parent.SyncSettings.DropGatherSeparationSuppression * parent.dropGather));

            return (alignmentSteering * alignmentWeight)
                + (cohesionSteering * cohesionWeight)
                + (separationSteering * separationWeight);
        }

        /// <summary>Adds collective turning, independent wander, Fill orbit, Drop gathering, and Drop aftermath steering.</summary>
        /// <param name="deltaTime">Elapsed effect time in seconds.</param>
        /// <param name="speedLimit">Current desired speed used by steering forces.</param>
        private void AddMusicalSteering(float deltaTime, float speedLimit)
        {
            float preDropDominance = Mathf.Max(parent.dropSpiral, parent.dropGather);
            float dropDominance = Mathf.Max(preDropDominance, parent.dropAftermath);
            float eventDominance = Mathf.Clamp01(Mathf.Max(parent.fillOrbitDrive, dropDominance));

            // Mid bends the shared course only while Fill/Drop choreography is not dominant.
            Vector2 collectiveTurn = GetCollectiveTurnDirection(velocity, parent.courseTurnDirection);
            acceleration += Steer(collectiveTurn, speedLimit)
                * (parent.SyncSettings.CollectiveTurnStrength * parent.midManeuver * (1f - eventDominance));

            // Every boid wanders independently; the spiral and gather suppress that local disagreement,
            // then the landing turns it back on so the torn-apart flock writes across the dark field.
            float quietWanderMultiplier = parent.IsSynced
                ? parent.SyncSettings.QuietWanderMultiplier
                : parent.standaloneSettings.QuietWanderMultiplier;
            float quietWanderTurnMultiplier = parent.IsSynced
                ? parent.SyncSettings.QuietWanderTurnMultiplier
                : parent.standaloneSettings.QuietWanderTurnMultiplier;
            float activeWanderStrength = parent.wanderStrength
                * Mathf.Lerp(quietWanderMultiplier, 1f, parent.movementActivity)
                * (1f + (parent.SyncSettings.SpectralWanderStrengthLift * parent.spectralAgitation));
            float activeWanderTurnRate = parent.wanderTurnRate
                * Mathf.Lerp(quietWanderTurnMultiplier, 1f, parent.movementActivity)
                * (1f + (parent.SyncSettings.SpectralWanderTurnLift * parent.spectralAgitation));
            acceleration += UpdateWander(
                    deltaTime,
                    speedLimit,
                    activeWanderStrength,
                    activeWanderTurnRate)
                * (1f - preDropDominance);

            // Fill circles the center, an announced Drop bends it inward immediately, the gather finishes
            // the collapse, and the Drop reverses it outward.
            Vector2 radial = position - parent.wallCenter;
            Vector2 tangent = new(-radial.y, radial.x);
            float orbitAmount = parent.fillOrbitDrive * Mathf.Lerp(
                1f,
                parent.SyncSettings.FillOrbitAtFullGather,
                parent.dropGather);
            acceleration += Steer(tangent * parent.fillOrbitDirection, speedLimit)
                * (parent.SyncSettings.FillOrbitSteering * orbitAmount);
            acceleration += Steer(-radial, speedLimit)
                * (parent.SyncSettings.DropSpiralSteering * parent.dropSpiral);
            acceleration += Steer(-radial, speedLimit)
                * (parent.SyncSettings.DropGatherSteering * parent.dropGather);
            acceleration += Steer(radial, speedLimit)
                * (parent.SyncSettings.DropOutwardSteering * parent.dropAftermath);
        }

        /// <summary>Advances this boid's independent wander phase and returns its gentle steering force.</summary>
        /// <param name="deltaTime">Elapsed effect time in seconds.</param>
        /// <param name="desiredSpeed">Current desired speed used by steering.</param>
        /// <param name="activeWanderStrength">Current music-scaled wander force.</param>
        /// <param name="activeWanderTurnRate">Current music-scaled wander phase rate.</param>
        /// <returns>Max-force-limited wander steering.</returns>
        private Vector2 UpdateWander(
            float deltaTime,
            float desiredSpeed,
            float activeWanderStrength,
            float activeWanderTurnRate)
        {
            wanderPhase = Mathf.Repeat(
                wanderPhase + (Mathf.Max(0f, deltaTime) * activeWanderTurnRate * wanderFrequency),
                Mathf.PI * 2f);
            float wanderHeadingRadians = parent.IsSynced
                ? parent.SyncSettings.WanderHeadingRadians
                : parent.standaloneSettings.WanderHeadingRadians;
            float headingOffset = Mathf.Sin(wanderPhase) * wanderHeadingRadians;
            return Steer(GetWanderDirection(velocity, headingOffset), desiredSpeed) * activeWanderStrength;
        }

        /// <summary>Returns a max-force-limited steering vector along the requested direction.</summary>
        /// <param name="direction">Desired travel direction.</param>
        /// <param name="desiredSpeed">Desired velocity magnitude.</param>
        /// <returns>Steering delta limited by <see cref="maxForce"/>.</returns>
        private Vector2 Steer(Vector2 direction, float desiredSpeed)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            Vector2 desired = direction.normalized * desiredSpeed;
            return Vector2.ClampMagnitude(desired - velocity, maxForce);
        }

        /// <summary>Wraps the boid to the opposite wall edge when it leaves the visible bounds.</summary>
        private void WrapAtBounds()
        {
            if (position.x > max.x)
            {
                position.x = min.x;
            }
            else if (position.x < min.x)
            {
                position.x = max.x;
            }

            if (position.y > max.y)
            {
                position.y = min.y;
            }
            else if (position.y < min.y)
            {
                position.y = max.y;
            }
        }

        /// <summary>Measures alignment, cohesion, and separation steering from nearby wrapped neighbors.</summary>
        /// <param name="desiredSpeed">Desired velocity magnitude used to form each steering vector.</param>
        private void MeasureFlockSteering(float desiredSpeed)
        {
            alignmentSteering = Vector2.zero;
            cohesionSteering = Vector2.zero;
            separationSteering = Vector2.zero;

            int neighborCount = 0;
            Vector2 boundsSize = max - min;
            float squaredPerception = perception * perception;
            for (int i = 0; i < boids.Length; i++)
            {
                Boid neighbor = boids[i];
                if (neighbor == this)
                {
                    continue;
                }

                Vector2 offset = GetWrappedOffset(position, neighbor.position, boundsSize);
                float squaredDistance = offset.sqrMagnitude;
                if (squaredDistance > squaredPerception)
                {
                    continue;
                }

                alignmentSteering += neighbor.velocity;

                // Average wrapped offsets, not absolute positions, so edge neighbors pull across the seam correctly.
                cohesionSteering += offset;

                if (squaredDistance > Mathf.Epsilon)
                {
                    separationSteering -= offset / squaredDistance;
                }

                neighborCount++;
            }

            if (neighborCount == 0)
            {
                return;
            }

            alignmentSteering /= neighborCount;
            alignmentSteering = alignmentSteering.SetMagnitude(desiredSpeed);
            alignmentSteering -= velocity;
            alignmentSteering = Vector2.ClampMagnitude(alignmentSteering, maxForce);

            cohesionSteering /= neighborCount;
            cohesionSteering = cohesionSteering.SetMagnitude(desiredSpeed);
            cohesionSteering -= velocity;
            cohesionSteering = Vector2.ClampMagnitude(cohesionSteering, maxForce);

            separationSteering /= neighborCount;
            separationSteering = separationSteering.SetMagnitude(desiredSpeed);
            separationSteering -= velocity;
            separationSteering = Vector2.ClampMagnitude(separationSteering, maxForce);
        }
    }
}

/// <summary>
/// The serializable value shape shared by Flock's fully populated Standalone Defaults and saved
/// Standalone Settings; Unity may create an empty instance before serialized values are applied.
/// </summary>
[Serializable]
public sealed class FlockStandaloneSettings
{
    /// <summary>Baseline weight of steering toward neighbors' travel direction.</summary>
    [Min(0f)] public float BaseAlignmentWeight;

    /// <summary>Baseline weight of steering toward the local flock center.</summary>
    [Min(0f)] public float BaseCohesionWeight;

    /// <summary>Baseline weight of steering away from nearby boids.</summary>
    [Min(0f)] public float BaseSeparationWeight;

    /// <summary>Fixed broad movement activity used without live musical placement.</summary>
    [Range(0f, 1f)] public float MovementActivity;

    /// <summary>
    /// Quiet-to-active range for the ordinary speed limit relative to each boid's authored maximum.
    /// </summary>
    public FloatRange SpeedMultiplier;

    /// <summary>Fixed Routine posture used without a musical Grid.</summary>
    [Range(0f, 1f)] public float RoutineEnvelope;

    /// <summary>Trail half-life range interpolated from the bottom to the top of the Routine envelope.</summary>
    public FloatRange TrailHalfLife;

    /// <summary>Maximum coordinated hue shift applied by the Routine.</summary>
    [Range(0f, 1f)] public float RoutineHueShift;

    /// <summary>Share of authored saturation retained at the bottom of the Routine.</summary>
    [Range(0f, 1f)] public float RoutineSaturationFloor;

    /// <summary>Share of authored value retained at the bottom of the Routine.</summary>
    [Range(0f, 1f)] public float RoutineValueFloor;

    /// <summary>Per-Grid range for continuous wander strength.</summary>
    public FloatRange WanderStrength;

    /// <summary>Per-Grid range for wander phase rate in radians per second.</summary>
    public FloatRange WanderTurnRate;

    /// <summary>Maximum heading offset used by continuous wander.</summary>
    [Min(0f)] public float WanderHeadingRadians;

    /// <summary>Per-boid range for the independent wander-frequency multiplier.</summary>
    public FloatRange WanderFrequency;

    /// <summary>Share of ordinary wander strength retained during complete quiet.</summary>
    [Range(0f, 1f)] public float QuietWanderMultiplier;

    /// <summary>Share of ordinary wander turn rate retained during complete quiet.</summary>
    [Range(0f, 1f)] public float QuietWanderTurnMultiplier;

    /// <summary>
    /// Copies every Flock Standalone Setting, including range endpoints and Rails, from another value.
    /// </summary>
    public void CopyFrom(FlockStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BaseAlignmentWeight = source.BaseAlignmentWeight;
        BaseCohesionWeight = source.BaseCohesionWeight;
        BaseSeparationWeight = source.BaseSeparationWeight;
        MovementActivity = source.MovementActivity;
        SpeedMultiplier = CopyRange(source.SpeedMultiplier);
        RoutineEnvelope = source.RoutineEnvelope;
        TrailHalfLife = CopyRange(source.TrailHalfLife);
        RoutineHueShift = source.RoutineHueShift;
        RoutineSaturationFloor = source.RoutineSaturationFloor;
        RoutineValueFloor = source.RoutineValueFloor;
        WanderStrength = CopyRange(source.WanderStrength);
        WanderTurnRate = CopyRange(source.WanderTurnRate);
        WanderHeadingRadians = source.WanderHeadingRadians;
        WanderFrequency = CopyRange(source.WanderFrequency);
        QuietWanderMultiplier = source.QuietWanderMultiplier;
        QuietWanderTurnMultiplier = source.QuietWanderTurnMultiplier;
    }

    /// <summary>Copies one range so a saved Rail can never mutate the authored default range.</summary>
    /// <param name="source">Range whose endpoints and Rails are copied.</param>
    /// <returns>An independent range with the same endpoints and Rails.</returns>
    private static FloatRange CopyRange(FloatRange source)
    {
        return new FloatRange(source.Min, source.Max, source.LowRail, source.HighRail);
    }
}

/// <summary>The serializable value shape shared by Flock's Sync Defaults and saved Sync Settings.</summary>
[Serializable]
public sealed class FlockSyncSettings
{
    /// <summary>Baseline weight of steering toward neighbors' travel direction.</summary>
    [Min(0f)] public float BaseAlignmentWeight;

    /// <summary>Baseline weight of steering toward the local flock center.</summary>
    [Min(0f)] public float BaseCohesionWeight;

    /// <summary>Baseline weight of steering away from nearby boids.</summary>
    [Min(0f)] public float BaseSeparationWeight;

    /// <summary>Share of broad movement activity contributed by the smoothed Low band.</summary>
    [Range(0f, 1f)] public float MovementLowWeight;

    /// <summary>Share of broad movement activity contributed by the smoothed Mid band.</summary>
    [Range(0f, 1f)] public float MovementMidWeight;

    /// <summary>Share of broad movement activity contributed by the smoothed High band.</summary>
    [Range(0f, 1f)] public float MovementHighWeight;

    /// <summary>Weighted Levels range mapped from the flock's quiet posture to its active posture.</summary>
    public FloatRange MovementLevel;

    /// <summary>
    /// Quiet-to-active range for the ordinary speed limit relative to each boid's authored maximum.
    /// </summary>
    public FloatRange SpeedMultiplier;

    /// <summary>
    /// Low-to-High Energy range for phrase pace; Mid uses the midpoint, and the selected value also
    /// scales separation so Low tightens while High loosens.
    /// </summary>
    public FloatRange EnergyPaceMultiplier;

    /// <summary>Normalized Mid range mapped from no schooling maneuver to the full maneuver.</summary>
    public FloatRange MidManeuverLevel;

    /// <summary>Alignment lift on a strong normalized Mid-band maneuver.</summary>
    [Min(0f)] public float MidAlignmentLift;

    /// <summary>Cohesion lift on a strong normalized Mid-band maneuver.</summary>
    [Min(0f)] public float MidCohesionLift;

    /// <summary>Shared course-bending force applied by a strong normalized Mid-band maneuver.</summary>
    [Min(0f)] public float CollectiveTurnStrength;

    /// <summary>Normalized spectral-centroid range mapped from no agitation to full agitation.</summary>
    public FloatRange SpectralCentroid;

    /// <summary>Separation lift when the normalized spectrum leans toward high-frequency detail.</summary>
    [Min(0f)] public float SpectralSeparationLift;

    /// <summary>Trail half-life range interpolated from the bottom to the top of the Routine envelope.</summary>
    public FloatRange TrailHalfLife;

    /// <summary>Maximum coordinated hue shift applied by the Routine.</summary>
    [Range(0f, 1f)] public float RoutineHueShift;

    /// <summary>Share of authored saturation retained at the bottom of the Routine.</summary>
    [Range(0f, 1f)] public float RoutineSaturationFloor;

    /// <summary>Share of authored value retained at the bottom of the Routine.</summary>
    [Range(0f, 1f)] public float RoutineValueFloor;

    /// <summary>The common Fill length used as the unboosted response baseline.</summary>
    [Min(0.0001f)] public float TypicalFillBeats;

    /// <summary>The shortest supported Fill duration used to cap short-window compensation.</summary>
    [Min(0.0001f)] public float MinimumFillBeats;

    /// <summary>Base tangential velocity added when a typical Fill begins.</summary>
    [Min(0f)] public float FillOnsetImpulse;

    /// <summary>Strength of continuous Fill steering around wall center.</summary>
    [Min(0f)] public float FillOrbitSteering;

    /// <summary>Share of Fill orbit retained at the end of the Drop gather.</summary>
    [Range(0f, 1f)] public float FillOrbitAtFullGather;

    /// <summary>Alignment lift at full Fill drive.</summary>
    [Min(0f)] public float FillAlignmentLift;

    /// <summary>Separation lift that lets a Fill-only orbit widen.</summary>
    [Min(0f)] public float FillSeparationLift;

    /// <summary>Soft center-seeking steering that bends a Fill orbit as soon as its Drop is announced.</summary>
    [Min(0f)] public float DropSpiralSteering;

    /// <summary>Number of beats before a Drop over which the flock finishes gathering at wall center.</summary>
    [Min(1)] public int DropGatherBeats;

    /// <summary>Strength of center-seeking steering at the end of the pre-Drop gather.</summary>
    [Min(0f)] public float DropGatherSteering;

    /// <summary>Share of ordinary separation removed at the end of the pre-Drop gather.</summary>
    [Range(0f, 1f)] public float DropGatherSeparationSuppression;

    /// <summary>Number of Drop beats over which the torn-apart aftermath fades from the landing.</summary>
    [Min(16)] public int DropAftermathBeats;

    /// <summary>Trail half-life in seconds at the Drop landing before the bloom fades.</summary>
    [Min(0.0001f)] public float DropTrailHalfLife;

    /// <summary>Initial Drop burst speed relative to each boid's ordinary maximum speed.</summary>
    [Min(0f)] public float DropBurstSpeedMultiplier;

    /// <summary>Share of pre-Drop velocity retained in the radial burst.</summary>
    [Range(0f, 1f)] public float DropVelocityCarry;

    /// <summary>Strength of outward steering during the Drop aftermath.</summary>
    [Min(0f)] public float DropOutwardSteering;

    /// <summary>Maximum speed lift at the start of the Drop aftermath.</summary>
    [Min(0f)] public float DropSpeedLift;

    /// <summary>Share of cohesion removed at the start of the Drop aftermath.</summary>
    [Range(0f, 1f)] public float DropCohesionSuppression;

    /// <summary>Separation lift at the start of the Drop aftermath.</summary>
    [Min(0f)] public float DropSeparationLift;

    /// <summary>Share of ordinary wander strength retained during complete quiet.</summary>
    [Range(0f, 1f)] public float QuietWanderMultiplier;

    /// <summary>Share of ordinary wander turn rate retained during complete quiet.</summary>
    [Range(0f, 1f)] public float QuietWanderTurnMultiplier;

    /// <summary>Additional wander strength at full spectral agitation.</summary>
    [Min(0f)] public float SpectralWanderStrengthLift;

    /// <summary>Additional wander turn rate at full spectral agitation.</summary>
    [Min(0f)] public float SpectralWanderTurnLift;

    /// <summary>Per-Grid range for continuous wander strength.</summary>
    public FloatRange WanderStrength;

    /// <summary>Per-Grid range for wander phase rate in radians per second.</summary>
    public FloatRange WanderTurnRate;

    /// <summary>Maximum heading offset used by continuous wander.</summary>
    [Min(0f)] public float WanderHeadingRadians;

    /// <summary>Per-boid range for the independent wander-frequency multiplier.</summary>
    public FloatRange WanderFrequency;

    /// <summary>Copies every Flock Sync Setting from another value.</summary>
    public void CopyFrom(FlockSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BaseAlignmentWeight = source.BaseAlignmentWeight;
        BaseCohesionWeight = source.BaseCohesionWeight;
        BaseSeparationWeight = source.BaseSeparationWeight;
        MovementLowWeight = source.MovementLowWeight;
        MovementMidWeight = source.MovementMidWeight;
        MovementHighWeight = source.MovementHighWeight;
        MovementLevel = CopyRange(source.MovementLevel);
        SpeedMultiplier = CopyRange(source.SpeedMultiplier);
        EnergyPaceMultiplier = CopyRange(source.EnergyPaceMultiplier);
        MidManeuverLevel = CopyRange(source.MidManeuverLevel);
        MidAlignmentLift = source.MidAlignmentLift;
        MidCohesionLift = source.MidCohesionLift;
        CollectiveTurnStrength = source.CollectiveTurnStrength;
        SpectralCentroid = CopyRange(source.SpectralCentroid);
        SpectralSeparationLift = source.SpectralSeparationLift;
        TrailHalfLife = CopyRange(source.TrailHalfLife);
        RoutineHueShift = source.RoutineHueShift;
        RoutineSaturationFloor = source.RoutineSaturationFloor;
        RoutineValueFloor = source.RoutineValueFloor;
        TypicalFillBeats = source.TypicalFillBeats;
        MinimumFillBeats = source.MinimumFillBeats;
        FillOnsetImpulse = source.FillOnsetImpulse;
        FillOrbitSteering = source.FillOrbitSteering;
        FillOrbitAtFullGather = source.FillOrbitAtFullGather;
        FillAlignmentLift = source.FillAlignmentLift;
        FillSeparationLift = source.FillSeparationLift;
        DropSpiralSteering = source.DropSpiralSteering;
        DropGatherBeats = source.DropGatherBeats;
        DropGatherSteering = source.DropGatherSteering;
        DropGatherSeparationSuppression = source.DropGatherSeparationSuppression;
        DropAftermathBeats = source.DropAftermathBeats;
        DropTrailHalfLife = source.DropTrailHalfLife;
        DropBurstSpeedMultiplier = source.DropBurstSpeedMultiplier;
        DropVelocityCarry = source.DropVelocityCarry;
        DropOutwardSteering = source.DropOutwardSteering;
        DropSpeedLift = source.DropSpeedLift;
        DropCohesionSuppression = source.DropCohesionSuppression;
        DropSeparationLift = source.DropSeparationLift;
        QuietWanderMultiplier = source.QuietWanderMultiplier;
        QuietWanderTurnMultiplier = source.QuietWanderTurnMultiplier;
        SpectralWanderStrengthLift = source.SpectralWanderStrengthLift;
        SpectralWanderTurnLift = source.SpectralWanderTurnLift;
        WanderStrength = CopyRange(source.WanderStrength);
        WanderTurnRate = CopyRange(source.WanderTurnRate);
        WanderHeadingRadians = source.WanderHeadingRadians;
        WanderFrequency = CopyRange(source.WanderFrequency);
    }

    /// <summary>Copies one range so a saved Rail can never mutate the authored default range.</summary>
    /// <param name="source">Range whose endpoints and Rails are copied.</param>
    /// <returns>An independent range with the same endpoints and Rails.</returns>
    private static FloatRange CopyRange(FloatRange source)
    {
        return new FloatRange(source.Min, source.Max, source.LowRail, source.HighRail);
    }
}
