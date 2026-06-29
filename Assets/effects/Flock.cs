using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Runs a small boid simulation and projects boid positions onto nearby Penrose tiles.
/// </summary>
public class Flock : EffectBase
{
    private Boid[] flock;
    private int total = 80;
    private const float BaseSpeedMultiplier = 1f;
    private const float BeatSpeedLift = 2f;
    private const float LowEnergyHueShift = 0.2f;

    private float alignment = 0.75f;
    private float cohesion = 1f;
    private float separation = 1.25f;
    private int lastPhaseCount;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() { return $""; }

    /// <summary>
    /// Maps the selected Waveform envelope to flock movement speed. Disabled beat movement stays at normal speed.
    /// </summary>
    public static float GetBeatSpeedMultiplier(float envelope, bool beatEnabled)
    {
        if (!beatEnabled) return BaseSpeedMultiplier;

        float shapedEnvelope = Mathf.Pow(Mathf.Clamp01(envelope), 1.5f);
        return BaseSpeedMultiplier + (shapedEnvelope * BeatSpeedLift);
    }


    /// <summary>
    /// Slightly bends a palette color's hue from the smoothed low-band energy level.
    /// </summary>
    public static Color ShiftHueByLowEnergy(Color color, float lowEnergy)
    {
        var amount = Mathf.Clamp01(lowEnergy);
        if (amount <= 0f)
        {
            return color;
        }

        Color.RGBToHSV(color, out var hue, out var saturation, out var value);
        var shifted = Color.HSVToRGB((hue + (amount * LowEnergyHueShift)) % 1f, saturation, value);
        shifted.a = color.a;
        return shifted;
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
        lastPhaseCount = beatManager.Phase?.Count ?? 0;

        var min = penrose.Bounds.min;
        var max = penrose.Bounds.max;

        total = 80;
        alignment = 0.75f;
        cohesion = 1f;
        separation = 1.25f;

        flock = new Boid[total];
        for (int i = 0; i < total; i++)
        {
            Color bcolor;
            bcolor = APalette.read((float)i / total, true);
            flock[i] = new Boid(min, max, this)
            {
                color = bcolor,
                boids = flock,
            };
        }

        buffer.Clear();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Selects a new Waveform once at the one of each 16-beat On-Air Timing Phase, but only when the Phase
    /// lock is trusted (<see cref="PhaseLockState.Locked"/>) so a Coasting / Contradicted reading holds the
    /// current Waveform instead of re-rolling on a low-trust Phase.
    /// </summary>
    private void RerollWaveformOnPhaseOne()
    {
        var phase = beatManager.Phase;
        var phaseCount = phase?.Count ?? 0;
        if (phaseCount == 1 && lastPhaseCount != 1 && phase?.Confidence == PhaseLockState.Locked)
        {
            beatVariant = beatManager.GetRandomVariant();
        }

        lastPhaseCount = phaseCount;
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        RerollWaveformOnPhaseOne();

        var envelope = beatManager.Envelope(beatVariant) ?? 0f;
        var lowEnergy = beatManager.Levels?.low ?? 0f;
        float speedMultiplier = GetBeatSpeedMultiplier(envelope, beatEnable && beatManager.IsActive);
        buffer.Fade(0.925f);
        for (int i = 0; i < flock.Length; i++)
        {
            var f = flock[i];
            f.Update(effectDelta * speedMultiplier);
            buffer[controller.penrose.GetIndexFromPosition(f.position)] = ShiftHueByLowEnergy(f.color, lowEnergy);
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
        public Color color;
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
        }

        /// <summary>
        /// Advances boid position, flock steering, and edge wrapping for one frame.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (boids == null) return;

            position += deltaTime * velocity;

            UpdateFlock();
            acceleration = (alignmentVec * parent.alignment) + (cohesionVec * parent.cohesion) +
                           (separationVec * parent.separation);

            velocity += acceleration;
            velocity = Vector2.ClampMagnitude(velocity, maxSpeed);

            CheckEdges();
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
        /// Applies simple cohesion steering toward neighboring boids.
        /// </summary>
        private void UpdateFlock()
        {
            alignmentVec = Vector2.zero;
            cohesionVec = Vector2.zero;
            separationVec = Vector2.zero;
            int total = 0;
            for (int i = 0; i < boids.Length; i++)
            {
                var distance = Vector2.Distance(position, boids[i].position);
                if (boids[i] == this || distance > perception) continue;

                alignmentVec += boids[i].velocity;
                cohesionVec += boids[i].position;

                var diff = position - boids[i].position;
                diff /= distance * distance;
                separationVec += diff;

                total++;
            }

            if (total > 0)
            {
                alignmentVec /= total;
                alignmentVec = alignmentVec.SetMagnitude(maxSpeed);
                alignmentVec -= velocity;
                alignmentVec = Vector2.ClampMagnitude(alignmentVec, maxForce);

                cohesionVec /= total;
                cohesionVec -= position;
                cohesionVec = cohesionVec.SetMagnitude(maxSpeed);
                cohesionVec -= velocity;
                cohesionVec = Vector2.ClampMagnitude(cohesionVec, maxForce);

                separationVec /= total;
                separationVec = separationVec.SetMagnitude(maxSpeed);
                separationVec -= velocity;
                separationVec = Vector2.ClampMagnitude(separationVec, maxForce);
            }
        }
    }
}
