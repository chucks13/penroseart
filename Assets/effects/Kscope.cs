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

/// <summary>
/// Loads StreamingAssets textures and maps them through kaleidoscope/mirror patterns.
/// </summary>
[EffectSyncSettings(typeof(KscopeSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(KscopeStandaloneSettingsAsset))]
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

    /// <summary>Minimum number of texture-catalog slots advanced on each Synced activation.</summary>
    private const int SyncTextureMinimumAdvance = 1;

    /// <summary>Divisor forming the discrete random-advance upper bound in Synced Mode.</summary>
    private const int SyncTextureAdvanceRangeDivisor = 3;

    /// <summary>Exclusive upper bound of the discrete color-swap roll in Synced Mode.</summary>
    private const int SyncColorSwapRollMaxExclusive = 3;

    /// <summary>Exclusive upper bound of the channel-swap selector roll in Synced Mode.</summary>
    private const int SyncChannelSwapSelectorMaxExclusive = 2;

    /// <summary>
    /// Wall units the source image's width spans in Synced Mode — the sampling zoom. Image size
    /// must never reach the wall: the pools span 12x13 to 528x494 pixels, and any raw-size
    /// sampling makes speed and the On-Beat Push read differently per image. Twenty-five fits
    /// the whole picture in one Mirror2 half of the 50x22 wall.
    /// </summary>
    private const float SyncImageSpan = 25f;

    /// <summary>
    /// Fraction of the source image panned per beat before musical pacing. With every image
    /// scaled to the authored footprint, this is the same wall distance on every image — one
    /// tenth crosses the picture in ten beats.
    /// </summary>
    private const float SyncPanFractionPerBeat = 0.1f;

    /// <summary>
    /// Kaleidoscope rotation in radians per beat before musical pacing — about seventeen
    /// degrees. Uniform across images because Synced sampling scales every image to the
    /// authored footprint.
    /// </summary>
    private const float SyncRotationRadiansPerBeat = 0.3f;

    /// <summary>Low Energy pace slows the base motion while keeping the effect visibly alive.</summary>
    private const float SyncEnergyPaceLow = 0.75f;

    /// <summary>High Energy pace accelerates the base motion; Mid remains neutral at the range midpoint.</summary>
    private const float SyncEnergyPaceHigh = 1.25f;

    /// <summary>
    /// Normalized Low threshold where bass presence begins contributing to the On-Beat Push. Levels
    /// are track-relative, so this remains a live tuning knob instead of an absolute-loudness claim.
    /// </summary>
    private const float SyncLowPresenceThreshold = 0.45f;

    /// <summary>
    /// Pace added at a fully gated quarter-note pulse peak. One half makes the bass hit legible
    /// above the Energy-paced motion without overwhelming the underlying image.
    /// </summary>
    private const float SyncOnBeatPushStrength = 0.5f;

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    private const float SyncBeatHueOffset = 0.5f;

    /// <summary>
    /// Number of beats used by the inherited Drop slowdown. This was the call's implicit default before capture.
    /// </summary>
    private const int SyncDropSlowdownBeats = 8;

    // Runtime mechanism constants

    /// <summary>Reference frame rate converting Standalone's authored per-frame motion into delta-time motion.</summary>
    private const float ReferenceFrameRate = 60f;

    /// <summary>Resolves a fresh immutable-by-convention copy of Kscope's Standalone Defaults.</summary>
    public static KscopeStandaloneSettings StandaloneDefaults => new KscopeStandaloneSettings
    {
        TextureMinimumAdvance = StandaloneTextureMinimumAdvance,
        TextureAdvanceRangeDivisor = StandaloneTextureAdvanceRangeDivisor,
        ColorSwapRollMaxExclusive = StandaloneColorSwapRollMaxExclusive,
        ChannelSwapSelectorMaxExclusive = StandaloneChannelSwapSelectorMaxExclusive,
        MotionStep = new IntRange(StandaloneMotionStepMin, StandaloneMotionStepMaxExclusive),
        MotionStepDivisor = StandaloneMotionStepDivisor,
        AngularSpeedStep = new IntRange(StandaloneAngularSpeedStepMin, StandaloneAngularSpeedStepMaxExclusive),
        AngularSpeedStepDivisor = StandaloneAngularSpeedStepDivisor,
    };

    /// <summary>Resolves a fresh copy of Kscope's file-local Sync Defaults.</summary>
    public static KscopeSyncSettings SyncDefaults => new KscopeSyncSettings
    {
        TextureMinimumAdvance = SyncTextureMinimumAdvance,
        TextureAdvanceRangeDivisor = SyncTextureAdvanceRangeDivisor,
        ColorSwapRollMaxExclusive = SyncColorSwapRollMaxExclusive,
        ChannelSwapSelectorMaxExclusive = SyncChannelSwapSelectorMaxExclusive,
        ImageSpan = SyncImageSpan,
        PanFractionPerBeat = SyncPanFractionPerBeat,
        RotationRadiansPerBeat = SyncRotationRadiansPerBeat,
        EnergyPace = new FloatRange(SyncEnergyPaceLow, SyncEnergyPaceHigh),
        LowPresenceThreshold = SyncLowPresenceThreshold,
        OnBeatPushStrength = SyncOnBeatPushStrength,
        BeatHueOffset = SyncBeatHueOffset,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private KscopeStandaloneSettings standaloneSettings = StandaloneDefaults;

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

    List<picture> colorTex = new List<picture>();
    List<picture> monoTex = new List<picture>();
    Texture2D currentTex;

    /// <summary>
    /// The channel-swapped copy owned by the current activation, or null when the pick renders
    /// straight from the pool. Script-created textures are never garbage-collected, so this
    /// reference is what the next activation destroys.
    /// </summary>
    private Texture2D swappedTex;
    int mode;
    int texWidth;
    int texHeight;
    /// <summary>Two-way activation coin whose non-zero side enables the existing beat hue shift.</summary>
    int beatMode;
    float positionX;
    float positionY;
    /// <summary>Signed Standalone horizontal rate, or the Synced horizontal direction.</summary>
    float motionX;
    /// <summary>Signed Standalone vertical rate, or the Synced vertical direction.</summary>
    float motionY;
    float angle;
    /// <summary>Signed Standalone angular rate, or the Synced rotation direction.</summary>
    float aspeed;
    /// <summary>Texture-catalog index advanced by the activation Roll.</summary>
    int which = 0;

    /// <summary>
    /// Regenerates the files.txt manifest for one StreamingAssets image folder. Desktop platforms
    /// write the manifest because Android cannot enumerate StreamingAssets directories — the
    /// Android path can only read a manifest a desktop run already wrote.
    /// </summary>
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
        string[] fileNames = contents.Split('\n');
        foreach (string fileName in fileNames)
        {
            string fullPath = path + "/" + fileName.TrimEnd('\r');
            if (!fileName.Contains(".png"))
                continue;
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
    /// <param name="oldtex">The source texture whose pixels are copied.</param>
    /// <param name="channelSwapSelectorMaxExclusive">The exclusive selector bound for channel-pair choice.</param>
    /// <returns>A new texture containing the selected channel swap.</returns>
    Texture2D messTexture(Texture2D oldtex, int channelSwapSelectorMaxExclusive)
    {
        Texture2D newTex = new Texture2D(oldtex.width, oldtex.height);
        // The inline zero is the structural start of the selector domain. The authored bound reaches
        // only two of the switch's three swap arms; see StandaloneChannelSwapSelectorMaxExclusive and
        // SyncChannelSwapSelectorMaxExclusive.
        int swap = Random.Range(0, channelSwapSelectorMaxExclusive);
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
        // Unity never garbage-collects script-created textures, so the previous activation's
        // channel-swapped copy is destroyed here — every activation path (switch-in and re-roll)
        // passes through OnStart.
        if (swappedTex != null)
        {
            UnityEngine.Object.Destroy(swappedTex);
            swappedTex = null;
        }

        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Kscope),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Kscope),
            SyncDefaults);

        // Unfiltered acquisition spans the complete curated Waveform Pool, so there is no authored subrange.
        waveform = waveforms.Random();
        // This coin flip spans both available mirror layouts, so its complete selector domain stays inline.
        mirrorList = Random.Range(0, 2) == 0
            ? penrose.Layout.shapes.Mirror2
            : penrose.Layout.shapes.Mirror10;

        int colorCount = colorTex.Count;
        int monoCount = monoTex.Count;
        int total = colorCount + monoCount;
        bool isSynced = beatManager.IsSynced;
        int textureMinimumAdvance = isSynced
            ? SyncSettings.TextureMinimumAdvance
            : standaloneSettings.TextureMinimumAdvance;
        int textureAdvanceRangeDivisor = isSynced
            ? SyncSettings.TextureAdvanceRangeDivisor
            : standaloneSettings.TextureAdvanceRangeDivisor;
        int colorSwapRollMaxExclusive = isSynced
            ? SyncSettings.ColorSwapRollMaxExclusive
            : standaloneSettings.ColorSwapRollMaxExclusive;
        int channelSwapSelectorMaxExclusive = isSynced
            ? SyncSettings.ChannelSwapSelectorMaxExclusive
            : standaloneSettings.ChannelSwapSelectorMaxExclusive;
        // The inline zero is the structural no-bonus endpoint of this discrete advance roll.
        which = (which + textureMinimumAdvance +
            Random.Range(0, total / textureAdvanceRangeDivisor)) % total;
        if (which < colorCount)
        {
            currentTex = colorTex[which].tex;
            fname = colorTex[which].fname;
            // sometime swap 2 colors
            // Zero is the designated success slot; the authored slot count controls the one-in-N chance.
            if (Random.Range(0, colorSwapRollMaxExclusive) == 0)
            {
                // The copy is script-owned; the destroy at the top of the next OnStart releases it.
                swappedTex = messTexture(currentTex, channelSwapSelectorMaxExclusive);
                currentTex = swappedTex;
            }
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
        if (isSynced)
        {
            // Synced magnitudes stay live in Sync Settings; the Roll retains direction only.
            motionX = 1f;
            motionY = 1f;
        }
        else
        {
            IntRange motionStep = standaloneSettings.MotionStep;
            float motionStepDivisor = standaloneSettings.MotionStepDivisor;
            motionX = Random.Range(motionStep.MinInclusive, motionStep.MaxExclusive) / motionStepDivisor;
            motionY = Random.Range(motionStep.MinInclusive, motionStep.MaxExclusive) / motionStepDivisor;
        }
        // Each sign flip spans the complete two-direction domain, so its selector stays inline.
        motionX *= Random.Range(0, 2) == 0 ? 1f : -1f;
        motionY *= Random.Range(0, 2) == 0 ? 1f : -1f;

        // Each position roll spans the complete source-texture extent, not an authored subrange.
        positionX = Random.Range(0, texWidth);
        positionY = Random.Range(0, texHeight);
        angle = 0;
        if (isSynced)
        {
            // Synced rotation magnitude stays live in Sync Settings; the Roll retains direction only.
            aspeed = Random.Range(0, 2) == 0 ? 1f : -1f;
        }
        else
        {
            IntRange angularSpeedStep = standaloneSettings.AngularSpeedStep;
            aspeed = Random.Range(
                angularSpeedStep.MinInclusive,
                angularSpeedStep.MaxExclusive) / standaloneSettings.AngularSpeedStepDivisor;
        }
        // The discrete [0, 2) roll is a two-way coin deciding whether the beat hue shift is active.
        beatMode = Random.Range(0, 2);
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd()
    {
    }

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
        float localDelta = DropSlowdown(effectDelta, SyncSettings.DropSlowdownBeats);
        float rotationDelta;
        // Texture pixels sampled per wall unit. Synced scales every image to the authored
        // footprint so image size never reaches the wall — speed and the On-Beat Push read
        // the same on every image. Standalone keeps the historical 1:1 sampling and look.
        float sampleScale = 1f;
        if (beatManager.IsSynced)
        {
            float motionScale = ReadSyncedMotionScale();
            positionX += motionX * SyncSettings.PanFractionPerBeat * texWidth * motionScale * localDelta;
            positionY += motionY * SyncSettings.PanFractionPerBeat * texHeight * motionScale * localDelta;
            rotationDelta = aspeed * SyncSettings.RotationRadiansPerBeat * motionScale * effectDelta;
            sampleScale = texWidth / SyncSettings.ImageSpan;
        }
        else
        {
            positionX += motionX * localDelta * ReferenceFrameRate;
            positionY += motionY * localDelta * ReferenceFrameRate;
            rotationDelta = aspeed * effectDelta * ReferenceFrameRate;
        }

        double m11 = Math.Cos(angle);
        double m12 = -Math.Sin(angle);
        double m21 = Math.Sin(angle);
        double m22 = Math.Cos(angle);
        double wh = width / 2;
        double yh = height / 2;
        angle += rotationDelta;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // center about screen
                double x1 = x - wh;
                double y1 = y - yh;
                // apply rotation, then scale to the authored footprint
                double x2 = ((m11 * x1) + (m12 * y1)) * sampleScale;
                double y2 = ((m21 * x1) + (m22 * y1)) * sampleScale;
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
                    // The palette supplies hue and saturation only; the mono image keeps its own
                    // brightness, so the picture's structure reads under any palette, dark ones included.
                    color = APalette.read(color.r % 1f, true).WithBrightness(color.r);
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

    /// <summary>
    /// Converts the authored per-beat magnitudes into per-second motion, paced by current Energy
    /// and lifted by the gated On-Beat Push.
    /// </summary>
    /// <remarks>
    /// Musical meanings: <c>CONTEXT.md</c> entries Data Surface and Energy. The live interval is
    /// the <c>/rave/onair/beat_avg_ms</c> lane in <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    private float ReadSyncedMotionScale()
    {
        float beatSeconds = beatManager.Timing.BeatAverageMilliseconds.Value / 1000f;
        return (ReadEnergyPace() + ReadOnBeatPush()) / beatSeconds;
    }

    /// <summary>Maps Low/Mid/High Energy onto the authored pace range, resting at neutral when unavailable.</summary>
    /// <remarks>
    /// Musical meaning: <c>CONTEXT.md</c> entry Energy; wire lane:
    /// <c>docs/osc-client-contract.md</c> <c>/rave/onair/energy_state</c>.
    /// </remarks>
    private float ReadEnergyPace()
    {
        FloatRange pace = SyncSettings.EnergyPace;
        return beatManager.Energy.Level switch
        {
            Energy.Low => pace.Min,
            Energy.Mid => (pace.Min + pace.Max) * 0.5f,
            Energy.High => pace.Max,
            _ => 1f,
        };
    }

    /// <summary>Returns the quarter-note Duration Pulse scaled by track-relative Normalized Low presence.</summary>
    /// <remarks>
    /// Musical meanings: <c>CONTEXT.md</c> entries Duration Pulse / Duration Gate and Levels;
    /// wire lane: <c>docs/osc-client-contract.md</c> <c>/rave/onair/levels</c>.
    /// </remarks>
    private float ReadOnBeatPush()
    {
        float lowPresence = beatManager.Levels.Normalized.Low.Remap(
            SyncSettings.LowPresenceThreshold,
            1f,
            0f,
            1f,
            clamp: true);
        return beatManager.Pulses.Every(Duration.Quarter) *
            lowPresence *
            SyncSettings.OnBeatPushStrength;
    }


}

/// <summary>Serializable Standalone Settings saved for Kscope and edited live through the Effects tab.</summary>
[Serializable]
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

    /// <summary>Integer step endpoints used to roll texture motion.</summary>
    public IntRange MotionStep = new IntRange();

    /// <summary>Divisor converting a rolled motion step into texture-motion rate.</summary>
    public float MotionStepDivisor;

    /// <summary>Integer step endpoints used to roll kaleidoscope rotation speed.</summary>
    public IntRange AngularSpeedStep = new IntRange();

    /// <summary>Divisor converting a rolled angular step into rotation rate.</summary>
    public float AngularSpeedStepDivisor;

    /// <summary>Copies every Kscope Standalone Setting and Rail from another value.</summary>
    public void CopyFrom(KscopeStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TextureMinimumAdvance = source.TextureMinimumAdvance;
        TextureAdvanceRangeDivisor = source.TextureAdvanceRangeDivisor;
        ColorSwapRollMaxExclusive = source.ColorSwapRollMaxExclusive;
        ChannelSwapSelectorMaxExclusive = source.ChannelSwapSelectorMaxExclusive;
        MotionStep = new IntRange(
            source.MotionStep.MinInclusive,
            source.MotionStep.MaxExclusive,
            source.MotionStep.LowRail,
            source.MotionStep.HighRail);
        MotionStepDivisor = source.MotionStepDivisor;
        AngularSpeedStep = new IntRange(
            source.AngularSpeedStep.MinInclusive,
            source.AngularSpeedStep.MaxExclusive,
            source.AngularSpeedStep.LowRail,
            source.AngularSpeedStep.HighRail);
        AngularSpeedStepDivisor = source.AngularSpeedStepDivisor;
    }
}

