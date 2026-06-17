using UnityEngine;
// Chuck Sommerville

[System.Serializable]
/// <summary>
/// Builds stochastic branching paths outward from center-star tiles.
/// </summary>
public class lightning : EffectBase
{
    float fadeValue;
    float starthue;
    float deltastart = 0f;
    float deltaray = 0f;
    float deltatile = 0f;

    int beatMode;

    int mode = 0;
    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"fade: {fadeValue}\n starthue:{starthue}\n deltastart:{deltastart}\n deltaray:{deltaray}\n deltatile:{deltatile}\n mode:{mode}";
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
        buffer.Clear();
        fadeValue = Random.value;
        starthue = Random.value;
        //  selectively modify animation
        deltastart = Random.Range(0, 2) == 0 ? 0f : 0.02f;
        deltaray = Random.Range(0, 2) == 0 ? 0f : 0.2f;
        deltatile = Random.Range(0, 2) == 0 ? 0f : 0.02f;
        // set random directions
        deltastart *= Random.Range(0, 2) == 0 ? 1f : -1f;
        deltaray *= Random.Range(0, 2) == 0 ? 1f : -1f;
        deltatile *= Random.Range(0, 2) == 0 ? 1f : -1f;
        mode = Random.Range(0, 4);
        beatMode = Random.Range(0, 3);

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
        // Beat pulse scales branching lightning path colors.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 0.75f, 1.0f, beatEnable);
        float beatHue = beatManager.GetBeatBrightness(beatVariant, 0.5f, 0.0f, beatEnable);

        // this selects the center star 5 tiles
        int[] shape = penrose.JsonRawData.shapes.stars;
        int list = shape[1];
        int start = list + 1;
        int end = start + shape[list];
        int[] possible = { 0, 0, 0, 0 };        // holds possible step possitions

        buffer.Fade(fadeValue);
        // for each of the 5 tiles in the center star
        float rayhue = starthue;
        starthue += deltastart;
        for (int j = start; j < end; j++)
        {
            int currentIdx = shape[j];
            // walk the line till it stops
            float tilehue = rayhue;
            rayhue += deltaray;
            while (true)
            {
                // color the current tile
                float currentRadius = tiles[currentIdx].radius;
                Color strokeColor;
                if (mode != 0)
                    strokeColor = APalette.read((tilehue + 10000f) % 1.0f, true);
                else
                    strokeColor = Color.HSVToRGB((tilehue + 10000f) % 1.0f, 1, 1);

                if (beatMode < 2)
                    strokeColor *= beatBrightness;
                if (beatMode > 0)
                {
                    Color.RGBToHSV(strokeColor, out float h, out float s, out float v);
                    strokeColor = Color.HSVToRGB((h + beatHue) % 1f, s, v);
                }

                buffer[currentIdx] = strokeColor * beatBrightness;
                tilehue += deltatile;
                // find possible paths
                int used = 0;
                for (int i = 0; i < tiles[currentIdx].neighbors.Length; i++)
                {
                    int testTile = tiles[currentIdx].neighbors[i].tileIdx;
                    float testRadius = tiles[testTile].radius;
                    // if the step takes us father form the origin
                    if (testRadius > currentRadius)
                        possible[used++] = testTile;
                }
                // stop if nowhere to go
                if (used == 0)
                    break;
                // step
                currentIdx = possible[Random.Range(0, used)];
            }
        }
    }

}