using System;
using System.Collections.Generic;
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
    /// Governs the Standalone roll only; synced energy-driven speed is planned separately.
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
    /// Authored smoothing rate that returns camera roll to zero when no live clock is placing Fills —
    /// reachable while a residual roll relaxes after the wire drops out.
    /// </summary>
    private const float StandaloneCameraRollReturnSpeed = 3.0f;

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

    /// <summary>Authored synced Fill assignment for the dynamic camera-roll response.</summary>
    /// <remarks>
    /// The call-site note described directly adding degrees per frame to currentRollAngle, while the
    /// existing path assigns 5f directly. Its unresolved authored alternatives were 45f, incrementing
    /// by 2f, and a continuous 30.0f * localDelta spin. The tuning note said to modify 30.0f to change
    /// the continuous spin speed, and the section described the response as continuous with smoothing.
    /// This capture preserves that intent trail without choosing among the alternatives.
    /// </remarks>
    private const float SyncFillCameraRollAngle = 5f;

    /// <summary>Authored smoothing rate that returns camera roll to zero between synced Fills.</summary>
    private const float SyncCameraRollReturnSpeed = 3.0f;

    /// <summary>Authored modulo selecting one quarter of hit voxels for synced Fill and Drop recoloring.</summary>
    private const int SyncEventCheckerModulo = 4;

    /// <summary>Authored effect-time multiplier driving synced Fill and Drop recoloring pulses.</summary>
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
        CameraRollReturnSpeed = StandaloneCameraRollReturnSpeed,
        DirectionChoiceThreshold = StandaloneDirectionChoiceThreshold,
        MaxRayDistance = StandaloneMaxRayDistance,
        XAxisFaceShade = StandaloneXAxisFaceShade,
        YAxisFaceShade = StandaloneYAxisFaceShade,
        ZAxisFaceShade = StandaloneZAxisFaceShade,
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
        FillCameraRollAngle = SyncFillCameraRollAngle,
        CameraRollReturnSpeed = SyncCameraRollReturnSpeed,
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

    /// <summary>Fixed width, height, and depth of the cubic voxel-grid algorithm.</summary>
    private const int VoxelGridSize = 16;

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

    /// <summary>Voxel colors of the maze. Color.clear (alpha 0) marks an empty voxel.</summary>
    private Color[,,] voxelGrid = new Color[VoxelGridSize, VoxelGridSize, VoxelGridSize];

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

    /// <summary>The current flight speed rolled on activation.</summary>
    private float flySpeed;

    /// <summary>The current camera-turn speed derived from <see cref="flySpeed"/>.</summary>
    private float turnSpeed;

    /// <summary>The current camera-roll angle, discarded to zero by each activation's Roll.</summary>
    private float currentRollAngle;

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
        // progress, or camera-roll angle survives from the previous activation.
        currentRollAngle = 0.0f;
        cameraRot = Quaternion.identity;
        targetRot = Quaternion.identity;
        moveDir = Vector3Int.forward;
        nextMoveDir = Vector3Int.forward;
        moveProgress = 0.0f;

        // Unfiltered acquisition spans the complete curated Waveform Pool, so MazeFlyer has no
        // authored Waveform-selection subrange to expose as Effect Settings.
        waveform = waveforms.Random();

        float overallSpeed = Random.Range(
            standaloneSettings.OverallSpeedMin,
            standaloneSettings.OverallSpeedMax);
        flySpeed = overallSpeed;
        turnSpeed = overallSpeed * standaloneSettings.TurnSpeedMultiplier;

        // The enum has five members and GetVoxelColor handles all five, so [0, 5) is the complete
        // selector domain rather than an authored subrange.
        activeColorMode = (ColorMode)Random.Range(0, 5);

        GenerateVoxelGrid();
        InitializeCameraPosition();
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

        Vector3 cameraForward = cameraRot * Vector3.forward;
        Vector3 cameraRight = cameraRot * Vector3.right;
        Vector3 cameraUp = cameraRot * Vector3.up;

        float focalLength = standaloneSettings.FocalLength;
        float sampleSpread = standaloneSettings.SampleSpread;
        float sampleWeight = 1.0f / RaySampleOffsets.Length;

        for (int i = 0; i < buffer.Length; i++)
        {
            float px = tiles[i].center.x;
            float py = tiles[i].center.y;

            float r = 0f, g = 0f, b = 0f;
            foreach (Vector2 offset in RaySampleOffsets)
            {
                float sx = px + (offset.x * sampleSpread);
                float sy = py + (offset.y * sampleSpread);

                Vector3 rayDir = (cameraForward * focalLength + cameraRight * sx + cameraUp * sy).normalized;
                Color sample = TraceVoxelRay(cameraPos, rayDir, rhythm, isBeatSynced);
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
                    if (onEvenLattice || Random.value < standaloneSettings.FillProbability)
                    {
                        voxelGrid[x, y, z] = GetVoxelColor(x, y, z);
                    }
                    else
                    {
                        voxelGrid[x, y, z] = Color.clear;
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
    /// Advances on the effect clock's <see cref="EffectBase.effectDelta"/> so flight, turning,
    /// and roll decay honor any authored Drop slowdown alongside the recoloring pulses.
    /// </summary>
    private void UpdateCameraNavigation(float deltaTime)
    {
        moveProgress += deltaTime * flySpeed;

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

        // An active synced Fill pins the camera roll at the authored angle; otherwise the roll
        // eases back to zero at the mode's return speed. The Fill response's authored design is
        // unresolved — the FillCameraRollAngle remarks preserve the intent trail.
        if (beatManager.IsSynced && beatManager.Fill.Active)
        {
            currentRollAngle = SyncSettings.FillCameraRollAngle;
        }
        else
        {
            currentRollAngle = Mathf.LerpAngle(
                currentRollAngle,
                0.0f,
                deltaTime * (beatManager.IsSynced
                    ? SyncSettings.CameraRollReturnSpeed
                    : standaloneSettings.CameraRollReturnSpeed));
        }

        cameraRot = Quaternion.Slerp(cameraRot, targetRot, deltaTime * turnSpeed)
                  * Quaternion.AngleAxis(currentRollAngle, Vector3.forward);
    }

    /// <summary>
    /// Pathfinding step: Sets immediate target cell and peeks one cell ahead to predict upcoming turns.
    /// </summary>
    private void SelectNextMoveDirection()
    {
        // Advance the target cell along the current move direction, then look ahead from it to
        // predict the turn after this one.
        targetCell = currentCell + moveDir;
        List<Vector3Int> nextOpenDirs = GetOpenDirectionsFrom(targetCell);

        if (nextOpenDirs.Count == 0)
        {
            nextMoveDir = -moveDir; // Dead end — prepare to turn around
            return;
        }

        bool canContinueAhead = nextOpenDirs.Contains(moveDir);

        // Random.value spans the complete probability domain; DirectionChoiceThreshold authors its
        // split. The short-circuit is load-bearing: the roll is consumed only when continuing ahead
        // is possible, so restructuring this condition would change the Standalone look.
        if (canContinueAhead &&
            (Random.value > standaloneSettings.DirectionChoiceThreshold || nextOpenDirs.Count == 1))
        {
            nextMoveDir = moveDir;
        }
        else
        {
            Vector3Int reverseDir = -moveDir;
            List<Vector3Int> nonReversingDirs = nextOpenDirs.FindAll(d => d != reverseDir);

            if (nonReversingDirs.Count > 0)
            {
                // The inline selector spans every currently valid non-reversing direction.
                nextMoveDir = nonReversingDirs[Random.Range(0, nonReversingDirs.Count)];
            }
            else
            {
                nextMoveDir = canContinueAhead ? moveDir : reverseDir;
            }
        }
    }

    /// <summary>Collects the cardinal directions whose neighboring cell is empty, in scan order.</summary>
    private List<Vector3Int> GetOpenDirectionsFrom(Vector3Int cell)
    {
        List<Vector3Int> openDirs = new List<Vector3Int>();

        foreach (var dir in CardinalDirections)
        {
            if (IsCellEmpty(cell + dir))
            {
                openDirs.Add(dir);
            }
        }

        return openDirs;
    }

    /// <summary>Reports whether the cell is empty, wrapping coordinates toroidally into the grid.</summary>
    private bool IsCellEmpty(Vector3Int cell)
    {
        int vx = PositiveModulo(cell.x, VoxelGridSize);
        int vy = PositiveModulo(cell.y, VoxelGridSize);
        int vz = PositiveModulo(cell.z, VoxelGridSize);

        return voxelGrid[vx, vy, vz].a == 0.0f;
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
    /// the first filled voxel — face-shaded, edge-darkened, and squared-exponentially fogged — or
    /// black past the fog range.
    /// </summary>
    private Color TraceVoxelRay(Vector3 rayOrigin, Vector3 rayDir, float rhythm, bool isBeatSynced)
    {
        // Apply spatial pulse along ray direction when audio is present and synced
        Vector3 pulsedOrigin = rayOrigin + (rayDir * (rhythm * SyncSettings.PulseStrength));

        float rx = ClampAwayFromZero(rayDir.x);
        float ry = ClampAwayFromZero(rayDir.y);
        float rz = ClampAwayFromZero(rayDir.z);

        Vector3 currentPos = pulsedOrigin;

        int mapX = Mathf.FloorToInt(currentPos.x);
        int mapY = Mathf.FloorToInt(currentPos.y);
        int mapZ = Mathf.FloorToInt(currentPos.z);

        int stepX = rx > 0 ? 1 : -1;
        int stepY = ry > 0 ? 1 : -1;
        int stepZ = rz > 0 ? 1 : -1;

        float deltaDistX = Mathf.Abs(1.0f / rx);
        float deltaDistY = Mathf.Abs(1.0f / ry);
        float deltaDistZ = Mathf.Abs(1.0f / rz);

        float sideDistX = (stepX > 0) ? (mapX + 1.0f - currentPos.x) * deltaDistX : (currentPos.x - mapX) * deltaDistX;
        float sideDistY = (stepY > 0) ? (mapY + 1.0f - currentPos.y) * deltaDistY : (currentPos.y - mapY) * deltaDistY;
        float sideDistZ = (stepZ > 0) ? (mapZ + 1.0f - currentPos.z) * deltaDistZ : (currentPos.z - mapZ) * deltaDistZ;

        float distanceTraveled = 0f;

        // A ray that starts inside a filled voxel (possible when the pulse displaces the origin)
        // shades as an X-axis face.
        Axis hitAxis = Axis.X;

        // Dynamic max fog distance modulation on beats
        float currentMaxDist =
            standaloneSettings.MaxRayDistance + (rhythm * SyncSettings.DynamicFogAmount);

        while (distanceTraveled < currentMaxDist)
        {
            int vx = PositiveModulo(mapX, VoxelGridSize);
            int vy = PositiveModulo(mapY, VoxelGridSize);
            int vz = PositiveModulo(mapZ, VoxelGridSize);

            Color voxelColor = voxelGrid[vx, vy, vz];

            if (voxelColor.a > 0.0f)
            {
                if (isBeatSynced)
                {
                    voxelColor = ApplySyncedEventRecolor(voxelColor, vx, vy, vz);
                }

                // Shading calculations with audio peak boost
                float baseShade = hitAxis switch
                {
                    Axis.X => standaloneSettings.XAxisFaceShade,
                    Axis.Y => standaloneSettings.YAxisFaceShade,
                    _ => standaloneSettings.ZAxisFaceShade,
                };
                float shade = baseShade + (rhythm * SyncSettings.PeakBrightnessBoost);

                // Squared-exponential fog over the normalized ray distance: near walls stay
                // bright, the sub-tile far field crushes toward black. At the range cutoff the
                // curve is already near zero, so the black past-range return stays seamless.
                float normalizedDistance = distanceTraveled / currentMaxDist;
                float fogExponent = standaloneSettings.FogDensity * normalizedDistance;
                float fog = Mathf.Exp(-fogExponent * fogExponent);

                float edge = EdgeLineFactor(pulsedOrigin + (rayDir * distanceTraveled), hitAxis, distanceTraveled);
                float brightness = shade * fog * edge;

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
    /// Computes the edge-darkening multiplier for a ray hit: 1 across the face interior, ramping
    /// down to the authored edge shade near any voxel edge. The band's voxel-space width scales
    /// with hit distance so the rendered line keeps a constant on-wall thickness in tiles at
    /// every depth. Drawing the voxel lattice onto wall faces keeps corridor geometry legible at
    /// the wall's 900-tile resolution, where a face only reads as structure when contrast lives
    /// at its boundaries.
    /// </summary>
    private float EdgeLineFactor(Vector3 hitPos, Axis hitAxis, float hitDistance)
    {
        // Constant on-wall thickness: one tile pitch at the focal plane projects from a
        // voxel-space width of distance / focalLength. Capped at half a face span, where the
        // bands from opposite edges meet.
        float bandWidth = Mathf.Min(
            standaloneSettings.EdgeLineThicknessTiles * hitDistance / standaloneSettings.FocalLength,
            0.5f);
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

        return Mathf.Lerp(
            standaloneSettings.EdgeLineShade,
            1.0f,
            Mathf.Clamp01(edgeDistance / bandWidth));
    }

    /// <summary>
    /// Recolors one checker-selected slice of hit voxels while a synced Fill or Drop is active:
    /// Drop flashes a grayscale pulse and Fill flashes a hue-cycling pulse. Fill wins when both
    /// events are active, matching the original apply order (Drop first, Fill overwrote it).
    /// </summary>
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
    private static float ClampAwayFromZero(float component)
    {
        if (Mathf.Abs(component) >= 1e-6f)
        {
            return component;
        }

        return component < 0f ? -1e-6f : 1e-6f;
    }

    /// <summary>Wraps a coordinate into [0, length), which is what makes the voxel grid toroidal.</summary>
    private static int PositiveModulo(int value, int length)
    {
        int result = value % length;
        return result < 0 ? result + length : result;
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

    /// <summary>Smoothing rate that returns camera roll to zero outside an active synced Fill.</summary>
    public float CameraRollReturnSpeed;

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

    /// <summary>Camera-roll angle assigned during an active synced Fill.</summary>
    /// <remarks>
    /// The call-site note described directly adding degrees per frame to currentRollAngle, while the
    /// captured assignment sets it to 5 degrees. Unresolved authored alternatives were 45 degrees,
    /// incrementing by 2 degrees, and a continuous 30.0 * localDelta spin; the old tuning note said
    /// to modify 30.0 to change continuous spin speed, and described the response as continuous with smoothing.
    /// </remarks>
    [Tooltip("Synced Fill camera-roll assignment. Current: 5 degrees. Unresolved alternatives: 45 degrees, += 2 degrees, or 30 * localDelta continuous spin; the old note said to modify 30 for spin speed.")]
    [Min(0f)]
    public float FillCameraRollAngle;

    /// <summary>Smoothing rate (per second) easing camera roll back to zero between synced Fills.</summary>
    [Min(0f)]
    public float CameraRollReturnSpeed;

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
        FillCameraRollAngle = source.FillCameraRollAngle;
        CameraRollReturnSpeed = source.CameraRollReturnSpeed;
        EventCheckerModulo = source.EventCheckerModulo;
        EventPulseSpeed = source.EventPulseSpeed;
    }
}
