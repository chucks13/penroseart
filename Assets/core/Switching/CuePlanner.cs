using System;

/// <summary>Per-beat answer to "may a cue for this Transition's beat plan fire on this beat?"</summary>
public enum CueTimingVerdict
{
    /// <summary>No cue should be issued on this beat.</summary>
    Wait,

    /// <summary>The beat is inside the cue window and cadence permits the cue.</summary>
    Cue,

    /// <summary>The beat is inside the cue window, but the minimum-change cadence blocks it.</summary>
    BlockedByCadence
}

/// <summary>
/// Director-owned cue planner: interprets one live rhythm snapshot (<see cref="OnAirTimingInput"/>) into
/// the Director-facing <see cref="TimingFrame"/>. Owns Cue Sheet derivation, the Cue Mark cursor,
/// change-cadence memory, and substantial beat-rewind handling.
///
/// With OSC schema v2 the wire always broadcasts current and next Phrase in sync mode, so the planner
/// plans strictly off Track Phase: it builds a fresh Cue Sheet on every Phrase change (same-length
/// turnover included) and never fabricates or coasts a grid. The pass-local cue/cadence memory is owned
/// outright — fed by plain data, never a reference back to the timing source.
/// </summary>
public sealed class CuePlanner
{
    private readonly Func<int, int, int> randomRange;

    private int lastBeat = -1;
    private readonly CueSheetCursor cueSheet = new CueSheetCursor();

    // Pass-local cue/cadence memory, owned outright (no Director round-trip). lastChangeBeat is the
    // change cadence memory the Director also queries and marks (cue commits and manual ShowNow).
    private int lastCueBeat = -1;
    private int lastChangeBeat = int.MinValue;

    private readonly struct ResolvedTimingTarget
    {
        public readonly TimingFrameSource Source;
        public readonly int CueMarkBeat;
        public readonly bool HasPhraseWindow;
        public readonly PhraseWindow PhraseWindow;

        public ResolvedTimingTarget(
            TimingFrameSource source,
            int cueMarkBeat,
            bool hasPhraseWindow,
            PhraseWindow phraseWindow)
        {
            Source = source;
            CueMarkBeat = cueMarkBeat;
            HasPhraseWindow = hasPhraseWindow;
            PhraseWindow = phraseWindow;
        }
    }

    private sealed class CueSheetCursor
    {
        private CueSheet sheet;
        private int phraseStartBeat = -1;
        private int index;

        public bool HasSheet { get; private set; }

        /// <summary>Absolute beat the current sheet's Phrase starts on, or -1 when no sheet is held.</summary>
        public int PhraseStartBeat => phraseStartBeat;

        public int PhraseEndBeat => phraseStartBeat + sheet.PhraseLengthBeats;

        /// <summary>
        /// Whether the held sheet already covers this exact Phrase Window (same start and length). A change
        /// in either — a new Phrase start or a same-start length change — is a Phrase change that needs a
        /// fresh sheet.
        /// </summary>
        public bool CoversWindow(PhraseWindow phraseWindow) =>
            HasSheet && phraseStartBeat == phraseWindow.StartBeat && PhraseEndBeat == phraseWindow.EndBeat;

        public void Reset()
        {
            HasSheet = false;
            sheet = default;
            phraseStartBeat = -1;
            index = 0;
        }

        public void Replace(CueSheet cueSheet, PhraseWindow phraseWindow)
        {
            HasSheet = true;
            sheet = cueSheet;
            phraseStartBeat = phraseWindow.StartBeat;
            index = 0;
        }

        public void RewindCursor()
        {
            index = 0;
        }

        /// <summary>
        /// The sheet's own window while it still has planning to serve. A sheet whose mandatory end
        /// was already consumed no longer drives; the next window (live or look-ahead) takes over.
        /// </summary>
        public bool TryGetActivePhraseWindow(int beat, int? consumedCueMarkBeat, out PhraseWindow phraseWindow)
        {
            phraseWindow = default;
            return HasSheet
                && !IsConsumedThroughPhraseEnd(consumedCueMarkBeat)
                && PhraseEndBeat >= beat
                && PhraseWindow.TryFromStartAndLength(phraseStartBeat, sheet.PhraseLengthBeats, out phraseWindow);
        }

        public void AdvanceTo(int beat, int? consumedCueMarkBeat)
        {
            var cueMarkOffsets = CueMarkOffsets;
            while (index < cueMarkOffsets.Length - 1 && CueMarkAt(index) < beat)
            {
                index++;
            }

            while (index < cueMarkOffsets.Length - 1
                && consumedCueMarkBeat is { } firedCueMark
                && CueMarkAt(index) <= firedCueMark)
            {
                index++;
            }
        }

        public int CurrentCueMarkOr(int fallbackCueMark)
        {
            var cueMarkOffsets = CueMarkOffsets;
            return cueMarkOffsets.Length > 0
                ? CueMarkAt(ClampIndex(index, cueMarkOffsets.Length))
                : fallbackCueMark;
        }

