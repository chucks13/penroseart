// The Levels doorway: the live audio band triple in three forms — Normalized, Smoothed, Peak —
// with the shared readings and the Color Bank's parameterized mappings (beat-data spec).

#nullable enable

using UnityEngine;

/// <summary>
/// The three audio bands of the Levels triple. Distinct from <see cref="Energy"/> (phrase-level
/// intensity): a Band names an instantaneous audio lane, not a tier on the intensity ladder.
/// </summary>
public enum Band
{
    /// <summary>The low-frequency band.</summary>
    Low,

    /// <summary>The mid-frequency band.</summary>
    Mid,

    /// <summary>The high-frequency band.</summary>
    High,
}

/// <summary>
/// A Color Bank component source: a band, a reading, or any float constant — the shared readings
/// vocabulary as knobs. Implicitly convertible from float, so a fixed component is just the
/// number: <c>triple.Hsv(s: 0.7f)</c>. StrongestBand is not a source — it names a band, not a
/// 0..1 value.
/// </summary>
public readonly struct LevelSource
{
    /// <summary>Which triple value the source reads; Constant reads the stored number instead.</summary>
    private enum Selector
    {
        Low,
        Mid,
        High,
        Average,
        Strongest,
        Centroid,
        Dominance,
        Constant,
    }

    private readonly Selector selector;
    private readonly float constant;

    private LevelSource(Selector selector, float constant)
    {
        this.selector = selector;
        this.constant = constant;
    }

    /// <summary>The low band.</summary>
    public static readonly LevelSource Low = new LevelSource(Selector.Low, 0f);

    /// <summary>The mid band.</summary>
    public static readonly LevelSource Mid = new LevelSource(Selector.Mid, 0f);

    /// <summary>The high band.</summary>
    public static readonly LevelSource High = new LevelSource(Selector.High, 0f);

    /// <summary>The Average reading — mean band energy.</summary>
    public static readonly LevelSource Average = new LevelSource(Selector.Average, 0f);

    /// <summary>The Strongest reading — the strongest band's value.</summary>
    public static readonly LevelSource Strongest = new LevelSource(Selector.Strongest, 0f);

    /// <summary>The Centroid reading — spectral balance, low 0 to high 1.</summary>
    public static readonly LevelSource Centroid = new LevelSource(Selector.Centroid, 0f);

    /// <summary>The Dominance reading — how much the strongest band dominates the weakest.</summary>
    public static readonly LevelSource Dominance = new LevelSource(Selector.Dominance, 0f);

    /// <summary>Any constant component value, e.g. pinning saturation: <c>Hsv(s: 0.7f)</c>.</summary>
    public static implicit operator LevelSource(float constant)
    {
        return new LevelSource(Selector.Constant, constant);
    }

    /// <summary>Resolves this source against one triple — the Color Bank's component read.</summary>
    internal float Read(in LevelsTriple triple)
    {
        switch (selector)
        {
            case Selector.Low: return triple.Low;
            case Selector.Mid: return triple.Mid;
            case Selector.High: return triple.High;
            case Selector.Average: return triple.Average;
            case Selector.Strongest: return triple.Strongest;
            case Selector.Centroid: return triple.Centroid;
            case Selector.Dominance: return triple.Dominance;
            default: return constant;
        }
    }
}

