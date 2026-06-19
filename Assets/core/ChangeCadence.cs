/// <summary>
/// Minimum beat spacing between Director-selected impact beats.
/// </summary>
public static class ChangeCadence
{
    /// <summary>Whether a selected impact beat is far enough from the previous impact.</summary>
    public static bool CanChangeAt(int selectedImpactBeat, int? previousImpactBeat, int minimumBeats)
    {
        return previousImpactBeat is not { } previous || selectedImpactBeat - previous >= minimumBeats;
    }
}
