/// <summary>
/// How much to trust where the one sits this frame. Degrades top-down and never
/// loses the floor: while a clock exists the pulse is ≈always locked, and Phase
/// carries its own confidence here. There is no "Unlocked" inside synced mode —
/// loss of the clock itself is a mode exit (see <see cref="PhaseReading.StandAloneFloor"/>),
/// not a degraded Phase state.
/// </summary>
public enum PhaseLockState
{
    /// <summary>Offset is freshly anchored or steadily dead-reckoned; the one is trusted.</summary>
    Locked,

    /// <summary>No fresh anchor available (e.g. Phrase data dropped out); holding the last good offset.</summary>
    Coasting,

    /// <summary>A freshly derived offset disagreed with the held one; holding it pending the next clean re-latch.</summary>
    Contradicted,
}

/// <summary>
/// Read-only Phase reading <see cref="PhaseLock"/> emits each frame. Every value is a
/// whole-beat integer: the model is exact integer modular arithmetic, so no floats leak
/// into grid math that must stay crisp.
/// </summary>
public readonly struct PhaseReading
{
    /// <summary>Sentinel "no Phase resolved" reading. The slice-01 skeleton emits this every frame.</summary>
    public static PhaseReading None { get; } =
        new PhaseReading(-1, -1, PhaseLockState.Coasting, 0, false, false);

    /// <summary>Held position of the one in the 16-beat grid (0..15), or -1 when unknown. Re-latched only at structural triggers.</summary>
    public readonly int Offset;

    /// <summary>Where this frame sits on the grid (1..16), or -1 when unknown: <c>((beat − 1) − offset) mod 16 + 1</c>. Recomputed every frame, so it tracks true playback (loops included).</summary>
    public readonly int Position;

    /// <summary>Confidence in the held offset this frame.</summary>
    public readonly PhaseLockState State;

    /// <summary>Beats dead-reckoned since the last re-latch; a staleness count the Director can threshold.</summary>
    public readonly int BeatsSinceAnchor;

    /// <summary>The latest Phrase ended off the 16-grid from its own start (length not a multiple of 16) — the "phase ≠ phrase" diagnostic.</summary>
    public readonly bool IrregularPhrase;

    /// <summary>The clock itself is gone (<c>beat_in_bar == -1</c>): the trigger to exit synced mode and reinstate stand-alone timing (ADR-0004). A mode exit, not a Phase state.</summary>
    public readonly bool StandAloneFloor;

    /// <summary>True while the held offset is being defended against a contradicting derivation. Derived from <see cref="State"/> — the enum is the single source of truth.</summary>
    public bool IsContradicted => State == PhaseLockState.Contradicted;

    public PhaseReading(
        int offset,
        int position,
        PhaseLockState state,
        int beatsSinceAnchor,
        bool irregularPhrase,
        bool standAloneFloor)
    {
        Offset = offset;
        Position = position;
        State = state;
        BeatsSinceAnchor = beatsSinceAnchor;
        IrregularPhrase = irregularPhrase;
        StandAloneFloor = standAloneFloor;
    }
}

