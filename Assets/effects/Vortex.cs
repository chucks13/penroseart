//using System.Drawing;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders nearest-spinner palette fields from moving angular sources.
/// </summary>
public class Vortex : EffectBase
{
    /// <summary>Vortex's swirling motion suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow |Repertoire.EnergyMid | Repertoire.EnergyHigh;


    private int count;
    private float speed;
    private float angle;
    private int distortionMode; // 0: Brightness, 1: Color, 2: Time


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
        distortionMode = Random.Range(0, 2)*2;      // 0 or 2

        buffer.Clear();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }
    public void Update(float delta)
    {
        float deg2rad = (Mathf.PI * 2f) / 360f;
        angle += speed * delta;
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
        float beatBrightness = 1.0f;
        float hueShift = 0.0f;
        float sampleeDelta = effectDelta;

        float rhythm = waveform.Envelope;
        if (distortionMode == 0)
            beatBrightness = waveform.Lerp(0.65f, 1f);
        else if (distortionMode == 1)
            hueShift = 0.25f * rhythm;
        else if (distortionMode == 2)
            sampleeDelta = (effectDelta*0.15f) + (0.025f * rhythm);

        // Beat pulse scales the nearest-spinner palette result for each tile.
        //        float beatBrightness = waveform.Lerp(0.5f, 1f);
        Update(sampleeDelta);
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
            Color c = spinners[which].Draw(i, tiles[i].position) * beatBrightness;
            if (hueShift > 0)
            {
                float h, s, v_col;
                Color.RGBToHSV(c, out h, out s, out v_col);
                c = Color.HSVToRGB((h + hueShift) % 1f, s, v_col);
            }
            buffer[i] = c * beatBrightness;
            // Draw the point
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
