// By Chuck Sommerville
/*
 * loads images in assets folder, and displays them as kscope
 */
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Random = UnityEngine.Random;

#if true
/// <summary>
/// Loads StreamingAssets textures and maps them through kaleidoscope/mirror patterns.
/// </summary>
[EffectSyncSettings(typeof(KscopeSyncSettingsAsset))]
public class Kscope : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Minimum number of texture-catalog slots advanced on each activation.</summary>
    private const int StandaloneTextureMinimumAdvance = 1;

    /// <summary>
    /// Divisor applied to the texture count to form the discrete random-advance upper bound.
    /// One third limits each activation's random bonus to roughly one third of the catalog.
    /// </summary>
    private const int StandaloneTextureAdvanceRangeDivisor = 3;

    /// <summary>
    /// Exclusive upper bound of the discrete color-swap roll; one selected slot out of three mutates a color texture.
    /// </summary>
    private const int StandaloneColorSwapRollMaxExclusive = 3;

    /// <summary>
    /// Exclusive upper bound of the discrete channel-swap selector roll in <see cref="messTexture"/>.
    /// The switch defines three channel swaps, but this authored bound reaches only the first two
    /// (red/blue and red/green); the green/blue arm is unreachable today, recorded on #111's
    /// findings list rather than changed here.
    /// </summary>
    private const int StandaloneChannelSwapSelectorMaxExclusive = 2;

    /// <summary>Inclusive minimum integer step used to roll horizontal and vertical texture motion.</summary>
    private const int StandaloneMotionStepMin = 1;

    /// <summary>Exclusive maximum integer step used to roll horizontal and vertical texture motion.</summary>
    private const int StandaloneMotionStepMaxExclusive = 3;

    /// <summary>Divisor converting a rolled integer motion step into the authored texture-motion rate.</summary>
    private const float StandaloneMotionStepDivisor = 4f;

    /// <summary>Inclusive minimum integer step used to roll kaleidoscope rotation speed.</summary>
    private const int StandaloneAngularSpeedStepMin = -1;

    /// <summary>Exclusive maximum integer step used to roll kaleidoscope rotation speed.</summary>
    private const int StandaloneAngularSpeedStepMaxExclusive = 2;

    /// <summary>Divisor converting a rolled integer angular step into the authored rotation rate.</summary>
    private const float StandaloneAngularSpeedStepDivisor = 100f;

    // Sync Defaults

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    private const float SyncBeatHueOffset = 0.5f;

    /// <summary>Extra frame delta contributed at the held Waveform's peak in beat-responsive motion modes.</summary>
    private const float SyncRhythmDeltaBoost = 0.002f;

    /// <summary>
    /// Number of beats used by the inherited Drop slowdown. This was the call's implicit default before capture.
    /// </summary>
    private const int SyncDropSlowdownBeats = 8;

    // Runtime mechanism constants

    /// <summary>Reference frame rate converting the effect's authored per-frame motion into delta-time motion.</summary>
    private const float ReferenceFrameRate = 60f;

    /// <summary>Resolves a fresh immutable-by-convention copy of Kscope's Standalone Defaults.</summary>
    public static KscopeStandaloneSettings StandaloneSettings => new KscopeStandaloneSettings
    {
        TextureMinimumAdvance = StandaloneTextureMinimumAdvance,
        TextureAdvanceRangeDivisor = StandaloneTextureAdvanceRangeDivisor,
        ColorSwapRollMaxExclusive = StandaloneColorSwapRollMaxExclusive,
        ChannelSwapSelectorMaxExclusive = StandaloneChannelSwapSelectorMaxExclusive,
        MotionStepMin = StandaloneMotionStepMin,
        MotionStepMaxExclusive = StandaloneMotionStepMaxExclusive,
        MotionStepDivisor = StandaloneMotionStepDivisor,
        AngularSpeedStepMin = StandaloneAngularSpeedStepMin,
        AngularSpeedStepMaxExclusive = StandaloneAngularSpeedStepMaxExclusive,
        AngularSpeedStepDivisor = StandaloneAngularSpeedStepDivisor,
    };

    /// <summary>Resolves a fresh copy of Kscope's file-local Sync Defaults.</summary>
    public static KscopeSyncSettings SyncDefaults => new KscopeSyncSettings
    {
        BeatHueOffset = SyncBeatHueOffset,
        RhythmDeltaBoost = SyncRhythmDeltaBoost,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private KscopeStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private KscopeSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Kscope's slow kaleidoscopic imagery suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    public class picture
    {
        public Texture2D tex;
        public string fname;
    };
    string fname = "";

    /// <summary>The allocation-free reader over the mirror groups rolled for this activation.</summary>
    private LayoutData.ShapeList.Reader mirrorList;

    private int[] centerline;
    List<picture> colorTex = new List<picture>();
    List<picture> monoTex = new List<picture>();
    Texture2D currentTex;
    //    int last = -1;
    int mode;
    int texWidth;
    int texHeight;
    int centerX;
    int centerY;
    int beatMode;
    float positionX;
    float positionY;
    float motionX;
    float motionY;
    float angle;
    float aspeed;
    int which = 0;

    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    /// 
    public void WriteFileList(string directoryPath)
    {
        string[] fileNames = Directory.GetFiles(directoryPath);
        for (int i = 0; i < fileNames.Length; i++)
        {
            fileNames[i] = Path.GetFileName(fileNames[i]);
        }
        File.WriteAllLines(directoryPath + "/files.txt", fileNames);
    }
    /// <summary>
    /// Reads a text file from StreamingAssets.
    /// </summary>
    public string LoadTextFile(string fileName)
    {
        string filePath = Application.streamingAssetsPath + "/" + fileName;
        string fileContents = "";

        if (Application.platform == RuntimePlatform.Android)
        {
            UnityWebRequest www = UnityWebRequest.Get(filePath);
            www.SendWebRequest();
            while (!www.isDone) { }
            fileContents = www.downloadHandler.text;
        }
        else
        {
            fileContents = File.ReadAllText(filePath);
        }

        return fileContents;
    }
    /// <summary>
    /// Loads a PNG file from disk into a Texture2D.
    /// </summary>
    public static Texture2D LoadPNG(string filePath)
    {
        Texture2D tex = null;
        byte[] fileData;

        if (File.Exists(filePath))
        {
            fileData = File.ReadAllBytes(filePath);
            tex = new Texture2D(2, 2);
            tex.LoadImage(fileData); //..this will auto-resize the texture dimensions.
        }
        return tex;
    }
    /// <summary>
    /// Loads a picture from the active StreamingAssets image folder.
    /// </summary>
    public Texture2D LoadPicture(string fileName)
    {
        string filePath = Application.streamingAssetsPath + "/" + fileName;
        byte[] fileData;
        if (Application.platform == RuntimePlatform.Android)
        {
            UnityWebRequest www = UnityWebRequest.Get(filePath);
            www.SendWebRequest();
            while (!www.isDone) { }
            fileData = www.downloadHandler.data;
        }
        else
        {
            fileData = File.ReadAllBytes(filePath);
        }
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);
        return texture;
    }
    List<picture> readDirectory(string path)
    {
        if (Application.platform != RuntimePlatform.Android)
            WriteFileList(Application.streamingAssetsPath + path);

        List<picture> texList = new List<picture>();
        string contents = LoadTextFile(path + "/files.txt");
        //        string[] fileNames = new string[0];
        string[] fileNames = contents.Split('\n');
        foreach (string fileName in fileNames)
        {
            string fullPath = path + "/" + fileName.TrimEnd('\r');
            if (!fileName.Contains(".png"))
                continue;
            //            if (fileName.Contains(".meta"))
            //                continue;
            //            if (fileName.Contains(".txt"))
            //                continue;
            Texture2D tex;
            try
            {
                tex = LoadPicture(fullPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error loading image: " + ex.Message);
                continue;
            }
            picture pic = new picture();
            pic.tex = tex;
            pic.fname = fileName;

            texList.Add(pic);
        }
        return texList;

    }
    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"file {fname} ";
    }

    /// <summary>
    /// Builds the eight-tile center patch list before the selected mirror groups are drawn.
    /// </summary>
    private void fixCenterLineInit()
    {
        centerline = new int[8];
        int y = 0;
        for (int x = 0; x < 900; x++)
        {
            if (y == centerline.Length)
                break;
            int groupcount = mirrorList.GroupCount;     // how many copies
            bool used = false;                                    // Draw the mirrors
            for (int i = 0; i < groupcount; i++)
            {
                LayoutData.ShapeList.Group group = mirrorList.GetGroup(i);
                for (int j = 0; j < group.TileCount; j++)
                {
                    if (group[j] == x)
                    {
                        used = true;
                        break;
                    }
                }
            }
            if (!used)
                centerline[y++] = x;
        }
    }
    /// <summary>
    /// Patches centerline tiles omitted by mirror shape data before mirror replication.
    /// </summary>
    private void fixCenterLineDraw()
    {
        for (int i = 0; i < centerline.Length; i++)
        {
            int j = centerline[i];
            buffer[j] = buffer[j];
        }

    }
    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        colorTex = readDirectory($"/images/color");
        monoTex = readDirectory($"/images/mono");
    }

    /// <summary>
    /// Returns a copy of a color texture with one randomly chosen pair of color channels swapped.
    /// </summary>
    Texture2D messTexture(Texture2D oldtex)
    {
        Texture2D newTex = new Texture2D(oldtex.width, oldtex.height);
        // The inline zero is the structural start of the selector domain. The authored bound reaches
        // only two of the switch's three swap arms; see StandaloneChannelSwapSelectorMaxExclusive.
        int swap = Random.Range(0, standaloneSettings.ChannelSwapSelectorMaxExclusive);
        float a;
        for (int x = 0; x < oldtex.width; x++)
        {
            for (int y = 0; y < oldtex.height; y++)
            {
                var color = oldtex.GetPixel(x, y);
                switch (swap)
                {
                    case 0:
                        a = color.r;
                        color.r = color.b;
                        color.b = a;
                        break;
                    case 1:
                        a = color.r;
                        color.r = color.g;
                        color.g = a;
                        break;
                    case 2:
                        a = color.g;
                        color.g = color.b;
                        color.b = a;
                        break;
                }
                newTex.SetPixel(x, y, color);
            }
        }
        return newTex;
    }
    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Kscope),
            SyncDefaults);

        // Unfiltered acquisition spans the complete curated Waveform Pool, so there is no authored subrange.
        waveform = waveforms.Random();
        // This coin flip spans both available mirror layouts, so its complete selector domain stays inline.
        mirrorList = penrose.Layout.shapes.Read(
            Random.Range(0, 2) == 0 ? penrose.Layout.shapes.mirror2 : penrose.Layout.shapes.mirror10);
        fixCenterLineInit();

        int colorCount = colorTex.Count;
        int monoCount = monoTex.Count;
        int total = colorCount + monoCount;
        // The inline zero is the structural no-bonus endpoint of this discrete advance roll.
        which = (which + standaloneSettings.TextureMinimumAdvance +
            Random.Range(0, total / standaloneSettings.TextureAdvanceRangeDivisor)) % total;// Random.Range(0, total);
        if (which < colorCount)
        {
            currentTex = colorTex[which].tex;
            fname = colorTex[which].fname;
            // sometime swap 2 colors
            // Zero is the designated success slot; the authored slot count controls the one-in-N chance.
            if (Random.Range(0, standaloneSettings.ColorSwapRollMaxExclusive) == 0)
                currentTex = messTexture(currentTex);
            mode = 0;
        }
        else
        {
            currentTex = monoTex[which - colorCount].tex;
            fname = monoTex[which - colorCount].fname;
            mode = 1;
        }
        texWidth = currentTex.width;
        texHeight = currentTex.height;
        motionX = Random.Range(standaloneSettings.MotionStepMin, standaloneSettings.MotionStepMaxExclusive) /
            standaloneSettings.MotionStepDivisor;
        motionY = Random.Range(standaloneSettings.MotionStepMin, standaloneSettings.MotionStepMaxExclusive) /
            standaloneSettings.MotionStepDivisor;
        // Each sign flip spans the complete two-direction domain, so its selector stays inline.
        motionX *= Random.Range(0, 2) == 0 ? 1f : -1f;
        motionY *= Random.Range(0, 2) == 0 ? 1f : -1f;

        // Each position roll spans the complete source-texture extent, not an authored subrange.
        positionX = Random.Range(0, texWidth);
        positionY = Random.Range(0, texHeight);
        centerX = texWidth / 2;
        centerY = texHeight / 2;
        angle = 0;
        aspeed = Random.Range(
            standaloneSettings.AngularSpeedStepMin,
            standaloneSettings.AngularSpeedStepMaxExclusive) / standaloneSettings.AngularSpeedStepDivisor;
        // The discrete [0, 3) roll spans all three algorithm modes, so its complete domain stays inline.
        beatMode = Random.Range(0, 3);
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd()
    {
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    /// 
    /*
     * x2=cosßx1-sinßy1
     * y2=sinßx1+cosßy1
     */
    /// <summary>Samples the moving texture into the wall buffer, then mirrors every selected Shape List group.</summary>
    public override void Draw()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            Init();
        }
        float rhythm = waveform.Envelope;
        float beatHue = SyncSettings.BeatHueOffset * rhythm;
        float localDelta = DropSlowdown(
            beatMode < 2 ? effectDelta + (SyncSettings.RhythmDeltaBoost * rhythm) : effectDelta,
            SyncSettings.DropSlowdownBeats);


        positionX += motionX * localDelta * ReferenceFrameRate;
        positionY += motionY * localDelta * ReferenceFrameRate;

        double m11 = Math.Cos(angle);
        double m12 = -Math.Sin(angle);
        double m21 = Math.Sin(angle);
        double m22 = Math.Cos(angle);
        double wh = width / 2;
        double yh = height / 2;
        angle += aspeed * effectDelta * ReferenceFrameRate;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // center about screen
                double x1 = x - wh;
                double y1 = y - yh;
                // apply rotation
                double x2 = (m11 * x1) + (m12 * y1);
                double y2 = (m21 * x1) + (m22 * y1);
                // center about texture
                //                x2 += centerX;
                //                y2 += centerY;
                // offset to position
                x2 += positionX;
                y2 += positionY;
                if (x2 < 0)
                    x2 = -x2;
                if (y2 < 0)
                    y2 = -y2;
                int xp = (int)x2 / texWidth;
                int yp = (int)y2 / texHeight;
                x2 %= texWidth;
                y2 %= texHeight;
                if ((xp & 1) != 0)
                    x2 = (texWidth - 1) - x2;
                if ((yp & 1) != 0)
                    y2 = (texHeight - 1) - y2;

                var color = currentTex.GetPixel((int)x2, (int)y2);
                if (mode != 0)
                    color = APalette.read(color.r % 1f, true);
                if (beatMode > 0)
                {
                    Color.RGBToHSV(color, out float h, out float s, out float v);
                    color = Color.HSVToRGB((h + beatHue) % 1f, s, v);
                }
                screenBuffer[x + (y * width)] = color;
            }
        }
        // convert the 2D Matrix buffer to a tile buffer
        ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in buffer);
        int groupcount = mirrorList.GroupCount;     // how many copies
        // fix missing verticle column
        fixCenterLineDraw();
        // Draw the mirrors
        for (int i = 0; i < groupcount; i++)
        {
            LayoutData.ShapeList.Group group = mirrorList.GetGroup(i);
            Color tileColor = buffer[group[0]];
            for (int j = 0; j < group.TileCount; j++)
            {
                if (beatManager.Fill.Active)            // blak and whire on fill
                {
                    float h, s, v_col;
                    Color.RGBToHSV(tileColor, out h, out s, out v_col);
                    v_col = (h + s + v_col) % 1f;                   // assure there is brightness variation
                    // Full desaturation defines the black-and-white Fill treatment, so zero stays structural.
                    s = 0f;
                    tileColor = Color.HSVToRGB(h, s, v_col);
                }
                buffer[group[j]] = tileColor;
            }
        }
    }


}

