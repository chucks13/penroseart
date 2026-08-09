using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a screen-space metaball field and maps it onto Penrose tiles.
/// </summary>
[EffectSyncSettings(typeof(MetaBallsSyncSettingsAsset))]
public class MetaBalls : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored metaball field radius for the unchanged Standalone look.</summary>
    private const float StandaloneRadius = 1f;

    /// <summary>Authored minimum pre-division velocity for each Ball axis in Standalone Mode.</summary>
    private const float StandaloneBallVelocityMin = -1f;

    /// <summary>Authored maximum pre-division velocity for each Ball axis in Standalone Mode.</summary>
    private const float StandaloneBallVelocityMax = 1f;

    /// <summary>Authored divisor applied to each two-axis Ball velocity Roll in Standalone Mode.</summary>
    private const float StandaloneBallVelocityDivisor = 60f;

    /// <summary>Authored inclusive minimum for the Standalone beat-mode Roll.</summary>
    /// <remarks>
    /// Preserves the authored note: "Randomize logic was commented out in original class."
    /// The active beat-mode Roll nevertheless existed and remains unchanged.
    /// </remarks>
    private const int StandaloneBeatModeMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Standalone beat-mode Roll.</summary>
    private const int StandaloneBeatModeMaxExclusive = 3;

    /// <summary>Authored horizontal bounce margin for the unchanged Standalone look.</summary>
    private const float StandaloneHorizontalBounceMargin = 5f;

    /// <summary>Authored vertical bounce margin for the unchanged Standalone look.</summary>
    private const float StandaloneVerticalBounceMargin = 2f;

    /// <summary>
    /// Authored brightness supplied to Waveform.Lerp's peak and Standalone-fallback slot.
    /// Without a live Bar Phase, Waveform.Lerp returns this value steadily.
    /// </summary>
    private const float StandaloneBeatBrightness = 0.75f;

    // Sync Defaults

    /// <summary>Authored metaball field radius in Synced Mode.</summary>
    private const float SyncRadius = 1f;

    /// <summary>Authored minimum pre-division velocity for each Ball axis in Synced Mode.</summary>
    private const float SyncBallVelocityMin = -1f;

    /// <summary>Authored maximum pre-division velocity for each Ball axis in Synced Mode.</summary>
    private const float SyncBallVelocityMax = 1f;

    /// <summary>Authored divisor applied to each two-axis Ball velocity Roll in Synced Mode.</summary>
    private const float SyncBallVelocityDivisor = 60f;

    /// <summary>Authored inclusive minimum for the Synced beat-mode Roll.</summary>
    private const int SyncBeatModeMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Synced beat-mode Roll.</summary>
    private const int SyncBeatModeMaxExclusive = 3;

    /// <summary>Authored horizontal bounce margin in Synced Mode.</summary>
    private const float SyncHorizontalBounceMargin = 5f;

    /// <summary>Authored vertical bounce margin in Synced Mode.</summary>
    private const float SyncVerticalBounceMargin = 2f;

    /// <summary>Authored brightness reached at a Waveform trough in Synced Mode.</summary>
    private const float SyncBeatBrightnessAtTrough = 1f;

    /// <summary>
    /// Authored brightness reached at a Waveform peak in Synced Mode; the peak slot is also
    /// Waveform.Lerp's Standalone fallback, so its fixed Standalone twin lives above.
    /// </summary>
    private const float SyncBeatBrightnessAtPeak = 0.75f;

    /// <summary>Authored maximum hue shift contributed by the Waveform in Synced Mode.</summary>
    private const float SyncBeatHueShift = 0.5f;

    /// <summary>Authored Waveform contribution added to the local frame delta in Synced Mode.</summary>
    private const float SyncRhythmDeltaBoost = 0.05f;

    /// <summary>Authored saturation used by the black-and-white Fill branch in Synced Mode.</summary>
    private const float SyncFillSaturation = 0f;

    /// <summary>
    /// Authored number of beats used by the inherited Drop slowdown. This was the call's implicit default
    /// before capture.
    /// </summary>
    private const int SyncDropSlowdownBeats = 8;

    /// <summary>MetaBalls' soft blobs suit Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
       Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh immutable-by-convention copy of MetaBalls' Standalone Defaults.</summary>
    public static MetaBallsStandaloneSettings StandaloneSettings => new MetaBallsStandaloneSettings(
        StandaloneRadius,
        new FloatRange(StandaloneBallVelocityMin, StandaloneBallVelocityMax),
        StandaloneBallVelocityDivisor,
        StandaloneBeatModeMinInclusive,
        StandaloneBeatModeMaxExclusive,
        StandaloneHorizontalBounceMargin,
        StandaloneVerticalBounceMargin,
        StandaloneBeatBrightness);

    /// <summary>Resolves a fresh copy of MetaBalls' file-local Sync Defaults.</summary>
    public static MetaBallsSyncSettings SyncDefaults => new MetaBallsSyncSettings
    {
        Radius = SyncRadius,
        BallVelocityMin = SyncBallVelocityMin,
        BallVelocityMax = SyncBallVelocityMax,
        BallVelocityDivisor = SyncBallVelocityDivisor,
        BeatModeMinInclusive = SyncBeatModeMinInclusive,
        BeatModeMaxExclusive = SyncBeatModeMaxExclusive,
        HorizontalBounceMargin = SyncHorizontalBounceMargin,
        VerticalBounceMargin = SyncVerticalBounceMargin,
        BeatBrightnessAtTrough = SyncBeatBrightnessAtTrough,
        BeatBrightnessAtPeak = SyncBeatBrightnessAtPeak,
        BeatHueShift = SyncBeatHueShift,
        RhythmDeltaBoost = SyncRhythmDeltaBoost,
        FillSaturation = SyncFillSaturation,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>Fixed number of Ball instances in the metaball simulation and its Roll.</summary>
    private const int BallCount = 8;

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private MetaBallsStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private MetaBallsSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The eight moving Ball sources in the current activation.</summary>
    private Ball[] balls;

    /// <summary>Reusable screen-space coordinate sampled by the field renderer.</summary>
    private Vector2 screen;

    /// <summary>The rolled numeric mode controlling rhythm motion and color response.</summary>
    private int beatMode;

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() { return $""; }

    /// <summary>
    /// Resolves Effect Settings and initializes per-activation random state before drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(MetaBalls),
            SyncDefaults);

        waveform = waveforms.Random();
        int beatModeMinInclusive = beatManager.IsSynced
            ? SyncSettings.BeatModeMinInclusive
            : standaloneSettings.BeatModeMinInclusive;
        int beatModeMaxExclusive = beatManager.IsSynced
            ? SyncSettings.BeatModeMaxExclusive
            : standaloneSettings.BeatModeMaxExclusive;
        beatMode = Random.Range(beatModeMinInclusive, beatModeMaxExclusive);

        float ballVelocityMin = beatManager.IsSynced
            ? SyncSettings.BallVelocityMin
            : standaloneSettings.BallVelocity.Min;
        float ballVelocityMax = beatManager.IsSynced
            ? SyncSettings.BallVelocityMax
            : standaloneSettings.BallVelocity.Max;
        float ballVelocityDivisor = beatManager.IsSynced
            ? SyncSettings.BallVelocityDivisor
            : standaloneSettings.BallVelocityDivisor;

        balls = new Ball[BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i] = new Ball(ballVelocityMin, ballVelocityMax, ballVelocityDivisor);
        }
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // This Effect owns its brightness, hue, time-warp, and clockless fallback mappings.
        float rhythm = waveform.Envelope;
        float beatBrightness = waveform.Lerp(
            SyncSettings.BeatBrightnessAtTrough,
            beatManager.IsSynced ? SyncSettings.BeatBrightnessAtPeak : standaloneSettings.BeatBrightness);
        float beatHue = SyncSettings.BeatHueShift * rhythm;
        float localDelta = DropSlowdown(
            beatMode < 2 ? effectDelta + (SyncSettings.RhythmDeltaBoost * rhythm) : effectDelta,
            SyncSettings.DropSlowdownBeats);
        float radius = beatManager.IsSynced ? SyncSettings.Radius : standaloneSettings.Radius;
        float horizontalBounceMargin = beatManager.IsSynced
            ? SyncSettings.HorizontalBounceMargin
            : standaloneSettings.HorizontalBounceMargin;
        float verticalBounceMargin = beatManager.IsSynced
            ? SyncSettings.VerticalBounceMargin
            : standaloneSettings.VerticalBounceMargin;


        buffer.Fade();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                screen.x = x;
                screen.y = y;
                var idx = x + (y * width);
                var sum = 0f;
                for (int i = 0; i < balls.Length; i++)
                {
                    balls[i].Update(localDelta, horizontalBounceMargin, verticalBounceMargin);
                    var d = Vector2.Distance(screen, balls[i].Position);
                    sum += radius / d;
                }

                sum = sum.Clamp();
                Color color = APalette.read(sum, true);

                if (beatMode > 0)
                {
                    Color.RGBToHSV(color, out float h, out float s, out float v);
                    h += beatHue;
                    v *= beatBrightness;
                    color = Color.HSVToRGB(h % 1f, s, v);
                }

                if (beatManager.Fill.Active)            // blak and whire on fill
                {
                    float h, s, v_col;
                    Color.RGBToHSV(color, out h, out s, out v_col);
                    v_col = (h + s + v_col) % 1f;                   // assure there is brightness variation
                    s = SyncSettings.FillSaturation;
                    color = Color.HSVToRGB(h, s, v_col);
                }
                screenBuffer[idx] = color;
            }
        }

        ConvertScreenBuffer(ref screenBuffer, in buffer);
    }

    /// <summary>
    /// Moving screen-space metaball source.
    /// </summary>
    public class Ball
    {
        /// <summary>Current screen-space center of this Ball.</summary>
        private Vector2 position;

        /// <summary>Rolled screen-space velocity retained for this activation.</summary>
        private Vector2 velocity;

        /// <summary>
        /// Creates one moving metaball source from the selected Effect Settings at a random screen position.
        /// </summary>
        /// <remarks>
        /// The two float velocity Rolls still precede the two integer position Rolls, preserving the original
        /// Random call order, overloads, and rendered distribution.
        /// </remarks>
        /// <param name="velocityMin">Minimum pre-division velocity for each axis.</param>
        /// <param name="velocityMax">Maximum pre-division velocity for each axis.</param>
        /// <param name="velocityDivisor">Divisor applied after both axis velocities are rolled.</param>
        public Ball(float velocityMin, float velocityMax, float velocityDivisor)
        {
            velocity = new Vector2(
                Random.Range(velocityMin, velocityMax),
                Random.Range(velocityMin, velocityMax)) / velocityDivisor;
            position = new Vector2(Random.Range(0, width), Random.Range(0, height));
        }

        /// <summary>Current screen-space metaball center.</summary>
        public Vector2 Position => position;

        /// <summary>
        /// Advances metaball position and bounces it inside the screen bounds.
        /// </summary>
        /// <remarks>
        /// The caller intentionally invokes this inside the per-pixel loop, so motion retains its original
        /// dependence on the screen's pixel count.
        /// </remarks>
        /// <param name="time">Local delta applied to the retained velocity.</param>
        /// <param name="horizontalBounceMargin">Distance from either horizontal edge that reverses velocity.</param>
        /// <param name="verticalBounceMargin">Distance from either vertical edge that reverses velocity.</param>
        public void Update(float time, float horizontalBounceMargin, float verticalBounceMargin)
        {
            position += time * velocity;
            if (position.x < horizontalBounceMargin || position.x > width - horizontalBounceMargin) velocity.x *= -1;
            if (position.y < verticalBounceMargin || position.y > height - verticalBounceMargin) velocity.y *= -1;
        }
    }
}