/// <summary>
/// The one triple shape every Levels form serves: three bands — each normalized 0..1 to the
/// track's own maxima by the sender, each carrying its own rhythm — plus the readings, which
/// double as the Color Bank's source vocabulary. The Color Bank's three parameterized mappings
/// ride the triple, so an effect colors with its chosen temperament: peak color snaps, smoothed
/// color glides, normalized color mirrors the wire.
/// </summary>
public readonly struct LevelsTriple
{
    /// <summary>Low band, 0..1.</summary>
    public float Low { get; }

    /// <summary>Mid band, 0..1.</summary>
    public float Mid { get; }

    /// <summary>High band, 0..1.</summary>
    public float High { get; }

    // ---- The readings — the ingredients consumers kept rebuilding privately --------------------

    /// <summary>Mean band energy, 0..1.</summary>
    public float Average => (Low + Mid + High) / 3f;

    /// <summary>The strongest band's value, 0..1.</summary>
    public float Strongest => Mathf.Max(Low, Mathf.Max(Mid, High));

    /// <summary>Which band is strongest. Ties break deterministically: Low over Mid over High.</summary>
    public Band StrongestBand => Low >= Mid && Low >= High ? Band.Low : Mid >= High ? Band.Mid : Band.High;

    /// <summary>
    /// Spectral balance point, 0 (all low) through 0.5 (all mid) to 1 (all high); 0 at silence,
    /// never NaN.
    /// </summary>
    public float Centroid
    {
        get
        {
            var total = Low + Mid + High;
            return total <= 0f ? 0f : ((Mid * 0.5f) + High) / total;
        }
    }

    /// <summary>
    /// How much the strongest band dominates the weakest, 0 (all bands equal) to 1 (weakest is
    /// silent), 0..1; 0 at silence, never NaN.
    /// </summary>
    public float Dominance
    {
        get
        {
            var strongest = Strongest;
            if (strongest <= 0f)
            {
                return 0f;
            }

            return (strongest - Mathf.Min(Low, Mathf.Min(Mid, High))) / strongest;
        }
    }

    // ---- The Color Bank: three parameterized mappings, defaults = the classic wirings ----------

    /// <summary>
    /// Color Bank, RGB mapping. A null knob is the classic wiring for that slot — low→R, mid→G,
    /// high→B — never "not available". Channels clamp to 0..1; alpha is 1.
    /// </summary>
    public Color Rgb(LevelSource? r = null, LevelSource? g = null, LevelSource? b = null)
    {
        return new Color(
            Mathf.Clamp01((r ?? LevelSource.Low).Read(this)),
            Mathf.Clamp01((g ?? LevelSource.Mid).Read(this)),
            Mathf.Clamp01((b ?? LevelSource.High).Read(this)),
            1f);
    }

    /// <summary>
    /// Color Bank, HSV mapping. Classic wiring: centroid→H, dominance→S, strongest→V — so the
    /// color moves with the music's balance while brightness pulses with its energy, and silence
    /// falls out black naturally. Pin a component with a constant: <c>Hsv(s: 0.7f)</c>.
    /// </summary>
    public Color Hsv(LevelSource? h = null, LevelSource? s = null, LevelSource? v = null)
    {
        return Color.HSVToRGB(
            (h ?? LevelSource.Centroid).Read(this),
            (s ?? LevelSource.Dominance).Read(this),
            (v ?? LevelSource.Strongest).Read(this));
    }

    /// <summary>
    /// Color Bank, palette-mediated mapping over the caller's palette — the Bank never fetches
    /// one. Classic wiring: centroid picks the palette position, the strongest band scales
    /// brightness; alpha is preserved, not scaled.
    /// </summary>
    public Color FromPalette(AnimPalette palette, LevelSource? position = null,
        LevelSource? brightness = null)
    {
        var color = palette.read((position ?? LevelSource.Centroid).Read(this));
        var scale = (brightness ?? LevelSource.Strongest).Read(this);
        return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
    }

    /// <summary>Built only by the hub's per-update capture, one form at a time.</summary>
    internal LevelsTriple(float low, float mid, float high)
    {
        Low = low;
        Mid = mid;
        High = high;
    }
}

/// <summary>
/// The Levels doorway payload: three forms of the one band triple, and effects pick by
/// temperament — Normalized mirrors the wire, Smoothed glides, Peak snaps and drains. The
/// doorway is nullable as a whole (the forms come all together or not at all) and carries no
/// signals: Levels runs at frame rate, so there is no edge to serve.
/// </summary>
public readonly struct LevelsView
{
    /// <summary>The wire fact untouched — each band already normalized 0..1 to the track's own maxima by the sender.</summary>
    public LevelsTriple Normalized { get; }

    /// <summary>
    /// The attack/release follower — bands glide. Its two knobs
    /// (<see cref="BeatManager.levelsAttackSeconds"/> / <see cref="BeatManager.levelsReleaseSeconds"/>)
    /// live with the inbound knobs, apart from the data reads.
    /// </summary>
    public LevelsTriple Smoothed { get; }

    /// <summary>
    /// Instant rise, linear drain of full scale in one beat — fixed 500 ms when levels flow with
    /// no usable tempo. Tempo-anchored: no knob.
    /// </summary>
    public LevelsTriple Peak { get; }

    /// <summary>Built only by the hub's per-update capture, after the shaping state has settled.</summary>
    internal LevelsView(LevelsTriple normalized, LevelsTriple smoothed, LevelsTriple peak)
    {
        Normalized = normalized;
        Smoothed = smoothed;
        Peak = peak;
    }
}

public partial class BeatManager
{
    // ---- Inbound knobs — feel with no musical anchor, living apart from the data reads ---------

    /// <summary>
    /// Attack time-constant in seconds for rising Levels. Small = bands jump up almost instantly,
    /// keeping strobes punchy.
    /// </summary>
    public float levelsAttackSeconds = 0.02f;

    /// <summary>
    /// Release time-constant in seconds for falling Levels. Larger than attack so bands decay
    /// smoothly instead of snapping down. Tune on the wall.
    /// </summary>
    public float levelsReleaseSeconds = 0.15f;

    // ---- Shaping state — dropped whole on the unavailable gate so live samples snap in fresh ---

    /// <summary>Smoothed low band — the attack/release follower's state.</summary>
    private float smoothedLow;

    /// <summary>Smoothed mid band — the attack/release follower's state.</summary>
    private float smoothedMid;

    /// <summary>Smoothed high band — the attack/release follower's state.</summary>
    private float smoothedHigh;

    /// <summary>
    /// Whether the shaping state (Smoothed and Peak alike) is primed by a live sample — the
    /// all-or-nothing availability gate <see cref="CaptureLevels"/> reads. False exactly while
    /// the levels lane is unavailable.
    /// </summary>
    private bool hasSmoothedLevels;

