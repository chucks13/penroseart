using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// The canonical codec for the Waveform Pool file (<c>penrose_waveforms.txt</c>): the single owner of
/// Preset parsing and serialization for the hand-editable StreamingAssets text format.
/// </summary>
/// <remarks>
/// <para>
/// Both sides of the system go through here so the read path and the write path can never disagree about
/// the format. The runtime (<see cref="Waveforms"/>) parses the file on load; the Editor's Waveform Pool
/// window parses on open and rewrites the whole file canonically on save. That single-owner rule is exactly
/// why the format is dead simple — one macro per line:
/// </para>
/// <code>DEFINE_WAVEFORM(name){ sequence | amplitude | rounding | offset }</code>
/// <para>
/// with <c>//</c> line comments and blank lines ignored, in the spirit of <c>palettedata.txt</c>. Every
/// defect (a broken macro, a wrong field count, an unparseable number) is reported; no record loss or
/// numeric substitution is silent. <see cref="Serialize"/> does not preserve hand-authored comments or formatting —
/// only <c>DEFINE_WAVEFORM</c> records survive a save, replaced under a fresh canonical header.
/// </para>
/// </remarks>
public static class WaveformPool
{
    /// <summary>The StreamingAssets file the Pool lives in, in the spirit of <c>palettedata.txt</c>.</summary>
    public const string FileName = "penrose_waveforms.txt";

    /// <summary>The macro token each Preset line opens with.</summary>
    private const string DefineToken = "DEFINE_WAVEFORM(";

    /// <summary>Characters that would alter the Pool record grammar when embedded in a Preset name.</summary>
    private static readonly char[] ReservedNameCharacters = { '(', ')', '{', '}', '|', '\r', '\n' };

    /// <summary>Absolute path to the Pool file under StreamingAssets (valid in the Editor and at runtime).</summary>
    public static string FilePath => Application.streamingAssetsPath + "/" + FileName;

    /// <summary>One named Preset: a display name paired with its parsed Waveform.</summary>
    public struct Entry
    {
        /// <summary>The human/editor display name; runtime acquisition does not treat it as identity.</summary>
        public string name;

        /// <summary>The parsed one-bar Waveform definition.</summary>
        public Waveform waveform;

        /// <summary>Creates one named Pool entry.</summary>
        /// <param name="name">The human/editor display name.</param>
        /// <param name="waveform">The parsed Waveform definition.</param>
        public Entry(string name, Waveform waveform)
        {
            this.name = name;
            this.waveform = waveform;
        }
    }

    /// <summary>
    /// Reads the Pool file. Missing or unreadable files return empty; runtime <see cref="Waveforms"/>
    /// treats that as a startup configuration error, while Editor tooling may present an empty document.
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
            using var reader = new StreamReader(path);
            return reader.ReadToEnd();
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
    /// splitting the body on <c>|</c>. Order is preserved for stable authoring and serialization.
    /// </summary>
    /// <remarks>
    /// Line comments are stripped first (see <see cref="StripLineComments"/>) so the canonical header comment
    /// — which itself spells out the macro — is not parsed as a bogus Preset. This runtime-facing overload
    /// logs every returned diagnostic; no skipped record is silent.
    /// </remarks>
    public static List<Entry> Parse(string fileText)
    {
        var entries = Parse(fileText, out var diagnostics);
        for (var i = 0; i < diagnostics.Length; i++)
        {
            Debug.LogWarning($"[Waveform] {diagnostics[i]}");
        }

        return entries;
    }

    /// <summary>
    /// Parses the Pool and returns every codec/notation diagnostic without writing to the Console.
    /// </summary>
    /// <param name="fileText">The complete Pool file content.</param>
    /// <param name="diagnostics">Every defect found while scanning records and parsing Waveforms.</param>
    /// <returns>All records that could be recovered in source order.</returns>
    public static List<Entry> Parse(string fileText, out string[] diagnostics)
    {
        var entries = new List<Entry>();
        var messages = new List<string>();
        if (string.IsNullOrEmpty(fileText))
        {
            diagnostics = Array.Empty<string>();
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
                messages.Add($"Malformed DEFINE_WAVEFORM near offset {def} in {FileName} — " +
                             "expected name(){ seq | amp | round | offset }. Stopping parse here.");
                break; // cannot reliably advance past a broken macro
            }

            var name = text[nameStart..nameEnd].Trim();
            if (!IsValidName(name))
            {
                messages.Add($"Preset name \"{name}\" is empty or contains a reserved delimiter.");
            }

            var body = text[(braceOpen + 1)..braceClose];
            cursor = braceClose + 1;

            var parts = body.Split('|');
            if (parts.Length != 4)
            {
                messages.Add($"Preset \"{name}\" has {parts.Length} field(s), expected 4 " +
                             "(seq | amp | round | offset) — skipped.");
                continue;
            }

            var seq = parts[0].Trim();
            var amp = parts[1].Trim();
            var rounding = ParseFloatField(parts[2], Waveform.BeatPulseRounding, name, "rounding", messages);
            var offset = ParseFloatField(parts[3], 0f, name, "offset", messages);
            var waveform = Waveform.Parse(seq, amp, rounding, offset, out var waveformDiagnostics);
            for (var i = 0; i < waveformDiagnostics.Length; i++)
            {
                messages.Add($"Preset \"{name}\": {waveformDiagnostics[i]}");
            }

            entries.Add(new Entry(name, waveform));
        }

