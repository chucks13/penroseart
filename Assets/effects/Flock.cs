using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Runs a small boid simulation and projects boid positions onto nearby Penrose tiles.
/// </summary>
public class Flock : EffectBase
{
    /// <summary>
    /// The boids react to Fill and Drop structure as well as live beat and Energy, so the Director can prefer
    /// the flock for those musical moments while retaining its Mid/High-energy casting range.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Number of boids simulated by the effect.</summary>
    private const int BoidCount = 80;

    /// <summary>Master switch for Routine-controlled trails; false renders only current boid positions.</summary>
    private static readonly bool TrailsEnabled = true;

    /// <summary>The boids projected onto the Penrose wall.</summary>
    private Boid[] flock;

    /// <summary>Four-bar trail and color choreography selected from the current track Energy.</summary>
    private Routine routine;

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

    /// <summary>Standalone Routine posture that preserves full palette color and visible trails without a Grid.</summary>
    private const float StandaloneRoutineEnvelope = 1f;

    /// <summary>Normalized Mid level below which no schooling maneuver is added.</summary>
    private const float MidManeuverThreshold = 0.2f;

    /// <summary>Normalized Mid level that produces the full schooling maneuver.</summary>
    private const float MidManeuverFull = 0.65f;

    /// <summary>Normalized spectral centroid below which no extra agitation is added.</summary>
    private const float SpectralCentroidThreshold = 0.25f;

    /// <summary>Normalized spectral centroid that produces full extra agitation.</summary>
    private const float SpectralCentroidFull = 0.65f;

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

    /// <summary>The common Fill length used as the unboosted response baseline.</summary>
    private const float TypicalFillBeats = 4f;

    /// <summary>The shortest supported Fill duration used to cap short-window compensation.</summary>
    private const float MinimumFillBeats = 1f;

    /// <summary>Maximum number of beats used to establish the Fill orbit before onset.</summary>
    private const float MaximumFillLeadBeats = 2f;

    /// <summary>Number of beats before a Drop during which the flock gathers at wall center.</summary>
    private const float DropRunwayBeats = 8f;

    /// <summary>Base tangential velocity added when a typical Fill begins; shorter Fills receive a duration boost.</summary>
    private const float FillOnsetImpulse = 6f;

    /// <summary>Strength of continuous Fill steering around wall center.</summary>
    private const float FillOrbitSteering = 2f;

    /// <summary>Share of Fill orbit retained at the end of the Drop gather, creating a tightening spiral.</summary>
    private const float FillOrbitAtFullGather = 0.35f;

    /// <summary>Alignment lift at full Fill drive.</summary>
    private const float FillAlignmentLift = 0.35f;

    /// <summary>Alignment lift on a strong normalized Mid-band maneuver.</summary>
    private const float MidAlignmentLift = 0.75f;

    /// <summary>Cohesion lift on a strong normalized Mid-band maneuver.</summary>
    private const float MidCohesionLift = 0.5f;

    /// <summary>Shared course-bending force applied by a strong normalized Mid-band maneuver.</summary>
    private const float CollectiveTurnStrength = 0.75f;

    /// <summary>Separation lift that lets a Fill-only orbit widen.</summary>
    private const float FillSeparationLift = 0.5f;

    /// <summary>Separation lift when the normalized spectrum leans toward high-frequency detail.</summary>
    private const float SpectralSeparationLift = 1f;

    /// <summary>Strength of center-seeking steering at the end of the Drop runway.</summary>
    private const float DropGatherSteering = 3f;

    /// <summary>Share of ordinary separation removed at the end of the Drop runway.</summary>
    private const float DropGatherSeparationSuppression = 0.9f;

    /// <summary>Number of Drop beats during which the outward release remains dominant.</summary>
    private const float DropReleaseBeats = 2f;

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

    private float alignment = 0.75f;
    private float cohesion = 1f;
    private float separation = 1.25f;

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
    private Vector2 center;

    /// <summary>Clockwise or counter-clockwise direction selected for the current Grid.</summary>
    private float orbitDirection;

    /// <summary>Alternating left or right collective course bend used for the current Grid.</summary>
    private float courseTurnDirection;

