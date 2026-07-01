using System.Collections.Generic;
using UnityEngine;
// Chuck Sommerville

/// <summary>
/// Builds stochastic branching paths outward from center-star tiles.
/// </summary>
[System.Serializable]
public class Lightning : EffectBase
{
    private float fadeValue;
    private float starthue;
    private float deltastart = 0f;
    private float deltaray = 0f;
    private float deltatile = 0f;

    private int beatMode;

    private int mode = 0;

    /// <summary>Lightning is a sharp beat-scaled burst. On a Fill it HOLDS a frozen bolt that hard-snaps to entirely
    /// new positions on every eighth note while strobing on the sixteenths (see <see cref="Draw"/>) — held, but
    /// jerking. On a Drop it inverts: an intensity swell, electric flicker, and a figure/ground flip where the wall
    /// floods with the rolled colors and the bolts cut through as dark negative space (see <see cref="OnNewGrid"/>).</summary>
    public override Repertoire Repertoire => Repertoire.HandlesFill | Repertoire.HandlesDrop;

    /// <summary>Beats per bar used to derive the Drop decay length from BPM.</summary>
    private const int BeatsPerBar = 4;

    /// <summary>Drop decay length in bars: the slam eases from full to nothing over this many bars.</summary>
    private const float DropBars = 2f;

    /// <summary>Drop decay length used when no BPM is available (≈2 bars at 120 BPM).</summary>
    private const float DropFallbackSeconds = 4f;

    /// <summary>How far the bolts swell toward full brightness (HSV value) at the Drop's peak (0 = unchanged, 1 = full):
    /// a pure intensity lift that keeps the rolled hue and saturation and caps at 1, so it never washes toward white. Tune on the readout.</summary>
    private const float DropValueLift = 1f;

    /// <summary>Depth of the fast electric flicker at the Drop's peak (0 = none, 1 = can strobe to black): the whole bolt stutters, easing out with the envelope. Tune on the readout.</summary>
    private const float DropFlickerDepth = 0.5f;

    /// <summary>Flicker speed (Perlin samples per second of effect time): higher = faster, sharper strobe. Tune on the readout.</summary>
    private const float DropFlickerHz = 22f;

    /// <summary>How fully the wall floods to the bright palette field at the Drop's peak (0 = none, 1 = solid field):
    /// the inverted ground that the bolts cut through as dark negative space. Scaled by the envelope. Tune on the readout.</summary>
    private const float DropFieldFlood = 1f;

    /// <summary>Extra brightness flashed into the flooded field at the Drop's peak (lerped toward white), fading out
    /// with the envelope so it is a brief over-bright impact rather than a sustained white wash. Tune on the readout.</summary>
    private const float DropFieldBright = 0.25f;

    /// <summary>Trail-fade amount held during the Drop slam (near 1 = slow fade): the bolt trails linger under the flood. Tune on the readout.</summary>
    private const float DropFadeHold = 0.97f;

    /// <summary>Bolt brightness while the Fill's sixteenth-note strobe gate is closed (0 = full black blink, 1 = no strobe):
    /// the held bolt hard-blinks between this and full on every sixteenth. Tune on the readout.</summary>
    private const float FillStrobeFloor = 0.15f;

    /// <summary>Fraction of each sixteenth the Fill strobe gate stays lit (duty cycle, 0..1): smaller = shorter, sharper flashes. Tune on the readout.</summary>
    private const float FillStrobeDuty = 0.5f;

    /// <summary>During a Fill the walked bolt freezes here and is only re-walked on the eighth-note jerk; one cached tile path per center-star ray.</summary>
    private List<int>[] heldRays;

    /// <summary>Last eighth-note <see cref="BeatManager.GateOf"/> state, for rising-edge detection so the bolt jerks once per eighth note, not every open frame.</summary>
    private bool eighthGateOpen;

    /// <summary>True while the Fill hold/jerk/strobe mode is driving the bolt (surfaced on the readout).</summary>
    private bool heldActive;

    /// <summary>Drop slam amount (1 at the downbeat, SmoothStep-eased to 0 over <see cref="DropBars"/>); drives the value lift, flicker, field inversion, and trail hold.</summary>
    private float dropEnv;

    /// <summary>Seconds elapsed into the current Drop slam.</summary>
    private float dropElapsed;

    /// <summary>Length of the current Drop slam in seconds (BPM-derived, or <see cref="DropFallbackSeconds"/>).</summary>
    private float dropSeconds;

    /// <summary>True once the current Drop has already slammed, so it fires only once per Drop (on the first Grid downbeat inside it). Cleared when the Drop ends.</summary>
    private bool dropFlashed;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"fade: {fadeValue}\n starthue:{starthue}\n deltastart:{deltastart}\n deltaray:{deltaray}\n deltatile:{deltatile}\n mode:{mode}" +
            (heldActive ? "\n FILL hold/jerk 8th, strobe 16th" : "") +
            (dropEnv > 0.01f ? $"\n DROP {dropEnv:0.00}" : "");
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
        buffer.Clear();
        Reroll();

        heldRays = null;
        eighthGateOpen = false;
        heldActive = false;

