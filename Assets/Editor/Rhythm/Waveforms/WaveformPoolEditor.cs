using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The authoring host for the Waveform Pool (<c>penrose_waveforms.txt</c>): the Editor window where you read the
/// StreamingAssets file, select / edit / reorder / add / remove Presets, and write the whole file back
/// canonically. This is the "primary" editing path the Pool's hand-editable text format only exists as the
/// bootstrap/fallback for.
/// </summary>
/// <remarks>
/// <para>
/// Both this window and the runtime go through <see cref="WaveformPool"/> for parse and serialize, so the read
/// path and the write path can never disagree about the format — see that type's remarks. This window never
/// reaches into <see cref="BeatManager"/>; it edits the file, and the runtime re-reads the file on load.
/// </para>
/// <para>
/// Each row is held as a <see cref="Draft"/> of raw editable fields plus an immutable parsed preview. The raw fields
/// remain the source of truth while editing; whenever their values change, the cached <see cref="Waveform"/> is
/// replaced with a freshly parsed value representing the complete current Draft.
/// </para>
/// <para>
/// The list ORDER is load-bearing: the legacy <c>int beatVariant</c> currency indexes it and
/// <see cref="BeatManager.IsBeatTriggered"/> couples gate labels to indexes 1/2/3. The reorder controls carry
/// that warning so a drag never silently re-points an effect at a different rhythm.
/// </para>
/// </remarks>
public sealed class WaveformPoolEditor : EditorWindow
{
    [MenuItem("Window/Penrose/Waveform Pool Editor")]
    public static void Open()
    {
        var window = GetWindow<WaveformPoolEditor>();
        window.titleContent = new GUIContent("Waveform Pool");
        window.minSize = new Vector2(620f, 420f);
        window.Show();
    }

    /// <summary>
    /// One editable Preset row: the raw text the user types, plus a cached parsed <see cref="preview"/> the plot
    /// reads. The raw strings — not the parsed <see cref="Waveform"/> — are the source of truth while editing;
    /// see the type remarks for why.
    /// </summary>
    private sealed class Draft
    {
        public string name;
        public string sequence;
        public string amplitude;
        public float rounding;
        public float offset;

        /// <summary>The immutable parsed value for the current Draft fields, which the plot draws.</summary>
        public Waveform preview;

        /// <summary>Replaces <see cref="preview"/> after the Draft's sequence or amplitude changes.</summary>
        public void RebuildPreview()
        {
            preview = Waveform.Parse(sequence, amplitude, rounding, offset);
        }
    }

    private readonly List<Draft> drafts = new List<Draft>();
    private int selected = -1;
    private bool dirty;
    private Vector2 listScroll;

    // The plot look (track/grid/curve, gridlines, sample count) lives in WaveformPlot; only this view's
    // malformed-state curve color stays here.
    private static readonly Color MalformedCurveColor = new Color(1f, 0.55f, 0.3f);

    private const string SequenceHint = "Note-value tokens, one per Hump: W=whole H=half Q=quarter E=eighth S=sixteenth (widths sum to one bar).";
    private const string AmplitudeHint = "One digit 0-8 per Hump (digit/8). 0 is the gate — a silent Hump is a skipped beat.";

    private void OnEnable()
    {
        Reload();
    }

    /// <summary>(Re)reads the file into editable Drafts, discarding any unsaved edits. The codec already parsed each
    /// entry, so the parsed <see cref="Waveform"/> is reused as the preview — no second <see cref="Waveform.Parse"/>
    /// (and no duplicate malformation log) on load.</summary>
    private void Reload()
    {
        drafts.Clear();
        foreach (var entry in WaveformPool.Parse(WaveformPool.ReadFileOrEmpty()))
        {
            drafts.Add(new Draft
            {
                name = entry.name,
                sequence = entry.waveform.sequence.ToUpperInvariant(),
                amplitude = entry.waveform.amplitude,
                rounding = entry.waveform.rounding,
                offset = entry.waveform.offset,
                preview = entry.waveform,
            });
        }

        selected = drafts.Count > 0 ? 0 : -1;
        SetDirty(false);
    }

