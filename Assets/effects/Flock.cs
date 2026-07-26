// Advanced reference effect: music interpretation, event choreography, boid simulation, and tile rendering.

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
public class Flock : EffectBase
{
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

    /// <summary>Number of boids simulated by the effect.</summary>
    private const int BoidCount = 80;

    // Baseline flocking

    /// <summary>Baseline weight of steering toward neighbors' travel direction.</summary>
    private const float BaseAlignmentWeight = 0.75f;

    /// <summary>Baseline weight of steering toward the local flock center.</summary>
    private const float BaseCohesionWeight = 1f;

    /// <summary>Baseline weight of steering away from nearby boids.</summary>
    private const float BaseSeparationWeight = 1.25f;

    // Broad level-driven movement

    /// <summary>Share of broad movement activity contributed by the smoothed Low band.</summary>
    private const float MovementLowWeight = 0.7f;

    /// <summary>Share of broad movement activity contributed by the smoothed Mid band.</summary>
    private const float MovementMidWeight = 0.25f;

    /// <summary>Share of broad movement activity contributed by the smoothed High band.</summary>
    private const float MovementHighWeight = 0.05f;

    /// <summary>Weighted level below which the flock adopts its quiet posture.</summary>
    private const float QuietActivityThreshold = 0.1f;

    /// <summary>Weighted level at which the flock reaches its active posture.</summary>
    private const float ActiveActivityThreshold = 0.5f;

    /// <summary>Ordinary speed limit at complete quiet, relative to each boid's authored maximum.</summary>
    private const float QuietSpeedMultiplier = 0.1f;

    /// <summary>Ordinary speed limit at full activity, relative to each boid's authored maximum.</summary>
    private const float ActiveSpeedMultiplier = 1.5f;

    /// <summary>Standalone movement posture that restores approximately the authored one-times speed.</summary>
    private const float StandaloneMovementActivity = 0.65f;

    // Normalized schooling and spectral detail

    /// <summary>Normalized Mid level below which no schooling maneuver is added.</summary>
    private const float MidManeuverThreshold = 0.2f;

    /// <summary>Normalized Mid level that produces the full schooling maneuver.</summary>
    private const float MidManeuverFull = 0.65f;

    /// <summary>Alignment lift on a strong normalized Mid-band maneuver.</summary>
    private const float MidAlignmentLift = 0.75f;

    /// <summary>Cohesion lift on a strong normalized Mid-band maneuver.</summary>
    private const float MidCohesionLift = 0.5f;

    /// <summary>Shared course-bending force applied by a strong normalized Mid-band maneuver.</summary>
    private const float CollectiveTurnStrength = 0.75f;

    /// <summary>Normalized spectral centroid below which no extra agitation is added.</summary>
    private const float SpectralCentroidThreshold = 0.25f;

    /// <summary>Normalized spectral centroid that produces full extra agitation.</summary>
    private const float SpectralCentroidFull = 0.65f;

    /// <summary>Separation lift when the normalized spectrum leans toward high-frequency detail.</summary>
    private const float SpectralSeparationLift = 1f;

    // Routine-driven color and trails

    /// <summary>Standalone Routine posture that preserves full palette color and visible trails without a Grid.</summary>
    private const float StandaloneRoutineEnvelope = 1f;

    /// <summary>Trail half-life in seconds at the bottom of the Routine envelope.</summary>
    private const float TrailHalfLifeMin = 0.03f;

    /// <summary>Trail half-life in seconds at the top of the Routine envelope.</summary>
    private const float TrailHalfLifeMax = 0.25f;

    /// <summary>Maximum coordinated hue shift applied by the Routine.</summary>
    private const float RoutineHueShift = 0.2f;

    /// <summary>Share of authored saturation retained at the bottom of the Routine.</summary>
    private const float RoutineSaturationFloor = 0.7f;

    /// <summary>Share of authored value retained at the bottom of the Routine.</summary>
    private const float RoutineValueFloor = 0.7f;

    // Fill choreography

    /// <summary>The common Fill length used as the unboosted response baseline.</summary>
    private const float TypicalFillBeats = 4f;

