// File-backed Waveform Pool editor lifecycle that keeps persisted state outside Unity Undo snapshots.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>The outcome of reading and parsing one Waveform Pool document.</summary>
internal enum WaveformPoolDocumentLoadStatus
{
    /// <summary>The complete disk document was represented safely.</summary>
    Loaded,

    /// <summary>File I/O or an unrecoverable record prevented replacement of the current draft.</summary>
    Failed,
}

/// <summary>The outcome of writing one Waveform Pool document.</summary>
internal enum WaveformPoolDocumentSaveStatus
{
    /// <summary>The complete document was written and became the persisted baseline.</summary>
    Saved,

    /// <summary>The disk document changed after the accepted load or save.</summary>
    ExternalChange,

    /// <summary>Validation, serialization, or file I/O prevented the write.</summary>
    Failed,
}

/// <summary>An immutable candidate document produced by one transactional disk read.</summary>
internal readonly struct WaveformPoolDocumentLoadResult
{
    /// <summary>Creates one load result.</summary>
    /// <param name="status">Whether the complete document loaded safely.</param>
    /// <param name="entries">Every parseable Pool entry.</param>
    /// <param name="diagnostics">Structured codec findings from the read.</param>
    /// <param name="error">A user-facing I/O or data-loss failure.</param>
    /// <param name="fileExists">Whether the candidate came from an existing Pool file.</param>
    /// <param name="fileHash">The exact disk-content identity.</param>
    public WaveformPoolDocumentLoadResult(
        WaveformPoolDocumentLoadStatus status,
        IReadOnlyList<WaveformPool.Entry> entries,
        IReadOnlyList<string> diagnostics,
        string error,
        bool fileExists,
        string fileHash)
    {
        Status = status;
        Entries = entries;
        Diagnostics = diagnostics;
        Error = error;
        FileExists = fileExists;
        FileHash = fileHash;
    }

    /// <summary>Whether the complete document loaded safely.</summary>
    public WaveformPoolDocumentLoadStatus Status { get; }

    /// <summary>Every parseable Pool entry.</summary>
    public IReadOnlyList<WaveformPool.Entry> Entries { get; }

    /// <summary>Structured codec findings from the read.</summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>A user-facing I/O or data-loss failure.</summary>
    public string Error { get; }

    /// <summary>Whether the candidate came from an existing Pool file.</summary>
    public bool FileExists { get; }

    /// <summary>The exact disk-content identity accepted with this candidate.</summary>
    internal string FileHash { get; }
}

/// <summary>An immutable result from one attempted Waveform Pool document write.</summary>
internal readonly struct WaveformPoolDocumentSaveResult
{
    /// <summary>Creates one save result.</summary>
    /// <param name="status">Whether the document saved, conflicted, or failed.</param>
    /// <param name="error">A user-facing failure reason.</param>
    public WaveformPoolDocumentSaveResult(WaveformPoolDocumentSaveStatus status, string error)
    {
        Status = status;
        Error = error;
    }

    /// <summary>Whether the document saved, conflicted, or failed.</summary>
    public WaveformPoolDocumentSaveStatus Status { get; }

    /// <summary>A user-facing failure reason.</summary>
    public string Error { get; }
}

/// <summary>
/// Owns the Waveform Pool editor's disk identity and persisted draft baseline independently of Unity Undo.
/// </summary>
/// <remarks>
/// This module is deliberately specific to the Waveform Pool text document. The Editor window remains the Unity
/// Undo and IMGUI adapter; tests and the window use this interface for transactional load, conflict-safe save, and
/// content-based dirty state without touching the production Pool path.
/// </remarks>
internal sealed class WaveformPoolDocument
{
    /// <summary>Session-state namespace for persisted baselines that must survive window reconstruction.</summary>
    private const string SessionPrefix = "Penrose.WaveformPoolDocument.";

    /// <summary>The external Pool file owned by this lifecycle.</summary>
    private readonly string path;

    /// <summary>The path-specific editor-session storage key.</summary>
    private readonly string sessionKey;

    /// <summary>The exact disk identity at the last accepted load or successful save.</summary>
    private string loadedFileHash = "";