    /// <summary>Writes every Draft back through the codec's canonical serializer, then refreshes the AssetDatabase so
    /// the StreamingAssets file Unity tracks updates. Re-parses each Draft so a malformed Preset is logged at save —
    /// saving a broken shape is never silent.</summary>
    private void Save()
    {
        var entries = new List<WaveformPool.Entry>(drafts.Count);
        foreach (var d in drafts)
        {
            entries.Add(new WaveformPool.Entry(d.name, Waveform.Parse(d.sequence, d.amplitude, d.rounding, d.offset)));
        }

        File.WriteAllText(WaveformPool.FilePath, WaveformPool.Serialize(entries));
        AssetDatabase.Refresh();
        Debug.Log($"[Waveform] Saved {entries.Count} Preset(s) to {WaveformPool.FileName}. " +
                  "The runtime picks them up on its next Pool load.");
        SetDirty(false);
    }

    private void SetDirty(bool value)
    {
        dirty = value;
        titleContent = new GUIContent(value ? "Waveform Pool*" : "Waveform Pool");
    }

    private void OnGUI()
    {
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawList();
            DrawEditor();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(64f)))
            {
                if (!dirty || EditorUtility.DisplayDialog("Discard edits?",
                        "Reloading drops unsaved Pool edits and re-reads the file from disk.", "Reload", "Cancel"))
                {
                    Reload();
                }
            }

            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    Save();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(WaveformPool.FileName, EditorStyles.miniLabel);
        }
    }

    private void DrawList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(210f)))
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

            // The index shown next to each name is the load-bearing beatVariant index — surfaced so a reorder's
            // consequence is visible, not hidden behind a friendly name.
            EditorGUILayout.HelpBox("Order = beatVariant index. Effects select rhythms by this number, and beats " +
                                    "1/2/3 are wired to indexes 1/2/3 — reorder with intent.", MessageType.None);

            using (var scroll = new EditorGUILayout.ScrollViewScope(listScroll, GUI.skin.box))
            {
                listScroll = scroll.scrollPosition;
                for (var i = 0; i < drafts.Count; i++)
                {
                    var isSelected = i == selected;
                    var prev = GUI.backgroundColor;
                    if (isSelected)
                    {
                        GUI.backgroundColor = new Color(0.3f, 0.55f, 0.7f);
                    }

                    var label = $"[{i}] {drafts[i].name}";
                    if (drafts[i].preview.IsMalformed)
                    {
                        label += "  ⚠";
                    }

                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        selected = i;
                        GUI.FocusControl(null); // drop focus so a pending DelayedTextField doesn't bleed across rows
                    }

                    GUI.backgroundColor = prev;
                }
            }

            DrawListButtons();
        }
    }

    private void DrawListButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add"))
            {
                drafts.Add(new Draft
                {
                    name = "new waveform",
                    sequence = "QQQQ",
                    amplitude = "8888",
                    rounding = Waveform.BeatPulseRounding,
                    offset = 0f,
                });
                drafts[drafts.Count - 1].RebuildPreview();
                selected = drafts.Count - 1;
                SetDirty(true);
            }

            using (new EditorGUI.DisabledScope(selected < 0))
            {
                if (GUILayout.Button("Duplicate"))
                {
                    var src = drafts[selected];
                    var copy = new Draft
                    {
                        name = src.name + " copy",
                        sequence = src.sequence,
                        amplitude = src.amplitude,
                        rounding = src.rounding,
                        offset = src.offset,
                    };
                    copy.RebuildPreview();
                    drafts.Insert(selected + 1, copy);
                    selected += 1;
                    SetDirty(true);
                }

                if (GUILayout.Button("Delete"))
                {
                    drafts.RemoveAt(selected);
                    selected = Mathf.Clamp(selected, -1, drafts.Count - 1);
                    if (drafts.Count == 0)
                    {
                        selected = -1;
                    }
                    SetDirty(true);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        using (new EditorGUI.DisabledScope(selected < 0))
        {
            if (GUILayout.Button("Move Up") && selected > 0)
            {
                (drafts[selected - 1], drafts[selected]) = (drafts[selected], drafts[selected - 1]);
                selected -= 1;
                SetDirty(true);
            }

            if (GUILayout.Button("Move Down") && selected >= 0 && selected < drafts.Count - 1)
            {
                (drafts[selected + 1], drafts[selected]) = (drafts[selected], drafts[selected + 1]);
                selected += 1;
                SetDirty(true);
            }
        }
    }

    /// <summary>Draws the selected Draft fields and its immutable parsed preview.</summary>
    private void DrawEditor()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (selected < 0 || selected >= drafts.Count)
            {
                EditorGUILayout.HelpBox("No Preset selected. Add one, or open a Pool file that has Presets.",
                    MessageType.Info);
                return;
            }

            var d = drafts[selected];

            // Sequence/amplitude rebuild the humps. We use TextField instead of DelayedTextField so the
            // envelope plot updates live as you type.
            EditorGUI.BeginChangeCheck();
            var newName = EditorGUILayout.TextField("Name", d.name);
            var newSeq = EditorGUILayout.TextField("Sequence", d.sequence);
            EditorGUILayout.LabelField(" ", SequenceHint, EditorStyles.wordWrappedMiniLabel);
            var newAmp = EditorGUILayout.TextField("Amplitude", d.amplitude);
            EditorGUILayout.LabelField(" ", AmplitudeHint, EditorStyles.wordWrappedMiniLabel);
            var structuralChanged = EditorGUI.EndChangeCheck();
            if (structuralChanged)
            {
                d.name = newName;
                d.sequence = newSeq.ToUpperInvariant();
                d.amplitude = newAmp;
                d.RebuildPreview();
                SetDirty(true);
            }

            // Rounding/offset are part of the immutable preview value, so replace it when either Draft field changes.
            EditorGUI.BeginChangeCheck();
            var newRounding = EditorGUILayout.Slider(
                new GUIContent("Rounding", "0 sharp triangle → ~0.5 cosine dome → 1 flat top. Trough always 0."),
                d.rounding, 0f, 1f);
            var newOffset = EditorGUILayout.FloatField(
                new GUIContent("Offset (beats)", "Phase shift in beats; 0.5 lands the peak on the \"&\" (offbeat)."),
                d.offset);
            if (EditorGUI.EndChangeCheck())
            {
                d.rounding = newRounding;
                d.offset = newOffset;
                d.preview = Waveform.Parse(d.sequence, d.amplitude, newRounding, newOffset);
                SetDirty(true);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Envelope (one bar)", EditorStyles.boldLabel);
            var plotRect = GUILayoutUtility.GetRect(100f, 120f, GUILayout.ExpandWidth(true));
            DrawPlot(plotRect, d.preview);

            if (d.preview.IsMalformed)
            {
                EditorGUILayout.HelpBox("This Preset is malformed (see Console for the exact defect — amplitude " +
                    "length vs. sequence, or widths not summing to one bar). The envelope is bounded but won't " +
                    "match intent.", MessageType.Warning);
            }

            // The exact line that will be written, composed the same way WaveformPool.Serialize does (invariant
            // culture for the numbers) so the preview matches the saved file byte-for-byte in content.
            EditorGUILayout.LabelField("Saves as", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(
                $"DEFINE_WAVEFORM({d.name}){{ {d.sequence} | {d.amplitude} | " +
                $"{d.rounding.ToString("0.###", CultureInfo.InvariantCulture)} | " +
                $"{d.offset.ToString("0.###", CultureInfo.InvariantCulture)} }}",
                EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }

    /// <summary>Plots one bar of the envelope: dark track, 4/4 beat gridlines, and the anti-aliased curve (orange when
    /// malformed). No playhead — this is the authoring view; the live playhead lives on the BeatManager dashboard.</summary>
    private static void DrawPlot(Rect rect, Waveform wf)
    {
        // The shared primitive draws the track, gridlines, and AA curve; the pool view is static (no
        // playhead) and shows a malformed envelope in orange.
        WaveformPlot.Draw(rect, wf, wf.IsMalformed ? MalformedCurveColor : WaveformPlot.Curve);
    }
}
