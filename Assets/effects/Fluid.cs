using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Runs a tile-neighbor diffusion simulation and colors the resulting scalar field.
/// </summary>
[EffectSyncSettings(typeof(FluidSyncSettingsAsset))]
public class Fluid : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored fraction of simulated motion retained each step for the unchanged Standalone look.</summary>
    private const float StandaloneDamping = 0.95f;

    /// <summary>Authored energy placed into the field by each random injection in Standalone Mode.</summary>
    private const float StandaloneImpulse = 1f;

    /// <summary>Authored inverse probability of injecting energy on a Standalone frame.</summary>
    private const int StandaloneActivity = 50;

    /// <summary>Authored multiplier applied before wrapping field values into palette space in Standalone Mode.</summary>
    private const float StandalonePaletteScale = 10f;

    /// <summary>Authored weight applied to the average neighboring field value in Standalone Mode.</summary>
    private const float StandaloneNeighborWeight = 2f;

    /// <summary>Authored steady palette offset outside a Drop approach in Standalone Mode.</summary>
    private const float StandalonePaletteOffset = 0f;

    // Sync Defaults

    /// <summary>Authored steady fraction of simulated motion retained each step in Synced Mode.</summary>
    private const float SyncDamping = 0.95f;

    /// <summary>Authored energy placed into the field by each random injection in Synced Mode.</summary>
    private const float SyncImpulse = 1f;

    /// <summary>Authored steady inverse probability of injecting energy on a Synced frame.</summary>
    private const int SyncActivity = 50;

    /// <summary>Authored multiplier applied before wrapping field values into palette space in Synced Mode.</summary>
    private const float SyncPaletteScale = 10f;

    /// <summary>Authored weight applied to the average neighboring field value in Synced Mode.</summary>
    private const float SyncNeighborWeight = 2f;

    /// <summary>Authored steady palette offset outside a Drop approach in Synced Mode.</summary>
    private const float SyncPaletteOffset = 0f;

    /// <summary>Authored number of beats over which Fluid expects an approaching Drop.</summary>
    private const int SyncDropApproachBeats = 6;

    /// <summary>Authored damping that makes the fluid settle faster while a Drop approaches.</summary>
    private const float SyncDropDamping = 0.7f;

    /// <summary>Authored inverse injection probability intended to force an impulse at Drop onset.</summary>
    private const int SyncDropForcedActivity = 1;

    /// <summary>Authored extra simulation advances intended to bump the animation at Drop onset.</summary>
    private const int SyncDropOnsetSimulationAdvances = 2;

    /// <summary>Authored palette offset at the Waveform trough while a Drop approaches.</summary>
    private const float SyncDropPaletteOffsetAtWaveformTrough = 0.5f;

    /// <summary>
    /// Authored palette offset at the Waveform peak and its Standalone fallback; the non-Drop overwrite
    /// prevents that fallback from reaching Standalone rendering.
    /// </summary>
    private const float SyncDropPaletteOffsetAtWaveformPeak = 1f;

    /// <summary>Fluid's smooth flow suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh immutable-by-convention copy of Fluid's Standalone Defaults.</summary>
    public static FluidStandaloneSettings StandaloneSettings => new FluidStandaloneSettings(
        StandaloneDamping,
        StandaloneImpulse,
        StandaloneActivity,
        StandalonePaletteScale,
        StandaloneNeighborWeight,
        StandalonePaletteOffset);

    /// <summary>Resolves a fresh copy of Fluid's file-local Sync Defaults.</summary>
    public static FluidSyncSettings SyncDefaults => new FluidSyncSettings
    {
        Damping = SyncDamping,
        Impulse = SyncImpulse,
        Activity = SyncActivity,
        PaletteScale = SyncPaletteScale,
        NeighborWeight = SyncNeighborWeight,
        PaletteOffset = SyncPaletteOffset,
        DropApproachBeats = SyncDropApproachBeats,
        DropDamping = SyncDropDamping,
        DropForcedActivity = SyncDropForcedActivity,
        DropOnsetSimulationAdvances = SyncDropOnsetSimulationAdvances,
        DropPaletteOffsetAtWaveformTrough = SyncDropPaletteOffsetAtWaveformTrough,
        DropPaletteOffsetAtWaveformPeak = SyncDropPaletteOffsetAtWaveformPeak,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private FluidStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private FluidSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The diffusion field rendered on the current frame.</summary>
    private float[] currentState;

    /// <summary>The previous diffusion field reused as the next simulation target.</summary>
    private float[] previousState;

    /// <summary>Frame counter used to advance the diffusion simulation every other frame.</summary>
    private int frameCount;

    /// <summary>
    /// Current Synced damping state. Synced rendering mutates and reads it, and it intentionally
    /// remains sticky across activations. Standalone rendering reads the Standalone Default instead.
    /// </summary>
    private float fdamping = StandaloneDamping;

    /// <summary>
    /// Current Synced injection-activity state. Synced rendering mutates and reads it, and it
    /// intentionally remains sticky across activations. Standalone rendering reads the Standalone
    /// Default instead.
    /// </summary>
    private int activity = StandaloneActivity;

    /// <summary>Whether Fluid is currently inside its configured Drop-approach window.</summary>
    private bool dropComing = false;

    /// <summary>Drop activity retained from the prior frame for consumer-local onset detection.</summary>
    private bool lastDropActive;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"drop coming: {dropComing}\n";
    }

    /// <summary>Resolves Effect Settings, acquires this activation's Waveform, and resets the diffusion buffers.</summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Fluid),
            SyncDefaults);
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
        float neighborWeight = beatManager.IsSynced
            ? SyncSettings.NeighborWeight
            : standaloneSettings.NeighborWeight;
        // Standalone rendering reads its authored damping, never the Synced-mutated sticky state,
        // so a live Sync Settings tweak can never reach the Standalone look.
        float damping = beatManager.IsSynced
            ? fdamping
            : standaloneSettings.Damping;

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
            neighbors *= neighborWeight;
            float displacement = neighbors - previousState[i];
            previousState[i] = displacement * damping;
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
        // Standalone rendering reads its authored activity, never the Synced-mutated sticky state,
        // so a live Sync Settings tweak can never reach the Standalone look.
        int injectionChance = beatManager.IsSynced
            ? activity
            : standaloneSettings.Activity;
        // Zero is the designated success slot; the chance value controls the authored one-in-N odds.
        if (Random.Range(0, injectionChance) == 0)
        {
            float impulse = beatManager.IsSynced
                ? SyncSettings.Impulse
                : standaloneSettings.Impulse;
            // The full diffusion-buffer index range is structural rather than an authored range.
            currentState[Random.Range(0, currentState.Length)] = impulse;
        }
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        dropComing = false;
        bool dropJustStarted = !lastDropActive && beatManager.Drop.Active;
        lastDropActive = beatManager.Drop.Active;
        if (beatManager.IsSynced)
        {
            // If a Drop is approaching, settle the fluid faster.
            if (beatManager.Drop.Before.Build(SyncSettings.DropApproachBeats) > 0f)
            {
                dropComing = true;
                fdamping = SyncSettings.DropDamping;
                if (dropJustStarted)
                {
                    activity = SyncSettings.DropForcedActivity;
                    for (int i = 0; i < SyncSettings.DropOnsetSimulationAdvances; i++)
                    {
                        AdvanceSimulation();
                    }
                }
            }
            else
            {
                fdamping = SyncSettings.Damping;
                activity = SyncSettings.Activity;
            }
        }

        frameCount++;
        if (frameCount % 2 == 0)
        {
            AdvanceSimulation();
        }

        // Hold off new energy while a Drop is approaching.
        if (!dropComing)
            InjectEnergy();

        // The Waveform offsets the palette lookup only while a Drop is approaching.
        float paletteOffset = waveform.Lerp(
            SyncSettings.DropPaletteOffsetAtWaveformTrough,
            SyncSettings.DropPaletteOffsetAtWaveformPeak);
        if (!dropComing)
        {
            paletteOffset = beatManager.IsSynced
                ? SyncSettings.PaletteOffset
                : standaloneSettings.PaletteOffset;
        }

        float paletteScale = beatManager.IsSynced
            ? SyncSettings.PaletteScale
            : standaloneSettings.PaletteScale;
        for (int i = 0; i < currentState.Length; i++)
        {
            float v = currentState[i] * paletteScale;
            v += 1000.5f;
            v %= 1f;
            buffer[i] = APalette.read(v + paletteOffset);
            if (beatManager.Fill.Active)            // Rotate the color channels during a Fill.
                 buffer[i] = new Color(buffer[i].g, buffer[i].b, buffer[i].r,buffer[i].a);
        }
    }
}

