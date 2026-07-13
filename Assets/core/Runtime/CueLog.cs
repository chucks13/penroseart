using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Which held Cue Sheet slot a <c>SHEET_BUILT</c> event refers to.</summary>
public enum CueLogSlot
{
    /// <summary>The current Phrase's Cue Sheet.</summary>
    Current,

    /// <summary>The next Phrase's Cue Sheet, built ahead from the announcement.</summary>
    Next,
}

/// <summary>Why a Cue Sheet landed in a slot: a first build or a rebuild over a changed announcement.</summary>
public enum CueLogBuildReason
{
    /// <summary>The slot was empty and a sheet was built from the Phrase announcement.</summary>
    Build,

    /// <summary>The slot held a sheet whose announcement changed, so it was rebuilt.</summary>
    Rebuild,
}

/// <summary>The event flavor a cast leaned toward: a Fill on this Grid, a Drop on the next, or neither.</summary>
public enum CueFlavor
{
    /// <summary>No Fill or Drop preference flavored the cast.</summary>
    None,

    /// <summary>A Fill on this Grid made Fill-capable Repertoire preferred.</summary>
    Fill,

    /// <summary>A Drop on the next Grid made Drop-capable Repertoire preferred.</summary>
    Drop,
}

/// <summary>Whether an accepted Loaded Cue was a fresh load or replaced a still-unlocked loaded cue.</summary>
public enum CueLogUpsert
{
    /// <summary>No cue was loaded before; this is a fresh load.</summary>
    New,

    /// <summary>An unlocked loaded cue was displaced by this one.</summary>
    Replaced,
}

/// <summary>Which Switcher clock latched the Lock Point: an offer's beat or the render clock's wall time.</summary>
public enum CueLockVia
{
    /// <summary>Latched by <c>LatchLockAtBeat</c> — an offer observed a beat at or past the Lock Point.</summary>
    Beat,

    /// <summary>Latched by <c>LatchLockAtTime</c> — the render clock reached the Lock Point wall time.</summary>
    Render,
}

/// <summary>
/// Pure formatters for the operator-facing Cue Log line shapes. Each method returns the exact event body
/// (without the per-line wall-clock timestamp, which the <see cref="CueLog"/> sink prepends), so the line
/// grammar is unit-testable with no clock, file IO, or BeatManager. Grid position and Phrase label are
/// rendered here from live values, never remembered verdicts.
/// </summary>
public static class CueLogFormat
{
    /// <summary>Renders the shared grid-position token: <c>grid=&lt;beat&gt;/16 bar=&lt;bar&gt;</c>, or <c>grid=off</c> when off the Grid.</summary>
    public static string GridPosition(GridFacts? grid) =>
        grid is { Beat: { } beat, Bar: { } bar } ? $"grid={beat}/16 bar={bar}" : "grid=off";

    /// <summary>Renders the Phrase-label token: <c>phrase="&lt;label&gt;"</c>, or <c>phrase=?</c> when the wire carried no label.</summary>
    public static string Phrase(string label) =>
        string.IsNullOrEmpty(label) ? "phrase=?" : $"phrase=\"{label}\"";

    /// <summary>Renders the bare Cue identity: <c>&lt;absoluteBeat&gt;[&lt;phraseRelativeOffset&gt;/&lt;phraseLength&gt;]</c>.</summary>
    public static string Cue(int absoluteBeat, int phraseRelativeOffset, int phraseLength) =>
        $"{absoluteBeat}[{phraseRelativeOffset}/{phraseLength}]";

    /// <summary>
    /// Renders a Phrase announcement identity: <c>"&lt;label&gt;"/&lt;length&gt;</c>, or <c>?/&lt;length&gt;</c> when
    /// the wire carried no label — the same unknown-label convention as <see cref="Phrase"/>.
    /// </summary>
    public static string Announcement(string label, int length) =>
        $"{(string.IsNullOrEmpty(label) ? "?" : Quote(label))}/{length}";

