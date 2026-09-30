using System.IO;
using System.Globalization;
using UnityEngine;

public class Life : EffectBase
{
    public override string DebugText() => "Life";

    private int[][] Neighbors;
    private int[] states = new int[900];
    private float[] mesh;
    private float timer = 0f;

    private Vector2 GetVertex(int triangleIndex, int vertexIndex)
    {
        int baseIndex = (triangleIndex * 6) + (vertexIndex * 2);
        return new Vector2(mesh[baseIndex], mesh[baseIndex + 1]);
    }

    private bool CheckNeighbor(int i, int j)
    {
        int[] trianglesI = { 2 * i, (2 * i) + 1 };
        int[] trianglesJ = { 2 * j, (2 * j) + 1 };

        foreach (int triI in trianglesI)
        {
            for (int x = 0; x < 3; x++)
            {
                Vector2 v1 = GetVertex(triI, x);
                foreach (int triJ in trianglesJ)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        Vector2 v2 = GetVertex(triJ, y);
                        if (Mathf.Approximately(v1.x, v2.x) && Mathf.Approximately(v1.y, v2.y))
                            return true;
                    }
                }
            }
        }
        return false;
    }

    public override void Init()
    {
        base.Init();
        mesh = penrose.Layout.Mesh;
        Neighbors = new int[900][];

        string filePath = Path.Combine(Application.persistentDataPath, "penrose_neighbors.txt");

        // Try loading from file first to make startup instantaneous
        if (File.Exists(filePath))
        {
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 0; i < 900 && i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    Neighbors[i] = new int[0];
                    continue;
                }

                string[] parts = lines[i].Split(',');
                int[] row = new int[parts.Length];
                for (int j = 0; j < parts.Length; j++)
                {
                    int.TryParse(parts[j], out row[j]);
                }
                Neighbors[i] = row;
            }
            Debug.Log("Loaded neighbor data from cache: " + filePath);
        }
        else
        {
            // Build neighbor map from scratch if cache doesn't exist
            int[] localList = new int[100];
            string[] lines = new string[900];

            for (int i = 0; i < 900; i++)
            {
                int localSize = 0;
                for (int j = 0; j < 900; j++)
                {
                    if (i == j) continue;
                    if (CheckNeighbor(i, j))
                    {
                        localList[localSize++] = j;
                    }
                }

                Neighbors[i] = new int[localSize];
                for (int j = 0; j < localSize; j++)
                    Neighbors[i][j] = localList[j];

                // Format row into comma-separated text for storage
                lines[i] = string.Join(",", Neighbors[i]);
            }

            File.WriteAllLines(filePath, lines);
            Debug.Log("Generated and saved neighbor data cache to: " + filePath);
        }
    }

    public override void OnStart()
    {
        // Clear or seed with noise
        for (int i = 0; i < 900; i++)
        {
            float roll = UnityEngine.Random.value;
            if (roll < 0.15f)
            {
                states[i] = 1;
            }
            else if (roll < 0.25f)
            {
                states[i] = 2;
            }
            else
            {
                states[i] = 0;
            }
        }
        
        timer = 0f;
    }

    protected override void OnNewGrid()
    {
        // Advance the cellular automaton simulation step on musical grid beats
        StepCellularAutomaton();
    }

    private void StepCellularAutomaton()
    {
        int[] nextStates = new int[900];

        for (int i = 0; i < 900; i++)
        {
            int currentState = states[i];
            int n1 = 0, n2 = 0, n3 = 0;

            // Count neighbor states
            foreach (int neighborIdx in Neighbors[i])
            {
                int s = states[neighborIdx];
                if (s == 1) n1++;
                else if (s == 2) n2++;
                else if (s == 3) n3++;
            }

            // Apply rules table
            int nextState = 0; // Default fallback -> state 0

            if (currentState == 0)
            {
                if (n1 >= 1 && n2 >= 1) nextState = 3;
                else if (n1 >= 1 && n3 >= 2) nextState = 1;
            }
            else if (currentState == 1)
            {
                if (n3 >= 1) nextState = 2;
                else nextState = 1;
            }
            else if (currentState == 2)
            {
                nextState = 3;
            }
            else
            {
                nextState = 0;
            }

            nextStates[i] = nextState;
        }

        states = nextStates;
    }

    public override void Draw()
    {
        StepCellularAutomaton();
        for(int i=0; i<8; i++)
            states[Random.Range(0,900)] = Random.Range(0,4);
            
        for (int i = 0; i < buffer.Length; i++)
        {
            switch (states[i])
            {
                case 0:
                    buffer[i] = Color.black;
                    break;
                case 1:
                    buffer[i] = APalette.read(0.4f);
                    break;
                case 2:
                    buffer[i] = APalette.read(0.6f);
                    break;
                case 3:
                    buffer[i] = APalette.read(0.8f);
                    break;
            }
        }
    }

    public override void OnEnd() { }
}