/// <summary>Serializable Sync Settings saved for Kscope and edited live through the Effects tab.</summary>
[Serializable]
public sealed class KscopeSyncSettings
{
    /// <summary>Minimum number of texture-catalog slots advanced on each activation.</summary>
    public int TextureMinimumAdvance;

    /// <summary>Divisor used to form the discrete random texture-advance upper bound.</summary>
    public int TextureAdvanceRangeDivisor;

    /// <summary>Exclusive upper bound of the discrete color-swap chance roll.</summary>
    public int ColorSwapRollMaxExclusive;

    /// <summary>Exclusive upper bound of the discrete channel-swap selector roll.</summary>
    public int ChannelSwapSelectorMaxExclusive;

    /// <summary>Wall units the image's width spans in Synced Mode — the sampling zoom.</summary>
    [Tooltip("Wall units the image's width spans on the 50x22 wall — the zoom. 25 fits the picture in one Mirror2 half; smaller tiles the image more, larger crops into it.")]
    [Min(0f)] public float ImageSpan;

    /// <summary>Fraction of the source image panned per beat before musical pacing — per axis, so X crosses that share of the width and Y of the height.</summary>
    [Tooltip("Fraction of the image panned per beat before Energy pace and On-Beat Push. Pattern-relative, so every image size reads the same speed; 0.1 crosses a tenth of the image per beat.")]
    [Min(0f)] public float PanFractionPerBeat;

