using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders nearest-spinner palette fields from moving angular sources.
/// </summary>
public class Vortex : EffectBase
{
    /// <summary>Vortex's swirling motion suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyMid | Repertoire.EnergyHigh;


    private int count;
    private float speed;
    private float angle;


    public spinner[] spinners;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Vortex: {count}\n";
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
        waveform = waveforms.Random();
        count = Random.Range(1, 5);
        angle = 0f;
        speed = Random.Range(50, 100);
        if (Random.Range(0, 2) == 0)
            speed = -speed;
        float twist = Random.Range(-0.02f, 0.02f);
        spinners = new spinner[count];
        for (int i = 0; i < count; i++)
        {
            spinner sample = new spinner();
            //            sample.palette.blend = (Random.Range(0, 2) == 0);
            sample.twist = twist;
            spinners[i] = sample;
            //            spinners[i].palette = spinners[0].palette;          // make palettes the same
        }
        buffer.Clear();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }
    public void Update()
    {
        float deg2rad = (Mathf.PI * 2f) / 360f;
        angle += speed * effectDelta;
        for (int i = 0; i < count; i++)
        {
            spinner sample = spinners[i];
            float local = angle + (i * 360 / count);
            local *= deg2rad;
            sample.center.x = Mathf.Sin(local) * 16f;
            sample.center.y = Mathf.Cos(local) * 8f;
            sample.angle = local;
        }
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales the nearest-spinner palette result for each tile.
        float? rhythm = waveforms.Evaluate(waveform);
        float beatBrightness = rhythm is { } envelope ? Mathf.Lerp(0.5f, 1f, envelope) : 1f;
        Update();
        for (int i = 0; i < buffer.Length; i++)
        {
            int which = 0;
            float min = 100000f;
            // find the closest
            for (int j = 0; j < spinners.Length; j++)
            {
                Vector2 delta = tiles[i].position - spinners[j].center;
                float d2 = (delta.x * delta.x) + (delta.y * delta.y);
                if (d2 < min)
                {
                    min = d2;
                    which = j;
                }
            }
            // Draw the point
            buffer[i] = spinners[which].Draw(i, tiles[i].position) * beatBrightness;
        }
    }

    [System.Serializable]
    /// <summary>
    /// Moving angular source used by Vortex to determine nearest palette influence.
    /// </summary>
    public class spinner
    {
        public Vector2 center;
        public int arms = 1;
        public float twist = 0.01f;
        public float angle = 0;
        const float rad2once = 1f / (Mathf.PI * 2f);
        public float speed = 0.5f;
        //        public GPalette palette = new GPalette();

        /// <summary>
        /// Samples the spinner's palette contribution for a tile position.
        /// </summary>
        public Color Draw(int i, Vector2 position)
        {
            Vector2 vect = position - center;
            float rotate = Mathf.Atan2(vect.y, vect.x);
            float length = Vector2.Distance(center, position);
            rotate += Mathf.PI;
            rotate *= rad2once;
            rotate *= arms;
            rotate += twist * length;
            rotate += angle;
            return APalette.read(rotate % 1f);// Color.HSVToRGB(rotate%1f, 1f, 1f);
        }
    }
}