        public bool IsConsumedThroughPhraseEnd(int? consumedCueMarkBeat)
        {
            return consumedCueMarkBeat is { } firedCueMark && firedCueMark >= PhraseEndBeat;
        }

        public CueSheetStatus Status(int currentCueMarkBeat)
        {
            if (!HasSheet)
            {
                return CueSheetStatus.Empty;
            }

            var cueMarkOffsets = CueMarkOffsets;
            var currentCueMarkIndex = cueMarkOffsets.Length > 0 ? ClampIndex(index, cueMarkOffsets.Length) : -1;
            return new CueSheetStatus(
                true,
                phraseStartBeat,
                PhraseEndBeat,
                sheet.PhraseLengthBeats,
                currentCueMarkIndex,
                currentCueMarkBeat,
                (int[])cueMarkOffsets.Clone());
        }

        private int CueMarkAt(int cueMarkIndex)
        {
            var cueMarkOffsets = CueMarkOffsets;
            return sheet.ToAbsoluteBeat(phraseStartBeat, cueMarkOffsets[ClampIndex(cueMarkIndex, cueMarkOffsets.Length)]);
        }

        private int[] CueMarkOffsets => sheet.CueMarkOffsets ?? Array.Empty<int>();
    }

    /// <summary>Creates a Cue Planner using Unity's runtime random source for boundary selection.</summary>
    public CuePlanner()
        : this((minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive))
    {
    }

    /// <summary>Creates a Cue Planner with an explicit random source for deterministic seam tests.</summary>
    public CuePlanner(Func<int, int, int> randomRange)
    {
        this.randomRange = randomRange ?? throw new ArgumentNullException(nameof(randomRange));
    }

    /// <summary>The last beat any change committed, or <see cref="int.MinValue"/> when none — the change cadence memory the Director queries.</summary>
    public int LastChangeBeat => lastChangeBeat;

    /// <summary>The last committed Cue Mark this pass, or null — the cadence anchor and consumed-mark memory.</summary>
    private int? ConsumedCueMarkBeat => lastChangeBeat == int.MinValue ? (int?)null : lastChangeBeat;

    /// <summary>Records that a change committed on <paramref name="beat"/> (a synced cue impact or a manual show-now), feeding change cadence.</summary>
    public void MarkChanged(int beat)
    {
        lastChangeBeat = beat;
    }

    /// <summary>Records that a synced cue was issued on <paramref name="beat"/>, so the same pass does not re-cue it.</summary>
    public void RecordCueIssued(int beat)
    {
        lastCueBeat = beat;
    }

    /// <summary>
    /// The per-beat cue timing verdict, answered from the planner's own pass-local memory: a beat
    /// that already issued a cue waits, an already-committed Cue Mark waits, a beat at or past the
    /// plan's Lock Point waits (too late to commit — the mark is missed, never fired late), and a
    /// Mark whose landing violates the minimum change cadence blocks. Timing is decided here;
    /// casting (which Performer/Transition) stays with the Director.
    /// </summary>
    public CueTimingVerdict EvaluateCueTiming(int cueMarkBeat, TransitionRepertoire repertoire, int beat, int minimumChangeCadenceBeats)
    {
        if (lastCueBeat == beat
            || lastChangeBeat == cueMarkBeat
            || !Switcher.CanCommitCue(cueMarkBeat, repertoire, beat))
        {
            return CueTimingVerdict.Wait;
        }

        return CanChangeAt(cueMarkBeat, minimumChangeCadenceBeats)
            ? CueTimingVerdict.Cue
            : CueTimingVerdict.BlockedByCadence;
    }

    /// <summary>Whether the minimum change cadence permits a change landing on <paramref name="beat"/>.</summary>
    public bool CanChangeAt(int beat, int minimumChangeCadenceBeats)
    {
        return ChangeCadence.CanChangeAt(beat, ConsumedCueMarkBeat, minimumChangeCadenceBeats);
    }

    /// <summary>Clears all remembered timing state so the next synced frame starts a new interpretation.</summary>
    public void Reset()
    {
        lastBeat = -1;
        cueSheet.Reset();
    }

