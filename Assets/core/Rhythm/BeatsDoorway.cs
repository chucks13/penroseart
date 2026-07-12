// The Beats doorway: the on-beat cluster — countdowns, per-count gates, and their opening edges
// (beat-data spec). Also home of the gate-lane machinery the OffBeats mirror reuses.

#nullable enable

using System;

/// <summary>
/// The per-count gate machinery behind the Beats and OffBeats doorways (they mirror exactly by
/// design, so the rules live once). Holds the four countdown/gate lanes captured for this frame
/// and serves them with the uniform nullability rule: a lane reads null when the wall is not in
/// Synced Mode or when that lane's countdown carries the wire's -1 sentinel — so an unavailable
/// lane can never read as an open gate.
/// </summary>
internal readonly struct GateLanes
{
    /// <summary>Whether this frame was captured in Synced Mode — every lane fact gates on it.</summary>
    private readonly bool synced;

    /// <summary>The running 1..4 count, or -1 when no count applies this frame.</summary>
    private readonly int currentCount;

    /// <summary>Label-ordered countdowns in ms; -1 marks an unavailable lane. Owned by this frame's capture.</summary>
    private readonly int[]? countdownsMs;

    /// <summary>Label-ordered gate windows. Owned by this frame's capture.</summary>
    private readonly bool[]? gates;

    /// <summary>The current count's lane-opening edge, precomputed at capture (see <see cref="CurrentOpened"/>).</summary>
    private readonly bool currentOpened;

    /// <summary>Label-ordered gate-opening edges, evaluated once at capture.</summary>
    private readonly bool[]? opened;

    /// <summary>Captures the complete label-ordered gate state for one hub frame.</summary>
    /// <param name="synced">Whether the wall had a live four-count when this frame was captured.</param>
    /// <param name="currentCount">The running 1..4 count, or -1 when no count applies.</param>
    /// <param name="countdownsMs">Milliseconds until each labeled count lands; -1 marks an unavailable lane.</param>
    /// <param name="gates">Whether each labeled count is inside its landing window.</param>
    /// <param name="currentOpened">Whether the running count's lane opened on this capture.</param>
    /// <param name="opened">Whether each labeled lane opened on this capture.</param>
    private GateLanes(bool synced, int currentCount, int[] countdownsMs, bool[] gates,
        bool currentOpened, bool[] opened)
    {
        this.synced = synced;
        this.currentCount = currentCount;
        this.countdownsMs = countdownsMs;
        this.gates = gates;
        this.currentOpened = currentOpened;
        this.opened = opened;
    }

    /// <summary>Whether this frame was captured in Synced Mode — the doorway's facts gate on it.</summary>
    internal bool Synced => synced;

    /// <summary>Ms until the soonest lane lands, whatever its label — null when none is available.</summary>
    internal float? SoonestMs
    {
        get
        {
            float? soonest = null;
            if (countdownsMs == null)
            {
                return null;
            }

            for (var label = 1; label <= countdownsMs.Length; label++)
            {
                var candidate = MsUntil(label);
                if (candidate is { } value && (soonest is not { } best || value < best))
                {
                    soonest = value;
                }
            }

            return soonest;
        }
    }

    /// <summary>The current count's gate — current-count semantics, null when no count applies.</summary>
    internal bool? CurrentGate => currentCount >= 1 ? Gate(currentCount) : null;

    /// <summary>
    /// Edge: the current count's gate opened this frame — exactly its lane's opening edge, so a
    /// beat that lands while the count advances (a frame hitch spanning two counts' open windows)
    /// still reads as an opening. Never null.
    /// </summary>
    internal bool CurrentOpened => currentOpened;

    /// <summary>Ms until the given 1-based label next lands; a landed label holds at 0 while its gate is open.</summary>
    internal float? MsUntil(int label)
    {
        var slot = label - 1;
        if (!synced || countdownsMs == null || slot < 0 || slot >= countdownsMs.Length || countdownsMs[slot] < 0)
        {
            return null;
        }

        return countdownsMs[slot];
    }

    /// <summary>The given 1-based label's gate window; null when that lane is unavailable.</summary>
    internal bool? Gate(int label)
    {
        return Serve(synced, countdownsMs, gates, label);
    }

    /// <summary>Edge: the given 1-based label's gate opened this frame. Never null.</summary>
    internal bool GateOpened(int label)
    {
        var slot = label - 1;
        return opened != null && slot >= 0 && slot < opened.Length && opened[slot];
    }

    /// <summary>
    /// The one spelling of the served-gate rule, shared by reads and capture: a lane reads null
    /// when not in Synced Mode or when its countdown carries the wire's -1 sentinel — an
    /// unavailable lane can never read as an open gate.
    /// </summary>
    private static bool? Serve(bool synced, int[]? countdownsMs, bool[]? gates, int label)
    {
        var slot = label - 1;
        if (!synced || countdownsMs == null || gates == null ||
            slot < 0 || slot >= countdownsMs.Length || slot >= gates.Length || countdownsMs[slot] < 0)
        {
            return null;
        }

        return gates[slot];
    }

    /// <summary>
    /// Captures one frame's lanes and evaluates every gate-opening edge against the prior served
    /// gates the hub retains between updates (ADR-0015). An opening is (closed or unavailable) →
    /// open of the <em>served</em> gate, so the edge can never fire off a sentinel. The current
    /// count's opening is its own lane's opening — never a transition of the served On Beat value,
    /// which would miss a beat landing on a count change. The arrays passed in must be owned by
    /// this capture (copied from, or rebuilt apart from, the snapshot).
    /// </summary>
    internal static GateLanes Capture(bool synced, int currentCount, int[] countdownsMs, bool[] gates,
        bool?[] previousGates)
    {
        var opened = new bool[previousGates.Length];
        for (var slot = 0; slot < previousGates.Length; slot++)
        {
            var served = Serve(synced, countdownsMs, gates, slot + 1);
            opened[slot] = Edges.Opened(previousGates[slot], served);
            previousGates[slot] = served;
        }

        var currentOpened = currentCount >= 1 && currentCount <= opened.Length && opened[currentCount - 1];
        return new GateLanes(synced, currentCount, countdownsMs, gates, currentOpened, opened);
    }
}