/// <summary>
/// Stateful Phase determiner, grounded on the 4-count tick. The feed's <c>beat_in_bar</c> is bedrock
/// (always-on, given, never derived from the beat); the running <see cref="OnAirTimingInput.Beat"/>
/// supplies position; the Phrase decides where the 16-grid starts. It holds the one as an
/// <see cref="PhaseReading.Offset"/> and recomputes the <see cref="PhaseReading.Position"/> every
/// frame, so a loop — a backward beat jump within the current Phrase — is absorbed for free with no
/// explicit loop detection. A Phrase boundary re-latches the offset, but only when the new grid lands
/// ON the tick (a real Phrase start is a downbeat); a Phrase start off the tick, or a held grid that
/// drifts off it, is a Phrase-vs-pulse disagreement held and flagged
/// <see cref="PhaseLockState.Contradicted"/> rather than silently applied. With no Phrase the offset
/// is held (coast); with nothing held the end-aligned <see cref="OnAirTimingInput.TotalBeats"/> grid
/// is a best-guess fallback. A track change resets everything — <c>beat</c> is a per-track counter, so
/// the old offset is meaningless on the new song — and the next frames re-acquire from scratch. The
/// grid arithmetic is shared with the legacy <see cref="PhaseClock"/> through <see cref="PhaseGrid"/>.
/// <para>
/// Layer-0 (the 4-count pulse) and Layer-1 (where the one sits) both currently surface their
/// disagreement through <see cref="PhaseLockState.Contradicted"/> / <see cref="PhaseReading.IsContradicted"/>.
/// A distinct Layer-0 "four-count continuous" field is intentionally NOT modelled yet: no consumer
/// distinguishes a broken pulse from a phase contradiction, and one hypothetical caller is not
/// evidence for the seam. Add it when the Director (slice 03/04) actually needs the distinction.
/// The <see cref="PhaseLockState"/> is derived fresh each frame, so a contradiction never sticks
/// past the frame whose disagreement caused it.
/// </para>
/// </summary>
public sealed class PhaseLock
{
    private const int UnsetOffset = -1;
    private const int UnsetPhraseStart = int.MinValue;
    private const int UnsetOrdinal = int.MinValue;

    private int heldOffset = UnsetOffset;
    private int lastPhraseStart = UnsetPhraseStart;
    private int anchorBeat = -1;
    private int previousTrackOrdinal = UnsetOrdinal;
    private bool irregular;

    /// <summary>
    /// Reads one frame of BeatManager's projected integer values and emits the current Phase
    /// reading. Stateful: call once per frame on a single instance so the held offset carries
    /// across frames. The lock state is derived fresh each frame from the branch that runs.
    /// </summary>
    public PhaseReading Read(in OnAirTimingInput input)
    {
        // Floor: no 4-count tick means the clock itself is gone. That is a mode exit to stand-alone
        // timing (ADR-0004), not a degraded Phase state, so we emit no grid position.
        if (input.BeatInBar < 1)
        {
            return Emit(input, position: -1, PhaseLockState.Coasting, standAloneFloor: true);
        }

        // A new song is a clean slate. `beat` is a per-track counter, so the held offset — tied to the
        // previous track's counter — is meaningless here. Drop it and re-acquire from this track's own
        // Phrase, with the total_beats fallback covering the gap until the first boundary lands.
        if (TrackChanged(input.TrackOrdinal))
        {
            ResetForNewTrack();
        }
        previousTrackOrdinal = input.TrackOrdinal;

        // Without a running beat there is no grid position to place; coast on whatever offset is held.
        if (input.Beat < 1)
        {
            return Emit(input, position: -1, PhaseLockState.Coasting);
        }

        if (HasPhrase(input))
        {
            var phraseStart = input.Beat - (input.PhraseLengthBeats - input.BeatsUntilPhraseBoundary);
            if (lastPhraseStart == UnsetPhraseStart || phraseStart != lastPhraseStart)
            {
                return ReLatch(input, phraseStart);
            }

            // Same Phrase still confirming the grid; a within-Phrase loop just recomputes the position.
            return Hold(input, PhaseLockState.Locked);
        }

        // Clock present but the Phrase feed dropped out: hold the phrase-anchored offset and coast.
        if (heldOffset != UnsetOffset)
        {
            return Hold(input, PhaseLockState.Coasting);
        }

        // No Phrase and nothing held (e.g. a fresh track before its first boundary): fall back to the
        // end-aligned grid from the track length. It is a guess — it may be wrong — so it never becomes
        // the held offset and reports COASTING, not LOCKED.
        if (input.TotalBeats >= 1)
        {
            var fallbackOffset = PhaseGrid.Mod(input.TotalBeats, PhaseGrid.PhraseBeats);
            return new PhaseReading(
                fallbackOffset,
                PhaseGrid.PositionFor(input.Beat, fallbackOffset),
                PhaseLockState.Coasting,
                BeatsSinceAnchor(input.Beat),
                irregular,
                standAloneFloor: false);
        }

        return Emit(input, position: -1, PhaseLockState.Coasting);
    }