    /// <summary>The last <see cref="Update(float)"/> clock value, for the shaping delta.</summary>
    private float lastSmoothingTime;

    /// <summary>Whether <see cref="lastSmoothingTime"/> holds a real clock value yet.</summary>
    private bool hasSmoothingClock;

    /// <summary>Peak low band — the tempo-anchored drain's state.</summary>
    private float peakLow;

    /// <summary>Peak mid band — the tempo-anchored drain's state.</summary>
    private float peakMid;

    /// <summary>Peak high band — the tempo-anchored drain's state.</summary>
    private float peakHigh;

    // ---- The doorway ----------------------------------------------------------------------------

    /// <summary>
    /// The Levels doorway, captured once per hub update ahead of effect Draw — identical for
    /// every reader within a frame. Null when the levels lane is unavailable; when present, the
    /// three forms come all together.
    /// </summary>
    public LevelsView? Levels { get; private set; }

    /// <summary>
    /// Advances the Levels shaping state — the Smoothed follower and the Peak drain — for this
    /// frame. Called from <see cref="Update(float)"/> after <see cref="beatData"/> has settled,
    /// for both live OSC and Standalone (no beat). On the unavailable gate the shaping state
    /// drops whole, so the next live samples snap in fresh instead of releasing or draining from
    /// stale values.
    /// </summary>
    private void UpdateLevelsShaping(float timeSeconds)
    {
        var deltaSeconds = hasSmoothingClock ? Mathf.Max(0f, timeSeconds - lastSmoothingTime) : 0f;
        lastSmoothingTime = timeSeconds;
        hasSmoothingClock = true;

        var raw = beatData?.snapshot.levels ?? PenroseArt.RaveOsc.Levels.Unavailable;
        if (raw.low < 0f || raw.mid < 0f || raw.high < 0f)
        {
            // Unavailable: drop the shaping state entirely so the next live sample snaps in fresh
            // instead of releasing from stale values.
            hasSmoothedLevels = false;
            return;
        }

        var low = Mathf.Clamp01(raw.low);
        var mid = Mathf.Clamp01(raw.mid);
        var high = Mathf.Clamp01(raw.high);
        if (!hasSmoothedLevels)
        {
            smoothedLow = low;
            smoothedMid = mid;
            smoothedHigh = high;
            peakLow = low;
            peakMid = mid;
            peakHigh = high;
            hasSmoothedLevels = true;
            return;
        }

        smoothedLow = SmoothTowards(smoothedLow, low, deltaSeconds);
        smoothedMid = SmoothTowards(smoothedMid, mid, deltaSeconds);
        smoothedHigh = SmoothTowards(smoothedHigh, high, deltaSeconds);

        // Linear drain of the full scale in one beat; the sample floors the drain (instant rise),
        // and samples are non-negative, so the peak can never fall below 0.
        var drain = deltaSeconds / PeakDrainBeatSeconds();
        peakLow = Mathf.Max(peakLow - drain, low);
        peakMid = Mathf.Max(peakMid - drain, mid);
        peakHigh = Mathf.Max(peakHigh - drain, high);
    }

    /// <summary>
    /// The Peak drain's beat duration in seconds: the live tempo the Clock doorway serves (the
    /// wire fact behind <see cref="ClockView.BeatAverageMs"/>, mirrored availability and all), or
    /// the fixed 500 ms when levels flow with no usable tempo. Tempo-anchored — no knob.
    /// </summary>
    private float PeakDrainBeatSeconds()
    {
        return IsSynced && beatData.snapshot.beatAverageMs > 0
            ? beatData.snapshot.beatAverageMs / 1000f
            : 0.5f;
    }

    /// <summary>
    /// Exponentially smooths one band towards its target, fast on the way up (attack) and slower on the
    /// way down (release).
    /// </summary>
    private float SmoothTowards(float current, float target, float deltaSeconds)
    {
        var timeConstant = target > current ? levelsAttackSeconds : levelsReleaseSeconds;
        if (timeConstant <= 0f)
        {
            return target;
        }

        var alpha = 1f - Mathf.Exp(-deltaSeconds / timeConstant);
        return current + ((target - current) * alpha);
    }

    /// <summary>
    /// Captures the Levels doorway from the settled shaping state. All-or-nothing: null exactly
    /// when this frame's shaping gate found the lane unavailable (any wire band negative, or
    /// Standalone clearing); otherwise Normalized serves the wire fact untouched beside the
    /// Smoothed follower and the Peak drain.
    /// </summary>
    private LevelsView? CaptureLevels()
    {
        if (!hasSmoothedLevels)
        {
            return null;
        }

        var raw = beatData.snapshot.levels;
        return new LevelsView(
            new LevelsTriple(raw.low, raw.mid, raw.high),
            new LevelsTriple(smoothedLow, smoothedMid, smoothedHigh),
            new LevelsTriple(peakLow, peakMid, peakHigh));
    }
}
