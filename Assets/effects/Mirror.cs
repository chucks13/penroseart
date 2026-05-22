using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wraps one child effect and mirrors its buffer through Penrose mirror shape groups.
/// </summary>
public class Mirror : MixerBase
{

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
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
        mirrorList = Random.Range(0, 2) == 0 ? penrose.JsonRawData.shapes.mirror2 : penrose.JsonRawData.shapes.mirror10;
        fixCenterLineInit();

        sourceEffect = GetRandomEffect();
        var debugText = string.Empty;
        sourceEffect.Init();
        sourceEffect.RandomizeTime();
        sourceEffect.OnStart();
        // Mirror is a wrapper, so the child uses the same beat variant as the parent.
        sourceEffect.beatVariant = this.beatVariant;
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
