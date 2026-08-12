using Random = UnityEngine.Random;
using System;
using UnityEngine;

/// <summary>
/// Renders a palette hue sweep based on each tile's stored geometric angle.
/// </summary>
/// <remarks>
/// FILL: the Data Surface's own <see cref="InSpan.Build"/> advances outer-to-inner through one
/// selected Shape List. Every motif receives one continuous envelope from its group's existing radius
/// rank, easing the whole unit in and out instead of switching it on one frame. Stars are one solid
/// part. Starballs split into a five-fat-Tile Star core and five-thin-Tile surrounding ball. Lotusballs
/// split into their unique degree-four fat center and the connected region around it. Every Tile in a
/// part converges to one shared moving palette coordinate, while the live part separation keeps a
/// compound motif's structure visible. The coordinate is mixed continuously from each Tile's ordinary
/// angle along the shortest hue-wheel path. The Tiles bordering a motif take the same mix half a
/// palette cycle away, so every shape carries a contour drawn in colour rather than in darkness — the
/// background occupies the whole hue wheel, so only a local boundary can define a shape against it.
/// No Tile is removed or written black, and the Fill rests at
/// zero when inactive so no invented recovery time crosses into a following Drop or Standalone Mode.
///
/// DROP: the Data Surface's <see cref="InSpan.Decay(int)"/> opens all four distinct Line Ribbon
/// families on the landing beat, then drops the three ragged families one at a time until only
/// <c>lines0</c> remains. A member Tile's ordered position along its ribbon temporarily replaces its
/// geometric angle as the palette coordinate's source. One shared envelope also slows the current
/// running along those paths and mixes every remaining ribbon Tile continuously back to its own angle.
/// The window is authored independently of the wire's Drop length; there is no Before response.
/// Palette conditioning, saturation, directional shading, and brightness stay on the ordinary Angles
/// path throughout, so the impact reads as flowing colour rather than a blackout or a crash.
///
/// SHADING: a gentle directional brightness gradient keyed to each tile's orientation (as if the faceted
/// quasicrystal were lit from one direction) gives the ten families brightness definition, not just hue.
/// Standalone holds the authored baseline depth. In Synced Mode the smoothed
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
/// Standalone's sweep speed takes a new random value on every Grid, preserving the authored
/// no-music motion.
/// Synced sweep velocity never reads that roll. The shading light direction is seeded once per
/// activation instead: re-rolling it at a Grid caused a visible flash.
/// </remarks>
[EffectSyncSettings(typeof(AnglesSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(AnglesStandaloneSettingsAsset))]
public class Angles : EffectBase
{
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
    private static PaletteConditioning StandalonePaletteConditioning => new()
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

    /// <summary>Minimum sweep speed randomized on activation and each new Grid.</summary>
    private const float StandaloneSpeedMin = 0.15f;

    /// <summary>Maximum sweep speed randomized on activation and each new Grid.</summary>
    private const float StandaloneSpeedMax = 0.4f;

    /// <summary>
    /// Standing directional-shading depth: the dimmest orientation drops this far below full (so its
    /// floor is 1 - this). This is the depth the wall shows whenever Energy is not driving shading, and
    /// it doubles as the Low-energy endpoint, so the look tuned here is where the Synced musical
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
    private static PaletteConditioning SyncPaletteConditioning => new()
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

    /// <summary>
    /// Shape List unit transformed as the Fill drains inward. Starballs are the default because
    /// contours changed what makes a good unit: a shape reads by its edge, and an edge needs
    /// untouched background to sit against. Starballs' 32 ten-Tile motifs claim 320 Tiles and border
    /// 304 more, and no two motifs ever contend for the same bordering Tile, so every shape gets a
    /// private uncontested ring with 276 Tiles left ordinary. Lotusballs covers more wall — 489
    /// member Tiles — but leaves only 160 ordinary and has 46% of its border contested, so its
    /// contour stops reading as an outline and becomes a second colour field.
    /// </summary>
    private const AnglesSyncSettings.FillUnitKind SyncFillUnit =
        AnglesSyncSettings.FillUnitKind.Starballs;

    /// <summary>
    /// Additional palette-cycle rotation shared by every part of every lit Fill motif, in cycles per
    /// beat. One advances the complete conditioned palette once per beat on top of Angles' ordinary
    /// sweep. Tune live on the FILL readout; negative values reverse direction.
    /// </summary>
    private const float SyncFillRotationCyclesPerBeat = 1f;

    /// <summary>
    /// Hue-wheel distance between the center/core and surrounding part of a compound Fill motif.
    /// Stars have one part and therefore ignore this value. Tune live on the FILL readout.
    /// </summary>
    /// <remarks>
    /// A quarter cycle, set on the wall, because a compound motif shows four regions rather than two:
    /// its two parts, and each part's contour sitting <see cref="FillContourHueOffset"/> away. A
    /// quarter spaces all four evenly around the wheel — core 0, ball 0.25, core's contour 0.5,
    /// ball's contour 0.75. One half looks like maximum separation and is not: it collides the ball
    /// with the core's contour and the ball's contour with the core, leaving two colours where the
    /// motif has four regions.
    /// </remarks>
    private const float SyncFillPartHueSeparation = 0.25f;

    /// <summary>
    /// Width of one motif's smooth rise-and-fall envelope in outer-to-inner unit-rank space. One half
    /// makes each shape readable for one third of the Fill while stretching the travel so the outer
    /// unit starts at zero and the inner unit returns to zero exactly at the Fill's end.
    /// </summary>
    private const float SyncFillUnitEnvelopeWidth = 0.5f;

    /// <summary>
    /// How completely the Tiles bordering a lit motif are recruited as its contour. Authored at full
    /// because a contour is the only edge the shapes have: the background occupies the whole hue wheel,
    /// so no motif colour contrasts with it globally, and only a local boundary defines the shape. Dial
    /// down at the wall when a Shape List leaves too little untouched background — Lotusballs borders
    /// 251 of the 900 Tiles, against 429 Tiles left ordinary under Stars. Zero disables contours.
    /// </summary>
    private const float SyncFillContourStrength = 1f;

    /// <summary>
    /// Authored Drop response window in beats. Sixteen beats gives the landing one complete nominal
    /// Grid in which to resolve, regardless of how long the wire's Drop Phrase continues. Tune on the
    /// DROP readout; this is the existing DropBeats slot with its old preparation/blackout meaning cut.
    /// </summary>
    private const int SyncDropBeats = 16;

    /// <summary>
    /// Line Ribbon current at the landing, in palette cycles per beat. One cycle sends the complete
    /// conditioned palette down every stored ribbon path during the impact beat; the shared Drop
    /// envelope slows this continuously to rest. Tune on the DROP readout.
    /// </summary>
    private const float SyncDropFlowCyclesPerBeatAtImpact = 1f;

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
    /// value doubles as the Low-energy endpoint the Energy lerp starts from. Set on the wall.
    /// </summary>
    private const float SyncShadeDepthLow = 0.5f;

    /// <summary>Directional-shading depth at High energy: deeper contrast so intense sections read the ten families more strongly, without ever going as dark as the Drop. Set on the wall.</summary>
    private const float SyncShadeDepthHigh = 0.8f;

    /// <summary>Smoothing rate (per second) easing both sweep velocity and shading depth between Energy tiers, so a Low/Mid/High change ramps over ~0.5s instead of snapping. Tune on the EN and SWEEP readouts.</summary>
    private const float SyncEnergySmoothing = 2f;

    // Runtime mechanism constants

    /// <summary>
    /// Four distinct Line Ribbon directions exist in the layout: lines0, lines4, lines2, and lines1.
    /// Lines3 is byte-for-byte identical to lines2 and deliberately contributes no fifth family.
    /// </summary>
    private const int RibbonFamilyCount = 4;

    /// <summary>The wire-authored On Beat gate occupies the first quarter of its beat interval; this contract duration places the spatial front without locally rebuilding musical timing.</summary>
    private const float BeatTriggerWindowFraction = 0.25f;

    /// <summary>
    /// Number of leading packed positions that form the Star inside every ten-Tile Starball. The
    /// current layout stores all 32 Starballs as five fat Star Tiles followed by five thin border
    /// Tiles, and each leading subset exactly matches one Stars group and walks its closed Neighbor cycle.
    /// </summary>
    private const int StarballStarTileCount = 5;

    /// <summary>
    /// Hue-wheel distance a contour Tile sits from the motif part it borders. Half a cycle is the most
    /// distant entry in the conditioned palette's cyclic order, so this is the largest step the palette
    /// can make and needs no tuning — it is a property of the wheel rather than a matter of taste.
    /// </summary>
    private const float FillContourHueOffset = 0.5f;

    /// <summary>
    /// Mid's position on the normalized Energy ladder, which runs Low 0, Mid 0.5, High 1. This is
    /// the ladder's own geometry, not a tuning value: the tunable resting position a nullable
    /// <see cref="EnergyValues.Level"/> falls back to is
    /// <see cref="AnglesSyncSettings.EnergyRestingLevel"/>.
    /// </summary>
    private const float EnergyLadderMid = 0.5f;

    /// <summary>
    /// Advertises that Angles handles Fill through additive rotating Shape List motifs, handles Drop
    /// through its four-family Line Ribbon flow, and suits all three Energy tiers now that they drive
    /// its motion and shading.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill |
        Repertoire.HandlesDrop |
        Repertoire.EnergyLow |
        Repertoire.EnergyMid |
        Repertoire.EnergyHigh;

    /// <summary>
    /// Resolves a fresh immutable-by-convention copy of Angles' Standalone Defaults, including
    /// live angle spread, effect-local palette conditioning, and independent speed and directional-
    /// shading depth ranges.
    /// </summary>
    public static AnglesStandaloneSettings StandaloneDefaults => new()
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
    /// conditioning, the Low-gated beat phase front, Shape List Fill behavior, part separation,
    /// envelope width, and shared rotation rate, the authored Drop window and Line Ribbon impact
    /// speed, three Energy-tier sweep rates, and directional-shading depth.
    /// </summary>
    public static AnglesSyncSettings SyncDefaults => new()
    {
        PaletteConditioning = SyncPaletteConditioning,
        BeatLowThreshold = SyncBeatLowThreshold,
        BeatLowLevelReading = SyncBeatLowLevelReading,
        BeatPhaseStep = SyncBeatPhaseStep,
        BeatFrontAxisDegrees = SyncBeatFrontAxisDegrees,
        BeatFrontSoftness = SyncBeatFrontSoftness,
        FillUnit = SyncFillUnit,
        FillRotationCyclesPerBeat = SyncFillRotationCyclesPerBeat,
        FillPartHueSeparation = SyncFillPartHueSeparation,
        FillUnitEnvelopeWidth = SyncFillUnitEnvelopeWidth,
        FillContourStrength = SyncFillContourStrength,
        DropBeats = SyncDropBeats,
        DropFlowCyclesPerBeatAtImpact = SyncDropFlowCyclesPerBeatAtImpact,
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

    /// <summary>
    /// Angles' Effect-local conditioned endpoint cache. It follows shared palette revisions and live
    /// conditioning controls while preserving the animated cross-fade without steady-frame allocation.
    /// </summary>
    private readonly ConditionedPaletteCache conditionedPalette = new();

    /// <summary>
    /// Static per-Tile Fill geometry: the unit's existing outer-to-inner rank and this Tile's explicit
    /// motif part. A negative unit rank marks a Tile outside the selected motif set.
    /// </summary>
    private readonly struct FillTileFields
    {
        /// <summary>Creates one cached Fill membership entry from invariant wall geometry.</summary>
        /// <param name="unitRank">The motif's normalized outer-to-inner rank, or -1 for no membership.</param>
        /// <param name="partIndex">The zero-based motif part shared by Tiles that converge to one hue coordinate.</param>
        /// <param name="contourRank">The rank of the motif this Tile outlines, or -1 when it outlines none.</param>
        /// <param name="contourPartIndex">The outer part index of the motif this Tile outlines, or -1 when it outlines none.</param>
        public FillTileFields(
            float unitRank,
            int partIndex,
            float contourRank,
            int contourPartIndex)
        {
            UnitRank = unitRank;
            PartIndex = partIndex;
            ContourRank = contourRank;
            ContourPartIndex = contourPartIndex;
        }

        /// <summary>The motif's normalized outer-to-inner rank, shared by every participating Tile in it.</summary>
        public float UnitRank { get; }

        /// <summary>The zero-based motif part whose Tiles share one Fill hue coordinate.</summary>
        public int PartIndex { get; }

        /// <summary>
        /// The rank of the motif this non-member Tile borders, so its contour rides the same envelope
        /// as the shape it outlines. -1 when the Tile borders no motif. Membership always wins, so two
        /// touching motifs merge rather than drawing a seam between them.
        /// </summary>
        public float ContourRank { get; }

        /// <summary>The outer part index of the bordered motif, whose hue coordinate the contour opposes.</summary>
        public int ContourPartIndex { get; }
    }

    /// <summary>
    /// Standalone hue-sweep rate in cycles per second, randomized for this activation or Grid.
    /// Synced per-frame velocity never reads it; retaining the value preserves Standalone motion
    /// and the shared Random draw order.
    /// </summary>
    private float standaloneSweepCyclesPerSecond;

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

    /// <summary>Each tile's raw angle-hue (pre-Spread, pre-sweep, pre-beat), cached once since <see cref="Penrose.TileData.tileangle"/> never changes.</summary>
    private float[] rawHue;

    /// <summary>Each tile's immutable wall-centered position, cached once so a changed live beat-front axis can refresh its normalized ranks without reading tile metadata or allocating.</summary>
    private Vector2[] tileCenters;

    /// <summary>Each Tile's normalized projection along the live beat-front axis, refreshed only when <see cref="AnglesSyncSettings.BeatFrontAxisDegrees"/> changes.</summary>
    private float[] beatFrontRankByTile;

    /// <summary>Whether <see cref="beatFrontRankByTile"/> represents an axis yet.</summary>
    private bool beatFrontRanksInitialized;

    /// <summary>The live beat-front axis represented by <see cref="beatFrontRankByTile"/>.</summary>
    private float beatFrontRankAxisDegrees;

    /// <summary>Per Tile, its Lotusball Fill membership, existing unit rank, and center-or-surround part.</summary>
    private FillTileFields[] lotusballFillFields;

    /// <summary>Per Tile, its Starball Fill membership, existing full-group rank, and fat-core-or-thin-surround part.</summary>
    private FillTileFields[] starballFillFields;

    /// <summary>Per Tile, its Star Fill membership and existing unit rank; all five Tiles share one part.</summary>
    private FillTileFields[] starFillFields;

    /// <summary>
    /// Per Line Ribbon family and Tile, the Tile's normalized stored position along its group, or -1
    /// when it belongs to no ribbon in that family. Families are ordered cleanest-first as lines0,
    /// lines4, lines2, lines1 so overlap resolution and density decay share one stable priority.
    /// </summary>
    /// <remarks>
    /// This is initialization-only geometry staging. Its arrays become the resolved active-family
    /// cache below, then this outer reference is cleared. The Drop envelope, active-family count,
    /// flow phase, and mix remain live per-frame values, so no Sync Setting is baked into either cache.
    /// </remarks>
    private float[][] ribbonPositionByFamily;

    /// <summary>
    /// Per active-family count and Tile, the normalized stored position from the cleanest active
    /// Line Ribbon family that contains it, or -1 when no active family contains it. Multiply-covered
    /// Tiles keep one stable source until their current family drops out, while every family still
    /// contributes where cleaner families do not.
    /// </summary>
    private float[][] ribbonPositionByActiveFamilyCount;

    /// <summary>
    /// Per Tile, its folded orientation in [0,1) (tileangle mod 180° / 180°), cached once. It drives
    /// directional shading and wraps smoothly so same-facing Tiles (0° ≡ 180°) shade identically.
    /// </summary>
    private float[] normalizedOrientationByTile;

    /// <summary>
    /// Per-Tile alignment with the activation's fixed light direction. The random light direction
    /// changes only in <see cref="OnStart"/>, so its cosine is cached there while live shading depth
    /// remains a per-frame value.
    /// </summary>
    private float[] lightAlignmentByTile;

    /// <summary>
    /// Bounded palette-cycle phase integrated only while the active Drop response is visible. The
    /// phase holds silently after the authored window and resets at the next activation.
    /// </summary>
    private float ribbonFlowPhase;

    /// <summary>
    /// Bounded additional palette-cycle phase integrated only while a Fill is active and shared by
    /// every motif part. The ordinary hue sweep remains part of the target coordinate, so a saved asset
    /// that has not yet serialized the new rate still produces motion; live Sync tuning adds or
    /// reverses whole-part rotation without a cache.
    /// </summary>
    private float fillRotationPhase;

    /// <summary>
    /// The frame's single <see cref="InSpan.Decay(int)"/> read, retained for the debug display after
    /// it has driven density, flow speed, and angle-to-ribbon mix in <see cref="Draw"/>.
    /// </summary>
    private float dropResponseEnvelope;

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
    /// <returns>The current Energy, sweep, shading, Fill, and Drop response readout.</returns>
    public override string DebugText()
    {
        bool isSynced = beatManager.IsSynced;
        float cyclesPerBeat = isSynced ? ResolveSyncedSweepCyclesPerBeat() : 0f;
        float cyclesPerSecond = isSynced
            ? ResolveSyncedSweepCyclesPerSecond()
            : standaloneSweepCyclesPerSecond;
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
                ? $"\nFILL {beatManager.Fill.In.Build():0.00}  {SyncSettings.FillUnit}  SEP {SyncSettings.FillPartHueSeparation:0.00}  WIDTH {SyncSettings.FillUnitEnvelopeWidth:0.00}  ROT {SyncSettings.FillRotationCyclesPerBeat:0.00} cpb  EDGE {SyncSettings.FillContourStrength:0.00}"
                : "") +
            (dropResponseEnvelope > 0f
                ? $"\nDROP {dropResponseEnvelope:0.00}  {ResolveActiveRibbonFamilyCount(dropResponseEnvelope)}/{RibbonFamilyCount}  {SyncSettings.DropFlowCyclesPerBeatAtImpact:0.00} cpb"
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
    /// Caches the static per-Tile geometry used by the beat phase front, additive Fill parts, Drop
    /// Line Ribbons, and directional shading. Fill caches contain geometry only, so their live unit
    /// selection, part separation, envelope width, and rotation rate stay editable throughout Play Mode.
    /// </summary>
    private void PrecomputeTileFields()
    {
        int total = tiles.Length;
        rawHue = new float[total];
        tileCenters = new Vector2[total];
        beatFrontRankByTile = new float[total];
        ribbonPositionByFamily = new float[RibbonFamilyCount][];
        normalizedOrientationByTile = new float[total];
        lightAlignmentByTile = new float[total];

        for (int i = 0; i < total; i++)
        {
            float tileAngle = tiles[i].tileangle;
            rawHue[i] = tileAngle / 180f;
            tileCenters[i] = tiles[i].center;

            // Folded orientation in [0,1): tileangle mod 180° normalized. Directional shading reads
            // it continuously so same-facing Tiles remain one brightness family.
            normalizedOrientationByTile[i] = Mathf.Repeat(tileAngle, 180f) / 180f;
        }

        lotusballFillFields = PrecomputeFillFields(
            penrose.Layout.shapes.Lotusballs,
            AnglesSyncSettings.FillUnitKind.Lotusballs);
        starballFillFields = PrecomputeFillFields(
            penrose.Layout.shapes.Starballs,
            AnglesSyncSettings.FillUnitKind.Starballs);
        starFillFields = PrecomputeFillFields(
            penrose.Layout.shapes.Stars,
            AnglesSyncSettings.FillUnitKind.Stars);

        PrecomputeRibbonFamily(0, penrose.Layout.shapes.Lines0, total);
        PrecomputeRibbonFamily(1, penrose.Layout.shapes.Lines4, total);
        PrecomputeRibbonFamily(2, penrose.Layout.shapes.Lines2, total);
        PrecomputeRibbonFamily(3, penrose.Layout.shapes.Lines1, total);
        ribbonPositionByActiveFamilyCount = PrecomputeRibbonPositionsByActiveFamilyCount(
            ribbonPositionByFamily,
            total);
        ribbonPositionByFamily = null;
    }

    /// <summary>
    /// Caches one Line Ribbon family's membership and normalized ordered position without retaining
    /// its packed Reader. Consecutive duplicate Tile positions count once, so lines2 group 10's
    /// repeated Tile 466 neither divides by a false path length nor shifts the visible current.
    /// </summary>
    /// <param name="familyIndex">Cleanest-first destination family index.</param>
    /// <param name="shapeList">The allocation-free Line Ribbon Shape List reader.</param>
    /// <param name="total">Number of Tiles whose membership array is allocated once.</param>
    private void PrecomputeRibbonFamily(
        int familyIndex,
        LayoutData.ShapeList.Reader shapeList,
        int total)
    {
        var positions = new float[total];
        for (int tileIndex = 0; tileIndex < positions.Length; tileIndex++)
        {
            positions[tileIndex] = -1f;
        }

        for (int groupIndex = 0; groupIndex < shapeList.GroupCount; groupIndex++)
        {
            LayoutData.ShapeList.Group group = shapeList.GetGroup(groupIndex);
            int uniqueTileCount = group.TileCount;
            for (int pathIndex = 1; pathIndex < group.TileCount; pathIndex++)
            {
                if (group[pathIndex] == group[pathIndex - 1])
                {
                    uniqueTileCount--;
                }
            }

            int uniquePathIndex = 0;
            for (int pathIndex = 0; pathIndex < group.TileCount; pathIndex++)
            {
                int tile = group[pathIndex];
                if (pathIndex > 0 && tile == group[pathIndex - 1])
                {
                    continue;
                }

                positions[tile] = uniqueTileCount > 1
                    ? uniquePathIndex / (float)(uniqueTileCount - 1)
                    : 0f;
                uniquePathIndex++;
            }
        }

        ribbonPositionByFamily[familyIndex] = positions;
    }

    /// <summary>
    /// Resolves every possible Drop density to the cleanest active family containing each Tile.
    /// Settling the stable priority during <see cref="Init"/> removes the per-Tile family scan from
    /// <see cref="Draw"/> while still allowing the live Drop envelope to select a density each frame.
    /// </summary>
    /// <param name="positionsByFamily">Cleanest-first per-family Line Ribbon positions.</param>
    /// <param name="total">Number of Tiles represented by every resolved density.</param>
    /// <returns>Per active-family count and Tile, the selected ribbon position or -1.</returns>
    private static float[][] PrecomputeRibbonPositionsByActiveFamilyCount(
        float[][] positionsByFamily,
        int total)
    {
        var positionsByActiveFamilyCount = new float[RibbonFamilyCount + 1][];
        positionsByActiveFamilyCount[1] = positionsByFamily[0];
        for (int activeFamilyCount = 2;
            activeFamilyCount <= RibbonFamilyCount;
            activeFamilyCount++)
        {
            float[] positions = positionsByFamily[activeFamilyCount - 1];
            float[] cleanerPositions = positionsByActiveFamilyCount[activeFamilyCount - 1];
            for (int tileIndex = 0; tileIndex < total; tileIndex++)
            {
                if (cleanerPositions[tileIndex] >= 0f)
                {
                    positions[tileIndex] = cleanerPositions[tileIndex];
                }
            }

            positionsByActiveFamilyCount[activeFamilyCount] = positions;
        }

        return positionsByActiveFamilyCount;
    }

    /// <summary>
    /// Caches one Shape List's participating Tiles, preserving its existing full-group radius rank
    /// while assigning every Tile to the explicit parts defined by its motif kind.
    /// </summary>
    /// <param name="shapeList">The allocation-free Shape List reader whose motif groups become Fill units.</param>
    /// <param name="fillUnit">The motif kind whose measured part decomposition is applied.</param>
    /// <returns>One immutable geometry entry per wall Tile, with negative values for nonmembers.</returns>
    /// <remarks>
    /// Stars are one part. Starballs use their known five-fat-Tile prefix as the Star core and their
    /// five-thin-Tile suffix as the surrounding ball. Lotusballs use the unique fat Tile with four
    /// in-group Neighbors as the center and every other Tile as the connected surround, including the
    /// one clipped nine-Tile group. Packed traversal order and clockwise traversal orientation deliberately
    /// do not enter the cache: those made hue vary per Tile, while Fill motion belongs to whole parts.
    /// A second pass then claims each motif's bordering Tiles as its contour, which is why membership
    /// has to be complete first: a Tile bordering one motif is very often a member of the next.
    /// Every array allocation and both adjacency scans happen here during <see cref="Init"/> so
    /// <see cref="Draw"/> reads only cached scalars.
    /// </remarks>
    private FillTileFields[] PrecomputeFillFields(
        LayoutData.ShapeList.Reader shapeList,
        AnglesSyncSettings.FillUnitKind fillUnit)
    {
        var fields = new FillTileFields[tiles.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            fields[i] = new FillTileFields(-1f, partIndex: -1, -1f, contourPartIndex: -1);
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

        var groupRanks = new float[groupCount];
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            float unitRank = Mathf.InverseLerp(
                maximumRadius,
                minimumRadius,
                groupRadii[groupIndex]);
            groupRanks[groupIndex] = unitRank;
            LayoutData.ShapeList.Group group = shapeList.GetGroup(groupIndex);
            int lotusballCenterTile = fillUnit == AnglesSyncSettings.FillUnitKind.Lotusballs
                ? FindLotusballCenter(group, groupIndex)
                : -1;

            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                int tile = group[tileIndex];
                int partIndex = fillUnit switch
                {
                    AnglesSyncSettings.FillUnitKind.Stars => 0,
                    AnglesSyncSettings.FillUnitKind.Starballs =>
                        tileIndex < StarballStarTileCount ? 0 : 1,
                    _ => tile == lotusballCenterTile ? 0 : 1,
                };
                fields[tile] = new FillTileFields(
                    unitRank,
                    partIndex,
                    -1f,
                    contourPartIndex: -1);
            }
        }

        // Contours run only after every motif has claimed its Tiles, because a Tile that borders one
        // motif is frequently a member of the next one and membership always wins. A Tile bordering two
        // motifs keeps the outermost, so a shared contour lights with the leading edge of the wave —
        // settling it here costs one cached value instead of a per-frame comparison in Draw.
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            LayoutData.ShapeList.Group group = shapeList.GetGroup(groupIndex);
            float unitRank = groupRanks[groupIndex];

            int outerPartIndex = 0;
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                outerPartIndex = Mathf.Max(outerPartIndex, fields[group[tileIndex]].PartIndex);
            }

            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                foreach (var neighbor in tiles[group[tileIndex]].neighbors)
                {
                    int candidate = neighbor.tileIdx;
                    FillTileFields existing = fields[candidate];
                    if (existing.UnitRank >= 0f)
                    {
                        continue;
                    }

                    if (existing.ContourRank >= 0f && existing.ContourRank <= unitRank)
                    {
                        continue;
                    }

                    fields[candidate] = new FillTileFields(
                        -1f,
                        partIndex: -1,
                        unitRank,
                        outerPartIndex);
                }
            }
        }

        return fields;
    }

    /// <summary>
    /// Finds the center part of one Lotusball from its measured Rhomb Type and Neighbor topology.
    /// </summary>
    /// <param name="group">The packed Lotusball group whose unique degree-four fat Tile is requested.</param>
    /// <param name="groupIndex">Group index reported if the measured center invariant is broken.</param>
    /// <returns>The direct Tile index of the Lotusball's center.</returns>
    /// <remarks>
    /// All 49 groups have exactly one fat Tile touching four other group Tiles—two fat and two thin.
    /// Packed position does not express that role, so adjacency and Rhomb Type are the source.
    /// </remarks>
    private int FindLotusballCenter(
        LayoutData.ShapeList.Group group,
        int groupIndex)
    {
        for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
        {
            int tile = group[tileIndex];
            if (tiles[tile].type != 0)
            {
                continue;
            }

            int groupNeighborCount = 0;
            foreach (var neighbor in tiles[tile].neighbors)
            {
                for (int candidateIndex = 0; candidateIndex < group.TileCount; candidateIndex++)
                {
                    if (neighbor.tileIdx != group[candidateIndex])
                    {
                        continue;
                    }

                    groupNeighborCount++;
                    break;
                }
            }

            if (groupNeighborCount == 4)
            {
                return tile;
            }
        }

        throw new InvalidOperationException(
            $"Lotusball group {groupIndex} has no fat Tile with four in-group Neighbors.");
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
        conditionedPalette.Refresh(APalette, beatManager.IsSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning);
        RandomizeStandaloneSweepRate();
        lightPhase = Random.Range(0f, Mathf.PI * 2f);
        RefreshLightAlignmentByTile();
        huePhase = Mathf.Repeat(effectTime * standaloneSweepCyclesPerSecond, 1f);
        previousBeatGateOpen = false;
        beatFrontActive = false;
        beatPhaseFrom = 0f;
        beatPhaseTo = 0f;
        latchedBeatPhaseStep = 0f;
        fillRotationPhase = 0f;
        ribbonFlowPhase = 0f;
        dropResponseEnvelope = 0f;
        // Seeded in both modes because BeatManager recomputes IsSynced from the wire every frame: the
        // wall can go Synced mid-activation, and the ladder must already sit at its resting position
        // when the first Synced frame reads it rather than ramping up from a stale or zero value.
        smoothedEnergy = SyncSettings.EnergyRestingLevel;
        controller.debugText.text = DebugText();
        buffer.Clear();
    }

    /// <summary>
    /// Refreshes the fixed directional-light alignment after the activation rolls
    /// <see cref="lightPhase"/>, keeping the per-frame shading path to one cached read per Tile.
    /// </summary>
    private void RefreshLightAlignmentByTile()
    {
        for (int i = 0; i < lightAlignmentByTile.Length; i++)
        {
            lightAlignmentByTile[i] = 0.5f +
                (0.5f * Mathf.Cos(
                    (normalizedOrientationByTile[i] * Mathf.PI * 2f) - lightPhase));
        }
    }

    /// <summary>
    /// Randomizes the held Standalone sweep speed so the no-music look takes a fresh character every
    /// Grid. Synced sweep velocity never reads the random speed. The shading light direction is
    /// intentionally seeded only in <see cref="OnStart"/> because changing it on a Grid caused a
    /// visible flash.
    /// </summary>
    private void RandomizeStandaloneSweepRate()
    {
        standaloneSweepCyclesPerSecond = Random.Range(
            standaloneSettings.Speed.Min,
            standaloneSettings.Speed.Max);
    }

    /// <summary>
    /// On each new Grid the held Standalone sweep speed takes a fresh random value.
    /// </summary>
    protected override void OnNewGrid()
    {
        RandomizeStandaloneSweepRate();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Exponentially eases a value toward a target at a frame-rate-independent rate.
    /// </summary>
    /// <param name="current">The value at the start of the frame.</param>
    /// <param name="target">The value being approached.</param>
    /// <param name="rate">The exponential response rate per second.</param>
    /// <param name="deltaTime">The current frame duration in seconds.</param>
    /// <returns>The eased value for the current frame.</returns>
    private static float SmoothToward(float current, float target, float rate, float deltaTime) =>
        (1f - Mathf.Exp(-rate * deltaTime)).Lerp(current, target);

    /// <summary>
    /// Shapes one motif's continuous rise and fall as the Fill travels through its retained
    /// outer-to-inner unit rank. Stretching progress by the live envelope width makes the outer
    /// motif start at zero and the inner motif return to zero at the Fill's exact endpoint.
    /// </summary>
    /// <param name="fillProgress">The Data Surface's active Fill build in the range zero to one.</param>
    /// <param name="unitRank">The motif's retained normalized outer-to-inner radius rank.</param>
    /// <param name="envelopeWidth">The live positive width of one motif's rise and fall in unit-rank space.</param>
    /// <returns>A smooth zero-to-one-to-zero envelope for the complete motif.</returns>
    private static float ResolveFillUnitEnvelope(
        float fillProgress,
        float unitRank,
        float envelopeWidth)
    {
        float localProgress = Mathf.Clamp01(
            ((fillProgress * (1f + envelopeWidth)) - unitRank) /
            envelopeWidth);
        float distanceFromPeak = Mathf.Abs((localProgress * 2f) - 1f);
        return 1f - Mathf.SmoothStep(0f, 1f, distanceFromPeak);
    }

    /// <summary>
    /// Integrates the live whole-part Fill rotation rate while the Data Surface's Fill envelope is
    /// visible. The measured beat interval comes from the Data Surface, so cycles-per-beat tuning
    /// follows the live track without reconstructing musical time or affecting Standalone Mode.
    /// </summary>
    /// <param name="fillProgress">The active Fill's zero-to-one build value.</param>
    private void UpdateFillRotationPhase(float fillProgress)
    {
        if (fillProgress <= 0f)
        {
            return;
        }

        float cyclesPerSecond =
            SyncSettings.FillRotationCyclesPerBeat *
            1000f /
            beatManager.Timing.BeatAverageMilliseconds.Value;
        fillRotationPhase = Mathf.Repeat(
            fillRotationPhase + (cyclesPerSecond * effectDelta),
            1f);
    }

    /// <summary>
    /// Maps the shared Drop envelope to density: all four families at impact, then one fewer at each
    /// quarter of the authored window, with lines0 remaining until the envelope reaches zero.
    /// </summary>
    /// <param name="envelope">The active Drop's zero-to-one decay value.</param>
    /// <returns>The number of cleanest-first Line Ribbon families still flowing.</returns>
    private static int ResolveActiveRibbonFamilyCount(float envelope) =>
        Mathf.CeilToInt(envelope * RibbonFamilyCount);

    /// <summary>
    /// Integrates the Line Ribbon current at the impact speed scaled by the same envelope that drives
    /// density and mix. The measured beat interval comes from the Data Surface, so cycles-per-beat
    /// tuning stays locked to the live track without reconstructing musical time locally.
    /// </summary>
    /// <param name="envelope">The active Drop's zero-to-one decay value.</param>
    private void UpdateRibbonFlowPhase(float envelope)
    {
        if (envelope <= 0f)
        {
            return;
        }

        float cyclesPerSecond =
            SyncSettings.DropFlowCyclesPerBeatAtImpact *
            1000f /
            beatManager.Timing.BeatAverageMilliseconds.Value;
        ribbonFlowPhase = Mathf.Repeat(
            ribbonFlowPhase + (cyclesPerSecond * envelope * effectDelta),
            1f);
    }

    /// <summary>
    /// Interpolates the three independently authored Energy-tier sweep rates through the smoothed
    /// ladder position. Mid is a real authored value, never an arithmetic midpoint imposed by Low
    /// and High.
    /// </summary>
    /// <returns>The current Synced hue-sweep rate in palette cycles per beat.</returns>
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
    /// <returns>The current Synced hue-sweep rate in palette cycles per second.</returns>
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
    /// <returns>The current Low-band strength from the selected Levels form.</returns>
    private float ReadBeatGateLowLevel() => SyncSettings.BeatLowLevelReading switch
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
        bool engaged = ReadBeatGateLowLevel() > SyncSettings.BeatLowThreshold;

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
    /// Refreshes normalized Tile ranks only when the live beat-front axis changes. The setting is
    /// compared on every sweeping frame, so a Play Mode edit reaches the next frame whose rendering
    /// depends on it instead of becoming an <see cref="Init"/>-baked half-live value.
    /// </summary>
    private void RefreshBeatFrontRanks()
    {
        float axisDegrees = SyncSettings.BeatFrontAxisDegrees;
        if (beatFrontRanksInitialized && beatFrontRankAxisDegrees == axisDegrees)
        {
            return;
        }

        float axisRadians = axisDegrees * Mathf.Deg2Rad;
        var axis = new Vector2(Mathf.Cos(axisRadians), Mathf.Sin(axisRadians));
        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;
        for (int i = 0; i < tileCenters.Length; i++)
        {
            float projection = Vector2.Dot(tileCenters[i], axis);
            beatFrontRankByTile[i] = projection;
            minimum = Mathf.Min(minimum, projection);
            maximum = Mathf.Max(maximum, projection);
        }

        for (int i = 0; i < beatFrontRankByTile.Length; i++)
        {
            beatFrontRankByTile[i] = Mathf.InverseLerp(
                minimum,
                maximum,
                beatFrontRankByTile[i]);
        }

        beatFrontRankAxisDegrees = axisDegrees;
        beatFrontRanksInitialized = true;
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

        // Directional shading is a standing part of both looks. Standalone holds its authored
        // ShadeDepth.Min exactly; Synced Energy deepens from its independently authored Min baseline
        // toward Max, so the approved static look remains the musical response's starting point.
        float shadeDepth = isSynced
            ? smoothedEnergy.Lerp(SyncSettings.ShadeDepth.Min, SyncSettings.ShadeDepth.Max)
            : standaloneSettings.ShadeDepth.Min;
        float sweepCyclesPerSecond = isSynced
            ? ResolveSyncedSweepCyclesPerSecond()
            : standaloneSweepCyclesPerSecond;
        huePhase = Mathf.Repeat(huePhase + (sweepCyclesPerSecond * effectDelta), 1f);

        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        conditionedPalette.Refresh(APalette, paletteConditioning);

        float fillProgress = isSynced ? beatManager.Fill.In.Build() : 0f;
        FillTileFields[] activeFillFields = null;
        float fillUnitEnvelopeWidth = 0f;
        float fillContourStrength = 0f;
        float fillPartHueSeparation = 0f;
        if (fillProgress > 0f)
        {
            activeFillFields = SyncSettings.FillUnit switch
            {
                AnglesSyncSettings.FillUnitKind.Stars => starFillFields,
                AnglesSyncSettings.FillUnitKind.Starballs => starballFillFields,
                _ => lotusballFillFields,
            };
            fillUnitEnvelopeWidth = SyncSettings.FillUnitEnvelopeWidth;
            fillContourStrength = SyncSettings.FillContourStrength;
            fillPartHueSeparation = SyncSettings.FillPartHueSeparation;
        }
        UpdateFillRotationPhase(fillProgress);
        float dropEnvelope = isSynced
            ? beatManager.Drop.In.Decay(SyncSettings.DropBeats)
            : 0f;
        dropResponseEnvelope = dropEnvelope;
        UpdateRibbonFlowPhase(dropEnvelope);
        int activeRibbonFamilyCount = ResolveActiveRibbonFamilyCount(dropEnvelope);
        float[] activeRibbonPositions = activeRibbonFamilyCount > 0
            ? ribbonPositionByActiveFamilyCount[activeRibbonFamilyCount]
            : null;
        // One Spread serves both modes, so the angular structure keeps its density whether or not a
        // track is playing.
        float spread = standaloneSettings.Spread;

        bool beatFrontSweeping = beatMovementEngaged && beatFrontActive;
        float beatFrontPosition = 0f;
        float beatFrontSoftness = 0f;
        if (beatFrontSweeping)
        {
            beatFrontSoftness = SyncSettings.BeatFrontSoftness;
            RefreshBeatFrontRanks();

            // Begin one soft-edge width before the first tile and finish on the last tile so the
            // whole wall reaches the new phase exactly as the trigger window closes.
            beatFrontPosition = Mathf.Lerp(-beatFrontSoftness, 1f, beatFrontProgress);
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float hueCoordinate = (rawHue[i] * spread) + huePhase;
            float appliedBeatPhase = 0f;
            if (beatMovementEngaged)
            {
                float tileBeatPhase = beatPhaseTo;
                if (beatFrontSweeping)
                {
                    float projection01 = beatFrontRankByTile[i];
                    float phaseMix = beatFrontPosition.Remap(
                        projection01 - beatFrontSoftness,
                        projection01,
                        0f,
                        1f,
                        clamp: true);
                    tileBeatPhase = beatPhaseFrom + (latchedBeatPhaseStep * phaseMix);
                }

                // The soft edge interpolates phase before the one normal palette sample below. It
                // therefore selects only real Angles colours rather than blending, tinting, or
                // dimming pixels after palette lookup.
                hueCoordinate += tileBeatPhase;
                appliedBeatPhase = tileBeatPhase;
            }

            // Directional shading: same-facing tiles (0° ≡ 180°) shade identically, giving the angle
            // families brightness definition on top of hue. Alignment reads the same orientation the
            // hue does, so brightness and colour reinforce each other rather than cutting across.
            // lightPhase is seeded once per activation and then holds, so the lit direction stays put
            // while huePhase sweeps colour through it — a fixed light is what lets the rhombs read as
            // lit solids; a turning one would just add motion competing with the hue drift.
            float lightAlignment = lightAlignmentByTile[i];
            float directionalShade = lightAlignment.Lerp(1f - shadeDepth, 1f);

            // Sample Angles' current and next conditioned copies separately, mirroring AnimPalette's
            // three-second fade while cyclic sampling joins the last entry back to the first.
            float palettePosition = Mathf.Repeat(hueCoordinate, 1f);

            // How completely this Tile belongs to a lit motif, and therefore how far its value is
            // carried to full below. A motif reads as one shape only when it shares every channel
            // that varies across the background: lightAlignment is per-Tile orientation and the Tiles of one
            // motif sit in several different orientation classes, so a hue-uniform part still renders
            // at several brightnesses and dissolves into the wall. Sharing value makes each Starball
            // part read as a solid shape.
            float fillSolidity = 0f;
            if (activeFillFields != null)
            {
                FillTileFields fillTile = activeFillFields[i];
                bool isMotifMember = fillTile.UnitRank >= 0f;
                float fillRank = isMotifMember ? fillTile.UnitRank : fillTile.ContourRank;
                if (fillRank >= 0f && (isMotifMember || fillContourStrength > 0f))
                {
                    float fillUnitEnvelope = ResolveFillUnitEnvelope(
                        fillProgress,
                        fillRank,
                        fillUnitEnvelopeWidth);
                    int fillPartIndex = fillTile.PartIndex;
                    float contourOffset = 0f;
                    if (!isMotifMember)
                    {
                        // A contour Tile rides its motif's own envelope, so the outline appears and
                        // fades with the shape rather than on a clock of its own. Strength scales hue
                        // travel and the value lift together, so one live knob dials the whole contour
                        // in and zero is an honest off.
                        fillUnitEnvelope *= fillContourStrength;
                        fillPartIndex = fillTile.ContourPartIndex;
                        contourOffset = FillContourHueOffset;
                    }

                    float fillPalettePosition = Mathf.Repeat(
                        (fillPartIndex * fillPartHueSeparation) +
                        huePhase +
                        fillRotationPhase +
                        contourOffset,
                        1f);
                    float shortestHueDelta = Mathf.Repeat(
                        fillPalettePosition - palettePosition + 0.5f,
                        1f) - 0.5f;

                    // The complete motif shares one continuous envelope, every Tile in one part aims
                    // at the same hue coordinate, and only the live part separation distinguishes its
                    // internal regions. Mixing before lookup adds the solid shape without switching
                    // Tiles off or manufacturing RGB colours. A contour Tile takes the same mix half a
                    // palette cycle away from the part it borders, so the boundary is the sharpest hue
                    // step the conditioned palette can make — an edge drawn in colour, taking no light
                    // off the wall the way a dark outline would.
                    palettePosition = Mathf.Repeat(
                        palettePosition + (shortestHueDelta * fillUnitEnvelope),
                        1f);
                    fillSolidity = fillUnitEnvelope;
                }
            }

            float ribbonPosition = activeRibbonPositions != null
                ? activeRibbonPositions[i]
                : -1f;
            if (ribbonPosition >= 0f)
            {
                float ribbonPalettePosition = Mathf.Repeat(
                    ribbonPosition + huePhase + appliedBeatPhase + ribbonFlowPhase,
                    1f);
                float shortestHueDelta = Mathf.Repeat(
                    ribbonPalettePosition - palettePosition + 0.5f,
                    1f) - 0.5f;

                // Mix the hue coordinate before the one conditioned-palette lookup. Colour.Lerp
                // would manufacture intermediate RGB values outside the palette; this keeps every
                // Drop frame a real Angles colour while the Tile returns continuously to its angle.
                palettePosition = Mathf.Repeat(
                    palettePosition + (shortestHueDelta * dropEnvelope),
                    1f);
            }
            Color paletteColor = conditionedPalette.ReadCyclic(
                palettePosition,
                doblend: true);

            // Directional shading stays in its ordinary post-palette stage. The Drop changes only the
            // palette coordinate's geometric source and never value, so ribbons stay lit solids. The
            // Fill also carries value to full across a lit motif, on the same envelope that mixes its
            // hue, so the shape rises out of the shading rather than popping past it.
            float tileBrightness = fillSolidity.Lerp(directionalShade, 1f);
            buffer[i] = new Color(
                paletteColor.r * tileBrightness,
                paletteColor.g * tileBrightness,
                paletteColor.b * tileBrightness,
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
    /// <remarks>
    /// Deliberately single-homed: <c>Draw</c> reads this one value in both modes, so the angular
    /// structure carries the same density whether or not a track is playing. Nothing has asked the
    /// two modes to differ here. Give it a Sync home the day one of them should.
    /// </remarks>
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

    /// <summary>Selects which allocation-free Shape List supplies the Fill's outer-to-inner motif events.</summary>
    public enum FillUnitKind
    {
        /// <summary>Lotusball units; one degree-four fat center part and one connected surrounding part across 489 member Tiles.</summary>
        Lotusballs,

        /// <summary>Starball units; one five-fat-Tile Star core part and one five-thin-Tile surrounding-ball part.</summary>
        Starballs,

        /// <summary>Star units; every closed five-fat-Tile cycle is one solid part.</summary>
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

    /// <summary>Shape List whose groups transform as outer-to-inner units during a Fill.</summary>
    public FillUnitKind FillUnit;

    /// <summary>
    /// Additional conditioned-palette cycles per beat shared by every part of every lit Fill motif.
    /// The ordinary Angles sweep remains underneath, and negative values reverse colour travel.
    /// </summary>
    [Range(-4f, 4f)] public float FillRotationCyclesPerBeat;

    /// <summary>
    /// Hue-wheel distance between a compound motif's center/core and surrounding part. This is what
    /// keeps a Starball or Lotusball reading as separate regions rather than one undifferentiated
    /// blob; a Star has a single part and ignores it. Each part's contour sits half a cycle from the
    /// part it borders, so this value spaces four regions on a compound motif, not two.
    /// </summary>
    [Range(0f, 0.5f)] public float FillPartHueSeparation;

    /// <summary>
    /// Width of each motif's continuous rise-and-fall envelope in normalized outer-to-inner unit-rank
    /// space. Existing saved settings that have not serialized this nonzero field read the authored
    /// Sync Default until restored.
    /// </summary>
    [Range(0.05f, 1f)] public float FillUnitEnvelopeWidth;

    /// <summary>
    /// How completely the Tiles bordering a lit motif are recruited as its contour, scaling both the
    /// hue travel and the value lift so zero leaves the surrounding wall untouched. The contour draws
    /// the motif's edge in colour rather than in darkness: it sits half a palette cycle from the part
    /// it borders and rises to full value alongside it, so nothing on the wall is dimmed.
    /// </summary>
    [Range(0f, 1f)] public float FillContourStrength;

    /// <summary>
    /// Authored active Drop response window in beats. It is independent of the wire's Drop length,
    /// so a long Drop Phrase still receives one finite ribbon-flow response from its landing beat.
    /// </summary>
    [Min(1)] public int DropBeats;

    /// <summary>
    /// Line Ribbon palette cycles per beat at the Drop landing. The active Drop envelope scales this
    /// speed continuously to zero across <see cref="DropBeats"/>.
    /// </summary>
    [Min(0f)] public float DropFlowCyclesPerBeatAtImpact;

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
    /// conditioning, the Low-gated beat phase front, Shape List Fill parts, live part separation,
    /// envelope width, and shared rotation rate, the Drop ribbon window and impact speed, three
    /// Energy-tier sweep rates, and directional-shading depth.
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
        FillRotationCyclesPerBeat = source.FillRotationCyclesPerBeat;
        FillPartHueSeparation = source.FillPartHueSeparation;
        FillUnitEnvelopeWidth = source.FillUnitEnvelopeWidth;
        FillContourStrength = source.FillContourStrength;
        DropBeats = source.DropBeats;
        DropFlowCyclesPerBeatAtImpact = source.DropFlowCyclesPerBeatAtImpact;
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
