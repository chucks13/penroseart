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
[EffectStandaloneSettings(typeof(MazeFlyerStandaloneSettingsAsset))]
public class MazeFlyer : EffectBase
{
    // Standalone Defaults

    /// <summary>
    /// Authored inclusive minimum for the per-activation flight-speed roll, in voxel cells per
    /// second.
    /// </summary>
    private const float StandaloneFlightSpeedMin = 1.0f;

    /// <summary>
    /// Authored inclusive maximum for the per-activation flight-speed roll, in voxel cells per
    /// second. The continuous roll replaced the original integer roll whose top speed of 4 read
    /// as streaking: corridor walls pass ~0.5 units from the camera, so a wall feature crossed
    /// the whole ~75-degree view in ~0.16 s. Near 2.25 the fastest rolls stay brisk but readable.
    /// Governs the Standalone roll only; Synced Mode replaces the roll with the energy-state
    /// tier speeds in the Sync Settings.
    /// </summary>
    private const float StandaloneFlightSpeedMax = 2.25f;

    /// <summary>Authored multiplier deriving camera turn speed from the rolled flight speed.</summary>
    private const float StandaloneTurnSpeedMultiplier = 2.5f;

    /// <summary>
    /// Authored SmoothDamp time constant, in seconds, used when Standalone flight returns to its
    /// rolled speed after leaving Synced Mode.
    /// </summary>
    private const float StandaloneFlightSpeedSmoothTime = 2.0f;

    /// <summary>Authored probability that a non-guaranteed voxel cell is filled.</summary>
    private const float StandaloneRandomCellOccupancyProbability = 0.25f;

    /// <summary>Authored coordinate scale used to generate the Spatial Waves hue field.</summary>
    private const float StandaloneSpatialWavesHueScale = 0.15f;

    /// <summary>Authored voxel-block width used by the Block Regions color mode.</summary>
    private const int StandaloneBlockRegionsSize = 4;

    /// <summary>Authored saturation of voxels in the Pure Random color mode.</summary>
    private const float StandalonePureRandomSaturation = 0.9f;

    /// <summary>Authored value of voxels in the Pure Random color mode.</summary>
    private const float StandalonePureRandomValue = 0.95f;

    /// <summary>Authored saturation of voxels in the Spatial Waves color mode.</summary>
    private const float StandaloneSpatialWavesSaturation = 0.85f;

    /// <summary>Authored value of voxels in the Spatial Waves color mode.</summary>
    private const float StandaloneSpatialWavesValue = 0.95f;

    /// <summary>Authored minimum hue jitter applied within each Block Regions voxel.</summary>
    private const float StandaloneBlockRegionsHueJitterMin = -0.05f;

    /// <summary>Authored maximum hue jitter applied within each Block Regions voxel.</summary>
    private const float StandaloneBlockRegionsHueJitterMax = 0.05f;

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
    private const float StandaloneCameraFocalLength = 18.0f;

    /// <summary>Authored normalized move progress at which look-ahead turn blending begins.</summary>
    private const float StandaloneTurnBlendStart = 0.2f;

    /// <summary>
    /// Authored navigation threshold: rolls above this value continue ahead, while rolls at or below
    /// it enter the non-reversing direction choice when alternatives exist.
    /// </summary>
    private const float StandaloneForwardContinuationThreshold = 0.35f;

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
    /// is purely angular — distance falloff is the fog's job.
    /// </summary>
    private const float StandaloneHeadlightMinShade = 0.5f;

    /// <summary>
    /// Authored density of the squared-exponential fog curve over the normalized ray distance.
    /// The squared curve keeps near walls bright and crushes the sub-tile far field toward black,
    /// unlike the linear fade it replaced, which compressed midrange contrast.
    /// </summary>
    private const float StandaloneFogDensity = 2.5f;

    /// <summary>
    /// Authored on-wall thickness of voxel edge lines, in tile pitches. The band converts to
    /// voxel-space width per hit (thickness * distance / focal length), so lines render at
    /// constant thickness at every depth: near faces get crisp lines instead of swollen bands,
    /// and far faces keep a band wide enough to register on a point-sampled tile. Edge lines
    /// draw the voxel lattice onto wall faces so corridor geometry reads at the wall's 900-tile
    /// resolution.
    /// </summary>
    private const float StandaloneEdgeLineThicknessTiles = 0.7f;

    /// <summary>
    /// Authored shade multiplier at the very edge of a voxel face, ramping linearly back to 1
    /// across <see cref="StandaloneEdgeLineThicknessTiles"/>.
    /// </summary>
    private const float StandaloneEdgeLineShade = 0.1f;

    /// <summary>
    /// Authored scale of the rotated-grid supersample pattern, in tile-center units; 0.7 spans
    /// roughly one tile pitch, so the four rays cover the tile's footprint rather than its center
    /// point. Smaller sharpens toward point sampling; larger blurs across neighboring tiles.
    /// </summary>
    private const float StandaloneRaySampleSpread = 0.7f;

    /// <summary>
    /// Authored minimum HSV value for colors rolled from the shared palette. Palettes may carry
    /// dark entries, and face shading and fog only multiply downward from the rolled color, so a
    /// dark roll reads as an unlit wall. The floor lifts value while keeping the entry's hue and
    /// saturation.
    /// </summary>
    private const float StandaloneSharedPaletteMinValue = 0.8f;

    /// <summary>
    /// Authored brightness the darkest traced surface maps to, lifting the whole picture without
    /// touching the fog curve that produced the darkness. The traced brightness is remapped into
    /// the band from this floor up to 1 rather than clamped against it, so the dark field keeps
    /// its gradient instead of collapsing to one flat tone. Judged at the wall; 0 is the identity.
    /// </summary>
    private const float StandaloneMinBrightness = 0.4f;

    // Sync Defaults

    /// <summary>Authored inclusive minimum for the Synced per-activation flight-speed roll.</summary>
    private const float SyncFlightSpeedMin = 1.0f;

    /// <summary>Authored inclusive maximum for the Synced per-activation flight-speed roll.</summary>
    private const float SyncFlightSpeedMax = 2.25f;

    /// <summary>Authored Synced multiplier deriving camera turn speed from flight speed.</summary>
    private const float SyncTurnSpeedMultiplier = 2.5f;

