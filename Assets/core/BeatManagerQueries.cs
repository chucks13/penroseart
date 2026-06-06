// Cooked rhythm query layer for BeatManager (ADR-0002: nullable cooked rhythm queries).

#nullable enable

using System;
using System.Runtime.CompilerServices;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>Phrase energy vocabulary mirrored from RaveSystem's closed PhraseEnergy set.</summary>
public enum EnergyLevel
{
    Low = 0,
    Mid = 1,
    High = 2,
}

/// <summary>
/// Cooked two-phase Fill state: a short build-up flourish (typically one measure, 4-8 beats) that
/// effects can anticipate before it starts and ride while it plays.
/// </summary>
/// <remarks>
/// Returned by <see cref="BeatManager.Fill"/>; null there means no Fill data is available right now.
/// Within a non-null value, null fields mean the wire did not supply that detail.
/// </remarks>
public readonly struct FillInfo
{
    /// <summary>True while the Fill is playing now; false while counting down to the next one.</summary>
    public readonly bool inProgress;

    /// <summary>Whole beats until the Fill starts. Null while in progress, or when the wire did not say.</summary>
    public readonly int? beatsUntilStart;

    /// <summary>Beat-smoothed progress through the Fill in [0..1]. 0 while the Fill is upcoming.</summary>
    public readonly float progress;

    /// <summary>Total Fill length in beats. Null when the wire did not say.</summary>
    public readonly int? lengthBeats;

    /// <summary>Remaining Fill occurrences in this track. Null when the wire did not say.</summary>
    public readonly int? remaining;

    public FillInfo(bool inProgress, int? beatsUntilStart, float progress, int? lengthBeats, int? remaining)
    {
        this.inProgress = inProgress;
        this.beatsUntilStart = beatsUntilStart;
        this.progress = progress;
        this.lengthBeats = lengthBeats;
        this.remaining = remaining;
    }
}

/// <summary>
/// Cooked two-phase Drop state, mirroring <see cref="FillInfo"/>: effects can anticipate an upcoming
/// Drop ("land the transition on it") and ride it while it plays.
/// </summary>
/// <remarks>
/// Returned by <see cref="BeatManager.Drop"/>; null there means no Drop data is available right now.
/// Within a non-null value, null fields mean the wire did not supply that detail.
/// </remarks>
public readonly struct DropInfo
{
    /// <summary>True while the Drop is playing now; false while counting down to the next one.</summary>
    public readonly bool inProgress;

    /// <summary>Whole beats until the Drop starts. Null while in progress, or when the wire did not say.</summary>
    public readonly int? beatsUntilStart;

    /// <summary>Beat-smoothed progress through the Drop in [0..1]. 0 while the Drop is upcoming.</summary>
    public readonly float progress;

    /// <summary>Total Drop length in beats. Null when the wire did not say.</summary>
    public readonly int? lengthBeats;

    /// <summary>Remaining Drop occurrences in this track. Null when the wire did not say.</summary>
    public readonly int? remaining;

    public DropInfo(bool inProgress, int? beatsUntilStart, float progress, int? lengthBeats, int? remaining)
    {
        this.inProgress = inProgress;
        this.beatsUntilStart = beatsUntilStart;
        this.progress = progress;
        this.lengthBeats = lengthBeats;
        this.remaining = remaining;
    }
}

