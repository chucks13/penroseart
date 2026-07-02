using System;

/// <summary>
/// Director-owned cue planner: interprets one live rhythm snapshot into the Director-facing
/// <see cref="TimingFrame"/>. Owns Cue Sheet derivation, the Cue Mark cursor, change-cadence
/// memory, substantial beat-rewind handling, and Grid Anchor coasting.
///
/// Relocated from the former On-Air Timing core (slice 04b). On-Air Timing is now a pure
/// Grid/Phrase determiner (GridSync + PhraseTracker readings the Director composes); the cue
/// decisions live here, fed by plain data — never a reference back to the timing source. The
/// pass-local cue/cadence memory it used to round-trip through the Director is now owned outright.
/// </summary>
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

public sealed class CuePlanner
{
    /// <summary>
    /// Length in beats of the fabricated Phrase Window used for the beat-only fallback: four 16-beat
    /// Grids, so the random subset has real interior choices and the window ties to the 64-beat
    /// maximum Cue Mark gap (ADR-0008).
    /// </summary>
    private const int SyntheticPhraseLengthBeats = 64;

    private readonly Func<int, int, int> randomRange;

    private int lastBeat = -1;
    private bool hasGridAnchor;
    private int gridAnchorLandingBeat = -1;
    private readonly CueSheetCursor cueSheet = new CueSheetCursor();
    private readonly SyntheticCueGrid syntheticCueGrid = new SyntheticCueGrid();
    private TimingFrameSource lastSource = TimingFrameSource.Unlocked;

    // Pass-local cue/cadence memory, owned outright (no Director round-trip). lastChangeBeat is the
    // change cadence memory the Director also queries and marks (cue commits and manual ShowNow).
    private int lastCueBeat = -1;
    private int lastChangeBeat = int.MinValue;

    // Synthetic-fallback loop guard: the previous synthetic Cue Mark and its countdown. A countdown that
    // climbs for the same mark on a substantial rewind means a loop sent the beat back before the mark
    // could land — it is stranded past the looped section — so the fallback grids until it is reached.
    private int syntheticCueMarkBeat = -1;
    private int syntheticBeatsUntilCueMark = int.MinValue;
    private bool syntheticCueMarkStranded;

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

