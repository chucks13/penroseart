using UnityEngine;


/// <summary>
/// Perlin-threshold transition with a colored border band.
/// </summary>
public class NoiseTransition : TransitionBase
{
    private const int RunwayBeats = 4;
    private const int TailBeats = 4;
    private const TransitionShape Shape = TransitionShape.Noise;
    private const TransitionIntensity Intensity = TransitionIntensity.High;
    private const float DefaultDurationSeconds = 4f;
    private const float ExternalBlendDefaultProgress = 0.5f;
    private const float ExternalBlendDefaultBorderHue = 0f;
    private const float NoiseScale = 0.07f;
    private const float NoiseProgressRange = 1.1f;
    private const float NoiseBorderWidth = 0.1f;

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
            ExternalBlendDefaultBorderHue = ExternalBlendDefaultBorderHue,
            NoiseScale = NoiseScale,
            NoiseProgressRange = NoiseProgressRange,
            NoiseBorderWidth = NoiseBorderWidth,
        };
    }

    private Color border;
    public override void OnStart()
    {
        buffer.Clear();
        border = Color.HSVToRGB(Random.value, 1, 1);
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
        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, border, EffectiveSettings);
    }

    /// <summary>
    /// Shared Perlin-threshold implementation for normal transitions and external blending.
    /// </summary>
    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, Color brd, TransitionSettings transitionSettings)
    {
        float v2 = V2.Remap(0f, 1f, -transitionSettings.NoiseProgressRange, transitionSettings.NoiseProgressRange);

        for (int i = 0; i < buffer.Length; i++)
        {
            float x = controller.penrose.tiles[i].center.x * transitionSettings.NoiseScale;
            float y = controller.penrose.tiles[i].center.y * transitionSettings.NoiseScale;
            float z = effectTime;

            float n = Perlin.Noise(x, y, z);
            n += v2;

            if (n > transitionSettings.NoiseBorderWidth)
                dest[i] = src2[i];
            else if (n > -transitionSettings.NoiseBorderWidth)
                dest[i] = brd;
            else
                dest[i] = src1[i];
        }
    }

    /// <summary>
    /// Uses this transition algorithm as an external-source blender.
    /// </summary>
    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        var effectiveSettings = EffectiveSettings;
        float V2 = effectiveSettings.ExternalBlendDefaultProgress;
        Color brd = Color.HSVToRGB(effectiveSettings.ExternalBlendDefaultBorderHue, 1, 1);
        if (settings.Length > 0)
            V2 = settings[0];
        if (settings.Length > 1)
            brd = Color.HSVToRGB(settings[1], 1, 1);

        Draw2(dest, src1, src2, V2, brd, effectiveSettings);
    }
    /// <summary>
    /// Returns the external-blender fader argument format for this transition.
    /// </summary>
    public override string Usage()
    {
        return "[ratio] [borderHue]";
    }




}
