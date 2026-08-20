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

    /// <summary>Minimum breathing-zoom speed rolled on the first activation of a Play Mode session.</summary>
    private const float StandaloneBreathingZoomSpeedMin = 0.1f;

    /// <summary>Maximum breathing-zoom speed rolled on the first activation of a Play Mode session.</summary>
    private const float StandaloneBreathingZoomSpeedMax = 0.3f;

    /// <summary>Radius of the Julia constant's circular morph orbit in the complex plane.</summary>
    private const float StandaloneConstantMorphRadius = 0.012f;

    /// <summary>Speed of the Julia constant's circular morph orbit in revolutions per second.</summary>
    private const float StandaloneConstantMorphRate = 0.01f;

    /// <summary>Width of the complex-plane window, in complex units, at full zoom-out.</summary>
    private const float StandaloneWindowWidthMax = 5f;

    /// <summary>
    /// Floor for the complex-plane window width. Keeps the dive above float precision and
    /// stops the breathing zoom from collapsing to a single flat point at sin = 1.
    /// </summary>
    private const float StandaloneWindowWidthMin = 0.002f;

    /// <summary>Lower smooth escape count admitted by the boundary-detail tracker.</summary>
    private const float StandaloneBoundaryEscapeBandMin = 6f;

    /// <summary>Upper smooth escape count admitted by the boundary-detail tracker.</summary>
    private const float StandaloneBoundaryEscapeBandMax = 72f;

    /// <summary>Exponential response rate toward visible boundary detail, in inverse seconds.</summary>
    private const float StandaloneEdgeTrackingRate = 0.18f;

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

    /// <summary>Minimum breathing-zoom speed rolled on the first activation of a Play Mode session.</summary>
    private const float SyncBreathingZoomSpeedMin = 0.1f;

    /// <summary>Maximum breathing-zoom speed rolled on the first activation of a Play Mode session.</summary>
    private const float SyncBreathingZoomSpeedMax = 0.3f;

    /// <summary>Radius of the Julia constant's circular morph orbit in the complex plane.</summary>
    private const float SyncConstantMorphRadius = 0.012f;

    /// <summary>Speed of the Julia constant's circular morph orbit in revolutions per second.</summary>
    private const float SyncConstantMorphRate = 0.01f;

    /// <summary>Width of the complex-plane window, in complex units, at full zoom-out.</summary>
    private const float SyncWindowWidthMax = 5f;

    /// <summary>
    /// Floor for the complex-plane window width. Keeps the dive above float precision and
    /// stops the breathing zoom from collapsing to a single flat point at sin = 1.
    /// </summary>
    private const float SyncWindowWidthMin = 0.002f;

    /// <summary>Lower smooth escape count admitted by the boundary-detail tracker.</summary>
    private const float SyncBoundaryEscapeBandMin = 6f;

    /// <summary>Upper smooth escape count admitted by the boundary-detail tracker.</summary>
    private const float SyncBoundaryEscapeBandMax = 72f;

    /// <summary>Exponential response rate toward visible boundary detail, in inverse seconds.</summary>
    private const float SyncEdgeTrackingRate = 0.18f;

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
        ConstantMorphRadius = StandaloneConstantMorphRadius,
        ConstantMorphRate = StandaloneConstantMorphRate,
        WindowWidth = new FloatRange(StandaloneWindowWidthMin, StandaloneWindowWidthMax),
        BoundaryEscapeBand = new FloatRange(
            StandaloneBoundaryEscapeBandMin,
            StandaloneBoundaryEscapeBandMax),
        EdgeTrackingRate = StandaloneEdgeTrackingRate,
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
        ConstantMorphRadius = SyncConstantMorphRadius,
        ConstantMorphRate = SyncConstantMorphRate,
        WindowWidth = new FloatRange(SyncWindowWidthMin, SyncWindowWidthMax),
        BoundaryEscapeBand = new FloatRange(
            SyncBoundaryEscapeBandMin,
            SyncBoundaryEscapeBandMax),
        EdgeTrackingRate = SyncEdgeTrackingRate,
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

    /// <summary>The current breathing-zoom sine position in radians.</summary>
    private float zoomBreathPhase;

    /// <summary>Breathing-zoom speed rolled once for this Play Mode session.</summary>
    private float breathingZoomSpeed;

    /// <summary>The authored Julia constant at the center of the selected preset's morph orbit.</summary>
    private Vector2 presetConstant;

    /// <summary>The Julia constant at its current position on the session journey's morph orbit.</summary>
    private Vector2 morphedConstant;

    /// <summary>The current normalized position around the Julia constant's circular morph orbit.</summary>
    private float constantMorphPhase;

    /// <summary>Whether the one-time Play Mode session journey Roll has run.</summary>
    private bool sessionJourneyStarted;

    /// <summary>The session journey's current complex-plane view center.</summary>
    private Vector2 viewCenter;

    /// <summary>The latest complex-plane centroid of visible boundary detail.</summary>
    /// <remarks>
    /// Seeded to the activation's view center, so the tracking drift rests at exactly zero
    /// until a frame observes boundary detail.
    /// </remarks>
    private Vector2 edgeTarget;

    /// <summary>The curated Julia preset selected for this Play Mode session.</summary>
    /// <remarks>
    /// Its <c>0..JuliaConstants.Length</c> roll covers the complete preset catalog, not an
    /// authored subrange, so the bounds remain part of the selection mechanism.
    /// </remarks>
    private int presetIndex;

    /// <summary>The hue-wheel offset, reset on each activation.</summary>
    private float hueScroll;

    /// <summary>The current Fill Build envelope used by the zoom dive.</summary>
    private float fillEnv;

    /// <summary>The current Drop Decay envelope used by the spin and zoom blowout.</summary>
    private float dropEnv;

    /// <summary>The signed unit direction used by the current Drop spin.</summary>
    /// <remarks>
    /// The <c>-1</c>/<c>+1</c> outcomes are the complete direction domain rather than an authored
    /// magnitude range; <see cref="JuliaSyncSettings.NegativeDropSpinChance"/> carries the tunable bias.
    /// </remarks>
    private float dropSpinDir = 1f;

    /// <summary>The session journey's accumulated rotation in radians.</summary>
    private float rotation;

    /// <summary>Whether the current color Roll uses the shared palette instead of the HSV rainbow.</summary>
    private bool usePalette;

    /// <summary>Whether Drop was active on the preceding frame, retained for local onset detection.</summary>
    private bool previousDropActive;

    /// <summary>
    /// Called every frame to update the debug UI text element.
    /// </summary>
    public override string DebugText()
    {
        return $"{presetIndex}, {breathingZoomSpeed}, ({viewCenter.x}, {viewCenter.y})\n" +
            (usePalette ? "PALETTE" : "RAINBOW") + $" HUE {hueScroll:0.00}" +
            (fillEnv > 0.01f ? $"\nFILL {fillEnv:0.00}" : "") +
            (dropEnv > 0.01f ? $"\nDROP {dropEnv:0.00}" : "");
    }

    /// <summary>
    /// Resolves the current Settings, starts the session journey once, and performs the shared
    /// activation re-roll while preserving zoom breath, view center, morphed constant, and rotation.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Julia),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Julia),
            SyncDefaults);

        if (!sessionJourneyStarted)
        {
            RollSessionJourney();
        }

        Reroll();
        hueScroll = 0f;
        fillEnv = 0f;
        dropEnv = 0f;
        edgeTarget = viewCenter;
        previousDropActive = beatManager.Drop.Active;
        buffer.Clear();
    }

    /// <summary>
    /// Rolls the preset, breathing-zoom speed, and morph starting phase once for the Play Mode
    /// session. Julia deliberately resumes this session journey when it returns to the wall,
    /// rather than discarding its carried motion state at later Rolls.
    /// </summary>
    private void RollSessionJourney()
    {
        var isSynced = beatManager.IsSynced;
        var juliaConstants = isSynced
            ? SyncSettings.JuliaConstants
            : standaloneSettings.JuliaConstants;
        var presetViewCenters = isSynced
            ? SyncSettings.PresetViewCenters
            : standaloneSettings.PresetViewCenters;
        var breathingZoomSpeedRange = isSynced
            ? SyncSettings.BreathingZoomSpeed
            : standaloneSettings.BreathingZoomSpeed;

        presetIndex = Random.Range(0, juliaConstants.Length);
        presetConstant = juliaConstants[presetIndex];
        viewCenter = presetViewCenters[presetIndex];
        breathingZoomSpeed = Random.Range(
            breathingZoomSpeedRange.Min,
            breathingZoomSpeedRange.Max);
        constantMorphPhase = Random.value;
        sessionJourneyStarted = true;
    }

    /// <summary>
    /// Performs the shared activation/Grid re-roll: color mode, an optional fresh palette, and
    /// held Waveform. It never changes the session journey.
    /// </summary>
    private void Reroll()
    {
        var isSynced = beatManager.IsSynced;
        var paletteChance = isSynced
            ? SyncSettings.PaletteChance
            : standaloneSettings.PaletteChance;
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
    /// On each new Grid Julia re-rolls only its color mode and held Waveform.
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
    /// Advances the session journey's Julia constant deterministically around the preset's
    /// circular orbit, reading the active mode's live Settings on every frame.
    /// </summary>
    private void UpdateConstantMorph(bool isSynced)
    {
        var morphRadius = isSynced
            ? SyncSettings.ConstantMorphRadius
            : standaloneSettings.ConstantMorphRadius;
        var morphRate = isSynced
            ? SyncSettings.ConstantMorphRate
            : standaloneSettings.ConstantMorphRate;

        constantMorphPhase = Mathf.Repeat(
            constantMorphPhase + (morphRate * effectDelta),
            1f);
        var orbitAngle = constantMorphPhase * 2f * Mathf.PI;
        morphedConstant = presetConstant + new Vector2(
            Mathf.Cos(orbitAngle) * morphRadius,
            Mathf.Sin(orbitAngle) * morphRadius);
    }

    /// <summary>
    /// Scores one existing smooth escape result for boundary detail. A triangular weight peaks
    /// halfway through the live boundary band and reaches zero at either endpoint.
    /// </summary>
    private static float BoundaryWeight(float smoothEscape, FloatRange boundaryBand)
    {
        var midpoint = (boundaryBand.Min + boundaryBand.Max) * 0.5f;
        var halfWidth = (boundaryBand.Max - boundaryBand.Min) * 0.5f;
        return Mathf.Max(0f, 1f - (Mathf.Abs(smoothEscape - midpoint) / halfWidth));
    }

    /// <summary>
    /// Renders the current session journey and accumulates its visible boundary target from the
    /// smooth escape results already produced for color, without another fractal pass.
    /// </summary>
    public override void Draw()
    {
        var isSynced = beatManager.IsSynced;
        UpdateConstantMorph(isSynced);

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
        var sa = Mathf.Sin(zoomBreathPhase).Remap(1f, -1f, 0f, 1f);
        var window = Mathf.Clamp(
            windowWidth.Max * sa * Mathf.Exp(
                (SyncSettings.DropBlowout * dropEnv) - (SyncSettings.FillDiveDepth * fillEnv)),
            windowWidth.Min,
            windowWidth.Max);
        var scale = window / penrose.Bounds.size.x;
        zoomBreathPhase += breathingZoomSpeed * effectDelta;

        // Drop spin: rotate wall space into the complex plane.
        var rotCos = Mathf.Cos(rotation);
        var rotSin = Mathf.Sin(rotation);
        var boundaryEscapeBand = isSynced
            ? SyncSettings.BoundaryEscapeBand
            : standaloneSettings.BoundaryEscapeBand;
        var edgeTrackingRate = isSynced
            ? SyncSettings.EdgeTrackingRate
            : standaloneSettings.EdgeTrackingRate;
        var boundaryWeightTotal = 0f;
        var boundaryWeightedX = 0f;
        var boundaryWeightedY = 0f;

        for (var i = 0; i < buffer.Length; i++)
        {
            var center = tiles[i].center;
            var pix = Color.black;
            for (var s = 0; s < aaOffsets.Length; s++)
            {
                var world = center + aaOffsets[s];
                var rx = (world.x * rotCos) - (world.y * rotSin);
                var ry = (world.x * rotSin) + (world.y * rotCos);
                var sampleX = viewCenter.x + (rx * scale);
                var sampleY = viewCenter.y + (ry * scale);
                var smoothEscape = SampleEscape(sampleX, sampleY);
                pix += EscapeColor(smoothEscape);

                var boundaryWeight = BoundaryWeight(smoothEscape, boundaryEscapeBand);
                boundaryWeightTotal += boundaryWeight;
                boundaryWeightedX += sampleX * boundaryWeight;
                boundaryWeightedY += sampleY * boundaryWeight;
            }

            buffer[i] = pix / aaOffsets.Length;
        }

        if (boundaryWeightTotal > 0f)
        {
            edgeTarget = new Vector2(
                boundaryWeightedX / boundaryWeightTotal,
                boundaryWeightedY / boundaryWeightTotal);
        }

        var edgeTrackingBlend = 1f - Mathf.Exp(-edgeTrackingRate * effectDelta);
        viewCenter += edgeTrackingBlend * (edgeTarget - viewCenter);
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

            a = aa - bb + morphedConstant.x;
            b = twoAb + morphedConstant.y;
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
    /// <summary>Breathing-zoom speed range rolled on the first activation of a Play Mode session.</summary>
    public FloatRange BreathingZoomSpeed;

    /// <summary>Radius of the Julia constant's circular morph orbit in the complex plane.</summary>
    public float ConstantMorphRadius;

    /// <summary>Speed of the Julia constant's circular morph orbit in revolutions per second.</summary>
    public float ConstantMorphRate;

    /// <summary>Complex-plane window-width range from the precision floor to full zoom-out.</summary>
    public FloatRange WindowWidth;

    /// <summary>Smooth escape-count band treated as visible Julia boundary detail.</summary>
    public FloatRange BoundaryEscapeBand;

    /// <summary>Exponential response rate toward visible boundary detail, in inverse seconds.</summary>
    public float EdgeTrackingRate;

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
        ConstantMorphRadius = source.ConstantMorphRadius;
        ConstantMorphRate = source.ConstantMorphRate;
        WindowWidth = CopyRange(source.WindowWidth);
        BoundaryEscapeBand = CopyRange(source.BoundaryEscapeBand);
        EdgeTrackingRate = source.EdgeTrackingRate;
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
    /// <summary>Breathing-zoom speed range rolled on the first activation of a Play Mode session.</summary>
    public FloatRange BreathingZoomSpeed;

    /// <summary>Radius of the Julia constant's circular morph orbit in the complex plane.</summary>
    public float ConstantMorphRadius;

    /// <summary>Speed of the Julia constant's circular morph orbit in revolutions per second.</summary>
    public float ConstantMorphRate;

    /// <summary>Complex-plane window-width range from the precision floor to full zoom-out.</summary>
    public FloatRange WindowWidth;

    /// <summary>Smooth escape-count band treated as visible Julia boundary detail.</summary>
    public FloatRange BoundaryEscapeBand;

    /// <summary>Exponential response rate toward visible boundary detail, in inverse seconds.</summary>
    public float EdgeTrackingRate;

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
        ConstantMorphRadius = source.ConstantMorphRadius;
        ConstantMorphRate = source.ConstantMorphRate;
        WindowWidth = CopyRange(source.WindowWidth);
        BoundaryEscapeBand = CopyRange(source.BoundaryEscapeBand);
        EdgeTrackingRate = source.EdgeTrackingRate;
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