    /// <summary>The editable-content identity at the last accepted load or successful save.</summary>
    private string cleanDraftFingerprint = "";

    /// <summary>Whether an accepted persisted baseline exists for comparison.</summary>
    private bool hasBaseline;

    /// <summary>Creates a document lifecycle for one Pool file path and restores its editor-session baseline.</summary>
    /// <param name="path">The Pool text file owned by this lifecycle.</param>
    public WaveformPoolDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A Waveform Pool document path is required.", nameof(path));
        }

        this.path = path;
        sessionKey = SessionPrefix + Hash128.Compute(Path.GetFullPath(path));
        hasBaseline = SessionState.GetBool(sessionKey + ".established", false);
        if (hasBaseline)
        {
            loadedFileHash = SessionState.GetString(sessionKey + ".file", "");
            cleanDraftFingerprint = SessionState.GetString(sessionKey + ".draft", "");
        }
    }

    /// <summary>Whether this editor session has accepted a successful load or save for the document.</summary>
    public bool HasBaseline => hasBaseline;

    /// <summary>Reads and parses the complete file without changing the accepted baseline or caller drafts.</summary>
    public WaveformPoolDocumentLoadResult Load()
    {
        if (!TryReadFile(out var text, out var exists, out var error))
        {
            return new WaveformPoolDocumentLoadResult(
                WaveformPoolDocumentLoadStatus.Failed,
                Array.Empty<WaveformPool.Entry>(),
                Array.Empty<string>(),
                error,
                fileExists: false,
                "");
        }

        var entries = WaveformPool.Parse(text, out var diagnostics);
        if (HasUnrecoverableRecord(diagnostics))
        {
            return new WaveformPoolDocumentLoadResult(
                WaveformPoolDocumentLoadStatus.Failed,
                entries,
                diagnostics,
                "The Pool contains a record that could not be represented safely. " +
                "The current Drafts were preserved; fix the source text and reload.",
                fileExists: true,
                HashFileContent(text, exists));
        }

        return new WaveformPoolDocumentLoadResult(
            WaveformPoolDocumentLoadStatus.Loaded,
            entries,
            diagnostics,
            "",
            exists,
            HashFileContent(text, exists));
    }

    /// <summary>Accepts a successful load after the window has adopted its entries as the current draft.</summary>
    /// <param name="loaded">The complete candidate returned by <see cref="Load"/>.</param>
    /// <param name="draftFingerprint">The adopted draft's editable-content fingerprint.</param>
    public void AcceptLoad(WaveformPoolDocumentLoadResult loaded, string draftFingerprint)
    {
        if (loaded.Status != WaveformPoolDocumentLoadStatus.Loaded)
        {
            throw new ArgumentException("Only a successful complete load can become the persisted baseline.", nameof(loaded));
        }

        SetBaseline(loaded.FileHash, draftFingerprint);
    }

    /// <summary>Writes the complete valid Pool unless an unapproved external change is present.</summary>
    /// <param name="entries">The complete Pool entries to serialize.</param>
    /// <param name="draftFingerprint">The editable-content fingerprint that becomes clean after success.</param>
    /// <param name="overwriteExternalChange">Whether a detected external edit may be replaced.</param>
    public WaveformPoolDocumentSaveResult Save(
        IReadOnlyList<WaveformPool.Entry> entries,
        string draftFingerprint,
        bool overwriteExternalChange)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        if (entries.Count == 0)
        {
            return new WaveformPoolDocumentSaveResult(
                WaveformPoolDocumentSaveStatus.Failed,
                "The Pool must contain at least one Preset.");
        }

        if (!TryReadFile(out var currentText, out var currentExists, out var readError))
        {
            return new WaveformPoolDocumentSaveResult(WaveformPoolDocumentSaveStatus.Failed, readError);
        }

        if (hasBaseline && HashFileContent(currentText, currentExists) != loadedFileHash && !overwriteExternalChange)
        {
            return new WaveformPoolDocumentSaveResult(WaveformPoolDocumentSaveStatus.ExternalChange, "");
        }

        string serialized;
        try
        {
            serialized = WaveformPool.Serialize(entries);
            File.WriteAllText(path, serialized);
        }
        catch (Exception exception)
        {
            return new WaveformPoolDocumentSaveResult(
                WaveformPoolDocumentSaveStatus.Failed,
                $"Failed to write {Path.GetFileName(path)}: {exception.Message}");
        }

        SetBaseline(HashFileContent(serialized, exists: true), draftFingerprint);
        return new WaveformPoolDocumentSaveResult(WaveformPoolDocumentSaveStatus.Saved, "");
    }

    /// <summary>Returns whether the current editable content differs from the accepted persisted baseline.</summary>
    /// <param name="draftFingerprint">The current editable-content fingerprint.</param>
    /// <param name="recoveredValuesNeedSave">Whether loading normalized recoverable source values.</param>
    public bool IsDirty(string draftFingerprint, bool recoveredValuesNeedSave)
    {
        return recoveredValuesNeedSave || !hasBaseline || draftFingerprint != cleanDraftFingerprint;
    }

    /// <summary>Erases one path's retained editor-session baseline.</summary>
    /// <param name="path">The Pool document path whose baseline should be forgotten.</param>
    internal static void ForgetBaseline(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var key = SessionPrefix + Hash128.Compute(Path.GetFullPath(path));
        SessionState.EraseBool(key + ".established");
        SessionState.EraseString(key + ".file");
        SessionState.EraseString(key + ".draft");
    }

    /// <summary>Whether a codec diagnostic means source content could not be represented as editable entries.</summary>
    /// <param name="diagnostic">One diagnostic emitted by the canonical Pool codec.</param>
    internal static bool DiagnosticBlocksSave(string diagnostic)
    {
        return diagnostic.StartsWith("Malformed DEFINE_WAVEFORM", StringComparison.Ordinal) ||
               diagnostic.Contains("field(s), expected 4");
    }

    /// <summary>Stores one accepted disk/draft identity outside the window's Undoable serialized state.</summary>
    /// <param name="fileHash">The exact accepted disk-content identity.</param>
    /// <param name="draftFingerprint">The corresponding editable-content identity.</param>
    private void SetBaseline(string fileHash, string draftFingerprint)
    {
        loadedFileHash = fileHash ?? "";
        cleanDraftFingerprint = draftFingerprint ?? "";
        hasBaseline = true;
        SessionState.SetString(sessionKey + ".file", loadedFileHash);
        SessionState.SetString(sessionKey + ".draft", cleanDraftFingerprint);
        SessionState.SetBool(sessionKey + ".established", true);
    }

    /// <summary>Reads the exact file while distinguishing a missing document from an empty document.</summary>
    /// <param name="text">The complete file content, or empty on failure or absence.</param>
    /// <param name="exists">Whether a regular file existed when read.</param>
    /// <param name="error">A user-facing read failure that promises draft preservation.</param>
    private bool TryReadFile(out string text, out bool exists, out string error)
    {
        try
        {
            if (Directory.Exists(path))
            {
                exists = false;
                text = "";
                error = $"Could not read {Path.GetFileName(path)}; the current Drafts were preserved. " +
                        "The document path is a directory, not a file.";
                return false;
            }

            exists = File.Exists(path);
            text = exists ? File.ReadAllText(path) : "";
            error = "";
            return true;
        }
        catch (Exception exception)
        {
            exists = false;
            text = "";
            error = $"Could not read {Path.GetFileName(path)}; the current Drafts were preserved. {exception.Message}";
            return false;
        }
    }

    /// <summary>Whether parsing skipped source content that cannot be reconstructed from editable entries.</summary>
    /// <param name="diagnostics">The complete codec report.</param>
    internal static bool HasUnrecoverableRecord(IReadOnlyList<string> diagnostics)
    {
        for (var i = 0; i < diagnostics.Count; i++)
        {
            if (DiagnosticBlocksSave(diagnostics[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Hashes exact file content together with existence for external-change detection.</summary>
    /// <param name="text">The complete file content.</param>
    /// <param name="exists">Whether the file existed.</param>
    private static string HashFileContent(string text, bool exists)
    {
        return Hash128.Compute((exists ? "present:" : "missing:") + (text ?? "")).ToString();
    }
}
