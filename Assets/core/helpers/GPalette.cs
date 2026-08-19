using System;
using System.IO;

using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/*
 * Gradient palette - Chuck Sommerville
 * given a palette table, this will return a value within
 * 0f will return the first entry, 1f will return entry length-1
 * If hard is true then there is no Lerping
 */

/// <summary>
/// Artistic controls for deriving an effect-local palette with balanced luminance and evenly
/// distributed colour change while retaining the source palette's colour family.
/// </summary>
[Serializable]
public struct PaletteConditioning
{
    /// <summary>
    /// Working relative luminance every conditioned palette is moved toward. This is an absolute
    /// target rather than the source palette's own mean, so a palette authored dark reaches the same
    /// working band as one authored bright instead of being equalized to its own darkness.
    /// </summary>
    [Range(0.05f, 0.9f)] public float TargetLuminance;

    /// <summary>
    /// Relative-luminance floor no conditioned entry falls below. A saturated hue whose channels
    /// clip before reaching the floor stops at its own ceiling; pure blue peaks near 0.072, so the
    /// floor is a goal rather than a guarantee while saturation is taken as authored.
    /// </summary>
    [Range(0f, 0.5f)] public float MinimumLuminance;

    /// <summary>Amount each visible colour's relative luminance moves toward <see cref="TargetLuminance"/>.</summary>
    [Range(0f, 1f)] public float LuminanceEqualization;

    /// <summary>
    /// Hue spread at which <see cref="LuminanceEqualization"/> reaches full strength. Below it,
    /// equalization backs off proportionally: a palette whose entries share one hue is separated by
    /// brightness alone, and equalizing that brightness away would erase the only distinction it has.
    /// </summary>
    [Range(0.05f, 1f)] public float HueSpreadReference;

    /// <summary>Upper bound on uniform RGB lift when equalizing a low-luminance colour.</summary>
    [Min(1f)] public float MaximumLuminanceScale;

    /// <summary>
    /// Relative-luminance threshold below which an entry is rebuilt at the working target, borrowing
    /// neighbouring hue when necessary so black never becomes colourless grey.
    /// </summary>
    [Range(0.001f, 0.25f)] public float DarkLuminanceThreshold;

    /// <summary>Combined hue, saturation, and luminance distance below which a consecutive entry collapses into its run.</summary>
    [Range(0f, 0.5f)] public float DuplicateThreshold;

    /// <summary>Amount palette positions move from equal anchor spacing toward equal colour-distance spacing.</summary>
    [Range(0f, 1f)] public float HueRedistribution;

    /// <summary>Returns whether every conditioning control exactly matches another live settings value.</summary>
    /// <param name="other">The value to compare without boxing or heap allocation.</param>
    public readonly bool Matches(PaletteConditioning other) =>
        TargetLuminance == other.TargetLuminance &&
        MinimumLuminance == other.MinimumLuminance &&
        LuminanceEqualization == other.LuminanceEqualization &&
        HueSpreadReference == other.HueSpreadReference &&
        MaximumLuminanceScale == other.MaximumLuminanceScale &&
        DarkLuminanceThreshold == other.DarkLuminanceThreshold &&
        DuplicateThreshold == other.DuplicateThreshold &&
        HueRedistribution == other.HueRedistribution;
}

/// <summary>
/// Static palette sampling helpers for discrete color palettes.
/// </summary>
public class GPalette
{
    public Color[] values;
    public int length;
    public bool blend;
    public string paletteSources;

    /// <summary>
    /// Mean relative luminance of the immutable palette table, cached when the table is populated so
    /// effect-local conditioning never recomputes a source statistic.
    /// </summary>
    public float MeanRelativeLuminance { get; private set; }

    /// <summary>
    /// Saturation-weighted circular spread of the immutable palette's hues, cached beside
    /// <see cref="MeanRelativeLuminance"/> when the table is populated. Zero means every entry sits on
    /// one hue and only brightness tells the entries apart; one means the hues fill the wheel.
    /// Weighting by saturation keeps a near-grey entry from voting on a hue it barely has.
    /// </summary>
    public float HueSpread { get; private set; }

