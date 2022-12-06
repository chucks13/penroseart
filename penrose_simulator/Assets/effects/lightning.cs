using UnityEngine;
// Chuck Sommerville

[System.Serializable]
public class lightning : EffectBase
{
    float huerot=0;
    float fadeValue;
    float starthue = Random.value;
    float deltastart=0f;
    float deltaray = 0f;
    float deltatile = 0f;
    int mode = 0;
    public override string DebugText()
    {
        return $"fade: {fadeValue}\n starthue:{starthue}\n deltastart:{deltastart}\n deltaray:{deltaray}\n deltatile:{deltatile}\n mode:{mode}";
    }

    public override void Init()
    {
        base.Init();
    }

    public override void OnStart()
    {
        buffer.Clear();
        fadeValue = Random.value;
        starthue = Random.value;
        //  selectively modify animation
        deltastart = Random.Range(0, 2) == 0 ? 0f : 0.02f;
        deltaray = Random.Range(0, 2) == 0 ? 0f : 0.2f;
        deltatile = Random.Range(0, 2) == 0 ? 0f : 0.02f;
        // set random directions
        deltastart *= Random.Range(0, 2) == 0 ?1f:-1f;
        deltaray *= Random.Range(0, 2) == 0 ? 1f : -1f;
        deltatile *= Random.Range(0, 2) == 0 ? 1f : -1f;
        mode = Random.Range(0, 4);
    }

    public override void OnEnd() { }

    public override void Draw()
    {
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
                if(mode!=0)
                    buffer[currentIdx] = APalette.read((tilehue+10000f) % 1.0f, true); //Color.HSVToRGB((hue+ huerot)%1.0f, 1, 1);
                else
                    buffer[currentIdx] = Color.HSVToRGB((tilehue+10000f) %1.0f, 1, 1);
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