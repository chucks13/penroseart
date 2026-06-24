// Contrived rhythm query layer for BeatManager (ADR-0002: nullable contrived rhythm queries).

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
/// Contrived phrase-event state shared by Fill and Drop: an event effects can anticipate before it
/// starts and ride while it plays. <see cref="BeatManager.Fill"/> and <see cref="BeatManager.Drop"/>
/// each return their own instance of this shape.
/// </summary>
/// <remarks>
/// Null from the query means no data is on the wire right now (nothing on air, no track analysis, or
/// the track contains none of these events at all). Within a non-null value, null fields are ordinary
/// musical state — "no start is known" — and 0 is just a number: <c>remaining == 0</c> means the
/// track's events are all behind the playhead. <see cref="inProgress"/> is the only state flag;
/// everything else is values.
/// </remarks>
public readonly struct PhraseEventInfo
{
    /// <summary>True while the event is playing now; false otherwise.</summary>
    public readonly bool inProgress;

    /// <summary>Whole beats until the event starts. Null while in progress, or when no next start is known.</summary>
    public readonly int? beatsUntilStart;

    /// <summary>
    /// Milliseconds until the event starts, contrived as <see cref="beatsUntilStart"/> × the average
    /// beat interval (beat-quantized, not sub-beat smooth). Null when either side is unavailable.
    /// </summary>
    public readonly int? msUntilStart;

    /// <summary>Whole beats until the event ends. Null unless in progress.</summary>
    public readonly int? beatsUntilEnd;

    /// <summary>Beat-smoothed progress through the event in [0..1]. Null unless in progress.</summary>
    public readonly float? progress;

    /// <summary>
    /// Beat-smoothed countdown ramp that fills 0→1 as the start approaches over the last
    /// <see cref="BeatManager.AnticipationWindowBeats"/> beats (counting up to something fills).
    /// Null while in progress, outside the window, or when no next start is known.
    /// </summary>
    public readonly float? anticipation;

    /// <summary>Length in beats of the current event (while in progress) or the next one. Null when the wire did not say.</summary>
    public readonly int? lengthBeats;

    /// <summary>Remaining occurrences in this track. 0 means none left; null when the wire did not say.</summary>
    public readonly int? remaining;

    public PhraseEventInfo(bool inProgress, int? beatsUntilStart, int? msUntilStart, int? beatsUntilEnd,
        float? progress, float? anticipation, int? lengthBeats, int? remaining)
    {
        this.inProgress = inProgress;
        this.beatsUntilStart = beatsUntilStart;
        this.msUntilStart = msUntilStart;
        this.beatsUntilEnd = beatsUntilEnd;
        this.progress = progress;
        this.anticipation = anticipation;
        this.lengthBeats = lengthBeats;
        this.remaining = remaining;
    }
}

/// <summary>
/// Contrived phrase Energy: the track's current intensity tier parsed once into the closed
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

    /// <summary>Beat-smoothed progress through the current same-energy run in [0..1]. Null when the run shape is unknown.</summary>
    public readonly float? runProgress;

    /// <summary>Total length in beats of the current same-energy run. Null when the wire did not say.</summary>
    public readonly int? runLengthBeats;

    /// <summary>Energy changes still ahead in this track. 0 means none left; null when the wire did not say.</summary>
    public readonly int? changesRemaining;

    public EnergyInfo(EnergyLevel level, EnergyLevel? next, int? beatsUntilChange, float normalized, int direction,
        float? runProgress, int? runLengthBeats, int? changesRemaining)
    {
        this.level = level;
        this.next = next;
        this.beatsUntilChange = beatsUntilChange;
        this.normalized = normalized;
        this.direction = direction;
        this.runProgress = runProgress;
        this.runLengthBeats = runLengthBeats;
        this.changesRemaining = changesRemaining;
    }
}

