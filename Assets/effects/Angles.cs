using Random = UnityEngine.Random;
using System;
using UnityEngine;

/// <summary>
/// Renders a palette hue sweep based on each tile's stored geometric angle.
/// </summary>
/// <remarks>
/// FILL: the Data Surface's own <see cref="InSpan.Build"/> advances an outer-to-inner mask through
/// one selected Shape List. Every member of a Star, Starball, or Lotusball shares one removal
/// threshold derived from that group's radius. Removed Tiles are written black; surviving Tiles
/// follow the ordinary Angles path exactly. The mask snaps clear when the Fill ends so no invented
/// recovery time crosses into a following Drop.
///
/// PRE-DROP / DROP (preserved behind temporary rebuild flags): one soft-edged wavefront, ordered by
/// each tile's hue distance from the wall's own mean hue (closest first), compresses the wall toward
/// that charged mean color. <see cref="DropValues.Before"/> advances the front so a Drop receives
/// preparation. An active Drop has exclusive priority. At its landing the whole wall holds near-black
/// while fully compressed, then one Drop release timeline both expands the hues and reignites a
/// staccato cascade through the tiling's ten orientation classes — the multiples-of-18° directional
/// families of the underlying pentagrid. Because orientation drives hue here, each class is also a
/// single hue, so the rainbow and its ten hidden families return together out of the darkness.
///
/// SHADING: a gentle directional brightness gradient keyed to each tile's orientation (as if the faceted
/// quasicrystal were lit from one direction) gives the ten families brightness definition, not just hue.
/// Standalone holds the authored baseline depth. In Synced Mode the existing smoothed
/// <see cref="BeatManager.Energy"/> ladder deepens that shading and selects one of three independently
/// authored hue-cycle-per-beat sweep rates, which the measured live beat interval converts to velocity.
/// A missing nullable Energy rests at Mid; the beat interval itself is always present while the wall reads
/// Synced, because the wire withholds it only when no live player can contribute one.
///
/// BEAT PHASE FRONT: while the authored Low reading clears its gate, each On Beat rising edge
/// latches one new hue-phase target. The step crosses the wall along an authored axis during the
/// wire's quarter-beat trigger window. Its soft edge interpolates which phase each tile samples; it
/// never changes brightness, saturation, palette conditioning, or the continuous hue sweep beneath it.
/// Below the Low gate the offset is cleared and Angles follows its existing rendering path exactly.
///
/// Standalone's sweep speed re-rolls on every new Grid, preserving the authored no-music motion.
/// Synced sweep velocity never reads that roll. The shading light direction is seeded once per
/// activation instead: re-rolling it at a Grid caused a visible flash.
/// </remarks>
[EffectSyncSettings(typeof(AnglesSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(AnglesStandaloneSettingsAsset))]
public class Angles : EffectBase
{
    // Temporary musical-layer rebuild flags

    /// <summary>Temporary rebuild scaffolding for pre-Drop hue compression; the Fill half has been replaced by the Shape List mask.</summary>
    private const bool EnablePreDropHueCompression = false;

    /// <summary>Temporary rebuild scaffolding for Drop blackout and orientation-class reignition; this layer returns reshaped or is deleted when its turn comes.</summary>
    private const bool EnableDropBlackoutAndReignition = false;

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

    /// <summary>
    /// Standing directional-shading depth: the dimmest orientation drops this far below full (so its
    /// floor is 1 - this). This is the depth the wall shows whenever Energy is not driving shading, and
    /// it doubles as the Low-energy endpoint, so the look tuned here is where the later musical
    /// response starts rather than something Energy overrides. Set on the wall.
    /// </summary>
    private const float StandaloneShadeDepthLow = 0.5f;

    /// <summary>Directional-shading depth at High energy: deeper contrast so intense sections read the ten families more strongly, without ever going as dark as the Drop. Set on the wall.</summary>
    private const float StandaloneShadeDepthHigh = 0.8f;

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

    /// <summary>Low-band strength that engages the beat phase front. The 0.35 default matches the established bass-drive threshold used by MazeFlyer; tune it until kicks engage without sustained low-frequency material holding the front on.</summary>
    private const float SyncBeatLowThreshold = 0.35f;

    /// <summary>Levels form the Low gate reads. Normalized is the default because the gate's job is to report what is happening on the beat: it tracks the hit instantly, where Smoothed averages across the beat boundary and blurs the moment, and Peak holds on after a kick fades. Flip it live on the wall.</summary>
    private const AnglesSyncSettings.BeatLevelReading SyncBeatLowLevelReading =
        AnglesSyncSettings.BeatLevelReading.Normalized;

    /// <summary>Hue phase added by each engaged beat. The 0.1 default advances one of the tiling's ten orientation classes, permuting their colour assignment without changing the wall's colour set.</summary>
    private const float SyncBeatPhaseStep = 0.1f;

    /// <summary>Direction the beat front travels in wall coordinates, in degrees: zero sweeps left-to-right and 90 sweeps bottom-to-top. The default is zero.</summary>
    private const float SyncBeatFrontAxisDegrees = 0f;

    /// <summary>Width of the beat phase front's soft edge in normalized wall-projection space. The 0.12 default follows the dormant pre-Drop/Drop front's authored softness; smaller is crisper and larger blends more of the wall between phases.</summary>
    private const float SyncBeatFrontSoftness = 0.12f;

    /// <summary>Shape List unit removed as the Fill drains inward. Lotusballs are the default because their 489 member Tiles are the only selectable motif set covering most of the current 900-Tile wall.</summary>
    private const AnglesSyncSettings.FillUnitKind SyncFillUnit =
        AnglesSyncSettings.FillUnitKind.Lotusballs;

    /// <summary>Width, in normalized rank space (0..1), of the pre-Drop hue-compression wavefront's soft edge. Smaller = a crisper traveling edge; larger = a blurrier gradient.</summary>
    private const float SyncFrontSoftness = 0.12f;

    /// <summary>Per-second smoothing rate used when pre-Drop hue compression loses its approaching Drop, so abandoned preparation relaxes instead of snapping. Tune on the TENSION readout.</summary>
    private const float SyncCompressionReleaseRate = 2f;

    /// <summary>Drop window in beats: preparation reaches full hue compression across this many beats before landing, then blackout release and orientation reignition share the same length after it. Kept short so the event reads within a 2-4 beat window.</summary>
    private const int SyncDropBeats = 3;

    /// <summary>Brightness the wall drops to during the blackout — a floor, not literal off, so it reads as intentional impact rather than a crash. Tune on the readout.</summary>
    private const float SyncDarkFloor = 0.04f;

    /// <summary>Fraction of the active Drop window held fully black and hue-compressed before release begins — the punctuation of the impact. Tune on the readout.</summary>
    private const float SyncBlackHold = 0.12f;

    /// <summary>Reignition snap width (in cascade-progress units) for each class: smaller = harder staccato pop as each orientation family lights; larger = a softer roll. Tune on the readout.</summary>
    private const float SyncClassSnapWidth = 0.08f;

    /// <summary>Energy ladder position assumed when <see cref="EnergyValues.Level"/> has no value: 0.5 = Mid, a steady moderate sweep rate and shading depth rather than either endpoint. Tune on the EN readout.</summary>
    private const float SyncEnergyRestingLevel = 0.5f;

    /// <summary>Low-Energy sweep rate in hue cycles per beat: one full hue cycle in about 16 beats. Tune on the SWEEP readout.</summary>
    private const float SyncSweepCyclesPerBeatLow = 0.06f;

    /// <summary>Mid-Energy sweep rate in hue cycles per beat: one full hue cycle in about 8 beats, authored independently so Mid can keep its own decent pace. Tune on the SWEEP readout.</summary>
    private const float SyncSweepCyclesPerBeatMid = 0.12f;

    /// <summary>High-Energy sweep rate in hue cycles per beat: one full hue cycle every 4 beats. Tune on the SWEEP readout.</summary>
    private const float SyncSweepCyclesPerBeatHigh = 0.25f;

    /// <summary>
    /// Standing directional-shading depth, mirroring Standalone so the two modes carry the same look
    /// until a musical reason parts them: the dimmest orientation drops this far below full, and the
    /// value doubles as the Low-energy endpoint the later Energy lerp starts from. Set on the wall.
    /// </summary>
    private const float SyncShadeDepthLow = 0.5f;

    /// <summary>Directional-shading depth at High energy: deeper contrast so intense sections read the ten families more strongly, without ever going as dark as the Drop. Set on the wall.</summary>
    private const float SyncShadeDepthHigh = 0.8f;

    /// <summary>Smoothing rate (per second) easing both sweep velocity and shading depth between Energy tiers, so a Low/Mid/High change ramps over ~0.5s instead of snapping. Tune on the EN and SWEEP readouts.</summary>
    private const float SyncEnergySmoothing = 2f;

    // Runtime mechanism constants

    /// <summary>Number of orientation (tileangle) classes the wall reignites through, one per 18° pentagrid direction. Verified against the 900-tile data: exactly 10 classes of 62-119 tiles each.</summary>
    private const int OrientationClasses = 10;

    /// <summary>The wire-authored On Beat gate occupies the first quarter of its beat interval; this contract duration places the spatial front without locally rebuilding musical timing.</summary>
    private const float BeatTriggerWindowFraction = 0.25f;

    /// <summary>Fill progress at which the outermost group can first disappear, leaving a short readable onset before subtraction begins.</summary>
    private const float FillFirstRemovalProgress = 0.05f;

    /// <summary>Fill progress by which the innermost group has disappeared, reserving the final tenth of the Fill as its fully drained peak before the end snap restores the wall.</summary>
    private const float FillFullRemovalProgress = 0.9f;

    /// <summary>
    /// Mid's position on the normalized Energy ladder, which runs Low 0, Mid 0.5, High 1. This is
    /// the ladder's own geometry, not a tuning value: the tunable resting position a nullable
    /// <see cref="EnergyValues.Level"/> falls back to is
    /// <see cref="AnglesSyncSettings.EnergyRestingLevel"/>.
    /// </summary>
    private const float EnergyLadderMid = 0.5f;

    /// <summary>
    /// Advertises that Angles handles Fill through its Shape List mask and suits all three Energy tiers
    /// now that they drive its motion and shading, while withholding Drop capability until the dormant
    /// pre-Drop/Drop layers are rebuilt.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill |
        Repertoire.EnergyLow |
        Repertoire.EnergyMid |
        Repertoire.EnergyHigh;

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
    };

    /// <summary>
    /// Resolves a fresh copy of Angles' file-local Sync Defaults, including independent palette
    /// conditioning, the Low-gated beat phase front, the Shape List Fill mask, three Energy-tier
    /// sweep rates, and directional-shading depth.
    /// </summary>
    public static AnglesSyncSettings SyncDefaults => new AnglesSyncSettings
    {
        PaletteConditioning = SyncPaletteConditioning,
        BeatLowThreshold = SyncBeatLowThreshold,
        BeatLowLevelReading = SyncBeatLowLevelReading,
        BeatPhaseStep = SyncBeatPhaseStep,
        BeatFrontAxisDegrees = SyncBeatFrontAxisDegrees,
        BeatFrontSoftness = SyncBeatFrontSoftness,
        FillUnit = SyncFillUnit,
        FrontSoftness = SyncFrontSoftness,
        CompressionReleaseRate = SyncCompressionReleaseRate,
        DropBeats = SyncDropBeats,
        DarkFloor = SyncDarkFloor,
        BlackHold = SyncBlackHold,
        ClassSnapWidth = SyncClassSnapWidth,
        EnergyRestingLevel = SyncEnergyRestingLevel,
        SweepCyclesPerBeatLow = SyncSweepCyclesPerBeatLow,
        SweepCyclesPerBeatMid = SyncSweepCyclesPerBeatMid,
        SweepCyclesPerBeatHigh = SyncSweepCyclesPerBeatHigh,
        ShadeDepth = new FloatRange(
            SyncShadeDepthLow,
            SyncShadeDepthHigh,
            0f,
            1f),
        EnergySmoothing = SyncEnergySmoothing,
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

    /// <summary>
    /// Standalone sweep speed rolled for this activation or Grid. Synced per-frame velocity never
    /// reads it; retaining the roll preserves Standalone motion and the shared Random draw order.
    /// </summary>
    private float speed;

    /// <summary>Bounded hue-wheel position integrated from the active mode's sweep rate, seeded from the activation's randomized <see cref="EffectBase.effectTime"/> phase so rate, tempo, Energy, and mode changes alter velocity without teleporting position.</summary>
    private float huePhase;

    /// <summary>Whether the selected On Beat lane was open on the previous frame, retained locally so its multi-frame window produces one phase latch.</summary>
    private bool previousBeatGateOpen;

    /// <summary>Whether an engaged beat's old-to-new phase boundary is currently crossing the wall.</summary>
    private bool beatFrontActive;

    /// <summary>Settled beat-driven hue offset from which the current front starts.</summary>
    private float beatPhaseFrom;

    /// <summary>Bounded beat-driven hue offset every tile holds after the current front passes.</summary>
    private float beatPhaseTo;

    /// <summary>The authored beat phase step captured when the current target latched, so live tuning cannot bend an in-flight front.</summary>
    private float latchedBeatPhaseStep;

    /// <summary>Current pre-Drop tension (0..1) expressed as progress of the hue-compression wavefront toward <see cref="meanHue"/>.</summary>
    private float hueCompression;

    /// <summary>Each tile's raw angle-hue (pre-Spread, pre-sweep, pre-beat), cached once since <see cref="Penrose.TileData.tileangle"/> never changes.</summary>
    private float[] rawHue;

    /// <summary>Each tile's immutable wall-centered position, cached once so the live beat-front axis can project it every frame without reading tile metadata or allocating.</summary>
    private Vector2[] tileCenters;

    /// <summary>Per Tile, its Lotusball group's outer-to-inner radius rank, or -1 when the Tile belongs to no Lotusball.</summary>
    private float[] lotusballFillRingRank;

    /// <summary>Per Tile, its Starball group's outer-to-inner radius rank, or -1 when the Tile belongs to no Starball.</summary>
    private float[] starballFillRingRank;

    /// <summary>Per Tile, its Star group's outer-to-inner radius rank, or -1 when the Tile belongs to no Star.</summary>
    private float[] starFillRingRank;

    /// <summary>Per tile, the shortest signed hue delta (in [-0.5, 0.5)) from <see cref="rawHue"/> toward <see cref="meanHue"/>, cached once for the dormant pre-Drop compression.</summary>
    private float[] hueDelta;

    /// <summary>Per tile, the cascade-progress point (0..~0.9) at which its orientation class reignites during a Drop — its class index / <see cref="OrientationClasses"/>. Cached once.</summary>
    private float[] classReveal;

    /// <summary>Per tile, its normalized rank (0..1) by ascending hue-distance from <see cref="meanHue"/>, cached once for the dormant pre-Drop compression. The tile with the closest hue ranks 0 and compresses first.</summary>
    /// <remarks>
    /// This holds the rank alone, not the wavefront envelope value at which the tile's collapse begins.
    /// <see cref="Draw"/> scales it by the live <see cref="AnglesSyncSettings.FrontSoftness"/> each frame to
    /// reach that start value, so a Play Mode edit of the soft-edge width moves the whole front coherently
    /// instead of only its trailing edge.
    /// </remarks>
    private float[] frontRank;

    /// <summary>Per tile, its folded orientation in [0,1) (tileangle mod 180° / 180°), cached once. Drives the directional shading; wraps smoothly so same-facing tiles (0° ≡ 180°) shade identically.</summary>
    private float[] orient01;

    /// <summary>Circular mean of every tile's raw angle-hue: the charged color the dormant pre-Drop/Drop choreography compresses toward.</summary>
    private float meanHue;

    /// <summary>Direction (radians) the shading gradient is "lit" from; seeded once per activation so a Grid boundary cannot flash the bright/shadowed sides of the orientation field.</summary>
    /// <remarks>
    /// Its <c>0..2π</c> roll is deliberately not captured as a Standalone randomization range. A full turn is the
    /// complete angular domain of a direction, not an authored span — narrowing it would stop the light reaching
    /// some orientations at all, which is a different effect rather than a tuning of this one.
    /// </remarks>
    private float lightPhase;

    /// <summary>
    /// Energy ladder position (Low 0, Mid 0.5, High 1) smoothed frame-to-frame in Synced Mode,
    /// driving sweep velocity and shading depth together. It starts at Mid so a nullable Energy read
    /// has a steady moderate resting value; Standalone rendering does not read it.
    /// </summary>
    private float smoothedEnergy;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        bool isSynced = beatManager.IsSynced;
        float cyclesPerBeat = isSynced ? ResolveSyncedSweepCyclesPerBeat() : 0f;
        float cyclesPerSecond = isSynced ? ResolveSyncedSweepCyclesPerSecond() : speed;
        float shadeDepth = isSynced
            ? smoothedEnergy.Lerp(SyncSettings.ShadeDepth.Min, SyncSettings.ShadeDepth.Max)
            : standaloneSettings.ShadeDepth.Min;
        string energyReadout = isSynced
            ? $"{beatManager.Energy.Level?.ToString() ?? "—"}  {smoothedEnergy:0.00}"
            : "Standalone";
        string sweepReadout = isSynced
            ? $"{cyclesPerBeat:0.000} cpb  {cyclesPerSecond:0.000} cps  {beatManager.Timing.BeatAverageMilliseconds?.ToString() ?? "—"} ms"
            : $"{cyclesPerSecond:0.000} cps";

        return "Angles" +
            $"\nEN {energyReadout}" +
            $"\nSWEEP {sweepReadout}" +
            $"\nSHADE {shadeDepth:0.00}" +
            (beatManager.Fill.Active
                ? $"\nFILL {beatManager.Fill.In.Build():0.00}  {SyncSettings.FillUnit}"
                : "") +
            (hueCompression > 0.01f ? $"\nTENSION {hueCompression:0.00}" : "") +
            (beatManager.Drop.Active
                ? $"\nDROP {beatManager.Drop.In.Build(SyncSettings.DropBeats):0.00}"
                : "");
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        PrecomputeTileFields();
    }

    /// <summary>
    /// Caches the static per-tile geometry used by the beat phase front, Fill, Drop, and directional shading.
    /// </summary>
    private void PrecomputeTileFields()
    {
        int total = tiles.Length;
        rawHue = new float[total];
        tileCenters = new Vector2[total];
        lotusballFillRingRank = new float[total];
        starballFillRingRank = new float[total];
        starFillRingRank = new float[total];
        hueDelta = new float[total];
        classReveal = new float[total];
        frontRank = new float[total];
        orient01 = new float[total];

        float sumX = 0f, sumY = 0f;
        for (int i = 0; i < total; i++)
        {
            rawHue[i] = tiles[i].tileangle / 180f;
            tileCenters[i] = tiles[i].center;
            float radians = rawHue[i] * Mathf.PI * 2f;
            sumX += Mathf.Cos(radians);
            sumY += Mathf.Sin(radians);
        }
        meanHue = Mathf.Repeat(Mathf.Atan2(sumY, sumX) / (Mathf.PI * 2f), 1f);

        PrecomputeFillMask(
            penrose.Layout.shapes.Lotusballs,
            lotusballFillRingRank);
        PrecomputeFillMask(
            penrose.Layout.shapes.Starballs,
            starballFillRingRank);
        PrecomputeFillMask(
            penrose.Layout.shapes.Stars,
            starFillRingRank);

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
    /// Caches one Shape List's group membership and outer-to-inner radius rank per Tile. Every Tile
    /// in a group receives the same rank so the Fill can only remove whole motif units.
    /// </summary>
    /// <param name="shapeList">The allocation-free Shape List reader whose motif groups become Fill units.</param>
    /// <param name="ringRank">Destination receiving each member Tile's normalized outer-to-inner group rank, or -1 for nonmembers.</param>
    private void PrecomputeFillMask(
        LayoutData.ShapeList.Reader shapeList,
        float[] ringRank)
    {
        for (int i = 0; i < ringRank.Length; i++)
        {
            ringRank[i] = -1f;
        }

        int groupCount = shapeList.GroupCount;
        var groupRadii = new float[groupCount];
        float minimumRadius = float.PositiveInfinity;
        float maximumRadius = float.NegativeInfinity;

        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            LayoutData.ShapeList.Group group = shapeList.GetGroup(groupIndex);
            Vector2 groupCenter = Vector2.zero;
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                groupCenter += tileCenters[group[tileIndex]];
            }

            groupCenter /= group.TileCount;
            float radius = groupCenter.magnitude;
            groupRadii[groupIndex] = radius;
            minimumRadius = Mathf.Min(minimumRadius, radius);
            maximumRadius = Mathf.Max(maximumRadius, radius);
        }

        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            float groupRingRank = Mathf.InverseLerp(
                maximumRadius,
                minimumRadius,
                groupRadii[groupIndex]);
            LayoutData.ShapeList.Group group = shapeList.GetGroup(groupIndex);
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                int tile = group[tileIndex];
                ringRank[tile] = groupRingRank;
            }
        }
    }

    /// <summary>
    /// Resolves Effect Settings and initializes per-activation random and beat-front state before this effect starts drawing.
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
        previousBeatGateOpen = false;
        beatFrontActive = false;
        beatPhaseFrom = 0f;
        beatPhaseTo = 0f;
        latchedBeatPhaseStep = 0f;
        hueCompression = 0f;
        // Seeded in both modes because BeatManager recomputes IsSynced from the wire every frame: the
        // wall can go Synced mid-activation, and the ladder must already sit at its resting position
        // when the first Synced frame reads it rather than ramping up from a stale or zero value.
        smoothedEnergy = SyncSettings.EnergyRestingLevel;
        controller.debugText.text = DebugText();
        buffer.Clear();
    }

    /// <summary>
    /// Re-rolls the held Standalone sweep speed so the no-music look takes a fresh character every
    /// 16 beats. Synced sweep velocity never reads the random speed. The shading light direction is
    /// intentionally seeded only in <see cref="OnStart"/> because changing it on a Grid caused a
    /// visible flash.
    /// </summary>
    private void Reroll()
    {
        speed = Random.Range(standaloneSettings.Speed.Min, standaloneSettings.Speed.Max);
    }

    /// <summary>
    /// On each new Grid the held Standalone sweep speed takes a fresh roll.
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
    /// Advances the dormant pre-Drop hue-compression tension, with an active Drop owning the
    /// blackout-to-release timeline exclusively. Fill is intentionally absent because its Shape List
    /// mask never modulates the colour of a surviving Tile.
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

        float target = drop.Before.Build(SyncSettings.DropBeats);
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
    /// Interpolates the three independently authored Energy-tier sweep rates through the smoothed
    /// ladder position. Mid is a real authored value, never an arithmetic midpoint imposed by Low
    /// and High.
    /// </summary>
    private float ResolveSyncedSweepCyclesPerBeat()
    {
        return smoothedEnergy <= EnergyLadderMid
            ? Mathf.Lerp(
                SyncSettings.SweepCyclesPerBeatLow,
                SyncSettings.SweepCyclesPerBeatMid,
                Mathf.InverseLerp(0f, EnergyLadderMid, smoothedEnergy))
            : Mathf.Lerp(
                SyncSettings.SweepCyclesPerBeatMid,
                SyncSettings.SweepCyclesPerBeatHigh,
                Mathf.InverseLerp(EnergyLadderMid, 1f, smoothedEnergy));
    }

    /// <summary>
    /// Converts the smoothed hue-cycles-per-beat response to cycles per second with the Data
    /// Surface's measured live beat interval, so a faster track sweeps faster at the same tier.
    /// </summary>
    /// <remarks>
    /// The interval is typed nullable, but it cannot be absent here. <c>IsSynced</c> is true only
    /// while the wire reports a real beat-in-bar, which means a live player holds a beat position,
    /// and the wire reports no beat average only when no live player can contribute one. The null
    /// arm therefore exists to unwrap the <see cref="int"/>? and never renders.
    /// </remarks>
    private float ResolveSyncedSweepCyclesPerSecond()
    {
        return beatManager.Timing.BeatAverageMilliseconds is { } beatAverageMilliseconds
            ? ResolveSyncedSweepCyclesPerBeat() * 1000f / beatAverageMilliseconds
            : 0f;
    }

    /// <summary>
    /// Updates the shared Synced Energy ladder position that eases both sweep velocity and
    /// directional-shading depth. A missing nullable Energy rests at Mid rather than snapping either
    /// response to an endpoint.
    /// </summary>
    private void UpdateSmoothedEnergy()
    {
        float energyTarget = beatManager.Energy.Level switch
        {
            Energy.Low => 0f,
            Energy.Mid => EnergyLadderMid,
            Energy.High => 1f,
            _ => SyncSettings.EnergyRestingLevel,
        };
        smoothedEnergy = SmoothToward(
            smoothedEnergy,
            energyTarget,
            SyncSettings.EnergySmoothing,
            effectDelta);
    }

    /// <summary>
    /// Reads the Low band through the authored Levels form, so the wall can pick whether the gate
    /// tracks the kick instantly, follows it smoothly, or holds on after it fades.
    /// </summary>
    private float ResolveBeatGateLow() => SyncSettings.BeatLowLevelReading switch
    {
        AnglesSyncSettings.BeatLevelReading.Smoothed => beatManager.Levels.Smoothed.Low,
        AnglesSyncSettings.BeatLevelReading.Peak => beatManager.Levels.Peak.Low,
        _ => beatManager.Levels.Normalized.Low,
    };

    /// <summary>
    /// Updates the consumer-local On Beat edge and latches the old and new beat-driven phase offsets
    /// while the authored Low reading engages the response.
    /// </summary>
    /// <param name="frontProgress">Normalized travel through the wire's quarter-beat trigger window, or one while no front is crossing.</param>
    /// <returns>True while the Low gate is engaged and the settled beat phase should contribute to rendering.</returns>
    /// <remarks>
    /// Called only in Synced Mode, where <see cref="TimingValues.BeatInBar"/> and
    /// <see cref="TimingValues.BeatProgress"/> are present. Reading their values directly preserves
    /// BeatManager as the sole musical source instead of rebuilding window timing inside Angles.
    /// </remarks>
    private bool UpdateBeatPhaseFront(out float frontProgress)
    {
        int beatInBar = beatManager.Timing.BeatInBar.Value;
        bool beatGateOpen = beatManager.Beats.OnBeat(beatInBar);
        bool engaged = ResolveBeatGateLow() > SyncSettings.BeatLowThreshold;

        if (!engaged)
        {
            beatFrontActive = false;
            beatPhaseFrom = 0f;
            beatPhaseTo = 0f;
            latchedBeatPhaseStep = 0f;
            previousBeatGateOpen = beatGateOpen;
            frontProgress = 1f;
            return false;
        }

        if (beatGateOpen && !previousBeatGateOpen)
        {
            beatPhaseFrom = beatPhaseTo;
            latchedBeatPhaseStep = SyncSettings.BeatPhaseStep;
            beatPhaseTo = Mathf.Repeat(beatPhaseFrom + latchedBeatPhaseStep, 1f);
            beatFrontActive = true;
        }
        else if (!beatGateOpen)
        {
            beatFrontActive = false;
        }

        previousBeatGateOpen = beatGateOpen;
        frontProgress = beatFrontActive
            ? Mathf.Clamp01(beatManager.Timing.BeatProgress.Value / BeatTriggerWindowFraction)
            : 1f;
        return true;
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        bool isSynced = beatManager.IsSynced;
        float beatFrontProgress = 1f;
        bool beatMovementEngaged = false;
        if (isSynced)
        {
            UpdateSmoothedEnergy();
            beatMovementEngaged = UpdateBeatPhaseFront(out beatFrontProgress);
        }
        else
        {
            previousBeatGateOpen = false;
            beatFrontActive = false;
            beatPhaseFrom = 0f;
            beatPhaseTo = 0f;
            latchedBeatPhaseStep = 0f;
        }
        float shadeDepth = isSynced
            ? smoothedEnergy.Lerp(SyncSettings.ShadeDepth.Min, SyncSettings.ShadeDepth.Max)
            : standaloneSettings.ShadeDepth.Min;
        float sweepCyclesPerSecond = isSynced
            ? ResolveSyncedSweepCyclesPerSecond()
            : speed;
        huePhase = Mathf.Repeat(huePhase + (sweepCyclesPerSecond * effectDelta), 1f);

        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        RefreshConditionedPalettes(paletteConditioning);
        GPalette frameCurrentPalette = conditionedCurrentPalette;
        GPalette frameNextPalette = conditionedNextPalette;
        bool paletteIsTransitioning = APalette.IsTransitioning;
        float paletteTransitionProgress = APalette.TransitionProgress;

        float fillProgress = beatManager.Fill.In.Build();
        float[] frameFillRingRank = null;
        if (fillProgress > 0f)
        {
            switch (SyncSettings.FillUnit)
            {
                case AnglesSyncSettings.FillUnitKind.Stars:
                    frameFillRingRank = starFillRingRank;
                    break;
                case AnglesSyncSettings.FillUnitKind.Starballs:
                    frameFillRingRank = starballFillRingRank;
                    break;
                default:
                    frameFillRingRank = lotusballFillRingRank;
                    break;
            }
        }
        var drop = beatManager.Drop;
        bool inDrop = drop.Active;
        float dropRelease = UpdateChoreography(drop);
        float frameHueCompression = EnablePreDropHueCompression ? hueCompression : 0f;
        // Directional shading is a standing part of both looks. Standalone holds its authored
        // ShadeDepth.Min exactly; Synced Energy deepens from its independently authored Min baseline
        // toward Max, so the approved static look remains the musical response's starting point.
        float spread = standaloneSettings.Spread;

        bool beatFrontSweeping = beatMovementEngaged && beatFrontActive;
        Vector2 beatFrontAxis = default;
        float beatFrontMinimum = 0f;
        float beatFrontMaximum = 0f;
        float beatFrontPosition = 0f;
        float beatFrontSoftness = SyncSettings.BeatFrontSoftness;
        if (beatFrontSweeping)
        {
            float axisRadians = SyncSettings.BeatFrontAxisDegrees * Mathf.Deg2Rad;
            beatFrontAxis = new Vector2(Mathf.Cos(axisRadians), Mathf.Sin(axisRadians));
            beatFrontMinimum = float.PositiveInfinity;
            beatFrontMaximum = float.NegativeInfinity;
            for (int i = 0; i < tileCenters.Length; i++)
            {
                float projection = Vector2.Dot(tileCenters[i], beatFrontAxis);
                beatFrontMinimum = Mathf.Min(beatFrontMinimum, projection);
                beatFrontMaximum = Mathf.Max(beatFrontMaximum, projection);
            }

            // Begin one soft-edge width before the first tile and finish on the last tile so the
            // whole wall reaches the new phase exactly as the trigger window closes.
            beatFrontPosition = Mathf.Lerp(-beatFrontSoftness, 1f, beatFrontProgress);
        }

        // Hoisted: the front's soft edge is uniform across the wall, so its rank scale is one
        // frame-wide value rather than 900 identical products.
        float frontSoftness = SyncSettings.FrontSoftness;
        float rankScale = 1f - frontSoftness;

        for (int i = 0; i < buffer.Length; i++)
        {
            if (frameFillRingRank != null && frameFillRingRank[i] >= 0f)
            {
                float removalThreshold = Mathf.Lerp(
                    FillFirstRemovalProgress,
                    FillFullRemovalProgress,
                    frameFillRingRank[i]);
                if (fillProgress >= removalThreshold)
                {
                    // Fill is a mask: black is literal off, while every survivor continues through
                    // the unchanged Angles render path below with no tint, dim, or hue modulation.
                    buffer[i] = Color.black;
                    continue;
                }
            }

            float collapseStart = frontRank[i] * rankScale;
            float collapse = frameHueCompression.Remap(
                collapseStart,
                collapseStart + frontSoftness,
                0f,
                1f,
                clamp: true);
            float angle = (rawHue[i] * spread) + (hueDelta[i] * collapse) + huePhase;
            if (beatMovementEngaged)
            {
                float beatPhase = beatPhaseTo;
                if (beatFrontSweeping)
                {
                    float projection = Vector2.Dot(tileCenters[i], beatFrontAxis);
                    float projection01 = Mathf.InverseLerp(
                        beatFrontMinimum,
                        beatFrontMaximum,
                        projection);
                    float phaseMix = beatFrontPosition.Remap(
                        projection01 - beatFrontSoftness,
                        projection01,
                        0f,
                        1f,
                        clamp: true);
                    beatPhase = beatPhaseFrom + (latchedBeatPhaseStep * phaseMix);
                }

                // The soft edge interpolates phase before the one normal palette sample below. It
                // therefore selects only real Angles colours rather than blending, tinting, or
                // dimming pixels after palette lookup.
                angle += beatPhase;
            }

            // Directional shading: same-facing tiles (0° ≡ 180°) shade identically, giving the angle
            // families brightness definition on top of hue. Alignment reads the same orientation the
            // hue does, so brightness and colour reinforce each other rather than cutting across.
            // lightPhase is seeded once per activation and then holds, so the lit direction stays put
            // while huePhase sweeps colour through it — a fixed light is what lets the rhombs read as
            // lit solids; a turning one would just add motion competing with the hue drift.
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
            float palettePosition = Mathf.Repeat(angle, 1f);
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

            // Keep shading and the dormant Drop value as their separate post-palette stage; the
            // temporary pre-Drop and Drop gates remain isolated from the Fill mask.
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
    /// Directional-shading depth authored for Standalone. Min is the standing depth the renderer
    /// reads; Max remains paired with it so the two Effect Settings surfaces retain the same tuned
    /// depth range and rails.
    /// </summary>
    public FloatRange ShadeDepth;

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

    /// <summary>Selects which allocation-free Shape List supplies the Fill's whole-motif removal units.</summary>
    public enum FillUnitKind
    {
        /// <summary>Lotusball units; the authored default and the only selectable list whose 489 member Tiles cover most of the current wall.</summary>
        Lotusballs,

        /// <summary>Starball units; 32 ten-Tile compound motifs produce a lighter Fill mask.</summary>
        Starballs,

        /// <summary>Star units; 45 five-Tile closed fat-rhomb cycles produce the lightest Fill mask.</summary>
        Stars,
    }

    /// <summary>
    /// Which form of the Levels reading the beat phase front's Low gate consults; renders as an
    /// Inspector dropdown so the reading can be flipped live on the wall.
    /// </summary>
    public enum BeatLevelReading
    {
        /// <summary>The instantaneous wire value — high only while the kick is actually sounding.</summary>
        Normalized,

        /// <summary>The attack/release follower — steadier, but lags the kick.</summary>
        Smoothed,

        /// <summary>Instant rise with a tempo-based linear fall — holds on after the kick fades.</summary>
        Peak,
    }

    /// <summary>Low-band strength that must be exceeded before beat-driven phase movement runs.</summary>
    [Range(0f, 1f)] public float BeatLowThreshold;

    /// <summary>Levels reading the beat phase front's Low gate reads its value from.</summary>
    public BeatLevelReading BeatLowLevelReading;

    /// <summary>Forward hue-phase step latched on each engaged beat; 0.1 advances one orientation class.</summary>
    [Range(0f, 1f)] public float BeatPhaseStep;

    /// <summary>Beat-front travel direction in wall-space degrees: zero is left-to-right and 90 is bottom-to-top.</summary>
    [Range(0f, 360f)] public float BeatFrontAxisDegrees;

    /// <summary>Width of the beat phase front's soft edge in normalized wall-projection space.</summary>
    [Range(0.0001f, 1f)] public float BeatFrontSoftness;

    /// <summary>Shape List whose groups are removed as whole units during a Fill.</summary>
    public FillUnitKind FillUnit;

    /// <summary>Width of the dormant pre-Drop hue-compression wavefront's soft edge in normalized rank space.</summary>
    [Range(0.0001f, 1f)] public float FrontSoftness;

    /// <summary>Per-second smoothing rate used when abandoned pre-Drop hue compression releases.</summary>
    [Min(0.0001f)] public float CompressionReleaseRate;

    /// <summary>Shared length of Drop preparation before landing and blackout release after landing.</summary>
    [Min(1)] public int DropBeats;

    /// <summary>Minimum brightness retained during the Drop blackout.</summary>
    [Range(0f, 1f)] public float DarkFloor;

    /// <summary>Fraction of the active Drop window held at the blackout floor and full hue compression.</summary>
    [Range(0f, 1f)] public float BlackHold;

    /// <summary>Soft-edge width used as each orientation class reignites.</summary>
    [Range(0.0001f, 1f)] public float ClassSnapWidth;

    /// <summary>Live Energy ladder position held while the track reports no Energy level, so a nullable read rests at a tunable moderate sweep rate and shading depth.</summary>
    [Range(0f, 1f)] public float EnergyRestingLevel;

    /// <summary>Low-Energy hue sweep rate in cycles per beat, authored independently of Mid and High.</summary>
    [Min(0f)] public float SweepCyclesPerBeatLow;

    /// <summary>Mid-Energy hue sweep rate in cycles per beat, authored independently so it can keep its own decent pace.</summary>
    [Min(0f)] public float SweepCyclesPerBeatMid;

    /// <summary>High-Energy hue sweep rate in cycles per beat, authored independently of Low and Mid.</summary>
    [Min(0f)] public float SweepCyclesPerBeatHigh;

    /// <summary>
    /// Directional-shading depth endpoints at Low and High track Energy, with editor rails
    /// spanning the full normalized depth.
    /// </summary>
    public FloatRange ShadeDepth;

    /// <summary>Per-second smoothing rate between track Energy sweep and shading targets.</summary>
    [Min(0f)] public float EnergySmoothing;

    /// <summary>
    /// Copies every Angles Sync Setting from another value, including independent palette
    /// conditioning, the Low-gated beat phase front, Shape List Fill mask, three Energy-tier sweep
    /// rates, and directional-shading depth.
    /// </summary>
    /// <param name="source">The Sync Settings whose values become this value.</param>
    public void CopyFrom(AnglesSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        PaletteConditioning = source.PaletteConditioning;
        BeatLowThreshold = source.BeatLowThreshold;
        BeatLowLevelReading = source.BeatLowLevelReading;
        BeatPhaseStep = source.BeatPhaseStep;
        BeatFrontAxisDegrees = source.BeatFrontAxisDegrees;
        BeatFrontSoftness = source.BeatFrontSoftness;
        FillUnit = source.FillUnit;
        FrontSoftness = source.FrontSoftness;
        CompressionReleaseRate = source.CompressionReleaseRate;
        DropBeats = source.DropBeats;
        DarkFloor = source.DarkFloor;
        BlackHold = source.BlackHold;
        ClassSnapWidth = source.ClassSnapWidth;
        EnergyRestingLevel = source.EnergyRestingLevel;
        SweepCyclesPerBeatLow = source.SweepCyclesPerBeatLow;
        SweepCyclesPerBeatMid = source.SweepCyclesPerBeatMid;
        SweepCyclesPerBeatHigh = source.SweepCyclesPerBeatHigh;
        ShadeDepth = new FloatRange(
            source.ShadeDepth.Min,
            source.ShadeDepth.Max,
            source.ShadeDepth.LowRail,
            source.ShadeDepth.HighRail);
        EnergySmoothing = source.EnergySmoothing;
    }
}