    /// <summary>
    /// Formats a <c>PHRASE_TURNOVER</c> line: one observed phrase-lane wrap, carrying grid context and both
    /// sides of the boundary. A same-(label, length) turnover reads plainly as its own line.
    /// </summary>
    public static string PhraseTurnover(
        GridFacts? grid,
        string outgoingLabel,
        int outgoingLength,
        string incomingLabel,
        int incomingLength,
        int instance) =>
        $"PHRASE_TURNOVER {GridPosition(grid)} out={Announcement(outgoingLabel, outgoingLength)} in={Announcement(incomingLabel, incomingLength)} instance={instance}";

    /// <summary>
    /// Formats a <c>NEXT_PHRASE</c> line: the next-announcement identity changed against the previous
    /// observation. <c>replaced=none</c> renders an announcement appearing after absence. <c>instance</c> is the
    /// wrap ordinal the announced Phrase is tracked as (the coming instance the next slot is keyed to).
    /// </summary>
    public static string NextPhrase(
        GridFacts? grid,
        string newLabel,
        int newLength,
        string replacedLabel,
        int? replacedLength,
        int instance) =>
        $"NEXT_PHRASE {GridPosition(grid)} next={Announcement(newLabel, newLength)}"
        + $" replaced={(replacedLength is { } length ? Announcement(replacedLabel, length) : "none")} instance={instance}";

    /// <summary>Formats a <c>SHEET_BUILT</c> line for a build or rebuild landing in a slot (never promotion).</summary>
    public static string SheetBuilt(
        CueLogSlot slot,
        CueLogBuildReason reason,
        GridFacts? grid,
        string phrase,
        int start,
        int length,
        IReadOnlyList<int> marks) =>
        $"SHEET_BUILT slot={SlotToken(slot)} reason={ReasonToken(reason)} {GridPosition(grid)} {Phrase(phrase)}"
        + $" start={start} length={length} marks=[{string.Join(",", marks ?? Array.Empty<int>())}]";

    /// <summary>Formats a <c>CUE_CAST</c> line: one Director cast offered to the Switcher, with its answer.</summary>
    public static string CueCast(
        GridFacts? grid,
        string phrase,
        int cueMarkBeat,
        int phraseRelativeOffset,
        int phraseLength,
        string effectName,
        string transitionName,
        CueFlavor flavor,
        bool accepted) =>
        $"CUE_CAST {GridPosition(grid)} {Phrase(phrase)} cue={Cue(cueMarkBeat, phraseRelativeOffset, phraseLength)}"
        + $" effect={Quote(effectName)} transition={Quote(transitionName)} flavor={FlavorToken(flavor)} result={(accepted ? "accepted" : "rejected")}";

    /// <summary>Formats a <c>CUE_KEPT</c> line: the keep-guard holds a workable loaded cue when a new Grid carries a mark.</summary>
    public static string CueKept(
        GridFacts? grid,
        string phrase,
        int offeredCueMarkBeat,
        int loadedCueMarkBeat,
        int loadedPhraseRelativeOffset,
        int loadedPhraseLength,
        string effectName,
        string transitionName) =>
        $"CUE_KEPT {GridPosition(grid)} {Phrase(phrase)} offered={offeredCueMarkBeat}"
        + $" loaded={Cue(loadedCueMarkBeat, loadedPhraseRelativeOffset, loadedPhraseLength)}"
        + $" effect={Quote(effectName)} transition={Quote(transitionName)}";

