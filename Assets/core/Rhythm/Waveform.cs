using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A one-bar rhythmic brightness envelope, built by merging <see cref="Hump"/>s end-to-end in time.
/// </summary>
/// <remarks>
/// <para>
/// A Waveform is an immutable rhythmic envelope definition. <see cref="Parse(string,string,float,float)"/>
/// constructs an authoring/kernel value; <see cref="Waveforms"/> binds Pool values to runtime playback.
/// It describes a one-bar rhythm with four parts:
/// </para>
/// <list type="bullet">
///   <item><b>sequence</b> — one note-value token per Hump, left to right, giving each Hump's width.
///     <c>W</c> whole (4 beats), <c>H</c> half (2), <c>Q</c> quarter (1), <c>E</c> eighth (½),
///     <c>S</c> sixteenth (¼). Sixteenth is the fastest allowed (32nd+ is a full-wall flicker hazard).
///     The widths of a well-formed Waveform sum to exactly one bar.</item>
///   <item><b>amplitude</b> — one digit <c>0–8</c> per Hump, read 1:1 straight across, mapped to
///     <c>[0..1]</c> via <c>digit ÷ 8</c>. <c>0</c> makes a Hump silent — that is how a beat is
///     skipped. There is no separate rest token; <b>amplitude 0 is the gate</b>.</item>
///   <item><b>rounding</b> — a scalar <c>[0..1]</c> shaping the peak: 0 = sharp triangle, rising to a
///     cosine dome, then growing a flat top pinned at 1. The trough between beats always falls to 0.</item>
///   <item><b>offset</b> — a phase shift measured in beats (<c>0.5</c> = the offbeat "&"); slides the
///     whole Waveform along the bar without changing the Humps' shape or count.</item>
/// </list>
/// <para>
/// Humps are <b>concatenated in time, never summed</b>: this is a unipolar envelope (1 at a peak on the
/// beat, 0 in the trough), not a bipolar audio signal. There is no layering and no overlap. (The web
/// "designer" app's additive/alternating-half-cycle model was deliberately rejected — see
/// <c>docs/adr/0001-waveform-rhythm-model.md</c>.)
/// </para>
/// <para>
/// A runtime Waveform acquired from <see cref="Waveforms"/> is bound to the shared musical clock and
/// exposes its current <see cref="Envelope"/> plus direct response helpers such as <see cref="Lerp"/>.
/// <see cref="Sample"/> remains the clock-agnostic shape kernel used by runtime playback and Editor
/// plots, so visualization and playback cannot drift.
/// </para>
/// <para>
/// <b>Malformation is reported and bounded, not hidden.</b> The runtime-facing parser logs diagnostics;
/// authoring callers can receive the same diagnostics as data. Missing or invalid amplitudes evaluate as
/// silence so <see cref="Sample"/> remains safe for inspection. Runtime <see cref="Waveforms"/> rejects
/// malformed Pool entries instead of silently falling back to the plain pulse.
/// </para>
/// </remarks>
public readonly struct Waveform
{
    /// <summary>Beats in one bar. The notation is defined in 4/4, so a bar is four quarter-note beats.</summary>
    public const int BeatsPerBar = 4;

    /// <summary>
    /// Canonical rounding of the plain Beat Pulse (<c>QQQQ</c> / <c>8888</c>). An inline spec that
    /// omits rounding and offset defaults to this shaping, and the seed Pool's <c>beat pulse</c>
    /// Preset carries the same value.
    /// </summary>
    public const float BeatPulseRounding = 0.3f;

    /// <summary>
    /// Largest fraction of a Hump's slot the flat top is allowed to occupy at maximum rounding.
    /// Capped below 1 so a trough always remains before the next beat — the contract is "trough always
    /// falls to 0 at every rounding value." Tunable in the property drawer against live playback.
    /// </summary>
    private const float FlatTopMaxFraction = 0.85f;

    /// <summary>The raw sequence string (note-value tokens), kept verbatim for display and canonical rewrite.</summary>
    public readonly string sequence;

    /// <summary>The raw amplitude string (digits 0–8), kept verbatim for display and canonical rewrite.</summary>
    public readonly string amplitude;

    /// <summary>Peak shape, 0 (sharp triangle) → cosine dome → flat top. Clamped to [0..1] when shaping.</summary>
    public readonly float rounding;

    /// <summary>Phase shift in beats. 0 leaves the Waveform on the beat; 0.5 lands it on the offbeat "&".</summary>
    public readonly float offset;

    /// <summary>Parsed Hump slots laid end-to-end across the bar. Built by <see cref="Parse(string,string,float,float)"/>.</summary>
    private readonly Hump[] humps;

    /// <summary>True when parsing found a defect (length mismatch, bad token, widths not summing to a bar).</summary>
    private readonly bool malformed;

    /// <summary>The shared live musical source for a runtime-bound Waveform; null for parsed authoring values.</summary>
    private readonly BeatManager clockSource;

    /// <summary>Whether this explicit value suppresses Waveform response for an owning Mixer.</summary>
    private readonly bool disabled;

    /// <summary>Distinguishes a constructed Waveform from the CLR default struct value.</summary>
    private readonly bool initialized;

    /// <summary>Whether this Waveform parsed with a reported defect. It still evaluates safely against one bounded bar.</summary>
    public bool IsMalformed => malformed;

    /// <summary>Creates one fully parsed immutable Waveform value.</summary>
    /// <param name="sequence">Raw note-value tokens retained for display and canonical rewrite.</param>
    /// <param name="amplitude">Raw amplitude digits retained for display and canonical rewrite.</param>
    /// <param name="rounding">Peak shape scalar.</param>
    /// <param name="offset">Phase shift in beats.</param>
    /// <param name="humps">Parsed Hump slots laid end-to-end across the bar.</param>
    /// <param name="malformed">Whether parsing found a reported defect.</param>
    private Waveform(
        string sequence,
        string amplitude,
        float rounding,
        float offset,
        Hump[] humps,
        bool malformed,
        BeatManager clockSource = null,
        bool disabled = false)
    {
        this.sequence = sequence;
        this.amplitude = amplitude;
        this.rounding = rounding;
        this.offset = offset;
        this.humps = humps;
        this.malformed = malformed;
        this.clockSource = clockSource;
        this.disabled = disabled;
        initialized = true;
    }

    /// <summary>
    /// Current Waveform envelope in <c>[0..1]</c>; rests at 0 when no live Bar Phase exists or this
    /// is the explicit <see cref="Waveforms.None"/> value.
    /// </summary>
    public float Envelope
    {
        get
        {
            EnsureInitialized();
            return TrySampleCurrent(out var envelope) ? envelope : 0f;
        }
    }

    /// <summary>
    /// Maps the current envelope between caller-chosen endpoints. Without a live Bar Phase, returns
    /// <paramref name="to"/> so Standalone rendering keeps the established steady response.
    /// </summary>
    /// <param name="from">Value at the Waveform trough.</param>
    /// <param name="to">Value at the Waveform peak and the Standalone fallback.</param>
    public float Lerp(float from, float to)
    {
        EnsureInitialized();
        return TrySampleCurrent(out var envelope) ? envelope.Lerp(from, to) : to;
    }

    /// <summary>
    /// Shortest spacing between audible peaks in milliseconds at the live tempo; 0 when tempo is
    /// unavailable or this value intentionally suppresses Waveform response.
    /// </summary>
    public float ShortestPeakSpacingMs
    {
        get
        {
            EnsureInitialized();
            EnsureRuntimeBound();
            return !disabled && clockSource.Timing.BeatAverageMilliseconds is { } beatAverageMs
                ? ShortestNonZeroPeakSpacing() * BeatsPerBar * beatAverageMs
                : 0f;
        }
    }

    /// <summary>Binds one parsed immutable shape to the shared live musical source.</summary>
    /// <param name="source">The BeatManager whose Timing and Grid values drive playback.</param>
    internal Waveform Bind(BeatManager source)
    {
        EnsureInitialized();
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new Waveform(sequence, amplitude, rounding, offset, humps, malformed, source);
    }

    /// <summary>Creates the explicit non-null value Mixers assign to suppress child Waveform response.</summary>
    /// <param name="source">The shared musical source that owns the value.</param>
    internal static Waveform Disabled(BeatManager source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new Waveform("", "", 0f, 0f, Array.Empty<Hump>(), false, source, disabled: true);
    }

    /// <summary>The shared musical source this runtime-bound value reads.</summary>
    internal BeatManager ClockSource
    {
        get
        {
            EnsureInitialized();
            return clockSource;
        }
    }

    /// <summary>Whether this value represents intentional Waveform-response suppression.</summary>
    internal bool IsDisabled
    {
        get
        {
            EnsureInitialized();
            return disabled;
        }
    }

    /// <summary>A single Hump's time slot in bar-fraction units, plus its normalized height.</summary>
    private readonly struct Hump
    {
        /// <summary>Slot start as a fraction of the bar, in [0..1). The beat is at this instant.</summary>
        public readonly float start;

        /// <summary>Slot width as a fraction of the bar (quarter = 0.25, eighth = 0.125, …).</summary>
        public readonly float width;

        /// <summary>Hump height in [0..1] (amplitude digit ÷ 8). Zero makes the Hump silent.</summary>
        public readonly float amplitude;

        /// <summary>Creates one immutable parsed Hump slot.</summary>
        /// <param name="start">Slot start as a fraction of the bar.</param>
        /// <param name="width">Slot width as a fraction of the bar.</param>
        /// <param name="amplitude">Normalized Hump height.</param>
        public Hump(float start, float width, float amplitude)
        {
            this.start = start;
            this.width = width;
            this.amplitude = amplitude;
        }

        /// <summary>
        /// Whether this Hump contributes a peak: nonzero Amplitude, a real time slot, and a start
        /// inside the bar. Silent, zero-width, and malformed overflow Humps are excluded from every
        /// peak measurement.
        /// </summary>
        public bool IsAudible => amplitude > 0f && width > 0f && start >= 0f && start < 1f;
    }

    /// <summary>
    /// Parses an inline Waveform with default shaping — the plain Beat Pulse's rounding and zero offset.
    /// </summary>
    /// <remarks>
    /// This convenience is used by the Pool codec, Editor previews, and tests. Omitting rounding and
    /// offset applies the canonical Beat Pulse shaping without deriving the authored notation from that
    /// Preset. Runtime Performers acquire clock-bound values from <see cref="Waveforms"/>.
    /// </remarks>
    public static Waveform Parse(string sequence, string amplitude)
    {
        return Parse(sequence, amplitude, BeatPulseRounding, 0f);
    }

    /// <summary>
    /// Parses a full Waveform spec into safe Hump slots and logs every diagnostic.
    /// </summary>
    /// <param name="sequence">Note-value tokens, one per Hump (<c>W H Q E S</c>).</param>
    /// <param name="amplitude">Digits <c>0–8</c>, one per Hump, read straight across.</param>
    /// <param name="rounding">Peak shape scalar [0..1].</param>
    /// <param name="offset">Phase shift in beats.</param>
    public static Waveform Parse(string sequence, string amplitude, float rounding, float offset)
    {
        var waveform = Parse(sequence, amplitude, rounding, offset, out var diagnostics);
        for (var i = 0; i < diagnostics.Length; i++)
        {
            Debug.LogWarning($"[Waveform] {diagnostics[i]}");
        }

        return waveform;
    }

    /// <summary>
    /// Parses a full Waveform spec and returns its diagnostics without writing to the Console.
    /// </summary>
    /// <remarks>
    /// The returned value remains safe to inspect through <see cref="Sample"/> when malformed. Runtime
    /// acquisition still rejects malformed Pool entries; this overload lets authoring surfaces present
    /// transient typing errors in place instead of logging on every keystroke.
    /// </remarks>
    /// <param name="sequence">Note-value tokens, one per Hump (<c>W H Q E S</c>).</param>
    /// <param name="amplitude">Digits <c>0–8</c>, one per Hump, read straight across.</param>
    /// <param name="rounding">Peak shape scalar [0..1].</param>
    /// <param name="offset">Phase shift in beats.</param>
    /// <param name="diagnostics">Every notation defect found while parsing.</param>
    public static Waveform Parse(
        string sequence,
        string amplitude,
        float rounding,
        float offset,
        out string[] diagnostics)
    {
        var seq = sequence ?? "";
        var amp = amplitude ?? "";
        var malformed = false;
        var messages = new List<string>();

        // Amplitude is read 1:1 against the sequence. A length mismatch is a defect we report and tolerate:
        // a missing digit reads as silent (0), an extra digit is ignored. We do not pad or truncate the
        // stored strings — the raw spec is preserved for canonical rewrite.
        if (seq.Length != amp.Length)
        {
            malformed = true;
            messages.Add($"sequence/amplitude length mismatch ({seq.Length} vs {amp.Length}) " +
                         $"in \"{seq}\" / \"{amp}\" — missing digits read as silent.");
        }

        var humps = new Hump[seq.Length];
        var cursorBeats = 0f;
        for (var i = 0; i < seq.Length; i++)
        {
            var widthBeats = TokenBeats(seq[i]);
            if (widthBeats <= 0f)
            {
                // Unknown token: report it, give it zero width so it occupies no time, and keep parsing the rest.
                malformed = true;
                messages.Add($"unknown sequence token '{seq[i]}' in \"{seq}\" — expected one of W H Q E S.");
                humps[i] = new Hump(cursorBeats / BeatsPerBar, 0f, 0f);
                continue;
            }

            var amplitudeValue = AmplitudeAt(amp, i, out var invalidAmplitude);
            if (invalidAmplitude)
            {
                malformed = true;
                messages.Add($"invalid amplitude '{amp[i]}' at position {i + 1} in \"{amp}\" — expected a digit 0–8; read as silent.");
            }

            humps[i] = new Hump(
                cursorBeats / BeatsPerBar,
                widthBeats / BeatsPerBar,
                amplitudeValue);
            cursorBeats += widthBeats;
        }

        // Widths should sum to exactly one bar. If they do not, it is a defect we report; Bar Phase still
        // bounds evaluation to [0..1), so an under-filled bar shows trough in the gap and an over-filled
        // bar simply never reaches its trailing Humps.
        if (!Mathf.Approximately(cursorBeats, BeatsPerBar))
        {
            malformed = true;
            messages.Add($"widths sum to {cursorBeats} beats, expected {BeatsPerBar} " +
                         $"in \"{seq}\" — evaluated against one bounded bar regardless.");
        }

        diagnostics = messages.ToArray();
        return new Waveform(seq, amp, rounding, offset, humps, malformed);
    }

    /// <summary>
    /// Samples this Waveform at a normalized Bar Phase and returns brightness in <c>[0..1]</c>.
    /// </summary>
    /// <remarks>
    /// This is the evaluation kernel. <paramref name="barPhase"/> is the live clock (0 on the downbeat,
    /// 1 at the next downbeat); the caller owns it. Brightness is <b>symmetric around every beat</b>: full
    /// on the beat (a Hump's onset), falling to 0 at the midpoint to the adjacent beat, then rising back
    /// into the next beat. Evaluation: shift by <see cref="offset"/>, find the segment between two
    /// consecutive beats, decide which beat <paramref name="barPhase"/> is nearer, and return that beat's
    /// amplitude × <see cref="ShapeHump"/> of the normalized distance to it. The trough sits exactly at the
    /// midpoint, so the fall after one beat and the rise into the next meet there.
    /// </remarks>
    /// <param name="barPhase">Position within the bar; values outside [0..1) are wrapped.</param>
    public float Sample(float barPhase)
    {
        EnsureInitialized();
        if (humps == null || humps.Length == 0)
        {
            return 0f;
        }

        // Looking up an earlier phase moves the authored peaks later by the requested offset.
        var lookup = Mathf.Repeat(barPhase - (offset / BeatsPerBar), 1f);

        for (var i = 0; i < humps.Length; i++)
        {
            var hump = humps[i];
            if (hump.width <= 0f)
            {
                continue;
            }

            var segmentStart = hump.start;
            var segmentEnd = hump.start + hump.width;
            if (lookup < segmentStart || lookup >= segmentEnd)
            {
                continue;
            }

            var midpoint = (segmentStart + segmentEnd) * 0.5f;
            if (lookup < midpoint)
            {
                var distanceFromPeak = (lookup - segmentStart) / (midpoint - segmentStart);
                return hump.amplitude * ShapeHump(distanceFromPeak, rounding);
            }

            // The next Hump owns the rising half; the final segment wraps to the next bar's downbeat.
            var nextAmplitude = humps[(i + 1) % humps.Length].amplitude;
            var distanceFromNextPeak = (segmentEnd - lookup) / (segmentEnd - midpoint);
            return nextAmplitude * ShapeHump(distanceFromNextPeak, rounding);
        }

        // A malformed under-filled bar reads its uncovered interval as trough.
        return 0f;
    }

    /// <summary>Samples this bound value at the current Bar Phase when one exists.</summary>
    /// <param name="envelope">The sampled envelope, or 0 when live placement is unavailable.</param>
    /// <returns>True when a live Bar Phase was available and sampled.</returns>
    private bool TrySampleCurrent(out float envelope)
    {
        EnsureRuntimeBound();
        if (!disabled && clockSource.Timing.BarProgress is { } barPhase)
        {
            envelope = Sample(barPhase);
            return true;
        }

        envelope = 0f;
        return false;
    }

    /// <summary>Fails visibly when an effect tries to use the CLR default instead of an acquired Waveform.</summary>
    private void EnsureInitialized()
    {
        if (!initialized)
        {
            throw new InvalidOperationException(
                "Waveform is uninitialized. Acquire one from Waveforms or assign waveforms.None explicitly.");
        }
    }

    /// <summary>Fails visibly when clock-driven playback is requested from an authoring-only parsed value.</summary>
    private void EnsureRuntimeBound()
    {
        if (clockSource == null)
        {
            throw new InvalidOperationException(
                "Waveform playback requires a runtime value acquired from Waveforms.");
        }
    }

    /// <summary>
    /// Returns the shortest normalized bar distance between any two audible peaks in this Waveform.
    /// </summary>
    /// <remarks>
    /// Only Humps with amplitude greater than 0 count as peaks. The closing wrap gap from the last audible
    /// peak back to the first audible peak in the next bar is included. A single audible peak is one full-bar
    /// spacing from itself next bar, so it returns 1; no audible peaks return 0.
    /// </remarks>
    public float ShortestNonZeroPeakSpacing()
    {
        if (humps == null || humps.Length == 0)
        {
            return 0f;
        }

        var firstPeak = -1f;
        var previousPeak = -1f;
        var shortest = 1f;
        var audiblePeakCount = 0;

        for (var i = 0; i < humps.Length; i++)
        {
            var h = humps[i];
            if (!h.IsAudible)
            {
                continue;
            }

            var peak = h.start;
            if (audiblePeakCount == 0)
            {
                firstPeak = peak;
            }
            else
            {
                shortest = Mathf.Min(shortest, peak - previousPeak);
            }

            previousPeak = peak;
            audiblePeakCount++;
        }

        if (audiblePeakCount == 0)
        {
            return 0f;
        }

        if (audiblePeakCount == 1)
        {
            return 1f;
        }

        var wrapGap = firstPeak + 1f - previousPeak;
        return Mathf.Clamp01(Mathf.Min(shortest, wrapGap));
    }

    /// <summary>
    /// This Waveform's Energy on the shared ladder — derived from the notation, never authored or
    /// stored, so it can never disagree with the shape. It covers every Waveform, Pool or inline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Energy = max(density tier, gap tier). Density is audible peaks per bar (Humps with nonzero
    /// Amplitude): 1–2 Low, 3–4 Mid, 5 or more High. Gap is the shortest audible peak spacing
    /// (<see cref="ShortestNonZeroPeakSpacing"/>, converted to beats): 2 beats or more Low, 1 beat
    /// or more Mid, under 1 beat High. Amplitude heights are excluded — a quiet pulse ranks with
    /// the full one — and there is no syncopation bump. All widths are powers-of-two beat
    /// fractions, so the tier boundaries compare exactly.
    /// </para>
    /// <para>
    /// A silent Waveform (no audible peak) reads Low: zero peaks sit below Low's density floor,
    /// and the spacing measurement's 0-for-silence convention must not leak High through the gap
    /// tier.
    /// </para>
    /// </remarks>
    public Energy Energy
    {
        get
        {
            var audible = AudiblePeakCount();
            if (audible == 0)
            {
                return Energy.Low;
            }

            var density = audible <= 2 ? Energy.Low : audible <= 4 ? Energy.Mid : Energy.High;
            var gapBeats = ShortestNonZeroPeakSpacing() * BeatsPerBar;
            var gap = gapBeats >= 2f ? Energy.Low : gapBeats >= 1f ? Energy.Mid : Energy.High;
            return density >= gap ? density : gap;
        }
    }

    /// <summary>Counts the audible peaks — the same Humps <see cref="ShortestNonZeroPeakSpacing"/> measures.</summary>
    private int AudiblePeakCount()
    {
        if (humps == null)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < humps.Length; i++)
        {
            if (humps[i].IsAudible)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Shapes the peak: returns brightness in <c>[0..1]</c> at normalized distance <paramref name="u"/> from a beat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="u"/> is 0 on the beat (peak) and 1 at the trough (the midpoint to the adjacent beat).
    /// The same curve is used on the falling and the rising side of every beat, so brightness is symmetric
    /// around the beat. Rounding reshapes only the region near the peak; the trough always reaches 0:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>rounding = 0</c> — linear fall (sharp triangle).</item>
    ///   <item><c>rounding ≈ 0.5</c> — full cosine dome.</item>
    ///   <item><c>rounding → 1</c> — a flat top pinned at 1 grows, capped by <see cref="FlatTopMaxFraction"/>.</item>
    /// </list>
    /// <para>
    /// The breakpoints (<c>(r-0.5)·2</c> for the plateau, <c>r·2</c> for the dome) and
    /// <see cref="FlatTopMaxFraction"/> are visual-tuning constants to refine in the property drawer; the
    /// contract is only "sharp at 0, dome then flat-top as it rises, trough always 0."
    /// </para>
    /// </remarks>
    private static float ShapeHump(float u, float rounding)
    {
        var distanceFromPeak = Mathf.Clamp01(u);
        var clampedRounding = Mathf.Clamp01(rounding);

        // The plateau grows only in the upper half of rounding and leaves room for a zero trough.
        var plateau = Mathf.Clamp01((clampedRounding - 0.5f) * 2f) * FlatTopMaxFraction;
        if (distanceFromPeak <= plateau)
        {
            return 1f;
        }

        var fallProgress = (distanceFromPeak - plateau) / (1f - plateau);
        var domeBlend = Mathf.Clamp01(clampedRounding * 2f);
        var triangle = 1f - fallProgress;
        var cosine = (Mathf.Cos(fallProgress * Mathf.PI) + 1f) * 0.5f;
        return domeBlend.Lerp(triangle, cosine);
    }

    /// <summary>Returns a token's width in beats, or 0 for an unrecognized token (caller reports and skips).</summary>
    private static float TokenBeats(char token) => token switch
    {
        'W' => 4f,    // whole — the full bar
        'H' => 2f,    // half
        'Q' => 1f,    // quarter — one beat
        'E' => 0.5f,  // eighth
        'S' => 0.25f, // sixteenth — the fastest allowed width
        _ => 0f,
    };

    /// <summary>Reads one amplitude digit and reports notation outside the documented <c>0–8</c> range.</summary>
    /// <param name="amplitude">The raw amplitude string.</param>
    /// <param name="index">The Hump position to read.</param>
    /// <param name="invalid">True when a present character is not a digit from <c>0</c> through <c>8</c>.</param>
    /// <returns>The normalized amplitude, or zero for missing/invalid notation.</returns>
    private static float AmplitudeAt(string amplitude, int index, out bool invalid)
    {
        invalid = false;
        if (amplitude == null || index < 0 || index >= amplitude.Length)
        {
            return 0f;
        }

        var c = amplitude[index];
        if (c < '0' || c > '8')
        {
            invalid = true;
            return 0f;
        }

        return (c - '0') / 8f;
    }
}
