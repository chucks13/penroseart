using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Runs a tile-neighbor diffusion simulation and colors the resulting scalar field.
/// </summary>
public class Fluid : ScreenEffect
{
    /// <summary>Fluid's smooth flow suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>The diffusion field rendered on the current frame.</summary>
    private float[] currentState;

    /// <summary>The previous diffusion field reused as the next simulation target.</summary>
    private float[] previousState;

    /// <summary>Frame counter used to advance the diffusion simulation every other frame.</summary>
    private int frameCount;

    /// <summary>Fraction of simulated motion retained each step.</summary>
    public float fdamping = 0.95f;

    /// <summary>Energy placed into the field by each random injection.</summary>
    public float impulse = 1f;

    /// <summary>Inverse probability of injecting energy on a frame.</summary>
    public int activity = 50;

    /// <summary>Multiplier applied before wrapping field values into palette space.</summary>
    public float scale = 10f;

    /// <summary>Weight applied to the average neighboring field value.</summary>
    public float fneighbors = 2f;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => string.Empty;

    /// <summary>Acquires this activation's Waveform and resets the diffusion buffers.</summary>
    public override void OnStart()
    {
        waveform = waveforms.Random();
        currentState = new float[Penrose.Total];
        previousState = new float[Penrose.Total];
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>Advances the neighbor-driven diffusion field by one simulation step.</summary>
    private void AdvanceSimulation()
    {
        for (int i = 0; i < currentState.Length; i++)
        {
            float total = 0;
            float count = 0;
            for (int j = 0; j < tiles[i].neighbors.Length; j++)
            {
                int n = tiles[i].neighbors[j].tileIdx;
                if (n >= 0)
                {
                    total += currentState[n];
                    count++;
                }
            }
            float neighbors = total / count;
            neighbors *= fneighbors;
            float displacement = neighbors - previousState[i];
            previousState[i] = displacement * fdamping;
        }
        float[] swap = currentState;
        currentState = previousState;
        previousState = swap;
    }

    /// <summary>
    /// Randomly injects energy into the diffusion field.
    /// </summary>
    private void InjectEnergy()
    {
        if (Random.Range(0, activity) == 0)
        {
            currentState[Random.Range(0, currentState.Length)] = impulse;
        }
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        frameCount++;
        if (frameCount % 2 == 0)
        {
            AdvanceSimulation();
        }

        InjectEnergy();
        // The Waveform offsets the palette lookup; clockless rendering keeps the previous steady offset.
        float? rhythm = waveforms.Evaluate(waveform);
        float paletteOffset = rhythm is { } envelope ? Mathf.Lerp(0.5f, 1f, envelope) : 1f;
        for (int i = 0; i < currentState.Length; i++)
        {
            float v = currentState[i] * scale;
            v += 1000.5f;
            v %= 1f;
            buffer[i] = APalette.read(v + paletteOffset);
        }
    }
}
