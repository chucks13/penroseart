// Editor behavior coverage for Waveform Pool persistence, history, failure, and preview semantics.

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>A serialized test Draft whose field changes travel through Unity's real Undo stack.</summary>
internal sealed class WaveformPoolUndoDraftHost : ScriptableObject
{
    /// <summary>The editable-content identity restored by Undo and Redo.</summary>
    [SerializeField] public string fingerprint = "";
}

/// <summary>Behavior tests for the Waveform Pool editor's file-backed document lifecycle.</summary>
public sealed class WaveformPoolDocumentTests
{
    /// <summary>The isolated directory removed after each test.</summary>
    private string directory = "";

    /// <summary>The isolated Waveform Pool document path exercised by each test.</summary>
    private string path = "";

    /// <summary>The Unity object used when a test exercises the real Undo stack.</summary>
    private WaveformPoolUndoDraftHost undoHost;

    /// <summary>Creates an isolated Pool document path for each test.</summary>
    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"penrose-waveform-document-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, WaveformPool.FileName);
    }

    /// <summary>Removes the isolated Pool document and its retained editor-session baseline.</summary>
    [TearDown]
    public void TearDown()
    {
        if (undoHost != null)
        {
            Undo.ClearUndo(undoHost);
            UnityEngine.Object.DestroyImmediate(undoHost);
        }

        WaveformPoolDocument.ForgetBaseline(path);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies Save changes only the persisted baseline, so older and restored drafts remain distinguishable.</summary>
    [Test]
    public void Save_UndoFingerprintIsDirtyAndRedoFingerprintIsClean()
    {
        File.WriteAllText(path, "DEFINE_WAVEFORM(original){ QQQQ | 8888 | 0.3 | 0 }");
        var document = new WaveformPoolDocument(path);
        var loaded = document.Load();
        Assert.That(loaded.Status, Is.EqualTo(WaveformPoolDocumentLoadStatus.Loaded));
        document.AcceptLoad(loaded, "original-draft");
        undoHost = ScriptableObject.CreateInstance<WaveformPoolUndoDraftHost>();
        undoHost.fingerprint = "original-draft";
        Undo.RegisterCompleteObjectUndo(undoHost, "Edit Waveform");
        undoHost.fingerprint = "saved-draft";

        var entries = new[]
        {
            new WaveformPool.Entry("saved", Waveform.Parse("QQQQ", "8080", 0.4f, 0f, out _)),
        };

        var saved = document.Save(entries, undoHost.fingerprint, overwriteExternalChange: false);

        Assert.That(saved.Status, Is.EqualTo(WaveformPoolDocumentSaveStatus.Saved));
        Undo.PerformUndo();
        Assert.That(undoHost.fingerprint, Is.EqualTo("original-draft"));
        Assert.That(document.IsDirty(undoHost.fingerprint, recoveredValuesNeedSave: false), Is.True,
            "Undoing to the older draft must not restore its obsolete clean baseline.");

        Undo.PerformRedo();
        Assert.That(undoHost.fingerprint, Is.EqualTo("saved-draft"));
        Assert.That(document.IsDirty(undoHost.fingerprint, recoveredValuesNeedSave: false), Is.False,
            "Redoing to the saved draft must compare clean against the persisted baseline.");

        var reconstructed = new WaveformPoolDocument(path);
        Assert.That(reconstructed.IsDirty("original-draft", recoveredValuesNeedSave: false), Is.True,
            "Window reconstruction must not move the persisted baseline back into Undoable draft state.");
        Assert.That(reconstructed.IsDirty("saved-draft", recoveredValuesNeedSave: false), Is.False);
    }

    /// <summary>Verifies accepting a later successful load resets both disk and draft identities together.</summary>
    [Test]
    public void Reload_AcceptedCandidateBecomesTheCleanBaseline()
    {
        File.WriteAllText(path, "DEFINE_WAVEFORM(first){ QQQQ | 8888 | 0.3 | 0 }");
        var document = new WaveformPoolDocument(path);
        document.AcceptLoad(document.Load(), "first-draft");

        File.WriteAllText(path, "DEFINE_WAVEFORM(second){ HHHH | 4444 | 0.5 | 1 }");
        var reloaded = document.Load();

        Assert.That(reloaded.Status, Is.EqualTo(WaveformPoolDocumentLoadStatus.Loaded));
        Assert.That(reloaded.Entries[0].name, Is.EqualTo("second"));
        Assert.That(document.IsDirty("second-draft", recoveredValuesNeedSave: false), Is.True,
            "Reading is transactional; the candidate is not the baseline until the window adopts it.");

        document.AcceptLoad(reloaded, "second-draft");

        Assert.That(document.IsDirty("second-draft", recoveredValuesNeedSave: false), Is.False);
        Assert.That(document.IsDirty("first-draft", recoveredValuesNeedSave: false), Is.True);
    }

    /// <summary>Verifies an unrecoverable parse leaves the last accepted draft identity untouched.</summary>
    [Test]
    public void Load_UnrecoverableParseFailsWithoutChangingBaseline()
    {
        File.WriteAllText(path, "DEFINE_WAVEFORM(valid){ QQQQ | 8888 | 0.3 | 0 }");
        var document = new WaveformPoolDocument(path);
        document.AcceptLoad(document.Load(), "preserved-draft");
        File.WriteAllText(path, "DEFINE_WAVEFORM(broken){ QQQQ | 8888");

        var failed = document.Load();

        Assert.That(failed.Status, Is.EqualTo(WaveformPoolDocumentLoadStatus.Failed));
        Assert.That(failed.Error, Does.Contain("preserved"));
        Assert.That(document.IsDirty("preserved-draft", recoveredValuesNeedSave: false), Is.False);
    }

    /// <summary>Verifies a read failure is reported without changing the last accepted draft identity.</summary>
    [Test]
    public void Load_ReadFailureFailsWithoutChangingBaseline()
    {
        File.WriteAllText(path, "DEFINE_WAVEFORM(valid){ QQQQ | 8888 | 0.3 | 0 }");
        var document = new WaveformPoolDocument(path);
        document.AcceptLoad(document.Load(), "preserved-draft");
        File.Delete(path);
        Directory.CreateDirectory(path);

        var failed = document.Load();

        Assert.That(failed.Status, Is.EqualTo(WaveformPoolDocumentLoadStatus.Failed));
        Assert.That(failed.Error, Does.Contain("preserved"));
        Assert.That(document.IsDirty("preserved-draft", recoveredValuesNeedSave: false), Is.False);
    }

    /// <summary>Verifies an external edit blocks Save until the operator explicitly chooses overwrite.</summary>
    [Test]
    public void Save_ExternalChangeConflictsUntilOverwriteIsApproved()
    {
        const string externalText = "DEFINE_WAVEFORM(external){ HHHH | 4444 | 0.5 | 1 }";
        File.WriteAllText(path, "DEFINE_WAVEFORM(original){ QQQQ | 8888 | 0.3 | 0 }");
        var document = new WaveformPoolDocument(path);
        document.AcceptLoad(document.Load(), "original-draft");
        File.WriteAllText(path, externalText);
        var entries = new[]
        {
            new WaveformPool.Entry("mine", Waveform.Parse("QQQQ", "8080", 0.4f, 0f, out _)),
        };

        var conflicted = document.Save(entries, "mine-draft", overwriteExternalChange: false);

        Assert.That(conflicted.Status, Is.EqualTo(WaveformPoolDocumentSaveStatus.ExternalChange));
        Assert.That(File.ReadAllText(path), Is.EqualTo(externalText));
        Assert.That(document.IsDirty("original-draft", recoveredValuesNeedSave: false), Is.False,
            "A refused save must not move the persisted baseline.");

        var overwritten = document.Save(entries, "mine-draft", overwriteExternalChange: true);

        Assert.That(overwritten.Status, Is.EqualTo(WaveformPoolDocumentSaveStatus.Saved));
        Assert.That(File.ReadAllText(path), Does.Contain("DEFINE_WAVEFORM(mine)"));
        Assert.That(document.IsDirty("mine-draft", recoveredValuesNeedSave: false), Is.False);
    }

    /// <summary>Verifies a failed write cannot make an unsaved draft appear persisted.</summary>
    [Test]
    public void Save_WriteFailureLeavesDraftDirty()
    {
        File.WriteAllText(path, "DEFINE_WAVEFORM(original){ QQQQ | 8888 | 0.3 | 0 }");
        var document = new WaveformPoolDocument(path);
        document.AcceptLoad(document.Load(), "original-draft");
        File.Delete(path);
        Directory.CreateDirectory(path);
        var entries = new[]
        {
            new WaveformPool.Entry("mine", Waveform.Parse("QQQQ", "8080", 0.4f, 0f, out _)),
        };

        var failed = document.Save(entries, "mine-draft", overwriteExternalChange: true);

        Assert.That(failed.Status, Is.EqualTo(WaveformPoolDocumentSaveStatus.Failed));
        Assert.That(document.IsDirty("mine-draft", recoveredValuesNeedSave: false), Is.True);
        Assert.That(document.IsDirty("original-draft", recoveredValuesNeedSave: false), Is.False);
    }

    /// <summary>Verifies missing and empty required Pools stay empty instead of gaining an implicit Preset.</summary>
    [Test]
    public void Load_MissingAndEmptyPoolsRemainEmpty()
    {
        var document = new WaveformPoolDocument(path);

        var missing = document.Load();
        File.WriteAllText(path, "");
        var empty = document.Load();

        Assert.That(missing.Status, Is.EqualTo(WaveformPoolDocumentLoadStatus.Loaded));
        Assert.That(missing.FileExists, Is.False);
        Assert.That(missing.Entries, Is.Empty);
        Assert.That(empty.Status, Is.EqualTo(WaveformPoolDocumentLoadStatus.Loaded));
        Assert.That(empty.FileExists, Is.True);
        Assert.That(empty.Entries, Is.Empty);
    }

    /// <summary>Verifies notation-invalid entries remain explicit diagnostic data for repair and preview failure.</summary>
    [Test]
    public void Load_NotationInvalidPoolReportsDiagnosticsWithoutSynthesizingReplacement()
    {
        File.WriteAllText(path, "DEFINE_WAVEFORM(broken){ ZZZZ | 9999 | 0.3 | 0 }");
        var document = new WaveformPoolDocument(path);

        var loaded = document.Load();

        Assert.That(loaded.Status, Is.EqualTo(WaveformPoolDocumentLoadStatus.Loaded));
        Assert.That(loaded.Entries, Has.Count.EqualTo(1));
        Assert.That(loaded.Entries[0].name, Is.EqualTo("broken"));
        Assert.That(loaded.Diagnostics, Is.Not.Empty);
        Assert.That(loaded.Entries[0].waveform.IsMalformed, Is.True);
    }

    /// <summary>Verifies an empty required Pool cannot be saved as a successful document.</summary>
    [Test]
    public void Save_EmptyPoolFailsAndLeavesDraftDirty()
    {
        File.WriteAllText(path, "DEFINE_WAVEFORM(original){ QQQQ | 8888 | 0.3 | 0 }");
        var document = new WaveformPoolDocument(path);
        document.AcceptLoad(document.Load(), "original-draft");

        var failed = document.Save(Array.Empty<WaveformPool.Entry>(), "empty-draft", overwriteExternalChange: false);

        Assert.That(failed.Status, Is.EqualTo(WaveformPoolDocumentSaveStatus.Failed));
        Assert.That(failed.Error, Does.Contain("at least one Preset"));
        Assert.That(document.IsDirty("empty-draft", recoveredValuesNeedSave: false), Is.True);
    }

    /// <summary>Duplicate persisted names fail Save visibly without replacing the last valid Pool document.</summary>
    [Test]
    public void Save_DuplicateNamesFailsAndLeavesFileUntouched()
    {
        const string original = "DEFINE_WAVEFORM(original){ QQQQ | 8888 | 0.3 | 0 }";
        File.WriteAllText(path, original);
        var document = new WaveformPoolDocument(path);
        document.AcceptLoad(document.Load(), "original-draft");
        var entries = new[]
        {
            new WaveformPool.Entry("same name", Waveform.Parse("QQQQ", "8888", 0.3f, 0f, out _)),
            new WaveformPool.Entry("same name", Waveform.Parse("QQQQ", "8000", 0.3f, 0f, out _)),
        };

        var failed = document.Save(entries, "duplicate-draft", overwriteExternalChange: false);

        Assert.That(failed.Status, Is.EqualTo(WaveformPoolDocumentSaveStatus.Failed));
        Assert.That(failed.Error, Does.Contain("duplicate").And.Contain("same name"));
        Assert.That(File.ReadAllText(path), Is.EqualTo(original));
        Assert.That(document.IsDirty("duplicate-draft", recoveredValuesNeedSave: false), Is.True);
    }
}

