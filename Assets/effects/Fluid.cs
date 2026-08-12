using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Runs a tile-neighbor diffusion simulation and colors the resulting scalar field.
/// </summary>
[EffectSyncSettings(typeof(FluidSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(FluidStandaloneSettingsAsset))]
public class Fluid : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored fraction of simulated motion retained each step for the unchanged Standalone look.</summary>
    private const float StandaloneDamping = 0.95f;

    /// <summary>Authored energy placed into the field by each random injection in Standalone Mode.</summary>
    private const float StandaloneImpulse = 1f;

    /// <summary>Authored denominator of the one-in-N energy-injection chance on a Standalone frame.</summary>
    private const int StandaloneInjectionChanceDenominator = 50;

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

    /// <summary>Authored denominator of the steady one-in-N energy-injection chance on a Synced frame.</summary>
    private const int SyncInjectionChanceDenominator = 50;

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

    /// <summary>Authored injection-chance denominator intended to force an impulse at Drop onset.</summary>
    private const int SyncDropForcedInjectionChanceDenominator = 1;

    /// <summary>Authored extra simulation advances intended to bump the animation at Drop onset.</summary>
    private const int SyncDropOnsetSimulationAdvances = 2;

    /// <summary>Authored palette offset at the Waveform trough while a Drop approaches.</summary>
    private const float SyncDropPaletteOffsetMin = 0.5f;

    /// <summary>
    /// Authored palette offset at the Waveform peak and its Standalone fallback; the non-Drop overwrite
    /// prevents that fallback from reaching Standalone rendering.
    /// </summary>
    private const float SyncDropPaletteOffsetMax = 1f;

    /// <summary>Fluid's smooth flow suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh copy of Fluid's file-local Standalone Defaults.</summary>
    public static FluidStandaloneSettings StandaloneDefaults => new()
    {
        Damping = StandaloneDamping,
        Impulse = StandaloneImpulse,
        InjectionChanceDenominator = StandaloneInjectionChanceDenominator,
        PaletteScale = StandalonePaletteScale,
        NeighborWeight = StandaloneNeighborWeight,
        PaletteOffset = StandalonePaletteOffset,
    };

    /// <summary>Resolves a fresh copy of Fluid's file-local Sync Defaults.</summary>
    public static FluidSyncSettings SyncDefaults => new FluidSyncSettings
    {
        Damping = SyncDamping,
        Impulse = SyncImpulse,
        InjectionChanceDenominator = SyncInjectionChanceDenominator,
        PaletteScale = SyncPaletteScale,
        NeighborWeight = SyncNeighborWeight,
        PaletteOffset = SyncPaletteOffset,
        DropApproachBeats = SyncDropApproachBeats,
        DropDamping = SyncDropDamping,
        DropForcedInjectionChanceDenominator = SyncDropForcedInjectionChanceDenominator,
        DropOnsetSimulationAdvances = SyncDropOnsetSimulationAdvances,
        DropPaletteOffset = new FloatRange(SyncDropPaletteOffsetMin, SyncDropPaletteOffsetMax),
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private FluidStandaloneSettings standaloneSettings = StandaloneDefaults;

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
    /// Current Synced injection-chance denominator. Synced rendering mutates and reads it, and it
    /// intentionally remains sticky across activations. Standalone rendering reads the Standalone
    /// Default instead.
    /// </summary>
    private int injectionChanceDenominator = StandaloneInjectionChanceDenominator;

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
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Fluid),
            StandaloneDefaults);
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
        // Standalone rendering reads its authored injection odds, never the Synced-mutated sticky state,
        // so a live Sync Settings tweak can never reach the Standalone look.
        int activeInjectionChanceDenominator = beatManager.IsSynced
            ? injectionChanceDenominator
            : standaloneSettings.InjectionChanceDenominator;
        // Zero is the designated success slot; the chance value controls the authored one-in-N odds.
        if (Random.Range(0, activeInjectionChanceDenominator) == 0)
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
                    injectionChanceDenominator = SyncSettings.DropForcedInjectionChanceDenominator;
                    for (int i = 0; i < SyncSettings.DropOnsetSimulationAdvances; i++)
                    {
                        AdvanceSimulation();
                    }
                }
            }
            else
            {
                fdamping = SyncSettings.Damping;
                injectionChanceDenominator = SyncSettings.InjectionChanceDenominator;
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
        FloatRange dropPaletteOffset = SyncSettings.DropPaletteOffset;
        float paletteOffset = waveform.Lerp(dropPaletteOffset.Min, dropPaletteOffset.Max);
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

/// <summary>Editable values saved as Fluid's Standalone Settings.</summary>
[Serializable]
public sealed class FluidStandaloneSettings
{
    /// <summary>Initial fraction of simulated motion retained each step.</summary>
    [Range(0f, 1f)] public float Damping;

    /// <summary>Energy placed into the field by each random injection.</summary>
    [Min(0f)] public float Impulse;

    /// <summary>Denominator of the per-frame one-in-N energy-injection chance.</summary>
    [Min(1)] public int InjectionChanceDenominator;

    /// <summary>Multiplier applied before wrapping field values into palette space.</summary>
    [Min(0f)] public float PaletteScale;

    /// <summary>Weight applied to the average neighboring field value.</summary>
    [Min(0f)] public float NeighborWeight;

    /// <summary>Steady palette offset used outside a Drop approach.</summary>
    [Range(0f, 1f)] public float PaletteOffset;

    /// <summary>Copies every Fluid Standalone Setting from another value.</summary>
    public void CopyFrom(FluidStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Damping = source.Damping;
        Impulse = source.Impulse;
        InjectionChanceDenominator = source.InjectionChanceDenominator;
        PaletteScale = source.PaletteScale;
        NeighborWeight = source.NeighborWeight;
        PaletteOffset = source.PaletteOffset;
    }
}

/// <summary>Editable music-response values saved as Fluid's Sync Settings.</summary>
[Serializable]
public sealed class FluidSyncSettings
{
    /// <summary>Steady fraction of simulated motion retained each step.</summary>
    [Range(0f, 1f)] public float Damping;

    /// <summary>Energy placed into the field by each random injection.</summary>
    [Min(0f)] public float Impulse;

    /// <summary>Denominator of the steady per-frame one-in-N energy-injection chance.</summary>
    [Min(1)] public int InjectionChanceDenominator;

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

    /// <summary>Injection-chance denominator intended to force an impulse at Drop onset.</summary>
    [Min(1)] public int DropForcedInjectionChanceDenominator;

    /// <summary>Extra simulation advances intended to bump the animation at Drop onset.</summary>
    [Min(0)] public int DropOnsetSimulationAdvances;

    /// <summary>Palette-offset endpoints interpolated from Waveform trough to peak during a Drop approach.</summary>
    public FloatRange DropPaletteOffset = new();

    /// <summary>Copies every Fluid Sync Setting from another value.</summary>
    public void CopyFrom(FluidSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Damping = source.Damping;
        Impulse = source.Impulse;
        InjectionChanceDenominator = source.InjectionChanceDenominator;
        PaletteScale = source.PaletteScale;
        NeighborWeight = source.NeighborWeight;
        PaletteOffset = source.PaletteOffset;
        DropApproachBeats = source.DropApproachBeats;
        DropDamping = source.DropDamping;
        DropForcedInjectionChanceDenominator = source.DropForcedInjectionChanceDenominator;
        DropOnsetSimulationAdvances = source.DropOnsetSimulationAdvances;
        DropPaletteOffset = new FloatRange(
            source.DropPaletteOffset.Min,
            source.DropPaletteOffset.Max,
            source.DropPaletteOffset.LowRail,
            source.DropPaletteOffset.HighRail);
    }
}
