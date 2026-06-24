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
/// Stateful Phase determiner. It holds the one as an <see cref="PhaseReading.Offset"/> and
/// recomputes the <see cref="PhaseReading.Position"/> every frame from the running beat, so a
/// loop — a bar-aligned backward beat jump — is absorbed for free with no explicit loop
/// detection. Phrase wins the grid (re-latch at every boundary, gated by bar-alignment);
/// total_beats is only an end-aligned fallback when Phrase data is absent.
/// <para>
/// Slice 01 is the red contract phase: this is the seam and a compiling skeleton only.
/// The held-offset latch, the bar-alignment gate, phrase-boundary detection, per-layer
/// degradation, and the stand-alone floor land in slice 02. Until then <see cref="Read"/>
/// emits <see cref="PhaseReading.None"/>, which the contract tests run red against.
/// </para>
/// </summary>
public sealed class PhaseLock
{
    /// <summary>
    /// Reads one frame of BeatManager's projected integer values and emits the current Phase
    /// reading. Stateful: call once per frame on a single instance so the held offset and
    /// lock state carry across frames.
    /// </summary>
    public PhaseReading Read(in OnAirTimingInput input)
    {
        // Slice 01 skeleton — no Phase logic yet. Slice 02 fills in the held-offset model.
        return PhaseReading.None;
    }
}
