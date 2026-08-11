using System.Collections.Generic;
using UnityEngine;
// Chuck Sommerville

/// <summary>
/// Builds stochastic branching paths outward from center-star tiles.
/// </summary>
[System.Serializable]
[EffectSyncSettings(typeof(LightningSyncSettingsAsset))]
public class Lightning : EffectBase
{
    // Standalone Defaults

    /// <summary>Starting-hue drift magnitude applied when the coin flip enables that animation in the Standalone look.</summary>
    private const float StandaloneStartHueDelta = 0.02f;

    /// <summary>Per-ray hue drift magnitude applied when the coin flip enables that animation in the Standalone look.</summary>
    private const float StandaloneRayHueDelta = 0.2f;

    /// <summary>Per-tile hue drift magnitude applied when the coin flip enables that animation in the Standalone look.</summary>
    private const float StandaloneTileHueDelta = 0.02f;

    /// <summary>Fixed bolt-brightness multiplier returned when no live clock can place the held Waveform.</summary>
    private const float StandaloneBeatBrightness = 0.75f;

    // Sync Defaults

    /// <summary>Bolt-brightness multiplier at the held Waveform's trough.</summary>
    private const float SyncBeatBrightnessAtTrough = 1f;

    /// <summary>Bolt-brightness multiplier at the held Waveform's peak.</summary>
    private const float SyncBeatBrightnessAtPeak = 0.75f;

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    private const float SyncBeatHueOffset = 0.5f;

    /// <summary>Drop decay length in bars: the slam falls linearly from full to nothing over this many bars.</summary>
    private const int SyncDropBars = 2;

    /// <summary>How far the bolts swell toward full brightness (HSV value) at the Drop's peak (0 = unchanged, 1 = full):
    /// a pure intensity lift that keeps the rolled hue and saturation and caps at 1, so it never washes toward white. Tune on the readout.</summary>
    private const float SyncDropValueLift = 1f;

    /// <summary>Depth of the fast electric flicker at the Drop's peak (0 = none, 1 = can strobe to black): the whole bolt stutters, fading out linearly with the envelope. Tune on the readout.</summary>
    private const float SyncDropFlickerDepth = 0.5f;

    /// <summary>Flicker speed (Perlin samples per second of effect time): higher = faster, sharper strobe. Tune on the readout.</summary>
    private const float SyncDropFlickerHz = 22f;

    /// <summary>How fully the wall floods to the bright palette field at the Drop's peak (0 = none, 1 = solid field):
    /// the inverted ground that the bolts cut through as dark negative space. Scaled by the envelope. Tune on the readout.</summary>
    private const float SyncDropFieldFlood = 1f;

    /// <summary>Extra brightness flashed into the flooded field at the Drop's peak (lerped toward white), fading out
    /// with the envelope so it is a brief over-bright impact rather than a sustained white wash. Tune on the readout.</summary>
    private const float SyncDropFieldBright = 0.25f;

    /// <summary>Trail-fade amount held during the Drop slam (near 1 = slow fade): the bolt trails linger under the flood. Tune on the readout.</summary>
    private const float SyncDropFadeHold = 0.97f;

    /// <summary>Pulse duration whose rising edge re-walks the held Fill bolt.</summary>
    private const Duration SyncFillJerkDuration = Duration.Sixteenth;

    /// <summary>Pulse duration that drives the held Fill bolt's strobe gate.</summary>
    private const Duration SyncFillStrobeDuration = Duration.Sixteenth;

    /// <summary>Bolt brightness while the Fill's strobe gate is closed (0 = full black blink, 1 = no strobe):
    /// the held bolt hard-blinks between this and full on every strobe pulse (sixteenths by default). Tune on the readout.</summary>
    private const float SyncFillStrobeFloor = 0.15f;

    /// <summary>Fraction of each strobe pulse the Fill strobe gate stays lit (duty cycle, 0..1): smaller = shorter, sharper flashes. Tune on the readout.</summary>
    private const float SyncFillStrobeDuty = 0.5f;

    // Runtime mechanism constants

    /// <summary>Beats per bar used to express the authored Drop decay length in beats.</summary>
    private const int BeatsPerBar = 4;