    /// <summary>Duration-aware active Fill drive, allowed above one for very short Fills.</summary>
    private float fillDrive;

    /// <summary>Smooth zero-to-one progress through the four-beat pre-Drop gather.</summary>
    private float dropApproach;

    /// <summary>Short one-to-zero release envelope after the Drop begins.</summary>
    private float dropRelease;

    /// <summary>Previous Fill state retained locally to identify its onset.</summary>
    private bool previousFillActive;

    /// <summary>Previous Drop state retained locally to identify its onset.</summary>
    private bool previousDropActive;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() =>
        $"ACT {movementActivity:0.00} MID {midManeuver:0.00} DETAIL {spectralAgitation:0.00}\n" +
        $"ROUTINE {routineEnvelope:0.00} WANDER {wanderStrength:0.00}/{wanderTurnRate:0.00}\n" +
        $"FILL {fillDrive:0.00}\nGATHER {dropApproach:0.00}\nDROP {dropRelease:0.00}";

    /// <summary>Returns low-dominant broad activity, or an active default when musical levels are unavailable.</summary>
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
    public static float GetRoutineEnvelope(float envelope, bool isSynced)
    {
        return isSynced ? Mathf.Clamp01(envelope) : StandaloneRoutineEnvelope;
    }

    /// <summary>Maps broad activity to the flock's quiet-to-active ordinary speed limit.</summary>
    public static float GetMovementSpeedMultiplier(float activity)
    {
        return Mathf.Clamp01(activity).Lerp(QuietSpeedMultiplier, ActiveSpeedMultiplier);
    }

    /// <summary>Returns an activity-gated schooling maneuver from normalized Mid.</summary>
    public static float GetMidManeuver(float normalizedMid, float activity)
    {
        float maneuver = normalizedMid.Remap(MidManeuverThreshold, MidManeuverFull, 0f, 1f, clamp: true);
        return maneuver * Mathf.Clamp01(activity);
    }

    /// <summary>Returns activity-gated separation and wander drive from normalized spectral centroid.</summary>
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
    public static float GetTrailRetention(float envelope, float deltaTime)
    {
        float halfLife = Mathf.Clamp01(envelope).Lerp(TrailHalfLifeMin, TrailHalfLifeMax);
        return Mathf.Pow(0.5f, Mathf.Max(0f, deltaTime) / halfLife);
    }

    /// <summary>Applies the Routine's coordinated hue, saturation, and value treatment to one palette color.</summary>
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
    public static Vector2 GetCollectiveTurnDirection(Vector2 velocity, float turnDirection)
    {
        float signedQuarterTurn = turnDirection < 0f ? -Mathf.PI * 0.5f : Mathf.PI * 0.5f;
        return GetWanderDirection(velocity, signedQuarterTurn);
    }

    /// <summary>Returns the shortest offset from one point to another across wrapped rectangular bounds.</summary>
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
    public static float GetFillDurationBoost(int? lengthBeats)
    {
        float duration = lengthBeats is > 0
            ? Mathf.Clamp(lengthBeats.Value, MinimumFillBeats, TypicalFillBeats)
            : TypicalFillBeats;
        return Mathf.Sqrt(TypicalFillBeats / duration);
    }

    /// <summary>Builds the duration-aware orbit before Fill onset and holds it throughout the active Fill.</summary>
    public static float GetFillApproach(
        bool fillActive,
        int? beatsUntil,
        float? beatProgress,
        int? lengthBeats)
    {
        float durationBoost = GetFillDurationBoost(lengthBeats);
        if (fillActive)
        {
            return durationBoost;
        }

        float leadBeats = lengthBeats is > 0
            ? Mathf.Clamp(lengthBeats.Value, MinimumFillBeats, MaximumFillLeadBeats)
            : MaximumFillLeadBeats;
        if (beatsUntil is not { } beats || beats < 0 || beats > leadBeats)
        {
            return 0f;
        }

        float continuousBeatsUntil = beats - Mathf.Clamp01(beatProgress ?? 0f);
        float progress = 1f - Mathf.Clamp01(continuousBeatsUntil / leadBeats);
        return Mathf.SmoothStep(0f, 1f, progress) * durationBoost;
    }

