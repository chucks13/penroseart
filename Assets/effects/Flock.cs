using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Runs a small boid simulation and projects boid positions onto nearby Penrose tiles.
/// </summary>
public class Flock : EffectBase
{
    /// <summary>
    /// The boids' speed lifts with the beat and their motion reads live band energy, so the flock advertises
    /// as a Mid/High-energy Performer the Director can cast when the track is driving.
    /// </summary>
    public override Repertoire Repertoire => Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>The consumer-owned Waveform that seasons flock speed until the next Grid.</summary>
    private Waveform waveform;

    /// <summary>Number of boids simulated by the effect.</summary>
    private const int BoidCount = 80;

    /// <summary>The boids projected onto the Penrose wall.</summary>
    private Boid[] flock;
    private const float BaseSpeedMultiplier = 1f;
    private const float BeatSpeedLift = 2f;
    private const float LowEnergyHueShift = 0.2f;

    private float alignment = 0.75f;
    private float cohesion = 1f;
    private float separation = 1.25f;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => string.Empty;

    /// <summary>
    /// Maps an available Waveform envelope to flock movement speed, falling back to normal speed without a clock.
    /// </summary>
    public static float GetBeatSpeedMultiplier(float? envelope)
    {
        float shapedEnvelope = Mathf.Pow(Mathf.Clamp01(envelope ?? 0f), 1.5f);
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
    /// Acquires this activation's Waveform and initializes the flock's artistic state.
    /// </summary>
    public override void OnStart()
    {
        waveform = synth.Random();

        var min = penrose.Bounds.min;
        var max = penrose.Bounds.max;

        alignment = 0.75f;
        cohesion = 1f;
        separation = 1.25f;

        flock = new Boid[BoidCount];
        for (int i = 0; i < BoidCount; i++)
        {
            flock[i] = new Boid(min, max, this)
            {
                color = APalette.read((float)i / BoidCount, true),
                boids = flock,
            };
        }

        buffer.Clear();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>Acquires a fresh consumer-owned Waveform at each new 16-beat Grid.</summary>
    protected override void OnNewGrid() => waveform = synth.Random();

    /// <summary>
    /// Renders one frame using the live Waveform envelope and smoothed low-band level when available.
    /// </summary>
    public override void Draw()
    {
        float? envelope = synth.Evaluate(waveform);
        float lowEnergy = beatManager.Levels?.Smoothed.Low ?? 0f;
        float speedMultiplier = GetBeatSpeedMultiplier(envelope);
        buffer.Fade(0.925f);
        for (int i = 0; i < flock.Length; i++)
        {
            Boid boid = flock[i];
            boid.Update(effectDelta * speedMultiplier);
            buffer[penrose.GetIndexFromPosition(boid.position)] = ShiftHueByLowEnergy(boid.color, lowEnergy);
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
