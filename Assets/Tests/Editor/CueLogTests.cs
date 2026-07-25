// Seam tests for the per-run trace sink. Everything goes through CueLog's public surface
// (CreateForSession / Write / Dispose) against a throwaway directory, so what is pinned is the
// file behavior a session actually depends on: rotation, lazy creation, per-line flushing, and
// failing quietly rather than taking the wall down mid-performance.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Behavioral coverage for <see cref="CueLog"/> through its session seam.</summary>
public sealed class CueLogTests
{
    private string logsDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        logsDir = Path.Combine(Path.GetTempPath(), "penrose-cuelog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logsDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(logsDir))
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }

    /// <summary>Pins rotation: starting a session leaves the directory holding the newest logs and no more.</summary>
    [Test]
    public void StartingASessionKeepsOnlyTheNewestSessionLogs()
    {
        const int existing = CueLog.MaxSessionLogs + 5;
        for (var i = 0; i < existing; i++)
        {
            var path = Path.Combine(logsDir, $"penrose-old-{i:00}.log");
            File.WriteAllText(path, "old session");
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i));
        }

        using (var log = CueLog.CreateForSession(logsDir))
        {
            log.Write("this session");
        }

        var remaining = SessionLogNames();
        Assert.That(remaining.Count, Is.EqualTo(CueLog.MaxSessionLogs), "The newest logs, including this session's, survive.");
        Assert.That(remaining.Contains("penrose-old-00.log"), Is.False, "The oldest log was rotated away.");
        Assert.That(remaining.Contains($"penrose-old-{existing - 1:00}.log"), Is.True, "The newest existing log survived.");
    }

    /// <summary>Pins lazy creation: a run that never traces leaves no file behind at all.</summary>
    [Test]
    public void ASessionThatNeverWritesLeavesNoFileBehind()
    {
        using (CueLog.CreateForSession(logsDir))
        {
            Assert.That(SessionLogNames().Count, Is.Zero, "Naming the session does not create the file.");
        }

        Assert.That(SessionLogNames().Count, Is.Zero, "Disposing an unused sink still leaves nothing.");
    }

    /// <summary>
    /// Pins the per-line flush and the record shape: each line is readable while the session is still running,
    /// so a run killed mid-performance leaves everything it traced.
    /// </summary>
    [Test]
    public void EachWrittenLineIsTimestampedAndReadableBeforeDispose()
    {
        using (var log = CueLog.CreateForSession(logsDir))
        {
            log.Write("SWITCHER_PERFORM impact=33");
            log.Write("Director MODE Standalone->Synced");

            var live = ReadSessionLines();
            Assert.That(live.Length, Is.EqualTo(2), "Every line is flushed as it is written.");
        }

        var lines = ReadSessionLines();
        Assert.That(lines.Length, Is.EqualTo(2));
        Assert.That(lines[0], Does.Match(@"^\d{4}-\d{2}-\d{2} \d{2}\D\d{2}\D\d{2}\D\d{3} SWITCHER_PERFORM impact=33$"));
        Assert.That(lines[1], Does.EndWith("Director MODE Standalone->Synced"));
    }

    /// <summary>
    /// Pins failing quietly: a session directory that cannot be created disables the sink after one warning,
    /// and every later line is dropped in silence rather than warning again or throwing into the frame loop.
    /// </summary>
    [Test]
    public void AnUnopenableSessionWarnsOnceAndThenStaysQuiet()
    {
        // A plain file where the log directory should be, so creating the directory fails on the first line.
        var blockedDir = Path.Combine(logsDir, "blocked");
        File.WriteAllText(blockedDir, "not a directory");

        var warnings = CountWarningsWhile(() =>
        {
            using var log = CueLog.CreateForSession(blockedDir);
            log.Write("first");
            log.Write("second");
            log.Write("third");
        });

        Assert.That(warnings, Is.EqualTo(1), "A broken sink warns once and then stays quiet for the rest of the run.");
        Assert.That(File.ReadAllText(blockedDir), Is.EqualTo("not a directory"), "The blocking file is left alone.");
    }

    /// <summary>Runs <paramref name="body"/> and counts the Unity warnings it emits.</summary>
    private static int CountWarningsWhile(Action body)
    {
        var warnings = 0;
        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
            {
                warnings++;
            }
        }

        Application.logMessageReceived += OnLog;
        try
        {
            body();
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
        }

        return warnings;
    }

    /// <summary>File names of every session log currently in the test directory.</summary>
    private List<string> SessionLogNames()
    {
        return Directory.GetFiles(logsDir, "penrose-*.log")
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .ToList();
    }

    /// <summary>Reads the one session log, sharing the handle so an open session can still be read.</summary>
    private string[] ReadSessionLines()
    {
        var files = Directory.GetFiles(logsDir, "penrose-*.log");
        Assert.That(files.Length, Is.EqualTo(1), "Exactly one session log is expected.");
        using var stream = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n').Where(line => line.Trim().Length > 0).ToArray();
    }
}
