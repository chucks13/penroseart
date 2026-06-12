using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Runs a small boid simulation and projects boid positions onto nearby Penrose tiles.
/// </summary>
public class Flock : EffectBase
{
    private Boid[] flock;
    private int total = 80;
    private float alignment = 0.75f;
    private float cohesion = 1f;
    private float separation = 1.25f;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() { return $""; }

    /// <summary>
    /// Maps Rave beat pulse to flock movement speed. Disabled beat movement stays at normal speed.
    /// </summary>
    public static float GetBeatSpeedMultiplier(float beatPulse, bool beatEnabled)
    {
        if (!beatEnabled) return 1f;

        float shapedPulse = Mathf.Pow(Mathf.Clamp01(beatPulse), 1.5f);
        return Mathf.Lerp(0.25f, 3.0f, shapedPulse);
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
            bcolor = Color.HSVToRGB((float)i / total % 1f, 1f, 1f);
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
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        float speedMultiplier = GetBeatSpeedMultiplier(beatManager.Pulse ?? 0f, beatEnable && beatManager.IsActive);
        buffer.Fade(0.925f);
        for (int i = 0; i < flock.Length; i++)
        {
            var f = flock[i];
            f.Update(effectDelta * speedMultiplier);
            buffer[controller.penrose.GetIndexFromPosition(f.position)] = f.color;
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