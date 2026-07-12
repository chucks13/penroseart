// The OffBeats doorway: the "&" cluster, the exact mirror of Beats (beat-data spec).

#nullable enable

/// <summary>
/// The "&amp;" — the moment exactly midway between beats, four per bar, one Data Surface doorway.
/// The exact mirror of <see cref="BeatsView"/>: the wire carries nothing about the "&amp;", so
/// every offering here is contrived from measured beat-time midpoints. Facts are nullable; edges
/// are never null and rest at false.
/// </summary>
public readonly struct OffBeatsView
{
    private readonly GateLanes lanes;

    /// <summary>Next Off Beat: ms until the next "&amp;", whichever slot — the mirror of Next Beat.</summary>
    public float? NextOffBeatMs => lanes.SoonestMs;

    /// <summary>
    /// Off Beat: is the current slot's gate open — the same current-slot semantics as On Beat.
    /// </summary>
    public bool? OffBeat => lanes.CurrentGate;

    /// <summary>Edge: the current slot's gate opened this frame. Never null.</summary>
    public bool OffBeatOpened => lanes.CurrentOpened;

    /// <summary>Per-"&amp;" countdown: ms until the given slot (1..4) next lands; a landed slot holds at 0 while its gate is open.</summary>
    public float? MsUntil(int slot) => lanes.MsUntil(slot);

    /// <summary>Per-"&amp;" gate: is the given slot's window open.</summary>
    public bool? Gate(int slot) => lanes.Gate(slot);

    /// <summary>Edge: the given slot's gate opened this frame. Never null — every gate has an opening edge.</summary>
    public bool GateOpened(int slot) => lanes.GateOpened(slot);

    /// <summary>Built only by the hub's per-update capture over that frame's derived offbeat lanes.</summary>
    internal OffBeatsView(GateLanes lanes)
    {
        this.lanes = lanes;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The OffBeats doorway, captured once per hub update ahead of effect Draw — identical for
    /// every reader within a frame.
    /// </summary>
    public OffBeatsView OffBeats { get; private set; }

    /// <summary>
    /// Prior served per-slot offbeat gates, retained between hub updates for the opening edges
    /// (<see cref="OffBeatsView.OffBeatOpened"/> is the current slot's lane edge, so this is the
    /// only edge memory the doorway needs).
    /// </summary>
    private readonly bool?[] previousOffBeatGates = new bool?[BeatSlotCount];

    /// <summary>
    /// Captures the OffBeats doorway from the derived offbeat state. Unlike the wire arrays,
    /// <see cref="offBeatsCountMs"/> and <see cref="offBeats"/> are rebuilt fresh by
    /// <see cref="DeriveOffBeats"/> every update — nothing mutates them in place afterwards, so the
    /// capture can hold their references without copying.
    /// </summary>
    private OffBeatsView CaptureOffBeats()
    {
        var synced = IsSynced;
        var lanes = GateLanes.Capture(
            synced,
            CurrentCountOrNone(synced, beatData.snapshot.beatInBar),
            offBeatsCountMs,
            offBeats,
            previousOffBeatGates);
        return new OffBeatsView(lanes);
    }
}