/// <summary>
/// Cooked phrase Energy: the track's current intensity tier parsed once into the closed
/// <see cref="EnergyLevel"/> vocabulary, with where it is heading next.
/// </summary>
/// <remarks>
/// Returned by <see cref="BeatManager.Energy"/>; null there means Energy is unavailable or the wire
/// label was not in the Low/Mid/High vocabulary (an unrecognized label never becomes a wrong enum).
/// </remarks>
public readonly struct EnergyInfo
{
    /// <summary>The current energy tier.</summary>
    public readonly EnergyLevel level;

    /// <summary>The upcoming energy tier. Null when the wire did not say or the label was unrecognized.</summary>
    public readonly EnergyLevel? next;

    /// <summary>Whole beats until the energy changes. Null when the wire did not say.</summary>
    public readonly int? beatsUntilChange;

    /// <summary>The current tier as a normalized value: Low = 0, Mid = 0.5, High = 1.</summary>
    public readonly float normalized;

    /// <summary>Where the energy is heading: +1 rising, -1 falling, 0 steady or unknown.</summary>
    public readonly int direction;

    public EnergyInfo(EnergyLevel level, EnergyLevel? next, int? beatsUntilChange, float normalized, int direction)
    {
        this.level = level;
        this.next = next;
        this.beatsUntilChange = beatsUntilChange;
        this.normalized = normalized;
        this.direction = direction;
    }
}

/// <summary>
/// Cooked Track Phase: open-vocabulary section labels passed through untouched, with the countdown
/// structure cooked into usable numbers.
/// </summary>
/// <remarks>
/// Returned by <see cref="BeatManager.Phase"/>; null there means no phase data is available right now.
/// Labels are an open vocabulary ("Drop", "Break", "Chorus 2") — do not keyword-parse them as if the
/// set were closed; that is what <see cref="BeatManager.Energy"/> is for.
/// </remarks>
public readonly struct PhaseInfo
{
    /// <summary>The current phase label as broadcast. Never null or empty for a non-null PhaseInfo.</summary>
    public readonly string label;

    /// <summary>The upcoming phase label. Null when the wire did not say.</summary>
    public readonly string? next;

    /// <summary>True while the phase state is active now (RaveSystem tri-state 1).</summary>
    public readonly bool inPhase;

    /// <summary>Whole beats until the next phase boundary. Null when the wire did not say.</summary>
    public readonly int? beatsUntilNext;

    /// <summary>Total length of the current phase in beats. Null when the wire did not say.</summary>
    public readonly int? lengthBeats;

    /// <summary>Remaining phase changes in this track. Null when the wire did not say.</summary>
    public readonly int? remaining;

    /// <summary>Beat-smoothed progress through the current phase in [0..1]. 0 when not in a phase.</summary>
    public readonly float progress;

    public PhaseInfo(string label, string? next, bool inPhase, int? beatsUntilNext, int? lengthBeats, int? remaining, float progress)
    {
        this.label = label;
        this.next = next;
        this.inPhase = inPhase;
        this.beatsUntilNext = beatsUntilNext;
        this.lengthBeats = lengthBeats;
        this.remaining = remaining;
        this.progress = progress;
    }
}

/// <summary>
/// Cooked Levels: normalized low/mid/high band energy with BeatManager's attack/release smoothing
/// already applied, so effects can drive the wall from them without re-implementing anti-flicker.
/// </summary>
/// <remarks>
/// Returned by <see cref="BeatManager.Levels"/>; null there means no live Levels are available
/// (the local beat simulator never supplies them).
/// </remarks>
public readonly struct LevelsInfo
{
    /// <summary>Smoothed low-band energy in [0..1].</summary>
    public readonly float low;

    /// <summary>Smoothed mid-band energy in [0..1].</summary>
    public readonly float mid;

    /// <summary>Smoothed high-band energy in [0..1].</summary>
    public readonly float high;

    public LevelsInfo(float low, float mid, float high)
    {
        this.low = low;
        this.mid = mid;
        this.high = high;
    }
}

/// <summary>
/// The cooked rhythm query layer (ADR-0002). Effects and transitions pull all musical state through
/// these nullable queries: null always means "not available right now", and the caller owns its
/// Default Mode fallback (<c>?? fallback</c>) or Synced Mode branch (<c>is { } x</c>).
/// </summary>
/// <remarks>
/// This is the only place that reads the transport's -1 sentinels and tri-state ints; sentinels never
/// cross into effect math. The serialized <see cref="BeatData"/> transport keeps its plain fields for
/// Unity serialization and Inspector visibility.
/// </remarks>
public partial class BeatManager
{
    /// <summary>
    /// Attack time-constant in seconds for rising Levels. Small = bands jump up almost instantly,
    /// keeping strobes punchy.
    /// </summary>
    public float levelsAttackSeconds = 0.02f;