    /// <summary>Authored Synced probability that a non-guaranteed voxel cell is occupied.</summary>
    private const float SyncRandomCellOccupancyProbability = 0.25f;

    /// <summary>Authored Synced coordinate scale used to generate the Spatial Waves hue field.</summary>
    private const float SyncSpatialWavesHueScale = 0.15f;

    /// <summary>Authored Synced voxel-block width used by the Block Regions color mode.</summary>
    private const int SyncBlockRegionsSize = 4;

    /// <summary>Authored Synced saturation of voxels in the Pure Random color mode.</summary>
    private const float SyncPureRandomSaturation = 0.9f;

    /// <summary>Authored Synced value of voxels in the Pure Random color mode.</summary>
    private const float SyncPureRandomValue = 0.95f;

    /// <summary>Authored Synced saturation of voxels in the Spatial Waves color mode.</summary>
    private const float SyncSpatialWavesSaturation = 0.85f;

    /// <summary>Authored Synced value of voxels in the Spatial Waves color mode.</summary>
    private const float SyncSpatialWavesValue = 0.95f;

    /// <summary>Authored Synced minimum hue jitter within each Block Regions voxel.</summary>
    private const float SyncBlockRegionsHueJitterMin = -0.05f;

    /// <summary>Authored Synced maximum hue jitter within each Block Regions voxel.</summary>
    private const float SyncBlockRegionsHueJitterMax = 0.05f;

    /// <summary>Authored Synced saturation of voxels in the Block Regions color mode.</summary>
    private const float SyncBlockRegionsSaturation = 0.88f;

    /// <summary>Authored Synced value of voxels in the Block Regions color mode.</summary>
    private const float SyncBlockRegionsValue = 0.95f;

    /// <summary>Authored Synced colors sampled by the Curated Palette mode.</summary>
    private static readonly Color[] SyncCuratedPalette =
    {
        Color.HSVToRGB(0.78f, 0.9f, 0.95f), // Purple/Magenta
        Color.HSVToRGB(0.55f, 0.9f, 0.95f), // Cyan/Blue
        Color.HSVToRGB(0.18f, 0.9f, 0.95f), // Yellow/Gold
        Color.HSVToRGB(0.38f, 0.9f, 0.95f)  // Lime Green
    };

    /// <summary>Authored Synced camera focal length used to project tile centers into voxel rays.</summary>
    private const float SyncCameraFocalLength = 18.0f;

    /// <summary>Authored Synced move progress where look-ahead turn blending begins.</summary>
    private const float SyncTurnBlendStart = 0.2f;

    /// <summary>Authored Synced threshold above which navigation continues ahead when possible.</summary>
    private const float SyncForwardContinuationThreshold = 0.35f;

    /// <summary>Authored Synced maximum ray distance and baseline fog range.</summary>
    private const float SyncMaxRayDistance = 14.0f;

    /// <summary>Authored Synced shade multiplier for voxel faces hit across the X axis.</summary>
    private const float SyncXAxisFaceShade = 0.75f;

    /// <summary>Authored Synced shade multiplier for voxel faces hit across the Y axis.</summary>
    private const float SyncYAxisFaceShade = 0.95f;

    /// <summary>Authored Synced shade multiplier for voxel faces hit across the Z axis.</summary>
    private const float SyncZAxisFaceShade = 0.60f;

    /// <summary>Authored Synced camera-headlight shade floor at full grazing incidence.</summary>
    private const float SyncHeadlightMinShade = 0.5f;

    /// <summary>Authored Synced density of the squared-exponential fog curve.</summary>
    private const float SyncFogDensity = 2.5f;

    /// <summary>Authored Synced on-wall thickness of voxel edge lines, in tile pitches.</summary>
    private const float SyncEdgeLineThicknessTiles = 0.7f;

    /// <summary>Authored Synced shade multiplier at the edge of a voxel face.</summary>
    private const float SyncEdgeLineShade = 0.1f;

    /// <summary>Authored Synced scale of the rotated-grid supersample pattern.</summary>
    private const float SyncRaySampleSpread = 0.7f;

    /// <summary>Authored Synced minimum HSV value for colors rolled from the shared palette.</summary>
    private const float SyncSharedPaletteMinValue = 0.8f;

    /// <summary>Authored Synced brightness the darkest traced surface maps to. Judged at the wall.</summary>
    private const float SyncMinBrightness = 0.4f;

    /// <summary>
    /// Authored low-band strength that arms the On Beat brightness pulse. Lows are read from
    /// BeatManager's smoothed levels (20 ms attack, 150 ms release), so the gate reacts to a
    /// bass hit within a frame or two without flickering at the threshold.
    /// </summary>
    private const float SyncOnBeatLowThreshold = 0.35f;

    /// <summary>
    /// Authored brightness multiplier boost applied to every traced wall while a quarter-beat
    /// On Beat gate is open and lows exceed <see cref="SyncOnBeatLowThreshold"/>. Multiplicative
    /// and applied before <see cref="SyncMinBrightness"/>, so it scales the picture rather than
    /// adding to it and the depth gradient survives the pulse. Zero disables the response.
    /// </summary>
    private const float SyncOnBeatBrightnessPulse = 0.75f;

    /// <summary>
    /// Authored flight speed while the phrase-scoped energy state reads Low. The energy-state
    /// speeds replace the Standalone speed roll in Synced Mode; the real-time Levels play no
    /// part in speed.
    /// </summary>
    private const float SyncLowEnergyFlightSpeed = 1.0f;

    /// <summary>
    /// Authored flight speed while the phrase-scoped energy state reads Mid.
    /// </summary>
    private const float SyncMidEnergyFlightSpeed = 1.6f;

    /// <summary>
    /// Authored flight speed while the phrase-scoped energy state reads High.
    /// </summary>
    private const float SyncHighEnergyFlightSpeed = 2.25f;

    /// <summary>
    /// Authored SmoothDamp time constant, in seconds, easing the flight speed between energy
    /// tier plateaus so a phrase change never jerks the flight.
    /// </summary>
    private const float SyncFlightSpeedSmoothTime = 2.0f;

