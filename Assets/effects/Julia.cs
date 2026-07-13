using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a Julia fractal by iterating the escape-time function directly at each Penrose
/// tile center. The view is a plain linear transform from wall space to the complex plane
/// (center + uniform scale), so recentering, zooming, and rotating are vector math on the
/// sample coordinates rather than raster operations.
/// </summary>
public class Julia : EffectBase
{
    /// <summary>
    /// Julia's fractal drift suits Low/Mid-energy sections, dives on Fills, and answers
    /// Drops with a spin/blowout slam.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop |
        Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Escape-time iteration cap; points that survive this long count as inside the set and render black.</summary>
    private const int Iterations = 100;

    /// <summary>Width of the complex-plane window, in complex units, at full zoom-out.</summary>
    private const float WindowWidth = 5f;

    /// <summary>ln(2), for the smooth escape-time calculation.</summary>
    private const float Ln2 = 0.6931472f;

    /// <summary>
    /// Fill dive depth: at full Fill the zoom is e^FillDiveDepth (~7×) deeper than the
    /// breathing zoom alone. Exponential so the dive speed feels constant at any depth.
    /// </summary>
    private const float FillDiveDepth = 2f;

    /// <summary>
    /// Floor for the complex-plane window width. Keeps the dive above float precision and
    /// stops the breathing zoom from collapsing to a single flat point at sin = 1.
    /// </summary>
    private const float MinWindow = 0.002f;

    /// <summary>Chance that an activation colors from the shared palette instead of the HSV rainbow.</summary>
    private const float PaletteChance = 0.5f;

    /// <summary>Beats the Drop slam takes to decay back to rest.</summary>
    private const float DropDecayBeats = 8f;

    /// <summary>Spin speed in revolutions per second at the Drop slam's peak.</summary>
    private const float DropSpinRate = 1f;

    /// <summary>
    /// Drop zoom blowout: at the slam's peak the window widens by e^DropBlowout, blasting
    /// back out of the Fill's dive. Clamped so it never zooms out past the full-set view.
    /// </summary>
    private const float DropBlowout = 1.5f;

    /// <summary>Hue-wheel kick at the Drop hit: a half turn, an instant palette inversion.</summary>
    private const float DropHueKick = 0.5f;

    /// <summary>Baseline hue cycling speed in wheel revolutions per second; the colors never stop marching.</summary>
    private const float HueBaseRate = 0.05f;

    /// <summary>
    /// Extra hue cycling speed, in wheel revolutions per second, added at the beat envelope's
    /// peak. The held Waveform's envelope (0..1, peaking on its hits) scales this, so the cycle
    /// surges on those hits and settles back to the base rate between them.
    /// </summary>
    private const float HueBeatRate = 0.25f;

    /// <summary>Anti-aliasing footprint radius around each tile center, in wall units (~half the typical tile spacing).</summary>
    private const float AaRadius = 1.2f;

    /// <summary>Anti-aliasing samples per tile; two interleaved rings of AaSamples/2.</summary>
    private const int AaSamples = 8;

    /// <summary>Anti-aliasing sample offsets around each tile center, in wall units.</summary>
    private static readonly Vector2[] aaOffsets = BuildAaOffsets();