    /// <summary>
    /// Returns a smooth zero-to-one gathering amount across the final eight beats before an upcoming Drop.
    /// </summary>
    public static float GetDropApproach(bool dropActive, int? beatsUntil, float? beatProgress)
    {
        if (dropActive || beatsUntil is not { } beats || beats < 0 || beats > DropRunwayBeats)
        {
            return 0f;
        }

        float continuousBeatsUntil = beats - Mathf.Clamp01(beatProgress ?? 0f);
        float progress = 1f - Mathf.Clamp01(continuousBeatsUntil / DropRunwayBeats);
        return Mathf.SmoothStep(0f, 1f, progress);
    }


    /// <summary>
    /// Acquires this activation's energy-weighted Routine and initializes the flock's artistic state.
    /// </summary>
    public override void OnStart()
    {
        RerollRoutine();
        RerollWander();
        orbitDirection = Random.value < 0.5f ? -1f : 1f;
        courseTurnDirection = Random.value < 0.5f ? -1f : 1f;

        var min = penrose.Bounds.min;
        var max = penrose.Bounds.max;
        center = penrose.Bounds.center;

        alignment = 0.75f;
        cohesion = 1f;
        separation = 1.25f;
        movementActivity = 0f;
        midManeuver = 0f;
        spectralAgitation = 0f;
        routineEnvelope = 0f;
        fillDrive = 0f;
        dropApproach = 0f;
        dropRelease = 0f;
        previousFillActive = beatManager.Fill.Active == true;
        previousDropActive = beatManager.Drop.Active == true;

        flock = new Boid[BoidCount];
        for (int i = 0; i < BoidCount; i++)
        {
            flock[i] = new Boid(min, max, this)
            {
                boids = flock,
            };
        }

        buffer.Clear();
    }

    /// <summary>
    /// Composes and shuffles one Grid's Routine from the current track Energy. Missing Energy uses the
    /// Mid recipe so Standalone keeps a balanced choreography.
    /// </summary>
    private void RerollRoutine()
    {
        Energy energy = beatManager.Energy.Level ?? Energy.Mid;
        Energy[] levels = energy switch
        {
            Energy.Low => new[] { Energy.Low, Energy.Low, Energy.Low, Energy.Mid },
            Energy.Mid => new[] { Energy.Mid, Energy.Mid, Energy.Mid, Energy.Low },
            Energy.High => new[] { Energy.High, Energy.Mid, Energy.Mid, Energy.Low },
            _ => throw new System.ArgumentOutOfRangeException(nameof(energy), energy, "Unsupported Energy level."),
        };

        for (int i = levels.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (levels[i], levels[swapIndex]) = (levels[swapIndex], levels[i]);
        }

        routine = Routine.Of(
            waveforms.Random(levels[0]),
            waveforms.Random(levels[1]),
            waveforms.Random(levels[2]),
            waveforms.Random(levels[3]));
    }

