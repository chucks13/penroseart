using Random = UnityEngine.Random;
using System;
using UnityEngine;

/// <summary>
/// Renders a palette hue sweep based on each tile's stored geometric angle.
/// </summary>
/// <remarks>
/// FILL / DROP (preserved behind temporary rebuild flags): one soft-edged wavefront, ordered by each tile's hue distance from the wall's own mean
/// hue (closest first), compresses the wall toward that charged mean color. An active Fill advances the
/// front through its <see cref="InSpan.Build"/>. Independently, <see cref="DropValues.Before"/> advances
/// the same front so a Drop receives preparation even when no Fill precedes it. A Fill that ends without
/// a Drop eases its compression away instead of snapping back or inventing a relationship the wire does
/// not carry.
///
/// An active Drop has exclusive priority. At its landing the whole wall holds near-black while fully
/// compressed, then one Drop release timeline both expands the hues and reignites a staccato cascade
/// through the tiling's ten orientation classes — the multiples-of-18° directional families of the
/// underlying pentagrid. Because orientation drives hue here, each class is also a single hue, so the
/// rainbow and its ten hidden families return together out of the darkness. The four-bar Routine keeps
/// its own full-pattern hue rotation and does not drive this choreography; that Routine hue offset is
/// likewise preserved behind temporary rebuild scaffolding.
///
/// SHADING (preserved behind temporary rebuild scaffolding): a gentle directional brightness gradient keyed to each tile's orientation (as if the faceted
/// quasicrystal were lit from one direction) gives the ten families brightness definition, not just hue.
/// Its depth scales with musical <see cref="BeatManager.Energy"/> — subtle in low-energy sections, more
/// pronounced in high-energy ones. This is pure geometry plus a nullable Energy read, so it renders steady
/// at a fixed mid depth in Standalone (no beat clock) rather than going flat.
///
/// On every new Grid the sweep speed and held Waveform re-roll, so the look changes character every 16
/// beats even outside a Fill/Drop. The shading light direction is seeded once per activation instead:
/// re-rolling it at a Grid caused a visible flash, and it drives no pixels while shading is disabled.
/// </remarks>
[EffectSyncSettings(typeof(AnglesSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(AnglesStandaloneSettingsAsset))]
public class Angles : EffectBase
{
    // Temporary musical-layer rebuild flags

    /// <summary>Temporary rebuild scaffolding for Fill/pre-Drop hue compression; this layer returns reshaped or is deleted when its turn comes.</summary>
    private const bool EnableFillAndPreDropHueCompression = false;

    /// <summary>Temporary rebuild scaffolding for Drop blackout and orientation-class reignition; this layer returns reshaped or is deleted when its turn comes.</summary>
    private const bool EnableDropBlackoutAndReignition = false;

    /// <summary>Temporary rebuild scaffolding for Energy-driven directional shading; this layer returns reshaped or is deleted when its turn comes.</summary>
    private const bool EnableEnergyDrivenDirectionalShading = false;

    /// <summary>Temporary rebuild scaffolding for the Routine rhythm hue offset; this layer returns reshaped or is deleted when its turn comes.</summary>
    private const bool EnableRoutineRhythmHueOffset = false;

    // Standalone Defaults

    /// <summary>
    /// Angle-to-hue gain: the hue distance between adjacent orientation classes. One maps the 180°
    /// angular domain across a single hue cycle, spacing the classes 0.1 apart; larger values push
    /// them further apart and raise the colour contrast between directions.
    /// </summary>
    /// <remarks>
    /// This does not add colours. The tiling carries exactly ten orientation clusters on the 18°
    /// pentagrid (verified against the 900-tile data), so ten is the ceiling however high the gain
    /// goes. Integer gains alias: the visible count is 10/gcd(10, gain), so two and four collapse to
    /// five colours and five collapses to two. Any later musical layer that sweeps this value must
    /// avoid resting on those points.
    /// </remarks>
    private const float StandaloneSpread = 1f;

    /// <summary>
    /// Standalone palette-family conditioning. The absolute target and the floor put every palette in
    /// the same working band, so a palette authored dark no longer arrives dark; luminance equalization
    /// tames one dominant colour, backing off through the hue-spread reference on palettes whose
    /// entries share a hue and are told apart by brightness alone; bounded lift prevents amplification
    /// from exploding; the nonzero dark threshold replaces black and near-black stops that would switch
    /// tiles off while retaining authored dark colour above it; duplicate collapse and full
    /// redistribution give the ten orientation classes distinct colour positions. Tune on the wall.
    /// </summary>
    private static PaletteConditioning StandalonePaletteConditioning => new PaletteConditioning
    {
        TargetLuminance = 0.4f,
        MinimumLuminance = 0.12f,
        LuminanceEqualization = 0.85f,
        HueSpreadReference = 0.5f,
        MaximumLuminanceScale = 4f,
        DarkLuminanceThreshold = 0.03f,
        DuplicateThreshold = 0.08f,
        HueRedistribution = 1f,
    };

    /// <summary>Minimum sweep speed re-rolled on activation and each new Grid.</summary>
    private const float StandaloneSpeedMin = 0.15f;

    /// <summary>Maximum sweep speed re-rolled on activation and each new Grid.</summary>
    private const float StandaloneSpeedMax = 0.4f;

    /// <summary>Directional-shading depth at Low energy: the dimmest orientation drops this far below full (so its floor is 1 - this). Kept shallow so calm sections stay subtle. Tune on the readout.</summary>
    private const float StandaloneShadeDepthLow = 0.12f;

    /// <summary>Directional-shading depth at High energy: deeper contrast so intense sections read the ten families more strongly, without ever going as dark as the Drop. Tune on the readout.</summary>
    private const float StandaloneShadeDepthHigh = 0.4f;

    /// <summary>Energy level assumed when <see cref="EnergyValues.Level"/> has no value: 0.5 = Mid, a steady moderate shading depth. Tune on the readout.</summary>
    private const float StandaloneEnergy = 0.5f;

    /// <summary>Smoothing rate (per second) easing the shading depth between energy tiers, so a Low/Mid/High change ramps over ~0.5s instead of snapping. Tune on the readout.</summary>
    private const float StandaloneEnergySmoothing = 2f;

    /// <summary>Fixed full-cycle Routine hue offset returned when no live clock can place the choreography.</summary>
    private const float StandaloneRhythmHueOffset = 1f;

    // Sync Defaults

    /// <summary>
    /// Sync palette-family conditioning, independently authored so ADR-0013 live tuning in one mode
    /// cannot drift the other. It starts equal to Standalone: one working luminance band with a floor,
    /// hue-spread-aware equalization, bounded lift, no black stops, collapsed duplicates, and full
    /// colour-distance redistribution. Tune on the wall.
    /// </summary>
    private static PaletteConditioning SyncPaletteConditioning => new PaletteConditioning
    {
        TargetLuminance = 0.4f,
        MinimumLuminance = 0.12f,
        LuminanceEqualization = 0.85f,
        HueSpreadReference = 0.5f,
        MaximumLuminanceScale = 4f,
        DarkLuminanceThreshold = 0.03f,
        DuplicateThreshold = 0.08f,
        HueRedistribution = 1f,
    };

    /// <summary>Width, in normalized rank space (0..1), of the hue-compression wavefront's soft edge. Smaller = a crisper traveling edge; larger = a blurrier gradient.</summary>
    private const float SyncFrontSoftness = 0.12f;

    /// <summary>Per-second smoothing rate used only when hue compression has no active Fill or approaching Drop to sustain it, so an unpaired Fill relaxes instead of snapping. Tune on the TENSION readout.</summary>
    private const float SyncCompressionReleaseRate = 2f;

    /// <summary>Drop window in beats: preparation reaches full hue compression across this many beats before landing, then blackout release and orientation reignition share the same length after it. Kept short so the event reads within a 2-4 beat window.</summary>
    private const int SyncDropBeats = 3;

    /// <summary>Brightness the wall drops to during the blackout — a floor, not literal off, so it reads as intentional impact rather than a crash. Tune on the readout.</summary>
    private const float SyncDarkFloor = 0.04f;

    /// <summary>Fraction of the active Drop window held fully black and hue-compressed before release begins — the punctuation of the impact. Tune on the readout.</summary>
    private const float SyncBlackHold = 0.12f;

    /// <summary>Reignition snap width (in cascade-progress units) for each class: smaller = harder staccato pop as each orientation family lights; larger = a softer roll. Tune on the readout.</summary>
    private const float SyncClassSnapWidth = 0.08f;

    /// <summary>First energy pool sampled for the four-bar Routine choreography.</summary>
    private const Energy SyncRoutineEnergyOne = Energy.Mid;

    /// <summary>Second energy pool sampled for the four-bar Routine choreography.</summary>
    private const Energy SyncRoutineEnergyTwo = Energy.Low;

    /// <summary>Third energy pool sampled for the four-bar Routine choreography.</summary>
    private const Energy SyncRoutineEnergyThree = Energy.Mid;

    /// <summary>Fourth energy pool sampled for the four-bar Routine choreography.</summary>
    private const Energy SyncRoutineEnergyFour = Energy.Low;

    /// <summary>Directional-shading depth at Low energy: the dimmest orientation drops this far below full (so its floor is 1 - this). Kept shallow so calm sections stay subtle. Tune on the readout.</summary>
    private const float SyncShadeDepthLow = 0.12f;

    /// <summary>Directional-shading depth at High energy: deeper contrast so intense sections read the ten families more strongly, without ever going as dark as the Drop. Tune on the readout.</summary>
    private const float SyncShadeDepthHigh = 0.4f;

    /// <summary>Smoothing rate (per second) easing the shading depth between energy tiers, so a Low/Mid/High change ramps over ~0.5s instead of snapping. Tune on the readout.</summary>
    private const float SyncEnergySmoothing = 2f;

    /// <summary>Minimum hue rotation applied at the bottom of the live Routine envelope.</summary>
    private const float SyncRhythmHueOffsetMin = 0.8f;

    /// <summary>Maximum hue rotation applied at the top of the live Routine envelope.</summary>
    private const float SyncRhythmHueOffsetMax = 1f;

    // Runtime mechanism constants

    /// <summary>Number of orientation (tileangle) classes the wall reignites through, one per 18° pentagrid direction. Verified against the 900-tile data: exactly 10 classes of 62-119 tiles each.</summary>
    private const int OrientationClasses = 10;

    /// <summary>
    /// Advertises no Fill/Drop capability or Energy character while Angles returns to its bare base effect,
    /// preventing the Director from casting it to musical Anchors until those layers are rebuilt.
    /// </summary>
    public override Repertoire Repertoire => Repertoire.None;

    /// <summary>
    /// Resolves a fresh immutable-by-convention copy of Angles' Standalone Defaults, including
    /// live angle spread, effect-local palette conditioning, and independent speed and directional-
    /// shading depth ranges.
    /// </summary>
    public static AnglesStandaloneSettings StandaloneDefaults => new AnglesStandaloneSettings
    {
        Spread = StandaloneSpread,
        PaletteConditioning = StandalonePaletteConditioning,
        Speed = new FloatRange(StandaloneSpeedMin, StandaloneSpeedMax),
        ShadeDepth = new FloatRange(
            StandaloneShadeDepthLow,
            StandaloneShadeDepthHigh,
            0f,
            1f),
        Energy = StandaloneEnergy,
        EnergySmoothing = StandaloneEnergySmoothing,
        RhythmHueOffset = StandaloneRhythmHueOffset,
    };

    /// <summary>
    /// Resolves a fresh copy of Angles' file-local Sync Defaults, including independent palette
    /// conditioning, directional-shading depth, and Routine hue-offset ranges.
    /// </summary>
    public static AnglesSyncSettings SyncDefaults => new AnglesSyncSettings
    {
        PaletteConditioning = SyncPaletteConditioning,
        FrontSoftness = SyncFrontSoftness,
        CompressionReleaseRate = SyncCompressionReleaseRate,
        DropBeats = SyncDropBeats,
        DarkFloor = SyncDarkFloor,
        BlackHold = SyncBlackHold,
        ClassSnapWidth = SyncClassSnapWidth,
        RoutineEnergyOne = SyncRoutineEnergyOne,
        RoutineEnergyTwo = SyncRoutineEnergyTwo,
        RoutineEnergyThree = SyncRoutineEnergyThree,
        RoutineEnergyFour = SyncRoutineEnergyFour,
        ShadeDepth = new FloatRange(
            SyncShadeDepthLow,
            SyncShadeDepthHigh,
            0f,
            1f),
        EnergySmoothing = SyncEnergySmoothing,
        RhythmHueOffset = new FloatRange(
            SyncRhythmHueOffsetMin,
            SyncRhythmHueOffsetMax,
            0f,
            1f),
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private AnglesStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private AnglesSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The shared animated palette instance from which the current Angles-owned copies derive.</summary>
    private AnimPalette conditionedPaletteOwner;

    /// <summary>The shared palette endpoint revision represented by the current conditioned copies.</summary>
    private int conditionedPaletteRevision = -1;

    /// <summary>The live Angles conditioning controls represented by the current conditioned copies.</summary>
    private PaletteConditioning conditionedPaletteSettings;

    /// <summary>The immutable shared source represented by <see cref="conditionedCurrentPalette"/>.</summary>
    private GPalette conditionedCurrentSource;

    /// <summary>The immutable shared source represented by <see cref="conditionedNextPalette"/>.</summary>
    private GPalette conditionedNextSource;

    /// <summary>Angles' conditioned copy of the shared current palette endpoint.</summary>
    private GPalette conditionedCurrentPalette;

    /// <summary>Angles' conditioned copy of the shared next palette endpoint.</summary>
    private GPalette conditionedNextPalette;

    /// <summary>Current sweep speed rolled for this activation or Grid.</summary>
    private float speed;

    /// <summary>Bounded hue-wheel position integrated from sweep speed, seeded from the activation's randomized <see cref="EffectBase.effectTime"/> phase so speed changes alter velocity without teleporting position.</summary>
    private float huePhase;

    /// <summary>Four-bar waveform choreography, one Waveform per bar drawn from the energy pools named by <see cref="AnglesSyncSettings.RoutineEnergyOne"/> through <see cref="AnglesSyncSettings.RoutineEnergyFour"/>.</summary>
    private Routine routine;

    /// <summary>Current tension (0..1) expressed as progress of the hue-compression wavefront toward <see cref="meanHue"/>.</summary>
    private float hueCompression;

    /// <summary>Each tile's raw angle-hue (pre-Spread, pre-sweep, pre-beat), cached once since <see cref="Penrose.TileData.tileangle"/> never changes.</summary>
    private float[] rawHue;

    /// <summary>Per tile, the shortest signed hue delta (in [-0.5, 0.5)) from <see cref="rawHue"/> toward <see cref="meanHue"/>, cached once.</summary>
    private float[] hueDelta;

    /// <summary>Per tile, the cascade-progress point (0..~0.9) at which its orientation class reignites during a Drop — its class index / <see cref="OrientationClasses"/>. Cached once.</summary>
    private float[] classReveal;

    /// <summary>Per tile, its normalized rank (0..1) by ascending hue-distance from <see cref="meanHue"/>, cached once. The tile with the closest hue ranks 0 and compresses first.</summary>
    /// <remarks>
    /// This holds the rank alone, not the wavefront envelope value at which the tile's collapse begins.
    /// <see cref="Draw"/> scales it by the live <see cref="AnglesSyncSettings.FrontSoftness"/> each frame to
    /// reach that start value, so a Play Mode edit of the soft-edge width moves the whole front coherently
    /// instead of only its trailing edge.
    /// </remarks>
    private float[] frontRank;

    /// <summary>Per tile, its folded orientation in [0,1) (tileangle mod 180° / 180°), cached once. Drives the directional shading; wraps smoothly so same-facing tiles (0° ≡ 180°) shade identically.</summary>
    private float[] orient01;

    /// <summary>Circular mean of every tile's raw angle-hue: the charged color the Fill/Drop choreography compresses toward.</summary>
    private float meanHue;

    /// <summary>Direction (radians) the shading gradient is "lit" from; seeded once per activation so a Grid boundary cannot flash the bright/shadowed sides of the orientation field.</summary>
    /// <remarks>
    /// Its <c>0..2π</c> roll is deliberately not captured as a Standalone randomization range. A full turn is the
    /// complete angular domain of a direction, not an authored span — narrowing it would stop the light reaching
    /// some orientations at all, which is a different effect rather than a tuning of this one.
    /// </remarks>
    private float lightPhase;

    /// <summary>Energy level (0..1) smoothed frame-to-frame, driving shading depth. Seeded to <see cref="StandaloneEnergy"/> so the first frame and all of Standalone render at a steady mid depth.</summary>
    private float smoothedEnergy;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() =>
        "Angles" +
        $"\nEN {smoothedEnergy:0.00}" +
        (hueCompression > 0.01f ? $"\nTENSION {hueCompression:0.00}" : "") +
        (beatManager.Drop.Active
            ? $"\nDROP {beatManager.Drop.In.Build(SyncSettings.DropBeats):0.00}"
            : "");

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        PrecomputeTileFields();
    }

    /// <summary>
    /// Caches the static per-tile geometry used by Fill, Drop, and directional shading.
    /// </summary>
    private void PrecomputeTileFields()
    {
        int total = tiles.Length;
        rawHue = new float[total];
        hueDelta = new float[total];
        classReveal = new float[total];
        frontRank = new float[total];
        orient01 = new float[total];

        float sumX = 0f, sumY = 0f;
        for (int i = 0; i < total; i++)
        {
            rawHue[i] = tiles[i].tileangle / 180f;
            float radians = rawHue[i] * Mathf.PI * 2f;
            sumX += Mathf.Cos(radians);
            sumY += Mathf.Sin(radians);
        }
        meanHue = Mathf.Repeat(Mathf.Atan2(sumY, sumX) / (Mathf.PI * 2f), 1f);

        float[] distance = new float[total];
        int[] order = new int[total];
        for (int i = 0; i < total; i++)
        {
            hueDelta[i] = Mathf.Repeat(meanHue - rawHue[i] + 0.5f, 1f) - 0.5f;
            distance[i] = Mathf.Abs(hueDelta[i]);
            order[i] = i;

            // Folded orientation in [0,1): tileangle mod 180° normalized. Same field feeds both the Drop
            // cascade order (snapped to the nearest 18° class) and the continuous directional shading.
            float folded = Mathf.Repeat(tiles[i].tileangle, 180f) / 180f;
            orient01[i] = folded;
            int cls = Mathf.RoundToInt(folded * OrientationClasses) % OrientationClasses;
            classReveal[i] = cls / (float)OrientationClasses;
        }
        Array.Sort(order, (a, b) => distance[a].CompareTo(distance[b]));

        for (int rank = 0; rank < total; rank++)
        {
            float normalizedRank = total > 1 ? rank / (float)(total - 1) : 0f;
            frontRank[order[rank]] = normalizedRank;
        }
    }

    /// <summary>
    /// Resolves Effect Settings and initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Angles),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Angles),
            SyncDefaults);
        RefreshConditionedPalettes(beatManager.IsSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning);
        Reroll();
        lightPhase = Random.Range(0f, Mathf.PI * 2f);
        huePhase = Mathf.Repeat(effectTime * speed, 1f);
        hueCompression = 0f;
        smoothedEnergy = standaloneSettings.Energy;
        controller.debugText.text = DebugText();
        buffer.Clear();
    }

    /// <summary>
    /// Re-rolls the per-activation or per-Grid sweep speed and four-bar Waveform Routine, so the look takes a
    /// fresh character every 16 beats. The shading light direction is intentionally seeded only in
    /// <see cref="OnStart"/> because changing it on a Grid caused a visible flash.
    /// </summary>
    private void Reroll()
    {
        speed = Random.Range(standaloneSettings.Speed.Min, standaloneSettings.Speed.Max);
        routine = Routine.Of(
            waveforms.Random(SyncSettings.RoutineEnergyOne),
            waveforms.Random(SyncSettings.RoutineEnergyTwo),
            waveforms.Random(SyncSettings.RoutineEnergyThree),
            waveforms.Random(SyncSettings.RoutineEnergyFour));
    }

    /// <summary>
    /// On each new Grid the sweep takes a fresh speed and four-bar Waveform routine.
    /// </summary>
    protected override void OnNewGrid()
    {
        Reroll();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Reuses a conditioned endpoint when its immutable source is already cached, otherwise derives
    /// one new effect-local palette with the current Angles controls.
    /// </summary>
    /// <param name="source">The shared immutable palette endpoint to represent.</param>
    /// <param name="previousCurrentSource">The source of the previous conditioned current endpoint.</param>
    /// <param name="previousCurrent">The previous conditioned current endpoint.</param>
    /// <param name="previousNextSource">The source of the previous conditioned next endpoint.</param>
    /// <param name="previousNext">The previous conditioned next endpoint.</param>
    /// <param name="conditioning">The unchanged live controls used by every reusable endpoint.</param>
    /// <returns>A reusable or newly conditioned Angles-owned palette, or null for a null endpoint.</returns>
    private static GPalette ReuseOrCondition(
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

    /// <summary>
    /// Refreshes Angles' current and next conditioned copies only when the shared palette endpoints
    /// or live conditioning controls change. A landed next endpoint rotates into current without
    /// reconditioning, preserving the shared three-second fade with no steady-frame allocation.
    /// </summary>
    private void RefreshConditionedPalettes(PaletteConditioning conditioning)
    {
        AnimPalette owner = APalette;
        bool ownerChanged = !ReferenceEquals(owner, conditionedPaletteOwner);
        bool settingsChanged = ownerChanged || !conditionedPaletteSettings.Matches(conditioning);
        bool revisionChanged = ownerChanged || owner.Revision != conditionedPaletteRevision;
        if (!settingsChanged && !revisionChanged)
        {
            return;
        }

        GPalette currentSource = owner.CurrentPalette;
        GPalette nextSource = owner.NextPalette;
        GPalette previousCurrentSource = conditionedCurrentSource;
        GPalette previousCurrent = conditionedCurrentPalette;
        GPalette previousNextSource = conditionedNextSource;
        GPalette previousNext = conditionedNextPalette;

        GPalette current = settingsChanged
            ? currentSource.Conditioned(conditioning)
            : ReuseOrCondition(
                currentSource,
                previousCurrentSource,
                previousCurrent,
                previousNextSource,
                previousNext,
                conditioning);
        GPalette next = ReferenceEquals(nextSource, currentSource)
            ? current
            : settingsChanged
                ? nextSource?.Conditioned(conditioning)
                : ReuseOrCondition(
                    nextSource,
                    previousCurrentSource,
                    previousCurrent,
                    previousNextSource,
                    previousNext,
                    conditioning);

        conditionedPaletteOwner = owner;
        conditionedPaletteRevision = owner.Revision;
        conditionedPaletteSettings = conditioning;
        conditionedCurrentSource = currentSource;
        conditionedNextSource = nextSource;
        conditionedCurrentPalette = current;
        conditionedNextPalette = next;
    }

    /// <summary>
    /// Exponentially eases a value toward a target at a frame-rate-independent rate.
    /// </summary>
    private static float SmoothToward(float current, float target, float rate, float deltaTime) =>
        (1f - Mathf.Exp(-rate * deltaTime)).Lerp(current, target);

    /// <summary>
    /// Composes independent Fill and Drop facts into one hue-compression tension, with an active Drop owning
    /// the blackout-to-release timeline exclusively.
    /// </summary>
    /// <param name="drop">The frame-coherent Drop facts used for both preparation and active release.</param>
    /// <returns>
    /// Progress from blackout to full reignition during an active Drop, or one outside a Drop so every
    /// orientation class remains lit.
    /// </returns>
    private float UpdateChoreography(DropValues drop)
    {
        if (!beatManager.IsSynced)
        {
            // Standalone owns a fixed no-music look, so no Synced tension carries across loss of the clock.
            hueCompression = 0f;
            return 1f;
        }

        if (drop.Active)
        {
            float release = drop.In.Build(SyncSettings.DropBeats).Remap(
                SyncSettings.BlackHold,
                1f,
                0f,
                1f,
                clamp: true);
            hueCompression = 1f - release;
            return release;
        }

        float target = Mathf.Max(
            beatManager.Fill.In.Build(),
            drop.Before.Build(SyncSettings.DropBeats));
        hueCompression = target >= hueCompression
            ? target
            : SmoothToward(
                hueCompression,
                target,
                SyncSettings.CompressionReleaseRate,
                effectDelta);
        return 1f;
    }

    /// <summary>
    /// Updates musical-energy smoothing and interpolates the active mode's directional-shading
    /// depth endpoints.
    /// </summary>
    private float UpdateShadeDepth()
    {
        float energyTarget = beatManager.Energy.Level switch
        {
            Energy.Low => 0f,
            Energy.Mid => 0.5f,
            Energy.High => 1f,
            _ => standaloneSettings.Energy,
        };
        float energySmoothing = beatManager.IsSynced
            ? SyncSettings.EnergySmoothing
            : standaloneSettings.EnergySmoothing;
        smoothedEnergy = SmoothToward(smoothedEnergy, energyTarget, energySmoothing, effectDelta);
        FloatRange shadeDepth = beatManager.IsSynced
            ? SyncSettings.ShadeDepth
            : standaloneSettings.ShadeDepth;
        return smoothedEnergy.Lerp(shadeDepth.Min, shadeDepth.Max);
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        huePhase = Mathf.Repeat(huePhase + (speed * effectDelta), 1f);

        PaletteConditioning paletteConditioning = beatManager.IsSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        RefreshConditionedPalettes(paletteConditioning);
        GPalette frameCurrentPalette = conditionedCurrentPalette;
        GPalette frameNextPalette = conditionedNextPalette;
        bool paletteIsTransitioning = APalette.IsTransitioning;
        float paletteTransitionProgress = APalette.TransitionProgress;

        // The Routine rotates the full angle-to-hue pattern without changing the tiles' relative hues.
        float rhythmHueOffset = EnableRoutineRhythmHueOffset
            ? routine.Lerp(
                SyncSettings.RhythmHueOffset.Min,
                beatManager.IsSynced
                    ? SyncSettings.RhythmHueOffset.Max
                    : standaloneSettings.RhythmHueOffset)
            : 0f;
        var drop = beatManager.Drop;
        bool inDrop = drop.Active;
        float dropRelease = UpdateChoreography(drop);
        float frameHueCompression = EnableFillAndPreDropHueCompression ? hueCompression : 0f;
        float shadeDepth = EnableEnergyDrivenDirectionalShading ? UpdateShadeDepth() : 0f;
        float spread = standaloneSettings.Spread;

        // Hoisted: the front's soft edge is uniform across the wall, so its rank scale is one
        // frame-wide value rather than 900 identical products.
        float frontSoftness = SyncSettings.FrontSoftness;
        float rankScale = 1f - frontSoftness;

        for (int i = 0; i < buffer.Length; i++)
        {
            float collapseStart = frontRank[i] * rankScale;
            float collapse = frameHueCompression.Remap(
                collapseStart,
                collapseStart + frontSoftness,
                0f,
                1f,
                clamp: true);
            float angle = (rawHue[i] * spread) + (hueDelta[i] * collapse) + huePhase;

            // Directional shading: same-facing tiles (0° ≡ 180°) shade identically, giving the angle
            // families brightness definition on top of hue.
            float align = 0.5f + (0.5f * Mathf.Cos((orient01[i] * Mathf.PI * 2f) - lightPhase));
            float shade = align.Lerp(1f - shadeDepth, 1f);

            // Drop reignition: outside a Drop every tile sits at its shaded brightness; during a Drop,
            // each orientation class snaps up as the shared release reaches its reveal point.
            float lit = EnableDropBlackoutAndReignition && inDrop
                ? dropRelease.Remap(
                    classReveal[i],
                    classReveal[i] + SyncSettings.ClassSnapWidth,
                    0f,
                    1f,
                    clamp: true)
                : 1f;
            float value = lit.Lerp(SyncSettings.DarkFloor, shade);

            // Sample Angles' current and next conditioned copies separately, mirroring AnimPalette's
            // three-second fade while cyclic sampling joins the last entry back to the first.
            float palettePosition = Mathf.Repeat(angle + rhythmHueOffset, 1f);
            Color paletteColor = frameCurrentPalette.ReadCyclic(
                palettePosition,
                doblend: true);
            if (paletteIsTransitioning)
            {
                Color nextPaletteColor = frameNextPalette.ReadCyclic(
                    palettePosition,
                    doblend: true);
                paletteColor = Color.Lerp(
                    paletteColor,
                    nextPaletteColor,
                    paletteTransitionProgress);
            }

            // Keep the existing dormant shading/Drop value as its separate post-palette stage; all four
            // temporary musical-layer flags remain disabled and unchanged in this pass.
            buffer[i] = new Color(
                paletteColor.r * value,
                paletteColor.g * value,
                paletteColor.b * value,
                paletteColor.a);
        }
    }
}

/// <summary>The resolved Standalone Settings that preserve Angles' authored no-music look.</summary>
[Serializable]
public sealed class AnglesStandaloneSettings
{
    /// <summary>
    /// Live angle-to-hue gain: the hue distance between adjacent orientation classes, and so the
    /// colour contrast between directions. The rail extends above one to widen that separation, not
    /// to add colours — the tiling's ten orientation clusters are the ceiling. Integer values alias
    /// (two and four show five colours, five shows two); prefer non-integer settings above one.
    /// </summary>
    [Range(0f, 4f)] public float Spread;

    /// <summary>
    /// Live effect-local palette conditioning. Its nonzero luminance threshold keeps black outside
    /// the authored Angles look while neighbour hue repair avoids replacing dark stops with grey.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Per-activation and per-Grid sweep-speed range.</summary>
    public FloatRange Speed;

    /// <summary>
    /// Low- and High-energy directional-shading depth endpoints used by the Standalone fallback
    /// interpolation, with editor rails spanning the full normalized depth.
    /// </summary>
    public FloatRange ShadeDepth;

    /// <summary>Normalized fallback energy used when no track Energy value exists.</summary>
    [Range(0f, 1f)] public float Energy;

    /// <summary>Per-second shading-depth smoothing rate outside live musical placement.</summary>
    [Min(0f)] public float EnergySmoothing;

    /// <summary>Fixed Routine hue offset returned without live musical placement.</summary>
    [Range(0f, 1f)] public float RhythmHueOffset;

    /// <summary>
    /// Copies every Angles Standalone Setting from another value, including live angle spread,
    /// effect-local palette conditioning, independent speed, and directional-shading depth endpoints
    /// and editor rails.
    /// </summary>
    /// <param name="source">The Standalone Settings whose values become this value.</param>
    public void CopyFrom(AnglesStandaloneSettings source)
    {
        Spread = source.Spread;
        PaletteConditioning = source.PaletteConditioning;
        Speed = new FloatRange(
            source.Speed.Min,
            source.Speed.Max,
            source.Speed.LowRail,
            source.Speed.HighRail);
        ShadeDepth = new FloatRange(
            source.ShadeDepth.Min,
            source.ShadeDepth.Max,
            source.ShadeDepth.LowRail,
            source.ShadeDepth.HighRail);
        Energy = source.Energy;
        EnergySmoothing = source.EnergySmoothing;
        RhythmHueOffset = source.RhythmHueOffset;
    }
}

/// <summary>The saved-or-default musical-response settings used by Angles in Synced Mode.</summary>
[Serializable]
public sealed class AnglesSyncSettings
{
    /// <summary>
    /// Live effect-local palette conditioning for Synced Mode, independently saved so tuning it
    /// cannot drift the Standalone look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Width of the Fill/Drop hue-compression wavefront's soft edge in normalized rank space.</summary>
    [Range(0.0001f, 1f)] public float FrontSoftness;

    /// <summary>Per-second smoothing rate used when an unpaired Fill's hue compression releases.</summary>
    [Min(0.0001f)] public float CompressionReleaseRate;

    /// <summary>Shared length of Drop preparation before landing and blackout release after landing.</summary>
    [Min(1)] public int DropBeats;

    /// <summary>Minimum brightness retained during the Drop blackout.</summary>
    [Range(0f, 1f)] public float DarkFloor;

    /// <summary>Fraction of the active Drop window held at the blackout floor and full hue compression.</summary>
    [Range(0f, 1f)] public float BlackHold;

    /// <summary>Soft-edge width used as each orientation class reignites.</summary>
    [Range(0.0001f, 1f)] public float ClassSnapWidth;

    /// <summary>First energy pool sampled for the four-bar Routine choreography.</summary>
    public Energy RoutineEnergyOne;

    /// <summary>Second energy pool sampled for the four-bar Routine choreography.</summary>
    public Energy RoutineEnergyTwo;

    /// <summary>Third energy pool sampled for the four-bar Routine choreography.</summary>
    public Energy RoutineEnergyThree;

    /// <summary>Fourth energy pool sampled for the four-bar Routine choreography.</summary>
    public Energy RoutineEnergyFour;

    /// <summary>
    /// Directional-shading depth endpoints at Low and High track Energy, with editor rails
    /// spanning the full normalized depth.
    /// </summary>
    public FloatRange ShadeDepth;

    /// <summary>Per-second smoothing rate between track Energy shading targets.</summary>
    [Min(0f)] public float EnergySmoothing;

    /// <summary>
    /// Lower and upper hue rotations applied at the bottom and top of the live Routine envelope,
    /// with editor rails spanning the full normalized hue cycle.
    /// </summary>
    public FloatRange RhythmHueOffset;

    /// <summary>
    /// Copies every Angles Sync Setting from another value, including independent palette
    /// conditioning, directional-shading depth, and Routine hue-offset endpoints and editor rails.
    /// </summary>
    /// <param name="source">The Sync Settings whose values become this value.</param>
    public void CopyFrom(AnglesSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        PaletteConditioning = source.PaletteConditioning;
        FrontSoftness = source.FrontSoftness;
        CompressionReleaseRate = source.CompressionReleaseRate;
        DropBeats = source.DropBeats;
        DarkFloor = source.DarkFloor;
        BlackHold = source.BlackHold;
        ClassSnapWidth = source.ClassSnapWidth;
        RoutineEnergyOne = source.RoutineEnergyOne;
        RoutineEnergyTwo = source.RoutineEnergyTwo;
        RoutineEnergyThree = source.RoutineEnergyThree;
        RoutineEnergyFour = source.RoutineEnergyFour;
        ShadeDepth = new FloatRange(
            source.ShadeDepth.Min,
            source.ShadeDepth.Max,
            source.ShadeDepth.LowRail,
            source.ShadeDepth.HighRail);
        EnergySmoothing = source.EnergySmoothing;
        RhythmHueOffset = new FloatRange(
            source.RhythmHueOffset.Min,
            source.RhythmHueOffset.Max,
            source.RhythmHueOffset.LowRail,
            source.RhythmHueOffset.HighRail);
    }
}
