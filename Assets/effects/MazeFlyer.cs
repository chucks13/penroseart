using UnityEngine;
using System.Collections.Generic;

// designed by Chuck, coded by Gemini

public class MazeFlyer : EffectBase
{
    private const int GRID_SIZE = 16;
    private const float MAX_RAY_DIST = 20.0f;

    public override Repertoire Repertoire =>
     Repertoire.HandlesFill | Repertoire.HandlesDrop;

    // Color generation modes
    private enum ColorMode
    {
        PureRandom,
        SpatialWaves,
        BlockRegions,
        CuratedPalette
    }

    private ColorMode activeColorMode = ColorMode.PureRandom;

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
    private float flySpeed = 2.0f;
    private float turnSpeed = 4.0f;

    // Pre-defined palette for CuratedPalette mode
    private Color[] curatedPalette = new Color[]
    {
        Color.HSVToRGB(0.78f, 0.9f, 0.95f), // Purple/Magenta
        Color.HSVToRGB(0.55f, 0.9f, 0.95f), // Cyan/Blue
        Color.HSVToRGB(0.18f, 0.9f, 0.95f), // Yellow/Gold
        Color.HSVToRGB(0.38f, 0.9f, 0.95f)  // Lime Green
    };

    public override void Init()
    {
        base.Init();
        OnStart();
    }

    public override void OnStart()
    {
        base.OnStart();

        float overallSpeed = (float)Random.Range(1, 5);
        flySpeed = overallSpeed;
        turnSpeed = overallSpeed * 2.5f;

        // 1. Randomly choose one of the 4 color modes
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
        float fillProbability = 0.25f;
        float spatialScale = 0.15f;
        int blockSize = 4;

        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int z = 0; z < GRID_SIZE; z++)
                {
                    bool isEvenIndex = (x % 2 == 0) && (y % 2 == 0) && (z % 2 == 0);

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
                return Color.HSVToRGB(Random.value, 0.9f, 0.95f);

            case ColorMode.SpatialWaves:
                float waveHue = (Mathf.Sin(x * spatialScale) + Mathf.Cos(y * spatialScale) + Mathf.Sin(z * spatialScale) + 3f) / 6f;
                return Color.HSVToRGB(waveHue, 0.85f, 0.95f);

            case ColorMode.BlockRegions:
                int blockX = x / blockSize;
                int blockY = y / blockSize;
                int blockZ = z / blockSize;
                int blockHash = blockX * 73 + blockY * 179 + blockZ * 283;

                float baseHue = (Mathf.Abs(blockHash) % 100) / 100.0f;
                float blockHue = (baseHue + Random.Range(-0.05f, 0.05f) + 1.0f) % 1.0f;
                return Color.HSVToRGB(blockHue, 0.88f, 0.95f);

            case ColorMode.CuratedPalette:
                return curatedPalette[Random.Range(0, curatedPalette.Length)];

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

    public override void Draw()
    {
        UpdateCameraNavigation(Time.deltaTime);

        Vector3 cameraForward = cameraRot * Vector3.forward;
        Vector3 cameraRight = cameraRot * Vector3.right;
        Vector3 cameraUp = cameraRot * Vector3.up;

        float focalLength = 18.0f;

        for (int i = 0; i < buffer.Length; i++)
        {
            float px = tiles[i].center.x;
            float py = tiles[i].center.y;

            Vector3 rayDir = (cameraForward * focalLength + cameraRight * px + cameraUp * py).normalized;
            buffer[i] = TraceVoxelRay(cameraPos, rayDir);
        }
    }

    /// <summary>
    /// Handles camera movement along the grid path and smooth look-ahead rotation blending.
    /// </summary>
    private void UpdateCameraNavigation(float deltaTime)
    {
        moveProgress += deltaTime * flySpeed;

        // When reaching the target cell, advance state and pick next turn ahead of time
        if (moveProgress >= 1.0f)
        {
            currentCell = targetCell;
            moveDir = nextMoveDir; // Carry over the previously predicted direction
            moveProgress = 0.0f;
            SelectNextMoveDirection();
        }

        // Linear position interpolation ensures the camera stays strictly inside open corridors
        Vector3 startPos = GetCellCenter(currentCell);
        Vector3 endPos = GetCellCenter(targetCell);
        cameraPos = Vector3.Lerp(startPos, endPos, moveProgress);

        // Smoothly lead rotation into upcoming turns before reaching the intersection
        Vector3 currentDirVec = new Vector3(moveDir.x, moveDir.y, moveDir.z);
        Vector3 nextDirVec = new Vector3(nextMoveDir.x, nextMoveDir.y, nextMoveDir.z);

        // Blend looking direction toward nextMoveDir in the latter portion of the cell traversal
        Vector3 blendedForward = Vector3.Lerp(currentDirVec, nextDirVec, Mathf.SmoothStep(0.2f, 1.0f, moveProgress)).normalized;

        if (blendedForward != Vector3.zero)
        {
            targetRot = Quaternion.LookRotation(blendedForward);
        }

        cameraRot = Quaternion.Slerp(cameraRot, targetRot, deltaTime * turnSpeed);
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

        if (canContinueAhead && (Random.value > 0.35f || nextOpenDirs.Count == 1))
        {
            nextMoveDir = moveDir;
        }
        else
        {
            Vector3Int reverseDir = -moveDir;
            List<Vector3Int> nonReversingDirs = nextOpenDirs.FindAll(d => d != reverseDir);

            if (nonReversingDirs.Count > 0)
            {
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

    private Color TraceVoxelRay(Vector3 rayOrigin, Vector3 rayDir)
    {
        float rx = Mathf.Abs(rayDir.x) < 1e-6f ? 1e-6f : rayDir.x;
        float ry = Mathf.Abs(rayDir.y) < 1e-6f ? 1e-6f : rayDir.y;
        float rz = Mathf.Abs(rayDir.z) < 1e-6f ? 1e-6f : rayDir.z;

        Vector3 currentPos = rayOrigin;

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

        while (distanceTraveled < MAX_RAY_DIST)
        {
            int vx = PositiveModulo(mapX, GRID_SIZE);
            int vy = PositiveModulo(mapY, GRID_SIZE);
            int vz = PositiveModulo(mapZ, GRID_SIZE);

            Color voxelColor = voxelGrid[vx, vy, vz];

            if (voxelColor.a > 0.0f)
            {
                if (beatManager.Drop.Active)
                {
                    int checker = (vx + vy + vz) % 4;
                    if (checker == 0)
                    {
                        var t = Mathf.PingPong(effectTime * 4, 2).Remap(0f, 2, 0f, 1f, clamp: true);
                        voxelColor = Color.HSVToRGB(0f, 0f, t);
                    }
                }
                if (beatManager.Fill.Active)
                {
                    int checker = (vx + vy + vz) % 4;
                    if (checker == 0)
                    {
                        var t = Mathf.PingPong(effectTime * 4, 2).Remap(0f, 2, 0f, 1f, clamp: true);
                        voxelColor = Color.HSVToRGB(t, 1f, 1f);
                    }
                }

                float shade = hitSide == 0 ? 0.75f : (hitSide == 1 ? 0.95f : 0.60f);
                float fog = 1.0f - Mathf.Clamp01(distanceTraveled / MAX_RAY_DIST);

                return new Color(
                    voxelColor.r * shade * fog,
                    voxelColor.g * shade * fog,
                    voxelColor.b * shade * fog,
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

    private static int PositiveModulo(int value, int length)
    {
        int result = value % length;
        return result < 0 ? result + length : result;
    }

    public override void OnEnd()
    {
    }
}