/// <summary>
/// The on-beat cluster, one Data Surface doorway: when the next hit lands, whether we are on one
/// now, and the per-count detail for count-specific wants ("hit on every 2 and 4"). Facts are
/// nullable; edges are never null and rest at false.
/// </summary>
public readonly struct BeatsView
{
    private readonly GateLanes lanes;
    private readonly int nextBarMs;

    /// <summary>
    /// Next Beat: ms until the next hit, whatever its count — the soonest of the four per-count
    /// countdowns. Contrived.
    /// </summary>
    public float? NextBeatMs => lanes.SoonestMs;

    /// <summary>Ms until the next bar line — pre-arm a hit on the One. Never holds at 0. Wire fact.</summary>
    public float? NextBarMs => lanes.Synced && nextBarMs >= 0 ? nextBarMs : (float?)null;

    /// <summary>
    /// On Beat: is the <em>current</em> count's gate open (the first quarter of the beat
    /// interval). Contrived; current-count semantics.
    /// </summary>
    public bool? OnBeat => lanes.CurrentGate;

    /// <summary>Edge: the current count's gate opened this frame — "a beat landed". Never null.</summary>
    public bool OnBeatOpened => lanes.CurrentOpened;

    /// <summary>
    /// Per-count countdown: ms until the given count (1..4) next lands. A landed count holds at 0
    /// while its gate is open. Wire fact.
    /// </summary>
    public float? MsUntil(int count) => lanes.MsUntil(count);

    /// <summary>Per-count gate: is the given count's window open. Wire fact (the on_beats lanes).</summary>
    public bool? Gate(int count) => lanes.Gate(count);

    /// <summary>Edge: the given count's gate opened this frame. Never null — every gate has an opening edge.</summary>
    public bool GateOpened(int count) => lanes.GateOpened(count);

    /// <summary>Built only by the hub's per-update capture over that frame's gate lanes; nextBarMs arrives raw (-1 = unavailable).</summary>
    internal BeatsView(GateLanes lanes, int nextBarMs)
    {
        this.lanes = lanes;
        this.nextBarMs = nextBarMs;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Beats doorway, captured once per hub update ahead of effect Draw — identical for every
    /// reader within a frame.
    /// </summary>
    public BeatsView Beats { get; private set; }

    /// <summary>
    /// Prior served per-count gates, retained between hub updates for the opening edges
    /// (<see cref="BeatsView.OnBeatOpened"/> is the current count's lane edge, so this is the
    /// only edge memory the doorway needs).
    /// </summary>
    private readonly bool?[] previousBeatGates = new bool?[BeatSlotCount];

    /// <summary>
    /// Captures the Beats doorway from the settled transport state. The snapshot-owned countdown
    /// and gate arrays are copied — the transport (and tests) mutate them in place, and a captured
    /// view must stay identical for every reader until the next hub update.
    /// </summary>
    private BeatsView CaptureBeats()
    {
        var snapshot = beatData.snapshot;
        var synced = IsSynced;
        var lanes = GateLanes.Capture(
            synced,
            CurrentCountOrNone(synced, snapshot.beatInBar),
            CopyCountdownLanes(snapshot.beatsCountMs),
            CopyGateLanes(snapshot.onBeats),
            previousBeatGates);
        return new BeatsView(lanes, snapshot.bar.nextMs);
    }

    /// <summary>The running 1..4 count for current-count semantics, or -1 when no count applies.</summary>
    private static int CurrentCountOrNone(bool synced, int beatInBar)
    {
        return synced && beatInBar >= 1 && beatInBar <= BeatSlotCount ? beatInBar : -1;
    }

    /// <summary>Copies the wire countdowns into a capture-owned four-lane array (missing lanes read -1).</summary>
    private static int[] CopyCountdownLanes(int[]? source)
    {
        var copy = CreateUnavailableCountdowns();
        if (source != null)
        {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }

        return copy;
    }

    /// <summary>Copies the wire gates into a capture-owned four-lane array (missing lanes read closed).</summary>
    private static bool[] CopyGateLanes(bool[]? source)
    {
        var copy = new bool[BeatSlotCount];
        if (source != null)
        {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }

        return copy;
    }
}
