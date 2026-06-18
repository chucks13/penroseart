using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared editor rendering of a one-bar <see cref="Waveform"/> envelope: a dark track, 4/4 beat
/// gridlines, and the anti-aliased curve of <see cref="Waveform.Evaluate"/> across the bar, with an
/// optional live playhead.
///
/// Both the BeatManager dashboard (live, with a playhead) and the WaveformPool authoring inspector
/// (static) draw this exact plot. Keeping the track/grid/curve look — and the value→Y alignment that
/// places the curve and the playhead dot — in one place is what stops the two views from drifting.
///
/// This is intentionally a thin drawing primitive (locality/dedup, not testability): callers own the
/// state→color decision (active vs idle, normal vs malformed) and any text readout, which lives
/// outside the plot rect.
/// </summary>
internal static class WaveformPlot
{
    /// <summary>
    /// Canonical "on" curve color. Callers pass this for the active/normal state and substitute their
    /// own state color (e.g. idle or malformed) when appropriate.
    /// </summary>
    public static readonly Color Curve = new Color(0.12f, 0.92f, 1f);

    private static readonly Color TrackColor = new Color(0.055f, 0.065f, 0.085f);
    private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.07f);
    private static readonly Color PlayheadColor = new Color(1f, 0.92f, 0.35f);
    private static readonly Color PlayheadLineColor = new Color(1f, 0.92f, 0.35f, 0.55f);

    private const float VPad = 3f;   // keep the peak (1) and trough (0) just off the track edges
    private const int Samples = 128; // polyline resolution across the bar

    /// <summary>
    /// Draws the envelope plot into <paramref name="rect"/>: dark track,
    /// <see cref="Waveform.BeatsPerBar"/> gridlines, and the anti-aliased curve of
    /// <paramref name="wf"/> in <paramref name="curveColor"/>.
    ///
    /// When <paramref name="playheadPhase"/> has a value, also draws an aligned vertical playhead line
    /// and a dot at the emitted value for that bar phase. The numeric readout (if any) is the caller's
    /// responsibility and is expected to be laid out outside <paramref name="rect"/>.
    /// </summary>
    /// <param name="rect">Plot bounds. Gridlines and the curve fill this rect; the caller reserves any
    /// space it needs for a readout before passing the rect in.</param>
    /// <param name="wf">The waveform whose one-bar envelope is plotted via <see cref="Waveform.Evaluate"/>.</param>
    /// <param name="curveColor">Color for the AA curve — the caller's state decides it (active/idle/malformed).</param>
    /// <param name="playheadPhase">Bar phase for the live playhead, or <c>null</c> for a static plot.</param>
    public static void Draw(Rect rect, Waveform wf, Color curveColor, float? playheadPhase = null)
    {
        EditorGUI.DrawRect(rect, TrackColor);

        // 4/4 gridlines at the quarter-note fractions (0, ¼, ½, ¾, and the closing bar edge).
        for (var i = 0; i <= Waveform.BeatsPerBar; i++)
        {
            var gx = Mathf.Floor(rect.x + (rect.width * (i / (float)Waveform.BeatsPerBar)));
            EditorGUI.DrawRect(new Rect(gx, rect.y, 1f, rect.height), GridColor);
        }

        // Track and gridlines are filled above so the shape reads even on layout passes; the AA curve
        // and playhead are Repaint-only, so the rest of the method short-circuits otherwise.
        if (Event.current.type != EventType.Repaint || rect.width <= 1f)
        {
            return;
        }

        var top = rect.y + VPad;
        var bottom = rect.yMax - VPad;

        var points = new Vector3[Samples + 1];
        for (var i = 0; i <= Samples; i++)
        {
            var p = i / (float)Samples;
            var y = Mathf.Lerp(bottom, top, Mathf.Clamp01(wf.Evaluate(p)));
            points[i] = new Vector3(rect.x + (rect.width * p), y, 0f);
        }

        Handles.color = curveColor;
        Handles.DrawAAPolyLine(2.5f, points);

        if (!playheadPhase.HasValue)
        {
            return;
        }

        // Vertical playhead line at the (wrapped) phase, and a dot at the emitted value. The dot uses the
        // same VPad→top/bottom mapping as the curve so it always sits exactly on the line.
        var phase = Mathf.Repeat(playheadPhase.Value, 1f);
        var px = rect.x + (rect.width * phase);
        EditorGUI.DrawRect(new Rect(Mathf.Floor(px), rect.y, 1f, rect.height), PlayheadLineColor);

        var emitted = Mathf.Clamp01(wf.Evaluate(playheadPhase.Value));
        var dotY = Mathf.Lerp(bottom, top, emitted);
        EditorGUI.DrawRect(new Rect(px - 3f, dotY - 3f, 6f, 6f), PlayheadColor);
    }
}
