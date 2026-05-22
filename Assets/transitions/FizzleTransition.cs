using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
/// <summary>
/// Reveals destination tiles through a fixed shuffled tile order.
/// </summary>
public class FizzleTransition : TransitionBase
{
    short[] order = null;
    public override void OnStart()
    {
        buffer.Clear();
    }
    /// <summary>
  /// Builds the fixed randomized tile reveal order used by this transition instance.
  /// </summary>
  public void OnInit()
    {
        if (order == null)
        {
            order = new short[Penrose.Total];
            for (short i = 0; i < order.Length; i++)
                order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int x = Random.Range(0, i);
                short y = order[i];     //swap
                order[i] = order[x];
                order[x] = y;
            }


        }
    }

    /// <summary>
  /// Creates a fizzle transition and initializes its persistent reveal order.
  /// </summary>
  public FizzleTransition()
    {
        OnInit();
    }


      /// <summary>
  /// Reserved deactivation hook. Controller does not currently call this.
  /// </summary>
public override void OnEnd() { }

      /// <summary>
  /// Draws source and destination effects and writes the transition frame into buffer.
  /// </summary>
public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();
        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V);
    }

    /// <summary>
  /// Shared fixed-order reveal implementation for normal transitions and external blending.
  /// </summary>
  private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2)
    {
        if (order == null)
            return;

        short total = (short)(Penrose.Total * V2);

        for (int i = 0; i < Penrose.Total; i++)
        {
            if (order[i] > total)
                dest[i] = src1[i];
            else
                dest[i] = src2[i];
        }


    }

      /// <summary>
  /// Uses this transition algorithm as an external-source blender.
  /// </summary>
public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        float V2 = 0.5f;
        if (settings.Length > 0)
            V2 = settings[0];
        Draw2(dest, src1, src2, V2);
    }
    public override string Usage()
    {
        return "[ratio]";
    }


}
