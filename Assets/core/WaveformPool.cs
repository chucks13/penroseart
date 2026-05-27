using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// The canonical codec for the Waveform Pool file (<c>penrose_waveforms.txt</c>): the single owner of how
/// Presets are parsed from, and serialized to, the hand-editable StreamingAssets text format.
/// </summary>
/// <remarks>
/// <para>
/// Both sides of the system go through here so the read path and the write path can never disagree about
/// the format. The runtime (<see cref="BeatManager"/>) parses the file on load; the Editor's Waveform Pool
/// window parses on open and rewrites the whole file canonically on save. That single-owner rule is exactly
/// why the format is dead simple — one macro per line:
/// </para>
/// <code>DEFINE_WAVEFORM(name){ sequence | amplitude | rounding | offset }</code>
/// <para>
/// with <c>//</c> line comments and blank lines ignored, in the spirit of <c>palettedata.txt</c>. Every
/// defect (a broken macro, a wrong field count, an unparseable number) is logged; nothing is silently
/// dropped or substituted. <see cref="Serialize"/> does not preserve hand-authored comments or formatting —
/// only <c>DEFINE_WAVEFORM</c> records survive a save, replaced under a fresh canonical header.
/// </para>
/// </remarks>
public static class WaveformPool
{
    /// <summary>The StreamingAssets file the Pool lives in, in the spirit of <c>palettedata.txt</c>.</summary>
    public const string FileName = "penrose_waveforms.txt";

    /// <summary>The macro token each Preset line opens with.</summary>
    private const string DefineToken = "DEFINE_WAVEFORM(";

    /// <summary>Absolute path to the Pool file under StreamingAssets (valid in the Editor and at runtime).</summary>
    public static string FilePath => Application.streamingAssetsPath + "/" + FileName;

    /// <summary>One named Preset: a display name paired with its parsed Waveform.</summary>
    public struct Entry
    {
        public string name;
        public Waveform waveform;

        public Entry(string name, Waveform waveform)
        {
            this.name = name;
            this.waveform = waveform;
        }
    }

