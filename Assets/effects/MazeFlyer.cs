using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a first-person flight through a randomized voxel maze onto the Penrose tile buffer.
/// Each activation Rolls a fresh maze and flight; each frame advances the camera one step along
/// the maze path, then traces four rotated-grid voxel rays per tile (a 3D DDA over a toroidal
/// 16-cubed grid) and averages them into the tile's color.
/// </summary>
[EffectSyncSettings(typeof(MazeFlyerSyncSettingsAsset))]
public class MazeFlyer : EffectBase
{
    // Standalone Defaults

    /// <summary>
    /// Authored inclusive minimum for the per-activation flight-speed roll, in voxel cells per
    /// second.
    /// </summary>
    private const float StandaloneOverallSpeedMin = 1.0f;

    /// <summary>
    /// Authored inclusive maximum for the per-activation flight-speed roll, in voxel cells per
    /// second. The continuous roll replaced the original integer roll whose top speed of 4 read
    /// as streaking: corridor walls pass ~0.5 units from the camera, so a wall feature crossed
    /// the whole ~75-degree view in ~0.16 s. Near 2.25 the fastest rolls stay brisk but readable.
    /// Governs the Standalone roll only; Synced Mode replaces the roll with the energy-state
    /// tier speeds in the Sync Settings.
    /// </summary>
    private const float StandaloneOverallSpeedMax = 2.25f;

    /// <summary>Authored multiplier deriving camera turn speed from the rolled flight speed.</summary>
    private const float StandaloneTurnSpeedMultiplier = 2.5f;

    /// <summary>Authored probability that a non-guaranteed voxel cell is filled.</summary>
    private const float StandaloneFillProbability = 0.25f;

    /// <summary>Authored coordinate scale used to generate the Spatial Waves hue field.</summary>
    private const float StandaloneSpatialScale = 0.15f;

    /// <summary>Authored voxel-block width used by the Block Regions color mode.</summary>
    private const int StandaloneBlockSize = 4;

    /// <summary>Authored saturation of voxels in the Pure Random color mode.</summary>
    private const float StandalonePureRandomSaturation = 0.9f;

    /// <summary>Authored value of voxels in the Pure Random color mode.</summary>
    private const float StandalonePureRandomValue = 0.95f;

    /// <summary>Authored saturation of voxels in the Spatial Waves color mode.</summary>
    private const float StandaloneSpatialWavesSaturation = 0.85f;

    /// <summary>Authored value of voxels in the Spatial Waves color mode.</summary>
    private const float StandaloneSpatialWavesValue = 0.95f;

    /// <summary>Authored minimum hue jitter applied within each Block Regions voxel.</summary>
    private const float StandaloneBlockHueJitterMin = -0.05f;

    /// <summary>Authored maximum hue jitter applied within each Block Regions voxel.</summary>
    private const float StandaloneBlockHueJitterMax = 0.05f;

    /// <summary>Authored saturation of voxels in the Block Regions color mode.</summary>
    private const float StandaloneBlockRegionsSaturation = 0.88f;

    /// <summary>Authored value of voxels in the Block Regions color mode.</summary>
    private const float StandaloneBlockRegionsValue = 0.95f;

    /// <summary>Authored colors sampled by the Curated Palette mode.</summary>
    private static readonly Color[] StandaloneCuratedPalette =
    {
        Color.HSVToRGB(0.78f, 0.9f, 0.95f), // Purple/Magenta
        Color.HSVToRGB(0.55f, 0.9f, 0.95f), // Cyan/Blue
        Color.HSVToRGB(0.18f, 0.9f, 0.95f), // Yellow/Gold
        Color.HSVToRGB(0.38f, 0.9f, 0.95f)  // Lime Green
    };

    /// <summary>Authored camera focal length used to project tile centers into voxel rays.</summary>
    private const float StandaloneFocalLength = 18.0f;

    /// <summary>Authored normalized move progress at which look-ahead turn blending begins.</summary>
    private const float StandaloneTurnBlendStart = 0.2f;

    /// <summary>
    /// Authored navigation threshold: rolls above this value continue ahead, while rolls at or below
    /// it enter the non-reversing direction choice when alternatives exist.
    /// </summary>
    private const float StandaloneDirectionChoiceThreshold = 0.35f;

    /// <summary>
    /// Authored maximum ray distance and baseline fog range. Pulled in from 20 so the fog curve
    /// concentrates on distances rays actually hit (most land within 2-6 units); the far corridor
    /// goes black sooner in exchange for visible depth falloff in the midrange.
    /// </summary>
    private const float StandaloneMaxRayDistance = 14.0f;

    /// <summary>Authored shade multiplier for voxel faces hit across the X axis.</summary>
    private const float StandaloneXAxisFaceShade = 0.75f;

    /// <summary>Authored shade multiplier for voxel faces hit across the Y axis.</summary>
    private const float StandaloneYAxisFaceShade = 0.95f;

    /// <summary>Authored shade multiplier for voxel faces hit across the Z axis.</summary>
    private const float StandaloneZAxisFaceShade = 0.60f;

    /// <summary>
    /// Authored shade floor of the camera headlight at full grazing incidence. Faces struck
    /// square-on keep their full axis shade; faces seen edge-on dim toward this floor, so a long
    /// corridor wall picks up a smooth bright-to-dark ramp instead of one flat tone. The headlight
    /// is purely angular — distance falloff is the fog's job. First-audition value pending wall
    /// judgment.
    /// </summary>
    private const float StandaloneHeadlightMinShade = 0.5f;

    /// <summary>
    /// Authored density of the squared-exponential fog curve over the normalized ray distance.
    /// The squared curve keeps near walls bright and crushes the sub-tile far field toward black,
    /// unlike the linear fade it replaced, which compressed midrange contrast. First-audition
    /// value pending wall judgment.
    /// </summary>
    private const float StandaloneFogDensity = 2.5f;

    /// <summary>
    /// Authored on-wall thickness of voxel edge lines, in tile pitches. The band converts to
    /// voxel-space width per hit (thickness * distance / focal length), so lines render at
    /// constant thickness at every depth: near faces get crisp lines instead of swollen bands,
    /// and far faces keep a band wide enough to register on a point-sampled tile. Edge lines
    /// draw the voxel lattice onto wall faces so corridor geometry reads at the wall's 900-tile
    /// resolution. First-audition value pending wall judgment.
    /// </summary>
    private const float StandaloneEdgeLineThicknessTiles = 0.7f;

    /// <summary>
    /// Authored shade multiplier at the very edge of a voxel face, ramping linearly back to 1
    /// across <see cref="StandaloneEdgeLineThicknessTiles"/>. First-audition value pending wall
    /// judgment.
    /// </summary>
    private const float StandaloneEdgeLineShade = 0.25f;

    /// <summary>
    /// Authored scale of the rotated-grid supersample pattern, in tile-center units; 0.7 spans
    /// roughly one tile pitch, so the four rays cover the tile's footprint rather than its center
    /// point. Smaller sharpens toward point sampling; larger blurs across neighboring tiles.
    /// First-audition value pending wall judgment.
    /// </summary>
    private const float StandaloneSampleSpread = 0.7f;

    /// <summary>
    /// Authored minimum HSV value for colors rolled from the shared palette. Palettes may carry
    /// dark entries, and face shading and fog only multiply downward from the rolled color, so a
    /// dark roll reads as an unlit wall. The floor lifts value while keeping the entry's hue and
    /// saturation. First-audition value pending wall judgment.
    /// </summary>
    private const float StandaloneSharedPaletteMinValue = 0.8f;

    // Sync Defaults

    /// <summary>
    /// Authored maximum ray displacement on heavy beat hits. High values produce bigger wall recoil.
    /// This audio-reactivity control is a beat-feel tuning point. The response is temporarily disabled
    /// at 0f; the commented suggestion was 0.20f, with a recommended range of 0.05f to 0.40f.
    /// </summary>
    private const float SyncPulseStrength = 0f;

    /// <summary>
    /// Authored extra brightness boost added to voxel faces on audio peaks. This audio-reactivity control
    /// is a beat-feel tuning point. The response is temporarily disabled at 0f; the commented suggestion
    /// was 0.25f, with a recommended range of 0.00f to 0.50f.
    /// </summary>
    private const float SyncPeakBrightnessBoost = 0f;

    /// <summary>
    /// Authored amount by which fog distance contracts or expands with the rhythm, where 0 means off.
    /// This audio-reactivity control is a beat-feel tuning point. The response is temporarily disabled
    /// at 0f; the commented suggestion was 3.0f, with a recommended range of 0.0f to 6.0f.
    /// </summary>
    private const float SyncDynamicFogAmount = 0f;

