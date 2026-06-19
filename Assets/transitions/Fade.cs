using UnityEngine;
/// <summary>
/// Straight linear crossfade between source and destination effects.
/// </summary>
public class Fade : TransitionBase
{
    private const global::Repertoire DefaultTags = global::Repertoire.None;
    private const int RunwayBeats = 4;
    private const int TailBeats = 0;
    private const TransitionShape Shape = TransitionShape.Blend;
    private const TransitionIntensity Intensity = TransitionIntensity.Subtle;
    private const float DefaultDurationSeconds = 4f;
    private const float ExternalBlendDefaultProgress = 0.5f;

    protected override TransitionSettings BuildCodeDefaults()
    {
        return new TransitionSettings
        {
            Tags = DefaultTags,
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
    public override void OnEnd() { }

    /// <summary>
    /// Draws source and destination effects and writes the transition frame into buffer.
    /// </summary>
    public override void Draw()
    {

        controller.effects[A].Draw();
        controller.effects[B].Draw();
        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, D);

    }

    /// <summary>
    /// Shared linear blend implementation for normal transitions and external blending.
    /// </summary>
    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, float D2)
    {

        for (int i = 0; i < buffer.Length; i++)
        {
            var colorA = src1[i] * D2;
            var colorB = src2[i] * V2;
            dest[i] = colorA + colorB;
        }
    }
    /// <summary>
    /// Uses this transition algorithm as an external-source blender.
    /// </summary>
    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        float V2 = EffectiveSettings.ExternalBlendDefaultProgress;
        if (settings.Length > 0)
            V2 = settings[0];
        Draw2(dest, src1, src2, V2, 1f - V2);
    }
    public override string Usage()
    {
        return "[ratio]";
    }


}