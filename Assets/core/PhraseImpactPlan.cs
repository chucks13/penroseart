using System;
using System.Collections.Generic;

/// <summary>
/// Selected impact beats for one Phrase Window.
/// </summary>
public readonly struct PhraseImpactPlan
{
    public readonly int PhraseStartBeat;
    public readonly int PhraseEndBeat;
    public readonly int[] ImpactBeats;

    private PhraseImpactPlan(int phraseStartBeat, int phraseEndBeat, int[] impactBeats)
    {
        PhraseStartBeat = phraseStartBeat;
        PhraseEndBeat = phraseEndBeat;
        ImpactBeats = impactBeats;
    }

    /// <summary>
    /// Randomly selects eligible interior Phase Boundaries and always includes the phrase boundary.
    /// </summary>
    public static PhraseImpactPlan Build(
        PhraseWindow window,
        int currentBeat,
        Func<int, bool> canChangeAtBeat,
        Func<int, int, int> randomRange)
    {
        var futureInteriorSlots = new List<int>();
        foreach (var slotBeat in window.ImpactSlotsAfter(currentBeat))
        {
            if (slotBeat < window.EndBeat && canChangeAtBeat(slotBeat))
            {
                futureInteriorSlots.Add(slotBeat);
            }
        }

        var selectedTargets = new List<int>();
        var interiorTransitionCount = futureInteriorSlots.Count > 0
            ? randomRange(0, futureInteriorSlots.Count + 1)
            : 0;
        for (var i = 0; i < interiorTransitionCount; i++)
        {
            var chosenIndex = randomRange(0, futureInteriorSlots.Count);
            selectedTargets.Add(futureInteriorSlots[chosenIndex]);
            futureInteriorSlots.RemoveAt(chosenIndex);
        }

        selectedTargets.Add(window.EndBeat);
        selectedTargets.Sort();
        return new PhraseImpactPlan(window.StartBeat, window.EndBeat, selectedTargets.ToArray());
    }
}