    /// <summary>Selects the gentle wander strength and turn rate used for the current Grid.</summary>
    private void RerollWander()
    {
        wanderStrength = Random.Range(WanderStrengthMin, WanderStrengthMax);
        wanderTurnRate = Random.Range(WanderTurnRateMin, WanderTurnRateMax);
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>Acquires fresh Grid behavior and reverses the collective course bend to prevent long straight runs.</summary>
    protected override void OnNewGrid()
    {
        RerollRoutine();
        RerollWander();
        orbitDirection = Random.value < 0.5f ? -1f : 1f;
        courseTurnDirection = courseTurnDirection < 0f ? 1f : -1f;
    }

    /// <summary>
    /// Updates duration-aware Fill motion, the eight-beat Drop gather, and consumer-local onset impulses.
    /// </summary>
    private void UpdateMusicalMotion()
    {
        var fill = beatManager.Fill;
        bool fillActive = fill.Active == true;
        fillDrive = GetFillApproach(
            fillActive,
            fill.BeatsUntil,
            beatManager.Timing.BeatProgress,
            fill.LengthBeats);
        if (fillActive && !previousFillActive)
        {
            float impulse = FillOnsetImpulse * GetFillDurationBoost(fill.LengthBeats);
            for (int i = 0; i < flock.Length; i++)
            {
                flock[i].ApplyTangentialImpulse(center, orbitDirection, impulse);
            }
        }
        previousFillActive = fillActive;

        var drop = beatManager.Drop;
        bool dropActive = drop.Active == true;
        dropApproach = GetDropApproach(dropActive, drop.BeatsUntil, beatManager.Timing.BeatProgress);
        if (dropActive && !previousDropActive)
        {
            for (int i = 0; i < flock.Length; i++)
            {
                Boid boid = flock[i];
                boid.ApplyRadialImpulse(center, boid.maxSpeed * DropBurstSpeedMultiplier, DropVelocityCarry);
            }
        }
        previousDropActive = dropActive;
        dropRelease = drop.Decay(DropReleaseBeats);
    }

    /// <summary>
    /// Renders one frame using level-driven motion, Routine-driven visuals, and Fill/Drop choreography.
    /// </summary>
    public override void Draw()
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
        UpdateMusicalMotion();
        if (TrailsEnabled)
        {
            buffer.Fade(GetTrailRetention(routineEnvelope, effectDelta));
        }
        else
        {
            buffer.Clear();
        }

        for (int i = 0; i < flock.Length; i++)
        {
            Boid boid = flock[i];
            boid.Update(effectDelta);
            Color paletteColor = APalette.read((float)i / BoidCount, true);
            buffer[penrose.GetIndexFromPosition(boid.position)] = ApplyRoutineColor(paletteColor, routineEnvelope);
        }
    }

    /// <summary>
    /// Small moving particle used by Flock before projection onto the nearest Penrose tile.
    /// </summary>
    public class Boid
    {
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 acceleration;
        public float perception = 3f;
        public float maxSpeed = 20f;
        public float maxForce = 0.25f;
        public Boid[] boids;
        public Flock parent;
        private Vector2 min;
        private Vector2 max;
        private Vector2 alignmentVec;
        private Vector2 cohesionVec;
        private Vector2 separationVec;

        /// <summary>Current position within this boid's independent wander cycle.</summary>
        private float wanderPhase;

        /// <summary>Per-boid phase multiplier that keeps the flock from steering in lockstep.</summary>
        private readonly float wanderFrequency;

        /// <summary>
        /// Creates one boid within bounds and assigns initial velocity.
        /// </summary>
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

        /// <summary>Launches the boid away from a center point while retaining the requested share of its momentum.</summary>
        public void ApplyRadialImpulse(Vector2 center, float magnitude, float velocityCarry)
        {
            Vector2 radial = position - center;
            if (radial.sqrMagnitude <= Mathf.Epsilon)
            {
                radial = velocity.sqrMagnitude > Mathf.Epsilon ? velocity : Vector2.right;
            }

            velocity = (velocity * Mathf.Clamp01(velocityCarry)) + (radial.normalized * magnitude);
        }

        /// <summary>
        /// Advances boid position, flock steering, and edge wrapping for one frame.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (boids == null) return;

            position += deltaTime * velocity;

            float ordinarySpeedLimit = maxSpeed * GetMovementSpeedMultiplier(parent.movementActivity);
            float eventDrive = Mathf.Clamp01(Mathf.Max(parent.fillDrive, parent.dropApproach));
            float speedLimit = Mathf.Lerp(
                ordinarySpeedLimit,
                Mathf.Max(ordinarySpeedLimit, maxSpeed),
                eventDrive);
            speedLimit = Mathf.Lerp(speedLimit, maxSpeed * (1f + DropSpeedLift), parent.dropRelease);
            UpdateFlock(speedLimit);

            float fillSpread = parent.fillDrive * (1f - parent.dropApproach);
            float alignmentWeight = parent.alignment
                * (1f + (FillAlignmentLift * parent.fillDrive) + (MidAlignmentLift * parent.midManeuver));
            float cohesionWeight = parent.cohesion
                * (1f + (MidCohesionLift * parent.midManeuver))
                * (1f - (DropCohesionSuppression * parent.dropRelease));
            float separationWeight = parent.separation
                * (1f + (FillSeparationLift * fillSpread) +
                    (SpectralSeparationLift * parent.spectralAgitation) +
                    (DropSeparationLift * parent.dropRelease))
                * (1f - (DropGatherSeparationSuppression * parent.dropApproach));