    // init function
    /// <summary>
    /// Copies source colors into the fixed palette table and caches the two source statistics
    /// conditioning reads: mean relative luminance and hue spread.
    /// </summary>
    private void Populate(Color[] initialvalues, bool blendtype = false)
    {
        blend = blendtype;
        values = initialvalues;
        length = values.Length;

        float luminanceTotal = 0f;
        float hueX = 0f;
        float hueY = 0f;
        float saturationTotal = 0f;
        for (int i = 0; i < length; i++)
        {
            luminanceTotal += values[i].RelativeLuminance();
            Color.RGBToHSV(values[i], out float hue, out float saturation, out _);
            float radians = hue * 2f * Mathf.PI;
            hueX += saturation * Mathf.Cos(radians);
            hueY += saturation * Mathf.Sin(radians);
            saturationTotal += saturation;
        }
        MeanRelativeLuminance = luminanceTotal / length;
        HueSpread = saturationTotal > 0f
            ? 1f - (Mathf.Sqrt((hueX * hueX) + (hueY * hueY)) / saturationTotal)
            : 0f;
    }

    // array constructor
    /// <summary>
    /// Creates a palette from explicit Color entries.
    /// </summary>
    public GPalette(Color[] initialvalues, bool blendtype = false)
    {
        Populate(initialvalues, blendtype);
    }

    /// <summary>
    /// Parses a comma-separated numeric RGB list into Color values.
    /// </summary>
    private Color[] listFromString(string list)
    {
        string[] colors = list.Split(',');
        length = colors.Length;
        Color[] values = new Color[length];
        for (int i = 0; i < length; i++)
        {
            uint raw = uint.Parse(colors[i], System.Globalization.NumberStyles.AllowHexSpecifier);
            values[i] = new Color32((byte)(raw >> 16), (byte)(raw >> 8), (byte)(raw), 0);
        }
        return values;
    }

    // string constructor
    /// <summary>
    /// Creates a palette from a serialized RGB component list.
    /// </summary>
    public GPalette(string list, bool blendtype = false)
    {
        Color[] values = listFromString(list);
        Populate(values, blendtype);
    }

    /// <summary>
    /// Returns a new effect-local palette whose visible entries are luminance-balanced, whose dark
    /// entries borrow useful neighbouring colour, and whose distinct colour movement is redistributed
    /// across the cyclic table. The source palette and its table are never mutated.
    /// </summary>
    /// <param name="conditioning">The caller-owned artistic controls for this derived palette.</param>
    public GPalette Conditioned(PaletteConditioning conditioning)
    {
        // Equalization earns its full strength only from a palette whose hues already do the
        // separating. A single-hue palette is told apart by brightness alone, so flattening its
        // brightness would leave one colour where the caller needs several.
        float equalization = Mathf.Clamp01(conditioning.LuminanceEqualization) *
            Mathf.Clamp01(HueSpread / Mathf.Max(0.001f, conditioning.HueSpreadReference));

        // One uniform scale first carries the whole palette into the working band. Being uniform it
        // preserves every relative luminance the palette was authored with; only the differential
        // equalization below changes those relationships.
        float paletteLift = MeanRelativeLuminance > 0f
            ? Mathf.Min(
                conditioning.TargetLuminance / MeanRelativeLuminance,
                Mathf.Max(1f, conditioning.MaximumLuminanceScale))
            : 1f;

        Color[] balanced = new Color[length];
        for (int i = 0; i < length; i++)
        {
            Color source = values[i];
            float luminance = source.RelativeLuminance();
            balanced[i] = luminance <= conditioning.DarkLuminanceThreshold
                ? RepairDarkColor(i, conditioning)
                : EqualizeLuminance(source, luminance, equalization, paletteLift, conditioning);
        }

        List<Color> anchors = CollapseNearDuplicates(
            balanced,
            conditioning.DuplicateThreshold);
        Color[] redistributed = Redistribute(
            anchors,
            length,
            conditioning.HueRedistribution);
        return new GPalette(redistributed, blend);
    }