/// <summary>Fixed Standalone Settings resolved from Kscope's file-local Standalone Defaults.</summary>
public sealed class KscopeStandaloneSettings
{
    /// <summary>Minimum number of texture-catalog slots advanced on each activation.</summary>
    public int TextureMinimumAdvance;

    /// <summary>Divisor used to form the discrete random texture-advance upper bound.</summary>
    public int TextureAdvanceRangeDivisor;

    /// <summary>Exclusive upper bound of the discrete color-swap chance roll.</summary>
    public int ColorSwapRollMaxExclusive;

    /// <summary>Exclusive upper bound of the discrete channel-swap selector roll.</summary>
    public int ChannelSwapSelectorMaxExclusive;

    /// <summary>Inclusive minimum integer step used to roll texture motion.</summary>
    public int MotionStepMin;

    /// <summary>Exclusive maximum integer step used to roll texture motion.</summary>
    public int MotionStepMaxExclusive;

    /// <summary>Divisor converting a rolled motion step into texture-motion rate.</summary>
    public float MotionStepDivisor;

    /// <summary>Inclusive minimum integer step used to roll kaleidoscope rotation speed.</summary>
    public int AngularSpeedStepMin;

    /// <summary>Exclusive maximum integer step used to roll kaleidoscope rotation speed.</summary>
    public int AngularSpeedStepMaxExclusive;

    /// <summary>Divisor converting a rolled angular step into rotation rate.</summary>
    public float AngularSpeedStepDivisor;
}

/// <summary>Serializable Sync Settings saved for Kscope and edited live through the Effects tab.</summary>
[Serializable]
public sealed class KscopeSyncSettings
{
    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    [Range(0f, 1f)] public float BeatHueOffset;

    /// <summary>Extra frame delta contributed at the held Waveform's peak.</summary>
    [Min(0f)] public float RhythmDeltaBoost;

    /// <summary>Number of beats used by the inherited Drop slowdown.</summary>
    [Min(1)] public int DropSlowdownBeats;

    /// <summary>Copies every Kscope Sync Setting from another value.</summary>
    public void CopyFrom(KscopeSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BeatHueOffset = source.BeatHueOffset;
        RhythmDeltaBoost = source.RhythmDeltaBoost;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
#endif