/// <summary>The non-editable Standalone Settings that reproduce Fluid's authored no-music look.</summary>
public sealed class FluidStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from Fluid's file-local defaults.</summary>
    public FluidStandaloneSettings(
        float damping,
        float impulse,
        int activity,
        float paletteScale,
        float neighborWeight,
        float paletteOffset)
    {
        Damping = damping;
        Impulse = impulse;
        Activity = activity;
        PaletteScale = paletteScale;
        NeighborWeight = neighborWeight;
        PaletteOffset = paletteOffset;
    }

    /// <summary>Initial fraction of simulated motion retained each step.</summary>
    public float Damping;

    /// <summary>Energy placed into the field by each random injection.</summary>
    public float Impulse;

    /// <summary>Initial inverse probability of injecting energy on a frame.</summary>
    public int Activity;

    /// <summary>Multiplier applied before wrapping field values into palette space.</summary>
    public float PaletteScale;

    /// <summary>Weight applied to the average neighboring field value.</summary>
    public float NeighborWeight;

    /// <summary>Steady palette offset used outside a Drop approach.</summary>
    public float PaletteOffset;
}

/// <summary>Editable music-response values saved as Fluid's Sync Settings.</summary>
[Serializable]
public sealed class FluidSyncSettings
{
    /// <summary>Steady fraction of simulated motion retained each step.</summary>
    [Range(0f, 1f)] public float Damping;

