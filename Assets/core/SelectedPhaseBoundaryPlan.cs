using System;
using System.Collections.Generic;

/// <summary>
/// Selected Phase Boundary plan for one Phrase Window.
/// </summary>
public readonly struct SelectedPhaseBoundaryPlan
{
    /// <summary>Absolute beat where the planned Phrase Window starts.</summary>
    public readonly int PhraseStartBeat;

    /// <summary>Absolute beat where the planned Phrase Window ends.</summary>
    public readonly int PhraseEndBeat;

    /// <summary>Total length of the planned Phrase Window in beats.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>Selected Phase Boundaries where the Director may target transitions.</summary>
    public readonly int[] SelectedPhaseBoundaries;

    private SelectedPhaseBoundaryPlan(
        int phraseStartBeat,
        int phraseEndBeat,
        int phraseLengthBeats,
        int[] selectedPhaseBoundaries)
    {
        PhraseStartBeat = phraseStartBeat;
        PhraseEndBeat = phraseEndBeat;
        PhraseLengthBeats = phraseLengthBeats;
        SelectedPhaseBoundaries = selectedPhaseBoundaries;
    }

    /// <summary>
    /// Randomly selects eligible interior Phase Boundaries and always includes the phrase boundary.
    /// </summary>
    public static SelectedPhaseBoundaryPlan Build(
        PhraseWindow window,
        int currentBeat,
        Func<int, bool> canChangeAtBeat,
        Func<int, int, int> randomRange)
    {
        var eligibleInteriorPhaseBoundaries = new List<int>();
        foreach (var phaseBoundary in window.PhaseBoundariesAfter(currentBeat))
        {
            if (phaseBoundary < window.EndBeat && canChangeAtBeat(phaseBoundary))
            {
                eligibleInteriorPhaseBoundaries.Add(phaseBoundary);
            }
        }

        var selectedPhaseBoundaries = new List<int>();
        var interiorBoundaryCount = eligibleInteriorPhaseBoundaries.Count > 0
            ? randomRange(0, eligibleInteriorPhaseBoundaries.Count + 1)
            : 0;
        for (var i = 0; i < interiorBoundaryCount; i++)
        {
            var chosenIndex = randomRange(0, eligibleInteriorPhaseBoundaries.Count);
            selectedPhaseBoundaries.Add(eligibleInteriorPhaseBoundaries[chosenIndex]);
            eligibleInteriorPhaseBoundaries.RemoveAt(chosenIndex);
        }

        selectedPhaseBoundaries.Add(window.EndBeat);
        selectedPhaseBoundaries.Sort();
        return new SelectedPhaseBoundaryPlan(
            window.StartBeat,
            window.EndBeat,
            window.LengthBeats,
            selectedPhaseBoundaries.ToArray());
    }

    /// <summary>
    /// Returns whether this plan belongs to the supplied Phrase Window's exact timing identity.
    /// </summary>
    public bool Matches(PhraseWindow window)
    {
        return PhraseStartBeat == window.StartBeat
            && PhraseEndBeat == window.EndBeat
            && PhraseLengthBeats == window.LengthBeats;
    }
}
