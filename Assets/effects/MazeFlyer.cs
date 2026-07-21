using UnityEngine;
using System.Collections.Generic;

public class MazeFlyer : EffectBase
{
    private const int GRID_SIZE = 16;
    private const float MAX_RAY_DIST = 20.0f; // Limit ray distance (beyond this is black)

    // 16x16x16 Voxel grid colors. Color.clear (alpha=0) indicates an empty voxel.
    private Color[,,] voxelGrid = new Color[GRID_SIZE, GRID_SIZE, GRID_SIZE];

    // Flying camera navigation state
    private Vector3Int currentCell = new Vector3Int(1, 1, 1);
    private Vector3Int targetCell = new Vector3Int(1, 1, 1);
    private Vector3Int moveDir = Vector3Int.forward;
    
    // Smooth interpolation variables
    private Vector3 cameraPos;
    private Quaternion cameraRot = Quaternion.identity;
    private Quaternion targetRot = Quaternion.identity;
    private float moveProgress = 1.0f; // 0.0 to 1.0 step transition
    private float flySpeed = 4.0f;      // Cells per second

    public override void Init()
    {
        base.Init();
        GenerateVoxelGrid();
        InitializeCameraPosition();
    }

    public override void OnStart()
    {
        base.OnStart();
        GenerateVoxelGrid();
        InitializeCameraPosition();
    }

    /// <summary>
    /// Generates the 16x16x16 voxel grid:
    /// - Even locations (x, y, z % 2 == 0) are ALWAYS filled.
    /// - In-between locations have a random probability of being filled.
    /// </summary>
    private void GenerateVoxelGrid()
    {
        float fillProbability = 0.25f; // Probability for non-even locations

        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int z = 0; z < GRID_SIZE; z++)
                {
                    bool isEvenIndex = (x % 2 == 0) && (y % 2 == 0) && (z % 2 == 0);
                    
                    if (isEvenIndex || Random.value < fillProbability)
                    {
                        voxelGrid[x, y, z] = Color.HSVToRGB(Random.value, 0.9f, 0.95f);
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
    /// Finds a guaranteed empty starting cell and aligns the camera.
    /// </summary>
    private void InitializeCameraPosition()
    {
        // Search for an empty cell in the grid to spawn into
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
                        
                        // Pick initial valid move direction
                        SelectNextMoveDirection();
                        return;
                    }
                }
            }
        }
    }

    public override string DebugText() => "Maze Flyer (Smart Navigator)";

    public override void Draw()
    {
        // 1. Update camera movement and turning navigation
        UpdateCameraNavigation(Time.deltaTime);

        // Compute camera coordinate vectors from current smooth rotation
        Vector3 cameraForward = cameraRot * Vector3.forward;
        Vector3 cameraRight = cameraRot * Vector3.right;
        Vector3 cameraUp = cameraRot * Vector3.up;

        float focalLength = 18.0f; // Perspective strength relative to tile bounds

        // 2. Cast a ray for each Penrose tile pixel
        for (int i = 0; i < buffer.Length; i++)
        {
            float px = tiles[i].center.x;
            float py = tiles[i].center.y;

            // Screen-space ray direction based on tile coordinates
            Vector3 rayDir = (cameraForward * focalLength + cameraRight * px + cameraUp * py).normalized;

            // Trace ray through the tiled infinite voxel grid
            buffer[i] = TraceVoxelRay(cameraPos, rayDir);
        }
    }

    /// <summary>
    /// Handles collision avoidance, step interpolation, and 90-degree turning decisions.
    /// </summary>
    private void UpdateCameraNavigation(float deltaTime)
    {
        moveProgress += deltaTime * flySpeed;

        if (moveProgress >= 1.0f)
        {
            // Arrived at target cell
            currentCell = targetCell;
            moveProgress = 0.0f;

            // Choose next valid direction
            SelectNextMoveDirection();
        }

        // Interpolate position between current cell center and target cell center
        Vector3 startPos = GetCellCenter(currentCell);
        Vector3 endPos = GetCellCenter(targetCell);
        cameraPos = Vector3.Lerp(startPos, endPos, moveProgress);

        // Smoothly rotate toward the current movement direction
        if (moveDir != Vector3Int.zero)
        {
            targetRot = Quaternion.LookRotation(new Vector3(moveDir.x, moveDir.y, moveDir.z));
        }
        cameraRot = Quaternion.Slerp(cameraRot, targetRot, deltaTime * 8.0f);
    }

    /// <summary>
    /// Evaluates adjacent cells and chooses an open path, prioritizing forward motion.
    /// </summary>
    private void SelectNextMoveDirection()
    {
        List<Vector3Int> openDirections = GetOpenDirectionsFrom(currentCell);

        if (openDirections.Count == 0)
        {
            // Emergency fallback if completely trapped
            targetCell = currentCell;
            return;
        }

        // 1. Try to keep moving forward if possible (with occasional random turns at open junctions)
        bool keepGoingForward = openDirections.Contains(moveDir) && (Random.value > 0.35f || openDirections.Count == 1);

        if (keepGoingForward)
        {
            targetCell = currentCell + moveDir;
        }
        else
        {
            // 2. Pick a new open direction (preferring non-reversing directions if available)
            Vector3Int reverseDir = -moveDir;
            List<Vector3Int> nonReversingDirs = openDirections.FindAll(d => d != reverseDir);

            if (nonReversingDirs.Count > 0)
            {
                moveDir = nonReversingDirs[Random.Range(0, nonReversingDirs.Count)];
            }
            else
            {
                // Must backtrack
                moveDir = reverseDir;
            }

            targetCell = currentCell + moveDir;
        }
    }

    /// <summary>
    /// Returns all orthogonal neighbor directions (X, Y, Z) that lead to empty cells.
    /// </summary>
    private List<Vector3Int> GetOpenDirectionsFrom(Vector3Int cell)
    {
        List<Vector3Int> openDirs = new List<Vector3Int>();

        Vector3Int[] neighbors = new Vector3Int[]
        {
            Vector3Int.forward,
            Vector3Int.back,
            Vector3Int.left,
            Vector3Int.right,
            Vector3Int.up,
            Vector3Int.down
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

    /// <summary>
    /// Checks if a cell is empty in the wrapped infinite voxel space.
    /// </summary>
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
    /// Fast Voxel Traversal (3D DDA) through infinite 16x16x16 tiled voxel space.
    /// </summary>
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