    /// <summary>
    /// Release time-constant in seconds for falling Levels. Larger than attack so bands decay smoothly
    /// instead of flickering — flicker is the enemy, strobing is the point. Tune on the wall.
    /// </summary>
    public float levelsReleaseSeconds = 0.15f;

    private float smoothedLow;
    private float smoothedMid;
    private float smoothedHigh;
    private bool hasSmoothedLevels;
    private float lastSmoothingTime;
    private bool hasSmoothingClock;

    /// <summary>
    /// The Waveform envelope for a Pool variant at the current Bar Phase, or null when no beat clock
    /// is running. This is the primitive under every beat-synced brightness/time derivation.
    /// </summary>
    public float? Envelope(int variant)
    {
        if (!IsActive)
        {
            return null;
        }

        return GetWaveform(variant).Evaluate(BarPhase);
    }

    /// <summary>Cooked Fill state, or null when no Fill data is available right now.</summary>
    public FillInfo? Fill
    {
        get
        {
            var data = beatData;
            if (data == null || data.fillState.active < 0)
            {
                return null;
            }

            var state = data.fillState;
            var inProgress = state.active > 0;
            return new FillInfo(
                inProgress,
                CookBeatsUntilStart(inProgress, state.countBeats),
                inProgress ? CookPhraseProgress(state.countBeats, state.lengthBeats) : 0f,
                NonNegativeOrNull(state.lengthBeats),
                NonNegativeOrNull(state.remaining));
        }
    }

    /// <summary>Cooked Drop state, or null when no Drop data is available right now.</summary>
    public DropInfo? Drop
    {
        get
        {
            var data = beatData;
            if (data == null || data.dropState.active < 0)
            {
                return null;
            }

            var state = data.dropState;
            var inProgress = state.active > 0;
            return new DropInfo(
                inProgress,
                CookBeatsUntilStart(inProgress, state.countBeats),
                inProgress ? CookPhraseProgress(state.countBeats, state.lengthBeats) : 0f,
                NonNegativeOrNull(state.lengthBeats),
                NonNegativeOrNull(state.remaining));
        }
    }

    /// <summary>
    /// Cooked phrase Energy, or null when Energy is unavailable or the wire label is outside the
    /// Low/Mid/High vocabulary.
    /// </summary>
    public EnergyInfo? Energy
    {
        get
        {
            var data = beatData;
            if (data == null || data.energyState.active < 0)
            {
                return null;
            }

            var state = data.energyState;
            if (!TryParseEnergyLevel(state.current, out var level))
            {
                return null;
            }

            EnergyLevel? next = TryParseEnergyLevel(state.next, out var nextLevel) ? nextLevel : (EnergyLevel?)null;
            var direction = next is { } heading ? Math.Sign((int)heading - (int)level) : 0;
            return new EnergyInfo(
                level,
                next,
                NonNegativeOrNull(state.countBeats),
                (int)level * 0.5f,
                direction);
        }
    }

    /// <summary>Cooked Track Phase, or null when no phase data is available right now.</summary>
    public PhaseInfo? Phase
    {
        get
        {
            var data = beatData;
            if (data == null || data.phaseState.active < 0 || string.IsNullOrEmpty(data.phaseState.current))
            {
                return null;
            }

            var state = data.phaseState;
            var inPhase = state.active > 0;
            return new PhaseInfo(
                state.current!,
                string.IsNullOrEmpty(state.next) ? null : state.next,
                inPhase,
                NonNegativeOrNull(state.countBeats),
                NonNegativeOrNull(state.lengthBeats),
                NonNegativeOrNull(state.remaining),
                inPhase ? CookPhraseProgress(state.countBeats, state.lengthBeats) : 0f);
        }
    }