    /// <summary>
    /// Authored ramp window, in beats, for the beat-locked speed glide. Once the announced next
    /// energy state is this close, the speed target slides from the current tier's speed toward
    /// the next tier's, arriving as the phrase boundary lands instead of trailing it.
    /// </summary>
    private const int SyncEnergyFlightSpeedRampBeats = 8;

    /// <summary>
    /// Authored wind-down, in beats, over which the flight falls to a dead stop ahead of an
    /// announced Drop landing. The stop completes <see cref="SyncDropSitBeats"/> before the
    /// landing, and the wind-down is intra-beat smooth via BeatManager's continuous Drop
    /// approach envelope.
    /// </summary>
    private const int SyncDropStopBeats = 4;

    /// <summary>
    /// Authored hold, in beats, that the flight sits parked at the dead stop before the Drop
    /// lands — the held breath between the wind-down and the launch.
    /// </summary>
    private const int SyncDropSitBeats = 2;

    /// <summary>
    /// Authored speed multiplier at the instant a Drop lands. The launch decays back to 1 across
    /// the phrase-relative 16-beat timing-grid cycle, so the drop hits at full boost and glides
    /// back to the energy-tier cruise over the full grid.
    /// </summary>
    private const float SyncDropLaunchMultiplier = 2.5f;

    /// <summary>
    /// Authored strength of the Fill edge inversion: each eighth-note pulse pumps the voxel
    /// lattice from dark lines on lit faces toward glowing lines on darkened faces while a
    /// synced Fill runs. One is a full flip at each pulse peak; zero disables the response.
    /// </summary>
    private const float SyncFillEdgeInversion = 1.0f;

    /// <summary>
    /// Authored brightness multiplier of the lattice lines at full Fill inversion. Above 1 the
    /// inverted wireframe overdrives brighter than any normal face, so the flip adds light to
    /// the wall instead of only removing it — a plain endpoint swap dims the whole picture and
    /// reads as a fade, not an event.
    /// </summary>
    private const float SyncFillLineGlow = 2.0f;

    /// <summary>
    /// Authored camera spin rate, in degrees per second, at the instant a Drop lands. The spin
    /// rides the launch envelope — fastest at the hit, settling with the speed across the
    /// 16-beat grid — and the look-ahead turning levels the camera naturally as it fades. It
    /// replaces the retired Fill camera roll, whose per-frame accumulation spun uncontrolled
    /// whenever the flight froze.
    /// </summary>
    private const float SyncDropCameraSpinSpeed = 90f;

    // Effect Settings resolution

    /// <summary>
    /// Resolves a fresh copy of MazeFlyer's Standalone Defaults. The curated palette is cloned
    /// per resolve, so no activation shares mutable state with the authored table.
    /// </summary>
    public static MazeFlyerStandaloneSettings StandaloneDefaults => new MazeFlyerStandaloneSettings
    {
        FlightSpeed = new FloatRange(StandaloneFlightSpeedMin, StandaloneFlightSpeedMax),
        TurnSpeedMultiplier = StandaloneTurnSpeedMultiplier,
        FlightSpeedSmoothTime = StandaloneFlightSpeedSmoothTime,
        RandomCellOccupancyProbability = StandaloneRandomCellOccupancyProbability,
        SpatialWavesHueScale = StandaloneSpatialWavesHueScale,
        BlockRegionsSize = StandaloneBlockRegionsSize,
        PureRandomSaturation = StandalonePureRandomSaturation,
        PureRandomValue = StandalonePureRandomValue,
        SpatialWavesSaturation = StandaloneSpatialWavesSaturation,
        SpatialWavesValue = StandaloneSpatialWavesValue,
        BlockRegionsHueJitter = new FloatRange(
            StandaloneBlockRegionsHueJitterMin,
            StandaloneBlockRegionsHueJitterMax),
        BlockRegionsSaturation = StandaloneBlockRegionsSaturation,
        BlockRegionsValue = StandaloneBlockRegionsValue,
        CuratedPalette = (Color[])StandaloneCuratedPalette.Clone(),
        CameraFocalLength = StandaloneCameraFocalLength,
        TurnBlendStart = StandaloneTurnBlendStart,
        ForwardContinuationThreshold = StandaloneForwardContinuationThreshold,
        MaxRayDistance = StandaloneMaxRayDistance,
        XAxisFaceShade = StandaloneXAxisFaceShade,
        YAxisFaceShade = StandaloneYAxisFaceShade,
        ZAxisFaceShade = StandaloneZAxisFaceShade,
        HeadlightMinShade = StandaloneHeadlightMinShade,
        FogDensity = StandaloneFogDensity,
        EdgeLineThicknessTiles = StandaloneEdgeLineThicknessTiles,
        EdgeLineShade = StandaloneEdgeLineShade,
        RaySampleSpread = StandaloneRaySampleSpread,
        SharedPaletteMinValue = StandaloneSharedPaletteMinValue,
        MinBrightness = StandaloneMinBrightness,
    };