        dropEnv = 0f;
        dropElapsed = 0f;
        dropSeconds = DropFallbackSeconds;
        dropFlashed = false;
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
    /// On each new Grid the bolts take a fresh form. If that Grid downbeat lands inside a Drop and this Drop has
    /// not slammed yet, it also fires the one-shot intensity/flicker/inversion slam that decays over <see cref="DropBars"/>
    /// bars — so the Drop always lands on the colors just rolled here.
    /// </summary>
    protected override void OnNewGrid()
    {
        Reroll();
        if (beatManager.Drop is { inProgress: true } && !dropFlashed)
        {
            TriggerDrop();
            dropFlashed = true;
        }
    }

    /// <summary>Arms the Drop slam: full envelope now, eased to nothing over <see cref="DropBars"/> bars of the current tempo.</summary>
    private void TriggerDrop()
    {
        dropSeconds = beatManager.Bpm is { } bpm && bpm > 0f
            ? 60f / bpm * BeatsPerBar * DropBars
            : DropFallbackSeconds;
        dropElapsed = 0f;
        dropEnv = 1f;
    }

    /// <summary>
    /// Updates the one-shot Drop envelope: a Grid-aligned Drop snaps it to full in <see cref="TriggerDrop"/>,
    /// this eases it back to zero over the BPM-sized window, and the guard re-arms only after the Drop window
    /// ends. At zero, every Drop term collapses out and the normal bright-bolts-on-black render is unchanged.
    /// </summary>
    private void UpdateDropSlam()
    {
        if (beatManager.Drop is not { inProgress: true })
        {
            dropFlashed = false;
        }

        if (dropEnv <= 0f)
        {
            return;
        }

        dropElapsed += effectDelta;
        float decayProgress = dropSeconds > 0f ? Mathf.Clamp01(dropElapsed / dropSeconds) : 1f;
        dropEnv = 1f - Mathf.SmoothStep(0f, 1f, decayProgress);
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

        float noise = Mathf.PerlinNoise(effectTime * DropFlickerHz, 0.37f);
        return 1f - (DropFlickerDepth * dropEnv * (1f - noise));
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
        Color floodColor = Color.Lerp(fieldColor * flicker, Color.white, DropFieldBright * dropEnv);
        float flood = dropEnv * DropFieldFlood;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Color.Lerp(buffer[i], floodColor, flood);
        }
    }

    /// <summary>
    /// Updates the Fill hold/jerk path. Outside a Fill the bolt re-walks every frame; inside a Fill it freezes
    /// and only re-walks on the rising edge of the eighth-note gate, so the whole branch hard-snaps to new
    /// positions twice a beat instead of flowing continuously. If the beat gate is unavailable, it holds.
    /// </summary>
    private void UpdateHeldBolt()
    {
        heldActive = beatManager.Fill is { inProgress: true };
        if (heldActive)
        {
            bool open = GateOpen(beatManager.GateOf(Subdivision.Eighth));
            if ((open && !eighthGateOpen) || heldRays == null)
            {
                GenerateBolt();
            }

            eighthGateOpen = open;
        }
        else
        {
            GenerateBolt();
            eighthGateOpen = false;
        }
    }

    /// <summary>
    /// Returns the Fill's hard sixteenth-note strobe multiplier. The held bolt blinks between full and
    /// <see cref="FillStrobeFloor"/> while the sixteenth gate is closed; outside a Fill, 1 means no strobe.
    /// </summary>
    private float FillStrobe()
    {
        if (!heldActive)
        {
            return 1f;
        }

        return GateOpen(beatManager.GateOf(Subdivision.Sixteenth, FillStrobeDuty)) ? 1f : FillStrobeFloor;
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales branching lightning path colors.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 0.75f, 1.0f, beatEnable);
        float beatHue = beatManager.GetBeatBrightness(beatVariant, 0.5f, 0.0f, beatEnable);

        UpdateDropSlam();
        float flicker = DropFlicker();

        buffer.Fade(Mathf.Lerp(fadeValue, DropFadeHold, dropEnv));
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
        int[] shape = penrose.JsonRawData.shapes.stars;
        int list = shape[1];
        int start = list + 1;
        int end = start + shape[list];
        int rayCount = end - start;
        if (heldRays == null || heldRays.Length != rayCount)
            heldRays = new List<int>[rayCount];

        int[] possible = { 0, 0, 0, 0 };        // holds possible step positions
        for (int j = start; j < end; j++)
        {
            List<int> ray = heldRays[j - start] ??= new List<int>();
            ray.Clear();
            int currentIdx = shape[j];
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
                    strokeColor = Color.HSVToRGB(dh, ds, Mathf.Lerp(dv, 1f, DropValueLift * dropEnv));
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
    /// </summary>
    private Color RolledColor(float hue)
    {
        float wrapped = (hue + 10000f) % 1f;
        return mode != 0 ? APalette.read(wrapped, true) : Color.HSVToRGB(wrapped, 1f, 1f);
    }

    /// <summary>True when a subdivision gate from <see cref="BeatManager.GateOf"/> is present and open. GateOf returns
    /// a hard 0 or 1 on-clock and null off-clock, so this is the single place that reads "is this beat slot lit now".</summary>
    private static bool GateOpen(float? gate) => gate.HasValue && gate.Value > 0.5f;

}
