using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Per-run diagnostic sink for sequencing traces. Writes one session file,
/// <c>penrose-&lt;yyyyMMdd-HHmmss&gt;.log</c>, holding one timestamped line per event, and rotates
/// older session files away so the directory stays readable.
/// <para>
/// This is the sink only — it owns the file, not the record format. Callers hand it a finished
/// line; the vocabulary of those lines belongs to whoever writes them (today
/// <see cref="Controller.LogDirectorSwitching"/>). Keeping it that way is what lets the trace
/// vocabulary change with the runtime without touching the file plumbing.
/// </para>
/// <para>
/// A run that never traces leaves no file behind: the writer opens lazily on the first line. Any
/// I/O failure disables the sink and warns once rather than throwing — a broken log must never
/// take the wall down mid-performance.
/// </para>
/// </summary>
public sealed class CueLog : IDisposable
{
    /// <summary>Newest session log files kept on startup; older ones are deleted.</summary>
    public const int MaxSessionLogs = 20;

    private readonly Func<TextWriter> writerFactory;
    private readonly bool ownsWriter;
    private TextWriter writer;
    private bool writerFailed;
    private bool warned;

    /// <summary>
    /// Creates a sink over an injected writer factory, opened lazily on the first line. Test callers
    /// pass an in-memory writer; <paramref name="ownsWriter"/> controls whether <see cref="Dispose"/>
    /// disposes it.
    /// </summary>
    /// <param name="writerFactory">Opens the backing writer; called at most once.</param>
    /// <param name="ownsWriter">When true, <see cref="Dispose"/> closes the writer as well as flushing it.</param>
    public CueLog(Func<TextWriter> writerFactory, bool ownsWriter = true)
    {
        this.writerFactory = writerFactory ?? throw new ArgumentNullException(nameof(writerFactory));
        this.ownsWriter = ownsWriter;
    }

    /// <summary>
    /// Builds the session sink under <paramref name="logsDir"/>, rotating away all but the newest
    /// <see cref="MaxSessionLogs"/> existing logs first. The session file is created lazily on the
    /// first line, so an idle session leaves no file behind.
    /// </summary>
    /// <param name="logsDir">Directory holding the <c>penrose-*.log</c> session files.</param>
    /// <returns>A sink writing a fresh session file named for the current local time.</returns>
    public static CueLog CreateForSession(string logsDir)
    {
        RotateSessionLogs(logsDir, MaxSessionLogs - 1);
        var path = Path.Combine(logsDir, $"penrose-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        return new CueLog(() =>
        {
            Directory.CreateDirectory(logsDir);
            return new StreamWriter(path, append: false);
        });
    }

    /// <summary>Deletes oldest <c>penrose-*.log</c> files in <paramref name="logsDir"/> beyond the newest <paramref name="keep"/>.</summary>
    /// <param name="logsDir">Directory to rotate; a missing directory is a no-op.</param>
    /// <param name="keep">How many of the newest session files survive.</param>
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

    /// <summary>
    /// Writes one timestamped line and flushes it, so a session killed mid-run still leaves a
    /// readable log. Failures disable the sink rather than propagating.
    /// </summary>
    /// <param name="eventBody">The finished record text, without a timestamp.</param>
    public void Write(string eventBody)
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