    /// <summary>
    /// Reads the Pool file, distinguishing the expected missing-file case (returns empty, the caller seeds)
    /// from a present-but-unreadable file (logged as an error — a real defect, never silently swallowed).
    /// </summary>
    public static string ReadFileOrEmpty()
    {
        var path = FilePath;
        if (!File.Exists(path))
        {
            return "";
        }

        try
        {
            using var sr = new StreamReader(path);
            return sr.ReadToEnd();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Waveform] Failed to read {FileName}: {e.Message}");
            return "";
        }
    }

    /// <summary>
    /// Parses every <c>DEFINE_WAVEFORM(name){ seq | amp | round | offset }</c> record from file text into
    /// ordered <see cref="Entry"/> values, mirroring the GPalette hand-rolled <c>DEFINE_*</c> scan but
    /// splitting the body on <c>|</c>. Order is preserved — it is load-bearing for <c>beatVariant</c> indexing.
    /// </summary>
    /// <remarks>
    /// Line comments are stripped first (see <see cref="StripLineComments"/>) so the canonical header comment
    /// — which itself spells out the macro — is not parsed as a bogus Preset. Each defect is logged;
    /// <see cref="Waveform.Parse"/> logs its own notation defects. Nothing is silently dropped.
    /// </remarks>
    public static List<Entry> Parse(string fileText)
    {
        var entries = new List<Entry>();
        if (string.IsNullOrEmpty(fileText))
        {
            return entries;
        }

        var text = StripLineComments(fileText);
        var cursor = 0;
        while (true)
        {
            var def = text.IndexOf(DefineToken, cursor, StringComparison.Ordinal);
            if (def < 0)
            {
                break;
            }

            var nameStart = def + DefineToken.Length;
            var nameEnd = text.IndexOf(')', nameStart);
            var braceOpen = nameEnd >= 0 ? text.IndexOf('{', nameEnd) : -1;
            var braceClose = braceOpen >= 0 ? text.IndexOf('}', braceOpen) : -1;
            if (nameEnd < 0 || braceOpen < 0 || braceClose < 0)
            {
                Debug.LogWarning($"[Waveform] Malformed DEFINE_WAVEFORM near offset {def} in {FileName} — " +
                                 "expected name(){ seq | amp | round | offset }. Stopping parse here.");
                break; // cannot reliably advance past a broken macro
            }

            var name = text.Substring(nameStart, nameEnd - nameStart).Trim();
            var body = text.Substring(braceOpen + 1, braceClose - braceOpen - 1);
            cursor = braceClose + 1;

            var parts = body.Split('|');
            if (parts.Length != 4)
            {
                Debug.LogWarning($"[Waveform] Preset \"{name}\" has {parts.Length} field(s), expected 4 " +
                                 "(seq | amp | round | offset) — skipped.");
                continue;
            }

            var seq = parts[0].Trim();
            var amp = parts[1].Trim();
            var rounding = ParseFloatField(parts[2], Waveform.BeatPulseRounding, name, "rounding");
            var offset = ParseFloatField(parts[3], 0f, name, "offset");

            entries.Add(new Entry(name, Waveform.Parse(seq, amp, rounding, offset)));
        }

        return entries;
    }

    /// <summary>
    /// Rewrites the whole Pool canonically: a fixed header comment, then one column-aligned macro per entry,
    /// in list order. This is the full-file rewrite the Editor performs on save — see the type remarks.
    /// </summary>
    public static string Serialize(IReadOnlyList<Entry> entries)
    {
        var sb = new StringBuilder();
        sb.Append(CanonicalHeader);

        // Pad name/sequence/amplitude to aligned columns so a hand-reading the file (the bootstrap path)
        // gets the same tidy, stacked look the seed file ships with.
        var nameWidth = 0;
        var seqWidth = 0;
        var ampWidth = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            nameWidth = Mathf.Max(nameWidth, (entries[i].name ?? "").Length);
            seqWidth = Mathf.Max(seqWidth, (entries[i].waveform.sequence ?? "").Length);
            ampWidth = Mathf.Max(ampWidth, (entries[i].waveform.amplitude ?? "").Length);
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var name = entries[i].name ?? "";
            var wf = entries[i].waveform;
            var seq = (wf.sequence ?? "").PadRight(seqWidth);
            var amp = (wf.amplitude ?? "").PadRight(ampWidth);
            var round = wf.rounding.ToString("0.###", CultureInfo.InvariantCulture);
            var offset = wf.offset.ToString("0.###", CultureInfo.InvariantCulture);

            sb.Append(DefineToken).Append(name).Append(')')
              .Append(' ', Mathf.Max(1, (nameWidth - name.Length) + 1))
              .Append("{ ").Append(seq).Append(" | ").Append(amp).Append(" | ")
              .Append(round).Append(" | ").Append(offset).Append(" }")
              .Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>The canonical header re-emitted on every save. Carries the load-bearing ORDER warning.</summary>
    private const string CanonicalHeader =
        "// penrose_waveforms.txt — Waveform Pool\n" +
        "//\n" +
        "// Canonically rewritten by the Waveform Pool editor (Window > Penrose). Comments and hand formatting\n" +
        "// are NOT preserved across a save — only DEFINE_WAVEFORM records survive. Hand-editing is the\n" +
        "// bootstrap/fallback path; the editor is the primary one.\n" +
        "//\n" +
        "// Format, one Preset per line:\n" +
        "//   DEFINE_WAVEFORM(name){ sequence | amplitude | rounding | offset }\n" +
        "//   sequence   note-value tokens per Hump: W=whole(4) H=half(2) Q=quarter(1) E=eighth(1/2) S=sixteenth(1/4);\n" +
        "//              widths sum to one bar (4/4).\n" +
        "//   amplitude  one digit 0-8 per Hump, read straight across (digit/8 -> [0..1]). 0 is the gate: a\n" +
        "//              silent Hump = a skipped beat.\n" +
        "//   rounding   0..1 peak shape: 0 sharp triangle -> ~0.5 cosine dome -> 1 flat top.\n" +
        "//   offset     phase shift in beats; 0.5 lands on the \"&\" (offbeat).\n" +
        "//\n" +
        "// NOTE: the entry ORDER is load-bearing. The legacy `int beatVariant` currency indexes this list, and\n" +
        "// BeatManager.IsBeatTriggered couples its gate labels to indexes 1/2/3 (beats 1&3, beats 2&4, measure\n" +
        "// start). The \"chill\" random subset is the lower-index range. Reorder with that in mind.\n" +
        "\n";

    /// <summary>Parses a Pool numeric field with invariant culture, logging and defaulting on a bad value.</summary>
    /// <remarks>Invariant culture is required so <c>0.5</c> reads the same on comma-decimal locales.</remarks>
    private static float ParseFloatField(string raw, float fallback, string presetName, string fieldName)
    {
        if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        Debug.LogWarning($"[Waveform] Preset \"{presetName}\" has an unparseable {fieldName} \"{raw.Trim()}\" — " +
                         $"defaulting to {fallback}.");
        return fallback;
    }

    /// <summary>Removes <c>//</c> line comments while preserving line structure, so the macro scan ignores them.</summary>
    private static string StripLineComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var line in text.Split('\n'))
        {
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            sb.Append(comment >= 0 ? line.Substring(0, comment) : line);
            sb.Append('\n');
        }

        return sb.ToString();
    }
}
