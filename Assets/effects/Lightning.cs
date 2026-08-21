using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
// Chuck Sommerville

/// <summary>
/// Builds stochastic branching paths outward from center-star tiles.
/// </summary>
[Serializable]
[EffectSyncSettings(typeof(LightningSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(LightningStandaloneSettingsAsset))]
public class Lightning : EffectBase
{
    // Standalone Defaults

    /// <summary>Starting-hue drift magnitude applied when the coin flip enables that animation in the Standalone look.</summary>
    private const float StandaloneStartHueDelta = 0.02f;

    /// <summary>Per-ray hue drift magnitude applied when the coin flip enables that animation in the Standalone look.</summary>
    private const float StandaloneRayHueDelta = 0.2f;

    /// <summary>Per-tile hue drift magnitude applied when the coin flip enables that animation in the Standalone look.</summary>
    private const float StandaloneTileHueDelta = 0.02f;

    /// <summary>Bolt brightness at the held Waveform's peak; the range minimum and the no-placement fallback, preserving the authored inverse pulse.</summary>
    private const float StandaloneWaveformBrightnessMin = 0.75f;

    /// <summary>Bolt brightness at the held Waveform's trough; the range maximum.</summary>
    private const float StandaloneWaveformBrightnessMax = 1f;

    /// <summary>
    /// Lifts dark Standalone palette entries into a visible working range without changing the hue
    /// walk. Wall-tuned: the third-luminance floor keeps bolt tiles reading against the dark field,
    /// and the mild duplicate collapse and redistribution shift conditioned entries off the authored
    /// positions the brightest-entry scan samples, an accepted approximation. Zero equalization
    /// preserves authored brightness relationships.
    /// </summary>
    private static PaletteConditioning StandalonePaletteConditioning => new()
    {
        TargetLuminance = 0.5f,
        MinimumLuminance = 0.33333f,
        LuminanceEqualization = 0f,
        HueSpreadReference = 1f,
        MaximumLuminanceScale = 4f,
        DarkLuminanceThreshold = 0.2f,
        DuplicateThreshold = 0.1f,
        HueRedistribution = 0.1f,
    };

    // Sync Defaults

    /// <summary>Bolt brightness at the held Waveform's peak; the range minimum, preserving the authored inverse pulse.</summary>
    private const float SyncWaveformBrightnessMin = 0.75f;

    /// <summary>Bolt brightness at the held Waveform's trough; the range maximum, preserving the authored inverse pulse.</summary>
    private const float SyncWaveformBrightnessMax = 1f;

    /// <summary>Starting-hue drift magnitude used by the Synced look when its coin flip enables that animation.</summary>
    private const float SyncStartHueDelta = 0.02f;

    /// <summary>Per-ray hue drift magnitude used by the Synced look when its coin flip enables that animation.</summary>
    private const float SyncRayHueDelta = 0.2f;

    /// <summary>Per-tile hue drift magnitude used by the Synced look when its coin flip enables that animation.</summary>
    private const float SyncTileHueDelta = 0.02f;

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    private const float SyncWaveformHueOffset = 0.5f;

    /// <summary>Pool Preset name of the Waveform held for brightness and hue response.</summary>
    private const string SyncWaveformName = "beats 2 and 4";

    /// <summary>Low-band level above which an On the Beat gate opens the flash.</summary>
    private const float SyncOnBeatLowThreshold = 0.4f;

    /// <summary>Levels form sampled for the bass-gated On the Beat flash.</summary>
    private const LevelsForm SyncLowLevelsForm = LevelsForm.Normalized;

    /// <summary>Hue-wheel offset applied to the burning ray.</summary>
    private const float SyncFlashHueOffset = 0.5f;

    /// <summary>Beats taken for the burning ray to decay after its gate closes.</summary>
    private const float SyncFlashDecayBeats = 0.5f;

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
    private const float SyncDropFieldWhiteFlash = 0.25f;

    /// <summary>Trail-fade amount held during the Drop slam (near 1 = slow fade): the bolt trails linger under the flood. Tune on the readout.</summary>
    private const float SyncDropTrailFade = 0.97f;

    /// <summary>Pulse duration whose rising edge re-walks the held Fill bolt.</summary>
    private const Duration SyncFillRewalkDuration = Duration.Sixteenth;

    /// <summary>Pulse duration that drives the held Fill bolt's strobe gate.</summary>
    private const Duration SyncFillStrobeDuration = Duration.Sixteenth;

    /// <summary>Bolt brightness while the Fill's strobe gate is closed (0 = full black blink, 1 = no strobe):
    /// the held bolt hard-blinks between this and full on every strobe pulse (sixteenths by default). Tune on the readout.</summary>
    private const float SyncFillStrobeFloor = 0.15f;

    /// <summary>Fraction of each strobe pulse the Fill strobe gate stays lit (duty cycle, 0..1): smaller = shorter, sharper flashes. Tune on the readout.</summary>
    private const float SyncFillStrobeDuty = 0.5f;

    /// <summary>
    /// Lifts dark Synced palette entries into a visible working range without changing the hue walk.
    /// Wall-tuned: the third-luminance floor keeps bolt tiles reading against the dark field, and
    /// the mild duplicate collapse and redistribution shift conditioned entries off the authored
    /// positions the brightest-entry scan samples, an accepted approximation. Zero equalization
    /// preserves authored brightness relationships.
    /// </summary>
    private static PaletteConditioning SyncPaletteConditioning => new()
    {
        TargetLuminance = 0.5f,
        MinimumLuminance = 0.33333f,
        LuminanceEqualization = 0f,
        HueSpreadReference = 1f,
        MaximumLuminanceScale = 4f,
        DarkLuminanceThreshold = 0.2f,
        DuplicateThreshold = 0.1f,
        HueRedistribution = 0.1f,
    };

    // Runtime mechanism constants

    /// <summary>Beats per bar used to express the authored Drop decay length in beats.</summary>
    private const int BeatsPerBar = 4;

    /// <summary>
    /// Pure yellow, the full-saturation and full-value rainbow hue with the greatest relative
    /// luminance. White is outside the rainbow branch's source domain.
    /// </summary>
    private static readonly Color BrightestRainbowColor = new(1f, 1f, 0f, 1f);

    /// <summary>Lightning is a sharp beat-scaled burst. On a Fill it HOLDS a frozen bolt that hard-snaps to entirely
    /// new positions on every rewalk pulse (sixteenth notes by default) while strobing on the strobe pulses
    /// (sixteenths by default) (see <see cref="Draw"/>) — held, but
    /// rewalking. On a Drop it inverts: an intensity swell, electric flicker, and a figure/ground flip where the wall
    /// floods with the rolled colors and the bolts cut through as dark negative space (see <see cref="OnNewGrid"/>);
    /// its electric energy suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh copy of Lightning's file-local Standalone Defaults.</summary>
    public static LightningStandaloneSettings StandaloneDefaults => new()
    {
        StartHueDelta = StandaloneStartHueDelta,
        RayHueDelta = StandaloneRayHueDelta,
        TileHueDelta = StandaloneTileHueDelta,
        WaveformBrightness = new FloatRange(
            StandaloneWaveformBrightnessMin,
            StandaloneWaveformBrightnessMax),
        PaletteConditioning = StandalonePaletteConditioning,
    };

    /// <summary>Resolves a fresh copy of Lightning's file-local Sync Defaults.</summary>
    public static LightningSyncSettings SyncDefaults => new()
    {
        StartHueDelta = SyncStartHueDelta,
        RayHueDelta = SyncRayHueDelta,
        TileHueDelta = SyncTileHueDelta,
        WaveformBrightness = new FloatRange(
            SyncWaveformBrightnessMin,
            SyncWaveformBrightnessMax),
        WaveformName = SyncWaveformName,
        WaveformHueOffset = SyncWaveformHueOffset,
        OnBeatLowThreshold = SyncOnBeatLowThreshold,
        LowLevelsForm = SyncLowLevelsForm,
        FlashHueOffset = SyncFlashHueOffset,
        FlashDecayBeats = SyncFlashDecayBeats,
        DropBars = SyncDropBars,
        DropValueLift = SyncDropValueLift,
        DropFlickerDepth = SyncDropFlickerDepth,
        DropFlickerHz = SyncDropFlickerHz,
        DropFieldFlood = SyncDropFieldFlood,
        DropFieldWhiteFlash = SyncDropFieldWhiteFlash,
        DropTrailFade = SyncDropTrailFade,
        FillRewalkDuration = SyncFillRewalkDuration,
        FillStrobeDuration = SyncFillStrobeDuration,
        FillStrobeFloor = SyncFillStrobeFloor,
        FillStrobeDuty = SyncFillStrobeDuty,
        PaletteConditioning = SyncPaletteConditioning,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private LightningStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private LightningSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>
    /// Lightning's Effect-local conditioned palette cache. It follows the shared palette crossfade,
    /// palette revisions, and the active mode's live conditioning controls.
    /// </summary>
    private readonly ConditionedPaletteCache conditionedPalette = new();

    /// <summary>The shared animated palette represented by <see cref="brightestPaletteColor"/>.</summary>
    private AnimPalette brightestPaletteOwner;

    /// <summary>The shared palette revision represented by <see cref="brightestPaletteColor"/>.</summary>
    private int brightestPaletteRevision = -1;

    /// <summary>The live conditioning controls represented by <see cref="brightestPaletteColor"/>.</summary>
    private PaletteConditioning brightestPaletteConditioning;

    /// <summary>The shared crossfade position represented by <see cref="brightestPaletteColor"/>.</summary>
    private float brightestPaletteTransitionProgress = -1f;

    /// <summary>The brightest color in the conditioned palette output for the current frame.</summary>
    private Color brightestPaletteColor;

    /// <summary>Current trail-fade amount rolled across the complete 0..1 fade domain; the full-domain roll is mechanism rather than an authored subrange.</summary>
    private float fadeValue;

    /// <summary>Current starting hue rolled across the complete 0..1 hue domain; the full hue wheel is structural rather than an authored subrange.</summary>
    private float starthue;

    /// <summary>Current signed starting-hue drift; its inline rolls span the complete off/on and direction domains while the enabled drift magnitude comes from the mode-selected settings surface.</summary>
    private float deltastart = 0f;

    /// <summary>Current signed per-ray hue drift; its inline rolls span the complete off/on and direction domains while the enabled drift magnitude comes from the mode-selected settings surface.</summary>
    private float deltaray = 0f;

    /// <summary>Current signed per-tile hue drift; its inline rolls span the complete off/on and direction domains while the enabled drift magnitude comes from the mode-selected settings surface.</summary>
    private float deltatile = 0f;

    /// <summary>Current beat-response mode; the inline [0, 3) roll spans all three algorithm modes and is not an authored subrange.</summary>
    private int beatMode;

    /// <summary>Current color-mode slot; the inline [0, 4) roll spans the complete one-HSV/three-palette weighting and is not an authored subrange.</summary>
    private int mode = 0;

    /// <summary>During a Fill the walked bolt freezes here and is only re-walked on the rewalk pulse; one cached tile path per center-star ray.</summary>
    private List<int>[] heldRays;

    /// <summary>The Star Motif nearest the layout origin, cached for bolt roots and flash-ray selection.</summary>
    private LayoutData.ShapeList.Group centerStar;

    /// <summary>True while the Fill hold/rewalk/strobe mode is driving the bolt (surfaced on the readout).</summary>
    private bool heldActive;
    /// <summary>Previous frame's rewalk-gate state; the rising edge triggers the Fill re-walk.</summary>
    private bool previousRewalkOn;

    /// <summary>Pool Preset name used for the current held Waveform acquisition.</summary>
    private string acquiredWaveformName;

    /// <summary>Previous frame's bass-gated On the Beat state; its rising edge chooses the burning ray.</summary>
    private bool previousFlashGate;

    /// <summary>Index of the ray whose path and flash response remain held for the flash lifetime.</summary>
    private int burningRayIndex;

    /// <summary>Burning-ray intensity, held at one while gated and released over the authored beat count.</summary>
    private float flashEnvelope;

    /// <summary>Drop slam amount (1 at the downbeat, then falling linearly to 0 over <see cref="LightningSyncSettings.DropBars"/>); drives the value lift, flicker, field inversion, and trail hold.</summary>
    private float dropEnv;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"fade: {fadeValue}\n starthue:{starthue}\n deltastart:{deltastart}\n deltaray:{deltaray}\n deltatile:{deltatile}\n mode:{mode}" +
            (heldActive ? $"\n FILL hold/rewalk {SyncSettings.FillRewalkDuration}, strobe {SyncSettings.FillStrobeDuration}" : "") +
            (dropEnv > 0.01f ? $"\n DROP {dropEnv:0.00}" : "");
    }

    /// <summary>
    /// Initializes live settings, center-star geometry, and per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Lightning),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Lightning),
            SyncDefaults);
        var requestedWaveformName = SyncSettings.WaveformName;
        waveform = waveforms.Named(requestedWaveformName);
        acquiredWaveformName = requestedWaveformName;
        RefreshCenterStar();
        buffer.Clear();
        Reroll();

        heldRays = null;
        heldActive = false;
        previousRewalkOn = false;
        previousFlashGate = false;
        burningRayIndex = -1;
        flashEnvelope = 0f;

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
        bool isSynced = beatManager.IsSynced;
        float startHueDelta = isSynced ? SyncSettings.StartHueDelta : standaloneSettings.StartHueDelta;
        float rayHueDelta = isSynced ? SyncSettings.RayHueDelta : standaloneSettings.RayHueDelta;
        float tileHueDelta = isSynced ? SyncSettings.TileHueDelta : standaloneSettings.TileHueDelta;
        deltastart = Random.Range(0, 2) == 0 ? 0f : startHueDelta;
        deltaray = Random.Range(0, 2) == 0 ? 0f : rayHueDelta;
        deltatile = Random.Range(0, 2) == 0 ? 0f : tileHueDelta;
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
    /// On each new Grid the center-star cache follows any refreshed layout before the bolts take a fresh form.
    /// Drop intensity is read independently from the hub's direct Drop decay, so this hook owns only Lightning's
    /// Grid-aligned geometry refresh and visual reroll.
    /// </summary>
    protected override void OnNewGrid()
    {
        RefreshCenterStar();
        Reroll();
    }

    /// <summary>
    /// Caches the Star Motif whose centroid has the smallest squared distance from the layout origin. Refreshing
    /// this allocation-free group view at activation and each new Grid follows layout changes without treating
    /// packed payload order as geometry.
    /// </summary>
    private void RefreshCenterStar()
    {
        LayoutData.ShapeList.Reader stars = penrose.Layout.shapes.Stars;
        int centerGroupIndex = 0;
        float centerRadiusSquared = stars.GetCentroid(0).sqrMagnitude;
        for (int groupIndex = 1; groupIndex < stars.GroupCount; groupIndex++)
        {
            float radiusSquared = stars.GetCentroid(groupIndex).sqrMagnitude;
            if (radiusSquared < centerRadiusSquared)
            {
                centerGroupIndex = groupIndex;
                centerRadiusSquared = radiusSquared;
            }
        }

        centerStar = stars.GetGroup(centerGroupIndex);
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
        Color floodColor = Color.Lerp(fieldColor * flicker, Color.white, SyncSettings.DropFieldWhiteFlash * dropEnv);
        float flood = dropEnv * SyncSettings.DropFieldFlood;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Color.Lerp(buffer[i], floodColor, flood);
        }
    }

    /// <summary>
    /// Updates the held bolt path. Outside a Fill the bolt re-walks every frame in both modes, preserving the original
    /// dancing look; inside a Fill it freezes and only re-walks on the rising edge of the configured rewalk gate
    /// (sixteenth notes by default), so the whole branch hard-snaps to new positions in step with that pulse instead of
    /// flowing continuously. If the beat gate is unavailable, it holds. An active burning ray is preserved through
    /// every re-walk so its path lasts for the whole flash.
    /// </summary>
    private void UpdateHeldBolt()
    {
        heldActive = beatManager.Fill.Active;
        bool fillRewalkOn = beatManager.Pulses.On(SyncSettings.FillRewalkDuration);
        int preservedRayIndex = flashEnvelope > 0f ? burningRayIndex : -1;
        if (heldActive)
        {
            if ((fillRewalkOn && !previousRewalkOn) || heldRays == null)
            {
                GenerateBolt(preservedRayIndex);
            }
        }
        else
        {
            GenerateBolt(preservedRayIndex);
        }
        previousRewalkOn = fillRewalkOn;
    }

    /// <summary>
    /// Holds one freshly rolled ray at full flash while any On the Beat lane and the selected low-band form open
    /// the gate, then releases it over the authored beat count. Standalone clears the local state because every
    /// musical group rests there; only the gate-opening ray choice consumes Random, as effect art.
    /// </summary>
    private void UpdateFlash()
    {
        if (!beatManager.IsSynced)
        {
            previousFlashGate = false;
            burningRayIndex = -1;
            flashEnvelope = 0f;
            return;
        }

        bool flashGate =
            (beatManager.Beats.OnBeat(1) ||
             beatManager.Beats.OnBeat(2) ||
             beatManager.Beats.OnBeat(3) ||
             beatManager.Beats.OnBeat(4)) &&
            beatManager.Levels.Select(SyncSettings.LowLevelsForm).Low >
            SyncSettings.OnBeatLowThreshold;
        if (flashGate)
        {
            if (!previousFlashGate)
            {
                int rayCount = centerStar.TileCount;
                burningRayIndex = Random.Range(0, rayCount);
            }
            flashEnvelope = 1f;
        }
        else if (SyncSettings.FlashDecayBeats == 0f)
        {
            burningRayIndex = -1;
            flashEnvelope = 0f;
        }
        else if (flashEnvelope > 0f)
        {
            float decaySeconds = SyncSettings.FlashDecayBeats *
                beatManager.Timing.BeatAverageMilliseconds.Value /
                1000f;
            flashEnvelope = Mathf.MoveTowards(flashEnvelope, 0f, effectDelta / decaySeconds);
            if (flashEnvelope == 0f)
            {
                burningRayIndex = -1;
            }
        }

        previousFlashGate = flashGate;
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
        bool isSynced = beatManager.IsSynced;
        var requestedWaveformName = SyncSettings.WaveformName;
        if (requestedWaveformName != acquiredWaveformName)
        {
            waveform = waveforms.Named(requestedWaveformName);
            acquiredWaveformName = requestedWaveformName;
        }

        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        RefreshConditionedPalette(paletteConditioning);

        float rhythm = waveform.Envelope;
        FloatRange brightnessRange = isSynced
            ? SyncSettings.WaveformBrightness
            : standaloneSettings.WaveformBrightness;
        float waveformBrightness = waveform.Lerp(brightnessRange.Max, brightnessRange.Min);
        float waveformHueOffset = SyncSettings.WaveformHueOffset * rhythm;

        dropEnv = beatManager.Drop.In.Decay(SyncSettings.DropBars * BeatsPerBar);
        float flicker = DropFlicker();

        buffer.Fade(dropEnv.Lerp(fadeValue, SyncSettings.DropTrailFade));
        FloodDropField(flicker);

        UpdateFlash();
        UpdateHeldBolt();
        float strobe = FillStrobe();

        RenderBolt(waveformBrightness, waveformHueOffset, flicker, strobe);
    }

    /// <summary>
    /// Refreshes the conditioned palette for the active settings surface and recomputes its brightest
    /// output only when the owner, revision, controls, or live crossfade position changes.
    /// </summary>
    /// <param name="conditioning">The mode-selected live palette conditioning controls.</param>
    private void RefreshConditionedPalette(PaletteConditioning conditioning)
    {
        conditionedPalette.Refresh(APalette, conditioning);

        int revision = APalette.Revision;
        float transitionProgress = APalette.TransitionProgress;
        if (ReferenceEquals(APalette, brightestPaletteOwner) &&
            revision == brightestPaletteRevision &&
            brightestPaletteConditioning.Matches(conditioning) &&
            transitionProgress == brightestPaletteTransitionProgress)
        {
            return;
        }

        brightestPaletteColor = FindBrightestConditionedPaletteColor();
        brightestPaletteOwner = APalette;
        brightestPaletteRevision = revision;
        brightestPaletteConditioning = conditioning;
        brightestPaletteTransitionProgress = transitionProgress;
    }

    /// <summary>
    /// Finds the conditioned palette output with greatest relative luminance without consuming
    /// Random. During a crossfade, scanning both endpoint entry coordinates covers every linear
    /// segment endpoint, where a relative-luminance maximum must occur.
    /// </summary>
    /// <returns>The brightest conditioned palette color at the current crossfade position.</returns>
    private Color FindBrightestConditionedPaletteColor()
    {
        GPalette current = APalette.CurrentPalette;
        Color brightest = conditionedPalette.Read(0f, doblend: true);
        float brightestLuminance = brightest.RelativeLuminance();

        for (int i = 1; i < current.length; i++)
        {
            float coordinate = LegacyPaletteEntryCoordinate(i, current.length);
            Color candidate = conditionedPalette.Read(coordinate, doblend: true);
            float luminance = candidate.RelativeLuminance();
            if (luminance > brightestLuminance)
            {
                brightest = candidate;
                brightestLuminance = luminance;
            }
        }

        if (APalette.IsTransitioning)
        {
            GPalette next = APalette.NextPalette;
            for (int i = 0; i < next.length; i++)
            {
                float coordinate = LegacyPaletteEntryCoordinate(i, next.length);
                Color candidate = conditionedPalette.Read(coordinate, doblend: true);
                float luminance = candidate.RelativeLuminance();
                if (luminance > brightestLuminance)
                {
                    brightest = candidate;
                    brightestLuminance = luminance;
                }
            }
        }

        return brightest;
    }

    /// <summary>
    /// Returns the legacy non-cyclic coordinate for one palette entry. A one-entry palette lives at
    /// zero, while a longer palette distributes its entries evenly across the closed zero-to-one
    /// domain.
    /// </summary>
    /// <param name="index">The zero-based palette entry index.</param>
    /// <param name="length">The palette entry count.</param>
    /// <returns>The normalized coordinate that samples the requested entry.</returns>
    private static float LegacyPaletteEntryCoordinate(int index, int length)
    {
        if (length == 1)
        {
            return 0f;
        }

        return (float)index / (length - 1);
    }

    /// <summary>
    /// Walks the stochastic branch path outward from each center-star tile and caches the visited tile indices in
    /// <see cref="heldRays"/>. Splitting the walk (here) from the coloring (<see cref="RenderBolt"/>) is what lets a
    /// Fill hold one bolt and re-walk it only on the rewalk; outside a Fill it is simply called every frame, preserving
    /// the original per-frame stochastic redraw. During a flash, <paramref name="preservedRayIndex"/> keeps the burning
    /// path while the other rays re-walk.
    /// </summary>
    /// <param name="preservedRayIndex">Ray index to retain, or -1 when every ray should be re-walked.</param>
    private void GenerateBolt(int preservedRayIndex = -1)
    {
        int rayCount = centerStar.TileCount;
        if (heldRays == null || heldRays.Length != rayCount)
        {
            heldRays = new List<int>[rayCount];
            preservedRayIndex = -1;
        }

        Span<int> possible = stackalloc int[4]; // holds possible step positions
        for (int j = 0; j < centerStar.TileCount; j++)
        {
            if (j == preservedRayIndex)
                continue;

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
    /// bright-bolts-on-black look. Each ray root substitutes the brightest color from the active source before all
    /// downstream brightness, flicker, strobe, Drop, and trail behavior runs unchanged. The burning ray blends toward
    /// a fully saturated, full-value hue-shifted color without the inverse Waveform dim, then decays back to the ordinary
    /// bolt. It renders last so an overlapping ordinary walk cannot hide any of its flash tiles.
    /// </summary>
    private void RenderBolt(float waveformBrightness, float waveformHueOffset, float flicker, float strobe)
    {
        if (heldRays == null)
            return;

        // for each of the center-star rays
        Color centerColor = mode != 0 ? brightestPaletteColor : BrightestRainbowColor;
        bool flashActive = burningRayIndex >= 0 && flashEnvelope > 0f;
        float firstRayHue = starthue;
        float rayhue = firstRayHue;
        starthue += deltastart;
        for (int rayOrder = 0; rayOrder < heldRays.Length; rayOrder++)
        {
            int r = rayOrder;
            if (flashActive)
            {
                if (rayOrder == heldRays.Length - 1)
                    r = burningRayIndex;
                else if (rayOrder >= burningRayIndex)
                    r++;
            }
            List<int> ray = heldRays[r];
            bool rayBurning = r == burningRayIndex && flashActive;
            float tilehue = flashActive ? firstRayHue + (deltaray * r) : rayhue;
            rayhue += deltaray;
            for (int k = 0; k < ray.Count; k++)
            {
                int currentIdx = ray[k];
                // color the current tile under the rolled palette/mode
                Color strokeColor = k == 0 ? centerColor : RolledColor(tilehue);
                Color flashStrokeColor = strokeColor;

                if (beatMode < 2)
                    strokeColor *= waveformBrightness;
                if (beatMode > 0)
                {
                    Color.RGBToHSV(strokeColor, out float h, out float s, out float v);
                    strokeColor = Color.HSVToRGB((h + waveformHueOffset) % 1f, s, v);
                }

                if (dropEnv > 0f)
                {
                    // Swell intensity in value space (caps at 1) so the rolled hue/saturation are untouched and it
                    // never washes toward white — pure "change of intensity," felt as the bolts return after the slam.
                    Color.RGBToHSV(strokeColor, out float dh, out float ds, out float dv);
                    strokeColor = Color.HSVToRGB(dh, ds, (SyncSettings.DropValueLift * dropEnv).Lerp(dv, 1f));
                }
                Color boltColor = strokeColor * waveformBrightness * flicker * strobe;
                if (rayBurning)
                {
                    float flashWaveformHueOffset = beatMode > 0 ? waveformHueOffset : 0f;
                    Color.RGBToHSV(flashStrokeColor, out float fh, out _, out _);
                    flashStrokeColor = Color.HSVToRGB(
                        (fh + flashWaveformHueOffset + SyncSettings.FlashHueOffset) % 1f,
                        1f,
                        1f);
                    Color flashBoltColor = flashStrokeColor * flicker * strobe;
                    boltColor = Color.Lerp(boltColor, flashBoltColor, flashEnvelope);
                }
                // Invert the bolt toward black so it reads as a dark cut through the flooded field at the Drop's peak,
                // returning to a bright bolt as the Drop decays. At dropEnv 0 this is just the bright bolt.
                buffer[currentIdx] = Color.Lerp(boltColor, Color.black, dropEnv);
                tilehue += deltatile;
            }
        }
    }

    /// <summary>
    /// Maps a (possibly negative) hue to a color under the current <see cref="mode"/>: the shared animated palette
    /// conditioned by the active settings surface when mode is non-zero, otherwise an unconditioned,
    /// fully-saturated HSV color. The +10000 bias keeps the modulo positive so hues driven negative by the rolled
    /// deltas still wrap cleanly into [0,1).
    /// Full saturation and value define the HSV rainbow branch, so both complete-domain endpoints remain structural inline literals.
    /// </summary>
    private Color RolledColor(float hue)
    {
        float wrapped = (hue + 10000f) % 1f;
        return mode != 0
            ? conditionedPalette.Read(wrapped, doblend: true)
            : Color.HSVToRGB(wrapped, 1f, 1f);
    }
}

/// <summary>The serializable value shape shared by Lightning's Standalone Defaults and Standalone Settings.</summary>
[Serializable]
public sealed class LightningStandaloneSettings
{
    /// <summary>Drift magnitude applied to the starting hue when its coin flip enables that animation.</summary>
    public float StartHueDelta;

    /// <summary>Drift magnitude applied per ray when its coin flip enables that animation.</summary>
    public float RayHueDelta;

    /// <summary>Drift magnitude applied per tile when its coin flip enables that animation.</summary>
    public float TileHueDelta;

    /// <summary>Bolt-brightness range whose maximum is the held Waveform's trough and whose minimum is the peak and no-placement fallback, preserving the authored inverse pulse.</summary>
    public FloatRange WaveformBrightness;

    /// <summary>
    /// Live effect-local palette conditioning for Standalone Mode, independently saved so tuning it
    /// cannot drift the Synced look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Copies every Lightning Standalone Setting from another value.</summary>
    public void CopyFrom(LightningStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        StartHueDelta = source.StartHueDelta;
        RayHueDelta = source.RayHueDelta;
        TileHueDelta = source.TileHueDelta;
        WaveformBrightness = new FloatRange(
            source.WaveformBrightness.Min,
            source.WaveformBrightness.Max,
            source.WaveformBrightness.LowRail,
            source.WaveformBrightness.HighRail);
        PaletteConditioning = source.PaletteConditioning;
    }
}

/// <summary>The saved musical-response settings used by Lightning in Synced Mode.</summary>
[Serializable]
public sealed class LightningSyncSettings
{
    /// <summary>Drift magnitude applied to the starting hue when its coin flip enables that animation.</summary>
    [Min(0f)] public float StartHueDelta;

    /// <summary>Drift magnitude applied per ray when its coin flip enables that animation.</summary>
    [Min(0f)] public float RayHueDelta;

    /// <summary>Drift magnitude applied per tile when its coin flip enables that animation.</summary>
    [Min(0f)] public float TileHueDelta;

    /// <summary>Bolt-brightness range whose maximum is the held Waveform's trough and whose minimum is the peak and no-placement fallback, preserving the authored inverse pulse.</summary>
    public FloatRange WaveformBrightness;

    /// <summary>
    /// Live Pool entry name of the one Waveform Lightning holds for its inverse brightness and hue response. A name
    /// missing from the Pool is a configuration error and fails visibly.
    /// </summary>
    [WaveformName]
    public string WaveformName;

    /// <summary>
    /// Live effect-local palette conditioning for Synced Mode, independently saved so tuning it
    /// cannot drift the Standalone look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Maximum hue-wheel offset contributed by the held Waveform.</summary>
    [Range(0f, 1f)] public float WaveformHueOffset;

    /// <summary>Low-band level above which an On the Beat gate opens the flash.</summary>
    [Range(0f, 1f)] public float OnBeatLowThreshold;

    /// <summary>Levels form sampled for the bass-gated On the Beat flash.</summary>
    public LevelsForm LowLevelsForm;

    /// <summary>Hue-wheel offset applied to the burning ray.</summary>
    [Range(0f, 1f)] public float FlashHueOffset;

    /// <summary>Beats taken for the burning ray to decay after its gate closes; zero makes the gate a square cut.</summary>
    [Min(0f)] public float FlashDecayBeats;

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
    [Range(0f, 1f)] public float DropFieldWhiteFlash;

    /// <summary>Trail-fade amount held during the Drop slam.</summary>
    [Range(0f, 1f)] public float DropTrailFade;

    /// <summary>Pulse duration whose rising edge re-walks the held Fill bolt.</summary>
    public Duration FillRewalkDuration;

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
            throw new ArgumentNullException(nameof(source));
        }

        StartHueDelta = source.StartHueDelta;
        RayHueDelta = source.RayHueDelta;
        TileHueDelta = source.TileHueDelta;
        WaveformBrightness = new FloatRange(
            source.WaveformBrightness.Min,
            source.WaveformBrightness.Max,
            source.WaveformBrightness.LowRail,
            source.WaveformBrightness.HighRail);
        WaveformName = source.WaveformName;
        PaletteConditioning = source.PaletteConditioning;
        WaveformHueOffset = source.WaveformHueOffset;
        OnBeatLowThreshold = source.OnBeatLowThreshold;
        LowLevelsForm = source.LowLevelsForm;
        FlashHueOffset = source.FlashHueOffset;
        FlashDecayBeats = source.FlashDecayBeats;
        DropBars = source.DropBars;
        DropValueLift = source.DropValueLift;
        DropFlickerDepth = source.DropFlickerDepth;
        DropFlickerHz = source.DropFlickerHz;
        DropFieldFlood = source.DropFieldFlood;
        DropFieldWhiteFlash = source.DropFieldWhiteFlash;
        DropTrailFade = source.DropTrailFade;
        FillRewalkDuration = source.FillRewalkDuration;
        FillStrobeDuration = source.FillStrobeDuration;
        FillStrobeFloor = source.FillStrobeFloor;
        FillStrobeDuty = source.FillStrobeDuty;
    }
}