    /// <summary>
    /// Smoothed Levels, or null when no live Levels are available. Smoothing is applied once per
    /// <see cref="Update(float)"/> using <see cref="levelsAttackSeconds"/>/<see cref="levelsReleaseSeconds"/>.
    /// </summary>
    public LevelsInfo? Levels
    {
        get
        {
            if (!hasSmoothedLevels)
            {
                return null;
            }

            return new LevelsInfo(smoothedLow, smoothedMid, smoothedHigh);
        }
    }

    /// <summary>
    /// Color Bank, raw form: the smoothed bands mapped straight onto RGB channels (low→red, mid→green,
    /// high→blue), so the color runs black→bright with the music. Null when Levels are unavailable.
    /// </summary>
    public Color? LevelsRgb
    {
        get
        {
            if (!(Levels is { } levels))
            {
                return null;
            }

            return new Color(Mathf.Clamp01(levels.low), Mathf.Clamp01(levels.mid), Mathf.Clamp01(levels.high), 1f);
        }
    }

    /// <summary>
    /// Color Bank, hue/saturation form: hue is the spectral centroid of the bands (low→red, mid→green,
    /// high→blue), saturation is how dominant the strongest band is over the weakest, and value is the
    /// strongest band — so the *color* moves with the music's balance while brightness pulses with its
    /// energy. Null when Levels are unavailable.
    /// </summary>
    public Color? LevelsHue
    {
        get
        {
            if (!(Levels is { } levels))
            {
                return null;
            }

            var low = Mathf.Clamp01(levels.low);
            var mid = Mathf.Clamp01(levels.mid);
            var high = Mathf.Clamp01(levels.high);
            var total = low + mid + high;
            if (total <= 0f)
            {
                return Color.black;
            }

            var hue = ((mid * (1f / 3f)) + (high * (2f / 3f))) / total;
            var strongest = Mathf.Max(low, Mathf.Max(mid, high));
            var weakest = Mathf.Min(low, Mathf.Min(mid, high));
            var saturation = strongest > 0f ? (strongest - weakest) / strongest : 0f;
            return Color.HSVToRGB(hue, saturation, strongest);
        }
    }

    /// <summary>
    /// Color Bank, palette-mediated form: the live AnimPalette read at the bands' spectral centroid and
    /// scaled by the strongest band, so the color stays cohesive with the wall's current palette while
    /// pulsing with the music. Null when Levels are unavailable or no live Controller owns a palette.
    /// </summary>
    public Color? LevelsPalette
    {
        get
        {
            if (!(Levels is { } levels))
            {
                return null;
            }

            // Guard before touching EffectBase.APalette: its static initializer constructs an AnimPalette,
            // which reads Controller.Instance — and Controller.Instance SPAWNS a Controller when none exists
            // (and NullReferences in edit mode, where AddComponent never runs Awake).
            if (!Application.isPlaying || !Controller.HasInstance)
            {
                return null;
            }

            return ReadLevelsPaletteColor(levels);
        }
    }

    /// <summary>
    /// Reads the palette-mediated Color Bank value. Isolated in its own non-inlined method because
    /// EffectBase is beforefieldinit: JIT-compiling any method that references <see cref="EffectBase.APalette"/>
    /// may run EffectBase's static initializer immediately, bypassing the caller's runtime guards. Keeping the
    /// reference here defers that static initialization until the guards in <see cref="LevelsPalette"/> have
    /// actually passed.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Color? ReadLevelsPaletteColor(LevelsInfo levels)
    {
        var palette = EffectBase.APalette;
        if (palette == null)
        {
            return null;
        }

        var low = Mathf.Clamp01(levels.low);
        var mid = Mathf.Clamp01(levels.mid);
        var high = Mathf.Clamp01(levels.high);
        var total = low + mid + high;
        var centroid = total <= 0f ? 0f : ((mid * 0.5f) + high) / total;
        var strongest = Mathf.Max(low, Mathf.Max(mid, high));
        var color = palette.read(centroid);
        return new Color(color.r * strongest, color.g * strongest, color.b * strongest, color.a);
    }