    /// <summary>
    /// Moves a visible colour into the working luminance band by one uniform RGB scale, preserving hue
    /// and HSV saturation exactly while bounding amplification and holding the floor.
    /// </summary>
    /// <param name="source">The immutable source entry, never mutated.</param>
    /// <param name="luminance">The source entry's relative luminance, already measured by the caller.</param>
    /// <param name="equalization">Equalization strength after the caller scaled it by palette hue spread.</param>
    /// <param name="paletteLift">The uniform scale carrying the whole palette's mean to the target.</param>
    /// <param name="conditioning">The caller-owned artistic controls for this derived palette.</param>
    private Color EqualizeLuminance(
        Color source,
        float luminance,
        float equalization,
        float paletteLift,
        PaletteConditioning conditioning)
    {
        float targetLuminance = Mathf.Lerp(
            luminance * paletteLift,
            conditioning.TargetLuminance,
            equalization);
        targetLuminance = Mathf.Max(targetLuminance, conditioning.MinimumLuminance);

        float scale = targetLuminance / luminance;
        scale = Mathf.Min(scale, Mathf.Max(1f, conditioning.MaximumLuminanceScale));

        // A colour whose brightest channel is already near full cannot be scaled further without
        // clipping that channel, which would shift hue. Such an entry stops short of the target
        // instead, and a deeply saturated hue can stop short of the floor for the same reason.
        float maximumChannel = Mathf.Max(source.r, Mathf.Max(source.g, source.b));
        scale = Mathf.Min(scale, 1f / maximumChannel);
        return new Color(
            source.r * scale,
            source.g * scale,
            source.b * scale,
            source.a);
    }

    /// <summary>
    /// Rebuilds a black or near-black entry at the working luminance band. Coloured entries retain their
    /// own hue and saturation; colourless entries borrow a cyclic interpolation of their visible
    /// neighbours, so black never becomes grey.
    /// </summary>
    /// <param name="sourceIndex">Index of the dark entry in the immutable source table.</param>
    /// <param name="conditioning">The caller-owned artistic controls for this derived palette.</param>
    private Color RepairDarkColor(int sourceIndex, PaletteConditioning conditioning)
    {
        Color source = values[sourceIndex];
        Color.RGBToHSV(source, out float hue, out float saturation, out float value);
        if (saturation <= 0.05f || value <= 0.0001f)
        {
            BorrowNeighbourHue(
                sourceIndex,
                conditioning.DarkLuminanceThreshold,
                out hue,
                out saturation);
        }

        Color vivid = Color.HSVToRGB(hue, saturation, 1f);
        float targetLuminance = Mathf.Max(
            conditioning.TargetLuminance,
            conditioning.MinimumLuminance);
        float scale = Mathf.Min(
            targetLuminance / vivid.RelativeLuminance(),
            1f);
        return new Color(
            vivid.r * scale,
            vivid.g * scale,
            vivid.b * scale,
            source.a);
    }

    /// <summary>
    /// Finds the nearest useful colour on each cyclic side of a colourless dark entry and blends
    /// their hues across the intervening run.
    /// </summary>
    private void BorrowNeighbourHue(
        int sourceIndex,
        float darkLuminanceThreshold,
        out float hue,
        out float saturation)
    {
        bool hasPrevious = false;
        bool hasNext = false;
        float previousHue = 0f;
        float previousSaturation = 0f;
        float nextHue = 0f;
        float nextSaturation = 0f;
        int previousDistance = 0;
        int nextDistance = 0;

        for (int distance = 1; distance < length && (!hasPrevious || !hasNext); distance++)
        {
            if (!hasPrevious)
            {
                int previousIndex = (sourceIndex - distance + length) % length;
                hasPrevious = TryReadHueDonor(
                    values[previousIndex],
                    darkLuminanceThreshold,
                    out previousHue,
                    out previousSaturation);
                if (hasPrevious)
                {
                    previousDistance = distance;
                }
            }

            if (!hasNext)
            {
                int nextIndex = (sourceIndex + distance) % length;
                hasNext = TryReadHueDonor(
                    values[nextIndex],
                    darkLuminanceThreshold,
                    out nextHue,
                    out nextSaturation);
                if (hasNext)
                {
                    nextDistance = distance;
                }
            }
        }

        if (hasPrevious && hasNext)
        {
            float amount = previousDistance / (float)(previousDistance + nextDistance);
            hue = LerpHue(previousHue, nextHue, amount);
            saturation = Mathf.Lerp(previousSaturation, nextSaturation, amount);
            return;
        }

        if (hasPrevious)
        {
            hue = previousHue;
            saturation = previousSaturation;
            return;
        }

        if (hasNext)
        {
            hue = nextHue;
            saturation = nextSaturation;
            return;
        }

        hue = 0f;
        saturation = 1f;
    }

    /// <summary>Reads hue and saturation from a visible, chromatic colour suitable for repairing a dark neighbour.</summary>
    private static bool TryReadHueDonor(
        Color color,
        float darkLuminanceThreshold,
        out float hue,
        out float saturation)
    {
        Color.RGBToHSV(color, out hue, out saturation, out _);
        return color.RelativeLuminance() > darkLuminanceThreshold && saturation > 0.05f;
    }

