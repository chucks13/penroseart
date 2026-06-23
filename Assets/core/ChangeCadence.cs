/// <summary>
/// Minimum beat spacing between Director-selected Phase Boundaries.
/// </summary>
public static class ChangeCadence
{
    /// <summary>Whether a Cue Mark is far enough from the previous Cue Mark.</summary>
    public static bool CanChangeAt(int cueMarkBeat, int? previousCueMarkBeat, int minimumBeats)
    {
        return previousCueMarkBeat is not { } previous
            || cueMarkBeat - previous >= minimumBeats;
    }
}