            acceleration = (alignmentVec * alignmentWeight) + (cohesionVec * cohesionWeight) +
                           (separationVec * separationWeight);

            float dropDominance = Mathf.Max(parent.dropApproach, parent.dropRelease);
            float eventDominance = Mathf.Clamp01(Mathf.Max(parent.fillDrive, dropDominance));
            Vector2 collectiveTurn = GetCollectiveTurnDirection(velocity, parent.courseTurnDirection);
            acceleration += Steer(collectiveTurn, speedLimit)
                * (CollectiveTurnStrength * parent.midManeuver * (1f - eventDominance));

            float wanderStrength = parent.wanderStrength
                * Mathf.Lerp(QuietWanderMultiplier, 1f, parent.movementActivity)
                * (1f + (SpectralWanderStrengthLift * parent.spectralAgitation));
            float wanderTurnRate = parent.wanderTurnRate
                * Mathf.Lerp(QuietWanderTurnMultiplier, 1f, parent.movementActivity)
                * (1f + (SpectralWanderTurnLift * parent.spectralAgitation));
            acceleration += UpdateWander(deltaTime, speedLimit, wanderStrength, wanderTurnRate)
                * (1f - dropDominance);

            Vector2 radial = position - parent.center;
            Vector2 tangent = new(-radial.y, radial.x);
            float orbitAmount = parent.fillDrive * Mathf.Lerp(
                1f,
                FillOrbitAtFullGather,
                parent.dropApproach);
            acceleration += Steer(tangent * parent.orbitDirection, speedLimit)
                * (FillOrbitSteering * orbitAmount);
            acceleration += Steer(-radial, speedLimit) * (DropGatherSteering * parent.dropApproach);
            acceleration += Steer(radial, speedLimit) * (DropOutwardSteering * parent.dropRelease);

            velocity += acceleration;
            velocity = Vector2.ClampMagnitude(velocity, speedLimit);

            CheckEdges();
        }

        /// <summary>Advances this boid's independent wander phase and returns its gentle steering force.</summary>
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
        private Vector2 Steer(Vector2 direction, float desiredSpeed)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            Vector2 desired = direction.normalized * desiredSpeed;
            return Vector2.ClampMagnitude(desired - velocity, maxForce);
        }

        /// <summary>
        /// Wraps boids around Penrose bounds when they leave the visible area.
        /// </summary>
        private void CheckEdges()
        {
            if (position.x > max.x)
                position.x = min.x;
            else if (position.x < min.x) position.x = max.x;

            if (position.y > max.y)
                position.y = min.y;
            else if (position.y < min.y) position.y = max.y;
        }

        /// <summary>
        /// Computes alignment, cohesion, and separation steering from nearby boids.
        /// </summary>
        private void UpdateFlock(float desiredSpeed)
        {
            alignmentVec = Vector2.zero;
            cohesionVec = Vector2.zero;
            separationVec = Vector2.zero;
            int total = 0;
            Vector2 boundsSize = max - min;
            for (int i = 0; i < boids.Length; i++)
            {
                if (boids[i] == this) continue;

                Vector2 offset = GetWrappedOffset(position, boids[i].position, boundsSize);
                float squaredDistance = offset.sqrMagnitude;
                if (squaredDistance > perception * perception) continue;

                alignmentVec += boids[i].velocity;
                cohesionVec += offset;

                if (squaredDistance > Mathf.Epsilon)
                {
                    separationVec -= offset / squaredDistance;
                }

                total++;
            }

            if (total > 0)
            {
                alignmentVec /= total;
                alignmentVec = alignmentVec.SetMagnitude(desiredSpeed);
                alignmentVec -= velocity;
                alignmentVec = Vector2.ClampMagnitude(alignmentVec, maxForce);

                cohesionVec /= total;
                cohesionVec = cohesionVec.SetMagnitude(desiredSpeed);
                cohesionVec -= velocity;
                cohesionVec = Vector2.ClampMagnitude(cohesionVec, maxForce);

                separationVec /= total;
                separationVec = separationVec.SetMagnitude(desiredSpeed);
                separationVec -= velocity;
                separationVec = Vector2.ClampMagnitude(separationVec, maxForce);
            }
        }
    }
}
