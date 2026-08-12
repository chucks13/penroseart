using UnityEngine;
using Random = UnityEngine.Random;
/// <summary>
/// Wipes from source to destination by projecting tile positions along a randomized direction.
/// </summary>
public class DirectionalWipe : TransitionBase
{
    // Hand-tuned DirectionalWipe Code Defaults. Keep these together so the transition
    // can be adjusted by hand without hunting through the implementation.
    private const int RunwayBeats = 4;
    private const int TailBeats = 4;
    private const TransitionShape Shape = TransitionShape.DirectionalWipe;
    private const TransitionIntensity Intensity = TransitionIntensity.Medium;
    private const float DefaultDurationSeconds = 4f;

    private const float ExternalBlendDefaultProgress = 0.5f;
    private const float ExternalBlendDefaultAngleRadians = 0f;

    private const float RandomDirectionRangeRadians = Mathf.PI * 2f;
    private const float ReactiveEdgeWidth = 0.055f;
    private const float BaseEdgeBrightnessBoost = 0.15f;
    private const float LowBandResponseGain = 2f;
    private const float MaxLowBandBrightnessBoost = 0.85f;
    private const float BaseEdgeBrightnessLift = 0.025f;
    private const float MaxLowBandBrightnessLift = 0.14f;

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
            ExternalBlendDefaultAngleRadians = ExternalBlendDefaultAngleRadians,
            DirectionalReactiveEdgeWidth = ReactiveEdgeWidth,
            DirectionalBaseEdgeBrightnessBoost = BaseEdgeBrightnessBoost,
            DirectionalLowBandResponseGain = LowBandResponseGain,
            DirectionalMaxLowBandBrightnessBoost = MaxLowBandBrightnessBoost,
            DirectionalBaseEdgeBrightnessLift = BaseEdgeBrightnessLift,
            DirectionalMaxLowBandBrightnessLift = MaxLowBandBrightnessLift,
        };
    }

    float angle;

    /// <summary>Half the Penrose bounds diagonal used to normalize every directional projection.</summary>
    float halfDiagonal;

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
        angle = Random.value * RandomDirectionRangeRadians;
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
        Vector2 diagonal = new(width, height);
        halfDiagonal = diagonal.magnitude / 2f;
    }

    /// <summary>
    /// Draws source and destination effects and writes the transition frame into buffer.
    /// </summary>
    public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();
        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, angle, EffectiveSettings);
    }

    /// <summary>
    /// Draws the shared directional wipe for normal transitions and external blending against the preserved coarse
    /// tile geometry.
    /// </summary>
    /// <param name="dest">Destination buffer for the blended frame.</param>
    /// <param name="src1">Source shown ahead of the wipe threshold.</param>
    /// <param name="src2">Source shown behind the wipe threshold.</param>
    /// <param name="V2">Normalized wipe progress.</param>
    /// <param name="Angle2">Rotation applied to the coarse tile position.</param>
    /// <param name="transitionSettings">Current settings for the reactive edge treatment.</param>
    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, float Angle2, TransitionSettings transitionSettings)
    {
        float lowBandLevel = CurrentLowBandLevel();
        float cosine = Mathf.Cos(Angle2);
        float sine = Mathf.Sin(Angle2);

        for (int i = 0; i < buffer.Length; i++)
        {
            Vector2Int position = controller.penrose.tiles[i].coarsePosition;
            float projectedPosition = position.x * cosine - position.y * sine;
            float projectedProgress = projectedPosition.Remap(-halfDiagonal, halfDiagonal, 0f, 1f);
            Color baseColor = projectedProgress >= V2 ? src1[i] : src2[i];
            float edgePresence = EdgePresence(projectedProgress, V2, transitionSettings.DirectionalReactiveEdgeWidth);
            dest[i] = ApplyLowBandEdgeBrightness(baseColor, edgePresence, lowBandLevel, transitionSettings);
        }
    }

    /// <summary>
    /// Reads the live low-band level for extra beat-reactive edge brightness.
    /// Missing OSC levels keep the base edge lift without adding a low-band pulse.
    /// </summary>
    private float CurrentLowBandLevel()
    {
        return beatManager.Levels.Smoothed.Low;
    }

    /// <summary>
    /// Returns how close a tile is to the active wipe boundary, from 0 outside the edge band to 1 on the boundary.
    /// </summary>
    public static float EdgePresence(float projectedProgress, float transitionProgress)
    {
        return EdgePresence(projectedProgress, transitionProgress, ReactiveEdgeWidth);
    }

    /// <summary>Maps distance from the wipe boundary into the configured edge band.</summary>
    private static float EdgePresence(
        float projectedProgress,
        float transitionProgress,
        float reactiveEdgeWidth)
    {
        return Mathf.Abs(projectedProgress - transitionProgress)
            .Remap(0f, reactiveEdgeWidth, 1f, 0f, clamp: true);
    }

    /// <summary>
    /// Applies a visible base edge lift plus extra live low-band brightness near the wipe edge.
    /// </summary>
    public static Color ApplyLowBandEdgeBrightness(Color color, float edgePresence, float lowBandLevel)
    {
        float edge = Mathf.Clamp01(edgePresence);
        if (edge <= 0f)
        {
            return color;
        }

        float reactiveInfluence = edge * Mathf.Clamp01(lowBandLevel * LowBandResponseGain);
        return new Color(
            BrightenChannel(color.r, edge, reactiveInfluence),
            BrightenChannel(color.g, edge, reactiveInfluence),
            BrightenChannel(color.b, edge, reactiveInfluence),
            color.a);
    }

    private static Color ApplyLowBandEdgeBrightness(
        Color color,
        float edgePresence,
        float lowBandLevel,
        TransitionSettings transitionSettings)
    {
        float edge = Mathf.Clamp01(edgePresence);
        if (edge <= 0f)
        {
            return color;
        }

        float reactiveInfluence = edge * Mathf.Clamp01(lowBandLevel * transitionSettings.DirectionalLowBandResponseGain);
        return new Color(
            BrightenChannel(color.r, edge, reactiveInfluence, transitionSettings),
            BrightenChannel(color.g, edge, reactiveInfluence, transitionSettings),
            BrightenChannel(color.b, edge, reactiveInfluence, transitionSettings),
            color.a);
    }

    private static float BrightenChannel(float channel, float edgePresence, float reactiveInfluence)
    {
        return Mathf.Clamp01(
            channel * (1f + edgePresence * BaseEdgeBrightnessBoost + reactiveInfluence * MaxLowBandBrightnessBoost)
            + edgePresence * BaseEdgeBrightnessLift
            + reactiveInfluence * MaxLowBandBrightnessLift);
    }

    private static float BrightenChannel(float channel, float edgePresence, float reactiveInfluence, TransitionSettings transitionSettings)
    {
        return Mathf.Clamp01(
            channel * (1f + edgePresence * transitionSettings.DirectionalBaseEdgeBrightnessBoost + reactiveInfluence * transitionSettings.DirectionalMaxLowBandBrightnessBoost)
            + edgePresence * transitionSettings.DirectionalBaseEdgeBrightnessLift
            + reactiveInfluence * transitionSettings.DirectionalMaxLowBandBrightnessLift);
    }

    /// <summary>
    /// Uses this transition algorithm as an external-source blender.
    /// </summary>
    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        var effectiveSettings = EffectiveSettings;
        float V2 = effectiveSettings.ExternalBlendDefaultProgress;
        float Angle2 = effectiveSettings.ExternalBlendDefaultAngleRadians;
        if (settings.Length > 0)
            V2 = settings[0];
        if (settings.Length > 1)
            Angle2 = settings[1];
        Draw2(dest, src1, src2, V2, Angle2, effectiveSettings);
    }
    /// <summary>
    /// Returns the external-blender fader argument format for this transition.
    /// </summary>
    public override string Usage()
    {
        return "[ratio] [angle (radians)]";
    }


}
