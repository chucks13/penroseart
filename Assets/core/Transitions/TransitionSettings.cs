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
    [Min(0)] public int RunwayBeats = 4;
    [Min(0)] public int TailBeats;
    public TransitionShape Shape = TransitionShape.Blend;
    public TransitionIntensity Intensity = TransitionIntensity.Subtle;
    [Min(0.01f)] public float DefaultDurationSeconds = 4f;

    /// <summary>Total authored A-to-B transition duration in beats.</summary>
    public int DurationBeats => RunwayBeats + TailBeats;

    /// <summary>Whether Runway plus Tail satisfies the authoring duration contract.</summary>
    public bool HasValidDuration => IsValidDuration(RunwayBeats, TailBeats);

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

    /// <summary>Returns whether the supplied Runway and Tail satisfy the duration contract.</summary>
    public static bool IsValidDuration(int runwayBeats, int tailBeats)
    {
        return runwayBeats >= 0
            && tailBeats >= 0
            && runwayBeats + tailBeats <= TransitionRepertoire.MaxDurationBeats;
    }

    /// <summary>Message suitable for editor feedback when Runway plus Tail is invalid.</summary>
    public static string DurationValidationMessage(int runwayBeats, int tailBeats)
    {
        var durationBeats = runwayBeats + tailBeats;
        return $"Runway and Tail must be non-negative and their sum must be at most {TransitionRepertoire.MaxDurationBeats} beats; current duration is {durationBeats} beats.";
    }

    /// <summary>Throws when the saved Runway and Tail cannot become a Director-facing repertoire.</summary>
    public void ValidateDuration()
    {
        if (!HasValidDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TailBeats),
                TailBeats,
                DurationValidationMessage(RunwayBeats, TailBeats));
        }
    }

    /// <summary>Builds the Director-facing repertoire contract from the saved authoring values.</summary>
    public TransitionRepertoire ToRepertoire()
    {
        ValidateDuration();
        return TransitionRepertoire.FromRunwayAndTail(
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
        RunwayBeats = repertoire.RunwayBeats;
        TailBeats = repertoire.TailBeats;
        Shape = repertoire.Shape;
        Intensity = repertoire.Intensity;
        DefaultDurationSeconds = repertoire.DefaultDurationSeconds;
    }
}