    /// <summary>Interpolates between two hues along the shortest cyclic path.</summary>
    private static float LerpHue(float from, float to, float amount)
    {
        float delta = Mathf.Repeat(to - from + 0.5f, 1f) - 0.5f;
        return Mathf.Repeat(from + (delta * amount), 1f);
    }

    /// <summary>
    /// Collapses consecutive near-duplicate entries into one anchor while retaining gradual runs once
    /// their accumulated colour change clears the caller's threshold.
    /// </summary>
    private static List<Color> CollapseNearDuplicates(
        Color[] colors,
        float duplicateThreshold)
    {
        float threshold = Mathf.Max(0f, duplicateThreshold);
        var anchors = new List<Color>(colors.Length) { colors[0] };
        for (int i = 1; i < colors.Length; i++)
        {
            if (PaletteDistance(anchors[anchors.Count - 1], colors[i]) >= threshold)
            {
                anchors.Add(colors[i]);
            }
        }

        if (anchors.Count > 1 && PaletteDistance(anchors[anchors.Count - 1], anchors[0]) < threshold)
        {
            anchors.RemoveAt(anchors.Count - 1);
        }
        return anchors;
    }

    /// <summary>
    /// Measures cyclic hue change, saturation change, and relative-luminance change in one normalized
    /// distance used for duplicate collapse and colour-path redistribution.
    /// </summary>
    private static float PaletteDistance(Color a, Color b)
    {
        Color.RGBToHSV(a, out float hueA, out float saturationA, out _);
        Color.RGBToHSV(b, out float hueB, out float saturationB, out _);
        float hueDistance = Mathf.Abs(hueA - hueB);
        hueDistance = Mathf.Min(hueDistance, 1f - hueDistance) * 2f;
        hueDistance *= Mathf.Min(saturationA, saturationB);

        float saturationDistance = Mathf.Abs(saturationA - saturationB);
        float luminanceDistance = Mathf.Abs(
            a.RelativeLuminance() - b.RelativeLuminance());
        return Mathf.Sqrt(
            (hueDistance * hueDistance) +
            (0.25f * saturationDistance * saturationDistance) +
            (luminanceDistance * luminanceDistance));
    }

    /// <summary>
    /// Resamples the cyclic anchor path into a smooth table. Full redistribution gives each palette
    /// coordinate equal colour travel; because relative luminance is linear in RGB, interpolation
    /// between balanced anchors keeps the same balanced luminance path without another correction pass.
    /// </summary>
    private static Color[] Redistribute(
        List<Color> anchors,
        int outputLength,
        float hueRedistribution)
    {
        Color[] output = new Color[outputLength];
        if (anchors.Count == 1)
        {
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = anchors[0];
            }
            return output;
        }

        float[] rawDistances = new float[anchors.Count];
        float rawTotal = 0f;
        for (int i = 0; i < anchors.Count; i++)
        {
            rawDistances[i] = PaletteDistance(
                anchors[i],
                anchors[(i + 1) % anchors.Count]);
            rawTotal += rawDistances[i];
        }

        float averageDistance = rawTotal / anchors.Count;
        float redistribution = Mathf.Clamp01(hueRedistribution);
        float[] segmentLengths = new float[anchors.Count];
        float pathLength = 0f;
        for (int i = 0; i < anchors.Count; i++)
        {
            float normalizedDistance = averageDistance > 0f
                ? rawDistances[i] / averageDistance
                : 1f;
            segmentLengths[i] = Mathf.Lerp(1f, normalizedDistance, redistribution);
            pathLength += segmentLengths[i];
        }