    /// <summary>Resolves a fresh copy of MazeFlyer's file-local Sync Defaults.</summary>
    public static MazeFlyerSyncSettings SyncDefaults => new MazeFlyerSyncSettings
    {
        FlightSpeed = new FloatRange(SyncFlightSpeedMin, SyncFlightSpeedMax),
        TurnSpeedMultiplier = SyncTurnSpeedMultiplier,
        RandomCellOccupancyProbability = SyncRandomCellOccupancyProbability,
        SpatialWavesHueScale = SyncSpatialWavesHueScale,
        BlockRegionsSize = SyncBlockRegionsSize,
        PureRandomSaturation = SyncPureRandomSaturation,
        PureRandomValue = SyncPureRandomValue,
        SpatialWavesSaturation = SyncSpatialWavesSaturation,
        SpatialWavesValue = SyncSpatialWavesValue,
        BlockRegionsHueJitter = new FloatRange(
            SyncBlockRegionsHueJitterMin,
            SyncBlockRegionsHueJitterMax),
        BlockRegionsSaturation = SyncBlockRegionsSaturation,
        BlockRegionsValue = SyncBlockRegionsValue,
        CuratedPalette = (Color[])SyncCuratedPalette.Clone(),
        CameraFocalLength = SyncCameraFocalLength,
        TurnBlendStart = SyncTurnBlendStart,
        ForwardContinuationThreshold = SyncForwardContinuationThreshold,
        MaxRayDistance = SyncMaxRayDistance,
        XAxisFaceShade = SyncXAxisFaceShade,
        YAxisFaceShade = SyncYAxisFaceShade,
        ZAxisFaceShade = SyncZAxisFaceShade,
        HeadlightMinShade = SyncHeadlightMinShade,
        FogDensity = SyncFogDensity,
        EdgeLineThicknessTiles = SyncEdgeLineThicknessTiles,
        EdgeLineShade = SyncEdgeLineShade,
        RaySampleSpread = SyncRaySampleSpread,
        SharedPaletteMinValue = SyncSharedPaletteMinValue,
        MinBrightness = SyncMinBrightness,
        OnBeatLowThreshold = SyncOnBeatLowThreshold,
        OnBeatBrightnessPulse = SyncOnBeatBrightnessPulse,
        LowEnergyFlightSpeed = SyncLowEnergyFlightSpeed,
        MidEnergyFlightSpeed = SyncMidEnergyFlightSpeed,
        HighEnergyFlightSpeed = SyncHighEnergyFlightSpeed,
        FlightSpeedSmoothTime = SyncFlightSpeedSmoothTime,
        EnergyFlightSpeedRampBeats = SyncEnergyFlightSpeedRampBeats,
        DropStopBeats = SyncDropStopBeats,
        DropSitBeats = SyncDropSitBeats,
        DropLaunchMultiplier = SyncDropLaunchMultiplier,
        DropCameraSpinSpeed = SyncDropCameraSpinSpeed,
        FillEdgeInversion = SyncFillEdgeInversion,
        FillLineGlow = SyncFillLineGlow,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private MazeFlyerStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private MazeFlyerSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Selects one dual-homed Effect Setting from the surface active this frame.</summary>
    private T ActiveSetting<T>(T standaloneValue, T syncValue) =>
        beatManager.IsSynced ? syncValue : standaloneValue;

    // Effect contract

    /// <summary>The musical capabilities and energy range advertised by MazeFlyer.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop
        | Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;

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

        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(MazeFlyer),
            StandaloneDefaults);
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

        // MazeFlyer carries its rhythm in Fill, Drop, Energy, and the On Beat low pulse, so it
        // suppresses the Waveform response outright rather than holding a Pool value it never reads.
        waveform = waveforms.None;

        FloatRange flightSpeedRange = ActiveSetting(
            standaloneSettings.FlightSpeed,
            SyncSettings.FlightSpeed);
        rolledFlySpeed = Random.Range(flightSpeedRange.Min, flightSpeedRange.Max);

        // An activation is already a visual cut, so the flight starts directly at its target —
        // the energy tier when synced, the roll otherwise — with no ramp from a stale speed.
        flySpeed = TargetFlySpeed();
        flySpeedVelocity = 0f;
        turnSpeed = flySpeed * ActiveSetting(
            standaloneSettings.TurnSpeedMultiplier,
            SyncSettings.TurnSpeedMultiplier);
        dropLaunchArmed = false;
        wasDropActive = false;
        previousGridBeat = null;
        dropLaunchStrength = 0f;

        // The enum has five members and GetVoxelColor handles all five, so [0, 5) is the complete
        // selector domain rather than an authored subrange.
        activeColorMode = (ColorMode)Random.Range(0, 5);

        GenerateVoxelGrid();
        InitializeCameraPosition();

        // Tile centers are copied out of the tile metadata objects into one contiguous array so
        // the per-tile loop reads sequential memory instead of chasing 900 heap references every
        // frame. Only invariant geometry is cached; live settings stay at their frame consumers.
        if (tileCenters == null)
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
    /// <see cref="RaySampleOffsets"/> scaled by the live sample spread once per frame. The array
    /// is allocated with the Effect so rebuilding it in <see cref="Draw"/> creates no garbage.
    /// </summary>
    private readonly Vector2[] scaledSampleOffsets = new Vector2[RaySampleOffsets.Length];

    /// <summary>Equal contribution of each rotated-grid ray to its tile's final color.</summary>
    private static readonly float RaySampleWeight = 1.0f / RaySampleOffsets.Length;

    /// <summary>
    /// Tile centers copied from <see cref="EffectBase.tiles"/> into one contiguous array on first
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
        /// <summary>Maximum ray distance for the frame.</summary>
        public float MaxRayDistance;

        /// <summary>Density of the squared-exponential fog curve.</summary>
        public float FogDensity;

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

        /// <summary>Brightness the darkest traced surface maps to, for the frame.</summary>
        public float MinBrightness;

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

        bool isBeatSynced = beatManager.IsSynced;

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

        // Everything invariant across the frame's rays is computed exactly once here, and the
        // camera basis is pre-scaled so each ray assembles its direction from component math alone.
        float focalLength = ActiveSetting(
            standaloneSettings.CameraFocalLength,
            SyncSettings.CameraFocalLength);
        TraceFrame frame = new TraceFrame
        {
            MaxRayDistance = ActiveSetting(
                standaloneSettings.MaxRayDistance,
                SyncSettings.MaxRayDistance),
            FogDensity = ActiveSetting(standaloneSettings.FogDensity, SyncSettings.FogDensity),
            BrightnessPulse = brightnessPulse,
            EdgeInversion = edgeInversion,
            XAxisFaceShade = ActiveSetting(standaloneSettings.XAxisFaceShade, SyncSettings.XAxisFaceShade),
            YAxisFaceShade = ActiveSetting(standaloneSettings.YAxisFaceShade, SyncSettings.YAxisFaceShade),
            ZAxisFaceShade = ActiveSetting(standaloneSettings.ZAxisFaceShade, SyncSettings.ZAxisFaceShade),
            HeadlightMinShade = ActiveSetting(standaloneSettings.HeadlightMinShade, SyncSettings.HeadlightMinShade),
            MinBrightness = ActiveSetting(standaloneSettings.MinBrightness, SyncSettings.MinBrightness),
            EdgeBandScale = ActiveSetting(
                standaloneSettings.EdgeLineThicknessTiles,
                SyncSettings.EdgeLineThicknessTiles) / focalLength,
            EdgeLineShade = ActiveSetting(standaloneSettings.EdgeLineShade, SyncSettings.EdgeLineShade),
            FillLineGlow = SyncSettings.FillLineGlow,
        };

