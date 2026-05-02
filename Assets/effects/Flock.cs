﻿using Random = UnityEngine.Random;
using UnityEngine;

public class Flock : EffectBase
{
    private Boid[] flock;
    private int total = 80;
    private float alignment = 0.75f;
    private float cohesion = 1f;
    private float separation = 1.25f;

    public override string DebugText() { return $""; }

    public override void Init()
    {
        base.Init();
    }

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

    public override void OnEnd() { }

    public override void Draw()
    {
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        buffer.Fade(0.925f);
        for (int i = 0; i < flock.Length; i++)
        {
            var f = flock[i];
            f.Update(effectDelta);
            buffer[controller.penrose.GetIndexFromPosition(f.position)] = f.color * beatBrightness;
        }
    }

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

        public Boid(Vector2 min, Vector2 max, Flock parent)
        {
            this.min = min;
            this.max = max;
            this.parent = parent;
            velocity = new Vector2(Random.Range(-maxSpeed, maxSpeed), Random.Range(-maxSpeed, maxSpeed));
            position = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
        }

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

        private void CheckEdges()
        {
            if (position.x > max.x)
                position.x = min.x;
            else if (position.x < min.x) position.x = max.x;

            if (position.y > max.y)
                position.y = min.y;
            else if (position.y < min.y) position.y = max.y;
        }

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