        int segment = 0;
        float segmentStart = 0f;
        float segmentEnd = segmentLengths[0];
        for (int i = 0; i < output.Length; i++)
        {
            float target = i / (float)output.Length * pathLength;
            while (segment < anchors.Count - 1 && target > segmentEnd)
            {
                segmentStart = segmentEnd;
                segment++;
                segmentEnd += segmentLengths[segment];
            }

            float segmentLength = segmentLengths[segment];
            float amount = segmentLength > 0f
                ? (target - segmentStart) / segmentLength
                : 0f;
            output[i] = Color.Lerp(
                anchors[segment],
                anchors[(segment + 1) % anchors.Count],
                amount);
        }
        return output;
    }

    // random constructor
    // general read
    /// <summary>
    /// Samples the palette at normalized position i, optionally interpolating between entries.
    /// </summary>
    public Color read(float i, bool doblend = false)
    {
        // check looping
        if (doblend)
            blend = true;
        if (i < 0f)
        {
            float bottom = Mathf.Floor(i);
            i -= bottom;
        }
        i %= 1f;
        // check boundries
        if (i <= 0)
            return values[0];
        if (i >= (length - 1))
            return values[length - 1];
        // find color in list
        if (length > 1)
        {
            float scaled = i * (float)(length - 1);
            int first = Mathf.FloorToInt(scaled);
            float fract = scaled % 1f;
            if (!blend)
                return (fract < 0.5f) ? values[first] : values[first + 1];
            return Color.Lerp(values[first], values[first + 1], fract);
        }

        return new Color(0f, 0f, 0f);
    }

    /// <summary>
    /// Samples a normalized cyclic position, including the interval from the final palette entry
    /// back to the first, without changing the established linear behavior of <see cref="read"/>.
    /// </summary>
    /// <param name="i">The cyclic palette coordinate, wrapped into the normalized domain.</param>
    /// <param name="doblend">Whether this read requests interpolation between adjacent entries.</param>
    /// <returns>The palette color at the wrapped cyclic coordinate.</returns>
    public Color ReadCyclic(float i, bool doblend = false)
    {
        // Deliberately does not latch `blend` the way read() does. That latch makes the first
        // blended caller switch every other effect sharing this palette to blended sampling for
        // the rest of the run, so a new entry point does not get to spread it further.
        bool useBlend = doblend || blend;

        if (i < 0f || i >= 1f)
        {
            i = Mathf.Repeat(i, 1f);
        }
        if (i <= 0f || length == 1)
            return values[0];

        float scaled = i * length;
        int first = Mathf.FloorToInt(scaled);
        int second = (first + 1) % length;
        float fract = scaled % 1f;
        if (!useBlend)
            return fract < 0.5f ? values[first] : values[second];
        return Color.Lerp(values[first], values[second], fract);
    }


}

/// <summary>
/// Shared animated palette state that fades to a randomly selected palette when asked. A palette
/// is picked at a Roll and holds until the next one: Change is the only mover, so nothing hops
/// palettes mid-activation on its own.
/// </summary>
public class AnimPalette
{
    List<string> names = new List<string>();
    List<GPalette> palettes = new List<GPalette>();
    GPalette current = null;
    GPalette next = null;
    float tween = 0;
    const float transitionTime = 3f;

    /// <summary>
    /// Monotonic signal incremented whenever the current/next palette endpoint state changes, so
    /// effect-local derived palettes can refresh without inspecting palette contents per frame.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>The source palette at the current side of the animated fade.</summary>
    public GPalette CurrentPalette => current;

    /// <summary>The source palette at the destination side of the fade, or null before the first change.</summary>
    public GPalette NextPalette => next;

    /// <summary>Whether the shared palette is currently inside its three-second cross-fade.</summary>
    public bool IsTransitioning => tween > 0f;

    /// <summary>Normalized progress from the current palette to the next palette.</summary>
    public float TransitionProgress => IsTransitioning
        ? 1f - (tween / transitionTime)
        : 1f;

    public static string[] StaticSamples =
    {
        "ff0000,000000,ffff00,000000,00ff00,000000,00ffff,000000,0000ff,000000,ff00ff,000000",
        "ffff00,000000,00ffff,000000,ff00ff,000000",
        "ff0000,000000,00ff00,000000,0000ff,000000",
        "ff0000,ffff00,00ff00,00ffff,0000ff,00ffff",
        "5500AB,84007C,B5004B,E5001B,E81700,B84700,AB7700,ABAB00,AB5500,DD2200,F2000E,C2003E,8F0071,5F00A1,2F00D0,0007F9",
        "000000,330000,660000,990000,CC0000,FF0000,FF3300,FF6600,FF9900,FFCC00,FFFF00,FFFF33,FFFF66,FFFF99,FFFFCC,FFFFFF",
    };

    /// <summary>Loads the built-in fallback palettes.</summary>
    public AnimPalette()
        : this(string.Empty)
    {
    }