    /// <summary>Formats a <c>CUE_LOADED</c> line: the Switcher accepted a cue (fresh or upsert-replace).</summary>
    public static string CueLoaded(
        GridFacts? grid,
        string phrase,
        int cueMarkBeat,
        int phraseRelativeOffset,
        int phraseLength,
        string effectName,
        string transitionName,
        int startBeat,
        int lockPointBeat,
        int runwayBeats,
        int tailBeats,
        CueLogUpsert upsert,
        int? displacedCueMarkBeat)
    {
        var line = $"CUE_LOADED {GridPosition(grid)} {Phrase(phrase)} cue={Cue(cueMarkBeat, phraseRelativeOffset, phraseLength)}"
            + $" effect={Quote(effectName)} transition={Quote(transitionName)} start={startBeat} lock={lockPointBeat}"
            + $" runway={runwayBeats} tail={tailBeats} upsert={UpsertToken(upsert)}";
        return displacedCueMarkBeat is { } displaced ? line + $" displaced={displaced}" : line;
    }

    /// <summary>Formats a <c>CUE_LOCKED</c> line: the monotonic lock latched for a loaded cue (once per cue).</summary>
    public static string CueLocked(
        GridFacts? grid,
        string phrase,
        int cueMarkBeat,
        int phraseRelativeOffset,
        int phraseLength,
        string effectName,
        string transitionName,
        int lockedAtBeat,
        CueLockVia via) =>
        $"CUE_LOCKED {GridPosition(grid)} {Phrase(phrase)} cue={Cue(cueMarkBeat, phraseRelativeOffset, phraseLength)}"
        + $" effect={Quote(effectName)} transition={Quote(transitionName)} lockedAt={lockedAtBeat} via={ViaToken(via)}";

    private static string Quote(string value) => $"\"{value ?? string.Empty}\"";

    private static string SlotToken(CueLogSlot slot) => slot == CueLogSlot.Current ? "current" : "next";

    private static string ReasonToken(CueLogBuildReason reason) => reason == CueLogBuildReason.Build ? "build" : "rebuild";

    private static string FlavorToken(CueFlavor flavor) => flavor switch
    {
        CueFlavor.Fill => "fill",
        CueFlavor.Drop => "drop",
        _ => "none",
    };

    private static string UpsertToken(CueLogUpsert upsert) => upsert == CueLogUpsert.New ? "new" : "replaced";

    private static string ViaToken(CueLockVia via) => via == CueLockVia.Beat ? "beat" : "render";
}

/// <summary>
/// Per-session operator log sink for the five Cue-lifecycle events (SHEET_BUILT, CUE_CAST, CUE_KEPT,
/// CUE_LOADED, CUE_LOCKED). It is a downstream view like the Observatory: the runtime never keeps state
/// just to feed it, and every event it receives is already an event-driven moment in the Director/Switcher
/// — nothing here runs per frame.
/// </summary>
/// <remarks>
/// The Director calls the first four methods, holding the display context (Phrase label, effect/transition
/// names, Cue offsets) at the accept. CUE_LOCKED latches deep in the Switcher, which has no Phrase or name
/// context, so it calls <see cref="CueLocked"/> with only the raw latch facts and the sink joins them with
/// the display context it remembered from the matching <see cref="CueLoaded"/> — a display-side join, not
/// runtime decision memory. Grid position is read live through the injected probe at write time. Writes are
/// buffered and flushed per event (events are rare) and never throw into the runtime: a write failure is
/// swallowed to a single <see cref="Debug.LogWarning"/>. The backing file is created lazily on the first
/// event and closed on Controller teardown via <see cref="Dispose"/>.
/// </remarks>
public sealed class CueLog : IDisposable
{
    /// <summary>Newest session log files kept on startup; older ones are deleted.</summary>
    public const int MaxSessionLogs = 20;

    private readonly Func<TextWriter> writerFactory;
    /// <summary>Reads canonical Grid facts at the instant an operator-facing line is written.</summary>
    private readonly Func<GridFacts?> gridProbe;
    private readonly bool ownsWriter;
    private TextWriter writer;
    private bool writerFailed;
    private bool warned;

    private bool hasLastLoaded;
    private int lastLoadedCueMarkBeat;
    private int lastLoadedOffset;
    private int lastLoadedLength;
    private string lastLoadedPhrase;
    private string lastLoadedEffectName;
    private string lastLoadedTransitionName;