    /// <summary>Lightning is a sharp beat-scaled burst. On a Fill it HOLDS a frozen bolt that hard-snaps to entirely
    /// new positions on every jerk pulse (sixteenth notes by default) while strobing on the strobe pulses
    /// (sixteenths by default) (see <see cref="Draw"/>) — held, but
    /// jerking. On a Drop it inverts: an intensity swell, electric flicker, and a figure/ground flip where the wall
    /// floods with the rolled colors and the bolts cut through as dark negative space (see <see cref="OnNewGrid"/>);
    /// its electric energy suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Lightning's Standalone Defaults.</summary>
    public static LightningStandaloneSettings StandaloneSettings => new LightningStandaloneSettings
    {
        StartHueDelta = StandaloneStartHueDelta,
        RayHueDelta = StandaloneRayHueDelta,
        TileHueDelta = StandaloneTileHueDelta,
        BeatBrightness = StandaloneBeatBrightness,
    };

    /// <summary>Resolves a fresh copy of Lightning's file-local Sync Defaults.</summary>
    public static LightningSyncSettings SyncDefaults => new LightningSyncSettings
    {
        BeatBrightnessAtTrough = SyncBeatBrightnessAtTrough,
        BeatBrightnessAtPeak = SyncBeatBrightnessAtPeak,
        BeatHueOffset = SyncBeatHueOffset,
        DropBars = SyncDropBars,
        DropValueLift = SyncDropValueLift,
        DropFlickerDepth = SyncDropFlickerDepth,
        DropFlickerHz = SyncDropFlickerHz,
        DropFieldFlood = SyncDropFieldFlood,
        DropFieldBright = SyncDropFieldBright,
        DropFadeHold = SyncDropFadeHold,
        FillJerkDuration = SyncFillJerkDuration,
        FillStrobeDuration = SyncFillStrobeDuration,
        FillStrobeFloor = SyncFillStrobeFloor,
        FillStrobeDuty = SyncFillStrobeDuty,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private LightningStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private LightningSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Current trail-fade amount rolled across the complete 0..1 fade domain; the full-domain roll is mechanism rather than an authored subrange.</summary>
    private float fadeValue;

    /// <summary>Current starting hue rolled across the complete 0..1 hue domain; the full hue wheel is structural rather than an authored subrange.</summary>
    private float starthue;

    /// <summary>Current signed starting-hue drift; its inline rolls span the complete off/on and direction domains while the enabled drift magnitude comes from Standalone Settings.</summary>
    private float deltastart = 0f;

    /// <summary>Current signed per-ray hue drift; its inline rolls span the complete off/on and direction domains while the enabled drift magnitude comes from Standalone Settings.</summary>
    private float deltaray = 0f;

    /// <summary>Current signed per-tile hue drift; its inline rolls span the complete off/on and direction domains while the enabled drift magnitude comes from Standalone Settings.</summary>
    private float deltatile = 0f;

    /// <summary>Current beat-response mode; the inline [0, 3) roll spans all three algorithm modes and is not an authored subrange.</summary>
    private int beatMode;

    /// <summary>Current color-mode slot; the inline [0, 4) roll spans the complete one-HSV/three-palette weighting and is not an authored subrange.</summary>
    private int mode = 0;

    /// <summary>During a Fill the walked bolt freezes here and is only re-walked on the jerk pulse; one cached tile path per center-star ray.</summary>
    private List<int>[] heldRays;

    /// <summary>True while the Fill hold/jerk/strobe mode is driving the bolt (surfaced on the readout).</summary>
    private bool heldActive;
    private bool previousJerkOn;

    /// <summary>Drop slam amount (1 at the downbeat, then falling linearly to 0 over <see cref="LightningSyncSettings.DropBars"/>); drives the value lift, flicker, field inversion, and trail hold.</summary>
    private float dropEnv;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"fade: {fadeValue}\n starthue:{starthue}\n deltastart:{deltastart}\n deltaray:{deltaray}\n deltatile:{deltatile}\n mode:{mode}" +
            (heldActive ? $"\n FILL hold/jerk {SyncSettings.FillJerkDuration}, strobe {SyncSettings.FillStrobeDuration}" : "") +
            (dropEnv > 0.01f ? $"\n DROP {dropEnv:0.00}" : "");
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Lightning),
            SyncDefaults);

        // Unfiltered acquisition spans the complete curated Waveform Pool, so Lightning has no
        // authored Waveform-selection subrange to expose as Effect Settings.
        waveform = waveforms.Random();
        buffer.Clear();
        Reroll();

        heldRays = null;
        heldActive = false;
        previousJerkOn = false;

        dropEnv = 0f;
    }

    /// <summary>
    /// Re-rolls the per-activation look: trail fade, starting hue, the three animation deltas and their directions,
    /// color mode, and beat mode. Called at activation and again on each new Grid, so the bolts take a fresh form
    /// in step with the music — and a Drop, which fires on a Grid downbeat, always slams a freshly-rolled bolt.
    /// </summary>
    private void Reroll()
    {
        fadeValue = Random.value;
        starthue = Random.value;
        //  selectively modify animation
        // The inline 0f is the structural "animation off" endpoint of each coin flip, not an authored
        // subrange bound; only the enabled drift magnitude is an authored value.
        deltastart = Random.Range(0, 2) == 0 ? 0f : standaloneSettings.StartHueDelta;
        deltaray = Random.Range(0, 2) == 0 ? 0f : standaloneSettings.RayHueDelta;
        deltatile = Random.Range(0, 2) == 0 ? 0f : standaloneSettings.TileHueDelta;
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
    /// On each new Grid the bolts take a fresh form. Drop intensity is read independently from the hub's
    /// direct Drop decay, so this hook owns only Lightning's Grid-aligned visual reroll.
    /// </summary>
    protected override void OnNewGrid()
    {
        Reroll();
    }

    /// <summary>
    /// Returns the Drop's whole-picture electric flicker multiplier. The flicker is a fast Perlin stutter scaled
    /// by the Drop envelope, so it is sharp at impact and disappears as the Drop resolves; 1 means no flicker.
    /// </summary>
    private float DropFlicker()
    {
        if (dropEnv <= 0f)
        {
            return 1f;
        }

        // The fixed non-axis coordinate selects one deterministic Perlin slice; it is flicker
        // mechanism rather than an authored response range.
        float noise = Mathf.PerlinNoise(effectTime * SyncSettings.DropFlickerHz, 0.37f);
        return 1f - (SyncSettings.DropFlickerDepth * dropEnv * (1f - noise));
    }

    /// <summary>
    /// Floods the background during the Drop to invert figure and ground: the wall moves toward a bright rolled
    /// palette field, then the bolt is rendered as a dark cut through it. The field gets a brief white lift at
    /// impact but settles back into the pure inverted color as the envelope fades.
    /// </summary>
    private void FloodDropField(float flicker)
    {
        if (dropEnv <= 0f)
        {
            return;
        }

        Color fieldColor = RolledColor(starthue);
        // Flash the field a touch brighter (toward white) at the peak, fading out with the envelope so the impact
        // hits bright and then settles back into the pure inverted color.
        Color floodColor = Color.Lerp(fieldColor * flicker, Color.white, SyncSettings.DropFieldBright * dropEnv);
        float flood = dropEnv * SyncSettings.DropFieldFlood;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Color.Lerp(buffer[i], floodColor, flood);
        }
    }

    /// <summary>
    /// Updates the Fill hold/jerk path. Outside a Fill the bolt re-walks every frame; inside a Fill it freezes
    /// and only re-walks on the rising edge of the configured jerk gate (sixteenth notes by default), so the whole
    /// branch hard-snaps to new positions in step with that pulse instead of flowing continuously. If the beat gate is unavailable, it holds.
    /// </summary>
    private void UpdateHeldBolt()
    {
        heldActive = beatManager.Fill.Active;
        var jerkOn = beatManager.Pulses.On(SyncSettings.FillJerkDuration);
        if (heldActive)
        {
            if ((jerkOn && !previousJerkOn) || heldRays == null)
            {
                GenerateBolt();
            }
        }
        else
        {
            GenerateBolt();
        }
        previousJerkOn = jerkOn;
    }

    /// <summary>
    /// Returns the Fill's strobe multiplier from the hub Duration gate (sixteenth notes by default). The held bolt blinks
    /// between full and <see cref="LightningSyncSettings.FillStrobeFloor"/> while closed; outside a Fill, 1 means no strobe.
    /// </summary>
    private float FillStrobe()
    {
        if (!heldActive)
        {
            return 1f;
        }

        return beatManager.Pulses.On(SyncSettings.FillStrobeDuration, SyncSettings.FillStrobeDuty)
            ? 1f
            : SyncSettings.FillStrobeFloor;
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // This Effect owns its brightness, hue, and clockless fallback mappings.
        float rhythm = waveform.Envelope;
        float beatBrightness = waveform.Lerp(
            SyncSettings.BeatBrightnessAtTrough,
            beatManager.IsSynced
                ? SyncSettings.BeatBrightnessAtPeak
                : standaloneSettings.BeatBrightness);
        float beatHue = SyncSettings.BeatHueOffset * rhythm;

        dropEnv = beatManager.Drop.In.Decay(SyncSettings.DropBars * BeatsPerBar);
        float flicker = DropFlicker();

        buffer.Fade(dropEnv.Lerp(fadeValue, SyncSettings.DropFadeHold));
        FloodDropField(flicker);

        UpdateHeldBolt();
        float strobe = FillStrobe();

        RenderBolt(beatBrightness, beatHue, flicker, strobe);
    }

    /// <summary>
    /// Walks the stochastic branch path outward from each center-star tile and caches the visited tile indices in
    /// <see cref="heldRays"/>. Splitting the walk (here) from the coloring (<see cref="RenderBolt"/>) is what lets a
    /// Fill hold one bolt and re-walk it only on the jerk; outside a Fill it is simply called every frame, preserving
    /// the original per-frame stochastic redraw.
    /// </summary>
    private void GenerateBolt()
    {
        // this selects the center star tiles
        LayoutData.ShapeList.Reader stars = penrose.Layout.shapes.Stars;
        LayoutData.ShapeList.Group centerStar = stars.GetGroup(0);
        int rayCount = centerStar.TileCount;
        if (heldRays == null || heldRays.Length != rayCount)
            heldRays = new List<int>[rayCount];

        int[] possible = { 0, 0, 0, 0 };        // holds possible step positions
        for (int j = 0; j < centerStar.TileCount; j++)
        {
            List<int> ray = heldRays[j] ??= new List<int>();
            ray.Clear();
            int currentIdx = centerStar[j];
            // walk the line till it stops
            while (true)
            {
                ray.Add(currentIdx);
                float currentRadius = tiles[currentIdx].radius;
                // find possible paths
                int used = 0;
                for (int i = 0; i < tiles[currentIdx].neighbors.Length; i++)
                {
                    int testTile = tiles[currentIdx].neighbors[i].tileIdx;
                    // if the step takes us farther from the origin
                    if (tiles[testTile].radius > currentRadius)
                        possible[used++] = testTile;
                }
                // stop if nowhere to go
                if (used == 0)
                    break;
                // step
                // The roll covers every valid outward neighbor; that complete choice domain is structural.
                currentIdx = possible[Random.Range(0, used)];
            }
        }
    }

    /// <summary>
    /// Colors the cached <see cref="heldRays"/> path into the buffer using the effect's per-ray/per-tile hue
    /// progression, then applies the beat pulse, Drop flicker/value-lift/inversion, and the Fill strobe. Outside a
    /// Fill <paramref name="strobe"/> is 1 and the Drop terms collapse at dropEnv 0, so the output is the ordinary
    /// bright-bolts-on-black look.
    /// </summary>
    private void RenderBolt(float beatBrightness, float beatHue, float flicker, float strobe)
    {
        if (heldRays == null)
            return;

        // for each of the center-star rays
        float rayhue = starthue;
        starthue += deltastart;
        for (int r = 0; r < heldRays.Length; r++)
        {
            List<int> ray = heldRays[r];
            float tilehue = rayhue;
            rayhue += deltaray;
            for (int k = 0; k < ray.Count; k++)
            {
                int currentIdx = ray[k];
                // color the current tile under the rolled palette/mode
                Color strokeColor = RolledColor(tilehue);

                if (beatMode < 2)
                    strokeColor *= beatBrightness;
                if (beatMode > 0)
                {
                    Color.RGBToHSV(strokeColor, out float h, out float s, out float v);
                    strokeColor = Color.HSVToRGB((h + beatHue) % 1f, s, v);
                }

                if (dropEnv > 0f)
                {
                    // Swell intensity in value space (caps at 1) so the rolled hue/saturation are untouched and it
                    // never washes toward white — pure "change of intensity," felt as the bolts return after the slam.
                    Color.RGBToHSV(strokeColor, out float dh, out float ds, out float dv);
                    strokeColor = Color.HSVToRGB(dh, ds, (SyncSettings.DropValueLift * dropEnv).Lerp(dv, 1f));
                }
                Color boltColor = strokeColor * beatBrightness * flicker * strobe;
                // Invert the bolt toward black so it reads as a dark cut through the flooded field at the Drop's peak,
                // returning to a bright bolt as the Drop decays. At dropEnv 0 this is just the bright bolt.
                buffer[currentIdx] = Color.Lerp(boltColor, Color.black, dropEnv);
                tilehue += deltatile;
            }
        }
    }

    /// <summary>
    /// Maps a (possibly negative) hue to a color under the current <see cref="mode"/>: the shared animated palette
    /// when mode is non-zero, otherwise a fully-saturated HSV color. The +10000 bias keeps the modulo positive so
    /// hues driven negative by the rolled deltas still wrap cleanly into [0,1).
    /// Full saturation and value define the HSV rainbow branch, so both complete-domain endpoints remain structural inline literals.
    /// </summary>
    private Color RolledColor(float hue)
    {
        float wrapped = (hue + 10000f) % 1f;
        return mode != 0 ? APalette.read(wrapped, true) : Color.HSVToRGB(wrapped, 1f, 1f);
    }
}