    /// <summary>
    /// Authored low-band strength that arms the On Beat brightness pulse. Lows are read from
    /// BeatManager's smoothed levels (20 ms attack, 150 ms release), so the gate reacts to a
    /// bass hit within a frame or two without flickering at the threshold.
    /// </summary>
    private const float SyncOnBeatLowThreshold = 0.35f;

    /// <summary>
    /// Authored brightness multiplier boost applied to every traced wall while a quarter-beat
    /// On Beat gate is open and lows exceed <see cref="SyncOnBeatLowThreshold"/>. Multiplicative,
    /// so fog-black stays black and depth survives the pulse. Zero disables the response.
    /// First-audition value pending wall judgment.
    /// </summary>
    private const float SyncOnBeatBrightnessPulse = 0.5f;

    /// <summary>
    /// Authored flight speed while the phrase-scoped energy state reads Low. The energy-state
    /// speeds replace the Standalone speed roll in Synced Mode; the real-time Levels play no
    /// part in speed. First-audition value pending wall judgment.
    /// </summary>
    private const float SyncLowEnergySpeed = 1.0f;

    /// <summary>
    /// Authored flight speed while the phrase-scoped energy state reads Mid. First-audition
    /// value pending wall judgment.
    /// </summary>
    private const float SyncMidEnergySpeed = 1.6f;

    /// <summary>
    /// Authored flight speed while the phrase-scoped energy state reads High. First-audition
    /// value pending wall judgment.
    /// </summary>
    private const float SyncHighEnergySpeed = 2.25f;

    /// <summary>
    /// Authored SmoothDamp time constant, in seconds, easing the flight speed between energy
    /// tier plateaus so a phrase change never jerks the flight. First-audition value pending
    /// wall judgment.
    /// </summary>
    private const float SyncSpeedSmoothTime = 2.0f;

    /// <summary>
    /// Authored ramp window, in beats, for the beat-locked speed glide. Once the announced next
    /// energy state is this close, the speed target slides from the current tier's speed toward
    /// the next tier's, arriving as the phrase boundary lands instead of trailing it.
    /// First-audition value pending wall judgment.
    /// </summary>
    private const int SyncSpeedRampBeats = 8;

    /// <summary>
    /// Authored wind-down, in beats, over which the flight falls to a dead stop ahead of an
    /// announced Drop landing. The stop completes <see cref="SyncDropSitBeats"/> before the
    /// landing, and the wind-down is intra-beat smooth via BeatManager's continuous Drop
    /// approach envelope. First-audition value pending wall judgment.
    /// </summary>
    private const int SyncDropStopBeats = 4;

    /// <summary>
    /// Authored hold, in beats, that the flight sits parked at the dead stop before the Drop
    /// lands — the held breath between the wind-down and the launch. First-audition value
    /// pending wall judgment.
    /// </summary>
    private const int SyncDropSitBeats = 2;

    /// <summary>
    /// Authored speed multiplier at the instant a Drop lands. The launch decays back to 1 across
    /// the phrase-relative 16-beat timing-grid cycle, so the drop hits at full boost and glides
    /// back to the energy-tier cruise over the full grid. First-audition value pending wall
    /// judgment.
    /// </summary>
    private const float SyncDropLaunchMultiplier = 2.5f;

    /// <summary>
    /// Authored strength of the Fill edge inversion: each eighth-note pulse pumps the voxel
    /// lattice from dark lines on lit faces toward glowing lines on darkened faces while a
    /// synced Fill runs. One is a full flip at each pulse peak; zero disables the response.
    /// First-audition value pending wall judgment.
    /// </summary>
    private const float SyncFillEdgeInversion = 1.0f;

    /// <summary>
    /// Authored brightness multiplier of the lattice lines at full Fill inversion. Above 1 the
    /// inverted wireframe overdrives brighter than any normal face, so the flip adds light to
    /// the wall instead of only removing it — a plain endpoint swap dims the whole picture and
    /// reads as a fade, not an event. First-audition value pending wall judgment.
    /// </summary>
    private const float SyncFillLineGlow = 2.0f;

    /// <summary>
    /// Authored camera spin rate, in degrees per second, at the instant a Drop lands. The spin
    /// rides the launch envelope — fastest at the hit, settling with the speed across the
    /// 16-beat grid — and the look-ahead turning levels the camera naturally as it fades. It
    /// replaces the retired Fill camera roll, whose per-frame accumulation spun uncontrolled
    /// whenever the flight froze. First-audition value pending wall judgment.
    /// </summary>
    private const float SyncDropSpinSpeed = 90f;

    /// <summary>
    /// Authored modulo selecting one quarter of hit voxels for the retired event recoloring —
    /// currently unwired, retained pending the Fill edge inversion's wall check.
    /// </summary>
    private const int SyncEventCheckerModulo = 4;

    /// <summary>
    /// Authored effect-time multiplier driving the retired event recoloring pulses — currently
    /// unwired, retained pending the Fill edge inversion's wall check.
    /// </summary>
    private const float SyncEventPulseSpeed = 4f;

    // Effect Settings resolution

    /// <summary>
    /// Resolves a fresh copy of MazeFlyer's Standalone Defaults. The curated palette is cloned
    /// per resolve, so no activation shares mutable state with the authored table.
    /// </summary>
    public static MazeFlyerStandaloneSettings StandaloneSettings => new MazeFlyerStandaloneSettings
    {
        OverallSpeedMin = StandaloneOverallSpeedMin,
        OverallSpeedMax = StandaloneOverallSpeedMax,
        TurnSpeedMultiplier = StandaloneTurnSpeedMultiplier,
        FillProbability = StandaloneFillProbability,
        SpatialScale = StandaloneSpatialScale,
        BlockSize = StandaloneBlockSize,
        PureRandomSaturation = StandalonePureRandomSaturation,
        PureRandomValue = StandalonePureRandomValue,
        SpatialWavesSaturation = StandaloneSpatialWavesSaturation,
        SpatialWavesValue = StandaloneSpatialWavesValue,
        BlockHueJitter = new FloatRange(StandaloneBlockHueJitterMin, StandaloneBlockHueJitterMax),
        BlockRegionsSaturation = StandaloneBlockRegionsSaturation,
        BlockRegionsValue = StandaloneBlockRegionsValue,
        CuratedPalette = (Color[])StandaloneCuratedPalette.Clone(),
        FocalLength = StandaloneFocalLength,
        TurnBlendStart = StandaloneTurnBlendStart,
        DirectionChoiceThreshold = StandaloneDirectionChoiceThreshold,
        MaxRayDistance = StandaloneMaxRayDistance,
        XAxisFaceShade = StandaloneXAxisFaceShade,
        YAxisFaceShade = StandaloneYAxisFaceShade,
        ZAxisFaceShade = StandaloneZAxisFaceShade,
        HeadlightMinShade = StandaloneHeadlightMinShade,
        FogDensity = StandaloneFogDensity,
        EdgeLineThicknessTiles = StandaloneEdgeLineThicknessTiles,
        EdgeLineShade = StandaloneEdgeLineShade,
        SampleSpread = StandaloneSampleSpread,
        SharedPaletteMinValue = StandaloneSharedPaletteMinValue,
    };

