using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
/// <summary>
/// Radial iris transition that expands or contracts from the center.
/// </summary>
public class IrisTransition : TransitionBase
{
    private const int RunwayBeats = 4;
    private const int TailBeats = 4;
    private const TransitionShape Shape = TransitionShape.Iris;
    private const TransitionIntensity Intensity = TransitionIntensity.High;
    private const float DefaultDurationSeconds = 4f;
    private const float ExternalBlendDefaultProgress = 0.5f;
    private const float ExternalBlendDefaultDirection = 0f;

    protected override TransitionSettings BuildCodeDefaults()
    {
        return new TransitionSettings
        {
            RunwayBeats = RunwayBeats,
            TailBeats = TailBeats,
            Shape = Shape,
            Intensity = Intensity,
            DefaultDurationSeconds = DefaultDurationSeconds,
            ExternalBlendDefaultProgress = ExternalBlendDefaultProgress,
            ExternalBlendDefaultDirection = ExternalBlendDefaultDirection,
        };
    }

    int direction;
    float diagonalsize;

    /// <summary>
    /// Performs one-time transition setup after reflection creates this instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        CacheGeometry();
    }

    /// <summary>
    /// Initializes per-run transition state before effect-to-effect blending begins.
    /// </summary>
    public override void OnStart()
    {
        buffer.Clear();
        direction = Random.Range(0, 2);
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Cache Penrose bounds once so transition-as-blender usage does not depend
    /// on a previous transition activation calling OnStart().
    /// </summary>
    private void CacheGeometry()
    {
        var width = (int)controller.penrose.Bounds.size.x.Round();
        var height = (int)controller.penrose.Bounds.size.y.Round();
        Vector2 diagonal = new Vector2(width, height);
        diagonalsize = diagonal.magnitude / 2f;
    }

    /// <summary>
    /// Draws source and destination effects and writes the transition frame into buffer.
    /// </summary>
    public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();

        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, direction);
    }

    /// <summary>
    /// Shared radial iris implementation for normal transitions and external blending.
    /// </summary>
    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, int dir)
    {
        Color[] a1 = src1;
        Color[] b1 = src2;
        float v1 = V2;

        if (dir == 0)
        {
            a1 = src2;
            b1 = src1;
            v1 = 1f - V2;
        }

        float size = v1 * diagonalsize;
        size = size * size;             // squared is faster

        for (int i = 0; i < buffer.Length; i++)
        {
            float dist = controller.penrose.tiles[i].position.sqrMagnitude;

            if (dist > size)
            {
                dest[i] = a1[i];
            }
            else
            {
                dest[i] = b1[i];
            }
        }

    }
    /// <summary>
    /// Uses this transition algorithm as an external-source blender.
    /// </summary>
    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        var effectiveSettings = EffectiveSettings;
        float V2 = effectiveSettings.ExternalBlendDefaultProgress;
        int d = effectiveSettings.ExternalBlendDefaultDirection > 0.5f ? 1 : 0;
        if (settings.Length > 0)
            V2 = settings[0];
        if (settings.Length > 1)
            d = settings[1] > 0.5f ? 1 : 0;
        Draw2(dest, src1, src2, V2, d);
    }
    /// <summary>
    /// Returns the external-blender fader argument format for this transition.
    /// </summary>
    public override string Usage()
    {
        return "[ratio] [direction (0,1)]";
    }



}