/// <summary>The fixed Standalone Settings resolved from Lightning's file-local Standalone Defaults.</summary>
public sealed class LightningStandaloneSettings
{
    /// <summary>Drift magnitude applied to the starting hue when its coin flip enables that animation.</summary>
    public float StartHueDelta;

    /// <summary>Drift magnitude applied per ray when its coin flip enables that animation.</summary>
    public float RayHueDelta;

    /// <summary>Drift magnitude applied per tile when its coin flip enables that animation.</summary>
    public float TileHueDelta;

    /// <summary>Fixed bolt-brightness multiplier used without live musical placement.</summary>
    public float BeatBrightness;
}

/// <summary>The saved musical-response settings used by Lightning in Synced Mode.</summary>
[System.Serializable]
public sealed class LightningSyncSettings
{
    /// <summary>Bolt-brightness multiplier at the held Waveform's trough.</summary>
    [Range(0f, 1f)] public float BeatBrightnessAtTrough;

    /// <summary>Bolt-brightness multiplier at the held Waveform's peak.</summary>
    [Range(0f, 1f)] public float BeatBrightnessAtPeak;

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    [Range(0f, 1f)] public float BeatHueOffset;

    /// <summary>Drop decay length in bars.</summary>
    [Min(1)] public int DropBars;