    /// <summary>
    /// Re-latches the held offset from a fresh Phrase boundary. A real Phrase start is a downbeat, so it
    /// must land ON the 4-count tick: the grid the new offset implies has to agree with the feed's
    /// <c>beat_in_bar</c>. The first latch (nothing held yet) bootstraps unconditionally; thereafter a
    /// Phrase start that is off the tick is a Phrase-vs-pulse contradiction — hold the last good offset
    /// and flag CONTRADICTED. An accepted move that shifts the offset flags the prior Phrase irregular
    /// (its length was not a multiple of 16). No special-casing for track change: a new song was already
    /// reset to nothing held, so its first boundary is a clean bootstrap latch.
    /// </summary>
    private PhaseReading ReLatch(in OnAirTimingInput input, int phraseStart)
    {
        var newOffset = PhaseGrid.OffsetForPhraseStart(phraseStart);

        if (heldOffset != UnsetOffset && !OnTick(input, newOffset))
        {
            return Hold(input, PhaseLockState.Contradicted);
        }

        irregular = heldOffset != UnsetOffset && newOffset != heldOffset;
        heldOffset = newOffset;
        lastPhraseStart = phraseStart;
        anchorBeat = input.Beat;
        return Emit(input, PhaseGrid.PositionFor(input.Beat, heldOffset), PhaseLockState.Locked);
    }

    /// <summary>
    /// Emits the position recomputed from the held offset after cross-checking that grid against the
    /// tick. If the grid still agrees with <c>beat_in_bar</c> the requested state stands; if it disagrees
    /// (a sub-bar flub / broken pulse) the grid is held but flagged CONTRADICTED for this frame only.
    /// Derived fresh each frame, so a contradiction never sticks past the frame whose disagreement
    /// caused it.
    /// </summary>
    private PhaseReading Hold(in OnAirTimingInput input, PhaseLockState state)
    {
        if (heldOffset == UnsetOffset)
        {
            return Emit(input, position: -1, state);
        }

        var resolved = OnTick(input, heldOffset) ? state : PhaseLockState.Contradicted;
        return Emit(input, PhaseGrid.PositionFor(input.Beat, heldOffset), resolved);
    }

    /// <summary>Whether the grid implied by <paramref name="offset"/> agrees with the feed's 4-count tick this frame.</summary>
    private static bool OnTick(in OnAirTimingInput input, int offset) =>
        input.BeatInBar < 1 || PhaseGrid.BarPositionFor(input.Beat, offset) == input.BeatInBar;

    /// <summary>Drops everything held so the next frames re-acquire Phase from the new track's own data.</summary>
    private void ResetForNewTrack()
    {
        heldOffset = UnsetOffset;
        lastPhraseStart = UnsetPhraseStart;
        anchorBeat = -1;
        irregular = false;
    }

    private PhaseReading Emit(in OnAirTimingInput input, int position, PhaseLockState state, bool standAloneFloor = false) =>
        new PhaseReading(heldOffset, position, state, BeatsSinceAnchor(input.Beat), irregular, standAloneFloor);

    /// <summary>Beats dead-reckoned since the last re-latch; 0 when no anchor or the beat is behind it.</summary>
    private int BeatsSinceAnchor(int beat)
    {
        if (anchorBeat < 1 || beat < 1)
        {
            return 0;
        }
        var delta = beat - anchorBeat;
        return delta > 0 ? delta : 0;
    }

    private bool TrackChanged(int trackOrdinal) =>
        previousTrackOrdinal != UnsetOrdinal && trackOrdinal >= 0 && trackOrdinal != previousTrackOrdinal;

    private static bool HasPhrase(in OnAirTimingInput input) =>
        input.TrackPhaseActive >= 1 && input.BeatsUntilPhraseBoundary > 0 && input.PhraseLengthBeats >= 1;
}
