using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Creates a mode-authored random count of child Effects and additively mixes their buffers.
/// In Standalone Mode, the fixed range preserves the original two-or-three-child roll.
/// </summary>
[EffectSyncSettings(typeof(RandomEffectsMixerSyncSettingsAsset))]
public class RandomEffectsMixer : MixerBase
{
    // Standalone Defaults

    /// <summary>Authored minimum child-count argument for the unchanged Standalone roll.</summary>
    private const int StandaloneChildCountMin = 2;

    /// <summary>Authored exclusive maximum child-count argument for the unchanged Standalone roll.</summary>
    private const int StandaloneChildCountMaxExclusive = 4;

    /// <summary>Authored additive mix gain for the unchanged Standalone look.</summary>
    private const float StandaloneMixGain = 2f;

    // Sync Defaults

    /// <summary>Authored minimum child-count argument for the Synced roll.</summary>
    private const int SyncChildCountMin = 2;

    /// <summary>Authored exclusive maximum child-count argument for the Synced roll.</summary>
    private const int SyncChildCountMaxExclusive = 4;

    /// <summary>Authored additive mix gain for Synced Mode.</summary>
    private const float SyncMixGain = 2f;

    // This mixer is intentionally not beat aware. It is meant to be used with beat-aware effects.

    /// <summary>RandomEffectsMixer's shifting mix accents Fills and suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of RandomEffectsMixer's Standalone Defaults.</summary>
    public static RandomEffectsMixerStandaloneSettings StandaloneSettings =>
        new RandomEffectsMixerStandaloneSettings(
            StandaloneChildCountMin,
            StandaloneChildCountMaxExclusive,
            StandaloneMixGain);

    /// <summary>Resolves a fresh copy of RandomEffectsMixer's file-local Sync Defaults.</summary>
    public static RandomEffectsMixerSyncSettings SyncDefaults => new RandomEffectsMixerSyncSettings
    {
        ChildCountMin = SyncChildCountMin,
        ChildCountMaxExclusive = SyncChildCountMaxExclusive,
        MixGain = SyncMixGain,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private RandomEffectsMixerStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private RandomEffectsMixerSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The child Effects owned by the current activation.</summary>
    private EffectBase[] effects;

    /// <summary>The child Effect count rolled for the current activation.</summary>
    private int total;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < total; i++)
        {
            debugText += (i < total - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        return debugText;
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>Resolves this activation's Effect Settings, then creates and starts independent child Effects.</summary>
    /// <remarks>
    /// Settings resolution consumes no <c>UnityEngine.Random</c>, so the child count remains
    /// the first roll and every child acquisition and activation keeps its original Random call order.
    /// </remarks>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(RandomEffectsMixer),
            SyncDefaults);

        int childCountMin = beatManager.IsSynced
            ? SyncSettings.ChildCountMin
            : standaloneSettings.ChildCountMin;
        int childCountMaxExclusive = beatManager.IsSynced
            ? SyncSettings.ChildCountMaxExclusive
            : standaloneSettings.ChildCountMaxExclusive;

        effects = new EffectBase[Random.Range(childCountMin, childCountMaxExclusive)];
        total = effects.Length;

        var debugText = string.Empty;
        for (var i = 0; i < total; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].RandomizeTime();
            effects[i].Init();
            effects[i].OnStart();
            // Passive: allow children to use the beat independently
            debugText += (i < total - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        controller.debugText.text = debugText;
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        for (int i = 0; i < total; i++)
        {
            effects[i].UpdateTime();
            effects[i].Draw();
        }

        float mixGain = beatManager.IsSynced
            ? SyncSettings.MixGain
            : standaloneSettings.MixGain;
        float percent = (1f / total) * mixGain;

        for (int i = 0; i < buffer.Length; i++)
        {
            float r = 0f, g = 0f, b = 0f;
            for (int j = 0; j < total; j++)
            {
                r += effects[j].buffer[i].r * percent;
                g += effects[j].buffer[i].g * percent;
                b += effects[j].buffer[i].b * percent;
            }

            buffer[i] = new Color(r, g, b, 1f);
        }
    }
}

/// <summary>The non-editable Standalone Settings that reproduce RandomEffectsMixer's authored no-music look.</summary>
public sealed class RandomEffectsMixerStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from RandomEffectsMixer's file-local defaults.</summary>
    public RandomEffectsMixerStandaloneSettings(int childCountMin, int childCountMaxExclusive, float mixGain)
    {
        ChildCountMin = childCountMin;
        ChildCountMaxExclusive = childCountMaxExclusive;
        MixGain = mixGain;
    }

    /// <summary>Minimum child-count argument used by the activation roll.</summary>
    public int ChildCountMin;

    /// <summary>Exclusive maximum child-count argument used by the activation roll; the roll never deals this count itself.</summary>
    public int ChildCountMaxExclusive;

    /// <summary>Additive gain applied after normalizing the mixed child buffers.</summary>
    public float MixGain;
}

/// <summary>Serializable musical-response settings used by RandomEffectsMixer in Synced Mode.</summary>
[Serializable]
public sealed class RandomEffectsMixerSyncSettings
{
    /// <summary>Minimum child-count argument used by the activation roll.</summary>
    [Min(1)] public int ChildCountMin;

    /// <summary>Exclusive maximum child-count argument used by the activation roll; the roll never deals this count itself.</summary>
    [Min(1)] public int ChildCountMaxExclusive;

    /// <summary>Additive gain applied after normalizing the mixed child buffers.</summary>
    [Min(0f)] public float MixGain;

    /// <summary>Copies every RandomEffectsMixer Sync Setting from another value.</summary>
    public void CopyFrom(RandomEffectsMixerSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ChildCountMin = source.ChildCountMin;
        ChildCountMaxExclusive = source.ChildCountMaxExclusive;
        MixGain = source.MixGain;
    }
}
