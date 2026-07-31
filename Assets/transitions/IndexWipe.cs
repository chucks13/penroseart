using UnityEngine;
/// <summary>
/// Wipes by raw tile index order rather than geometric position.
/// </summary>
public class IndexWipe : TransitionBase
{
    private const int RunwayBeats = 4;
    private const int TailBeats = 0;
    private const TransitionShape Shape = TransitionShape.IndexWipe;
    private const TransitionIntensity Intensity = TransitionIntensity.Medium;
    private const float DefaultDurationSeconds = 4f;
    private const float ExternalBlendDefaultProgress = 0.5f;

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
        };
    }

    /// <summary>
    /// Initializes per-run transition state before effect-to-effect blending begins.
    /// </summary>
    public override void OnStart() { buffer.Clear(); }

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
    /// Shared raw-index wipe implementation for normal transitions and external blending.
    /// </summary>
    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2)
    {
        var total = (int)(Penrose.Total * V2);

        for (int i = total; i < Penrose.Total; i++)
        {
            dest[i] = src1[i];
        }

        for (int j = 0; j < total; j++)
        {
            dest[j] = src2[j];
        }
    }
    /// <summary>
    /// Uses this transition algorithm as an external-source blender.
    /// </summary>
    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        //        OnInit();
        float V2 = EffectiveSettings.ExternalBlendDefaultProgress;
        if (settings.Length > 0)
            V2 = settings[0];
        Draw2(dest, src1, src2, V2);
    }
    public override string Usage()
    {
        return "[ratio]";
    }



}