using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a Julia fractal by iterating the escape-time function directly at each Penrose
/// tile center. The view is a plain linear transform from wall space to the complex plane
/// (center + uniform scale), so recentering, zooming, and rotating are vector math on the
/// sample coordinates rather than raster operations.
/// </summary>
[EffectSyncSettings(typeof(JuliaSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(JuliaStandaloneSettingsAsset))]
public class Julia : EffectBase
{
    // Standalone Defaults

    /// <summary>Minimum breathing-zoom speed re-rolled on activation and each new Grid.</summary>
    private const float StandaloneBreathingZoomSpeedMin = 0.1f;

    /// <summary>Maximum breathing-zoom speed re-rolled on activation and each new Grid.</summary>
    private const float StandaloneBreathingZoomSpeedMax = 0.3f;

    /// <summary>Width of the complex-plane window, in complex units, at full zoom-out.</summary>
    private const float StandaloneWindowWidthMax = 5f;

    /// <summary>
    /// Floor for the complex-plane window width. Keeps the dive above float precision and
    /// stops the breathing zoom from collapsing to a single flat point at sin = 1.
    /// </summary>
    private const float StandaloneWindowWidthMin = 0.002f;

    /// <summary>Chance that an activation colors from the shared palette instead of the HSV rainbow.</summary>
    private const float StandalonePaletteChance = 0.5f;

    /// <summary>Baseline hue cycling speed in wheel revolutions per second; the colors never stop marching.</summary>
    private const float StandaloneHueBaseRate = 0.05f;

    /// <summary>
    /// Extra hue cycling speed, in wheel revolutions per second, added at the no-clock beat
    /// envelope. The fixed Standalone envelope keeps the cycle at its authored no-music rate.
    /// </summary>
    private const float StandaloneHueBeatRate = 0.25f;

    /// <summary>Fixed drive applied to hue cycling when no live clock can place the held Waveform.</summary>
    private const float StandaloneHueCycleDrive = 1f;

    /// <summary>
    /// Julia constants (c = x + yi) known to produce interesting sets. An authored table rather
    /// than scalar constants: each entry is one curated look, and its pairing with the matching
    /// entry of <see cref="StandalonePresetViewCenters"/> is what the table exists to express.
    /// </summary>
    private static readonly Vector2[] StandaloneJuliaConstants = {
      new(0.285f, 0.01f),
      new(-0.70176f, -0.3842f),
      new(-0.835f, -0.2321f),
      new(-0.8f, 0.156f),
      new(-0.7269f, 0.1889f)
    };

    /// <summary>Per-constant view centers in the complex plane, paired by index with <see cref="StandaloneJuliaConstants"/>.</summary>
    private static readonly Vector2[] StandalonePresetViewCenters = {
      new(0.2f, 0.04f),
      new(-0.125f, -0.04f),
      new(-0.0375f, 0f),
      new(0.175f, 0.05f),
      new(0.0875f, 0.225f)
    };

    // Sync Defaults

    /// <summary>Minimum breathing-zoom speed re-rolled on activation and each new Grid.</summary>
    private const float SyncBreathingZoomSpeedMin = 0.1f;

    /// <summary>Maximum breathing-zoom speed re-rolled on activation and each new Grid.</summary>
    private const float SyncBreathingZoomSpeedMax = 0.3f;

    /// <summary>Width of the complex-plane window, in complex units, at full zoom-out.</summary>
    private const float SyncWindowWidthMax = 5f;

    /// <summary>
    /// Floor for the complex-plane window width. Keeps the dive above float precision and
    /// stops the breathing zoom from collapsing to a single flat point at sin = 1.
    /// </summary>
    private const float SyncWindowWidthMin = 0.002f;

    /// <summary>Chance that a Roll colors from the shared palette instead of the HSV rainbow.</summary>
    private const float SyncPaletteChance = 0.5f;

    /// <summary>Baseline hue cycling speed in wheel revolutions per second; the colors never stop marching.</summary>
    private const float SyncHueBaseRate = 0.05f;

    /// <summary>
    /// Fill dive depth: at full Fill the zoom is e^FillDiveDepth (~7×) deeper than the
    /// breathing zoom alone. Exponential so the dive speed feels constant at any depth.
    /// </summary>
    private const float SyncFillDiveDepth = 2f;

    /// <summary>Beats the Drop slam takes to decay back to rest.</summary>
    private const int SyncDropDecayBeats = 8;

    /// <summary>Spin speed in revolutions per second at the Drop slam's peak.</summary>
    private const float SyncDropSpinRate = 1f;

    /// <summary>
    /// Drop zoom blowout: at the slam's peak the window widens by e^DropBlowout, blasting
    /// back out of the Fill's dive. Clamped so it never zooms out past the full-set view.
    /// </summary>
    private const float SyncDropBlowout = 1.5f;

    /// <summary>Hue-wheel kick at the Drop hit: a half turn, an instant palette inversion.</summary>
    private const float SyncDropHueKick = 0.5f;

    /// <summary>Chance that a Drop hit chooses the negative direction for its spin.</summary>
    private const float SyncNegativeDropSpinChance = 0.5f;

    /// <summary>Hue-cycle drive at the held Waveform's trough.</summary>
    private const float SyncHueCycleDriveMin = 0f;

    /// <summary>Hue-cycle drive at the held Waveform's peak.</summary>
    private const float SyncHueCycleDriveMax = 1f;

    /// <summary>
    /// Extra hue cycling speed, in wheel revolutions per second, added at the beat envelope's
    /// peak. The held Waveform's envelope (0..1, peaking on its hits) scales this, so the cycle
    /// surges on those hits and settles back to the base rate between them.
    /// </summary>
    private const float SyncHueBeatRate = 0.25f;

    /// <summary>
    /// Julia constants (c = x + yi) known to produce interesting sets. This Synced copy keeps its
    /// live tuning independent from Standalone while starting from the identical authored look.
    /// </summary>
    private static readonly Vector2[] SyncJuliaConstants = {
      new(0.285f, 0.01f),
      new(-0.70176f, -0.3842f),
      new(-0.835f, -0.2321f),
      new(-0.8f, 0.156f),
      new(-0.7269f, 0.1889f)
    };

    /// <summary>Synced per-constant view centers paired by index with <see cref="SyncJuliaConstants"/>.</summary>
    private static readonly Vector2[] SyncPresetViewCenters = {
      new(0.2f, 0.04f),
      new(-0.125f, -0.04f),
      new(-0.0375f, 0f),
      new(0.175f, 0.05f),
      new(0.0875f, 0.225f)
    };

    // Runtime mechanism constants

    /// <summary>Escape-time iteration cap; points that survive this long count as inside the set and render black.</summary>
    private const int Iterations = 100;

    /// <summary>ln(2), for the smooth escape-time calculation.</summary>
    private const float Ln2 = 0.6931472f;

    /// <summary>
    /// Anti-aliasing samples per tile; two interleaved rings of AaSamples/2. The angular sweep
    /// covers the complete 0..2π direction domain, so it is an algorithm invariant rather than
    /// an authored range.
    /// </summary>
    private const int AaSamples = 8;

    /// <summary>Anti-aliasing footprint radius around each tile center, in wall units (~half the typical tile spacing).</summary>
    private const float AaRadius = 1.2f;

    /// <summary>
    /// Inner AA-ring radius relative to the outer ring. Its fixed stagger spreads samples across
    /// each tile footprint; it is part of the sampling mechanism rather than artistic tuning.
    /// </summary>
    private const float AaInnerRingScale = 0.55f;

    /// <summary>
    /// Julia's fractal drift suits Low/Mid-energy sections, dives on Fills, and answers
    /// Drops with a spin/blowout slam.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop |
        Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate Julia's authored
    /// Standalone Defaults, including the paired preset tables.
    /// </summary>
    public static JuliaStandaloneSettings StandaloneDefaults => new()
    {
        BreathingZoomSpeed = new FloatRange(
            StandaloneBreathingZoomSpeedMin,
            StandaloneBreathingZoomSpeedMax),
        WindowWidth = new FloatRange(StandaloneWindowWidthMin, StandaloneWindowWidthMax),
        PaletteChance = StandalonePaletteChance,
        HueBaseRate = StandaloneHueBaseRate,
        HueBeatRate = StandaloneHueBeatRate,
        HueCycleDrive = StandaloneHueCycleDrive,
        JuliaConstants = (Vector2[])StandaloneJuliaConstants.Clone(),
        PresetViewCenters = (Vector2[])StandalonePresetViewCenters.Clone(),
    };

    /// <summary>Resolves a fresh copy of Julia's file-local Sync Defaults.</summary>
    public static JuliaSyncSettings SyncDefaults => new()
    {
        BreathingZoomSpeed = new FloatRange(SyncBreathingZoomSpeedMin, SyncBreathingZoomSpeedMax),
        WindowWidth = new FloatRange(SyncWindowWidthMin, SyncWindowWidthMax),
        PaletteChance = SyncPaletteChance,
        HueBaseRate = SyncHueBaseRate,
        FillDiveDepth = SyncFillDiveDepth,
        DropDecayBeats = SyncDropDecayBeats,
        DropSpinRate = SyncDropSpinRate,
        DropBlowout = SyncDropBlowout,
        DropHueKick = SyncDropHueKick,
        NegativeDropSpinChance = SyncNegativeDropSpinChance,
        HueCycleDrive = new FloatRange(SyncHueCycleDriveMin, SyncHueCycleDriveMax),
        HueBeatRate = SyncHueBeatRate,
        JuliaConstants = (Vector2[])SyncJuliaConstants.Clone(),
        PresetViewCenters = (Vector2[])SyncPresetViewCenters.Clone(),
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private JuliaStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private JuliaSyncSettings SyncSettings { get; set; } = SyncDefaults;

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
            var r = AaRadius * (s % 2 == 0 ? 1f : AaInnerRingScale);
            offsets[s] = new(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        return offsets;
    }

    private float angle;

    /// <summary>Current breathing-zoom speed rolled for this activation or Grid.</summary>
    private float speed;

    private Vector2 c;
    private Vector2 viewCenter;

    /// <summary>The curated Julia preset selected for this activation.</summary>
    /// <remarks>
    /// Its <c>0..JuliaConstants.Length</c> roll covers the complete preset catalog, not an
    /// authored subrange, so the bounds remain part of the selection mechanism.
    /// </remarks>
    private int presetIndex;
    private float hueScroll;
    private float fillEnv;
    private float dropEnv;

    /// <summary>The signed unit direction used by the current Drop spin.</summary>
    /// <remarks>
    /// The <c>-1</c>/<c>+1</c> outcomes are the complete direction domain rather than an authored
    /// magnitude range; <see cref="JuliaSyncSettings.NegativeDropSpinChance"/> carries the tunable bias.
    /// </remarks>
    private float dropSpinDir = 1f;
    private float rotation;
    private bool usePalette;
    /// <summary>Whether Drop was active on the preceding frame, retained for local onset detection.</summary>
    private bool previousDropActive;

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
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Julia),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Julia),
            SyncDefaults);

        var isSynced = beatManager.IsSynced;
        var juliaConstants = isSynced
            ? SyncSettings.JuliaConstants
            : standaloneSettings.JuliaConstants;
        var presetViewCenters = isSynced
            ? SyncSettings.PresetViewCenters
            : standaloneSettings.PresetViewCenters;
        presetIndex = Random.Range(0, juliaConstants.Length);
        c = juliaConstants[presetIndex];
        viewCenter = presetViewCenters[presetIndex];
        Reroll();
        hueScroll = 0f;
        fillEnv = 0f;
        dropEnv = 0f;
        rotation = 0f;
        previousDropActive = beatManager.Drop.Active;
        buffer.Clear();
    }

    /// <summary>
    /// Re-rolls the per-activation look: Julia constant preset, zoom speed, color mode
    /// (fresh palette or rainbow), and held Waveform. Called once at activation and
    /// again on each new Grid, so the fractal takes a fresh form in step with the music.
    /// </summary>
    private void Reroll()
    {
        var isSynced = beatManager.IsSynced;
        var breathingZoomSpeed = isSynced
            ? SyncSettings.BreathingZoomSpeed
            : standaloneSettings.BreathingZoomSpeed;
        var paletteChance = isSynced
            ? SyncSettings.PaletteChance
            : standaloneSettings.PaletteChance;
        speed = Random.Range(breathingZoomSpeed.Min, breathingZoomSpeed.Max);
        usePalette = Random.value < paletteChance;
        if (usePalette) APalette.Change();

        // Unfiltered acquisition spans the complete curated Waveform Pool, so Julia has no
        // authored Waveform-selection subrange to expose as Effect Settings.
        waveform = waveforms.Random();
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
    /// Chooses the Drop slam's spin direction and applies its instant hue inversion; Drop Decay
    /// supplies the spin/blowout envelope.
    /// </summary>
    private void ApplyDropHit()
    {
        dropSpinDir = Random.value < SyncSettings.NegativeDropSpinChance ? -1f : 1f;
        hueScroll = Mathf.Repeat(hueScroll + SyncSettings.DropHueKick, 1f);
    }

    /// <summary>
    /// Detects the Drop onset from local prior state, reads Drop Decay, then integrates the spin.
    /// </summary>
    private void UpdateDropSlam()
    {
        var dropActive = beatManager.Drop.Active;
        if (dropActive && !previousDropActive)
        {
            ApplyDropHit();
        }
        previousDropActive = dropActive;

        dropEnv = beatManager.Drop.In.Decay(SyncSettings.DropDecayBeats);

        rotation += dropSpinDir * SyncSettings.DropSpinRate * dropEnv * effectDelta * 2f * Mathf.PI;
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        var isSynced = beatManager.IsSynced;

        // Beat drives color cycling, not brightness: the hue wheel always turns at the base
        // rate, and the held Waveform's envelope (0..1, peaking on its hits) adds speed on top.
        var hueCycleDrive = waveform.Lerp(
            SyncSettings.HueCycleDrive.Min,
            isSynced
                ? SyncSettings.HueCycleDrive.Max
                : standaloneSettings.HueCycleDrive);
        var hueBaseRate = isSynced
            ? SyncSettings.HueBaseRate
            : standaloneSettings.HueBaseRate;
        var hueBeatRate = isSynced
            ? SyncSettings.HueBeatRate
            : standaloneSettings.HueBeatRate;
        hueScroll = Mathf.Repeat(
            hueScroll + ((hueBaseRate + (hueCycleDrive * hueBeatRate)) * effectDelta),
            1f);

        // Fill Build becomes extra zoom depth below.
        fillEnv = beatManager.Fill.In.Build();
        UpdateDropSlam();

        // Breathing zoom (window width oscillates between the range endpoints), deepened
        // exponentially by the Fill dive and blasted back out by the Drop slam.
        var windowWidth = isSynced
            ? SyncSettings.WindowWidth
            : standaloneSettings.WindowWidth;
        var sa = Mathf.Sin(angle).Remap(1f, -1f, 0f, 1f);
        var window = Mathf.Clamp(
            windowWidth.Max * sa * Mathf.Exp(
                (SyncSettings.DropBlowout * dropEnv) - (SyncSettings.FillDiveDepth * fillEnv)),
            windowWidth.Min,
            windowWidth.Max);
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

/// <summary>
/// The serializable value shape shared by Julia's fully populated Standalone Defaults and saved
/// Standalone Settings; Unity may create an empty instance before serialized values are applied.
/// </summary>
[Serializable]
public sealed class JuliaStandaloneSettings
{
    /// <summary>Per-activation and per-Grid breathing-zoom speed range.</summary>
    public FloatRange BreathingZoomSpeed;

    /// <summary>Complex-plane window-width range from the precision floor to full zoom-out.</summary>
    public FloatRange WindowWidth;

    /// <summary>Chance that a color-mode roll selects the shared palette instead of the HSV rainbow.</summary>
    [Range(0f, 1f)] public float PaletteChance;

    /// <summary>Baseline hue cycling speed in wheel revolutions per second.</summary>
    [Min(0f)] public float HueBaseRate;

    /// <summary>Extra hue cycling speed applied by the fixed Standalone hue-cycle drive.</summary>
    [Min(0f)] public float HueBeatRate;

    /// <summary>Fixed drive applied to the extra hue-cycle rate in Standalone Mode.</summary>
    [Range(0f, 1f)] public float HueCycleDrive;

    /// <summary>Julia constants (c = x + yi) known to produce interesting sets.</summary>
    public Vector2[] JuliaConstants;

    /// <summary>Per-constant view centers in the complex plane, paired by index with <see cref="JuliaConstants"/>.</summary>
    public Vector2[] PresetViewCenters;

    /// <summary>Copies every Julia Standalone Setting, including range Rails and preset tables.</summary>
    public void CopyFrom(JuliaStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BreathingZoomSpeed = CopyRange(source.BreathingZoomSpeed);
        WindowWidth = CopyRange(source.WindowWidth);
        PaletteChance = source.PaletteChance;
        HueBaseRate = source.HueBaseRate;
        HueBeatRate = source.HueBeatRate;
        HueCycleDrive = source.HueCycleDrive;
        JuliaConstants = (Vector2[])source.JuliaConstants.Clone();
        PresetViewCenters = (Vector2[])source.PresetViewCenters.Clone();
    }

    /// <summary>Copies one Float Range with its endpoints and live-tuned Rails.</summary>
    private static FloatRange CopyRange(FloatRange source)
    {
        return new FloatRange(source.Min, source.Max, source.LowRail, source.HighRail);
    }
}

/// <summary>The saved-or-default musical-response settings used by Julia in Synced Mode.</summary>
[Serializable]
public sealed class JuliaSyncSettings
{
    /// <summary>Per-activation and per-Grid breathing-zoom speed range.</summary>
    public FloatRange BreathingZoomSpeed;

    /// <summary>Complex-plane window-width range from the precision floor to full zoom-out.</summary>
    public FloatRange WindowWidth;

    /// <summary>Chance that a color-mode Roll selects the shared palette instead of the HSV rainbow.</summary>
    [Range(0f, 1f)] public float PaletteChance;

    /// <summary>Baseline hue cycling speed in wheel revolutions per second.</summary>
    [Min(0f)] public float HueBaseRate;

    /// <summary>Exponential zoom depth added at full Fill.</summary>
    [Min(0f)] public float FillDiveDepth;

    /// <summary>Length of the Drop slam decay in beats.</summary>
    [Min(1)] public int DropDecayBeats;

    /// <summary>Spin speed in revolutions per second at the Drop slam's peak.</summary>
    [Min(0f)] public float DropSpinRate;

    /// <summary>Exponential zoom blowout added at the Drop slam's peak.</summary>
    [Min(0f)] public float DropBlowout;

    /// <summary>Hue-wheel offset applied instantly at a Drop hit.</summary>
    [Range(0f, 1f)] public float DropHueKick;

    /// <summary>Chance that a Drop hit chooses the negative direction for its spin.</summary>
    [Range(0f, 1f)] public float NegativeDropSpinChance;

    /// <summary>Hue-cycle drive range interpolated by the held Waveform.</summary>
    public FloatRange HueCycleDrive;

    /// <summary>Extra hue cycling speed applied at the top of the live beat envelope.</summary>
    [Min(0f)] public float HueBeatRate;

    /// <summary>Julia constants (c = x + yi) known to produce interesting sets.</summary>
    public Vector2[] JuliaConstants;

    /// <summary>Per-constant view centers in the complex plane, paired by index with <see cref="JuliaConstants"/>.</summary>
    public Vector2[] PresetViewCenters;

    /// <summary>Copies every Julia Sync Setting from another value.</summary>
    public void CopyFrom(JuliaSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BreathingZoomSpeed = CopyRange(source.BreathingZoomSpeed);
        WindowWidth = CopyRange(source.WindowWidth);
        PaletteChance = source.PaletteChance;
        HueBaseRate = source.HueBaseRate;
        FillDiveDepth = source.FillDiveDepth;
        DropDecayBeats = source.DropDecayBeats;
        DropSpinRate = source.DropSpinRate;
        DropBlowout = source.DropBlowout;
        DropHueKick = source.DropHueKick;
        NegativeDropSpinChance = source.NegativeDropSpinChance;
        HueCycleDrive = CopyRange(source.HueCycleDrive);
        HueBeatRate = source.HueBeatRate;
        JuliaConstants = (Vector2[])source.JuliaConstants.Clone();
        PresetViewCenters = (Vector2[])source.PresetViewCenters.Clone();
    }

    /// <summary>Copies one Float Range with its endpoints and live-tuned Rails.</summary>
    private static FloatRange CopyRange(FloatRange source)
    {
        return new FloatRange(source.Min, source.Max, source.LowRail, source.HighRail);
    }
}