    /// <summary>Resolves a fresh copy of MazeFlyer's file-local Sync Defaults.</summary>
    public static MazeFlyerSyncSettings SyncDefaults => new MazeFlyerSyncSettings
    {
        PulseStrength = SyncPulseStrength,
        PeakBrightnessBoost = SyncPeakBrightnessBoost,
        DynamicFogAmount = SyncDynamicFogAmount,
        OnBeatLowThreshold = SyncOnBeatLowThreshold,
        OnBeatBrightnessPulse = SyncOnBeatBrightnessPulse,
        LowEnergySpeed = SyncLowEnergySpeed,
        MidEnergySpeed = SyncMidEnergySpeed,
        HighEnergySpeed = SyncHighEnergySpeed,
        SpeedSmoothTime = SyncSpeedSmoothTime,
        SpeedRampBeats = SyncSpeedRampBeats,
        DropStopBeats = SyncDropStopBeats,
        DropSitBeats = SyncDropSitBeats,
        DropLaunchMultiplier = SyncDropLaunchMultiplier,
        DropSpinSpeed = SyncDropSpinSpeed,
        FillEdgeInversion = SyncFillEdgeInversion,
        FillLineGlow = SyncFillLineGlow,
        EventCheckerModulo = SyncEventCheckerModulo,
        EventPulseSpeed = SyncEventPulseSpeed,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private MazeFlyerStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private MazeFlyerSyncSettings SyncSettings { get; set; } = SyncDefaults;

    // Effect contract

    /// <summary>The musical capabilities and energy range advertised by MazeFlyer.</summary>
    public override Repertoire Repertoire =>
     Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow;

    /// <summary>Names the effect and the color mode the current activation rolled.</summary>
    public override string DebugText() => $"Maze Flyer [{activeColorMode}]";

    // Maze state

    /// <summary>
    /// Fixed width, height, and depth of the cubic voxel-grid algorithm. Must stay a power of
    /// two: the toroidal wrap and flat-grid indexing are bit operations derived from
    /// <see cref="VoxelGridShift"/> and <see cref="VoxelGridMask"/>.
    /// </summary>
    private const int VoxelGridSize = 16;

    /// <summary>Log2 of <see cref="VoxelGridSize"/>, the per-axis bit width of a flat grid index.</summary>
    private const int VoxelGridShift = 4;

    /// <summary>
    /// Bit mask wrapping a coordinate into [0, <see cref="VoxelGridSize"/>). For a power-of-two
    /// size the mask equals a positive modulo on two's-complement integers, so negative
    /// coordinates wrap toroidally with one AND instead of a divide and sign fix — the operation
    /// the ray tracer's inner loop runs three times per step.
    /// </summary>
    private const int VoxelGridMask = VoxelGridSize - 1;

    /// <summary>Total cell count of the flattened voxel grid.</summary>
    private const int VoxelCellCount = VoxelGridSize * VoxelGridSize * VoxelGridSize;

    /// <summary>The voxel coloring styles one activation can roll.</summary>
    private enum ColorMode
    {
        /// <summary>Every vertical column rolls an independent hue.</summary>
        PureRandom,

        /// <summary>Hue follows a smooth sine field over voxel coordinates.</summary>
        SpatialWaves,

        /// <summary>Voxel blocks share a hashed base hue with per-voxel jitter.</summary>
        BlockRegions,

        /// <summary>Every vertical column samples the authored curated palette.</summary>
        CuratedPalette,

        /// <summary>Every vertical column rolls a position in the shared animated show palette.</summary>
        SharedPalette
    }

    /// <summary>The color mode selected from the enum's complete five-member domain on activation.</summary>
    private ColorMode activeColorMode;

    /// <summary>
    /// Occupancy of the maze, indexed by <see cref="VoxelIndex"/>. The ray tracer's inner loop
    /// steps through mostly empty cells, so occupancy lives in its own compact array — small
    /// enough to stay cache-resident across a whole frame of rays — and the far larger color
    /// array is touched only on a hit.
    /// </summary>
    private readonly bool[] voxelSolid = new bool[VoxelCellCount];

    /// <summary>Voxel colors of the maze, indexed by <see cref="VoxelIndex"/>; read only for solid cells.</summary>
    private readonly Color[] voxelColors = new Color[VoxelCellCount];

    /// <summary>
    /// Column colors rolled per activation for the column-unit color modes (Pure Random, Curated
    /// Palette, and Shared Palette), indexed by (x, z). The unit of color is the vertical column —
    /// a pillar and the fill stacked above it — so color boundaries align with the maze's
    /// structure and a pillar reads as one object on the wall instead of a stack of unrelated
    /// hues.
    /// </summary>
    private readonly Color[,] columnColors = new Color[VoxelGridSize, VoxelGridSize];

    // Flight state

    /// <summary>The cell the camera is flying from.</summary>
    private Vector3Int currentCell;

    /// <summary>The cell the camera is flying toward.</summary>
    private Vector3Int targetCell;

    /// <summary>The direction of the current move.</summary>
    private Vector3Int moveDir = Vector3Int.forward;

    /// <summary>The move direction peeked one step ahead, used to blend upcoming turns early.</summary>
    private Vector3Int nextMoveDir = Vector3Int.forward;

    /// <summary>The camera position interpolated along the current move.</summary>
    private Vector3 cameraPos;

    /// <summary>The camera orientation, smoothed toward <see cref="targetRot"/> each frame.</summary>
    private Quaternion cameraRot = Quaternion.identity;

    /// <summary>The orientation the camera is turning toward, blended from the current and next move.</summary>
    private Quaternion targetRot = Quaternion.identity;

    /// <summary>Normalized progress of the current move, from 0 at the start cell to 1 at the target.</summary>
    private float moveProgress;

    /// <summary>The current flight speed, eased each frame toward <see cref="TargetFlySpeed"/>.</summary>
    private float flySpeed;

    /// <summary>The speed rolled on activation: the Standalone flight speed and the synced fallback target.</summary>
    private float rolledFlySpeed;

    /// <summary>SmoothDamp velocity state for the energy-state speed easing, discarded by each Roll.</summary>
    private float flySpeedVelocity;

    /// <summary>The current camera-turn speed derived from <see cref="flySpeed"/>.</summary>
    private float turnSpeed;

    /// <summary>
    /// Whether a Drop landing has armed the launch boost. Armed on the frame the Drop goes
    /// active and disarmed when the timing-grid cycle wraps — the point where the launch has
    /// fully decayed — so an ordinary grid cycle without a Drop never boosts.
    /// </summary>
    private bool dropLaunchArmed;

    /// <summary>The previous frame's Drop activity, for detecting the landing edge.</summary>
    private bool wasDropActive;

    /// <summary>The previous frame's timing-grid beat, for detecting the cycle wrap.</summary>
    private int? previousGridBeat;

    /// <summary>
    /// The frame's launch envelope, captured by <see cref="DropSpeedFactor"/>: the 16-beat grid
    /// decay while the launch is armed, otherwise zero. Drives the launch speed and camera spin
    /// together so both settle as one gesture.
    /// </summary>
    private float dropLaunchStrength;

    // Lifecycle

    /// <summary>
    /// Performs MazeFlyer's Roll: resolves Effect Settings, discards all carried flight state,
    /// rolls the activation's randomized values, and regenerates the voxel maze.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();

        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(MazeFlyer),
            SyncDefaults);

        // Discard carried flight state so the Roll is complete: no orientation, direction,
        // or progress survives from the previous activation.
        cameraRot = Quaternion.identity;
        targetRot = Quaternion.identity;
        moveDir = Vector3Int.forward;
        nextMoveDir = Vector3Int.forward;
        moveProgress = 0.0f;

        // Unfiltered acquisition spans the complete curated Waveform Pool, so MazeFlyer has no
        // authored Waveform-selection subrange to expose as Effect Settings.
        waveform = waveforms.Random();

        rolledFlySpeed = Random.Range(
            standaloneSettings.OverallSpeedMin,
            standaloneSettings.OverallSpeedMax);

        // An activation is already a visual cut, so the flight starts directly at its target —
        // the energy tier when synced, the roll otherwise — with no ramp from a stale speed.
        flySpeed = TargetFlySpeed();
        flySpeedVelocity = 0f;
        turnSpeed = flySpeed * standaloneSettings.TurnSpeedMultiplier;
        dropLaunchArmed = false;
        wasDropActive = false;
        previousGridBeat = null;
        dropLaunchStrength = 0f;

        // The enum has five members and GetVoxelColor handles all five, so [0, 5) is the complete
        // selector domain rather than an authored subrange.
        activeColorMode = (ColorMode)Random.Range(0, 5);

        GenerateVoxelGrid();
        InitializeCameraPosition();