/// <summary>
/// Contrived Track Phrase: open-vocabulary section labels passed through untouched, with the countdown
/// structure contrived into usable numbers.
/// </summary>
/// <remarks>
/// Returned by <see cref="BeatManager.Phrase"/>; null there means no phrase data is available right now.
/// Labels are an open vocabulary ("Drop", "Break", "Chorus 2") — do not keyword-parse them as if the
/// set were closed; that is what <see cref="BeatManager.Energy"/> is for.
/// </remarks>
public readonly struct PhraseInfo
{
    /// <summary>The current phrase label as broadcast. Never null or empty for a non-null PhraseInfo.</summary>
    public readonly string label;

    /// <summary>The upcoming phrase label. Null when the wire did not say.</summary>
    public readonly string? next;

    /// <summary>True while the phrase state is active now (RaveSystem tri-state 1).</summary>
    public readonly bool inPhrase;

    /// <summary>Whole beats until the active phrase boundary or upcoming phrase start. Null when the wire did not say.</summary>
    public readonly int? beatsUntilNext;

    /// <summary>Total length of the active or upcoming phrase in beats. Null when the wire did not say.</summary>
    public readonly int? lengthBeats;

    /// <summary>Remaining phrase changes in this track. 0 means none left; null when the wire did not say.</summary>
    public readonly int? remaining;

    /// <summary>Beat-smoothed progress through the current phrase in [0..1]. Null when not in a phrase or the shape is unknown.</summary>
    public readonly float? progress;

    public PhraseInfo(string label, string? next, bool inPhrase, int? beatsUntilNext, int? lengthBeats, int? remaining, float? progress)
    {
        this.label = label;
        this.next = next;
        this.inPhrase = inPhrase;
        this.beatsUntilNext = beatsUntilNext;
        this.lengthBeats = lengthBeats;
        this.remaining = remaining;
        this.progress = progress;
    }
}