        Vector3 forwardScaled = cameraRot * Vector3.forward * focalLength;
        Vector3 cameraRight = cameraRot * Vector3.right;
        Vector3 cameraUp = cameraRot * Vector3.up;

        float raySampleSpread = ActiveSetting(
            standaloneSettings.RaySampleSpread,
            SyncSettings.RaySampleSpread);
        for (int i = 0; i < RaySampleOffsets.Length; i++)
        {
            scaledSampleOffsets[i] = RaySampleOffsets[i] * raySampleSpread;
        }

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

            buffer[i] = new Color(
                r * RaySampleWeight,
                g * RaySampleWeight,
                b * RaySampleWeight,
                1.0f);
        }
    }

    // Maze generation

    /// <summary>Populates the voxel grid, coloring filled cells by the rolled color mode.</summary>
    private void GenerateVoxelGrid()
    {
        float occupancyProbability = ActiveSetting(
            standaloneSettings.RandomCellOccupancyProbability,
            SyncSettings.RandomCellOccupancyProbability);

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

                    // Random.value spans the complete probability domain; the occupancy probability authors its
                    // threshold. Guaranteed even-lattice cells short-circuit before that roll, so they
                    // consume no Random.value and retain the original mode-specific roll order.
                    int index = VoxelIndex(x, y, z);
                    if (onEvenLattice || Random.value < occupancyProbability)
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
                float spatialScale = ActiveSetting(
                    standaloneSettings.SpatialWavesHueScale,
                    SyncSettings.SpatialWavesHueScale);
                float waveHue = (Mathf.Sin(x * spatialScale) + Mathf.Cos(y * spatialScale) + Mathf.Sin(z * spatialScale) + 3f) / 6f;
                return Color.HSVToRGB(
                    waveHue,
                    ActiveSetting(standaloneSettings.SpatialWavesSaturation, SyncSettings.SpatialWavesSaturation),
                    ActiveSetting(standaloneSettings.SpatialWavesValue, SyncSettings.SpatialWavesValue));

            case ColorMode.BlockRegions:
                int blockSize = ActiveSetting(
                    standaloneSettings.BlockRegionsSize,
                    SyncSettings.BlockRegionsSize);
                FloatRange hueJitter = ActiveSetting(
                    standaloneSettings.BlockRegionsHueJitter,
                    SyncSettings.BlockRegionsHueJitter);
                int blockX = x / blockSize;
                int blockY = y / blockSize;
                int blockZ = z / blockSize;

                // Prime multipliers hash the block coordinates into a stable per-block base hue.
                int blockHash = blockX * 73 + blockY * 179 + blockZ * 283;

                float baseHue = (Mathf.Abs(blockHash) % 100) / 100.0f;
                float blockHue = (
                    baseHue +
                    Random.Range(
                        hueJitter.Min,
                        hueJitter.Max) +
                    1.0f) % 1.0f;
                return Color.HSVToRGB(
                    blockHue,
                    ActiveSetting(standaloneSettings.BlockRegionsSaturation, SyncSettings.BlockRegionsSaturation),
                    ActiveSetting(standaloneSettings.BlockRegionsValue, SyncSettings.BlockRegionsValue));

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
                    ActiveSetting(standaloneSettings.PureRandomSaturation, SyncSettings.PureRandomSaturation),
                    ActiveSetting(standaloneSettings.PureRandomValue, SyncSettings.PureRandomValue));

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
                    Mathf.Max(value, ActiveSetting(
                        standaloneSettings.SharedPaletteMinValue,
                        SyncSettings.SharedPaletteMinValue)));
            }

            default:
                // The inline selector spans every entry in the complete authored palette table.
                Color[] curatedPalette = ActiveSetting(
                    standaloneSettings.CuratedPalette,
                    SyncSettings.CuratedPalette);
                return curatedPalette[Random.Range(0, curatedPalette.Length)];
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
            flySpeed,
            TargetFlySpeed(),
            ref flySpeedVelocity,
            ActiveSetting(
                standaloneSettings.FlightSpeedSmoothTime,
                SyncSettings.FlightSpeedSmoothTime),
            float.PositiveInfinity,
            deltaTime);

        // The Drop response multiplies the smoothed speed rather than the SmoothDamp target:
        // its envelopes are already continuous and beat-locked, and the one discontinuity —
        // zero to full launch at the landing — is the hit itself, which the damp would blunt
        // into a slow swell. Turn agility follows the same effective speed, so a full stop
        // also freezes the camera's turning.
        float effectiveFlySpeed = flySpeed * DropSpeedFactor();
        turnSpeed = effectiveFlySpeed * ActiveSetting(
            standaloneSettings.TurnSpeedMultiplier,
            SyncSettings.TurnSpeedMultiplier);

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
            Mathf.SmoothStep(
                ActiveSetting(standaloneSettings.TurnBlendStart, SyncSettings.TurnBlendStart),
                1.0f,
                moveProgress)).normalized;

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
                      SyncSettings.DropCameraSpinSpeed * dropLaunchStrength * deltaTime,
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
            && beatsUntil <= SyncSettings.EnergyFlightSpeedRampBeats)
        {
            float rampProgress = 1.0f - ((float)beatsUntil / SyncSettings.EnergyFlightSpeedRampBeats);
            return Mathf.Lerp(tierSpeed, TierSpeed(nextState), rampProgress);
        }

        return tierSpeed;
    }

    /// <summary>Returns the authored flight speed for one phrase-scoped energy tier.</summary>
    private float TierSpeed(Energy energyState) => energyState switch
    {
        Energy.Low => SyncSettings.LowEnergyFlightSpeed,
        Energy.Mid => SyncSettings.MidEnergyFlightSpeed,
        _ => SyncSettings.HighEnergyFlightSpeed,
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
        int? gridBeat = beatManager.Grid.Beat;
        bool justLanded = dropActive && !wasDropActive;
        wasDropActive = dropActive;
        if (justLanded)
        {
            dropLaunchArmed = true;
        }
        else if (dropLaunchArmed
            && previousGridBeat is { } previous
            && gridBeat is { } current
            && current < previous)
        {
            dropLaunchArmed = false;
        }
        previousGridBeat = gridBeat;

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

        // Random.value spans the complete probability domain; ForwardContinuationThreshold authors its
        // split. The short-circuit is load-bearing: the roll is consumed only when continuing ahead
        // is possible, so restructuring this condition would change the Standalone look.
        if (canContinueAhead &&
            (Random.value > ActiveSetting(
                standaloneSettings.ForwardContinuationThreshold,
                SyncSettings.ForwardContinuationThreshold) || openCount == 1))
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
    /// Executes 3D DDA voxel ray stepping, returning the color of
    /// the first filled voxel — face-shaded, headlight-dimmed by grazing incidence, edge-lined at
    /// the frame's Fill inversion weight, squared-exponentially fogged, and scaled by the frame's
    /// On Beat brightness pulse — or black past the fog range. Runs 3,600 times per frame; every
    /// frame-invariant input arrives precomputed in the <see cref="TraceFrame"/>.
    /// </summary>
    private Color TraceVoxelRay(Vector3 rayOrigin, Vector3 rayDir, in TraceFrame frame)
    {
        float rx = ClampAwayFromZero(rayDir.x);
        float ry = ClampAwayFromZero(rayDir.y);
        float rz = ClampAwayFromZero(rayDir.z);

        int mapX = FloorToInt(rayOrigin.x);
        int mapY = FloorToInt(rayOrigin.y);
        int mapZ = FloorToInt(rayOrigin.z);

        int stepX = rx > 0 ? 1 : -1;
        int stepY = ry > 0 ? 1 : -1;
        int stepZ = rz > 0 ? 1 : -1;

        float deltaDistX = Mathf.Abs(1.0f / rx);
        float deltaDistY = Mathf.Abs(1.0f / ry);
        float deltaDistZ = Mathf.Abs(1.0f / rz);

        float sideDistX = (stepX > 0) ? (mapX + 1.0f - rayOrigin.x) * deltaDistX : (rayOrigin.x - mapX) * deltaDistX;
        float sideDistY = (stepY > 0) ? (mapY + 1.0f - rayOrigin.y) * deltaDistY : (rayOrigin.y - mapY) * deltaDistY;
        float sideDistZ = (stepZ > 0) ? (mapZ + 1.0f - rayOrigin.z) * deltaDistZ : (rayOrigin.z - mapZ) * deltaDistZ;

        float distanceTraveled = 0f;

        // The camera moves only through empty cells, so the first DDA step replaces this initial
        // value before a filled cell can be hit.
        Axis hitAxis = Axis.X;

        while (distanceTraveled < frame.MaxRayDistance)
        {
            // The occupancy test is the innermost operation of the whole effect: one masked
            // flat index into the compact occupancy array. Colors load only on a hit.
            int index = VoxelIndex(mapX, mapY, mapZ);

            if (voxelSolid[index])
            {
                Color voxelColor = voxelColors[index];

                // Resolve the base shade from the axis of the face the ray entered.
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
                float shade = baseShade * headlight;

                // Squared-exponential fog over the normalized ray distance: near walls stay
                // bright, the sub-tile far field crushes toward black. At the range cutoff the
                // curve is already near zero, so the black past-range return stays seamless.
                float normalizedDistance = distanceTraveled / frame.MaxRayDistance;
                float fogExponent = frame.FogDensity * normalizedDistance;
                float fog = MathF.Exp(-fogExponent * fogExponent);

                float edge = EdgeLineFactor(
                    rayOrigin + (rayDir * distanceTraveled), hitAxis, distanceTraveled, in frame);
                // Remap the traced brightness into the band from the authored floor up to 1, so a
                // lift of the dark end keeps the fog's gradient rather than clipping it flat. The
                // unclamped Lerp preserves the above-1 headroom the On Beat pulse writes, leaving
                // that response exactly as loud as it was before the floor existed.
                float brightness = Mathf.LerpUnclamped(
                    frame.MinBrightness,
                    1.0f,
                    shade * fog * edge * frame.BrightnessPulse);

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

/// <summary>The serializable value shape shared by MazeFlyer's Standalone Defaults and saved Settings.</summary>
[Serializable]
public sealed class MazeFlyerStandaloneSettings
{
    /// <summary>Per-activation flight-speed range, in voxel cells per second.</summary>
    public FloatRange FlightSpeed;

    /// <summary>Multiplier deriving camera turn speed from the rolled flight speed.</summary>
    public float TurnSpeedMultiplier;

    /// <summary>SmoothDamp time constant, in seconds, used to return to the rolled flight speed.</summary>
    public float FlightSpeedSmoothTime;

    /// <summary>Probability that a non-guaranteed voxel cell is occupied.</summary>
    public float RandomCellOccupancyProbability;

    /// <summary>Coordinate scale used to generate the Spatial Waves hue field.</summary>
    public float SpatialWavesHueScale;

    /// <summary>Voxel-block width used by the Block Regions color mode.</summary>
    public int BlockRegionsSize;

    /// <summary>Saturation of voxels in the Pure Random color mode.</summary>
    public float PureRandomSaturation;

    /// <summary>Value of voxels in the Pure Random color mode.</summary>
    public float PureRandomValue;

    /// <summary>Saturation of voxels in the Spatial Waves color mode.</summary>
    public float SpatialWavesSaturation;

    /// <summary>Value of voxels in the Spatial Waves color mode.</summary>
    public float SpatialWavesValue;

    /// <summary>Per-voxel hue-jitter range used by the Block Regions color mode.</summary>
    public FloatRange BlockRegionsHueJitter;

    /// <summary>Saturation of voxels in the Block Regions color mode.</summary>
    public float BlockRegionsSaturation;

    /// <summary>Value of voxels in the Block Regions color mode.</summary>
    public float BlockRegionsValue;

    /// <summary>Colors sampled by the Curated Palette mode.</summary>
    public Color[] CuratedPalette;

    /// <summary>Camera focal length used to project tile centers into voxel rays.</summary>
    public float CameraFocalLength;

    /// <summary>Normalized move progress at which look-ahead turn blending begins.</summary>
    public float TurnBlendStart;

    /// <summary>Threshold separating forward continuation from alternate non-reversing direction selection.</summary>
    public float ForwardContinuationThreshold;

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
    public float RaySampleSpread;

    /// <summary>Minimum HSV value for colors rolled from the shared palette.</summary>
    public float SharedPaletteMinValue;

    /// <summary>Minimum traced-surface brightness; zero leaves the rendering unchanged.</summary>
    [Tooltip("Brightness the darkest traced surface maps to. Lifts the whole effect out of the dark without touching the fog. 0 disables it.")]
    [Range(0f, 1f)]
    public float MinBrightness;

    /// <summary>
    /// Copies every MazeFlyer Standalone Setting, cloning mutable ranges and palette storage so
    /// saved assets cannot mutate the in-file Standalone Defaults.
    /// </summary>
    public void CopyFrom(MazeFlyerStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        FlightSpeed = new FloatRange(
            source.FlightSpeed.Min,
            source.FlightSpeed.Max,
            source.FlightSpeed.LowRail,
            source.FlightSpeed.HighRail);
        TurnSpeedMultiplier = source.TurnSpeedMultiplier;
        FlightSpeedSmoothTime = source.FlightSpeedSmoothTime;
        RandomCellOccupancyProbability = source.RandomCellOccupancyProbability;
        SpatialWavesHueScale = source.SpatialWavesHueScale;
        BlockRegionsSize = source.BlockRegionsSize;
        PureRandomSaturation = source.PureRandomSaturation;
        PureRandomValue = source.PureRandomValue;
        SpatialWavesSaturation = source.SpatialWavesSaturation;
        SpatialWavesValue = source.SpatialWavesValue;
        BlockRegionsHueJitter = new FloatRange(
            source.BlockRegionsHueJitter.Min,
            source.BlockRegionsHueJitter.Max,
            source.BlockRegionsHueJitter.LowRail,
            source.BlockRegionsHueJitter.HighRail);
        BlockRegionsSaturation = source.BlockRegionsSaturation;
        BlockRegionsValue = source.BlockRegionsValue;
        CuratedPalette = (Color[])source.CuratedPalette.Clone();
        CameraFocalLength = source.CameraFocalLength;
        TurnBlendStart = source.TurnBlendStart;
        ForwardContinuationThreshold = source.ForwardContinuationThreshold;
        MaxRayDistance = source.MaxRayDistance;
        XAxisFaceShade = source.XAxisFaceShade;
        YAxisFaceShade = source.YAxisFaceShade;
        ZAxisFaceShade = source.ZAxisFaceShade;
        HeadlightMinShade = source.HeadlightMinShade;
        FogDensity = source.FogDensity;
        EdgeLineThicknessTiles = source.EdgeLineThicknessTiles;
        EdgeLineShade = source.EdgeLineShade;
        RaySampleSpread = source.RaySampleSpread;
        SharedPaletteMinValue = source.SharedPaletteMinValue;
        MinBrightness = source.MinBrightness;
    }
}

/// <summary>The saved musical-response settings used by MazeFlyer in Synced Mode.</summary>
[Serializable]
public sealed class MazeFlyerSyncSettings
{
    /// <summary>Per-activation fallback flight-speed range, in voxel cells per second.</summary>
    public FloatRange FlightSpeed;

    /// <summary>Multiplier deriving camera turn speed from the active flight speed.</summary>
    public float TurnSpeedMultiplier;

    /// <summary>Probability that a non-guaranteed voxel cell is occupied.</summary>
    public float RandomCellOccupancyProbability;

    /// <summary>Coordinate scale used to generate the Spatial Waves hue field.</summary>
    public float SpatialWavesHueScale;

    /// <summary>Voxel-block width used by the Block Regions color mode.</summary>
    public int BlockRegionsSize;

    /// <summary>Saturation of voxels in the Pure Random color mode.</summary>
    public float PureRandomSaturation;

    /// <summary>Value of voxels in the Pure Random color mode.</summary>
    public float PureRandomValue;

    /// <summary>Saturation of voxels in the Spatial Waves color mode.</summary>
    public float SpatialWavesSaturation;

    /// <summary>Value of voxels in the Spatial Waves color mode.</summary>
    public float SpatialWavesValue;

    /// <summary>Per-voxel hue-jitter range used by the Block Regions color mode.</summary>
    public FloatRange BlockRegionsHueJitter;

    /// <summary>Saturation of voxels in the Block Regions color mode.</summary>
    public float BlockRegionsSaturation;

    /// <summary>Value of voxels in the Block Regions color mode.</summary>
    public float BlockRegionsValue;

    /// <summary>Colors sampled by the Curated Palette mode.</summary>
    public Color[] CuratedPalette;

    /// <summary>Camera focal length used to project tile centers into voxel rays.</summary>
    public float CameraFocalLength;

    /// <summary>Normalized move progress at which look-ahead turn blending begins.</summary>
    public float TurnBlendStart;

    /// <summary>Threshold above which navigation continues forward when that direction is open.</summary>
    public float ForwardContinuationThreshold;

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

    /// <summary>Density of the squared-exponential fog curve over normalized ray distance.</summary>
    public float FogDensity;

    /// <summary>On-wall thickness of voxel edge lines, in tile pitches.</summary>
    public float EdgeLineThicknessTiles;

    /// <summary>Shade multiplier at the edge of a voxel face.</summary>
    public float EdgeLineShade;

    /// <summary>Scale of the rotated-grid supersample pattern, in tile-center units.</summary>
    public float RaySampleSpread;

    /// <summary>Minimum HSV value for colors rolled from the shared palette.</summary>
    public float SharedPaletteMinValue;

    /// <summary>Minimum traced-surface brightness; zero leaves the rendering unchanged.</summary>
    [Tooltip("Brightness the darkest traced surface maps to. Lifts the whole effect out of the dark without touching the fog. 0 disables it.")]
    [Range(0f, 1f)]
    public float MinBrightness;

    /// <summary>
    /// Low-band strength that arms the On Beat brightness pulse, read from BeatManager's
    /// smoothed levels.
    /// </summary>
    [Header("Audio Reactivity Settings")]
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

    /// <summary>Flight speed while the phrase-scoped Energy state reads Low.</summary>
    [Tooltip("Flight speed (voxels/sec) while the phrase-scoped energy state is Low.")]
    [Min(0f)]
    public float LowEnergyFlightSpeed;

    /// <summary>Flight speed while the phrase-scoped energy state reads Mid.</summary>
    [Tooltip("Flight speed (voxels/sec) while the phrase-scoped energy state is Mid.")]
    [Min(0f)]
    public float MidEnergyFlightSpeed;

    /// <summary>Flight speed while the phrase-scoped energy state reads High.</summary>
    [Tooltip("Flight speed (voxels/sec) while the phrase-scoped energy state is High.")]
    [Min(0f)]
    public float HighEnergyFlightSpeed;

    /// <summary>SmoothDamp time constant, in seconds, for flight-speed changes between Energy tiers.</summary>
    [Tooltip("Seconds of SmoothDamp easing between energy tier speeds. Higher = gentler ramps.")]
    [Range(0.1f, 5f)]
    public float FlightSpeedSmoothTime;

    /// <summary>Ramp window, in beats, for the glide toward the announced next Energy tier.</summary>
    [Tooltip("Beats before an announced energy-state change over which speed glides to the next tier's speed.")]
    [Range(1, 32)]
    public int EnergyFlightSpeedRampBeats;

    /// <summary>Wind-down duration, in beats, before the pre-Drop sit.</summary>
    [Tooltip("Beats of wind-down to a dead stop, completing DropSitBeats before the Drop lands.")]
    [Range(1, 16)]
    public int DropStopBeats;

    /// <summary>Hold, in beats, that the flight sits parked at the dead stop before the Drop lands.</summary>
    [Tooltip("Beats the flight sits parked at the dead stop before the Drop lands. 0 stops exactly at the landing.")]
    [Range(0, 8)]
    public int DropSitBeats;

    /// <summary>Flight-speed multiplier at the Drop landing; one disables the launch.</summary>
    [Tooltip("Speed multiplier at the Drop landing, decaying back to 1 over the 16-beat grid. 1 disables the launch.")]
    [Min(1f)]
    public float DropLaunchMultiplier;

    /// <summary>Strength of the eighth-note Fill edge inversion; zero disables the response.</summary>
    [Tooltip("Eighth-note edge inversion strength during a synced Fill. 1 fully flips the lattice at each pulse peak; 0 disables.")]
    [Range(0f, 1f)]
    public float FillEdgeInversion;

    /// <summary>Brightness multiplier of the lattice lines at full Fill inversion; above 1 the wireframe glows.</summary>
    [Tooltip("Lattice-line brightness at full inversion. 2 overdrives the wireframe brighter than any normal face; 1 is a plain flip.")]
    [Min(1f)]
    public float FillLineGlow;

    /// <summary>Camera spin rate at the Drop landing, in degrees per second; zero disables the spin.</summary>
    [Tooltip("Camera spin rate at the Drop landing, in degrees per second, settling with the launch over the 16-beat grid. 0 disables.")]
    [Min(0f)]
    public float DropCameraSpinSpeed;

    /// <summary>Copies every MazeFlyer Sync Setting from another value.</summary>
    public void CopyFrom(MazeFlyerSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        FlightSpeed = new FloatRange(
            source.FlightSpeed.Min,
            source.FlightSpeed.Max,
            source.FlightSpeed.LowRail,
            source.FlightSpeed.HighRail);
        TurnSpeedMultiplier = source.TurnSpeedMultiplier;
        RandomCellOccupancyProbability = source.RandomCellOccupancyProbability;
        SpatialWavesHueScale = source.SpatialWavesHueScale;
        BlockRegionsSize = source.BlockRegionsSize;
        PureRandomSaturation = source.PureRandomSaturation;
        PureRandomValue = source.PureRandomValue;
        SpatialWavesSaturation = source.SpatialWavesSaturation;
        SpatialWavesValue = source.SpatialWavesValue;
        BlockRegionsHueJitter = new FloatRange(
            source.BlockRegionsHueJitter.Min,
            source.BlockRegionsHueJitter.Max,
            source.BlockRegionsHueJitter.LowRail,
            source.BlockRegionsHueJitter.HighRail);
        BlockRegionsSaturation = source.BlockRegionsSaturation;
        BlockRegionsValue = source.BlockRegionsValue;
        CuratedPalette = (Color[])source.CuratedPalette.Clone();
        CameraFocalLength = source.CameraFocalLength;
        TurnBlendStart = source.TurnBlendStart;
        ForwardContinuationThreshold = source.ForwardContinuationThreshold;
        MaxRayDistance = source.MaxRayDistance;
        XAxisFaceShade = source.XAxisFaceShade;
        YAxisFaceShade = source.YAxisFaceShade;
        ZAxisFaceShade = source.ZAxisFaceShade;
        HeadlightMinShade = source.HeadlightMinShade;
        FogDensity = source.FogDensity;
        EdgeLineThicknessTiles = source.EdgeLineThicknessTiles;
        EdgeLineShade = source.EdgeLineShade;
        RaySampleSpread = source.RaySampleSpread;
        SharedPaletteMinValue = source.SharedPaletteMinValue;
        MinBrightness = source.MinBrightness;
        OnBeatLowThreshold = source.OnBeatLowThreshold;
        OnBeatBrightnessPulse = source.OnBeatBrightnessPulse;
        LowEnergyFlightSpeed = source.LowEnergyFlightSpeed;
        MidEnergyFlightSpeed = source.MidEnergyFlightSpeed;
        HighEnergyFlightSpeed = source.HighEnergyFlightSpeed;
        FlightSpeedSmoothTime = source.FlightSpeedSmoothTime;
        EnergyFlightSpeedRampBeats = source.EnergyFlightSpeedRampBeats;
        DropStopBeats = source.DropStopBeats;
        DropSitBeats = source.DropSitBeats;
        DropLaunchMultiplier = source.DropLaunchMultiplier;
        DropCameraSpinSpeed = source.DropCameraSpinSpeed;
        FillEdgeInversion = source.FillEdgeInversion;
        FillLineGlow = source.FillLineGlow;
    }
}