/// <summary>The non-editable Standalone Settings that reproduce MetaBalls' authored no-music look.</summary>
public sealed class MetaBallsStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from MetaBalls' file-local defaults.</summary>
    /// <param name="radius">Metaball field radius.</param>
    /// <param name="ballVelocity">Per-axis pre-division velocity range.</param>
    /// <param name="ballVelocityDivisor">Divisor applied to each rolled two-axis velocity.</param>
    /// <param name="beatModeMinInclusive">Inclusive minimum for the beat-mode Roll.</param>
    /// <param name="beatModeMaxExclusive">Exclusive maximum for the beat-mode Roll.</param>
    /// <param name="horizontalBounceMargin">Horizontal screen-edge bounce margin.</param>
    /// <param name="verticalBounceMargin">Vertical screen-edge bounce margin.</param>
    /// <param name="beatBrightness">Brightness returned when no live Waveform placement exists.</param>
    public MetaBallsStandaloneSettings(
        float radius,
        FloatRange ballVelocity,
        float ballVelocityDivisor,
        int beatModeMinInclusive,
        int beatModeMaxExclusive,
        float horizontalBounceMargin,
        float verticalBounceMargin,
        float beatBrightness)
    {
        Radius = radius;
        BallVelocity = ballVelocity;
        BallVelocityDivisor = ballVelocityDivisor;
        BeatModeMinInclusive = beatModeMinInclusive;
        BeatModeMaxExclusive = beatModeMaxExclusive;
        HorizontalBounceMargin = horizontalBounceMargin;
        VerticalBounceMargin = verticalBounceMargin;
        BeatBrightness = beatBrightness;
    }

    /// <summary>Metaball field radius used in Standalone Mode.</summary>
    public float Radius;

    /// <summary>Per-axis pre-division velocity range used by each Ball Roll.</summary>
    public FloatRange BallVelocity;

    /// <summary>Divisor applied to each rolled two-axis Ball velocity.</summary>
    public float BallVelocityDivisor;

    /// <summary>Inclusive minimum for the per-activation beat-mode Roll.</summary>
    public int BeatModeMinInclusive;

    /// <summary>Exclusive maximum for the per-activation beat-mode Roll.</summary>
    public int BeatModeMaxExclusive;

    /// <summary>Distance from either horizontal screen edge that reverses Ball velocity.</summary>
    public float HorizontalBounceMargin;

    /// <summary>Distance from either vertical screen edge that reverses Ball velocity.</summary>
    public float VerticalBounceMargin;

    /// <summary>Brightness used when no live Waveform placement exists.</summary>
    public float BeatBrightness;
}