    /// <summary>Loads palette definitions from Controller-owned palette source text.</summary>
    public AnimPalette(string palettedata)
    {
        // built in palettes
        // leadables
        /*
        string liststring = readfile("filelist.txt");
        liststring = liststring.Replace("\r", "");
        liststring = liststring.Replace(" ", "");
        string[] filelist = liststring.Split('\n');
        for (int i = 0; i < filelist.Length; i++)
        {
            LoadGradientFile(filelist[i]);
            LoadHexGradientFile(filelist[i]);
        }
        */
        if (!string.IsNullOrEmpty(palettedata))
        {
            processhex(palettedata);
            processgradient(palettedata);
        }
        // if no loadable palettes, load default palettes
        if (palettes.Count == 0)
        {
            for (int i = 0; i < StaticSamples.Length; i++)
            {
                palettes.Add(new GPalette(StaticSamples[i]));
            }

        }

        //        LoadFromFile("palettedata.txt");
        //        LoadFromFile("jenpalettes.txt");
        current = palettes[0];
    }
    /// <summary>
    /// Starts a transition toward a randomly selected loaded palette and advances the shared revision
    /// so effect-local conditioned copies can prepare both fade endpoints once.
    /// </summary>
    public void Change()
    {
        if (tween == 0f)
        {
            next = palettes[Random.Range(0, palettes.Count)];
            tween = transitionTime;
            Revision++;
        }
    }

    /// <summary>
    /// Advances the animated palette fade toward the most recently requested palette.
    /// </summary>
    public void Update()
    {
        if (tween > 0f)
        {
            tween -= Time.deltaTime;
            if (tween <= 0f)
            {
                current = next;
                tween = 0f;
                Revision++;
            }
        }
    }

    /// <summary>
    /// Samples the currently animated palette at normalized position i. The doblend request is
    /// forwarded on both paths: dropping it on the steady-state path made callers' blended reads
    /// silently degrade to stepped colors whenever no palette transition was running.
    /// </summary>
    public Color read(float i, bool doblend = false)
    {
        if (tween == 0f)
            return current.read(i, doblend);
        else
            return Color.Lerp(next.read(i, doblend), current.read(i, doblend), tween / transitionTime);
    }

    /// <summary>
    /// Samples the live animated palette cyclically, preserving the active cross-fade while the
    /// final palette entry interpolates back to the first across the wrapping coordinate seam.
    /// </summary>
    /// <param name="i">The cyclic palette coordinate, wrapped into the normalized domain.</param>
    /// <param name="doblend">Whether this read requests interpolation between adjacent entries.</param>
    /// <returns>The live cross-faded palette color at the wrapped cyclic coordinate.</returns>
    public Color ReadCyclic(float i, bool doblend = false)
    {
        if (tween == 0f)
            return current.ReadCyclic(i, doblend);
        else
            return Color.Lerp(
                next.ReadCyclic(i, doblend),
                current.ReadCyclic(i, doblend),
                tween / transitionTime);
    }

    /// <summary>
    /// Reads a text file from StreamingAssets.
    /// </summary>
    private string readfile(string fileName)
    {
        try
        {
            var sr = new StreamReader(Application.streamingAssetsPath + "/" + fileName);
            string fileContents = sr.ReadToEnd();
            sr.Close();
            return fileContents;
        }
        catch (Exception)
        {
            // A missing/unreadable palette file degrades to "nothing to parse"; callers
            // treat the empty string as no palettes from this source.
            return "";
        }

    }

    /// <summary>
    /// Returns true if a parsed palette name has not already been loaded.
    /// </summary>
    public bool isNewName(string pname)
    {
        int x;
        for (x = 0; x < names.Count; x++)
        {
            if (pname.Equals(names[x]))
                return false;
        }
        names.Add(pname);
        return true;

    }

    /// <summary>
    /// Parses FastLED-style DEFINE_GRADIENT_PALETTE hex palette definitions.
    /// </summary>
    public void processhex(string fileContents)
    {
        while (true)
        {
            int def = fileContents.IndexOf("DEEFINE_HEX_PALETTE(", 0);
            if (def < 0)
                break;
            fileContents = fileContents.Substring(def + 20);
            def = fileContents.IndexOf(")", 0);
            string fn = fileContents.Substring(0, def);
            if (!isNewName(fn))
                continue;

            fileContents = fileContents.Substring(def + 1);
            // get the color info
            int begin = fileContents.IndexOf("{", 0) + 1;
            int end = fileContents.IndexOf("}", begin);
            string data = fileContents.Substring(begin, end - begin);
            palettes.Add(new GPalette(data));
        }

    }
    /// <summary>
    /// Loads and parses a hex gradient palette file from StreamingAssets.
    /// </summary>
    public void LoadHexGradientFile(string fileName)
    {
        processhex(readfile(fileName));

    }

