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
    /// Wall units panned per beat before musical pacing. Four preserves the wall-approved
    /// neutral-pace drift without coupling motion to the source image.
    /// </summary>
    private const float SyncPanWallUnitsPerBeat = 4f;

    /// <summary>Mirror2 motion calibration chosen on the wall.</summary>
    private const float SyncMirror2MotionScale = 2f;

    /// <summary>Mirror10 motion calibration chosen on the wall.</summary>
    private const float SyncMirror10MotionScale = 2f;

    /// <summary>
    /// Kaleidoscope rotation in radians per beat before musical pacing — about eleven
    /// degrees.
    /// </summary>
    private const float SyncRotationRadiansPerBeat = 0.2f;

    /// <summary>Low Energy pace slows the base motion while keeping the effect visibly alive.</summary>
    private const float SyncEnergyPaceLow = 0.75f;

    /// <summary>High Energy pace accelerates the base motion; Mid remains neutral at the range midpoint.</summary>
    private const float SyncEnergyPaceHigh = 1.5f;

    /// <summary>
    /// Upper editor rail matches the wall-tuned High Energy pace.
    /// </summary>
    private const float SyncEnergyPaceHighRail = 1.5f;

    /// <summary>
    /// Normalized Low threshold where bass presence begins contributing to the On-Beat Push. Levels
    /// are track-relative, so this remains a live tuning knob instead of an absolute-loudness claim.
    /// </summary>
    private const float SyncLowPresenceThreshold = 0.25f;

    /// <summary>
    /// Pace added where the continuous wire beat pulse reaches one after the Normalized Low
    /// gate. The gate product rarely nears one — the threshold remap discounts it — so strength
    /// calibrates in whole units; seven reads as clear acceleration above the Energy-paced base.
    /// </summary>
    private const float SyncOnBeatPushStrength = 7f;

    /// <summary>
    /// Minimum saturation applied to the shared-palette read in Synced mono mode. Every
    /// stage-directed activation starts a three-second RGB palette crossfade, and some palette
    /// pairs pass through gray mid-fade; the mono recombination would paint that gray across the
    /// whole footprint as a near-white wall. The floor keeps those moments tinted instead.
    /// </summary>
    private const float SyncPaletteSaturationFloor = 0.3f;

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    private const float SyncBeatHueOffset = 0.5f;

    /// <summary>Window in whole beats across which the Drop approach freeze and landing burst decay.</summary>
    private const int SyncDropSlowdownBeats = 8;

    // Runtime mechanism constants

    /// <summary>Reference frame rate converting Standalone's authored per-frame motion into delta-time motion.</summary>
    private const float ReferenceFrameRate = 60f;

    /// <summary>
    /// Pace added at the Drop landing, decaying to zero across the slowdown window. Additive so the
    /// landing displaces the same wall distance in every Energy state; 4.5 reproduces the prior
    /// 5x-of-Mid-pace landing magnitude.
    /// </summary>
    private const float SyncDropBurstPace = 4.5f;

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
        PanWallUnitsPerBeat = SyncPanWallUnitsPerBeat,
        Mirror2MotionScale = SyncMirror2MotionScale,
        Mirror10MotionScale = SyncMirror10MotionScale,
        RotationRadiansPerBeat = SyncRotationRadiansPerBeat,
        EnergyPace = new FloatRange(
            SyncEnergyPaceLow,
            SyncEnergyPaceHigh,
            SyncEnergyPaceLow,
            SyncEnergyPaceHighRail),
        LowPresenceThreshold = SyncLowPresenceThreshold,
        OnBeatPushStrength = SyncOnBeatPushStrength,
        PaletteSaturationFloor = SyncPaletteSaturationFloor,
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

    /// <summary>Whether the activation's mirror-layout coin selected Mirror2 rather than Mirror10.</summary>
    private bool usesMirror2;

    List<picture> colorTex = new List<picture>();
    List<picture> monoTex = new List<picture>();
    Texture2D currentTex;

    /// <summary>
    /// The channel-swapped copy owned by the current activation, or null when the pick renders
    /// straight from the pool. Script-created textures are never garbage-collected, so this
    /// reference is what the next activation destroys.
    /// </summary>
    private Texture2D ownedTex;

    /// <summary>Name of the mirror layout rolled for this activation, shown in the debug readout.</summary>
    private string debugMirrorName = "";
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
    /// <summary>Mode the last Roll determined its values under; a differing live mode re-rolls.</summary>
    bool rolledSynced;
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
    /// Returns cheap live tuning text for the Controller debug display: file, pool, mirror layout,
    /// wire beat pulse, Normalized Low, and the resulting On-Beat Push.
    /// </summary>
    public override string DebugText()
    {
        float pulse = beatManager.Pulses.Beat;
        float low = beatManager.Levels.Normalized.Low;
        float push = beatManager.IsSynced ? ReadOnBeatPush() : 0f;
        return $"file {fname} {(mode == 0 ? "color" : "mono")} {debugMirrorName} " +
            $"pulse {pulse:F2} low {low:F2} push {push:F2} ";
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
        if (ownedTex != null)
        {
            UnityEngine.Object.Destroy(ownedTex);
            ownedTex = null;
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
        usesMirror2 = Random.Range(0, 2) == 0;
        mirrorList = usesMirror2
            ? penrose.Layout.shapes.Mirror2
            : penrose.Layout.shapes.Mirror10;
        debugMirrorName = usesMirror2 ? "mirror2" : "mirror10";

        int colorCount = colorTex.Count;
        int monoCount = monoTex.Count;
        int total = colorCount + monoCount;
        bool isSynced = beatManager.IsSynced;
        rolledSynced = isSynced;
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
                ownedTex = messTexture(currentTex, channelSwapSelectorMaxExclusive);
                currentTex = ownedTex;
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
    /// <remarks>
    /// In Synced Mode, Energy pace, the gated On-Beat Push, and the additive Drop landing burst
    /// combine into one motion scale; the current mirror layout's live calibration and the Drop
    /// approach freeze scale that whole rate before it drives pan and rotation. <c>PanWallUnitsPerBeat</c> then moves the sampling position in screen-buffer pixels,
    /// independent of source dimensions, while sampling remains one source texel per screen-buffer
    /// pixel so image presentation stays unchanged. A mode change mid-activation re-rolls the
    /// Effect, so each mode's law always runs on values its own Roll determined. Musical meanings are defined by the Data Surface,
    /// Energy, and Levels entries in <c>CONTEXT.md</c>; timing and pulse lanes are defined in
    /// <c>docs/osc-client-contract.md</c>.
    /// </remarks>
    public override void Draw()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            Init();
        }
        // The Roll bakes mode-conditional values (direction-only magnitudes under Synced Mode),
        // so a mode change mid-activation re-rolls instead of running one mode's law on the
        // other mode's values.
        if (beatManager.IsSynced != rolledSynced)
        {
            OnStart();
        }
        float rhythm = waveform.Envelope;
        float beatHue = SyncSettings.BeatHueOffset * rhythm;
        float rotationDelta;
        if (beatManager.IsSynced)
        {
            float beatSeconds = beatManager.Timing.BeatAverageMilliseconds.Value / 1000f;
            float mirrorMotionScale = usesMirror2
                ? SyncSettings.Mirror2MotionScale
                : SyncSettings.Mirror10MotionScale;
            // Drop gesture: the approach freeze scales the whole movement, while the landing burst
            // is an additive pace term so the landing displaces the same wall distance in every
            // Energy state. Both envelopes rest at no-effect outside a Drop.
            float dropFreeze = beatManager.Drop.Before.Decay(SyncSettings.DropSlowdownBeats);
            float dropBurst = SyncDropBurstPace
                * beatManager.Drop.In.Decay(SyncSettings.DropSlowdownBeats);
            float motionScale = (ReadEnergyPace() + ReadOnBeatPush() + dropBurst)
                * mirrorMotionScale * dropFreeze / beatSeconds;
            float panWallDelta = SyncSettings.PanWallUnitsPerBeat * motionScale * effectDelta;
            positionX += motionX * panWallDelta;
            positionY += motionY * panWallDelta;
            rotationDelta = aspeed * SyncSettings.RotationRadiansPerBeat * motionScale * effectDelta;
        }
        else
        {
            positionX += motionX * effectDelta * ReferenceFrameRate;
            positionY += motionY * effectDelta * ReferenceFrameRate;
            rotationDelta = aspeed * effectDelta * ReferenceFrameRate;
        }

        // Synced floors the mono palette read's saturation: every stage-directed activation
        // starts a three-second RGB palette crossfade, and gray-passing pairs would wash the
        // footprint near-white. Standalone keeps the historical unfloored read.
        float paletteSaturationFloor = beatManager.IsSynced ? SyncSettings.PaletteSaturationFloor : 0f;
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
                // Apply rotation at one source texel per screen-buffer pixel.
                double x2 = (m11 * x1) + (m12 * y1);
                double y2 = (m21 * x1) + (m22 * y1);
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
                {
                    // The palette supplies hue and saturation only; the mono image keeps its own
                    // brightness, so the picture's structure reads under any palette, dark ones included.
                    Color palette = APalette.read(color.r % 1f, true);
                    if (paletteSaturationFloor > 0f)
                        palette = palette.MinSaturation(paletteSaturationFloor);
                    color = palette.WithBrightness(color.r);
                }
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
                buffer[group[j]] = tileColor;
            }
        }
        if (beatManager.Fill.Active)
        {
            // Fill drains color from every tile on the wall while holding its Rec.709 relative
            // luminance. The monotonic transfer preserves perceived brightness, so image
            // definition survives across every source. The pass runs on the whole buffer, after
            // mirror replication, because the mirror groups do not cover the wall — Mirror2
            // leaves its eight centerline tiles outside every group, and a group-loop treatment
            // left them colored on an otherwise black-and-white wall.
            for (int i = 0; i < Penrose.Total; i++)
            {
                Color linearColor = buffer[i].linear;
                float luminance = (0.2126f * linearColor.r) +
                    (0.7152f * linearColor.g) +
                    (0.0722f * linearColor.b);
                float gray = Mathf.LinearToGammaSpace(luminance);
                // Full desaturation defines the black-and-white Fill treatment, so zero stays structural.
                buffer[i] = new Color(gray, gray, gray);
            }
        }
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

    /// <summary>Returns the continuous wire beat pulse scaled by track-relative Normalized Low presence.</summary>
    /// <remarks>
    /// The wire pulse is a triangle: one on each beat, zero halfway to the next beat, then rising
    /// back to one. Multiplying that continuous shape by thresholded Normalized Low produces the
    /// authored beat-synchronous push; it is not a one-shot trigger. Musical meanings:
    /// <c>CONTEXT.md</c> entries Duration Pulse / Duration Gate (the wire <c>beat_pulse</c>
    /// offering) and Levels; wire lanes: <c>docs/osc-client-contract.md</c>
    /// <c>/rave/onair/beat_pulse</c> and <c>/rave/onair/levels</c>.
    /// </remarks>
    private float ReadOnBeatPush()
    {
        float lowPresence = beatManager.Levels.Normalized.Low.Remap(
            SyncSettings.LowPresenceThreshold,
            1f,
            0f,
            1f,
            clamp: true);
        return beatManager.Pulses.Beat *
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

    /// <summary>Screen-buffer pixels panned per beat before musical pacing.</summary>
    [Tooltip("Wall units panned per beat before Energy pace and On-Beat Push. One wall unit is one pixel in Kscope's 50x22 screen buffer; 10 moves ten wall pixels at motion scale 1.")]
    [Min(0f)] public float PanWallUnitsPerBeat;

    /// <summary>Motion calibration applied while Mirror2 is active.</summary>
    [Tooltip("Multiplier applied to Energy pace and On-Beat Push while Mirror2 is active. 1 is neutral; tune until motion reads like Mirror10.")]
    [Min(0f)] public float Mirror2MotionScale;

    /// <summary>Motion calibration applied while Mirror10 is active.</summary>
    [Tooltip("Multiplier applied to Energy pace and On-Beat Push while Mirror10 is active. 1 is neutral; tune until motion reads like Mirror2.")]
    [Min(0f)] public float Mirror10MotionScale;

    /// <summary>Kaleidoscope rotation in radians per beat before musical pacing.</summary>
    [Tooltip("Radians rotated per beat before Energy pace and On-Beat Push. 0.3 is about 17 degrees per beat; 0 stops rotation.")]
    [Min(0f)] public float RotationRadiansPerBeat;

    /// <summary>Low-to-High Energy pace range; Mid uses the midpoint.</summary>
    [Tooltip("Motion pace by Energy: Low = Min, Mid = midpoint, High = Max. A value of 1 is neutral; the range carries its own tuning Rails.")]
    public FloatRange EnergyPace = new FloatRange();

    /// <summary>Normalized Low threshold where bass presence begins opening the On-Beat Push.</summary>
    [Tooltip("Track-relative Normalized Low level where the On-Beat Push begins. Raise it when non-bass material triggers the push; lower it when bass hits do not open it.")]
    [Range(0f, 1f)] public float LowPresenceThreshold;

    /// <summary>Pace added where the continuous wire beat pulse reaches one after the Normalized Low gate.</summary>
    [Tooltip("Pace added where the continuous wire beat pulse reaches 1 after the Normalized Low gate. 0 disables the push; raise it for stronger beat-synchronous acceleration.")]
    [Min(0f)] public float OnBeatPushStrength;

    /// <summary>Minimum saturation applied to the shared-palette read in mono mode.</summary>
    [Tooltip("Floor on the palette read's saturation in mono mode. Keeps a mid-crossfade gray palette from washing the wall white; 0 disables, higher forces stronger tinting.")]
    [Range(0f, 1f)] public float PaletteSaturationFloor;

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    [Range(0f, 1f)] public float BeatHueOffset;

    /// <summary>Window in whole beats across which the Drop approach freeze and landing burst decay.</summary>
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
        PanWallUnitsPerBeat = source.PanWallUnitsPerBeat;
        Mirror2MotionScale = source.Mirror2MotionScale;
        Mirror10MotionScale = source.Mirror10MotionScale;
        RotationRadiansPerBeat = source.RotationRadiansPerBeat;
        EnergyPace = new FloatRange(
            source.EnergyPace.Min,
            source.EnergyPace.Max,
            source.EnergyPace.LowRail,
            source.EnergyPace.HighRail);
        LowPresenceThreshold = source.LowPresenceThreshold;
        OnBeatPushStrength = source.OnBeatPushStrength;
        PaletteSaturationFloor = source.PaletteSaturationFloor;
        BeatHueOffset = source.BeatHueOffset;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