    /// <summary>Kaleidoscope rotation in radians per beat before musical pacing.</summary>
    [Tooltip("Radians rotated per beat before Energy pace and On-Beat Push. 0.3 is about 17 degrees per beat; 0 stops rotation. Reads the same on every image because Synced sampling fixes the footprint.")]
    [Min(0f)] public float RotationRadiansPerBeat;

    /// <summary>Low-to-High Energy pace range; Mid uses the midpoint.</summary>
    [Tooltip("Motion pace by Energy: Low = Min, Mid = midpoint, High = Max. A value of 1 is neutral; the range carries its own tuning Rails.")]
    public FloatRange EnergyPace = new FloatRange();

    /// <summary>Normalized Low threshold where bass presence begins opening the On-Beat Push.</summary>
    [Tooltip("Track-relative Normalized Low level where the On-Beat Push begins. Raise it when non-bass material triggers the push; lower it when bass hits do not open it.")]
    [Range(0f, 1f)] public float LowPresenceThreshold;

    /// <summary>Pace added at a fully gated quarter-note pulse peak.</summary>
    [Tooltip("Pace added at the peak of each quarter-note Duration Pulse after the Normalized Low gate. 0 disables the push; raise it for a harder bass hit.")]
    [Min(0f)] public float OnBeatPushStrength;

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    [Range(0f, 1f)] public float BeatHueOffset;

    /// <summary>Number of beats used by the inherited Drop slowdown.</summary>
    [Min(1)] public int DropSlowdownBeats;

    /// <summary>Copies every Kscope Sync Setting from another value.</summary>
    public void CopyFrom(KscopeSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TextureMinimumAdvance = source.TextureMinimumAdvance;
        TextureAdvanceRangeDivisor = source.TextureAdvanceRangeDivisor;
        ColorSwapRollMaxExclusive = source.ColorSwapRollMaxExclusive;
        ChannelSwapSelectorMaxExclusive = source.ChannelSwapSelectorMaxExclusive;
        ImageSpan = source.ImageSpan;
        PanFractionPerBeat = source.PanFractionPerBeat;
        RotationRadiansPerBeat = source.RotationRadiansPerBeat;
        EnergyPace = new FloatRange(
            source.EnergyPace.Min,
            source.EnergyPace.Max,
            source.EnergyPace.LowRail,
            source.EnergyPace.HighRail);
        LowPresenceThreshold = source.LowPresenceThreshold;
        OnBeatPushStrength = source.OnBeatPushStrength;
        BeatHueOffset = source.BeatHueOffset;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