    /// <summary>The shortest supported Fill duration used to cap short-window compensation.</summary>
    private const int MinimumFillBeats = 1;

    /// <summary>Maximum number of beats used to establish the Fill orbit before onset.</summary>
    private const int MaximumFillLeadBeats = 2;

    /// <summary>Base tangential velocity added when a typical Fill begins; shorter Fills receive a duration boost.</summary>
    private const float FillOnsetImpulse = 6f;

    /// <summary>Strength of continuous Fill steering around wall center.</summary>
    private const float FillOrbitSteering = 2f;

    /// <summary>Share of Fill orbit retained at the end of the Drop gather, creating a tightening spiral.</summary>
    private const float FillOrbitAtFullGather = 0.35f;

    /// <summary>Alignment lift at full Fill drive.</summary>
    private const float FillAlignmentLift = 0.35f;

    /// <summary>Separation lift that lets a Fill-only orbit widen.</summary>
    private const float FillSeparationLift = 0.5f;

    // Drop gathering and release

    /// <summary>Number of beats before a Drop during which the flock gathers at wall center.</summary>
    private const int DropRunwayBeats = 8;

    /// <summary>Strength of center-seeking steering at the end of the Drop runway.</summary>
    private const float DropGatherSteering = 3f;

    /// <summary>Share of ordinary separation removed at the end of the Drop runway.</summary>
    private const float DropGatherSeparationSuppression = 0.9f;

    /// <summary>Number of Drop beats during which the outward release remains dominant.</summary>
    private const int DropReleaseBeats = 2;

    /// <summary>Initial Drop burst speed relative to each boid's ordinary maximum speed.</summary>
    private const float DropBurstSpeedMultiplier = 1.6f;

    /// <summary>Share of pre-Drop velocity retained in the radial burst so a Fill can leave angular momentum.</summary>
    private const float DropVelocityCarry = 0.35f;

    /// <summary>Strength of outward steering during the short Drop release.</summary>
    private const float DropOutwardSteering = 1.5f;

    /// <summary>Maximum speed lift during the short Drop release.</summary>
    private const float DropSpeedLift = 0.75f;

    /// <summary>Share of cohesion removed at the start of the Drop release.</summary>
    private const float DropCohesionSuppression = 0.9f;

    /// <summary>Separation lift at the start of the Drop release.</summary>
    private const float DropSeparationLift = 1.25f;

    // Continuous wander

    /// <summary>Minimum share of a boid's steering force devoted to continuous wander.</summary>
    private const float WanderStrengthMin = 0.2f;

    /// <summary>Maximum share of a boid's steering force devoted to continuous wander.</summary>
    private const float WanderStrengthMax = 0.45f;

    /// <summary>Slowest wander phase rate selected for a Grid, in radians per second.</summary>
    private const float WanderTurnRateMin = 0.5f;

    /// <summary>Fastest wander phase rate selected for a Grid, in radians per second.</summary>
    private const float WanderTurnRateMax = 1.1f;

    /// <summary>Maximum heading offset used by a boid's continuous wander.</summary>
    private const float WanderHeadingRadians = 0.7853982f;

    /// <summary>Minimum per-boid multiplier that prevents wander phases from moving in lockstep.</summary>
    private const float WanderFrequencyMin = 0.8f;

    /// <summary>Maximum per-boid multiplier that prevents wander phases from moving in lockstep.</summary>
    private const float WanderFrequencyMax = 1.2f;

    /// <summary>Share of ordinary wander strength retained during complete quiet.</summary>
    private const float QuietWanderMultiplier = 0.15f;

    /// <summary>Share of ordinary wander turn rate retained during complete quiet.</summary>
    private const float QuietWanderTurnMultiplier = 0.5f;

    /// <summary>Additional wander strength at full spectral agitation.</summary>
    private const float SpectralWanderStrengthLift = 1.5f;

    /// <summary>Additional wander turn rate at full spectral agitation.</summary>
    private const float SpectralWanderTurnLift = 1f;

    // Activation and per-frame state

    /// <summary>The boids projected onto the Penrose wall.</summary>
    private Boid[] flock;

