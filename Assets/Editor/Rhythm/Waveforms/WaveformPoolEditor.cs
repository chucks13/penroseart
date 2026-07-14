using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The authoring host for the Waveform Pool (<c>penrose_waveforms.txt</c>): the Editor window where you read the
/// StreamingAssets file, select / edit / reorder / add / remove Presets, and write the whole file back canonically.
/// </summary>
/// <remarks>
/// <para>
/// Both this window and the runtime go through <see cref="WaveformPool"/> for parse and serialize. The window is a
/// real Unity document: drafts survive domain reload, native close prompts protect unsaved work, and the window is
/// the Undo target. <see cref="WaveformPoolDocument"/> owns the persisted baseline outside those Undo snapshots,
/// so restored drafts cannot masquerade as saved content and stale drafts cannot overwrite external hand edits.
/// </para>
/// <para>
/// List order is authoring presentation only. Runtime performers acquire Waveforms by Energy or uniformly from the
/// whole Pool; names are display labels and need not be unique.
/// </para>
/// </remarks>
public sealed class WaveformPoolEditor : EditorWindow
{
    /// <summary>Opens or focuses the canonical Waveform Pool document window.</summary>
    [MenuItem("Window/Penrose/Waveform Pool Editor")]
    public static void Open()
    {
        var window = GetWindow<WaveformPoolEditor>();
        window.titleContent = new GUIContent("Waveform Pool");
        window.minSize = new Vector2(620f, 420f);
        window.Show();
    }

    /// <summary>One serialized editable Preset plus its rebuilt, non-serialized preview and diagnostics.</summary>
    [Serializable]
    private sealed class Draft
    {
        /// <summary>The Preset display name written inside <c>DEFINE_WAVEFORM(...)</c>.</summary>
        [SerializeField] public string name;

        /// <summary>The raw note-width tokens.</summary>
        [SerializeField] public string sequence;

        /// <summary>The raw amplitude digits.</summary>
        [SerializeField] public string amplitude;

        /// <summary>The persisted peak-shaping value.</summary>
        [SerializeField] public float rounding;

        /// <summary>The persisted beat offset.</summary>
        [SerializeField] public float offset;

        /// <summary>The immutable parsed value drawn by the plot.</summary>
        [NonSerialized] public Waveform preview;

        /// <summary>Current notation defects, rebuilt without Console logging.</summary>
        [NonSerialized] public string[] diagnostics = Array.Empty<string>();

        /// <summary>Rebuilds the preview and its structured diagnostics from the serialized Draft fields.</summary>
        public void RebuildPreview()
        {
            preview = Waveform.Parse(sequence, amplitude, rounding, offset, out diagnostics);
        }
    }

    /// <summary>The result of one attempted document save.</summary>
    private enum SaveResult
    {
        /// <summary>The document was written successfully.</summary>
        Saved,

        /// <summary>The operator chose not to overwrite or reload an external edit.</summary>
        Cancelled,

        /// <summary>Validation or file I/O prevented the write.</summary>
        Failed,
    }

    /// <summary>The serialized editable Presets, retained across domain reload.</summary>
    [SerializeField] private List<Draft> drafts = new();

    /// <summary>The selected Preset index.</summary>
    [SerializeField] private int selected = -1;

    /// <summary>The retained list scroll position.</summary>
    [SerializeField] private Vector2 listScroll;

    /// <summary>Whether this window already owns a loaded document rather than needing an initial disk read.</summary>
    [SerializeField] private bool documentLoaded;

    /// <summary>The non-Undoable disk and persisted-baseline lifecycle for the external Pool file.</summary>
    [NonSerialized] private WaveformPoolDocument document;

    /// <summary>A file read failure that prevents saving until a later successful reload.</summary>
    [SerializeField] private string loadError = "";

    /// <summary>Structured codec findings from the last disk read.</summary>
    [SerializeField] private List<string> loadDiagnostics = new();

    /// <summary>Whether the loaded source required a recoverable value substitution or precision normalization.</summary>
    [SerializeField] private bool recoveredValuesNeedSave;

    /// <summary>Whether the last adopted disk source was absent rather than an existing empty file.</summary>
    [SerializeField] private bool loadedSourceWasMissing;

    /// <summary>The current required-Pool failure shown without blocking edits that repair it.</summary>
    [SerializeField] private string configurationError = "";

    private static readonly Color MalformedCurveColor = new(1f, 0.55f, 0.3f);

    private const string SequenceHint =
        "Note-value tokens, one per Hump: W=whole H=half Q=quarter E=eighth S=sixteenth (widths sum to one bar).";
    private const string AmplitudeHint =
        "One digit 0-8 per Hump (digit/8). 0 is the gate — a silent Hump is a skipped beat.";