    /// <summary>
    /// Parses plain gradient palette data into runtime palette tables.
    /// </summary>
    public void processgradient(string fileContents)
    {
        while (true)
        {
            int def = fileContents.IndexOf("DEFINE_GRADIENT_PALETTE(", 0);
            if (def < 0)
                break;
            fileContents = fileContents.Substring(def + 24);
            def = fileContents.IndexOf(")", 0);
            string fn = fileContents.Substring(0, def);
            if (!isNewName(fn))
                continue;

            fileContents = fileContents.Substring(def + 1);
            // get the color info
            int begin = fileContents.IndexOf("{", 0) + 1;
            int end = fileContents.IndexOf("}", begin);
            string data = fileContents.Substring(begin, end - begin);
            colortab[] source = data2colortab(data);
            Color[] dest = new Color[32];           // how many mapped entries
            // do the mapping
            for (int x = 0; x < dest.Length; x++)        // for each output color
            {
                float f = (float)x / (float)dest.Length;       // position in color table
                dest[x] = Map2Palette(f, source);
            }
            palettes.Add(new GPalette(dest));
        }

    }
    /// <summary>
    /// Loads and parses a plain gradient palette file from StreamingAssets.
    /// </summary>
    public void LoadGradientFile(string fileName)
    {
        processgradient(readfile(fileName));
    }

    /// <summary>
    /// Maps a normalized position through parsed gradient stops.
    /// </summary>
    private Color Map2Palette(float f, colortab[] source)
    {
        for (int y = 1; y < source.Length; y++)               // for each input pair entry
        {
            float min = source[y - 1].i;            // bracket values
            float max = source[y].i;
            if ((min <= f) && (max >= f))                 // is this in the bracket
            {
                float tween = f.Remap(min, max, 0f, 1f);
                return Color.Lerp(source[y - 1].c, source[y].c, tween);
            }
        }
        return new Color32(0, 0, 0, 0);     // default
    }

    // convert the string data for the palette into a table of fractional indexes and colors
    class colortab
    {
        public float i;
        public Color c;
    };
    /// <summary>
    /// Converts comma-separated gradient stop data into color-table entries.
    /// </summary>
    private colortab[] data2colortab(string data)
    {
        // cleanup
        data = data.Replace("\n", "");
        data = data.Replace("\r", "");
        data = data.Replace(" ", "");
        string[] subs = data.Split(',');
        // build
        colortab[] table = new colortab[subs.Length / 4];
        int x = 0;
        for (int y = 0; y < table.Length; y++)
        {
            if ((x + 3) > subs.Length)
            {
                break;
            }
            table[y] = new colortab();
            table[y].i = float.Parse(subs[x++]) / 255f;
            byte r = byte.Parse(subs[x++]);
            byte g = byte.Parse(subs[x++]);
            byte b = byte.Parse(subs[x++]);
            table[y].c = new Color32(r, g, b, 0);
        }
        return table;
    }




}

/// <summary>
/// Holds one Effect's conditioned copies of the shared animated palette endpoints and samples their
/// live cross-fade without allocating on steady frames.
/// </summary>
/// <remarks>
/// The cache is deliberately per Effect because each Effect owns independently live Standalone and
/// Sync conditioning controls. Immutable shared endpoints are reused across revision changes, so a
/// landed next endpoint rotates into current without being conditioned again. Endpoint copies are
/// re-derived only when the shared owner, its endpoint revision, or the live conditioning controls
/// change; the shared three-second fade remains a per-frame value rather than entering the cache key.
/// </remarks>
public sealed class ConditionedPaletteCache
{
    /// <summary>The shared animated palette instance from which the conditioned copies derive.</summary>
    private AnimPalette owner;

    /// <summary>The shared palette endpoint revision represented by the conditioned copies.</summary>
    private int revision = -1;

    /// <summary>The live conditioning controls represented by the conditioned copies.</summary>
    private PaletteConditioning settings;

    /// <summary>The immutable shared source represented by <see cref="currentPalette"/>.</summary>
    private GPalette currentSource;

    /// <summary>The immutable shared source represented by <see cref="nextPalette"/>.</summary>
    private GPalette nextSource;

    /// <summary>The conditioned copy of the shared current palette endpoint.</summary>
    private GPalette currentPalette;

    /// <summary>The conditioned copy of the shared next palette endpoint.</summary>
    private GPalette nextPalette;

    /// <summary>Whether the refreshed frame is inside the shared palette's three-second cross-fade.</summary>
    private bool isTransitioning;