    /// <summary>
    /// Creates a sink over an injected writer factory (opened lazily on first event) and a grid probe read
    /// live at write time. Test callers pass an in-memory writer and a fixed probe; <paramref name="ownsWriter"/>
    /// controls whether <see cref="Dispose"/> disposes the writer.
    /// </summary>
    public CueLog(Func<TextWriter> writerFactory, Func<GridFacts?> gridProbe, bool ownsWriter = true)
    {
        this.writerFactory = writerFactory ?? throw new ArgumentNullException(nameof(writerFactory));
        this.gridProbe = gridProbe ?? (() => null);
        this.ownsWriter = ownsWriter;
    }

    /// <summary>
    /// Builds the session sink under <paramref name="logsDir"/>, rotating away all but the newest
    /// <see cref="MaxSessionLogs"/> existing logs first. The session file <c>penrose-&lt;yyyyMMdd-HHmmss&gt;.log</c>
    /// is created lazily on the first event so an idle session leaves no file behind.
    /// </summary>
    public static CueLog CreateForSession(string logsDir, Func<GridFacts?> gridProbe)
    {
        RotateSessionLogs(logsDir, MaxSessionLogs - 1);
        var fileName = $"penrose-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        var path = Path.Combine(logsDir, fileName);
        return new CueLog(
            () =>
            {
                Directory.CreateDirectory(logsDir);
                return new StreamWriter(path, append: false);
            },
            gridProbe);
    }

