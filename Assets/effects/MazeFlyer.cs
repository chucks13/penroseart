using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>Renders a first-person flight through a randomized voxel maze onto the Penrose tile buffer.</summary>
[EffectSyncSettings(typeof(MazeFlyerSyncSettingsAsset))]
public class MazeFlyer : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored inclusive minimum for the per-activation integer flight-speed roll.</summary>
    private const int StandaloneOverallSpeedMin = 1;

    /// <summary>Authored exclusive maximum for the per-activation integer flight-speed roll.</summary>
    private const int StandaloneOverallSpeedMaxExclusive = 5;

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

    /// <summary>Authored maximum ray distance and baseline fog range.</summary>
    private const float StandaloneMaxRayDistance = 20.0f;

    /// <summary>Authored shade multiplier for voxel faces hit across the X axis.</summary>
    private const float StandaloneXAxisFaceShade = 0.75f;

    /// <summary>Authored shade multiplier for voxel faces hit across the Y axis.</summary>
    private const float StandaloneYAxisFaceShade = 0.95f;

    /// <summary>Authored shade multiplier for voxel faces hit across the Z axis.</summary>
    private const float StandaloneZAxisFaceShade = 0.60f;

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

    // Runtime mechanism constants and state

    /// <summary>Fixed width, height, and depth of the cubic voxel-grid algorithm.</summary>
    private const int GRID_SIZE = 16;

    /// <summary>The current camera-roll angle, discarded to zero by each activation's Roll.</summary>
    private float currentRollAngle = 0.0f;

    /// <summary>The musical capabilities and energy range advertised by MazeFlyer.</summary>
    public override Repertoire Repertoire =>
     Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow;

    /// <summary>
    /// Resolves a fresh copy of MazeFlyer's Standalone Defaults. The curated palette is cloned
    /// per resolve, so no activation shares mutable state with the authored table.
    /// </summary>
    public static MazeFlyerStandaloneSettings StandaloneSettings => new MazeFlyerStandaloneSettings
    {
        OverallSpeedMin = StandaloneOverallSpeedMin,
        OverallSpeedMaxExclusive = StandaloneOverallSpeedMaxExclusive,
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

    // Color generation modes
    private enum ColorMode
    {
        PureRandom,
        SpatialWaves,
        BlockRegions,
        CuratedPalette
    }

    /// <summary>The color mode selected from the enum's complete four-member domain on activation.</summary>
    private ColorMode activeColorMode;

    // 16x16x16 Voxel grid colors. Color.clear (alpha=0) indicates an empty voxel.
    private Color[,,] voxelGrid = new Color[GRID_SIZE, GRID_SIZE, GRID_SIZE];

    // Flying camera navigation state
    private Vector3Int currentCell = new Vector3Int(1, 1, 1);
    private Vector3Int targetCell = new Vector3Int(1, 1, 1);
    private Vector3Int moveDir = Vector3Int.forward;

    // Look-Ahead Navigation: Next move direction peeked 1 step ahead for turn anticipation
    private Vector3Int nextMoveDir = Vector3Int.forward;

    private Vector3 cameraPos;
    private Quaternion cameraRot = Quaternion.identity;
    private Quaternion targetRot = Quaternion.identity;
    private float moveProgress = 1.0f;
    /// <summary>The current flight speed rolled on activation.</summary>
    private float flySpeed;

    /// <summary>The current camera-turn speed derived from <see cref="flySpeed"/>.</summary>
    private float turnSpeed;

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

        float overallSpeed = (float)Random.Range(
            standaloneSettings.OverallSpeedMin,
            standaloneSettings.OverallSpeedMaxExclusive);
        flySpeed = overallSpeed;
        turnSpeed = overallSpeed * standaloneSettings.TurnSpeedMultiplier;

        // 1. Randomly choose one of the 4 color modes
        // The enum has four members and GetVoxelColor handles all four, so [0, 4) is the complete
        // selector domain rather than an authored subrange.
        activeColorMode = (ColorMode)Random.Range(0, 4);

        // 2. Generate the grid with the selected color style
        GenerateVoxelGrid();

        // 3. Place camera in a valid empty spot
        InitializeCameraPosition();
    }

    /// <summary>
    /// Populates the 16x16x16 voxel grid based on activeColorMode.
    /// </summary>
    private void GenerateVoxelGrid()
    {
        float fillProbability = standaloneSettings.FillProbability;
        float spatialScale = standaloneSettings.SpatialScale;
        int blockSize = standaloneSettings.BlockSize;

        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int z = 0; z < GRID_SIZE; z++)
                {
                    bool isEvenIndex = (x % 2 == 0) && (y % 2 == 0) && (z % 2 == 0);

                    // Random.value spans the complete probability domain; FillProbability authors its threshold.
                    // Guaranteed even-index cells short-circuit before that roll, so they consume no
                    // Random.value and retain the original mode-specific roll order.
                    if (isEvenIndex || Random.value < fillProbability)
                    {
                        voxelGrid[x, y, z] = GetVoxelColor(x, y, z, spatialScale, blockSize);
                    }
                    else
                    {
                        voxelGrid[x, y, z] = Color.clear;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Evaluates color according to the active mode selected on Start.
    /// </summary>
    private Color GetVoxelColor(int x, int y, int z, float spatialScale, int blockSize)
    {
        switch (activeColorMode)
        {
            case ColorMode.PureRandom:
                // Random.value spans the complete hue-wheel domain; only saturation and value are authored settings.
                return Color.HSVToRGB(
                    Random.value,
                    standaloneSettings.PureRandomSaturation,
                    standaloneSettings.PureRandomValue);

            case ColorMode.SpatialWaves:
                float waveHue = (Mathf.Sin(x * spatialScale) + Mathf.Cos(y * spatialScale) + Mathf.Sin(z * spatialScale) + 3f) / 6f;
                return Color.HSVToRGB(
                    waveHue,
                    standaloneSettings.SpatialWavesSaturation,
                    standaloneSettings.SpatialWavesValue);

            case ColorMode.BlockRegions:
                int blockX = x / blockSize;
                int blockY = y / blockSize;
                int blockZ = z / blockSize;
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

            case ColorMode.CuratedPalette:
                // The inline selector spans every entry in the complete authored palette table.
                return standaloneSettings.CuratedPalette[
                    Random.Range(0, standaloneSettings.CuratedPalette.Length)];

            default:
                return Color.white;
        }
    }

    private void InitializeCameraPosition()
    {
        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int z = 0; z < GRID_SIZE; z++)
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

    public override string DebugText() => $"Maze Flyer [{activeColorMode}]";

    /// <summary>Advances the camera and traces one voxel ray for every Penrose tile.</summary>
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

        for (int i = 0; i < buffer.Length; i++)
        {
            float px = tiles[i].center.x;
            float py = tiles[i].center.y;

            Vector3 rayDir = (cameraForward * focalLength + cameraRight * px + cameraUp * py).normalized;
            buffer[i] = TraceVoxelRay(cameraPos, rayDir, rhythm, isBeatSynced);
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

        // ========================================================================
        // FILL EVENT: DYNAMIC CAMERA ROLL (Continuous with smoothing)
        // ========================================================================
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
        // 1. Advance target cell along current move direction
        targetCell = currentCell + moveDir;

        // 2. Look ahead from targetCell to predict the turn after this one
        List<Vector3Int> nextOpenDirs = GetOpenDirectionsFrom(targetCell);

        if (nextOpenDirs.Count == 0)
        {
            nextMoveDir = -moveDir; // Dead end — prepare to turn around
            return;
        }

        bool canContinueAhead = nextOpenDirs.Contains(moveDir);

        // Random.value spans the complete probability domain; DirectionChoiceThreshold authors its split.
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

    private List<Vector3Int> GetOpenDirectionsFrom(Vector3Int cell)
    {
        List<Vector3Int> openDirs = new List<Vector3Int>();

        Vector3Int[] neighbors = new Vector3Int[]
        {
            Vector3Int.forward, Vector3Int.back,
            Vector3Int.left, Vector3Int.right,
            Vector3Int.up, Vector3Int.down
        };

        foreach (var dir in neighbors)
        {
            if (IsCellEmpty(cell + dir))
            {
                openDirs.Add(dir);
            }
        }

        return openDirs;
    }

    private bool IsCellEmpty(Vector3Int cell)
    {
        int vx = PositiveModulo(cell.x, GRID_SIZE);
        int vy = PositiveModulo(cell.y, GRID_SIZE);
        int vz = PositiveModulo(cell.z, GRID_SIZE);

        return voxelGrid[vx, vy, vz].a == 0.0f;
    }

    private Vector3 GetCellCenter(Vector3Int cell)
    {
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
    }

    /// <summary>
    /// Executes 3D DDA voxel ray stepping with audio-driven spatial recoil.
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
        int hitSide = 0;

        // Dynamic max fog distance modulation on beats
        float currentMaxDist =
            standaloneSettings.MaxRayDistance + (rhythm * SyncSettings.DynamicFogAmount);

        while (distanceTraveled < currentMaxDist)
        {
            int vx = PositiveModulo(mapX, GRID_SIZE);
            int vy = PositiveModulo(mapY, GRID_SIZE);
            int vz = PositiveModulo(mapZ, GRID_SIZE);

            Color voxelColor = voxelGrid[vx, vy, vz];

            if (voxelColor.a > 0.0f)
            {
                // Gate Drop and Fill recoloring using the boolean flag
                if (isBeatSynced && (beatManager.Drop.Active || beatManager.Fill.Active))
                {
                    int checker = (vx + vy + vz) % SyncSettings.EventCheckerModulo;
                    if (checker == 0)
                    {
                        var t = Mathf.PingPong(effectTime * SyncSettings.EventPulseSpeed, 2)
                            .Remap(0f, 2, 0f, 1f, clamp: true);

                        // Fill wins when both events are active, matching the original
                        // apply order (Drop first, Fill overwrote it).
                        voxelColor = beatManager.Fill.Active
                            ? Color.HSVToRGB(t, 1f, 1f)
                            : Color.HSVToRGB(0f, 0f, t);
                    }
                }

                // Shading calculations with audio peak boost
                float baseShade = hitSide == 0
                    ? standaloneSettings.XAxisFaceShade
                    : (hitSide == 1
                        ? standaloneSettings.YAxisFaceShade
                        : standaloneSettings.ZAxisFaceShade);
                float shade = baseShade + (rhythm * SyncSettings.PeakBrightnessBoost);
                float fog = 1.0f - Mathf.Clamp01(distanceTraveled / currentMaxDist);

                return new Color(
                    Mathf.Clamp01(voxelColor.r * shade * fog),
                    Mathf.Clamp01(voxelColor.g * shade * fog),
                    Mathf.Clamp01(voxelColor.b * shade * fog),
                    1.0f
                );
            }

            if (sideDistX < sideDistY && sideDistX < sideDistZ)
            {
                distanceTraveled = sideDistX;
                sideDistX += deltaDistX;
                mapX += stepX;
                hitSide = 0;
            }
            else if (sideDistY < sideDistZ)
            {
                distanceTraveled = sideDistY;
                sideDistY += deltaDistY;
                mapY += stepY;
                hitSide = 1;
            }
            else
            {
                distanceTraveled = sideDistZ;
                sideDistZ += deltaDistZ;
                mapZ += stepZ;
                hitSide = 2;
            }
        }

        return Color.black;
    }

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

    private static int PositiveModulo(int value, int length)
    {
        int result = value % length;
        return result < 0 ? result + length : result;
    }

    public override void OnEnd()
    {
    }
}

/// <summary>The fixed Standalone Settings resolved from MazeFlyer's file-local Standalone Defaults.</summary>
public sealed class MazeFlyerStandaloneSettings
{
    /// <summary>Inclusive minimum for the per-activation integer flight-speed roll.</summary>
    public int OverallSpeedMin;

    /// <summary>Exclusive maximum for the per-activation integer flight-speed roll.</summary>
    public int OverallSpeedMaxExclusive;

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