/// <summary>Behavior tests for the Beat Manager's honest Waveform Pool preview state.</summary>
public sealed class WaveformPoolPreviewTests
{
    /// <summary>Verifies absence and empty content remain visible required-configuration failures.</summary>
    [Test]
    public void FromText_MissingAndEmptyPoolsAreUnavailable()
    {
        var missing = WaveformPoolPreview.FromText("", fileExists: false);
        var empty = WaveformPoolPreview.FromText("", fileExists: true);

        Assert.That(missing.IsUsable, Is.False);
        Assert.That(missing.Error, Does.Contain("missing"));
        Assert.That(missing.Entries, Is.Empty);
        Assert.That(empty.IsUsable, Is.False);
        Assert.That(empty.Error, Does.Contain("no Waveforms"));
        Assert.That(empty.Entries, Is.Empty);
    }

    /// <summary>Verifies notation-invalid content is reported rather than replaced by a Beat Pulse.</summary>
    [Test]
    public void FromText_NotationInvalidPoolIsUnavailable()
    {
        var preview = WaveformPoolPreview.FromText(
            "DEFINE_WAVEFORM(broken){ ZZZZ | 9999 | 0.3 | 0 }",
            fileExists: true);

        Assert.That(preview.IsUsable, Is.False);
        Assert.That(preview.Error, Does.Contain("broken").And.Contain("malformed"));
        Assert.That(preview.Entries, Is.Empty, "No synthetic or widened preview selection may hide the defect.");
    }

