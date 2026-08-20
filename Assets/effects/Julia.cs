using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a Julia fractal by iterating the escape-time function directly at each Penrose
/// tile center. The camera is solved onto an inverse image of a repelling fixed point every
/// frame, so its session journey rides the Julia boundary at every depth. The view remains a
/// plain linear transform from wall space to the complex plane (center + uniform scale), so
/// recentering, zooming, and rotating are vector math rather than raster operations.
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

    /// <summary>Proportion of the full breathing dive reached at the breath's deepest point.</summary>
    private const float StandaloneDepth = 1f;

    /// <summary>
    /// Radius of the Julia constant's circular morph orbit, as a fraction of current window
    /// width. The 0.0024 default preserves the former 0.012 radius at the five-unit full view.
    /// </summary>
    private const float StandaloneConstantMorphRadius = 0.0024f;

    /// <summary>Speed of the Julia constant's circular morph orbit in revolutions per second.</summary>
    private const float StandaloneConstantMorphRate = 0.01f;

    /// <summary>Width of the complex-plane window, in complex units, at full zoom-out.</summary>
    private const float StandaloneWindowWidthMax = 5f;

    /// <summary>
    /// Floor for the complex-plane window width. Keeps the dive above float precision and
    /// stops the breathing zoom from collapsing to a single flat point at sin = 1.
    /// </summary>
    private const float StandaloneWindowWidthMin = 0.002f;

    /// <summary>
    /// Window-width fraction that frames the solved boundary point toward the preset's authored
    /// view center. Zero keeps the boundary point at the camera center.
    /// </summary>
    private const float StandaloneEdgeLockFraming = 0f;

    /// <summary>Density of the inverted squared-exponential fog keyed to smooth escape depth.</summary>
    private const float StandaloneFogDensity = 2f;

    /// <summary>Brightness floor beneath the inverted depth-fog factor.</summary>
    private const float StandaloneFogBrightnessFloor = 0.22f;

    /// <summary>Wall-space azimuth of the fixed relief light, in degrees.</summary>
    private const float StandaloneReliefLightAzimuth = 315f;

    /// <summary>Depth of relief shading below full brightness.</summary>
    private const float StandaloneReliefShadingDepth = 0.72f;

    /// <summary>
    /// Standalone palette conditioning. The absolute target and floor lift dark contour bands;
    /// hue-spread-aware equalization, a tenfold lift ceiling, dark-stop repair, duplicate collapse,
    /// and full redistribution keep neighboring escape contours distinct across palette families.
    /// </summary>
    private static PaletteConditioning StandalonePaletteConditioning => new()
    {
        TargetLuminance = 0.4f,
        MinimumLuminance = 0.12f,
        LuminanceEqualization = 0.85f,
        HueSpreadReference = 0.5f,
        MaximumLuminanceScale = 10f,
        DarkLuminanceThreshold = 0.03f,
        DuplicateThreshold = 0.08f,
        HueRedistribution = 1f,
    };

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

    /// <summary>Proportion of the full breathing dive reached at the breath's deepest point.</summary>
    private const float SyncDepth = 1f;

    /// <summary>
    /// Radius of the Julia constant's circular morph orbit, as a fraction of current window
    /// width. The 0.0024 default preserves the former 0.012 radius at the five-unit full view.
    /// </summary>
    private const float SyncConstantMorphRadius = 0.0024f;

    /// <summary>Speed of the Julia constant's circular morph orbit in revolutions per second.</summary>
    private const float SyncConstantMorphRate = 0.01f;

    /// <summary>Width of the complex-plane window, in complex units, at full zoom-out.</summary>
    private const float SyncWindowWidthMax = 5f;

    /// <summary>
    /// Floor for the complex-plane window width. Keeps the dive above float precision and
    /// stops the breathing zoom from collapsing to a single flat point at sin = 1.
    /// </summary>
    private const float SyncWindowWidthMin = 0.002f;

    /// <summary>
    /// Window-width fraction that frames the solved boundary point toward the preset's authored
    /// view center. Zero keeps the boundary point at the camera center.
    /// </summary>
    private const float SyncEdgeLockFraming = 0f;

    /// <summary>Density of the inverted squared-exponential fog keyed to smooth escape depth.</summary>
    private const float SyncFogDensity = 2f;

    /// <summary>Brightness floor beneath the inverted depth-fog factor.</summary>
    private const float SyncFogBrightnessFloor = 0.22f;

    /// <summary>Wall-space azimuth of the fixed relief light, in degrees.</summary>
    private const float SyncReliefLightAzimuth = 315f;

    /// <summary>Depth of relief shading below full brightness.</summary>
    private const float SyncReliefShadingDepth = 0.72f;

    /// <summary>
    /// Sync palette conditioning, independently authored so live tuning in one mode cannot mutate
    /// the other. It begins with the same luminance band, dark-stop repair, duplicate collapse,
    /// and full redistribution as Standalone.
    /// </summary>
    private static PaletteConditioning SyncPaletteConditioning => new()
    {
        TargetLuminance = 0.4f,
        MinimumLuminance = 0.12f,
        LuminanceEqualization = 0.85f,
        HueSpreadReference = 0.5f,
        MaximumLuminanceScale = 10f,
        DarkLuminanceThreshold = 0.03f,
        DuplicateThreshold = 0.08f,
        HueRedistribution = 1f,
    };

    /// <summary>Chance that a Roll colors from the shared palette instead of the HSV rainbow.</summary>
    private const float SyncPaletteChance = 0.5f;

    /// <summary>Baseline hue cycling speed in wheel revolutions per second; the colors never stop marching.</summary>
    private const float SyncHueBaseRate = 0.05f;

    /// <summary>
    /// Fill dive depth: at full Fill the zoom is e^FillDiveDepth (~7×) deeper than the
    /// breathing zoom alone. Exponential so the dive speed feels constant at any depth.
    /// </summary>
    private const float SyncFillDiveDepth = 2f;

    /// <summary>
    /// Fill envelope attack rate in inverse seconds. The original 22/s response lets even a
    /// one-beat Fill reach its live Build quickly without a one-frame snap.
    /// </summary>
    private const float SyncFillAttackRate = 22f;

    /// <summary>Beats taken to release a full Fill dive back to rest after the Fill ends.</summary>
    private const float SyncFillReleaseBeats = 2f;

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

    /// <summary>
    /// Authored Pool entry name of the one Waveform Julia holds: peaks on counts 2 and 4 and drives
    /// the existing hue-cycle response across activations and Grids.
    /// </summary>
    private const string SyncWaveformName = "beats 2 and 4";

    /// <summary>Hue-cycle drive at the held Waveform's trough.</summary>
    private const float SyncHueCycleDriveMin = 0f;

    /// <summary>Hue-cycle drive at the held Waveform's peak.</summary>
    private const float SyncHueCycleDriveMax = 1f;

    /// <summary>
    /// Extra hue cycling speed, in wheel revolutions per second, added at the beat envelope's
    /// peak. The held Waveform's envelope (0..1, peaking on its hits) scales this, so the cycle
    /// surges on those hits and settles back to the base rate between them.
    /// </summary>
    private const float SyncHueBeatRate = 2f;

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
    /// Squared fixed-point magnitude where |2z| = 1. A fixed point above this threshold is
    /// repelling and therefore lies on the Julia boundary.
    /// </summary>
    private const float RepellingFixedPointMagnitudeSquared = 0.25f;

    /// <summary>
    /// Inverse-orbit depth used to choose the boundary point nearest the preset's authored view
    /// center. Twelve levels bound the one-time search at 4096 exact candidates and each later
    /// frame at twelve inverse roots.
    /// </summary>
    private const int EdgeLockPreimageDepth = 12;

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

    /// <summary>Scale that turns the fitted smooth-count gradient into the relief surface's tilt.</summary>
    private const float ReliefSlope = 0.28f;

    /// <summary>Fixed elevation of the relief light above the wall plane, in degrees.</summary>
    private const float ReliefLightElevationDegrees = 55f;

    /// <summary>
    /// Sum of squared x or y offsets in the symmetric two-ring AA footprint, used by the
    /// least-squares gradient fit.
    /// </summary>
    private const float AaGradientDenominator =
        2f * AaRadius * AaRadius * (1f + (AaInnerRingScale * AaInnerRingScale));

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
        Depth = StandaloneDepth,
        ConstantMorphRadius = StandaloneConstantMorphRadius,
        ConstantMorphRate = StandaloneConstantMorphRate,
        WindowWidth = new FloatRange(StandaloneWindowWidthMin, StandaloneWindowWidthMax),
        EdgeLockFraming = StandaloneEdgeLockFraming,
        FogDensity = StandaloneFogDensity,
        FogBrightnessFloor = StandaloneFogBrightnessFloor,
        ReliefLightAzimuth = StandaloneReliefLightAzimuth,
        ReliefShadingDepth = StandaloneReliefShadingDepth,
        PaletteConditioning = StandalonePaletteConditioning,
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
        Depth = SyncDepth,
        ConstantMorphRadius = SyncConstantMorphRadius,
        ConstantMorphRate = SyncConstantMorphRate,
        WindowWidth = new FloatRange(SyncWindowWidthMin, SyncWindowWidthMax),
        EdgeLockFraming = SyncEdgeLockFraming,
        FogDensity = SyncFogDensity,
        FogBrightnessFloor = SyncFogBrightnessFloor,
        ReliefLightAzimuth = SyncReliefLightAzimuth,
        ReliefShadingDepth = SyncReliefShadingDepth,
        PaletteConditioning = SyncPaletteConditioning,
        PaletteChance = SyncPaletteChance,
        HueBaseRate = SyncHueBaseRate,
        FillDiveDepth = SyncFillDiveDepth,
        FillAttackRate = SyncFillAttackRate,
        FillReleaseBeats = SyncFillReleaseBeats,
        DropDecayBeats = SyncDropDecayBeats,
        DropSpinRate = SyncDropSpinRate,
        DropBlowout = SyncDropBlowout,
        DropHueKick = SyncDropHueKick,
        NegativeDropSpinChance = SyncNegativeDropSpinChance,
        WaveformName = SyncWaveformName,
        HueCycleDrive = new FloatRange(SyncHueCycleDriveMin, SyncHueCycleDriveMax),
        HueBeatRate = SyncHueBeatRate,
        JuliaConstants = (Vector2[])SyncJuliaConstants.Clone(),
        PresetViewCenters = (Vector2[])SyncPresetViewCenters.Clone(),
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private JuliaStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private JuliaSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>
    /// Julia's Effect-local conditioned endpoint cache. It follows shared palette revisions and
    /// the active mode's live conditioning controls without steady-frame allocation.
    /// </summary>
    private readonly ConditionedPaletteCache conditionedPalette = new();

    /// <summary>Anti-aliasing sample offsets around each tile center, in wall units.</summary>
    private static readonly Vector2[] aaOffsets = BuildAaOffsets();

    /// <summary>
    /// Reusable smooth escape counts for one Tile's AA footprint, retained so relief can fit a
    /// gradient without evaluating the fractal twice or allocating per frame.
    /// </summary>
    private readonly float[] smoothEscapeSamples = new float[AaSamples];

    /// <summary>
    /// Reusable inverse-orbit chain from a repelling fixed point to the camera's boundary point;
    /// allocated once so the per-frame edge solve creates no garbage.
    /// </summary>
    private readonly Vector2[] edgeLockChain = new Vector2[EdgeLockPreimageDepth + 1];

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

    /// <summary>The Julia constant at its current zoom-relative position on the session journey's morph orbit.</summary>
    private Vector2 morphedConstant;

    /// <summary>The current normalized position around the Julia constant's circular morph orbit.</summary>
    private float constantMorphPhase;

    /// <summary>Whether the one-time Play Mode session journey Roll has run.</summary>
    private bool sessionJourneyStarted;

    /// <summary>The selected preset's authored view center, retained as the edge-lock framing direction.</summary>
    private Vector2 presetViewCenter;

    /// <summary>The inverse image that edge-locks the camera to the morphed Julia boundary.</summary>
    private Vector2 edgeLockPoint;

    /// <summary>Whether the session journey has selected its inverse-orbit address from the authored preset center.</summary>
    private bool edgeLockInitialized;

    /// <summary>The session journey's current zoom-relative, edge-locked complex-plane view center.</summary>
    private Vector2 viewCenter;

    /// <summary>The curated Julia preset selected for this Play Mode session.</summary>
    /// <remarks>
    /// Its <c>0..JuliaConstants.Length</c> roll covers the complete preset catalog, not an
    /// authored subrange, so the bounds remain part of the selection mechanism.
    /// </remarks>
    private int presetIndex;

    /// <summary>The hue-wheel offset, reset on each activation.</summary>
    private float hueScroll;

    /// <summary>The consumer-local Fill envelope whose fast attack and authored release drive the zoom dive.</summary>
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

    /// <summary>
    /// Pool entry name of the currently held Waveform, so a live Play Mode edit re-acquires while
    /// an unchanged setting leaves the held value, including any owner's replacement, alone.
    /// </summary>
    private string acquiredWaveformName;

    /// <summary>Whether Drop was active on the preceding frame, retained for local onset detection.</summary>
    private bool previousDropActive;

    /// <summary>
    /// Called every frame to update the debug UI text element.
    /// </summary>
    public override string DebugText()
    {
        return $"{presetIndex}, {breathingZoomSpeed}, ({viewCenter.x}, {viewCenter.y})\n" +
            (usePalette ? "PALETTE" : "RAINBOW") + $" HUE {hueScroll:0.00}" +
            $"\nWAVEFORM {acquiredWaveformName}" +
            (fillEnv > 0.01f ? $"\nFILL {fillEnv:0.00}" : "") +
            (dropEnv > 0.01f ? $"\nDROP {dropEnv:0.00}" : "");
    }

    /// <summary>
    /// Resolves the current Settings, acquires the selected Waveform, starts the session journey
    /// once, refreshes the conditioned palette, and performs the color re-roll while preserving
    /// zoom breath, view center, morphed constant, and rotation.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Julia),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Julia),
            SyncDefaults);
        var requestedWaveformName = SyncSettings.WaveformName;
        waveform = waveforms.Named(requestedWaveformName);
        acquiredWaveformName = requestedWaveformName;

        if (!sessionJourneyStarted)
        {
            RollSessionJourney();
        }

        Reroll();
        conditionedPalette.Refresh(APalette, beatManager.IsSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning);
        hueScroll = 0f;
        fillEnv = 0f;
        dropEnv = 0f;
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
        presetViewCenter = presetViewCenters[presetIndex];
        edgeLockPoint = presetViewCenter;
        viewCenter = presetViewCenter;
        edgeLockInitialized = false;
        breathingZoomSpeed = Random.Range(
            breathingZoomSpeedRange.Min,
            breathingZoomSpeedRange.Max);
        constantMorphPhase = Random.value;
        sessionJourneyStarted = true;
    }

    /// <summary>
    /// Performs the shared activation/Grid color re-roll: color mode and an optional fresh palette.
    /// It never changes the session journey or held Waveform.
    /// </summary>
    private void Reroll()
    {
        var isSynced = beatManager.IsSynced;
        var paletteChance = isSynced
            ? SyncSettings.PaletteChance
            : standaloneSettings.PaletteChance;
        usePalette = Random.value < paletteChance;
        if (usePalette) APalette.Change();
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// On each new Grid Julia re-rolls only its color mode and optional fresh palette.
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
    /// circular orbit. Its radius scales with the current window, so morph travel keeps the
    /// same visual pace at every depth while reading the active mode's live Settings each frame.
    /// </summary>
    private void UpdateConstantMorph(bool isSynced, float window)
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
        var orbitRadius = morphRadius * window;
        morphedConstant = presetConstant + new Vector2(
            Mathf.Cos(orbitAngle) * orbitRadius,
            Mathf.Sin(orbitAngle) * orbitRadius);
    }

    /// <summary>
    /// Tracks the live Fill Build with the original fast exponential attack, then releases the
    /// consumer-local dive linearly over the authored number of beats when Build falls away.
    /// </summary>
    private void UpdateFillEnvelope()
    {
        var fillTarget = beatManager.Fill.In.Build();
        if (fillTarget > fillEnv)
        {
            var attackBlend = 1f - Mathf.Exp(-SyncSettings.FillAttackRate * effectDelta);
            fillEnv += (fillTarget - fillEnv) * attackBlend;
            return;
        }

        var releaseSeconds = SyncSettings.FillReleaseBeats *
            beatManager.Timing.BeatAverageMilliseconds.Value /
            1000f;
        fillEnv = Mathf.MoveTowards(fillEnv, fillTarget, effectDelta / releaseSeconds);
    }

    /// <summary>
    /// Re-solves the inverse orbit of a repelling fixed point of z² + c on every frame.
    /// Repelling periodic points and all their inverse images belong to the Julia boundary, so
    /// framing the final point makes a featureless dive structurally impossible. Each root is
    /// chosen nearest its prior chain point, keeping the analytic branch continuous as c morphs.
    /// </summary>
    private void UpdateEdgeLock(float window, float edgeLockFraming)
    {
        var discriminantRoot = ComplexSquareRoot(new Vector2(
            1f - (4f * morphedConstant.x),
            -4f * morphedConstant.y));
        var first = (Vector2.right + discriminantRoot) * 0.5f;
        var second = (Vector2.right - discriminantRoot) * 0.5f;

        if (!edgeLockInitialized)
        {
            var strongerFixedPoint = first.sqrMagnitude >= second.sqrMagnitude ? first : second;
            InitializeEdgeLock(strongerFixedPoint);
        }
        else
        {
            var firstRepels = first.sqrMagnitude > RepellingFixedPointMagnitudeSquared;
            var secondRepels = second.sqrMagnitude > RepellingFixedPointMagnitudeSquared;
            if (firstRepels != secondRepels)
            {
                edgeLockChain[0] = firstRepels ? first : second;
            }
            else
            {
                edgeLockChain[0] = (first - edgeLockChain[0]).sqrMagnitude <=
                    (second - edgeLockChain[0]).sqrMagnitude
                    ? first
                    : second;
            }

            for (var depth = 1; depth <= EdgeLockPreimageDepth; depth++)
            {
                var positiveRoot = ComplexSquareRoot(edgeLockChain[depth - 1] - morphedConstant);
                edgeLockChain[depth] =
                    (positiveRoot - edgeLockChain[depth]).sqrMagnitude <=
                    (-positiveRoot - edgeLockChain[depth]).sqrMagnitude
                    ? positiveRoot
                    : -positiveRoot;
            }

            edgeLockPoint = edgeLockChain[EdgeLockPreimageDepth];
        }

        var framingDirection = (presetViewCenter - edgeLockPoint).normalized;
        viewCenter = edgeLockPoint + edgeLockFraming * window * framingDirection;
    }

    /// <summary>
    /// Searches the complete fixed-depth inverse orbit once, chooses the exact boundary point
    /// nearest the preset's authored view center, and records its branch address in the reusable
    /// chain. Later activations resume that same chain as part of the session journey.
    /// </summary>
    private void InitializeEdgeLock(Vector2 fixedPoint)
    {
        var bestAddress = 0;
        var bestDistanceSquared = float.PositiveInfinity;
        var addressCount = 1 << EdgeLockPreimageDepth;
        for (var address = 0; address < addressCount; address++)
        {
            var candidate = fixedPoint;
            for (var depth = 0; depth < EdgeLockPreimageDepth; depth++)
            {
                candidate = ComplexSquareRoot(candidate - morphedConstant);
                if ((address & (1 << depth)) != 0)
                {
                    candidate = -candidate;
                }
            }

            var distanceSquared = (candidate - presetViewCenter).sqrMagnitude;
            if (distanceSquared < bestDistanceSquared)
            {
                bestAddress = address;
                bestDistanceSquared = distanceSquared;
            }
        }

        edgeLockChain[0] = fixedPoint;
        for (var depth = 1; depth <= EdgeLockPreimageDepth; depth++)
        {
            var point = ComplexSquareRoot(edgeLockChain[depth - 1] - morphedConstant);
            edgeLockChain[depth] = (bestAddress & (1 << (depth - 1))) == 0
                ? point
                : -point;
        }

        edgeLockPoint = edgeLockChain[EdgeLockPreimageDepth];
        edgeLockInitialized = true;
    }

    /// <summary>
    /// Returns the principal square root of one complex value stored as (real, imaginary).
    /// The edge lock considers both its positive and negative roots, so principal-branch sign
    /// changes cannot jump the camera between fixed points.
    /// </summary>
    private static Vector2 ComplexSquareRoot(Vector2 value)
    {
        var magnitude = value.magnitude;
        return new Vector2(
            Mathf.Sqrt((magnitude + value.x) * 0.5f),
            Mathf.Sign(value.y) * Mathf.Sqrt((magnitude - value.x) * 0.5f));
    }

    /// <summary>
    /// Maps smooth escape depth through the baked inverted squared-exponential fog, then remaps
    /// that factor from the live brightness floor to full brightness.
    /// </summary>
    private static float DepthFogBrightness(
        float smoothEscape,
        float fogDensity,
        float brightnessFloor)
    {
        var proximity = Mathf.Sqrt(smoothEscape / Iterations);
        var exponent = fogDensity * (1f - proximity);
        var invertedFog = 1f - Mathf.Exp(-(exponent * exponent));
        return brightnessFloor + ((1f - brightnessFloor) * invertedFog);
    }

    /// <summary>
    /// Lights the fitted escape-potential gradient as a relief normal and dims its unlit side
    /// toward the live shading-depth floor.
    /// </summary>
    private static float ReliefBrightness(
        float gradientX,
        float gradientY,
        float lightX,
        float lightY,
        float lightZ,
        float shadingDepth)
    {
        var normalX = -gradientX * ReliefSlope;
        var normalY = -gradientY * ReliefSlope;
        var inverseNormalLength = 1f / Mathf.Sqrt(
            (normalX * normalX) + (normalY * normalY) + 1f);
        normalX *= inverseNormalLength;
        normalY *= inverseNormalLength;
        var normalZ = inverseNormalLength;
        var lambert = Mathf.Max(
            0f,
            normalX * lightX + normalY * lightY + normalZ * lightZ);
        return 1f - shadingDepth + shadingDepth * lambert;
    }

    /// <summary>
    /// Renders the current session journey after solving its boundary lock. Depth, morph, lock
    /// framing, inverted fog, and relief resolve live from the active mode's Settings without a
    /// second fractal pass or any per-frame heap allocation.
    /// </summary>
    public override void Draw()
    {
        var isSynced = beatManager.IsSynced;
        var requestedWaveformName = SyncSettings.WaveformName;
        if (requestedWaveformName != acquiredWaveformName)
        {
            waveform = waveforms.Named(requestedWaveformName);
            acquiredWaveformName = requestedWaveformName;
        }

        var paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        conditionedPalette.Refresh(APalette, paletteConditioning);

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

        if (isSynced)
        {
            UpdateFillEnvelope();
        }
        else
        {
            fillEnv = 0f;
        }
        UpdateDropSlam();

        // Breathing zoom (window width oscillates between the range endpoints), deepened
        // exponentially by the Fill dive and blasted back out by the Drop slam.
        var windowWidth = isSynced
            ? SyncSettings.WindowWidth
            : standaloneSettings.WindowWidth;
        var depth = isSynced
            ? SyncSettings.Depth
            : standaloneSettings.Depth;
        var sa = Mathf.Sin(zoomBreathPhase).Remap(1f, -1f, 0f, 1f);
        var breathWindow = windowWidth.Max * (1f + ((sa - 1f) * depth));
        var window = Mathf.Clamp(
            breathWindow * Mathf.Exp(
                (SyncSettings.DropBlowout * dropEnv) - (SyncSettings.FillDiveDepth * fillEnv)),
            windowWidth.Min,
            windowWidth.Max);
        var scale = window / penrose.Bounds.size.x;
        zoomBreathPhase += breathingZoomSpeed * effectDelta;

        UpdateConstantMorph(isSynced, window);
        var edgeLockFraming = isSynced
            ? SyncSettings.EdgeLockFraming
            : standaloneSettings.EdgeLockFraming;
        UpdateEdgeLock(window, edgeLockFraming);

        // Drop spin: rotate wall space into the complex plane.
        var rotCos = Mathf.Cos(rotation);
        var rotSin = Mathf.Sin(rotation);

        var fogDensity = isSynced
            ? SyncSettings.FogDensity
            : standaloneSettings.FogDensity;
        var fogBrightnessFloor = isSynced
            ? SyncSettings.FogBrightnessFloor
            : standaloneSettings.FogBrightnessFloor;
        var reliefLightAzimuth = isSynced
            ? SyncSettings.ReliefLightAzimuth
            : standaloneSettings.ReliefLightAzimuth;
        var reliefShadingDepth = isSynced
            ? SyncSettings.ReliefShadingDepth
            : standaloneSettings.ReliefShadingDepth;
        var lightAzimuthRadians = reliefLightAzimuth * Mathf.Deg2Rad;
        var lightElevationRadians = ReliefLightElevationDegrees * Mathf.Deg2Rad;
        var lightHorizontal = Mathf.Cos(lightElevationRadians);
        var lightX = Mathf.Cos(lightAzimuthRadians) * lightHorizontal;
        var lightY = Mathf.Sin(lightAzimuthRadians) * lightHorizontal;
        var lightZ = Mathf.Sin(lightElevationRadians);

        for (var i = 0; i < buffer.Length; i++)
        {
            var center = tiles[i].center;
            var smoothMean = 0f;
            for (var s = 0; s < aaOffsets.Length; s++)
            {
                var world = center + aaOffsets[s];
                var rx = (world.x * rotCos) - (world.y * rotSin);
                var ry = (world.x * rotSin) + (world.y * rotCos);
                var sampleX = viewCenter.x + (rx * scale);
                var sampleY = viewCenter.y + (ry * scale);
                var smoothEscape = SampleEscape(sampleX, sampleY);
                smoothEscapeSamples[s] = smoothEscape;
                smoothMean += smoothEscape;
            }
            smoothMean /= aaOffsets.Length;

            var gradientX = 0f;
            var gradientY = 0f;
            for (var s = 0; s < aaOffsets.Length; s++)
            {
                var centeredEscape = smoothEscapeSamples[s] - smoothMean;
                gradientX += aaOffsets[s].x * centeredEscape;
                gradientY += aaOffsets[s].y * centeredEscape;
            }
            gradientX /= AaGradientDenominator;
            gradientY /= AaGradientDenominator;
            var reliefBrightness = ReliefBrightness(
                gradientX,
                gradientY,
                lightX,
                lightY,
                lightZ,
                reliefShadingDepth);

            var pix = Color.black;
            for (var s = 0; s < aaOffsets.Length; s++)
            {
                var smoothEscape = smoothEscapeSamples[s];
                if (smoothEscape >= Iterations)
                {
                    pix += Color.black;
                    continue;
                }

                var color = EscapeColor(smoothEscape);
                var brightness = DepthFogBrightness(
                    smoothEscape,
                    fogDensity,
                    fogBrightnessFloor) * reliefBrightness;
                pix += new Color(
                    color.r * brightness,
                    color.g * brightness,
                    color.b * brightness,
                    color.a);
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
    /// mode picks the ramp — the conditioned shared palette or the HSV rainbow.
    /// </summary>
    private Color EscapeColor(float smoothN)
    {
        if (smoothN >= Iterations) return Color.black;

        var t = Mathf.Repeat(Mathf.Sqrt(smoothN / Iterations) + hueScroll, 1f);
        return usePalette
            ? conditionedPalette.ReadCyclic(t, doblend: true)
            : Color.HSVToRGB(t, 1f, 1f);
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

    /// <summary>Proportion of the full breathing dive reached at the breath's deepest point.</summary>
    public float Depth;

    /// <summary>Radius of the Julia constant's circular morph orbit, as a fraction of current window width.</summary>
    public float ConstantMorphRadius;

    /// <summary>Speed of the Julia constant's circular morph orbit in revolutions per second.</summary>
    public float ConstantMorphRate;

    /// <summary>Complex-plane window-width range from the precision floor to full zoom-out.</summary>
    public FloatRange WindowWidth;

    /// <summary>Window-width fraction framing the solved boundary point toward its authored preset center.</summary>
    public float EdgeLockFraming;

    /// <summary>Density of the inverted squared-exponential fog keyed to smooth escape depth.</summary>
    [Range(0.1f, 8f)] public float FogDensity;

    /// <summary>Brightness floor beneath the inverted depth-fog factor.</summary>
    [Range(0f, 1f)] public float FogBrightnessFloor;

    /// <summary>Wall-space azimuth of the fixed relief light, in degrees.</summary>
    [Range(0f, 360f)] public float ReliefLightAzimuth;

    /// <summary>Depth of relief shading below full brightness.</summary>
    [Range(0f, 1f)] public float ReliefShadingDepth;

    /// <summary>Live effect-local palette conditioning for Standalone palette contours.</summary>
    public PaletteConditioning PaletteConditioning;

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
        Depth = source.Depth;
        ConstantMorphRadius = source.ConstantMorphRadius;
        ConstantMorphRate = source.ConstantMorphRate;
        WindowWidth = CopyRange(source.WindowWidth);
        EdgeLockFraming = source.EdgeLockFraming;
        FogDensity = source.FogDensity;
        FogBrightnessFloor = source.FogBrightnessFloor;
        ReliefLightAzimuth = source.ReliefLightAzimuth;
        ReliefShadingDepth = source.ReliefShadingDepth;
        PaletteConditioning = source.PaletteConditioning;
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

    /// <summary>Proportion of the full breathing dive reached at the breath's deepest point.</summary>
    public float Depth;

    /// <summary>Radius of the Julia constant's circular morph orbit, as a fraction of current window width.</summary>
    public float ConstantMorphRadius;

    /// <summary>Speed of the Julia constant's circular morph orbit in revolutions per second.</summary>
    public float ConstantMorphRate;

    /// <summary>Complex-plane window-width range from the precision floor to full zoom-out.</summary>
    public FloatRange WindowWidth;

    /// <summary>Window-width fraction framing the solved boundary point toward its authored preset center.</summary>
    public float EdgeLockFraming;

    /// <summary>Density of the inverted squared-exponential fog keyed to smooth escape depth.</summary>
    [Range(0.1f, 8f)] public float FogDensity;

    /// <summary>Brightness floor beneath the inverted depth-fog factor.</summary>
    [Range(0f, 1f)] public float FogBrightnessFloor;

    /// <summary>Wall-space azimuth of the fixed relief light, in degrees.</summary>
    [Range(0f, 360f)] public float ReliefLightAzimuth;

    /// <summary>Depth of relief shading below full brightness.</summary>
    [Range(0f, 1f)] public float ReliefShadingDepth;

    /// <summary>Live effect-local palette conditioning for Synced palette contours.</summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Chance that a color-mode Roll selects the shared palette instead of the HSV rainbow.</summary>
    [Range(0f, 1f)] public float PaletteChance;

    /// <summary>Baseline hue cycling speed in wheel revolutions per second.</summary>
    [Min(0f)] public float HueBaseRate;

    /// <summary>Exponential zoom depth added at full Fill.</summary>
    [Min(0f)] public float FillDiveDepth;

    /// <summary>Fast exponential response rate toward the live Fill Build, in inverse seconds.</summary>
    public float FillAttackRate;

    /// <summary>Beats taken to release a full consumer-local Fill envelope back to rest.</summary>
    public float FillReleaseBeats;

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

    /// <summary>
    /// Live Pool entry name of the one Waveform Julia holds for its hue-cycle response. A name
    /// missing from the Pool is a configuration error and fails visibly.
    /// </summary>
    [WaveformName]
    public string WaveformName;

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
        Depth = source.Depth;
        ConstantMorphRadius = source.ConstantMorphRadius;
        ConstantMorphRate = source.ConstantMorphRate;
        WindowWidth = CopyRange(source.WindowWidth);
        EdgeLockFraming = source.EdgeLockFraming;
        FogDensity = source.FogDensity;
        FogBrightnessFloor = source.FogBrightnessFloor;
        ReliefLightAzimuth = source.ReliefLightAzimuth;
        ReliefShadingDepth = source.ReliefShadingDepth;
        PaletteConditioning = source.PaletteConditioning;
        PaletteChance = source.PaletteChance;
        HueBaseRate = source.HueBaseRate;
        FillDiveDepth = source.FillDiveDepth;
        FillAttackRate = source.FillAttackRate;
        FillReleaseBeats = source.FillReleaseBeats;
        DropDecayBeats = source.DropDecayBeats;
        DropSpinRate = source.DropSpinRate;
        DropBlowout = source.DropBlowout;
        DropHueKick = source.DropHueKick;
        NegativeDropSpinChance = source.NegativeDropSpinChance;
        WaveformName = source.WaveformName;
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