    /// <summary>Value-space brightness lift applied to bolts at the Drop's peak.</summary>
    [Range(0f, 1f)] public float DropValueLift;

    /// <summary>Depth of the fast electric flicker at the Drop's peak.</summary>
    [Range(0f, 1f)] public float DropFlickerDepth;

    /// <summary>Flicker speed in Perlin samples per second of effect time.</summary>
    [Min(0f)] public float DropFlickerHz;

    /// <summary>Fraction of the wall flooded to the bright palette field at the Drop's peak.</summary>
    [Range(0f, 1f)] public float DropFieldFlood;

    /// <summary>White-flash amount added to the flooded field at the Drop's peak.</summary>
    [Range(0f, 1f)] public float DropFieldBright;

    /// <summary>Trail-fade amount held during the Drop slam.</summary>
    [Range(0f, 1f)] public float DropFadeHold;

    /// <summary>Pulse duration whose rising edge re-walks the held Fill bolt.</summary>
    public Duration FillJerkDuration;

    /// <summary>Pulse duration that drives the held Fill bolt's strobe gate.</summary>
    public Duration FillStrobeDuration;

    /// <summary>Bolt brightness while the Fill strobe gate is closed.</summary>
    [Range(0f, 1f)] public float FillStrobeFloor;

    /// <summary>Fraction of each Fill strobe pulse for which the gate stays lit.</summary>
    [Range(0f, 1f)] public float FillStrobeDuty;

    /// <summary>Copies every Lightning Sync Setting from another value.</summary>
    public void CopyFrom(LightningSyncSettings source)
    {
        if (source == null)
        {
            throw new System.ArgumentNullException(nameof(source));
        }

        BeatBrightnessAtTrough = source.BeatBrightnessAtTrough;
        BeatBrightnessAtPeak = source.BeatBrightnessAtPeak;
        BeatHueOffset = source.BeatHueOffset;
        DropBars = source.DropBars;
        DropValueLift = source.DropValueLift;
        DropFlickerDepth = source.DropFlickerDepth;
        DropFlickerHz = source.DropFlickerHz;
        DropFieldFlood = source.DropFieldFlood;
        DropFieldBright = source.DropFieldBright;
        DropFadeHold = source.DropFadeHold;
        FillJerkDuration = source.FillJerkDuration;
        FillStrobeDuration = source.FillStrobeDuration;
        FillStrobeFloor = source.FillStrobeFloor;
        FillStrobeDuty = source.FillStrobeDuty;
    }
}
