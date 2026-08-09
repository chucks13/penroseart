using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Wraps one child effect and mirrors its buffer through Penrose mirror shape groups.
/// </summary>
[EffectSyncSettings(typeof(MirrorSyncSettingsAsset))]
public class Mirror : MixerBase
{
    // Standalone Defaults

    /// <summary>Authored inclusive minimum for Mirror's layout Roll in Standalone Mode.</summary>
    private const int StandaloneMirrorLayoutRollMinInclusive = 0;

    /// <summary>Authored exclusive maximum for Mirror's layout Roll in Standalone Mode.</summary>
    private const int StandaloneMirrorLayoutRollMaxExclusive = 2;

    // Sync Defaults

    /// <summary>Authored inclusive minimum for Mirror's layout Roll in Synced Mode.</summary>
    private const int SyncMirrorLayoutRollMinInclusive = 0;

    /// <summary>Authored exclusive maximum for Mirror's layout Roll in Synced Mode.</summary>
    private const int SyncMirrorLayoutRollMaxExclusive = 2;

    /// <summary>
    /// Mirror hands all meta effects to its child, so it reports every Fill, Drop, and Energy capability.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Mirror's Standalone Defaults.</summary>
    public static MirrorStandaloneSettings StandaloneSettings => new MirrorStandaloneSettings(
        StandaloneMirrorLayoutRollMinInclusive,
        StandaloneMirrorLayoutRollMaxExclusive);

    /// <summary>Resolves a fresh copy of Mirror's file-local Sync Defaults.</summary>
    public static MirrorSyncSettings SyncDefaults => new MirrorSyncSettings
    {
        MirrorLayoutRollMinInclusive = SyncMirrorLayoutRollMinInclusive,
        MirrorLayoutRollMaxExclusive = SyncMirrorLayoutRollMaxExclusive,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private MirrorStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private MirrorSyncSettings SyncSettings { get; set; } = SyncDefaults;

    private EffectBase sourceEffect;
    private int[] mirrorList;
    private int[] centerline;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = string.Empty;
        debugText += $"{sourceEffect.Name}";

        return debugText;
    }
    /*
     * The original mirror data for mirror 2 was missing tiles in the very center.  This caused a hole
     * in the effect, because even though the data is unchanged, it never gets copied from the
     * original buffer.  This code finds those tiles and makes a patch array.
     * it is know ahead of time that there are 900 tiles in the display
     * and 8 are missing.  It is true that this patch doesnt need to be drawn
     * on mirror 10, but its only 8 tiles, so no special check it made.
     */
    private void fixCenterLineInit()
    {
        centerline = new int[8];
        int y = 0;
        for (int x = 0; x < 900; x++)
        {
            if (y == centerline.Length)
                break;
            int groupcount = mirrorList[0];     // how many copies
            bool used = false;                                    // Draw the mirrors
            for (int i = 0; i < groupcount; i++)
            {
                int groupPointer = mirrorList[1 + i];
                int groupsize = mirrorList[groupPointer];
                for (int j = 0; j < groupsize; j++)
                {
                    if (mirrorList[groupPointer + 1 + j] == x)
                    {
                        used = true;
                        break;
                    }
                }
            }
            if (!used)
                centerline[y++] = x;
        }
    }
    /// <summary>
    /// Patches centerline tiles omitted by mirror shape data before mirror replication.
    /// </summary>
    private void fixCenterLineDraw()
    {
        for (int i = 0; i < centerline.Length; i++)
        {
            int j = centerline[i];
            buffer[j] = sourceEffect.buffer[j];
        }

    }


    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Resolves Effect Settings and initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Mirror),
            SyncDefaults);

        waveform = waveforms.Random();
        int mirrorLayoutRollMinInclusive = beatManager.IsSynced
            ? SyncSettings.MirrorLayoutRollMinInclusive
            : standaloneSettings.MirrorLayoutRollMinInclusive;
        int mirrorLayoutRollMaxExclusive = beatManager.IsSynced
            ? SyncSettings.MirrorLayoutRollMaxExclusive
            : standaloneSettings.MirrorLayoutRollMaxExclusive;
        mirrorList = Random.Range(mirrorLayoutRollMinInclusive, mirrorLayoutRollMaxExclusive) == 0
            ? penrose.Layout.shapes.mirror2
            : penrose.Layout.shapes.mirror10;
        fixCenterLineInit();

        sourceEffect = GetRandomEffect();
        var debugText = string.Empty;
        sourceEffect.Init();
        sourceEffect.RandomizeTime();
        sourceEffect.OnStart();
        // Mirror is a wrapper, so the child uses the same public Waveform configuration as the parent.
        sourceEffect.waveform = waveform;
        debugText += $"{sourceEffect.Name}";

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
        sourceEffect.UpdateTime();
        // Reassert unison after UpdateTime because the child may acquire a new Waveform on a Grid wrap.
        sourceEffect.waveform = waveform;
        sourceEffect.Draw();

        int groupcount = mirrorList[0];     // how many copies
        // fix missing verticle column
        fixCenterLineDraw();
        // Draw the mirrors
        for (int i = 0; i < groupcount; i++)
        {
            int groupPointer = mirrorList[1 + i];
            int groupsize = mirrorList[groupPointer];
            Color tileColor = sourceEffect.buffer[mirrorList[groupPointer + 1]];
            for (int j = 0; j < groupsize; j++)
            {
                buffer[mirrorList[groupPointer + 1 + j]] = tileColor;
            }
        }
    }

}

/// <summary>The non-editable Standalone Settings that reproduce Mirror's authored no-music look.</summary>
public sealed class MirrorStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from Mirror's file-local defaults.</summary>
    public MirrorStandaloneSettings(int mirrorLayoutRollMinInclusive, int mirrorLayoutRollMaxExclusive)
    {
        MirrorLayoutRollMinInclusive = mirrorLayoutRollMinInclusive;
        MirrorLayoutRollMaxExclusive = mirrorLayoutRollMaxExclusive;
    }

    /// <summary>Inclusive minimum supplied to the per-activation mirror-layout Roll.</summary>
    public int MirrorLayoutRollMinInclusive;

    /// <summary>Exclusive maximum supplied to the per-activation mirror-layout Roll.</summary>
    public int MirrorLayoutRollMaxExclusive;
}

/// <summary>Editable Synced Mode values saved as Mirror's Sync Settings.</summary>
[Serializable]
public sealed class MirrorSyncSettings
{
    /// <summary>Inclusive minimum supplied to the per-activation mirror-layout Roll.</summary>
    [Min(0)] public int MirrorLayoutRollMinInclusive;

    /// <summary>Exclusive maximum supplied to the per-activation mirror-layout Roll.</summary>
    [Min(1)] public int MirrorLayoutRollMaxExclusive;

    /// <summary>Copies every Mirror Sync Setting from another value.</summary>
    public void CopyFrom(MirrorSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        MirrorLayoutRollMinInclusive = source.MirrorLayoutRollMinInclusive;
        MirrorLayoutRollMaxExclusive = source.MirrorLayoutRollMaxExclusive;
    }
}