    /// <summary>
    /// Advances the Levels attack/release smoothing for this frame. Called from <see cref="Update(float)"/>
    /// after <see cref="beatData"/> has settled, in both the live and simulated paths.
    /// </summary>
    private void UpdateLevelsSmoothing(float timeSeconds)
    {
        var deltaSeconds = hasSmoothingClock ? Mathf.Max(0f, timeSeconds - lastSmoothingTime) : 0f;
        lastSmoothingTime = timeSeconds;
        hasSmoothingClock = true;

        var raw = beatData?.levels ?? PenroseArt.RaveOsc.Levels.Unavailable;
        if (raw.low < 0f || raw.mid < 0f || raw.high < 0f)
        {
            // Unavailable: drop the smoothing state entirely so the next live sample snaps in fresh
            // instead of releasing from stale values.
            hasSmoothedLevels = false;
            return;
        }

        if (!hasSmoothedLevels)
        {
            smoothedLow = Mathf.Clamp01(raw.low);
            smoothedMid = Mathf.Clamp01(raw.mid);
            smoothedHigh = Mathf.Clamp01(raw.high);
            hasSmoothedLevels = true;
            return;
        }

        smoothedLow = SmoothTowards(smoothedLow, Mathf.Clamp01(raw.low), deltaSeconds);
        smoothedMid = SmoothTowards(smoothedMid, Mathf.Clamp01(raw.mid), deltaSeconds);
        smoothedHigh = SmoothTowards(smoothedHigh, Mathf.Clamp01(raw.high), deltaSeconds);
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

    /// <summary>Beats until an upcoming phrase event starts: null while in progress or when unknown.</summary>
    private static int? CookBeatsUntilStart(bool inProgress, int countBeats)
    {
        return !inProgress && countBeats >= 0 ? countBeats : (int?)null;
    }

    /// <summary>
    /// Progress through an in-progress phrase event in [0..1], smoothed with the intra-beat fraction of
    /// the shared beat clock so it sweeps instead of stepping once per beat. 0 when the wire did not
    /// supply usable length/countdown data.
    /// </summary>
    private float CookPhraseProgress(int beatsToBoundary, int lengthBeats)
    {
        if (lengthBeats <= 0 || beatsToBoundary < 0 || beatsToBoundary > lengthBeats)
        {
            return 0f;
        }

        var elapsedBeats = (lengthBeats - beatsToBoundary) + IntraBeatFraction();
        return Mathf.Clamp01(elapsedBeats / lengthBeats);
    }

    /// <summary>Maps the transport's -1 "unknown" sentinel to null; passes real non-negative values through.</summary>
    private static int? NonNegativeOrNull(int value)
    {
        return value >= 0 ? value : (int?)null;
    }

    /// <summary>
    /// Parses a wire energy label against the closed Low/Mid/High vocabulary, case-insensitively.
    /// An unrecognized label fails the parse rather than degrading to a wrong tier.
    /// </summary>
    private static bool TryParseEnergyLevel(string? label, out EnergyLevel level)
    {
        if (string.Equals(label, "Low", StringComparison.OrdinalIgnoreCase))
        {
            level = EnergyLevel.Low;
            return true;
        }

        if (string.Equals(label, "Mid", StringComparison.OrdinalIgnoreCase))
        {
            level = EnergyLevel.Mid;
            return true;
        }

        if (string.Equals(label, "High", StringComparison.OrdinalIgnoreCase))
        {
            level = EnergyLevel.High;
            return true;
        }

        level = default;
        return false;
    }
}
