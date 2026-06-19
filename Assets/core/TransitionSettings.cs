using System;
using UnityEngine;

/// <summary>
/// Saved authoring values for a Transition. Code Defaults create and restore this data;
/// runtime reads the saved asset when present and falls back to Code Defaults otherwise.
/// </summary>
[Serializable]
public sealed class TransitionSettings
{
    [Header("Transition Repertoire")]
    public Repertoire Tags = Repertoire.None;
    [Min(1)] public int RunwayBeats = 4;
    [Min(0)] public int TailBeats;
    public TransitionShape Shape = TransitionShape.Blend;
    public TransitionIntensity Intensity = TransitionIntensity.Subtle;
    [Min(0.01f)] public float DefaultDurationSeconds = 4f;

    [Header("External Blend Defaults")]
    [Range(0f, 1f)] public float ExternalBlendDefaultProgress = 0.5f;
    public float ExternalBlendDefaultAngleRadians;
    [Range(0f, 1f)] public float ExternalBlendDefaultDirection;
    [Range(0f, 1f)] public float ExternalBlendDefaultBorderHue;

    [Header("Directional Wipe Visual Defaults")]
    [Min(0.001f)] public float DirectionalReactiveEdgeWidth = 0.055f;
    [Min(0f)] public float DirectionalBaseEdgeBrightnessBoost = 0.15f;
    [Min(0f)] public float DirectionalLowBandResponseGain = 2f;
    [Min(0f)] public float DirectionalMaxLowBandBrightnessBoost = 0.85f;
    [Min(0f)] public float DirectionalBaseEdgeBrightnessLift = 0.025f;
    [Min(0f)] public float DirectionalMaxLowBandBrightnessLift = 0.14f;

    [Header("Noise Visual Defaults")]
    [Min(0.001f)] public float NoiseScale = 0.07f;
    [Min(0f)] public float NoiseProgressRange = 1.1f;
    [Min(0f)] public float NoiseBorderWidth = 0.1f;

    /// <summary>Builds the Director-facing repertoire contract from the saved authoring values.</summary>
    public TransitionRepertoire ToRepertoire()
    {
        return TransitionRepertoire.FromRunwayAndTail(
            Tags,
            RunwayBeats,
            TailBeats,
            Shape,
            Intensity,
            DefaultDurationSeconds);
    }

    /// <summary>Creates settings initialized from an existing repertoire contract.</summary>
    public static TransitionSettings FromRepertoire(TransitionRepertoire repertoire)
    {
        var settings = new TransitionSettings();
        settings.CopyRepertoireFrom(repertoire);
        return settings;
    }

    /// <summary>Copies all saved authoring values from another settings object.</summary>
    public void CopyFrom(TransitionSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Tags = source.Tags;
        RunwayBeats = source.RunwayBeats;
        TailBeats = source.TailBeats;
        Shape = source.Shape;
        Intensity = source.Intensity;
        DefaultDurationSeconds = source.DefaultDurationSeconds;
        ExternalBlendDefaultProgress = source.ExternalBlendDefaultProgress;
        ExternalBlendDefaultAngleRadians = source.ExternalBlendDefaultAngleRadians;
        ExternalBlendDefaultDirection = source.ExternalBlendDefaultDirection;
        ExternalBlendDefaultBorderHue = source.ExternalBlendDefaultBorderHue;
        DirectionalReactiveEdgeWidth = source.DirectionalReactiveEdgeWidth;
        DirectionalBaseEdgeBrightnessBoost = source.DirectionalBaseEdgeBrightnessBoost;
        DirectionalLowBandResponseGain = source.DirectionalLowBandResponseGain;
        DirectionalMaxLowBandBrightnessBoost = source.DirectionalMaxLowBandBrightnessBoost;
        DirectionalBaseEdgeBrightnessLift = source.DirectionalBaseEdgeBrightnessLift;
        DirectionalMaxLowBandBrightnessLift = source.DirectionalMaxLowBandBrightnessLift;
        NoiseScale = source.NoiseScale;
        NoiseProgressRange = source.NoiseProgressRange;
        NoiseBorderWidth = source.NoiseBorderWidth;
    }

    /// <summary>Returns an independent copy so callers can mutate without changing the source.</summary>
    public TransitionSettings Clone()
    {
        var clone = new TransitionSettings();
        clone.CopyFrom(this);
        return clone;
    }

    private void CopyRepertoireFrom(TransitionRepertoire repertoire)
    {
        Tags = repertoire.Tags;
        RunwayBeats = repertoire.RunwayBeats;
        TailBeats = repertoire.TailBeats;
        Shape = repertoire.Shape;
        Intensity = repertoire.Intensity;
        DefaultDurationSeconds = repertoire.DefaultDurationSeconds;
    }
}