    /// <summary>Duplicate persisted names make the runtime-faithful editor preview visibly unavailable.</summary>
    [Test]
    public void FromText_DuplicateNamesAreUnavailable()
    {
        var preview = WaveformPoolPreview.FromText(
            "DEFINE_WAVEFORM(same name){ QQQQ | 8888 | 0.3 | 0 }\n" +
            "DEFINE_WAVEFORM(same name){ QQQQ | 8000 | 0.3 | 0 }",
            fileExists: true);

        Assert.That(preview.IsUsable, Is.False);
        Assert.That(preview.Error, Does.Contain("duplicate").And.Contain("same name"));
        Assert.That(preview.Entries, Is.Empty);
    }

    /// <summary>An invalid persisted name makes the runtime-faithful editor preview visibly unavailable.</summary>
    [Test]
    public void FromText_InvalidNameIsUnavailable()
    {
        var preview = WaveformPoolPreview.FromText(
            "DEFINE_WAVEFORM(bad|name){ QQQQ | 8888 | 0.3 | 0 }",
            fileExists: true);

        Assert.That(preview.IsUsable, Is.False);
        Assert.That(preview.Error, Does.Contain("empty").And.Contain("delimiter"));
        Assert.That(preview.Entries, Is.Empty);
    }

    /// <summary>Verifies a damaged suffix previews exactly the valid prefix the current runtime parser retains.</summary>
    [Test]
    public void FromText_UnrecoverableSuffixMatchesRuntimeValidPrefix()
    {
        var preview = WaveformPoolPreview.FromText(
            "DEFINE_WAVEFORM(valid){ QQQQ | 8888 | 0.3 | 0 }\n" +
            "DEFINE_WAVEFORM(broken){ QQQQ | 8888",
            fileExists: true);

        Assert.That(preview.IsUsable, Is.True);
        Assert.That(preview.Error, Is.Empty);
        Assert.That(preview.Entries, Has.Length.EqualTo(1));
        Assert.That(preview.Entries[0].name, Is.EqualTo("valid"));
    }

    /// <summary>Verifies a valid preview retains the runtime Waveform value used for sampling and plotting.</summary>
    [Test]
    public void FromText_ValidPoolExposesRuntimeWaveformSampling()
    {
        var preview = WaveformPoolPreview.FromText(
            "DEFINE_WAVEFORM(syncopated){ EEEEEEEE | 87654321 | 0.4 | 0.5 }",
            fileExists: true);

        Assert.That(preview.IsUsable, Is.True);
        Assert.That(preview.Error, Is.Empty);
        Assert.That(preview.Entries, Has.Length.EqualTo(1));
        var expected = Waveform.Parse("EEEEEEEE", "87654321", 0.4f, 0.5f, out _);
        Assert.That(preview.Entries[0].waveform.Sample(0.37f), Is.EqualTo(expected.Sample(0.37f)).Within(0.000001f));
    }
}