    /// <summary>
    /// Builds the AA sample pattern: AaSamples points spread evenly by angle, alternating
    /// between an outer and an inner ring so the footprint covers the tile area evenly.
    /// </summary>
    private static Vector2[] BuildAaOffsets()
    {
        var offsets = new Vector2[AaSamples];
        for (var s = 0; s < AaSamples; s++)
        {
            var a = (s + 0.5f) / AaSamples * 2f * Mathf.PI;
            var r = AaRadius * (s % 2 == 0 ? 1f : 0.55f);
            offsets[s] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        return offsets;
    }

    /// <summary>Julia constants (c = x + yi) known to produce interesting sets.</summary>
    private readonly Vector2[] valueSets = {
      new Vector2(0.285f, 0.01f),
      new Vector2(-0.70176f, -0.3842f),
      new Vector2(-0.835f, -0.2321f),
      new Vector2(-0.8f, 0.156f),
      new Vector2(-0.7269f, 0.1889f)
    };

    /// <summary>Per-constant view centers in the complex plane, paired by index with <see cref="valueSets"/>.</summary>
    private readonly Vector2[] offSets = {
      new Vector2(0.2f, 0.04f),
      new Vector2(-0.125f, -0.04f),
      new Vector2(-0.0375f, 0f),
      new Vector2(0.175f, 0.05f),
      new Vector2(0.0875f, 0.225f)
    };

    private float angle;
    private float speed = 0.15f;

    /// <summary>The consumer-owned Waveform that seasons Julia's hue cycling.</summary>
    private Waveform waveform;

    private Vector2 c;
    private Vector2 viewCenter;
    private int presetIndex;
    private float hueScroll;
    private float fillEnv;
    private float dropEnv;
    private float dropSpinDir = 1f;
    private float rotation;
    private bool usePalette;

    /// <summary>
    /// Called every frame to update the debug UI text element.
    /// </summary>
    public override string DebugText()
    {
        return $"{presetIndex}, {speed}, ({viewCenter.x}, {viewCenter.y})\n" +
            (usePalette ? "PALETTE" : "RAINBOW") + $" HUE {hueScroll:0.00}" +
            (fillEnv > 0.01f ? $"\nFILL {fillEnv:0.00}" : "") +
            (dropEnv > 0.01f ? $"\nDROP {dropEnv:0.00}" : "");
    }

    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    public override void OnStart()
    {
        Reroll();
        hueScroll = 0f;
        fillEnv = 0f;
        dropEnv = 0f;
        rotation = 0f;
        buffer.Clear();
    }

    /// <summary>
    /// Re-rolls the per-activation look: Julia constant preset, zoom speed, color mode
    /// (fresh palette or rainbow), and held Waveform. Called once at activation and
    /// again on each new Grid, so the fractal takes a fresh form in step with the music.
    /// </summary>
    private void Reroll()
    {
        presetIndex = Random.Range(0, valueSets.Length);
        c = valueSets[presetIndex];
        viewCenter = offSets[presetIndex];
        speed = Random.Range(0.1f, 0.3f);
        usePalette = Random.value < PaletteChance;
        if (usePalette) APalette.Change();
        waveform = synth.Random();
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// On each new Grid the fractal takes a fresh form and held Waveform.
    /// </summary>
    protected override void OnNewGrid()
    {
        Reroll();
    }

    /// <summary>
    /// Chooses the Drop slam's spin direction and applies its instant hue inversion; the hub-owned
    /// stock Decay supplies the spin/blowout envelope.
    /// </summary>
    private void ApplyDropHit()
    {
        dropSpinDir = Random.value < 0.5f ? -1f : 1f;
        hueScroll = Mathf.Repeat(hueScroll + DropHueKick, 1f);
    }

    /// <summary>
    /// Updates the Drop slam from the hub-owned Started edge and stock Decay, then integrates the spin.
    /// </summary>
    private void UpdateDropSlam()
    {
        if (beatManager.Drop.Span.Started)
        {
            ApplyDropHit();
        }

        dropEnv = beatManager.Drop.Span.Decay(DropDecayBeats);

        rotation += dropSpinDir * DropSpinRate * dropEnv * effectDelta * 2f * Mathf.PI;
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        // Beat drives color cycling, not brightness: the hue wheel always turns at the base
        // rate, and the held Waveform's envelope (0..1, peaking on its hits) adds speed on top.
        var beatEnvelope = synth.Evaluate(waveform) ?? 1f;
        hueScroll = Mathf.Repeat(hueScroll + ((HueBaseRate + (beatEnvelope * HueBeatRate)) * effectDelta), 1f);

        // The hub-owned Fill Build becomes extra zoom depth below.
        fillEnv = beatManager.Fill.Span.Build();
        UpdateDropSlam();

        // Breathing zoom (window width oscillates between WindowWidth and MinWindow), deepened
        // exponentially by the Fill dive and blasted back out by the Drop slam.
        var sa = Mathf.Sin(angle).Map01(1f, -1f);
        var window = Mathf.Clamp(
            WindowWidth * sa * Mathf.Exp((DropBlowout * dropEnv) - (FillDiveDepth * fillEnv)),
            MinWindow, WindowWidth);
        var scale = window / penrose.Bounds.size.x;
        angle += speed * effectDelta;

        // Drop spin: rotate wall space into the complex plane.
        var rotCos = Mathf.Cos(rotation);
        var rotSin = Mathf.Sin(rotation);

        for (var i = 0; i < buffer.Length; i++)
        {
            var center = tiles[i].center;
            var pix = Color.black;
            for (var s = 0; s < aaOffsets.Length; s++)
            {
                var world = center + aaOffsets[s];
                var rx = (world.x * rotCos) - (world.y * rotSin);
                var ry = (world.x * rotSin) + (world.y * rotCos);
                pix += EscapeColor(SampleEscape(viewCenter.x + (rx * scale), viewCenter.y + (ry * scale)));
            }

            buffer[i] = pix / aaOffsets.Length;
        }
    }

    /// <summary>
    /// Iterates z → z² + c from the given complex-plane point and returns the fractional
    /// (smooth) escape count, or <see cref="Iterations"/> for points inside the set. The
    /// fractional count keeps the hue continuous — integer counts produce hard hue bands
    /// that flicker as the zoom moves.
    /// </summary>
    private float SampleEscape(float a, float b)
    {
        var n = 0;
        var aa = a * a;
        var bb = b * b;
        while (n < Iterations)
        {
            if (aa + bb > 4f) break;

            var twoAb = 2f * a * b;

            a = aa - bb + c.x;
            b = twoAb + c.y;
            aa = a * a;
            bb = b * b;

            n++;
        }

        if (n == Iterations) return Iterations;

        // Fractional part from how far past the bailout |z| landed: n + 1 - log2(log|z|).
        var logZn = Mathf.Log(aa + bb) * 0.5f;
        var nu = Mathf.Log(logZn / Ln2) / Ln2;
        return Mathf.Max(0f, n + 1f - nu);
    }

    /// <summary>
    /// Maps a smooth escape count to its color: a ramp by escape speed shifted by the
    /// beat-driven hue scroll, black for points inside the set. The activation's color
    /// mode picks the ramp — the shared palette or the HSV rainbow.
    /// </summary>
    private Color EscapeColor(float smoothN)
    {
        if (smoothN >= Iterations) return Color.black;

        var t = Mathf.Repeat(Mathf.Sqrt(smoothN / Iterations) + hueScroll, 1f);
        return usePalette ? APalette.read(t, true) : Color.HSVToRGB(t, 1f, 1f);
    }
}