    /// <summary>
    /// Builds the current Timing Frame from one live rhythm snapshot. Owns the pass-local cue/cadence
    /// memory (no Director round-trip): it reads its own remembered cue/change beats, clears the stale
    /// ones when the beat rewound into a new pass, and remembers the corrected values for the next call.
    /// </summary>
    public TimingFrame Plan(
        OnAirTimingInput input,
        int minimumChangeCadenceBeats)
    {
        if (minimumChangeCadenceBeats <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumChangeCadenceBeats), minimumChangeCadenceBeats, "Minimum cadence must be positive.");
        }

        var beatRewoundToNewPass = BeatRewoundToNewPass(lastBeat, input.Beat, minimumChangeCadenceBeats);

        // A rewound beat starts a new pass: cue/commit memory at or after the rewound beat belongs to
        // the previous pass and would wrongly block this one, so it clears; memory before it still binds.
        if (beatRewoundToNewPass && input.Beat >= 1)
        {
            if (lastCueBeat >= input.Beat)
            {
                lastCueBeat = -1;
            }

            if (lastChangeBeat != int.MinValue && lastChangeBeat >= input.Beat)
            {
                lastChangeBeat = int.MinValue;
            }
        }

        if (input.Beat >= 1)
        {
            lastBeat = input.Beat;
        }

        if (input.Beat < 1)
        {
            return BuildUnlockedFrame(input, beatRewoundToNewPass);
        }

        var consumedCueMarkBeat = ConsumedCueMarkBeat;

        // The live Phrase Window: plan interior cues and the mandatory boundary against it.
        if (PhraseWindow.TryFromTrackPhase(
                input.Beat,
                input.BeatsUntilPhraseEnd,
                input.PhraseLengthBeats,
                out var liveWindow))
        {
            return BuildAnchoredFrame(
                input,
                ResolveFromWindow(liveWindow, input.Beat, beatRewoundToNewPass, consumedCueMarkBeat, minimumChangeCadenceBeats),
                beatRewoundToNewPass);
        }

        // No live window (Track Phase is counting down or absent): the sheet keeps serving its own
        // window until its mandatory end is consumed or passed.
        if (cueSheet.TryGetActivePhraseWindow(input.Beat, consumedCueMarkBeat, out var activeWindow))
        {
            return BuildAnchoredFrame(
                input,
                ResolveFromWindow(activeWindow, input.Beat, beatRewoundToNewPass, consumedCueMarkBeat, minimumChangeCadenceBeats),
                beatRewoundToNewPass);
        }

        // The next Phrase is known: build its sheet early off the true announced length (covers the
        // pre-first-phrase countdown). Its start beat — the Track Phase boundary — becomes a Cue Mark
        // when cadence allows (CueSheet.Build).
        if (PhraseWindow.TryFromUpcomingTrackPhase(
                input.Beat,
                input.NextPhraseStartInBeats,
                input.NextPhraseLengthBeats,
                out var upcomingWindow))
        {
            return BuildAnchoredFrame(
                input,
                ResolveFromWindow(upcomingWindow, input.Beat, beatRewoundToNewPass, consumedCueMarkBeat, minimumChangeCadenceBeats),
                beatRewoundToNewPass);
        }

        // No current and no next Phrase (brief, e.g. the first frames of a track): idle unlocked.
        return BuildUnlockedFrame(input, beatRewoundToNewPass);
    }

    private TimingFrame BuildAnchoredFrame(
        OnAirTimingInput input,
        ResolvedTimingTarget target,
        bool beatRewoundToNewPass)
    {
        return new TimingFrame(
            input,
            true,
            target.CueMarkBeat,
            target.HasPhraseWindow,
            target.PhraseWindow,
            target.Source,
            beatRewoundToNewPass,
            cueSheet.Status(target.CueMarkBeat));
    }

    private TimingFrame BuildUnlockedFrame(
        OnAirTimingInput input,
        bool beatRewoundToNewPass)
    {
        return new TimingFrame(
            input,
            false,
            -1,
            false,
            default,
            TimingFrameSource.Unlocked,
            beatRewoundToNewPass,
            CueSheetStatus.Empty);
    }

    /// <summary>
    /// Resolves the Cue Mark for a Phrase Window: a Window whose start differs from the held sheet's
    /// start is a fresh Phrase (same-length turnover included), so its sheet is rebuilt; the same
    /// Window on a rewound beat re-arms the cursor. Then the cursor advances to this beat and the mark
    /// is either an interior Cue Mark or the mandatory phrase end.
    /// </summary>
    private ResolvedTimingTarget ResolveFromWindow(
        PhraseWindow window,
        int beat,
        bool beatRewoundToNewPass,
        int? consumedCueMarkBeat,
        int minimumChangeCadenceBeats)
    {
        if (!cueSheet.CoversWindow(window))
        {
            var sheet = CueSheet.Build(
                window,
                beat,
                candidateCueMarkBeat => ChangeCadence.CanChangeAt(candidateCueMarkBeat, consumedCueMarkBeat, minimumChangeCadenceBeats),
                randomRange);
            cueSheet.Replace(sheet, window);
        }
        else if (beatRewoundToNewPass)
        {
            cueSheet.RewindCursor();
        }

        cueSheet.AdvanceTo(beat, consumedCueMarkBeat);
        var cueMarkBeat = cueSheet.CurrentCueMarkOr(window.EndBeat);
        var source = cueMarkBeat == cueSheet.PhraseEndBeat
            ? TimingFrameSource.TrackPhaseBoundary
            : TimingFrameSource.CueMark;
        return new ResolvedTimingTarget(source, cueMarkBeat, true, window);
    }

    private static bool BeatRewoundToNewPass(int previousBeat, int beat, int minimumChangeCadenceBeats)
    {
        return previousBeat >= 1
            && beat >= 1
            && beat < previousBeat
            && previousBeat - beat + 1 >= minimumChangeCadenceBeats;
    }

    private static int ClampIndex(int index, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        if (index < 0)
        {
            return 0;
        }

        return index >= length ? length - 1 : index;
    }
}