    /// <summary>Deletes oldest <c>penrose-*.log</c> files in <paramref name="logsDir"/> beyond the newest <paramref name="keep"/>.</summary>
    public static void RotateSessionLogs(string logsDir, int keep)
    {
        try
        {
            if (!Directory.Exists(logsDir))
            {
                return;
            }

            var stale = Directory.GetFiles(logsDir, "penrose-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(Math.Max(0, keep));
            foreach (var file in stale)
            {
                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"CueLog rotation skipped: {ex.Message}");
        }
    }

    /// <summary>Records a Cue Sheet build or rebuild landing in a slot (never a promotion).</summary>
    public void SheetBuilt(CueLogSlot slot, CueLogBuildReason reason, string phrase, int start, int length, IReadOnlyList<int> marks)
    {
        Write(CueLogFormat.SheetBuilt(slot, reason, gridProbe(), phrase, start, length, marks));
    }

    /// <summary>Records an observed phrase-lane wrap, with both sides of the boundary and the incoming instance ordinal — logged whether or not a sheet promotes.</summary>
    public void PhraseTurnover(string outgoingLabel, int outgoingLength, string incomingLabel, int incomingLength, int instance)
    {
        Write(CueLogFormat.PhraseTurnover(gridProbe(), outgoingLabel, outgoingLength, incomingLabel, incomingLength, instance));
    }

    /// <summary>
    /// Records a next-announcement identity change observed on the lane itself (including appearing after
    /// absence, rendered <c>replaced=none</c>), tagged with the coming instance the next slot is keyed to —
    /// independent of the sheet build, so a build suppressed downstream still logs the change.
    /// </summary>
    public void NextPhrase(string newLabel, int newLength, string replacedLabel, int? replacedLength, int instance)
    {
        Write(CueLogFormat.NextPhrase(gridProbe(), newLabel, newLength, replacedLabel, replacedLength, instance));
    }

    /// <summary>Records a Director cast offered to the Switcher, with the accept/reject answer.</summary>
    public void CueCast(
        string phrase,
        int cueMarkBeat,
        int phraseRelativeOffset,
        int phraseLength,
        string effectName,
        string transitionName,
        CueFlavor flavor,
        bool accepted)
    {
        Write(CueLogFormat.CueCast(gridProbe(), phrase, cueMarkBeat, phraseRelativeOffset, phraseLength, effectName, transitionName, flavor, accepted));
    }

    /// <summary>
    /// Records the Switcher keeping the same-mark loaded cue — its answer to an offer at the Cue Mark
    /// already loaded; the cue rides unchanged.
    /// </summary>
    public void CueKept(
        string phrase,
        int offeredCueMarkBeat,
        int loadedCueMarkBeat,
        int loadedPhraseRelativeOffset,
        int loadedPhraseLength,
        string effectName,
        string transitionName)
    {
        Write(CueLogFormat.CueKept(gridProbe(), phrase, offeredCueMarkBeat, loadedCueMarkBeat, loadedPhraseRelativeOffset, loadedPhraseLength, effectName, transitionName));
    }

    /// <summary>
    /// Records the Switcher accepting a cue and remembers its display context so a later
    /// <see cref="CueLocked"/> for the same Cue Mark can be joined without the Switcher carrying names.
    /// </summary>
    public void CueLoaded(
        string phrase,
        int cueMarkBeat,
        int phraseRelativeOffset,
        int phraseLength,
        string effectName,
        string transitionName,
        int startBeat,
        int lockPointBeat,
        int runwayBeats,
        int tailBeats,
        CueLogUpsert upsert,
        int? displacedCueMarkBeat)
    {
        hasLastLoaded = true;
        lastLoadedCueMarkBeat = cueMarkBeat;
        lastLoadedOffset = phraseRelativeOffset;
        lastLoadedLength = phraseLength;
        lastLoadedPhrase = phrase;
        lastLoadedEffectName = effectName;
        lastLoadedTransitionName = transitionName;
        Write(CueLogFormat.CueLoaded(gridProbe(), phrase, cueMarkBeat, phraseRelativeOffset, phraseLength, effectName, transitionName, startBeat, lockPointBeat, runwayBeats, tailBeats, upsert, displacedCueMarkBeat));
    }

    /// <summary>
    /// Records the Switcher's monotonic lock latching for the loaded cue. The Switcher passes only the raw
    /// latch facts; the sink joins them with the display context remembered from the matching
    /// <see cref="CueLoaded"/>, and reads grid position live.
    /// </summary>
    public void CueLocked(int cueMarkBeat, int lockedAtBeat, CueLockVia via)
    {
        var joined = hasLastLoaded && lastLoadedCueMarkBeat == cueMarkBeat;
        Write(CueLogFormat.CueLocked(
            gridProbe(),
            joined ? lastLoadedPhrase : null,
            cueMarkBeat,
            joined ? lastLoadedOffset : 0,
            joined ? lastLoadedLength : 0,
            joined ? lastLoadedEffectName : string.Empty,
            joined ? lastLoadedTransitionName : string.Empty,
            lockedAtBeat,
            via));
    }

    /// <summary>Flushes and, when the sink owns the writer, closes the backing file.</summary>
    public void Dispose()
    {
        if (writer == null)
        {
            return;
        }

        try
        {
            writer.Flush();
            if (ownsWriter)
            {
                writer.Dispose();
            }
        }
        catch (Exception ex)
        {
            WarnOnce(ex);
        }
    }

    private void Write(string eventBody)
    {
        var writerInstance = EnsureWriter();
        if (writerInstance == null)
        {
            return;
        }

        try
        {
            writerInstance.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {eventBody}");
            writerInstance.Flush();
        }
        catch (Exception ex)
        {
            WarnOnce(ex);
        }
    }

    private TextWriter EnsureWriter()
    {
        if (writer != null || writerFailed)
        {
            return writer;
        }

        try
        {
            writer = writerFactory();
        }
        catch (Exception ex)
        {
            writerFailed = true;
            WarnOnce(ex);
        }

        return writer;
    }

    private void WarnOnce(Exception ex)
    {
        if (warned)
        {
            return;
        }

        warned = true;
        Debug.LogWarning($"CueLog write disabled after failure: {ex.Message}");
    }
}
