// Implements the shared Build and Decay convenience calculations.
#nullable enable

using UnityEngine;

/// <summary>Shared implementation of the readable build and decay conveniences.</summary>
internal static class StockEnvelopes
{
    /// <summary>Returns a smooth zero-to-one value across the requested window.</summary>
    internal static float Build(float? elapsedBeats, float? windowBeats) =>
        Position(elapsedBeats, windowBeats) is { } progress
            ? Mathf.SmoothStep(0f, 1f, progress)
            : 0f;

    /// <summary>Returns a smooth one-to-zero value across the requested window.</summary>
    internal static float Decay(float? elapsedBeats, float? windowBeats) =>
        Position(elapsedBeats, windowBeats) is { } progress
            ? 1f - Mathf.SmoothStep(0f, 1f, progress)
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