    /// <summary>Energy placed into the field by each random injection.</summary>
    [Min(0f)] public float Impulse;

    /// <summary>Steady inverse probability of injecting energy on a frame.</summary>
    [Min(1)] public int Activity;

    /// <summary>Multiplier applied before wrapping field values into palette space.</summary>
    [Min(0f)] public float PaletteScale;

    /// <summary>Weight applied to the average neighboring field value.</summary>
    [Min(0f)] public float NeighborWeight;

    /// <summary>Steady palette offset used outside a Drop approach.</summary>
    [Range(0f, 1f)] public float PaletteOffset;

    /// <summary>Number of beats over which Fluid expects an approaching Drop.</summary>
    [Min(1)] public int DropApproachBeats;

    /// <summary>Damping that makes the fluid settle faster while a Drop approaches.</summary>
    [Range(0f, 1f)] public float DropDamping;

    /// <summary>Inverse injection probability intended to force an impulse at Drop onset.</summary>
    [Min(1)] public int DropForcedActivity;

    /// <summary>Extra simulation advances intended to bump the animation at Drop onset.</summary>
    [Min(0)] public int DropOnsetSimulationAdvances;

    /// <summary>Palette offset at the Waveform trough while a Drop approaches.</summary>
    [Range(0f, 1f)] public float DropPaletteOffsetAtWaveformTrough;

    /// <summary>Palette offset at the Waveform peak while a Drop approaches.</summary>
    [Range(0f, 1f)] public float DropPaletteOffsetAtWaveformPeak;

    /// <summary>Copies every Fluid Sync Setting from another value.</summary>
    public void CopyFrom(FluidSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Damping = source.Damping;
        Impulse = source.Impulse;
        Activity = source.Activity;
        PaletteScale = source.PaletteScale;
        NeighborWeight = source.NeighborWeight;
        PaletteOffset = source.PaletteOffset;
        DropApproachBeats = source.DropApproachBeats;
        DropDamping = source.DropDamping;
        DropForcedActivity = source.DropForcedActivity;
        DropOnsetSimulationAdvances = source.DropOnsetSimulationAdvances;
        DropPaletteOffsetAtWaveformTrough = source.DropPaletteOffsetAtWaveformTrough;
        DropPaletteOffsetAtWaveformPeak = source.DropPaletteOffsetAtWaveformPeak;
    }
}