    /// <summary>Restores serialized drafts after reload or reads the file when the window has no document yet.</summary>
    private void OnEnable()
    {
        titleContent = new GUIContent("Waveform Pool");
        minSize = new Vector2(620f, 420f);
        saveChangesMessage = "The Waveform Pool has unsaved changes. Save them before closing?";
        Undo.undoRedoPerformed -= OnUndoRedo;
        Undo.undoRedoPerformed += OnUndoRedo;
        document = new WaveformPoolDocument(WaveformPool.FilePath);

        if (documentLoaded && document.HasBaseline)
        {
            RebuildPreviews();
            RefreshUnsavedState();
            return;
        }

        ReloadFromDisk();
    }

    /// <summary>Detaches the window from the global Undo callback.</summary>
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    /// <summary>Rebuilds non-serialized previews after Unity restores a prior Draft state.</summary>
    private void OnUndoRedo()
    {
        RebuildPreviews();
        RefreshUnsavedState();
        Repaint();
    }

    /// <summary>Writes the document for Unity's native close/save workflow.</summary>
    public override void SaveChanges()
    {
        var result = SaveDocument(out var error);
        if (result != SaveResult.Saved)
        {
            throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "Waveform Pool save was cancelled." : error);
        }

        base.SaveChanges();
    }

    /// <summary>Lets Unity clear native unsaved state when the operator explicitly discards and closes.</summary>
    public override void DiscardChanges()
    {
        base.DiscardChanges();
    }

    /// <summary>Draws the toolbar, document warnings, Preset list, and selected Draft editor.</summary>
    private void OnGUI()
    {
        DrawToolbar();

        if (!string.IsNullOrEmpty(loadError))
        {
            EditorGUILayout.HelpBox(loadError, MessageType.Error);
        }

        if (!string.IsNullOrEmpty(configurationError))
        {
            EditorGUILayout.HelpBox(configurationError, MessageType.Error);
        }

        if (loadDiagnostics.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "The file loaded with diagnostics. Recovered fields can be repaired here; a broken record that " +
                "could not be recovered must be fixed in the text file before a full rewrite:\n• " +
                string.Join("\n• ", loadDiagnostics),
                MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawList();
            DrawEditor();
        }
    }

    /// <summary>Draws explicit reload/save commands around the native unsaved document state.</summary>
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(64f)) &&
                (!hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "Discard edits?",
                    "Reloading drops unsaved Pool edits and re-reads the file from disk.",
                    "Reload",
                    "Cancel")))
            {
                ReloadFromDisk();
            }

            var validationErrors = BuildValidationErrors();
            using (new EditorGUI.DisabledScope(!hasUnsavedChanges || validationErrors.Count > 0))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    var result = SaveDocument(out var error);
                    if (result == SaveResult.Saved)
                    {
                        base.SaveChanges();
                    }
                    else if (result == SaveResult.Failed)
                    {
                        EditorUtility.DisplayDialog("Waveform Pool was not saved", error, "OK");
                    }
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(WaveformPool.FileName, EditorStyles.miniLabel);
        }
    }

    /// <summary>Draws the ordered authoring list and its selection controls.</summary>
    private void DrawList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(210f)))
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Order controls presentation and save order. Runtime acquisition uses Energy, not row indexes or names.",
                MessageType.None);

            using (var scroll = new EditorGUILayout.ScrollViewScope(listScroll, GUI.skin.box))
            {
                listScroll = scroll.scrollPosition;
                for (var i = 0; i < drafts.Count; i++)
                {
                    var draft = drafts[i];
                    var previousColor = GUI.backgroundColor;
                    if (i == selected)
                    {
                        GUI.backgroundColor = new Color(0.3f, 0.55f, 0.7f);
                    }

                    var label = $"[{i}] {draft.name}";
                    if (!WaveformPool.IsValidName(draft.name) || draft.diagnostics.Length > 0)
                    {
                        label += "  ⚠";
                    }

                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        selected = i;
                        GUI.FocusControl(null);
                    }

                    GUI.backgroundColor = previousColor;
                }
            }

            DrawListButtons();
        }
    }

    /// <summary>Draws the add, duplicate, delete, and ordering commands with Undo support.</summary>
    private void DrawListButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add"))
            {
                RecordDocumentChange("Add Waveform");
                var draft = new Draft
                {
                    name = "new waveform",
                    sequence = "QQQQ",
                    amplitude = "8888",
                    rounding = Waveform.BeatPulseRounding,
                    offset = 0f,
                };
                draft.RebuildPreview();
                drafts.Add(draft);
                selected = drafts.Count - 1;
                RefreshUnsavedState();
            }

            using (new EditorGUI.DisabledScope(selected < 0))
            {
                if (GUILayout.Button("Duplicate"))
                {
                    RecordDocumentChange("Duplicate Waveform");
                    var source = drafts[selected];
                    var copy = new Draft
                    {
                        name = source.name + " copy",
                        sequence = source.sequence,
                        amplitude = source.amplitude,
                        rounding = source.rounding,
                        offset = source.offset,
                    };
                    copy.RebuildPreview();
                    drafts.Insert(selected + 1, copy);
                    selected++;
                    RefreshUnsavedState();
                }

                if (GUILayout.Button("Delete"))
                {
                    RecordDocumentChange("Delete Waveform");
                    drafts.RemoveAt(selected);
                    selected = drafts.Count == 0 ? -1 : Mathf.Clamp(selected, 0, drafts.Count - 1);
                    RefreshUnsavedState();
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        using (new EditorGUI.DisabledScope(selected < 0))
        {
            if (GUILayout.Button("Move Up") && selected > 0)
            {
                RecordDocumentChange("Move Waveform Up");
                (drafts[selected - 1], drafts[selected]) = (drafts[selected], drafts[selected - 1]);
                selected--;
                RefreshUnsavedState();
            }

            if (GUILayout.Button("Move Down") && selected >= 0 && selected < drafts.Count - 1)
            {
                RecordDocumentChange("Move Waveform Down");
                (drafts[selected + 1], drafts[selected]) = (drafts[selected], drafts[selected + 1]);
                selected++;
                RefreshUnsavedState();
            }
        }
    }

    /// <summary>Draws the selected Draft fields, diagnostics, canonical text, and immutable preview.</summary>
    private void DrawEditor()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (selected < 0 || selected >= drafts.Count)
            {
                EditorGUILayout.HelpBox("No Preset selected. Add one, or open a Pool file that has Presets.", MessageType.Info);
                return;
            }

            var draft = drafts[selected];
            EditorGUI.BeginChangeCheck();
            var newName = EditorGUILayout.TextField("Name", draft.name);
            var newSequence = EditorGUILayout.TextField("Sequence", draft.sequence);
            EditorGUILayout.LabelField(" ", SequenceHint, EditorStyles.wordWrappedMiniLabel);
            var newAmplitude = EditorGUILayout.TextField("Amplitude", draft.amplitude);
            EditorGUILayout.LabelField(" ", AmplitudeHint, EditorStyles.wordWrappedMiniLabel);
            if (EditorGUI.EndChangeCheck())
            {
                RecordDocumentChange("Edit Waveform Notation");
                draft.name = newName;
                draft.sequence = newSequence.ToUpperInvariant();
                draft.amplitude = newAmplitude;
                draft.RebuildPreview();
                RefreshUnsavedState();
            }

            EditorGUI.BeginChangeCheck();
            var newRounding = EditorGUILayout.Slider(
                new GUIContent("Rounding", "0 sharp triangle → ~0.5 cosine dome → 1 flat top. Trough always 0."),
                draft.rounding,
                0f,
                1f);
            var newOffset = EditorGUILayout.FloatField(
                new GUIContent("Offset (beats)", "Phase shift in beats; 0.5 lands the peak on the \"&\" (offbeat)."),
                draft.offset);
            if (EditorGUI.EndChangeCheck())
            {
                RecordDocumentChange("Edit Waveform Shape");
                draft.rounding = CanonicalizeScalar(newRounding);
                draft.offset = CanonicalizeScalar(newOffset);
                draft.RebuildPreview();
                RefreshUnsavedState();
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Envelope (one bar)", EditorStyles.boldLabel);
            var plotRect = GUILayoutUtility.GetRect(100f, 120f, GUILayout.ExpandWidth(true));
            DrawPlot(plotRect, draft.preview);

            if (!WaveformPool.IsValidName(draft.name))
            {
                EditorGUILayout.HelpBox(
                    "Name must contain visible text and cannot include ( ) { } | or a line break.",
                    MessageType.Error);
            }

            for (var i = 0; i < draft.diagnostics.Length; i++)
            {
                EditorGUILayout.HelpBox(draft.diagnostics[i], MessageType.Error);
            }

            EditorGUILayout.LabelField("Saves as", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(
                $"DEFINE_WAVEFORM({draft.name}){{ {draft.sequence} | {draft.amplitude} | " +
                $"{FormatScalar(draft.rounding)} | {FormatScalar(draft.offset)} }}",
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }

    /// <summary>Records the window before one user-visible document mutation.</summary>
    /// <param name="label">The command text shown in Unity's Undo menu.</param>
    private void RecordDocumentChange(string label)
    {
        Undo.RecordObject(this, label);
    }

    /// <summary>Transactionally reloads the file, replacing Drafts only after the complete source is representable.</summary>
    private bool ReloadFromDisk()
    {
        document ??= new WaveformPoolDocument(WaveformPool.FilePath);
        var loaded = document.Load();
        if (loaded.Status != WaveformPoolDocumentLoadStatus.Loaded)
        {
            loadError = loaded.Error;
            loadDiagnostics = new List<string>(loaded.Diagnostics);
            Repaint();
            return false;
        }

        var entries = loaded.Entries;
        var diagnostics = loaded.Diagnostics;
        var replacement = new List<Draft>(entries.Count);
        var requiresCanonicalRewrite = HasRecoverableNumericSubstitution(diagnostics);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var canonicalSequence = entry.waveform.sequence.ToUpperInvariant();
            var canonicalRounding = CanonicalizeScalar(entry.waveform.rounding);
            var canonicalOffset = CanonicalizeScalar(entry.waveform.offset);
            requiresCanonicalRewrite |= canonicalSequence != entry.waveform.sequence ||
                                        canonicalRounding != entry.waveform.rounding ||
                                        canonicalOffset != entry.waveform.offset;
            var draft = new Draft
            {
                name = entry.name,
                sequence = canonicalSequence,
                amplitude = entry.waveform.amplitude,
                rounding = canonicalRounding,
                offset = canonicalOffset,
            };
            draft.RebuildPreview();
            replacement.Add(draft);
        }

        Undo.ClearUndo(this);
        drafts = replacement;
        selected = drafts.Count == 0 ? -1 : Mathf.Clamp(selected, 0, drafts.Count - 1);
        if (selected < 0 && drafts.Count > 0)
        {
            selected = 0;
        }

        loadError = "";
        loadDiagnostics = new List<string>(diagnostics);
        recoveredValuesNeedSave = requiresCanonicalRewrite;
        loadedSourceWasMissing = !loaded.FileExists;
        documentLoaded = true;
        document.AcceptLoad(loaded, DraftFingerprint());
        RefreshUnsavedState();
        Repaint();
        return true;
    }

    /// <summary>Validates, conflict-checks, and rewrites the current Draft collection.</summary>
    /// <param name="error">A user-facing failure reason when the result is <see cref="SaveResult.Failed"/>.</param>
    private SaveResult SaveDocument(out string error)
    {
        var validationErrors = BuildValidationErrors();
        if (validationErrors.Count > 0)
        {
            error = "Fix the document diagnostics before saving:\n• " + string.Join("\n• ", validationErrors);
            return SaveResult.Failed;
        }

        error = "";
        var entries = new List<WaveformPool.Entry>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            entries.Add(new WaveformPool.Entry(drafts[i].name, drafts[i].preview));
        }

        document ??= new WaveformPoolDocument(WaveformPool.FilePath);
        var fingerprint = DraftFingerprint();
        var result = document.Save(entries, fingerprint, overwriteExternalChange: false);
        if (result.Status == WaveformPoolDocumentSaveStatus.ExternalChange)
        {
            var choice = EditorUtility.DisplayDialogComplex(
                "Waveform Pool changed on disk",
                "The file changed after this window loaded it. Overwrite the external edit, reload it, or cancel.",
                "Overwrite",
                "Cancel",
                "Reload");
            if (choice == 2)
            {
                ReloadFromDisk();
                return SaveResult.Cancelled;
            }

            if (choice != 0)
            {
                return SaveResult.Cancelled;
            }

            result = document.Save(entries, fingerprint, overwriteExternalChange: true);
        }

        if (result.Status != WaveformPoolDocumentSaveStatus.Saved)
        {
            error = result.Error;
            return SaveResult.Failed;
        }

        AssetDatabase.Refresh();
        loadDiagnostics.Clear();
        loadError = "";
        recoveredValuesNeedSave = false;
        loadedSourceWasMissing = false;
        documentLoaded = true;
        RefreshUnsavedState();
        Debug.Log($"[Waveform] Saved {entries.Count} Preset(s) to {WaveformPool.FileName}. " +
                  "The runtime picks them up on its next Pool load.");
        return SaveResult.Saved;
    }

    /// <summary>Returns every condition that makes the current full-file rewrite unsafe.</summary>
    private List<string> BuildValidationErrors()
    {
        var errors = new List<string>();
        if (!string.IsNullOrEmpty(loadError))
        {
            errors.Add(loadError);
        }

        for (var i = 0; i < loadDiagnostics.Count; i++)
        {
            if (WaveformPoolDocument.DiagnosticBlocksSave(loadDiagnostics[i]))
            {
                errors.Add(loadDiagnostics[i]);
            }
        }

        if (drafts.Count == 0)
        {
            errors.Add("The Pool must contain at least one Preset.");
        }

        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            if (!WaveformPool.IsValidName(draft.name))
            {
                errors.Add($"Preset {i + 1} has an empty or delimiter-containing name.");
            }

            for (var diagnosticIndex = 0; diagnosticIndex < draft.diagnostics.Length; diagnosticIndex++)
            {
                errors.Add($"Preset \"{draft.name}\": {draft.diagnostics[diagnosticIndex]}");
            }

            if (float.IsNaN(draft.rounding) || float.IsInfinity(draft.rounding) ||
                float.IsNaN(draft.offset) || float.IsInfinity(draft.offset))
            {
                errors.Add($"Preset \"{draft.name}\" has a non-finite rounding or offset.");
            }
        }

        return errors;
    }

    /// <summary>Whether parsing replaced at least one invalid number with its documented fallback.</summary>
    private static bool HasRecoverableNumericSubstitution(IReadOnlyList<string> diagnostics)
    {
        for (var i = 0; i < diagnostics.Count; i++)
        {
            if (diagnostics[i].Contains("— defaulting to"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Rebuilds every non-serialized preview after reload or Undo.</summary>
    private void RebuildPreviews()
    {
        for (var i = 0; i < drafts.Count; i++)
        {
            drafts[i].RebuildPreview();
        }
    }

    /// <summary>Derives native document dirty state from the serialized Draft content.</summary>
    private void RefreshUnsavedState()
    {
        hasUnsavedChanges = documentLoaded && document != null &&
                            document.IsDirty(DraftFingerprint(), recoveredValuesNeedSave);
        RefreshConfigurationError();
    }

    /// <summary>Derives the visible required-Pool failure from the current repairable Draft collection.</summary>
    private void RefreshConfigurationError()
    {
        if (drafts.Count == 0)
        {
            configurationError = loadedSourceWasMissing
                ? $"Required Waveform Pool '{WaveformPool.FileName}' is missing. Add a Preset and save to repair it."
                : $"Required Waveform Pool '{WaveformPool.FileName}' contains no Waveforms. Add a Preset before runtime startup.";
            return;
        }

        for (var i = 0; i < drafts.Count; i++)
        {
            if (!WaveformPool.IsValidName(drafts[i].name) || drafts[i].diagnostics.Length > 0)
            {
                configurationError = "The required Waveform Pool contains invalid Presets. " +
                                     "Runtime startup will fail until the highlighted diagnostics are fixed.";
                return;
            }
        }

        configurationError = "";
    }

    /// <summary>Builds a collision-resistant length-prefixed representation of the editable Draft fields.</summary>
    private string DraftFingerprint()
    {
        var builder = new StringBuilder();
        for (var i = 0; i < drafts.Count; i++)
        {
            AppendFingerprintField(builder, drafts[i].name);
            AppendFingerprintField(builder, drafts[i].sequence);
            AppendFingerprintField(builder, drafts[i].amplitude);
            AppendFingerprintField(builder, drafts[i].rounding.ToString("R", CultureInfo.InvariantCulture));
            AppendFingerprintField(builder, drafts[i].offset.ToString("R", CultureInfo.InvariantCulture));
        }

        return Hash128.Compute(builder.ToString()).ToString();
    }

    /// <summary>Appends one unambiguous field to a Draft fingerprint.</summary>
    private static void AppendFingerprintField(StringBuilder builder, string value)
    {
        value ??= "";
        builder.Append(value.Length).Append(':').Append(value).Append(';');
    }

    /// <summary>Rounds one authoring scalar to the exact precision emitted by the Pool codec.</summary>
    private static float CanonicalizeScalar(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return value;
        }

        return float.Parse(FormatScalar(value), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>Formats one Pool scalar exactly as <see cref="WaveformPool.Serialize"/> writes it.</summary>
    private static string FormatScalar(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>Draws one bar of the immutable preview, orange when notation is malformed.</summary>
    private static void DrawPlot(Rect rect, Waveform waveform)
    {
        WaveformPlot.Draw(
            rect,
            waveform,
            waveform.IsMalformed ? MalformedCurveColor : WaveformPlot.Curve);
    }
}