        diagnostics = messages.ToArray();
        return entries;
    }

    /// <summary>Whether a Preset name can be serialized without changing the Pool record structure.</summary>
    /// <remarks>Names are display labels. They need not be unique, but they cannot be blank or contain macro delimiters.</remarks>
    /// <param name="name">The proposed display name.</param>
    /// <returns><see langword="true"/> when the name is safe to serialize.</returns>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.IndexOfAny(ReservedNameCharacters) < 0;
    }

    /// <summary>
    /// Rewrites the whole Pool canonically: a fixed header comment, then one column-aligned macro per entry,
    /// in list order. This is the full-file rewrite the Editor performs on save — see the type remarks.
    /// </summary>
    public static string Serialize(IReadOnlyList<Entry> entries)
    {
        var builder = new StringBuilder();
        builder.Append(CanonicalHeader);

        // Align the hand-editable bootstrap format with the tidy, stacked look of the seed file.
        var nameWidth = 0;
        var sequenceWidth = 0;
        var amplitudeWidth = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!IsValidName(entry.name))
            {
                throw new ArgumentException(
                    $"Waveform Pool entry {i} has an empty or delimiter-containing name.",
                    nameof(entries));
            }

            nameWidth = Mathf.Max(nameWidth, entry.name.Length);
            sequenceWidth = Mathf.Max(sequenceWidth, (entry.waveform.sequence ?? "").Length);
            amplitudeWidth = Mathf.Max(amplitudeWidth, (entry.waveform.amplitude ?? "").Length);
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var sequence = (entry.waveform.sequence ?? "").PadRight(sequenceWidth);
            var amplitude = (entry.waveform.amplitude ?? "").PadRight(amplitudeWidth);
            var roundingText = entry.waveform.rounding.ToString("0.###", CultureInfo.InvariantCulture);
            var offsetText = entry.waveform.offset.ToString("0.###", CultureInfo.InvariantCulture);

            builder
                .Append(DefineToken)
                .Append(entry.name)
                .Append(')')
                .Append(' ', Mathf.Max(1, nameWidth - entry.name.Length + 1))
                .Append("{ ")
                .Append(sequence)
                .Append(" | ")
                .Append(amplitude)
                .Append(" | ")
                .Append(roundingText)
                .Append(" | ")
                .Append(offsetText)
                .Append(" }")
                .Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>The canonical header re-emitted on every save.</summary>
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
        "// Entry order is preserved for authoring. Runtime performers acquire by Energy, uniformly across\n" +
        "// the whole Pool, or by entry name; no performer stores a row index. Renaming an entry breaks any\n" +
        "// saved setting that selects it by name — visibly, at the next acquisition.\n" +
        "\n";

    /// <summary>Parses a Pool numeric field with invariant culture, reporting and defaulting on a bad value.</summary>
    /// <remarks>Invariant culture is required so <c>0.5</c> reads the same on comma-decimal locales.</remarks>
    /// <param name="raw">The raw field text.</param>
    /// <param name="fallback">The safe replacement used when parsing fails.</param>
    /// <param name="presetName">The containing Preset display name.</param>
    /// <param name="fieldName">The field label used in diagnostics.</param>
    /// <param name="diagnostics">The parse report receiving any substitution.</param>
    private static float ParseFloatField(
        string raw,
        float fallback,
        string presetName,
        string fieldName,
        ICollection<string> diagnostics)
    {
        var text = raw.Trim();
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            !float.IsNaN(value) && !float.IsInfinity(value))
        {
            return value;
        }

        diagnostics.Add($"Preset \"{presetName}\" has an unparseable {fieldName} \"{text}\" — defaulting to {fallback}.");
        return fallback;
    }

    /// <summary>Removes <c>//</c> line comments while preserving line structure, so the macro scan ignores them.</summary>
    private static string StripLineComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var line in text.Split('\n'))
        {
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            sb.Append(comment >= 0 ? line[..comment] : line);
            sb.Append('\n');
        }

        return sb.ToString();
    }
}