/// <summary>Editable music-response and Synced rendering values saved as MetaBalls' Sync Settings.</summary>
[Serializable]
public sealed class MetaBallsSyncSettings
{
    /// <summary>Metaball field radius used in Synced Mode.</summary>
    [Min(0f)] public float Radius;

    /// <summary>Minimum pre-division velocity for each Ball axis.</summary>
    public float BallVelocityMin;

    /// <summary>Maximum pre-division velocity for each Ball axis.</summary>
    public float BallVelocityMax;

    /// <summary>Divisor applied to each rolled two-axis Ball velocity.</summary>
    [Min(0.0001f)] public float BallVelocityDivisor;

    /// <summary>Inclusive minimum for the per-activation beat-mode Roll.</summary>
    [Range(0, 2)] public int BeatModeMinInclusive;

    /// <summary>Exclusive maximum for the per-activation beat-mode Roll; three numeric modes exist.</summary>
    [Range(1, 3)] public int BeatModeMaxExclusive;

    /// <summary>Distance from either horizontal screen edge that reverses Ball velocity.</summary>
    [Min(0f)] public float HorizontalBounceMargin;

    /// <summary>Distance from either vertical screen edge that reverses Ball velocity.</summary>
    [Min(0f)] public float VerticalBounceMargin;

    /// <summary>Brightness reached at a Waveform trough.</summary>
    [Range(0f, 1f)] public float BeatBrightnessAtTrough;

