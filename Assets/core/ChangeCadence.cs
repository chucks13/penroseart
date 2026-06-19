/// <summary>
/// Minimum beat spacing between Director-selected Phase Boundaries.
/// </summary>
public static class ChangeCadence
{
    /// <summary>Whether a selected Phase Boundary is far enough from the previous selected boundary.</summary>
    public static bool CanChangeAt(int selectedPhaseBoundary, int? previousSelectedPhaseBoundary, int minimumBeats)
    {
        return previousSelectedPhaseBoundary is not { } previous
            || selectedPhaseBoundary - previous >= minimumBeats;
    }
}