        // Per-activation ray-setup caches: the supersample offsets pre-scaled by the activation's
        // sample spread, and the tile centers copied out of the tile metadata objects into one
        // contiguous array so the per-tile loop reads sequential memory instead of chasing 900
        // heap references every frame.
        for (int i = 0; i < RaySampleOffsets.Length; i++)
        {
            scaledSampleOffsets[i] = RaySampleOffsets[i] * standaloneSettings.SampleSpread;
        }
        if (tileCenters == null || tileCenters.Length != tiles.Length)
        {
            tileCenters = new Vector2[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                tileCenters[i] = tiles[i].center;
            }
        }
    }

    /// <summary>MazeFlyer holds no external resources, so ending an activation needs no teardown.</summary>
    public override void OnEnd()
    {
    }

    // Frame loop

    /// <summary>
    /// Rotated-grid supersample offsets in tile-pitch units, applied around each tile center and
    /// scaled by the authored sample spread. Four rays per tile turn the point sample into an
    /// area sample: edges antialias, and sub-tile features fade with distance instead of popping
    /// in and out as the camera moves.
    /// </summary>
    private static readonly Vector2[] RaySampleOffsets =
    {
        new Vector2(0.125f, 0.375f),
        new Vector2(0.375f, -0.125f),
        new Vector2(-0.125f, -0.375f),
        new Vector2(-0.375f, 0.125f)
    };

    /// <summary>
    /// <see cref="RaySampleOffsets"/> pre-scaled by the activation's sample spread, rebuilt each
    /// Roll so the per-ray loop skips the two per-sample multiplies.
    /// </summary>
    private readonly Vector2[] scaledSampleOffsets = new Vector2[RaySampleOffsets.Length];

    /// <summary>
    /// Tile centers copied from <see cref="EffectBase.tiles"/> into one contiguous array per
    /// activation. Tile geometry is static, but the metadata objects live scattered on the heap;
    /// the copy keeps the hot per-tile loop on sequential memory.
    /// </summary>
    private Vector2[] tileCenters;

    /// <summary>
    /// The per-frame constants of the voxel ray trace, computed once in <see cref="Draw"/> and
    /// passed by readonly reference to every ray. Hoisting them out of the per-ray path removes
    /// thousands of repeated settings reads per frame — every value here is invariant across the
    /// frame's 3,600 rays.
    /// </summary>
    private struct TraceFrame
    {
        /// <summary>Ray-origin displacement along the ray, in cells: the rhythm-scaled pulse recoil.</summary>
        public float PulseDistance;

        /// <summary>Maximum ray distance for the frame, including the rhythm-scaled dynamic fog term.</summary>
        public float MaxRayDistance;

        /// <summary>Density of the squared-exponential fog curve.</summary>
        public float FogDensity;

        /// <summary>Additive face-brightness boost for the frame: the rhythm-scaled audio peak term.</summary>
        public float PeakBoost;

        /// <summary>Multiplicative wall-brightness factor for the frame: the On Beat low pulse.</summary>
        public float BrightnessPulse;

        /// <summary>Blend weight of the Fill edge inversion for the frame.</summary>
        public float EdgeInversion;

        /// <summary>Shade multiplier for voxel faces hit across the X axis.</summary>
        public float XAxisFaceShade;

        /// <summary>Shade multiplier for voxel faces hit across the Y axis.</summary>
        public float YAxisFaceShade;

        /// <summary>Shade multiplier for voxel faces hit across the Z axis.</summary>
        public float ZAxisFaceShade;

        /// <summary>Shade floor of the camera headlight at full grazing incidence.</summary>
        public float HeadlightMinShade;

        /// <summary>Edge-line band width per unit of hit distance: thickness in tiles over focal length.</summary>
        public float EdgeBandScale;

        /// <summary>Shade multiplier at the very edge of a voxel face in the normal lattice.</summary>
        public float EdgeLineShade;

        /// <summary>Brightness multiplier of the lattice lines at full Fill inversion.</summary>
        public float FillLineGlow;
    }

    /// <summary>
    /// Advances the camera, then traces four rotated-grid voxel rays for every Penrose tile and
    /// averages them into the tile's color.
    /// </summary>
    public override void Draw()
    {
        UpdateCameraNavigation(effectDelta);

        // Check if beat tracking is active via the boolean flag
        bool isBeatSynced = beatManager.IsSynced;

        // Sample waveform envelope only when synced. The hard 0f is what keeps the
        // rhythm-scaled Sync Settings (PulseStrength, PeakBrightnessBoost, DynamicFogAmount)
        // classified sync-only: Standalone frames reach those slots, but scaled by this 0f a
        // live tweak cannot change the Standalone look.
        float rhythm = isBeatSynced ? waveform.Envelope : 0.0f;

        // On Beat low pulse: while any musical count's quarter-beat gate is open and the smoothed
        // low band exceeds the threshold, every traced wall brightens by the authored boost. Both
        // reads go through BeatManager, the single musical source; OnBeat reads false when the
        // wire lane is unavailable, so Standalone frames always trace with the neutral 1.
        bool onBeat = isBeatSynced
            && (beatManager.Beats.OnBeat(1) || beatManager.Beats.OnBeat(2)
                || beatManager.Beats.OnBeat(3) || beatManager.Beats.OnBeat(4));
        float brightnessPulse =
            onBeat && beatManager.Levels.Smoothed.Low > SyncSettings.OnBeatLowThreshold
                ? 1.0f + SyncSettings.OnBeatBrightnessPulse
                : 1.0f;

        // Fill edge inversion: while a synced Fill runs, each eighth-note pulse pumps the voxel
        // lattice from dark lines on lit faces toward glowing lines on darkened faces, then relaxes.
        // The tempo-derived pulse rests at zero without synchronization, and the whole response
        // rests at zero outside an active Fill, so Standalone frames always trace the normal
        // lattice.
        float edgeInversion = isBeatSynced && beatManager.Fill.Active
            ? SyncSettings.FillEdgeInversion * beatManager.Pulses.Every(Duration.Eighth)
            : 0.0f;

        // Everything invariant across the frame's rays is computed exactly once here: the
        // rhythm-scaled sync terms fold into per-frame constants, and the camera basis is
        // pre-scaled so each ray assembles its direction from component math alone.
        TraceFrame frame = new TraceFrame
        {
            PulseDistance = rhythm * SyncSettings.PulseStrength,
            MaxRayDistance = standaloneSettings.MaxRayDistance + (rhythm * SyncSettings.DynamicFogAmount),
            FogDensity = standaloneSettings.FogDensity,
            PeakBoost = rhythm * SyncSettings.PeakBrightnessBoost,
            BrightnessPulse = brightnessPulse,
            EdgeInversion = edgeInversion,
            XAxisFaceShade = standaloneSettings.XAxisFaceShade,
            YAxisFaceShade = standaloneSettings.YAxisFaceShade,
            ZAxisFaceShade = standaloneSettings.ZAxisFaceShade,
            HeadlightMinShade = standaloneSettings.HeadlightMinShade,
            EdgeBandScale = standaloneSettings.EdgeLineThicknessTiles / standaloneSettings.FocalLength,
            EdgeLineShade = standaloneSettings.EdgeLineShade,
            FillLineGlow = SyncSettings.FillLineGlow,
        };

        Vector3 forwardScaled = cameraRot * Vector3.forward * standaloneSettings.FocalLength;
        Vector3 cameraRight = cameraRot * Vector3.right;
        Vector3 cameraUp = cameraRot * Vector3.up;

        float sampleWeight = 1.0f / RaySampleOffsets.Length;
        Vector3 rayOrigin = cameraPos;

        for (int i = 0; i < buffer.Length; i++)
        {
            Vector2 center = tileCenters[i];

            float r = 0f, g = 0f, b = 0f;
            foreach (Vector2 offset in scaledSampleOffsets)
            {
                float sx = center.x + offset.x;
                float sy = center.y + offset.y;

                // The ray direction is normalized by hand: the forward component is at least the
                // focal length, so the magnitude can never approach the zero-vector guard that
                // Vector3.normalized would otherwise re-check on all 3,600 rays.
                float dx = forwardScaled.x + (cameraRight.x * sx) + (cameraUp.x * sy);
                float dy = forwardScaled.y + (cameraRight.y * sx) + (cameraUp.y * sy);
                float dz = forwardScaled.z + (cameraRight.z * sx) + (cameraUp.z * sy);
                float invLength = 1.0f / MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                Vector3 rayDir = new Vector3(dx * invLength, dy * invLength, dz * invLength);

                Color sample = TraceVoxelRay(rayOrigin, rayDir, in frame);
                r += sample.r;
                g += sample.g;
                b += sample.b;
            }

            buffer[i] = new Color(r * sampleWeight, g * sampleWeight, b * sampleWeight, 1.0f);
        }
    }

    // Maze generation

    /// <summary>Populates the voxel grid, coloring filled cells by the rolled color mode.</summary>
    private void GenerateVoxelGrid()
    {
        // The column-unit modes roll their colors up front, one per vertical (x, z) column.
        if (activeColorMode is ColorMode.PureRandom or ColorMode.CuratedPalette or ColorMode.SharedPalette)
        {
            for (int x = 0; x < VoxelGridSize; x++)
            {
                for (int z = 0; z < VoxelGridSize; z++)
                {
                    columnColors[x, z] = RollColumnColor();
                }
            }
        }

        for (int x = 0; x < VoxelGridSize; x++)
        {
            for (int y = 0; y < VoxelGridSize; y++)
            {
                for (int z = 0; z < VoxelGridSize; z++)
                {
                    // Cells on the even lattice (all three coordinates even) are always filled,
                    // guaranteeing the maze a regular pillar structure to fly between.
                    bool onEvenLattice = (x % 2 == 0) && (y % 2 == 0) && (z % 2 == 0);

                    // Random.value spans the complete probability domain; FillProbability authors its
                    // threshold. Guaranteed even-lattice cells short-circuit before that roll, so they
                    // consume no Random.value and retain the original mode-specific roll order.
                    int index = VoxelIndex(x, y, z);
                    if (onEvenLattice || Random.value < standaloneSettings.FillProbability)
                    {
                        voxelSolid[index] = true;
                        voxelColors[index] = GetVoxelColor(x, y, z);
                    }
                    else
                    {
                        // A stale color may linger from the previous Roll; it is never read
                        // because reads gate on occupancy.
                        voxelSolid[index] = false;
                    }
                }
            }
        }
    }

    /// <summary>Evaluates one voxel's color according to the color mode the Roll selected.</summary>
    private Color GetVoxelColor(int x, int y, int z)
    {
        switch (activeColorMode)
        {
            case ColorMode.PureRandom:
            case ColorMode.CuratedPalette:
            case ColorMode.SharedPalette:
                return columnColors[x, z];

            case ColorMode.SpatialWaves:
                float spatialScale = standaloneSettings.SpatialScale;
                float waveHue = (Mathf.Sin(x * spatialScale) + Mathf.Cos(y * spatialScale) + Mathf.Sin(z * spatialScale) + 3f) / 6f;
                return Color.HSVToRGB(
                    waveHue,
                    standaloneSettings.SpatialWavesSaturation,
                    standaloneSettings.SpatialWavesValue);

            case ColorMode.BlockRegions:
                int blockSize = standaloneSettings.BlockSize;
                int blockX = x / blockSize;
                int blockY = y / blockSize;
                int blockZ = z / blockSize;

                // Prime multipliers hash the block coordinates into a stable per-block base hue.
                int blockHash = blockX * 73 + blockY * 179 + blockZ * 283;

                float baseHue = (Mathf.Abs(blockHash) % 100) / 100.0f;
                float blockHue = (
                    baseHue +
                    Random.Range(standaloneSettings.BlockHueJitter.Min, standaloneSettings.BlockHueJitter.Max) +
                    1.0f) % 1.0f;
                return Color.HSVToRGB(
                    blockHue,
                    standaloneSettings.BlockRegionsSaturation,
                    standaloneSettings.BlockRegionsValue);

            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Rolls one column color for the column-unit modes (Pure Random, Curated Palette, Shared
    /// Palette). The color itself is fixed here at Roll time, so an activation keeps the colors
    /// it rolled for its whole flight.
    /// </summary>
    private Color RollColumnColor()
    {
        switch (activeColorMode)
        {
            case ColorMode.PureRandom:
                // Random.value spans the complete hue-wheel domain; only saturation and value are authored settings.
                return Color.HSVToRGB(
                    Random.value,
                    standaloneSettings.PureRandomSaturation,
                    standaloneSettings.PureRandomValue);

            case ColorMode.SharedPalette:
            {
                // Random.value rolls a blended position across the shared animated show palette.
                // The rolled color's HSV value is lifted to the authored floor — face shading and
                // fog only multiply downward, so a dark palette entry would read as an unlit wall.
                Color rolled = APalette.read(Random.value, true);
                Color.RGBToHSV(rolled, out float hue, out float saturation, out float value);
                return Color.HSVToRGB(
                    hue,
                    saturation,
                    Mathf.Max(value, standaloneSettings.SharedPaletteMinValue));
            }

            default:
                // The inline selector spans every entry in the complete authored palette table.
                return standaloneSettings.CuratedPalette[
                    Random.Range(0, standaloneSettings.CuratedPalette.Length)];
        }
    }

    // Flight navigation

    /// <summary>The six cardinal step directions a flight move can take, in authored scan order.</summary>
    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.forward, Vector3Int.back,
        Vector3Int.left, Vector3Int.right,
        Vector3Int.up, Vector3Int.down
    };

    /// <summary>
    /// Places the camera in the first empty cell of a deterministic x, y, z scan and aims the
    /// first move from it.
    /// </summary>
    private void InitializeCameraPosition()
    {
        for (int x = 0; x < VoxelGridSize; x++)
        {
            for (int y = 0; y < VoxelGridSize; y++)
            {
                for (int z = 0; z < VoxelGridSize; z++)
                {
                    if (IsCellEmpty(new Vector3Int(x, y, z)))
                    {
                        currentCell = new Vector3Int(x, y, z);
                        targetCell = currentCell;
                        cameraPos = GetCellCenter(currentCell);

                        SelectNextMoveDirection();
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles camera movement along the grid path and smooth look-ahead rotation blending.
    /// Flight and turning move at the eased energy-tier speed scaled by the frame's
    /// <see cref="DropSpeedFactor"/>, so the flight stops into an announced Drop and launches
    /// out of its landing.
    /// </summary>
    private void UpdateCameraNavigation(float deltaTime)
    {
        // Energy-state speed chase: SmoothDamp eases the flight between tier plateaus so a
        // phrase change never jerks the flight; turn agility follows the same eased speed. On
        // Standalone frames the target is the rolled speed the flight already holds, so the
        // sync knobs cannot move a Standalone frame.
        flySpeed = Mathf.SmoothDamp(
            flySpeed, TargetFlySpeed(), ref flySpeedVelocity, SyncSettings.SpeedSmoothTime, float.PositiveInfinity, deltaTime);

        // The Drop response multiplies the smoothed speed rather than the SmoothDamp target:
        // its envelopes are already continuous and beat-locked, and the one discontinuity —
        // zero to full launch at the landing — is the hit itself, which the damp would blunt
        // into a slow swell. Turn agility follows the same effective speed, so a full stop
        // also freezes the camera's turning.
        float effectiveFlySpeed = flySpeed * DropSpeedFactor();
        turnSpeed = effectiveFlySpeed * standaloneSettings.TurnSpeedMultiplier;

        moveProgress += deltaTime * effectiveFlySpeed;

        if (moveProgress >= 1.0f)
        {
            currentCell = targetCell;
            moveDir = nextMoveDir;
            // Carry the overshoot into the next segment so motion stays continuous
            // instead of hitching at high fly speeds or low frame rates.
            moveProgress -= 1.0f;
            SelectNextMoveDirection();
        }

        Vector3 startPos = GetCellCenter(currentCell);
        Vector3 endPos = GetCellCenter(targetCell);
        cameraPos = Vector3.Lerp(startPos, endPos, moveProgress);

        Vector3 currentDirVec = new Vector3(moveDir.x, moveDir.y, moveDir.z);
        Vector3 nextDirVec = new Vector3(nextMoveDir.x, nextMoveDir.y, nextMoveDir.z);

        Vector3 blendedForward = Vector3.Lerp(
            currentDirVec,
            nextDirVec,
            Mathf.SmoothStep(standaloneSettings.TurnBlendStart, 1.0f, moveProgress)).normalized;

        if (blendedForward != Vector3.zero)
        {
            targetRot = Quaternion.LookRotation(blendedForward);
        }

        // The Drop launch spins the camera about its forward axis at the authored rate scaled
        // by the launch envelope: fastest at the hit, settling with the speed across the
        // 16-beat grid. The spin is an incremental per-frame rotation, and the look-ahead
        // Slerp — alive again because the launch restores turn speed — levels the camera
        // naturally as the spin fades. Outside a launch the increment is zero, so the stop
        // and sit hold a true standstill and Standalone frames never spin.
        cameraRot = Quaternion.Slerp(cameraRot, targetRot, deltaTime * turnSpeed)
                  * Quaternion.AngleAxis(
                      SyncSettings.DropSpinSpeed * dropLaunchStrength * deltaTime,
                      Vector3.forward);
    }

    /// <summary>
    /// Returns the frame's flight-speed target: the phrase-scoped energy state's tier speed when
    /// synced with a known state, otherwise the activation's rolled speed. The real-time Levels
    /// play no part in speed. Inside the ramp window before an announced state change, the
    /// target glides toward the next tier's speed so the flight arrives with the phrase boundary.
    /// </summary>
    private float TargetFlySpeed()
    {
        if (!beatManager.IsSynced || beatManager.Energy.Level is not { } energyState)
        {
            return rolledFlySpeed;
        }

        float tierSpeed = TierSpeed(energyState);

        // Beat-locked glide: once the announced next state is inside the ramp window, the target
        // slides toward the next tier's speed, reaching it as the countdown hits zero. BeatsUntil
        // moves in whole beats; SmoothDamp rounds the per-beat staircase into a continuous glide.
        // At the flip the new tier holds the speed the ramp just arrived at, so there is no kink.
        if (beatManager.NextEnergy.Level is { } nextState
            && beatManager.NextEnergy.BeatsUntil is { } beatsUntil
            && beatsUntil <= SyncSettings.SpeedRampBeats)
        {
            float rampProgress = 1.0f - ((float)beatsUntil / SyncSettings.SpeedRampBeats);
            return Mathf.Lerp(tierSpeed, TierSpeed(nextState), rampProgress);
        }

        return tierSpeed;
    }

    /// <summary>Returns the authored flight speed for one phrase-scoped energy tier.</summary>
    private float TierSpeed(Energy energyState) => energyState switch
    {
        Energy.Low => SyncSettings.LowEnergySpeed,
        Energy.Mid => SyncSettings.MidEnergySpeed,
        _ => SyncSettings.HighEnergySpeed,
    };

    /// <summary>
    /// Computes the frame's Drop speed multiplier: the flight winds down to a dead stop, sits
    /// parked through the final beats before the announced landing, then launches at the
    /// authored multiplier the instant the Drop hits and glides back to cruise across the
    /// phrase-relative 16-beat timing-grid cycle. Reads 1 on Standalone frames and whenever no
    /// Drop is announced, so the response is a safe always-on multiplier.
    /// </summary>
    private float DropSpeedFactor()
    {
        if (!beatManager.IsSynced)
        {
            dropLaunchArmed = false;
            wasDropActive = false;
            previousGridBeat = null;
            dropLaunchStrength = 0f;
            return 1.0f;
        }

        // The landing edge arms the launch; the grid-cycle wrap disarms it. The landing frame
        // itself skips the wrap check, because the landing restarts the grid and that restart
        // would otherwise read as the wrap that ends the launch it just began.
        bool dropActive = beatManager.Drop.Active;
        bool justLanded = dropActive && !wasDropActive;
        wasDropActive = dropActive;
        if (justLanded)
        {
            dropLaunchArmed = true;
        }
        else if (dropLaunchArmed
            && previousGridBeat is { } previous
            && beatManager.Grid.Beat is { } current
            && current < previous)
        {
            dropLaunchArmed = false;
        }
        previousGridBeat = beatManager.Grid.Beat;

        // The stop profile spans the wind-down plus the sit before the landing: the flight
        // falls continuously to a dead stop across DropStopBeats, sits parked through the
        // final DropSitBeats, and the landing launches it. The approach envelope rests at 1
        // with no announced Drop, so undropped stretches read a clean 1.
        int windowBeats = SyncSettings.DropStopBeats + SyncSettings.DropSitBeats;
        float stopFraction = (float)SyncSettings.DropStopBeats / windowBeats;
        float factor = Mathf.Clamp01(
            (beatManager.Drop.Before.Decay(windowBeats) - (1.0f - stopFraction)) / stopFraction);

        // Grid.Decay() falls 1 to 0 across the 16-beat cycle and rests at 0 when the grid is
        // unavailable, so a gridless wire quietly contributes no launch. The captured strength
        // also drives the camera spin, so speed and spin settle as one gesture.
        dropLaunchStrength = dropLaunchArmed ? beatManager.Grid.Decay() : 0f;
        return factor * (1.0f + ((SyncSettings.DropLaunchMultiplier - 1.0f) * dropLaunchStrength));
    }

    /// <summary>
    /// Reusable scratch buffer of open directions from the look-ahead cell, filled by
    /// <see cref="CountOpenDirectionsFrom"/>. Sized to the six cardinal directions and reused
    /// every pathfinding step so steady-state flight allocates nothing for the collector to sweep.
    /// </summary>
    private readonly Vector3Int[] openDirections = new Vector3Int[CardinalDirections.Length];

    /// <summary>Reusable scratch buffer of non-reversing candidates filtered from <see cref="openDirections"/>.</summary>
    private readonly Vector3Int[] nonReversingDirections = new Vector3Int[CardinalDirections.Length];

    /// <summary>
    /// Pathfinding step: Sets immediate target cell and peeks one cell ahead to predict upcoming turns.
    /// </summary>
    private void SelectNextMoveDirection()
    {
        // Advance the target cell along the current move direction, then look ahead from it to
        // predict the turn after this one.
        targetCell = currentCell + moveDir;
        int openCount = CountOpenDirectionsFrom(targetCell);

        if (openCount == 0)
        {
            nextMoveDir = -moveDir; // Dead end — prepare to turn around
            return;
        }

        bool canContinueAhead = false;
        for (int i = 0; i < openCount; i++)
        {
            if (openDirections[i] == moveDir)
            {
                canContinueAhead = true;
                break;
            }
        }

        // Random.value spans the complete probability domain; DirectionChoiceThreshold authors its
        // split. The short-circuit is load-bearing: the roll is consumed only when continuing ahead
        // is possible, so restructuring this condition would change the Standalone look.
        if (canContinueAhead &&
            (Random.value > standaloneSettings.DirectionChoiceThreshold || openCount == 1))
        {
            nextMoveDir = moveDir;
        }
        else
        {
            Vector3Int reverseDir = -moveDir;
            int nonReversingCount = 0;
            for (int i = 0; i < openCount; i++)
            {
                if (openDirections[i] != reverseDir)
                {
                    nonReversingDirections[nonReversingCount++] = openDirections[i];
                }
            }

            if (nonReversingCount > 0)
            {
                // The inline selector spans every currently valid non-reversing direction.
                nextMoveDir = nonReversingDirections[Random.Range(0, nonReversingCount)];
            }
            else
            {
                nextMoveDir = canContinueAhead ? moveDir : reverseDir;
            }
        }
    }

    /// <summary>
    /// Collects the cardinal directions whose neighboring cell is empty into
    /// <see cref="openDirections"/>, in scan order, and returns how many were found.
    /// </summary>
    private int CountOpenDirectionsFrom(Vector3Int cell)
    {
        int count = 0;

        foreach (var dir in CardinalDirections)
        {
            if (IsCellEmpty(cell + dir))
            {
                openDirections[count++] = dir;
            }
        }

        return count;
    }

    /// <summary>Reports whether the cell is empty, wrapping coordinates toroidally into the grid.</summary>
    private bool IsCellEmpty(Vector3Int cell)
    {
        return !voxelSolid[VoxelIndex(cell.x, cell.y, cell.z)];
    }

    /// <summary>Converts a cell coordinate to the world-space center of that cell.</summary>
    private static Vector3 GetCellCenter(Vector3Int cell)
    {
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
    }

    // Voxel ray tracing

    /// <summary>The grid axis a ray crossed when it entered the voxel it hit.</summary>
    private enum Axis
    {
        X,
        Y,
        Z
    }

    /// <summary>
    /// Executes 3D DDA voxel ray stepping with audio-driven spatial recoil, returning the color of
    /// the first filled voxel — face-shaded, headlight-dimmed by grazing incidence, edge-lined at
    /// the frame's Fill inversion weight, squared-exponentially fogged, and scaled by the frame's
    /// On Beat brightness pulse — or black past the fog range. Runs 3,600 times per frame; every
    /// frame-invariant input arrives precomputed in the <see cref="TraceFrame"/>.
    /// </summary>
    private Color TraceVoxelRay(Vector3 rayOrigin, Vector3 rayDir, in TraceFrame frame)
    {
        // Apply spatial pulse along ray direction when audio is present and synced
        Vector3 pulsedOrigin = rayOrigin + (rayDir * frame.PulseDistance);

        float rx = ClampAwayFromZero(rayDir.x);
        float ry = ClampAwayFromZero(rayDir.y);
        float rz = ClampAwayFromZero(rayDir.z);

        int mapX = FloorToInt(pulsedOrigin.x);
        int mapY = FloorToInt(pulsedOrigin.y);
        int mapZ = FloorToInt(pulsedOrigin.z);

        int stepX = rx > 0 ? 1 : -1;
        int stepY = ry > 0 ? 1 : -1;
        int stepZ = rz > 0 ? 1 : -1;

        float deltaDistX = Mathf.Abs(1.0f / rx);
        float deltaDistY = Mathf.Abs(1.0f / ry);
        float deltaDistZ = Mathf.Abs(1.0f / rz);

        float sideDistX = (stepX > 0) ? (mapX + 1.0f - pulsedOrigin.x) * deltaDistX : (pulsedOrigin.x - mapX) * deltaDistX;
        float sideDistY = (stepY > 0) ? (mapY + 1.0f - pulsedOrigin.y) * deltaDistY : (pulsedOrigin.y - mapY) * deltaDistY;
        float sideDistZ = (stepZ > 0) ? (mapZ + 1.0f - pulsedOrigin.z) * deltaDistZ : (pulsedOrigin.z - mapZ) * deltaDistZ;

        float distanceTraveled = 0f;

        // A ray that starts inside a filled voxel (possible when the pulse displaces the origin)
        // shades as an X-axis face.
        Axis hitAxis = Axis.X;

        while (distanceTraveled < frame.MaxRayDistance)
        {
            // The occupancy test is the innermost operation of the whole effect: one masked
            // flat index into the compact occupancy array. Colors load only on a hit.
            int index = VoxelIndex(mapX, mapY, mapZ);

            if (voxelSolid[index])
            {
                Color voxelColor = voxelColors[index];

                // Shading calculations with audio peak boost
                float baseShade = hitAxis switch
                {
                    Axis.X => frame.XAxisFaceShade,
                    Axis.Y => frame.YAxisFaceShade,
                    _ => frame.ZAxisFaceShade,
                };

                // Camera headlight: for an axis-aligned face the incidence is the ray's component
                // along the hit axis, so square-on faces keep the full axis shade while grazing
                // faces dim toward the floor. This puts a smooth view-tracking ramp along a wall
                // that the flat axis shade alone renders as one uniform tone.
                float incidence = hitAxis switch
                {
                    Axis.X => Mathf.Abs(rayDir.x),
                    Axis.Y => Mathf.Abs(rayDir.y),
                    _ => Mathf.Abs(rayDir.z),
                };
                float headlight = Mathf.Lerp(frame.HeadlightMinShade, 1.0f, incidence);
                float shade = (baseShade * headlight) + frame.PeakBoost;

                // Squared-exponential fog over the normalized ray distance: near walls stay
                // bright, the sub-tile far field crushes toward black. At the range cutoff the
                // curve is already near zero, so the black past-range return stays seamless.
                float normalizedDistance = distanceTraveled / frame.MaxRayDistance;
                float fogExponent = frame.FogDensity * normalizedDistance;
                float fog = MathF.Exp(-fogExponent * fogExponent);

                float edge = EdgeLineFactor(
                    pulsedOrigin + (rayDir * distanceTraveled), hitAxis, distanceTraveled, in frame);
                float brightness = shade * fog * edge * frame.BrightnessPulse;

                return new Color(
                    Mathf.Clamp01(voxelColor.r * brightness),
                    Mathf.Clamp01(voxelColor.g * brightness),
                    Mathf.Clamp01(voxelColor.b * brightness),
                    1.0f
                );
            }

            if (sideDistX < sideDistY && sideDistX < sideDistZ)
            {
                distanceTraveled = sideDistX;
                sideDistX += deltaDistX;
                mapX += stepX;
                hitAxis = Axis.X;
            }
            else if (sideDistY < sideDistZ)
            {
                distanceTraveled = sideDistY;
                sideDistY += deltaDistY;
                mapY += stepY;
                hitAxis = Axis.Y;
            }
            else
            {
                distanceTraveled = sideDistZ;
                sideDistZ += deltaDistZ;
                mapZ += stepZ;
                hitAxis = Axis.Z;
            }
        }

        return Color.black;
    }

    /// <summary>
    /// Computes the edge-line multiplier for a ray hit: 1 across the face interior, ramping down
    /// to the authored edge shade near any voxel edge — or that lattice flipped to bright lines
    /// on dimmed faces, blended at the frame's Fill inversion weight. The band's voxel-space width scales
    /// with hit distance so the rendered line keeps a constant on-wall thickness in tiles at
    /// every depth. Drawing the voxel lattice onto wall faces keeps corridor geometry legible at
    /// the wall's 900-tile resolution, where a face only reads as structure when contrast lives
    /// at its boundaries.
    /// </summary>
    private static float EdgeLineFactor(Vector3 hitPos, Axis hitAxis, float hitDistance, in TraceFrame frame)
    {
        // Constant on-wall thickness: one tile pitch at the focal plane projects from a
        // voxel-space width of distance / focalLength. Capped at half a face span, where the
        // bands from opposite edges meet.
        float bandWidth = Mathf.Min(frame.EdgeBandScale * hitDistance, 0.5f);
        if (bandWidth <= 0.0f)
        {
            return 1.0f;
        }

        // The two coordinates spanning the hit face; the third axis is the face plane itself.
        (float u, float v) = hitAxis switch
        {
            Axis.X => (hitPos.y, hitPos.z),
            Axis.Y => (hitPos.x, hitPos.z),
            _ => (hitPos.x, hitPos.y),
        };

        float uFrac = Mathf.Repeat(u, 1.0f);
        float vFrac = Mathf.Repeat(v, 1.0f);
        float edgeDistance = Mathf.Min(
            Mathf.Min(uFrac, 1.0f - uFrac),
            Mathf.Min(vFrac, 1.0f - vFrac));

        float interior = Mathf.Clamp01(edgeDistance / bandWidth);
        float normal = Mathf.Lerp(frame.EdgeLineShade, 1.0f, interior);
        if (frame.EdgeInversion <= 0.0f)
        {
            return normal;
        }

        // The Fill inversion flips the lattice — lines overdriven to the authored glow on faces
        // dimmed to the edge shade — and blends toward that flipped lattice at the frame's
        // inversion weight, so the eighth-note pulse pumps the wireframe in and out. The glow
        // pushes line brightness past any normal face, so the flip adds light to the wall
        // instead of only removing it.
        float inverted = Mathf.Lerp(frame.FillLineGlow, frame.EdgeLineShade, interior);
        return Mathf.Lerp(normal, inverted, frame.EdgeInversion);
    }

    /// <summary>
    /// Recolors one checker-selected slice of hit voxels while a synced Fill or Drop is active:
    /// Drop flashes a grayscale pulse and Fill flashes a hue-cycling pulse. Fill wins when both
    /// events are active, matching the original apply order (Drop first, Fill overwrote it).
    /// </summary>
    /// <remarks>
    /// Currently unwired: the Drop response moved to the stop-and-launch speed envelope and the
    /// Fill response to the edge inversion. Retained pending the edge inversion's wall check —
    /// if the inversion fails there, the grayscale flash moves over to Fill; otherwise this
    /// mechanism and its two settings (EventCheckerModulo, EventPulseSpeed) get deleted.
    /// </remarks>
    private Color ApplySyncedEventRecolor(Color voxelColor, int vx, int vy, int vz)
    {
        if (!beatManager.Drop.Active && !beatManager.Fill.Active)
        {
            return voxelColor;
        }

        if ((vx + vy + vz) % SyncSettings.EventCheckerModulo != 0)
        {
            return voxelColor;
        }

        var t = Mathf.PingPong(effectTime * SyncSettings.EventPulseSpeed, 2)
            .Remap(0f, 2, 0f, 1f, clamp: true);

        return beatManager.Fill.Active
            ? Color.HSVToRGB(t, 1f, 1f)
            : Color.HSVToRGB(0f, 0f, t);
    }

    // Math helpers

    /// <summary>
    /// Clamps a near-zero ray component to a small epsilon while preserving its sign, so DDA
    /// step distances stay finite and near-axis-aligned rays keep their true step direction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ClampAwayFromZero(float component)
    {
        if (Mathf.Abs(component) >= 1e-6f)
        {
            return component;
        }

        return component < 0f ? -1e-6f : 1e-6f;
    }

    /// <summary>
    /// Floors a float to an int by truncating cast plus a negative-fraction fix — the same
    /// result as Mathf.FloorToInt without its round trip through double-precision Math.Floor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FloorToInt(float value)
    {
        int truncated = (int)value;
        return value < truncated ? truncated - 1 : truncated;
    }

    /// <summary>
    /// Flattens a voxel coordinate into an index for <see cref="voxelSolid"/> and
    /// <see cref="voxelColors"/>. The <see cref="VoxelGridMask"/> ANDs wrap each axis
    /// toroidally — negatives included — so callers pass raw world cell coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int VoxelIndex(int x, int y, int z)
    {
        return ((x & VoxelGridMask) << (VoxelGridShift * 2))
             | ((y & VoxelGridMask) << VoxelGridShift)
             | (z & VoxelGridMask);
    }
}

/// <summary>The fixed Standalone Settings resolved from MazeFlyer's file-local Standalone Defaults.</summary>
public sealed class MazeFlyerStandaloneSettings
{
    /// <summary>Inclusive minimum for the per-activation flight-speed roll, in voxel cells per second.</summary>
    public float OverallSpeedMin;

    /// <summary>Inclusive maximum for the per-activation flight-speed roll, in voxel cells per second.</summary>
    public float OverallSpeedMax;

    /// <summary>Multiplier deriving camera turn speed from the rolled flight speed.</summary>
    public float TurnSpeedMultiplier;

    /// <summary>Probability that a non-guaranteed voxel cell is filled.</summary>
    public float FillProbability;

    /// <summary>Coordinate scale used to generate the Spatial Waves hue field.</summary>
    public float SpatialScale;

    /// <summary>Voxel-block width used by the Block Regions color mode.</summary>
    public int BlockSize;

    /// <summary>Saturation of voxels in the Pure Random color mode.</summary>
    public float PureRandomSaturation;

    /// <summary>Value of voxels in the Pure Random color mode.</summary>
    public float PureRandomValue;

    /// <summary>Saturation of voxels in the Spatial Waves color mode.</summary>
    public float SpatialWavesSaturation;

    /// <summary>Value of voxels in the Spatial Waves color mode.</summary>
    public float SpatialWavesValue;

    /// <summary>Per-voxel hue-jitter range used by the Block Regions color mode.</summary>
    public FloatRange BlockHueJitter;

    /// <summary>Saturation of voxels in the Block Regions color mode.</summary>
    public float BlockRegionsSaturation;

    /// <summary>Value of voxels in the Block Regions color mode.</summary>
    public float BlockRegionsValue;

    /// <summary>Colors sampled by the Curated Palette mode.</summary>
    public Color[] CuratedPalette;

    /// <summary>Camera focal length used to project tile centers into voxel rays.</summary>
    public float FocalLength;

    /// <summary>Normalized move progress at which look-ahead turn blending begins.</summary>
    public float TurnBlendStart;

    /// <summary>Threshold separating forward continuation from alternate non-reversing direction selection.</summary>
    public float DirectionChoiceThreshold;

    /// <summary>Maximum ray distance and baseline fog range.</summary>
    public float MaxRayDistance;

    /// <summary>Shade multiplier for voxel faces hit across the X axis.</summary>
    public float XAxisFaceShade;

    /// <summary>Shade multiplier for voxel faces hit across the Y axis.</summary>
    public float YAxisFaceShade;

    /// <summary>Shade multiplier for voxel faces hit across the Z axis.</summary>
    public float ZAxisFaceShade;

    /// <summary>Shade floor of the camera headlight at full grazing incidence.</summary>
    public float HeadlightMinShade;

    /// <summary>Density of the squared-exponential fog curve over the normalized ray distance.</summary>
    public float FogDensity;

    /// <summary>On-wall thickness of voxel edge lines, in tile pitches, held constant across depth.</summary>
    public float EdgeLineThicknessTiles;

    /// <summary>Shade multiplier at the very edge of a voxel face, ramping back to 1 across <see cref="EdgeLineThicknessTiles"/>.</summary>
    public float EdgeLineShade;

    /// <summary>Scale of the rotated-grid supersample pattern, in tile-center units.</summary>
    public float SampleSpread;

    /// <summary>Minimum HSV value for colors rolled from the shared palette.</summary>
    public float SharedPaletteMinValue;
}

/// <summary>The saved musical-response settings used by MazeFlyer in Synced Mode.</summary>
[Serializable]
public sealed class MazeFlyerSyncSettings
{
    /// <summary>
    /// Maximum ray displacement on heavy beat hits. High values produce bigger wall recoil. Zero
    /// temporarily disables the response. This is a beat-feel tuning control; 0.20 was suggested,
    /// with 0.05 to 0.40 recommended.
    /// </summary>
    [Header("Audio Reactivity Settings")]
    [Tooltip("Maximum ray displacement on heavy beat hits. High values = bigger wall recoil. 0 disables it; recommended: 0.05 to 0.40.")]
    [Range(0f, 0.40f)]
    public float PulseStrength;

    /// <summary>
    /// Extra brightness boost added to voxel faces on audio peaks. Zero temporarily disables the
    /// response. This is a beat-feel tuning control; 0.25 was suggested, with 0.00 to 0.50 recommended.
    /// </summary>
    [Tooltip("Extra brightness boost added to voxel faces on audio peaks. 0 disables it; recommended: 0.00 to 0.50.")]
    [Range(0f, 0.50f)]
    public float PeakBrightnessBoost;

    /// <summary>
    /// Amount by which fog distance contracts or expands with the rhythm. Zero means off and
    /// temporarily disables the response. This is a beat-feel tuning control; 3.0 was suggested,
    /// with 0.0 to 6.0 recommended.
    /// </summary>
    [Tooltip("How much fog distance contracts or expands with the rhythm. 0 disables it; recommended: 0.0 to 6.0.")]
    [Range(0f, 6.0f)]
    public float DynamicFogAmount;

    /// <summary>
    /// Low-band strength that arms the On Beat brightness pulse, read from BeatManager's
    /// smoothed levels.
    /// </summary>
    [Tooltip("Low-band strength (0-1) the smoothed lows must exceed for the On Beat brightness pulse to fire.")]
    [Range(0f, 1f)]
    public float OnBeatLowThreshold;

    /// <summary>
    /// Brightness multiplier boost applied to every traced wall while a quarter-beat On Beat
    /// gate is open and lows exceed the threshold. Zero disables the response.
    /// </summary>
    [Tooltip("How much wall brightness lifts while On Beat with strong lows. 0 disables it; recommended: 0.2 to 1.0.")]
    [Range(0f, 2f)]
    public float OnBeatBrightnessPulse;

    /// <summary>
    /// Flight speed while the phrase-scoped energy state reads Low. The energy-state speeds
    /// replace the Standalone speed roll in Synced Mode; the real-time Levels play no part.
    /// </summary>
    [Tooltip("Flight speed (voxels/sec) while the phrase-scoped energy state is Low.")]
    [Min(0f)]
    public float LowEnergySpeed;

    /// <summary>Flight speed while the phrase-scoped energy state reads Mid.</summary>
    [Tooltip("Flight speed (voxels/sec) while the phrase-scoped energy state is Mid.")]
    [Min(0f)]
    public float MidEnergySpeed;

    /// <summary>Flight speed while the phrase-scoped energy state reads High.</summary>
    [Tooltip("Flight speed (voxels/sec) while the phrase-scoped energy state is High.")]
    [Min(0f)]
    public float HighEnergySpeed;

    /// <summary>
    /// SmoothDamp time constant, in seconds, easing the flight speed between energy tier
    /// plateaus so a phrase change never jerks the flight.
    /// </summary>
    [Tooltip("Seconds of SmoothDamp easing between energy tier speeds. Higher = gentler ramps.")]
    [Range(0.1f, 5f)]
    public float SpeedSmoothTime;

    /// <summary>
    /// Ramp window, in beats, for the beat-locked speed glide toward the announced next energy
    /// state's tier speed.
    /// </summary>
    [Tooltip("Beats before an announced energy-state change over which speed glides to the next tier's speed.")]
    [Range(1, 32)]
    public int SpeedRampBeats;

    /// <summary>
    /// Wind-down, in beats, over which the flight falls to a dead stop; the stop completes
    /// <see cref="DropSitBeats"/> before the announced Drop landing.
    /// </summary>
    [Tooltip("Beats of wind-down to a dead stop, completing DropSitBeats before the Drop lands.")]
    [Range(1, 16)]
    public int DropStopBeats;

    /// <summary>Hold, in beats, that the flight sits parked at the dead stop before the Drop lands.</summary>
    [Tooltip("Beats the flight sits parked at the dead stop before the Drop lands. 0 stops exactly at the landing.")]
    [Range(0, 8)]
    public int DropSitBeats;

    /// <summary>
    /// Speed multiplier at the instant a Drop lands, decaying back to 1 across the 16-beat
    /// timing-grid cycle.
    /// </summary>
    [Tooltip("Speed multiplier at the Drop landing, decaying back to 1 over the 16-beat grid. 1 disables the launch.")]
    [Min(1f)]
    public float DropLaunchMultiplier;

    /// <summary>
    /// Strength of the Fill edge inversion: each eighth-note pulse pumps the voxel lattice from
    /// dark lines on lit faces toward glowing lines on darkened faces. Zero disables the response.
    /// </summary>
    [Tooltip("Eighth-note edge inversion strength during a synced Fill. 1 fully flips the lattice at each pulse peak; 0 disables.")]
    [Range(0f, 1f)]
    public float FillEdgeInversion;

    /// <summary>Brightness multiplier of the lattice lines at full Fill inversion; above 1 the wireframe glows.</summary>
    [Tooltip("Lattice-line brightness at full inversion. 2 overdrives the wireframe brighter than any normal face; 1 is a plain flip.")]
    [Min(1f)]
    public float FillLineGlow;

    /// <summary>
    /// Camera spin rate, in degrees per second, at the instant a Drop lands, settling with the
    /// launch across the 16-beat grid. Zero disables the spin.
    /// </summary>
    [Tooltip("Camera spin rate at the Drop landing, in degrees per second, settling with the launch over the 16-beat grid. 0 disables.")]
    [Min(0f)]
    public float DropSpinSpeed;

    /// <summary>Modulo selecting hit voxels for synced Fill and Drop recoloring.</summary>
    [Min(1)]
    public int EventCheckerModulo;

    /// <summary>Effect-time multiplier driving synced Fill and Drop recoloring pulses.</summary>
    [Min(0f)]
    public float EventPulseSpeed;

    /// <summary>Copies every MazeFlyer Sync Setting from another value.</summary>
    public void CopyFrom(MazeFlyerSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        PulseStrength = source.PulseStrength;
        PeakBrightnessBoost = source.PeakBrightnessBoost;
        DynamicFogAmount = source.DynamicFogAmount;
        OnBeatLowThreshold = source.OnBeatLowThreshold;
        OnBeatBrightnessPulse = source.OnBeatBrightnessPulse;
        LowEnergySpeed = source.LowEnergySpeed;
        MidEnergySpeed = source.MidEnergySpeed;
        HighEnergySpeed = source.HighEnergySpeed;
        SpeedSmoothTime = source.SpeedSmoothTime;
        SpeedRampBeats = source.SpeedRampBeats;
        DropStopBeats = source.DropStopBeats;
        DropSitBeats = source.DropSitBeats;
        DropLaunchMultiplier = source.DropLaunchMultiplier;
        DropSpinSpeed = source.DropSpinSpeed;
        FillEdgeInversion = source.FillEdgeInversion;
        FillLineGlow = source.FillLineGlow;
        EventCheckerModulo = source.EventCheckerModulo;
        EventPulseSpeed = source.EventPulseSpeed;
    }
}
