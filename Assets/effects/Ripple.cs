using Random = UnityEngine.Random;
using UnityEngine;
using System;

/// <summary>
/// Renders expanding screen-space ripple rings and maps them to Penrose tiles.
/// </summary>
public class Ripple : ScreenEffect
{

    private Color startColor;
    private Color endColor;
    private Drop[] drops;
    private Vector2 screen;
    private float intensity;

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
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
        intensity = Random.Range(0.01f, 0.02f);
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
        // Beat pulse scales ripple brightness while drop radius/progression remains independent.
        float hueShift = beatManager.GetBeatBrightness(beatVariant, 0.2f, 0.0f);
        if (Random.value < intensity)
        {
            Array.Resize(ref drops, drops.Length + 1);
            drops[drops.Length - 1] = new Drop();
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
                    sum += (drops[i].radius - (d / 20)).Clamp01();
                }
                sum += 0.5f;
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
        /// Creates a ripple drop at a random screen position.
        /// </summary>
        public Drop()
        {
            velocity = Random.Range(0.01f, 0.9f) / 2000f;
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