        public static ResolvedTimingTarget GridFallback(int cueMarkBeat)
        {
            return new ResolvedTimingTarget(
                TimingFrameSource.GridFallback,
                cueMarkBeat,
                false,
                default);
        }
    }

    private sealed class CueSheetCursor
    {
        private CueSheet sheet;
        private int phraseStartBeat;
        private int index;

        public bool HasSheet { get; private set; }

        public int PhraseEndBeat => phraseStartBeat + sheet.PhraseLengthBeats;

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

        /// <summary>
        /// Moves the sheet onto a same-length Phrase Window. When the start beat moves — a timing
        /// shift or a same-length turnover — the cursor rewinds so the reused pattern replays from
        /// its first mark; AdvanceTo re-skips whatever this pass already consumed or passed.
        /// </summary>
        public void Reanchor(PhraseWindow phraseWindow)
        {
            if (phraseStartBeat == phraseWindow.StartBeat)
            {
                return;
            }

            phraseStartBeat = phraseWindow.StartBeat;
            index = 0;
        }

        public void RewindCursor()
        {
            index = 0;
        }

        public bool NeedsSheet(PhraseWindow phraseWindow, int beat)
        {
            var cueMarkOffsets = CueMarkOffsets;
            return !HasSheet
                || cueMarkOffsets.Length == 0
                || !sheet.Matches(phraseWindow)
                // Adopting a Phrase that has not started yet (a look-ahead window): build its sheet
                // fresh so the start beat — the Track Phase boundary — gets rolled in as a Cue Mark
                // (CueSheet.Build). Length-identity reuse applies only to windows already underway.
                || (phraseWindow.StartBeat > beat && phraseStartBeat != phraseWindow.StartBeat);
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

        public bool TryKeepUnconsumedMandatoryBoundaryAt(
            int beat,
            int? consumedCueMarkBeat,
            int lateCueWindowBeats,
            out int cueMarkBeat,
            out PhraseWindow phraseWindow)
        {
            cueMarkBeat = -1;
            phraseWindow = default;
            if (!HasSheet
                || IsConsumedThroughPhraseEnd(consumedCueMarkBeat)
                || !PhraseWindow.TryFromStartAndLength(phraseStartBeat, sheet.PhraseLengthBeats, out phraseWindow))
            {
                return false;
            }

            AdvanceTo(beat, consumedCueMarkBeat, lateCueWindowBeats);
            var currentCueMark = CurrentCueMarkOr(-1);
            // Keep the mark only when the cursor's mark is the phrase end and this beat is at that mark
            // or late inside its cue window — an unconsumed mandatory boundary can still cue while a
            // Tail can complete there. AdvanceTo is monotone and idempotent, so advancing here costs
            // nothing when the keep declines.
            if (currentCueMark != PhraseEndBeat
                || beat < PhraseEndBeat
                || beat > PhraseEndBeat + lateCueWindowBeats)
            {
                return false;
            }

            cueMarkBeat = currentCueMark;
            return true;
        }

        public void AdvanceTo(int beat, int? consumedCueMarkBeat, int lateCueWindowBeats = 0)
        {
            var cueMarkOffsets = CueMarkOffsets;
            while (index < cueMarkOffsets.Length - 1 && CueMarkAt(index) + lateCueWindowBeats < beat)
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

    /// <summary>
    /// Fabricates a rolling Cue Sheet for the beat-only fallback (no Track Phase). It anchors a
    /// fixed-length synthetic Phrase Window on the grid one GridSync already holds, runs it through the
    /// real <see cref="CueSheet.Build"/> so the fallback inherits the same sparse-but-bounded Cue Mark
    /// character, and drives a real <see cref="CueSheetCursor"/> over it — so cursor advancement, the
    /// consumed-mark skip, beat-rewind handling, and Status all match the live-phrase path exactly rather
    /// than re-implementing them. It rolls the window forward by its length once consumed, re-rolling a
    /// fresh random subset only on that advance. Kept on its own cursor, separate from the live Cue
    /// Sheet, so a fabricated sheet can never be reused for a real Phrase (ADR-0008).
    /// </summary>
    private sealed class SyntheticCueGrid
    {
        private readonly CueSheetCursor cursor = new CueSheetCursor();
        private int phraseStartBeat = -1;

        public void Reset()
        {
            cursor.Reset();
            phraseStartBeat = -1;
        }

        public bool TryResolve(
            int beat,
            int gridOffset,
            int lengthBeats,
            bool beatRewoundToNewPass,
            int? consumedCueMarkBeat,
            int minimumChangeCadenceBeats,
            int lateCueWindowBeats,
            Func<int, int, int> randomRange,
            out int cueMarkBeat,
            out PhraseWindow window,
            out CueSheetStatus status)
        {
            cueMarkBeat = -1;
            status = CueSheetStatus.Empty;

            // The most recent length-aligned grid one at or before this beat. Honours GridSync's held
            // offset so the window stays aligned through a transient mid-track Phrase dropout; for a track
            // that never had Phrase the offset is 0 and the one sits at beat ≡ 1 (mod 16).
            var startBeat = beat - Grid.Mod(beat - 1 - gridOffset, lengthBeats);
            if (!PhraseWindow.TryFromStartAndLength(startBeat, lengthBeats, out window))
            {
                return false;
            }

            if (!cursor.HasSheet || startBeat != phraseStartBeat)
            {
                // The window advanced (or this is the first frame): re-roll a fresh sparse subset. The
                // cadence predicate is built only here — not every frame — so steady-state fallback
                // allocates nothing on the timing path.
                var sheet = CueSheet.Build(
                    window,
                    beat,
                    candidateCueMarkBeat => ChangeCadence.CanChangeAt(candidateCueMarkBeat, consumedCueMarkBeat, minimumChangeCadenceBeats),
                    randomRange);
                cursor.Replace(sheet, window);
                phraseStartBeat = startBeat;
            }
            else if (beatRewoundToNewPass)
            {
                // A loop/rewind inside the same window re-arms the interior marks the looped section replays.
                cursor.RewindCursor();
            }

            cursor.AdvanceTo(beat, consumedCueMarkBeat, lateCueWindowBeats);
            cueMarkBeat = cursor.CurrentCueMarkOr(window.EndBeat);
            status = cursor.Status(cueMarkBeat);
            return true;
        }
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
    /// that already issued a cue waits, an already-committed Cue Mark waits, a beat outside the
    /// Transition's cue window waits, and a Mark whose landing violates the minimum change cadence
    /// blocks. Timing is decided here; casting (which Performer/Transition) stays with the Director.
    /// </summary>
    public CueTimingVerdict EvaluateCueTiming(TransitionBeatPlan beatPlan, int beat, int minimumChangeCadenceBeats)
    {
        if (lastCueBeat == beat
            || lastChangeBeat == beatPlan.ImpactBeat
            || !beatPlan.IsCueBeat(beat))
        {
            return CueTimingVerdict.Wait;
        }

        return CanChangeAt(beatPlan.ImpactBeat, minimumChangeCadenceBeats)
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
        hasGridAnchor = false;
        gridAnchorLandingBeat = -1;
        cueSheet.Reset();
        syntheticCueGrid.Reset();
        ResetSyntheticLoopGuard();
        lastSource = TimingFrameSource.Unlocked;
    }

    /// <summary>
    /// Builds the current Timing Frame from one live rhythm snapshot. Owns the pass-local cue/cadence
    /// memory (no Director round-trip): it reads its own remembered cue/change beats, clears the stale
    /// ones when the beat rewound into a new pass, and remembers the corrected values for the next call.
    /// </summary>
    /// <param name="lateCueWindowBeats">Beats past an unconsumed Cue Mark during which it stays the
    /// frame's target — the staged transition's Tail, where a late cue can still start backdated and
    /// complete. Zero skips a missed mark immediately.</param>
    public TimingFrame Plan(
        OnAirTimingInput input,
        in GridReading grid,
        in PhraseTrackerReading phrase,
        int minimumChangeCadenceBeats,
        int lateCueWindowBeats = 0)
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

        if (TrackPhaseUnavailable(input) && input.Beat >= 1)
        {
            // A recently lost real anchor keeps coasting its known grid; a track that never had Track
            // Grid (no coastable anchor) cues from a fabricated rolling Phrase Window instead of idling,
            // so a phrase-less track still sequences until phrase data loads (ADR-0008).
            if (HasCoastableGridAnchor())
            {
                return BuildCoastingFrame(input, grid, beatRewoundToNewPass, minimumChangeCadenceBeats, lateCueWindowBeats);
            }

            return BuildSyntheticFrame(input, grid, beatRewoundToNewPass, minimumChangeCadenceBeats, lateCueWindowBeats);
        }

        // The gate is "do we have a usable grid position to plan interior cues against." A live phrase
        // always yields Position >= 1, so the mandatory phrase-end cue is never suppressed here. The
        // beat-only case (no Track Phase) is handled above, so this branch only sees a present Track
        // Phase channel — an active window or the idle tri-state-0 grid fallback.
        if (grid.Position >= 1 && input.Beat >= 1)
        {
            var previousSource = lastSource;
            var target = ResolveCueMark(
                input.Beat,
                phrase,
                grid,
                beatRewoundToNewPass,
                ConsumedCueMarkBeat,
                minimumChangeCadenceBeats,
                lateCueWindowBeats);
            var reanchored = ReanchoredFrom(previousSource, target.Source, hasGridAnchor);
            return BuildAnchoredFrame(
                input,
                grid,
                target,
                beatRewoundToNewPass,
                reanchored);
        }

        if (hasGridAnchor && input.Beat >= 1)
        {
            return BuildCoastingFrame(input, grid, beatRewoundToNewPass, minimumChangeCadenceBeats, lateCueWindowBeats);
        }

        return BuildUnlockedFrame(input, grid, beatRewoundToNewPass);
    }

    private TimingFrame BuildCoastingFrame(
        OnAirTimingInput input,
        in GridReading grid,
        bool beatRewoundToNewPass,
        int minimumChangeCadenceBeats,
        int lateCueWindowBeats)
    {
        CoastGridAnchor(input.Beat, minimumChangeCadenceBeats, lateCueWindowBeats);
        lastSource = TimingFrameSource.Coast;
        return CreateFrame(
            input,
            grid,
            true,
            gridAnchorLandingBeat,
            false,
            default,
            TimingFrameSource.Coast,
            beatRewoundToNewPass,
            false,
            CueSheetStatus.Empty);
    }

    private TimingFrame BuildAnchoredFrame(
        OnAirTimingInput input,
        in GridReading grid,
        ResolvedTimingTarget target,
        bool beatRewoundToNewPass,
        bool reanchored)
    {
        hasGridAnchor = true;
        gridAnchorLandingBeat = target.CueMarkBeat;
        lastSource = target.Source;
        return CreateFrame(
            input,
            grid,
            true,
            gridAnchorLandingBeat,
            target.HasPhraseWindow,
            target.PhraseWindow,
            target.Source,
            beatRewoundToNewPass,
            reanchored,
            cueSheet.Status(gridAnchorLandingBeat));
    }

    private TimingFrame BuildUnlockedFrame(
        OnAirTimingInput input,
        in GridReading grid,
        bool beatRewoundToNewPass)
    {
        hasGridAnchor = false;
        gridAnchorLandingBeat = -1;
        lastSource = TimingFrameSource.Unlocked;
        return CreateFrame(
            input,
            grid,
            false,
            -1,
            false,
            default,
            TimingFrameSource.Unlocked,
            beatRewoundToNewPass,
            false,
            CueSheetStatus.Empty);
    }

    private TimingFrame BuildSyntheticFrame(
        OnAirTimingInput input,
        in GridReading grid,
        bool beatRewoundToNewPass,
        int minimumChangeCadenceBeats,
        int lateCueWindowBeats)
    {
        // Clear the real Cue Sheet lifecycle once on entering the fallback so a stale real sheet can't be
        // reused (CueSheet.Matches is length-only) when a Phrase track later loads, and drop a stale loop
        // guard so a stranded latch from an earlier synthetic spell can't leak across a real-Phrase
        // excursion. Edge-triggered: while the synthetic grid drives nothing rebuilds the real plans, so
        // re-clearing every frame is waste.
        if (lastSource != TimingFrameSource.SyntheticPhrase)
        {
            cueSheet.Reset();
            ResetSyntheticLoopGuard();
        }

        if (!syntheticCueGrid.TryResolve(
                input.Beat,
                grid.Offset,
                SyntheticPhraseLengthBeats,
                beatRewoundToNewPass,
                ConsumedCueMarkBeat,
                minimumChangeCadenceBeats,
                lateCueWindowBeats,
                randomRange,
                out var resolvedCueMarkBeat,
                out var phraseWindow,
                out var cueSheetStatus))
        {
            // Too early in the track to anchor a full synthetic window (its start would precede beat 1).
            ResetSyntheticLoopGuard();
            return BuildUnlockedFrame(input, grid, beatRewoundToNewPass);
        }

        var beatsUntilCueMark = resolvedCueMarkBeat - input.Beat;
        var stranded = UpdateSyntheticLoopGuard(resolvedCueMarkBeat, beatsUntilCueMark);

        hasGridAnchor = true;
        // Hold lastSource at SyntheticPhrase even while gridding: the mode is still the synthetic fallback,
        // so the edge-triggered real-sheet clear above must not re-fire each looped frame, and a later real
        // Phrase still reads SyntheticPhrase as the prior source for Reanchored.
        lastSource = TimingFrameSource.SyntheticPhrase;

        if (stranded)
        {
            // A loop sent the beat back before this synthetic Cue Mark could land, so the mark sits past the
            // looped section and is never reached. Cue on the 16-beat Grid instead — the same grid
            // landing the real path uses with no Cue Sheet — until the countdown to the synthetic mark
            // naturally reaches zero (the loop releases and the beat carries through), then resume synthetic
            // cueing. The synthetic sheet status still rides along so the observatory shows the stranded
            // mark we are coasting the grid beneath; the Director cues off the grid landing, not the sheet.
            var gridLandingBeat = GetLandingBeatFromGridPosition(input.Beat, grid.Position);
            gridAnchorLandingBeat = gridLandingBeat;
            return CreateFrame(
                input,
                grid,
                true,
                gridLandingBeat,
                false,
                default,
                TimingFrameSource.GridFallback,
                beatRewoundToNewPass,
                false,
                cueSheetStatus);
        }

        gridAnchorLandingBeat = resolvedCueMarkBeat;
        return CreateFrame(
            input,
            grid,
            true,
            resolvedCueMarkBeat,
            true,
            phraseWindow,
            TimingFrameSource.SyntheticPhrase,
            beatRewoundToNewPass,
            false,
            cueSheetStatus);
    }

    /// <summary>
    /// Tracks the synthetic Cue Mark countdown across frames and reports whether the mark is stranded by a
    /// loop. The countdown normally falls one-per-beat; any climb for the same mark means the beat moved
    /// backward before the mark landed, so it is unreachable until the beat carries through. No rewind-size
    /// gate — a loop of any length is caught, and a stray backstep just grids harmlessly until the mark is
    /// reached. A new mark (the window rolled, or first frame) tracks clean; reaching the mark clears it.
    /// </summary>
    private bool UpdateSyntheticLoopGuard(int cueMarkBeat, int beatsUntilCueMark)
    {
        if (cueMarkBeat != syntheticCueMarkBeat)
        {
            syntheticCueMarkBeat = cueMarkBeat;
            syntheticBeatsUntilCueMark = beatsUntilCueMark;
            syntheticCueMarkStranded = false;
            return false;
        }

        if (beatsUntilCueMark <= 0)
        {
            // Reached (or passed) the mark — any loop has released and carried through. Resume synthetic cueing.
            syntheticCueMarkStranded = false;
        }
        else if (beatsUntilCueMark > syntheticBeatsUntilCueMark)
        {
            // The countdown climbed for the same mark: the beat looped back before the mark could land, so it
            // is stranded past the looped section. Stays gridded until the mark is reached (clause above).
            syntheticCueMarkStranded = true;
        }

        syntheticBeatsUntilCueMark = beatsUntilCueMark;
        return syntheticCueMarkStranded;
    }

    private void ResetSyntheticLoopGuard()
    {
        syntheticCueMarkBeat = -1;
        syntheticBeatsUntilCueMark = int.MinValue;
        syntheticCueMarkStranded = false;
    }

    private static TimingFrame CreateFrame(
        OnAirTimingInput input,
        in GridReading grid,
        bool hasGridAnchor,
        int cueMarkBeat,
        bool hasPhraseWindow,
        PhraseWindow phraseWindow,
        TimingFrameSource source,
        bool beatRewoundToNewPass,
        bool reanchored,
        CueSheetStatus cueSheet)
    {
        return new TimingFrame(
            input,
            grid,
            hasGridAnchor,
            cueMarkBeat,
            hasPhraseWindow,
            phraseWindow,
            source,
            beatRewoundToNewPass,
            reanchored,
            cueSheet);
    }

    private static bool ReanchoredFrom(TimingFrameSource previousSource, TimingFrameSource source, bool hadGridAnchor)
    {
        return hadGridAnchor
            && (source == TimingFrameSource.TrackPhaseBoundary || source == TimingFrameSource.CueMark)
            && (previousSource == TimingFrameSource.Coast
                || previousSource == TimingFrameSource.GridFallback
                || previousSource == TimingFrameSource.SyntheticPhrase);
    }

    private bool HasCoastableGridAnchor()
    {
        return hasGridAnchor
            && (lastSource == TimingFrameSource.CueMark
                || lastSource == TimingFrameSource.TrackPhaseBoundary
                || lastSource == TimingFrameSource.Coast);
    }

    private static bool TrackPhaseUnavailable(OnAirTimingInput input)
    {
        return input.TrackPhaseActive < 0;
    }

    private void CoastGridAnchor(int beat, int minimumChangeCadenceBeats, int lateCueWindowBeats)
    {
        while (beat > gridAnchorLandingBeat + lateCueWindowBeats)
        {
            gridAnchorLandingBeat += minimumChangeCadenceBeats;
        }
    }

    private ResolvedTimingTarget ResolveCueMark(
        int beat,
        in PhraseTrackerReading phrase,
        in GridReading grid,
        bool beatRewoundToNewPass,
        int? consumedCueMarkBeat,
        int minimumChangeCadenceBeats,
        int lateCueWindowBeats)
    {
        // A pending unconsumed mandatory boundary outlives its Phrase reading: it stays the target
        // through its late-cue window even after Track Phase has moved to the next Phrase Window.
        if (cueSheet.TryKeepUnconsumedMandatoryBoundaryAt(
            beat,
            consumedCueMarkBeat,
            lateCueWindowBeats,
            out var cueMarkBeat,
            out var phraseWindow))
        {
            return new ResolvedTimingTarget(
                TimingFrameSource.TrackPhaseBoundary,
                cueMarkBeat,
                true,
                phraseWindow);
        }

        if (phrase.PhraseLengthBeats >= 1
            && PhraseWindow.TryFromTrackPhase(
                beat,
                phrase.BeatsUntilNextPhrase,
                phrase.PhraseLengthBeats,
                out phraseWindow))
        {
            return ResolveCueMarkFromSheet(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedCueMarkBeat,
                minimumChangeCadenceBeats,
                lateCueWindowBeats);
        }

        // No live window (Track Phase is counting down or idle): the sheet keeps serving its own
        // window until its mandatory end is consumed or passed.
        if (cueSheet.TryGetActivePhraseWindow(beat, consumedCueMarkBeat, out phraseWindow))
        {
            return ResolveCueMarkFromSheet(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedCueMarkBeat,
                minimumChangeCadenceBeats,
                lateCueWindowBeats);
        }

        // A look-ahead window builds the next Phrase's sheet early; its start beat — the Track Phase
        // boundary itself — is already a Cue Mark when cadence allows (CueSheet.Build).
        if (phrase.HasLookAhead
            && PhraseWindow.TryFromUpcomingTrackPhase(
                beat,
                phrase.BeatsUntilNextPhrase,
                phrase.PredictedUpcomingLengthBeats,
                out phraseWindow))
        {
            return ResolveCueMarkFromSheet(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedCueMarkBeat,
                minimumChangeCadenceBeats,
                lateCueWindowBeats);
        }

        cueSheet.Reset();
        return ResolvedTimingTarget.GridFallback(GetLandingBeatFromGridPosition(beat, grid.Position));
    }

    private ResolvedTimingTarget ResolveCueMarkFromSheet(
        int beat,
        PhraseWindow phraseWindow,
        bool beatRewoundToNewPass,
        int? consumedCueMarkBeat,
        int minimumChangeCadenceBeats,
        int lateCueWindowBeats)
    {
        if (cueSheet.NeedsSheet(phraseWindow, beat))
        {
            // A new Phrase length rerolls the sheet. Same-length windows never reach here: the sheet
            // is reused by length identity (CueSheet.Matches) and only re-anchored below.
            var sheet = CueSheet.Build(
                phraseWindow,
                beat,
                candidateCueMarkBeat => ChangeCadence.CanChangeAt(candidateCueMarkBeat, consumedCueMarkBeat, minimumChangeCadenceBeats),
                randomRange);
            cueSheet.Replace(sheet, phraseWindow);
        }
        else
        {
            cueSheet.Reanchor(phraseWindow);
            if (beatRewoundToNewPass)
            {
                cueSheet.RewindCursor();
            }
        }

        cueSheet.AdvanceTo(beat, consumedCueMarkBeat, lateCueWindowBeats);
        var cueMarkBeat = cueSheet.CurrentCueMarkOr(phraseWindow.EndBeat);
        var source = cueMarkBeat == cueSheet.PhraseEndBeat
            ? TimingFrameSource.TrackPhaseBoundary
            : TimingFrameSource.CueMark;
        return new ResolvedTimingTarget(source, cueMarkBeat, true, phraseWindow);
    }

    private static int GetLandingBeatFromGridPosition(int beat, int gridPosition)
    {
        var beatsUntilLanding = Grid.GridBeats - gridPosition + 1;
        return beat + beatsUntilLanding;
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
