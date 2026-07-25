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

    /// <summary>Where this session's log will be written; the file itself is created on the first line.</summary>
    private readonly string sessionPath;

    /// <summary>The open writer, or null until the first line forces it open.</summary>
    private TextWriter writer;

    /// <summary>Set once opening the file has failed, so the sink stays quiet instead of retrying every line.</summary>
    private bool writerFailed;

    /// <summary>Set once a failure has been reported, so a broken sink warns once rather than every frame.</summary>
    private bool warned;

    /// <summary>Names this session's file. Private because <see cref="CreateForSession"/> owns rotation.</summary>
    /// <param name="sessionPath">Full path of the session log file.</param>
    private CueLog(string sessionPath)
    {
        this.sessionPath = sessionPath;
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
        return new CueLog(Path.Combine(logsDir, $"penrose-{DateTime.Now:yyyyMMdd-HHmmss}.log"));
    }

    /// <summary>Deletes oldest <c>penrose-*.log</c> files in <paramref name="logsDir"/> beyond the newest <paramref name="keep"/>.</summary>
    /// <param name="logsDir">Directory to rotate; a missing directory is a no-op.</param>
    /// <param name="keep">How many of the newest session files survive.</param>
    private static void RotateSessionLogs(string logsDir, int keep)
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

    /// <summary>Flushes and closes the session file. A session that never wrote a line has nothing to close.</summary>
    public void Dispose()
    {
        if (writer == null)
        {
            return;
        }

        try
        {
            writer.Flush();
            writer.Dispose();
        }
        catch (Exception ex)
        {
            WarnOnce(ex);
        }
    }

    /// <summary>Opens the session file on first use, disabling the sink for good if it cannot be opened.</summary>
    /// <returns>The open writer, or null once opening has failed.</returns>
    private TextWriter EnsureWriter()
    {
        if (writer != null || writerFailed)
        {
            return writer;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sessionPath));
            writer = new StreamWriter(sessionPath, append: false);
        }
        catch (Exception ex)
        {
            writerFailed = true;
            WarnOnce(ex);
        }

        return writer;
    }

    /// <summary>Reports the first I/O failure and stays silent afterwards.</summary>
    /// <param name="ex">The failure that disabled or interrupted the sink.</param>
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
