// Implements the shared Build and Decay convenience calculations.
#nullable enable

using UnityEngine;

/// <summary>Shared implementation of the readable build and decay conveniences.</summary>
internal static class StockEnvelopes
{
    /// <summary>Applies the house rising shape to an already-normalized 0..1 position.</summary>
    /// <remarks>
    /// The shaping is separated from <see cref="Position"/> so a caller that derives its own total
    /// position — the approach runway of <see cref="BeforeSpan"/> — shapes it identically.
    /// </remarks>
    internal static float Rise(float position) => Mathf.SmoothStep(0f, 1f, position);

    /// <summary>Applies the house falling shape to an already-normalized 0..1 position.</summary>
    internal static float Fall(float position) => 1f - Rise(position);

    /// <summary>Returns a smooth zero-to-one value across the requested window, resting at zero.</summary>
    internal static float Build(float? elapsedBeats, float? windowBeats) =>
        Position(elapsedBeats, windowBeats) is { } progress
            ? Rise(progress)
            : 0f;

    /// <summary>Returns a smooth one-to-zero value across the requested window, resting at zero.</summary>
    internal static float Decay(float? elapsedBeats, float? windowBeats) =>
        Position(elapsedBeats, windowBeats) is { } progress
            ? Fall(progress)
            : 0f;

    /// <summary>Normalizes elapsed beats into a valid duration window.</summary>
    private static float? Position(float? elapsedBeats, float? windowBeats)
    {
        if (elapsedBeats is not { } elapsed || windowBeats is not { } window || window <= 0f)
        {
            return null;
        }

        return Mathf.Clamp01(elapsed / window);
    }
}