/// <summary>
/// Contrived Levels: normalized low/mid/high band energy with BeatManager's attack/release smoothing
/// already applied, so effects can drive the wall from them without re-implementing anti-flicker.
/// </summary>
/// <remarks>
/// Returned by <see cref="BeatManager.Levels"/>; null there means no live Levels are available
/// (Standalone, with no live OSC source, never supplies them).
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
/// The contrived rhythm query layer (ADR-0002). Effects and transitions pull all musical state through
/// these nullable queries: null always means "not available right now", and the caller owns its
/// Standalone Mode behavior (<c>?? standalone</c>) or Synced Mode branch (<c>is { } x</c>).
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

    // ---- Raw passthrough queries --------------------------------------------------------------
    // BeatData is transport-only and never read by effects; these expose its raw values through the
    // same nullable seam as the contrived queries, with wire sentinels mapped to null. Null is an
    // ordinary musical state ("this value isn't there right now"), never an error (ADR-0002).

    /// <summary>Focused on-air tempo in BPM, or null when no usable beat clock is present.</summary>
    public float? Bpm
    {
        get
        {
            if (!IsActive)
            {
                return null;
            }

            return beatData.snapshot.bpm;
        }
    }

    /// <summary>Focused on-air absolute beat count, or null when the source has not supplied it.</summary>
    public int? Beat
    {
        get
        {
            if (!IsActive || beatData.snapshot.beat.current < 1)
            {
                return null;
            }

            return beatData.snapshot.beat.current;
        }
    }

    /// <summary>Focused on-air track length in beats, or null when the source has not supplied it.</summary>
    public int? TotalBeats
    {
        get
        {
            if (!IsActive || beatData.snapshot.beat.total < 1)
            {
                return null;
            }

            return beatData.snapshot.beat.total;
        }
    }

    /// <summary>Focused on-air bar count, or null when the source has not supplied it.</summary>
    public int? Bar
    {
        get
        {
            if (!IsActive || beatData.snapshot.bar.current < 1)
            {
                return null;
            }

            return beatData.snapshot.bar.current;
        }
    }

    /// <summary>Raw beat pulse — 1 on the beat decaying toward 0 — or null when no beat clock is present.</summary>
    public float? Pulse
    {
        get
        {
            if (!IsActive)
            {
                return null;
            }

            return beatData.snapshot.beatPulse;
        }
    }

    /// <summary>Derived offbeat pulse — 1 on the offbeat decaying toward 0 — or null when no beat clock is present.</summary>
    public float? OffBeatPulse
    {
        get
        {
            if (!IsActive)
            {
                return null;
            }

            return offBeatPulse;
        }
    }

    /// <summary>Fraction elapsed through the current beat in [0..1], or null when no usable beat clock is present.</summary>
    public float? BeatFraction
    {
        get
        {
            if (!IsActive)
            {
                return null;
            }

            return IntraBeatFraction();
        }
    }

    /// <summary>Musical 1-based beat label inside the current bar, or null when the label is unknown.</summary>
    public int? BeatInBar
    {
        get
        {
            if (!IsActive || beatData.snapshot.beatInBar < 1 || beatData.snapshot.beatInBar > BeatSlotCount)
            {
                return null;
            }

            return beatData.snapshot.beatInBar;
        }
    }

    /// <summary>Milliseconds until the nearest upcoming beat label, or null when unavailable.</summary>
    public int? NextBeatMs
    {
        get
        {
            if (!IsActive)
            {
                return null;
            }

            return NonNegativeOrNull(beatData.nextBeatMs);
        }
    }

    /// <summary>Milliseconds until the nearest upcoming offbeat, or null when unavailable.</summary>
    public int? NextOffBeatMs
    {
        get
        {
            if (!IsActive)
            {
                return null;
            }

            return NonNegativeOrNull(BeatData.ReadIntAt(offBeatsCountMs, BeatData.IndexOfNearestCountdown(offBeatsCountMs)));
        }
    }

    /// <summary>True while the nearest upcoming beat label gate is open, or null when no beat clock is present.</summary>
    public bool? OnBeat
    {
        get
        {
            if (!IsActive)
            {
                return null;
            }

            return beatData.onBeat;
        }
    }

    /// <summary>True while the nearest upcoming offbeat gate is open, or null when no beat clock is present.</summary>
    public bool? OffBeat
    {
        get
        {
            if (!IsActive)
            {
                return null;
            }

            return BeatData.ReadBoolAt(offBeats, BeatData.IndexOfNearestCountdown(offBeatsCountMs));
        }
    }

    /// <summary>Focused on-air track text, or null when nothing usable is on air.</summary>
    public string? Track
    {
        get
        {
            if (beatData == null || string.IsNullOrEmpty(beatData.snapshot.track))
            {
                return null;
            }

            return beatData.snapshot.track;
        }
    }

    /// <summary>CSV of live on-air player numbers (newest first), or null when none are live.</summary>
    public string? PlayersLive
    {
        get
        {
            if (beatData == null || string.IsNullOrEmpty(beatData.snapshot.playersLive))
            {
                return null;
            }

            return beatData.snapshot.playersLive;
        }
    }

    /// <summary>Contrived Fill event, or null when no Fill data is available right now.</summary>
    public PhraseEventInfo? Fill => beatData != null ? ContrivePhraseEvent(beatData.snapshot.fillState) : null;

    /// <summary>Contrived Drop event, or null when no Drop data is available right now.</summary>
    public PhraseEventInfo? Drop => beatData != null ? ContrivePhraseEvent(beatData.snapshot.dropState) : null;

    /// <summary>
    /// Contrived phrase Energy, or null when Energy is unavailable or the wire label is outside the
    /// Low/Mid/High vocabulary.
    /// </summary>
    public EnergyInfo? Energy
    {
        get
        {
            var data = beatData;
            if (data == null || data.snapshot.energyState.active < 0)
            {
                return null;
            }

            var state = data.snapshot.energyState;
            if (!TryParseEnergyLevel(state.current, out var level))
            {
                return null;
            }

            EnergyLevel? next = TryParseEnergyLevel(state.next, out var nextLevel) ? nextLevel : (EnergyLevel?)null;
            var direction = next is { } heading ? Math.Sign((int)heading - (int)level) : 0;
            var inRun = state.active > 0;
            return new EnergyInfo(
                level,
                next,
                NonNegativeOrNull(state.countBeats),
                (int)level * 0.5f,
                direction,
                inRun ? ContriveProgressOrNull(state.countBeats, state.lengthBeats) : (float?)null,
                NonNegativeOrNull(state.lengthBeats),
                NonNegativeOrNull(state.remaining));
        }
    }

    /// <summary>Contrived Track Phrase, or null when no phrase data is available right now.</summary>
    public PhraseInfo? Phrase
    {
        get
        {
            var data = beatData;
            if (data == null || data.snapshot.phraseState.active < 0 || string.IsNullOrEmpty(data.snapshot.phraseState.current))
            {
                return null;
            }

            var state = data.snapshot.phraseState;
            var inPhrase = state.active > 0;
            return new PhraseInfo(
                state.current!,
                string.IsNullOrEmpty(state.next) ? null : state.next,
                inPhrase,
                NonNegativeOrNull(state.countBeats),
                NonNegativeOrNull(state.lengthBeats),
                NonNegativeOrNull(state.remaining),
                inPhrase ? ContriveProgressOrNull(state.countBeats, state.lengthBeats) : (float?)null);
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
    /// after <see cref="beatData"/> has settled, for both live OSC and Standalone (no beat).
    /// </summary>
    private void UpdateLevelsSmoothing(float timeSeconds)
    {
        var deltaSeconds = hasSmoothingClock ? Mathf.Max(0f, timeSeconds - lastSmoothingTime) : 0f;
        lastSmoothingTime = timeSeconds;
        hasSmoothingClock = true;

        var raw = beatData?.snapshot.levels ?? PenroseArt.RaveOsc.Levels.Unavailable;
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

    /// <summary>How many beats out the anticipation ramp starts rising. 32 beats = 8 bars at 4/4.</summary>
    public const int AnticipationWindowBeats = 32;

    /// <summary>
    /// Contrives the shared Fill/Drop shape from one wire countdown state. Null when the wire says the
    /// state is unavailable (tri-state -1); otherwise every field is derived honestly — countdown fields
    /// null while in progress, progress fields null while upcoming, sentinels mapped to null once here.
    /// </summary>
    private PhraseEventInfo? ContrivePhraseEvent(CountdownState state)
    {
        if (state.active < 0)
        {
            return null;
        }

        var inProgress = state.active > 0;
        var beatsUntilStart = inProgress ? (int?)null : NonNegativeOrNull(state.countBeats);
        return new PhraseEventInfo(
            inProgress,
            beatsUntilStart,
            ContriveMsForBeats(beatsUntilStart),
            inProgress ? NonNegativeOrNull(state.countBeats) : (int?)null,
            inProgress ? ContriveProgressOrNull(state.countBeats, state.lengthBeats) : (float?)null,
            inProgress ? (float?)null : ContriveAnticipation(beatsUntilStart),
            NonNegativeOrNull(state.lengthBeats),
            NonNegativeOrNull(state.remaining));
    }

    /// <summary>
    /// Progress toward a phrase boundary in [0..1], smoothed with the intra-beat fraction of the shared
    /// beat clock so it sweeps instead of stepping once per beat. Null when the wire did not supply
    /// usable length/countdown data.
    /// </summary>
    private float? ContriveProgressOrNull(int beatsToBoundary, int lengthBeats)
    {
        if (lengthBeats <= 0 || beatsToBoundary < 0 || beatsToBoundary > lengthBeats)
        {
            return null;
        }

        var elapsedBeats = (lengthBeats - beatsToBoundary) + IntraBeatFraction();
        return Mathf.Clamp01(elapsedBeats / lengthBeats);
    }

    /// <summary>
    /// Countdown ramp that fills 0→1 as a phrase event's start approaches over the last
    /// <see cref="AnticipationWindowBeats"/> beats, smoothed with the intra-beat fraction.
    /// Null when no start is known or it is still outside the window.
    /// </summary>
    private float? ContriveAnticipation(int? beatsUntilStart)
    {
        if (beatsUntilStart is not { } beats || beats > AnticipationWindowBeats)
        {
            return null;
        }

        var remainingBeats = beats - IntraBeatFraction();
        return Mathf.Clamp01((AnticipationWindowBeats - remainingBeats) / AnticipationWindowBeats);
    }

    /// <summary>
    /// Converts a whole-beat countdown to milliseconds via the wire's average beat interval, as the OSC
    /// schema delegates to clients. Null when either side is unavailable.
    /// </summary>
    private int? ContriveMsForBeats(int? beats)
    {
        if (beats is not { } wholeBeats || beatData == null || beatData.snapshot.beatAverageMs <= 0)
        {
            return null;
        }

        return wholeBeats * beatData.snapshot.beatAverageMs;
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