    /// <summary>Four-bar trail and color choreography selected from the current track Energy.</summary>
    private Routine routine;

    /// <summary>Current Grid's gentle continuous wander force.</summary>
    private float wanderStrength = (WanderStrengthMin + WanderStrengthMax) * 0.5f;

    /// <summary>Current Grid's wander phase rate.</summary>
    private float wanderTurnRate = (WanderTurnRateMin + WanderTurnRateMax) * 0.5f;

    /// <summary>Low-dominant quiet-to-active posture derived from smoothed levels.</summary>
    private float movementActivity;

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

    /// <summary>Smooth zero-to-one progress through the eight-beat pre-Drop gather.</summary>
    private float dropGather;

    /// <summary>Short one-to-zero release envelope after the Drop begins.</summary>
    private float dropRelease;

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
        $"ACT {movementActivity:0.00} MID {midManeuver:0.00} DETAIL {spectralAgitation:0.00}\n" +
        $"ROUTINE {routineEnvelope:0.00} WANDER {wanderStrength:0.00}/{wanderTurnRate:0.00}\n" +
        $"FILL {fillOrbitDrive:0.00}\nGATHER {dropGather:0.00}\nDROP {dropRelease:0.00}";

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
    /// two Mid, and one Low. Missing Energy uses the Mid recipe so Standalone starts from balanced
    /// artistic state even though its live envelope later uses an explicit steady fallback.
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
        wanderStrength = Random.Range(WanderStrengthMin, WanderStrengthMax);
        wanderTurnRate = Random.Range(WanderTurnRateMin, WanderTurnRateMax);
    }

    /// <summary>Randomly chooses one of the two directions around the wall center.</summary>
    /// <returns>Negative one for clockwise motion or positive one for counterclockwise motion.</returns>
    private static float RollDirection() => Random.value < 0.5f ? -1f : 1f;

    /// <summary>Resets values that are sampled or edge-detected independently for each activation.</summary>
    private void ResetMusicalState()
    {
        movementActivity = 0f;
        midManeuver = 0f;
        spectralAgitation = 0f;
        routineEnvelope = 0f;
        fillOrbitDrive = 0f;
        dropGather = 0f;
        dropRelease = 0f;

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
        for (int i = 0; i < BoidCount; i++)
        {
            flock[i] = new Boid(min, max, this)
            {
                boids = flock,
            };
        }
    }

    // Per-frame musical interpretation and rendering

    /// <summary>Reads each musical source once and assigns it only to the visual behavior it was chosen to control.</summary>
    private void ReadMusicalInputs()
    {
        routineEnvelope = GetRoutineEnvelope(routine.Envelope, beatManager.IsSynced);

        LevelBands smoothed = beatManager.Levels.Smoothed;
        LevelBands normalized = beatManager.Levels.Normalized;
        movementActivity = GetMovementActivity(
            smoothed.Low,
            smoothed.Mid,
            smoothed.High,
            beatManager.IsSynced);

        midManeuver = GetMidManeuver(normalized.Mid, movementActivity);
        spectralAgitation = GetSpectralAgitation(normalized.Centroid, movementActivity);
    }

    /// <summary>Updates duration-aware Fill motion, the eight-beat Drop gather, and local onset impulses.</summary>
    private void UpdateMusicalMotion()
    {
        UpdateFillMotion();
        UpdateDropMotion();
    }

    /// <summary>Builds and sustains Fill orbit motion, then adds one tangential impulse at Fill onset.</summary>
    private void UpdateFillMotion()
    {
        var fill = beatManager.Fill;
        bool fillActive = fill.Active;
        float durationBoost = GetFillDurationBoost(fill.LengthBeats);
        fillOrbitDrive = fillActive
            ? durationBoost
            : fill.Before.Build(GetFillLeadBeats(fill.LengthBeats)) * durationBoost;

        if (fillActive && !previousFillActive)
        {
            float impulse = FillOnsetImpulse * GetFillDurationBoost(fill.LengthBeats);
            for (int i = 0; i < flock.Length; i++)
            {
                flock[i].ApplyTangentialImpulse(wallCenter, fillOrbitDirection, impulse);
            }
        }

        previousFillActive = fillActive;
    }

    /// <summary>Builds pre-Drop gathering, applies the onset burst, and samples the short release envelope.</summary>
    private void UpdateDropMotion()
    {
        var drop = beatManager.Drop;
        bool dropActive = drop.Active;
        dropGather = drop.Before.Build(DropRunwayBeats);

        if (dropActive && !previousDropActive)
        {
            for (int i = 0; i < flock.Length; i++)
            {
                Boid boid = flock[i];
                boid.ApplyRadialImpulse(wallCenter, boid.maxSpeed * DropBurstSpeedMultiplier, DropVelocityCarry);
            }
        }

        previousDropActive = dropActive;
        dropRelease = drop.In.Decay(DropReleaseBeats);
    }

    /// <summary>Fades the previous frame by Routine half-life, or clears it when trails are disabled.</summary>
    private void PrepareFrameBuffer()
    {
        if (TrailsEnabled)
        {
            buffer.Fade(GetTrailRetention(routineEnvelope, effectDelta));
        }
        else
        {
            buffer.Clear();
        }
    }

    /// <summary>Advances boids sequentially and projects their live palette colors onto nearest Penrose tiles.</summary>
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
            buffer[penrose.GetIndexFromPosition(boid.position)] =
                ApplyRoutineColor(paletteColor, routineEnvelope);
        }
    }

    // Pure musical and geometry mappings

    /// <summary>Returns low-dominant broad activity, or an active default when musical levels are unavailable.</summary>
    /// <param name="low">Smoothed Low-band level in <c>[0..1]</c>.</param>
    /// <param name="mid">Smoothed Mid-band level in <c>[0..1]</c>.</param>
    /// <param name="high">Smoothed High-band level in <c>[0..1]</c>.</param>
    /// <param name="isSynced">Whether live musical placement and levels are available.</param>
    /// <returns>Calibrated quiet-to-active movement in <c>[0..1]</c>.</returns>
    public static float GetMovementActivity(float low, float mid, float high, bool isSynced)
    {
        if (!isSynced)
        {
            return StandaloneMovementActivity;
        }

        float weighted = (Mathf.Clamp01(low) * MovementLowWeight)
            + (Mathf.Clamp01(mid) * MovementMidWeight)
            + (Mathf.Clamp01(high) * MovementHighWeight);
        return weighted.Remap(QuietActivityThreshold, ActiveActivityThreshold, 0f, 1f, clamp: true);
    }

    /// <summary>Returns the live Routine envelope, or its steady visual fallback in Standalone.</summary>
    /// <param name="envelope">Current Routine envelope in <c>[0..1]</c>.</param>
    /// <param name="isSynced">Whether a musical Grid is available to place the Routine.</param>
    /// <returns>The clamped live envelope, or the authored Standalone posture.</returns>
    public static float GetRoutineEnvelope(float envelope, bool isSynced)
    {
        return isSynced ? Mathf.Clamp01(envelope) : StandaloneRoutineEnvelope;
    }

    /// <summary>Maps broad activity to the flock's quiet-to-active ordinary speed limit.</summary>
    /// <param name="activity">Calibrated broad movement activity.</param>
    /// <returns>Multiplier applied to each boid's authored maximum speed.</returns>
    public static float GetMovementSpeedMultiplier(float activity)
    {
        return Mathf.Clamp01(activity).Lerp(QuietSpeedMultiplier, ActiveSpeedMultiplier);
    }

    /// <summary>Returns an activity-gated schooling maneuver from normalized Mid.</summary>
    /// <param name="normalizedMid">Normalized Mid-band level.</param>
    /// <param name="activity">Broad activity gate that prevents maneuvering during silence.</param>
    /// <returns>Schooling maneuver strength in <c>[0..1]</c>.</returns>
    public static float GetMidManeuver(float normalizedMid, float activity)
    {
        float maneuver = normalizedMid.Remap(MidManeuverThreshold, MidManeuverFull, 0f, 1f, clamp: true);
        return maneuver * Mathf.Clamp01(activity);
    }

    /// <summary>Returns activity-gated separation and wander drive from normalized spectral centroid.</summary>
    /// <param name="normalizedCentroid">Normalized spectral centroid.</param>
    /// <param name="activity">Broad activity gate that prevents agitation during silence.</param>
    /// <returns>Spectral agitation in <c>[0..1]</c>.</returns>
    public static float GetSpectralAgitation(float normalizedCentroid, float activity)
    {
        float agitation = normalizedCentroid.Remap(
            SpectralCentroidThreshold,
            SpectralCentroidFull,
            0f,
            1f,
            clamp: true);
        return agitation * Mathf.Clamp01(activity);
    }

    /// <summary>Maps the Routine envelope to frame-rate-independent trail retention for one elapsed step.</summary>
    /// <param name="envelope">Routine envelope controlling the trail half-life.</param>
    /// <param name="deltaTime">Elapsed seconds represented by this fade step.</param>
    /// <returns>Per-step buffer retention in <c>[0..1]</c>.</returns>
    public static float GetTrailRetention(float envelope, float deltaTime)
    {
        float halfLife = Mathf.Clamp01(envelope).Lerp(TrailHalfLifeMin, TrailHalfLifeMax);

        // Exponential decay gives an identical visual lifetime across different frame rates.
        return Mathf.Pow(0.5f, Mathf.Max(0f, deltaTime) / halfLife);
    }

    /// <summary>Applies the Routine's coordinated hue, saturation, and value treatment to one palette color.</summary>
    /// <param name="color">Live animated palette color.</param>
    /// <param name="envelope">Routine envelope controlling treatment strength.</param>
    /// <returns>The treated color with the original alpha preserved.</returns>
    public static Color ApplyRoutineColor(Color color, float envelope)
    {
        float amount = Mathf.Clamp01(envelope);
        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        float shiftedHue = (hue + (RoutineHueShift * amount)) % 1f;
        float shiftedSaturation = saturation * Mathf.Lerp(RoutineSaturationFloor, 1f, amount);
        float shiftedValue = value * Mathf.Lerp(RoutineValueFloor, 1f, amount);
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

    /// <summary>Returns the shared choreography and onset boost for Fill windows shorter than four beats.</summary>
    /// <param name="lengthBeats">Known Fill length, or <see langword="null"/> when unavailable.</param>
    /// <returns>Duration compensation: 1 for four or more beats, up to 2 for one beat.</returns>
    public static float GetFillDurationBoost(int? lengthBeats)
    {
        float duration = lengthBeats is > 0
            ? Mathf.Clamp(lengthBeats.Value, MinimumFillBeats, TypicalFillBeats)
            : TypicalFillBeats;
        return Mathf.Sqrt(TypicalFillBeats / duration);
    }

    /// <summary>
    /// Returns how many beats of runway this Fill gets, shortening the orbit lead for short Fills.
    /// </summary>
    /// <param name="lengthBeats">Known Fill length, or <see langword="null"/> when unavailable.</param>
    /// <returns>Runway length in whole beats, between one and <see cref="MaximumFillLeadBeats"/>.</returns>
    /// <remarks>
    /// This is Flock's own choice of window, not envelope math: the shape itself comes from the
    /// Fill handle's Before span.
    /// </remarks>
    private static int GetFillLeadBeats(int? lengthBeats) =>
        lengthBeats is > 0
            ? Mathf.Clamp(lengthBeats.Value, MinimumFillBeats, MaximumFillLeadBeats)
            : MaximumFillLeadBeats;

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
        public Boid(Vector2 min, Vector2 max, Flock parent)
        {
            this.min = min;
            this.max = max;
            this.parent = parent;
            velocity = new Vector2(Random.Range(-maxSpeed, maxSpeed), Random.Range(-maxSpeed, maxSpeed));
            position = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
            wanderPhase = Random.Range(0f, Mathf.PI * 2f);
            wanderFrequency = Random.Range(WanderFrequencyMin, WanderFrequencyMax);
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

        /// <summary>Returns the ordinary speed limit lifted as needed by Fill, gathering, or Drop release.</summary>
        /// <returns>Maximum velocity magnitude for the current frame.</returns>
        private float GetActiveSpeedLimit()
        {
            float ordinarySpeedLimit = maxSpeed * GetMovementSpeedMultiplier(parent.movementActivity);
            float eventDrive = Mathf.Clamp01(Mathf.Max(parent.fillOrbitDrive, parent.dropGather));
            float speedLimit = Mathf.Lerp(
                ordinarySpeedLimit,
                Mathf.Max(ordinarySpeedLimit, maxSpeed),
                eventDrive);
            return Mathf.Lerp(speedLimit, maxSpeed * (1f + DropSpeedLift), parent.dropRelease);
        }

        /// <summary>Combines classic flocking forces after applying live musical weight changes.</summary>
        /// <returns>Weighted alignment, cohesion, and separation acceleration.</returns>
        private Vector2 GetWeightedFlockingAcceleration()
        {
            float fillSpread = parent.fillOrbitDrive * (1f - parent.dropGather);
            float alignmentWeight = BaseAlignmentWeight
                * (1f + (FillAlignmentLift * parent.fillOrbitDrive) + (MidAlignmentLift * parent.midManeuver));
            float cohesionWeight = BaseCohesionWeight
                * (1f + (MidCohesionLift * parent.midManeuver))
                * (1f - (DropCohesionSuppression * parent.dropRelease));
            float separationWeight = BaseSeparationWeight
                * (1f + (FillSeparationLift * fillSpread)
                    + (SpectralSeparationLift * parent.spectralAgitation)
                    + (DropSeparationLift * parent.dropRelease))
                * (1f - (DropGatherSeparationSuppression * parent.dropGather));

            return (alignmentSteering * alignmentWeight)
                + (cohesionSteering * cohesionWeight)
                + (separationSteering * separationWeight);
        }

        /// <summary>Adds collective turning, independent wander, Fill orbit, Drop gathering, and Drop release steering.</summary>
        /// <param name="deltaTime">Elapsed effect time in seconds.</param>
        /// <param name="speedLimit">Current desired speed used by steering forces.</param>
        private void AddMusicalSteering(float deltaTime, float speedLimit)
        {
            float dropDominance = Mathf.Max(parent.dropGather, parent.dropRelease);
            float eventDominance = Mathf.Clamp01(Mathf.Max(parent.fillOrbitDrive, dropDominance));

            // Mid bends the shared course only while Fill/Drop choreography is not dominant.
            Vector2 collectiveTurn = GetCollectiveTurnDirection(velocity, parent.courseTurnDirection);
            acceleration += Steer(collectiveTurn, speedLimit)
                * (CollectiveTurnStrength * parent.midManeuver * (1f - eventDominance));

            // Every boid wanders independently; Drop choreography suppresses that local disagreement.
            float activeWanderStrength = parent.wanderStrength
                * Mathf.Lerp(QuietWanderMultiplier, 1f, parent.movementActivity)
                * (1f + (SpectralWanderStrengthLift * parent.spectralAgitation));
            float activeWanderTurnRate = parent.wanderTurnRate
                * Mathf.Lerp(QuietWanderTurnMultiplier, 1f, parent.movementActivity)
                * (1f + (SpectralWanderTurnLift * parent.spectralAgitation));
            acceleration += UpdateWander(
                    deltaTime,
                    speedLimit,
                    activeWanderStrength,
                    activeWanderTurnRate)
                * (1f - dropDominance);

            // Fill circles the center, gathering bends that circle into a spiral, and Drop reverses it outward.
            Vector2 radial = position - parent.wallCenter;
            Vector2 tangent = new(-radial.y, radial.x);
            float orbitAmount = parent.fillOrbitDrive * Mathf.Lerp(
                1f,
                FillOrbitAtFullGather,
                parent.dropGather);
            acceleration += Steer(tangent * parent.fillOrbitDirection, speedLimit)
                * (FillOrbitSteering * orbitAmount);
            acceleration += Steer(-radial, speedLimit) * (DropGatherSteering * parent.dropGather);
            acceleration += Steer(radial, speedLimit) * (DropOutwardSteering * parent.dropRelease);
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
            float headingOffset = Mathf.Sin(wanderPhase) * WanderHeadingRadians;
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