    /// <summary>The refreshed frame's normalized progress from current to next palette.</summary>
    private float transitionProgress;

    /// <summary>
    /// Refreshes conditioned endpoint copies only when the shared owner, endpoint revision, or live
    /// conditioning controls change, while capturing the current fade position on every frame.
    /// </summary>
    /// <param name="paletteOwner">The shared animated palette whose immutable endpoints are conditioned.</param>
    /// <param name="conditioning">The Effect-owned live controls applied to both endpoints.</param>
    public void Refresh(AnimPalette paletteOwner, PaletteConditioning conditioning)
    {
        isTransitioning = paletteOwner.IsTransitioning;
        transitionProgress = paletteOwner.TransitionProgress;

        bool ownerChanged = !ReferenceEquals(paletteOwner, owner);
        bool settingsChanged = ownerChanged || !settings.Matches(conditioning);
        bool revisionChanged = ownerChanged || paletteOwner.Revision != revision;
        if (!settingsChanged && !revisionChanged)
        {
            return;
        }

        GPalette refreshedCurrentSource = paletteOwner.CurrentPalette;
        GPalette refreshedNextSource = paletteOwner.NextPalette;
        GPalette previousCurrentSource = currentSource;
        GPalette previousCurrent = currentPalette;
        GPalette previousNextSource = nextSource;
        GPalette previousNext = nextPalette;

        GPalette refreshedCurrent = settingsChanged
            ? refreshedCurrentSource.Conditioned(conditioning)
            : ReuseOrConditionPalette(
                refreshedCurrentSource,
                previousCurrentSource,
                previousCurrent,
                previousNextSource,
                previousNext,
                conditioning);
        GPalette refreshedNext = ReferenceEquals(refreshedNextSource, refreshedCurrentSource)
            ? refreshedCurrent
            : settingsChanged
                ? refreshedNextSource?.Conditioned(conditioning)
                : ReuseOrConditionPalette(
                    refreshedNextSource,
                    previousCurrentSource,
                    previousCurrent,
                    previousNextSource,
                    previousNext,
                    conditioning);

        owner = paletteOwner;
        revision = paletteOwner.Revision;
        settings = conditioning;
        currentSource = refreshedCurrentSource;
        nextSource = refreshedNextSource;
        currentPalette = refreshedCurrent;
        nextPalette = refreshedNext;
    }

    /// <summary>
    /// Samples the conditioned current endpoint cyclically and, during a shared palette transition,
    /// samples the conditioned next endpoint at the same coordinate and blends by live progress.
    /// </summary>
    /// <param name="i">The cyclic palette coordinate, wrapped into the normalized domain.</param>
    /// <param name="doblend">Whether to interpolate between adjacent entries within each endpoint.</param>
    /// <returns>The conditioned, cyclic, and cross-faded palette colour.</returns>
    public Color ReadCyclic(float i, bool doblend = false)
    {
        Color color = currentPalette.ReadCyclic(i, doblend);
        if (!isTransitioning)
        {
            return color;
        }

        Color nextColor = nextPalette.ReadCyclic(i, doblend);
        return Color.Lerp(color, nextColor, transitionProgress);
    }

    /// <summary>
    /// Reuses a conditioned endpoint when its immutable source is already cached, otherwise derives
    /// one new Effect-local palette with the unchanged live controls.
    /// </summary>
    /// <param name="source">The shared immutable palette endpoint to represent.</param>
    /// <param name="previousCurrentSource">The source of the previous conditioned current endpoint.</param>
    /// <param name="previousCurrent">The previous conditioned current endpoint.</param>
    /// <param name="previousNextSource">The source of the previous conditioned next endpoint.</param>
    /// <param name="previousNext">The previous conditioned next endpoint.</param>
    /// <param name="conditioning">The unchanged live controls used by every reusable endpoint.</param>
    /// <returns>A reusable or newly conditioned Effect-owned palette, or null for a null endpoint.</returns>
    private static GPalette ReuseOrConditionPalette(
        GPalette source,
        GPalette previousCurrentSource,
        GPalette previousCurrent,
        GPalette previousNextSource,
        GPalette previousNext,
        PaletteConditioning conditioning)
    {
        if (source == null)
        {
            return null;
        }
        if (ReferenceEquals(source, previousCurrentSource))
        {
            return previousCurrent;
        }
        if (ReferenceEquals(source, previousNextSource))
        {
            return previousNext;
        }
        return source.Conditioned(conditioning);
    }
}