    /// <summary>Brightness reached at a Waveform peak.</summary>
    [Range(0f, 1f)] public float BeatBrightnessAtPeak;

    /// <summary>Maximum hue shift contributed by the Waveform.</summary>
    [Range(0f, 1f)] public float BeatHueShift;

    /// <summary>Waveform contribution added to the local frame delta.</summary>
    [Min(0f)] public float RhythmDeltaBoost;

    /// <summary>Saturation used while the Fill branch renders black and white.</summary>
    [Range(0f, 1f)] public float FillSaturation;

    /// <summary>Number of beats used by the inherited Drop slowdown.</summary>
    [Min(1)] public int DropSlowdownBeats;

    /// <summary>Copies every MetaBalls Sync Setting from another value.</summary>
    /// <param name="source">Sync Settings value to copy.</param>
    public void CopyFrom(MetaBallsSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Radius = source.Radius;
        BallVelocityMin = source.BallVelocityMin;
        BallVelocityMax = source.BallVelocityMax;
        BallVelocityDivisor = source.BallVelocityDivisor;
        BeatModeMinInclusive = source.BeatModeMinInclusive;
        BeatModeMaxExclusive = source.BeatModeMaxExclusive;
        HorizontalBounceMargin = source.HorizontalBounceMargin;
        VerticalBounceMargin = source.VerticalBounceMargin;
        BeatBrightnessAtTrough = source.BeatBrightnessAtTrough;
        BeatBrightnessAtPeak = source.BeatBrightnessAtPeak;
        BeatHueShift = source.BeatHueShift;
        RhythmDeltaBoost = source.RhythmDeltaBoost;
        FillSaturation = source.FillSaturation;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
