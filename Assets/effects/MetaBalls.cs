using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a screen-space metaball field and maps it onto Penrose tiles.
/// </summary>
[EffectSyncSettings(typeof(MetaBallsSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(MetaBallsStandaloneSettingsAsset))]
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

    /// <summary>Authored inclusive minimum for the Standalone Waveform-response-mode Roll.</summary>
    /// <remarks>
    /// Preserves the authored note: "Randomize logic was commented out in original class."
    /// The active Waveform-response-mode Roll nevertheless existed and remains unchanged.
    /// </remarks>
    private const int StandaloneWaveformResponseModeMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Standalone Waveform-response-mode Roll.</summary>
    private const int StandaloneWaveformResponseModeMaxExclusive = 3;

    /// <summary>Authored horizontal bounce margin for the unchanged Standalone look.</summary>
    private const float StandaloneHorizontalBounceMargin = 5f;

    /// <summary>Authored vertical bounce margin for the unchanged Standalone look.</summary>
    private const float StandaloneVerticalBounceMargin = 2f;

    /// <summary>
    /// Authored brightness supplied to Waveform.Lerp's peak and Standalone-fallback slot.
    /// Without a live Bar Phase, Waveform.Lerp returns this value steadily.
    /// </summary>
    private const float StandaloneWaveformBrightnessAtPeak = 0.75f;

    // Sync Defaults

    /// <summary>Authored metaball field radius in Synced Mode.</summary>
    private const float SyncRadius = 1f;

    /// <summary>Authored minimum pre-division velocity for each Ball axis in Synced Mode.</summary>
    private const float SyncBallVelocityMin = -1f;

    /// <summary>Authored maximum pre-division velocity for each Ball axis in Synced Mode.</summary>
    private const float SyncBallVelocityMax = 1f;

    /// <summary>Authored divisor applied to each two-axis Ball velocity Roll in Synced Mode.</summary>
    private const float SyncBallVelocityDivisor = 60f;

    /// <summary>Authored inclusive minimum for the Synced Waveform-response-mode Roll.</summary>
    private const int SyncWaveformResponseModeMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Synced Waveform-response-mode Roll.</summary>
    private const int SyncWaveformResponseModeMaxExclusive = 3;

    /// <summary>Authored horizontal bounce margin in Synced Mode.</summary>
    private const float SyncHorizontalBounceMargin = 5f;

    /// <summary>Authored vertical bounce margin in Synced Mode.</summary>
    private const float SyncVerticalBounceMargin = 2f;

    /// <summary>Authored brightness reached at a Waveform trough in Synced Mode.</summary>
    private const float SyncWaveformBrightnessAtTrough = 1f;

    /// <summary>
    /// Authored brightness reached at a Waveform peak in Synced Mode; the peak slot is also
    /// Waveform.Lerp's Standalone fallback, so its fixed Standalone twin lives above.
    /// </summary>
    private const float SyncWaveformBrightnessAtPeak = 0.75f;

    /// <summary>Authored maximum hue shift contributed by the Waveform in Synced Mode.</summary>
    private const float SyncWaveformHueShift = 0.5f;

    /// <summary>Authored Waveform contribution added to the local frame delta in Synced Mode.</summary>
    private const float SyncWaveformDeltaBoost = 0.05f;

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

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate MetaBalls' authored
    /// Standalone Defaults.
    /// </summary>
    public static MetaBallsStandaloneSettings StandaloneDefaults => new()
    {
        Radius = StandaloneRadius,
        BallVelocity = new FloatRange(StandaloneBallVelocityMin, StandaloneBallVelocityMax),
        BallVelocityDivisor = StandaloneBallVelocityDivisor,
        WaveformResponseMode = new IntRange(
            StandaloneWaveformResponseModeMinInclusive,
            StandaloneWaveformResponseModeMaxExclusive),
        HorizontalBounceMargin = StandaloneHorizontalBounceMargin,
        VerticalBounceMargin = StandaloneVerticalBounceMargin,
        WaveformBrightnessAtPeak = StandaloneWaveformBrightnessAtPeak,
    };

    /// <summary>Resolves a fresh copy of MetaBalls' file-local Sync Defaults.</summary>
    public static MetaBallsSyncSettings SyncDefaults => new MetaBallsSyncSettings
    {
        Radius = SyncRadius,
        BallVelocity = new FloatRange(SyncBallVelocityMin, SyncBallVelocityMax),
        BallVelocityDivisor = SyncBallVelocityDivisor,
        WaveformResponseMode = new IntRange(
            SyncWaveformResponseModeMinInclusive,
            SyncWaveformResponseModeMaxExclusive),
        HorizontalBounceMargin = SyncHorizontalBounceMargin,
        VerticalBounceMargin = SyncVerticalBounceMargin,
        WaveformBrightness = new FloatRange(
            SyncWaveformBrightnessAtPeak,
            SyncWaveformBrightnessAtTrough),
        WaveformHueShift = SyncWaveformHueShift,
        WaveformDeltaBoost = SyncWaveformDeltaBoost,
        FillSaturation = SyncFillSaturation,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>Fixed number of Ball instances in the metaball simulation and its Roll.</summary>
    private const int BallCount = 8;

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private MetaBallsStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private MetaBallsSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The eight moving Ball sources in the current activation.</summary>
    private Ball[] balls;

    /// <summary>Reusable screen-space coordinate sampled by the field renderer.</summary>
    private Vector2 screen;

    /// <summary>The rolled numeric mode controlling Waveform motion and color response.</summary>
    private int waveformResponseMode;

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
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(MetaBalls),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(MetaBalls),
            SyncDefaults);

        waveform = waveforms.Random();
        bool isSynced = beatManager.IsSynced;
        IntRange waveformResponseModeRange = isSynced
            ? SyncSettings.WaveformResponseMode
            : standaloneSettings.WaveformResponseMode;
        waveformResponseMode = Random.Range(
            waveformResponseModeRange.MinInclusive,
            waveformResponseModeRange.MaxExclusive);

        FloatRange ballVelocityRange = isSynced
            ? SyncSettings.BallVelocity
            : standaloneSettings.BallVelocity;
        float ballVelocityDivisor = isSynced
            ? SyncSettings.BallVelocityDivisor
            : standaloneSettings.BallVelocityDivisor;

        balls = new Ball[BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i] = new Ball(
                ballVelocityRange.Min,
                ballVelocityRange.Max,
                ballVelocityDivisor);
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
        float waveformBrightnessAtPeak = beatManager.IsSynced
            ? SyncSettings.WaveformBrightness.Min
            : standaloneSettings.WaveformBrightnessAtPeak;
        float waveformBrightness = waveform.Lerp(
            SyncSettings.WaveformBrightness.Max,
            waveformBrightnessAtPeak);
        float waveformHue = SyncSettings.WaveformHueShift * rhythm;
        float localDelta = DropSlowdown(
            waveformResponseMode < 2
                ? effectDelta + (SyncSettings.WaveformDeltaBoost * rhythm)
                : effectDelta,
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

                if (waveformResponseMode > 0)
                {
                    Color.RGBToHSV(color, out float h, out float s, out float v);
                    h += waveformHue;
                    v *= waveformBrightness;
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

/// <summary>
/// The serializable value shape shared by MetaBalls' fully populated Standalone Defaults and saved
/// Standalone Settings; Unity may create an empty instance before serialized values are applied.
/// </summary>
[Serializable]
public sealed class MetaBallsStandaloneSettings
{
    /// <summary>Metaball field radius used in Standalone Mode.</summary>
    public float Radius;

    /// <summary>Per-axis pre-division velocity range used by each Ball Roll.</summary>
    public FloatRange BallVelocity;

    /// <summary>Divisor applied to each rolled two-axis Ball velocity.</summary>
    public float BallVelocityDivisor;

    /// <summary>Per-activation range selecting the Waveform motion and color response.</summary>
    public IntRange WaveformResponseMode;

    /// <summary>Distance from either horizontal screen edge that reverses Ball velocity.</summary>
    public float HorizontalBounceMargin;

    /// <summary>Distance from either vertical screen edge that reverses Ball velocity.</summary>
    public float VerticalBounceMargin;

    /// <summary>Brightness used when no live Waveform placement exists.</summary>
    public float WaveformBrightnessAtPeak;

    /// <summary>Copies every MetaBalls Standalone Setting, including range endpoints and Rails.</summary>
    /// <param name="source">Standalone Settings value to copy.</param>
    public void CopyFrom(MetaBallsStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Radius = source.Radius;
        BallVelocity = new FloatRange(
            source.BallVelocity.Min,
            source.BallVelocity.Max,
            source.BallVelocity.LowRail,
            source.BallVelocity.HighRail);
        BallVelocityDivisor = source.BallVelocityDivisor;
        WaveformResponseMode = new IntRange(
            source.WaveformResponseMode.MinInclusive,
            source.WaveformResponseMode.MaxExclusive,
            source.WaveformResponseMode.LowRail,
            source.WaveformResponseMode.HighRail);
        HorizontalBounceMargin = source.HorizontalBounceMargin;
        VerticalBounceMargin = source.VerticalBounceMargin;
        WaveformBrightnessAtPeak = source.WaveformBrightnessAtPeak;
    }
}

/// <summary>Editable music-response and Synced rendering values saved as MetaBalls' Sync Settings.</summary>
[Serializable]
public sealed class MetaBallsSyncSettings
{
    /// <summary>Metaball field radius used in Synced Mode.</summary>
    [Min(0f)] public float Radius;

    /// <summary>Per-axis pre-division velocity range used by each Ball Roll.</summary>
    public FloatRange BallVelocity;

    /// <summary>Divisor applied to each rolled two-axis Ball velocity.</summary>
    [Min(0.0001f)] public float BallVelocityDivisor;

    /// <summary>Per-activation range selecting one of the three Waveform response modes.</summary>
    public IntRange WaveformResponseMode;

    /// <summary>Distance from either horizontal screen edge that reverses Ball velocity.</summary>
    [Min(0f)] public float HorizontalBounceMargin;

    /// <summary>Distance from either vertical screen edge that reverses Ball velocity.</summary>
    [Min(0f)] public float VerticalBounceMargin;

    /// <summary>
    /// Brightness endpoints interpolated by the Waveform; Min is the darkened peak and Max is the
    /// bright trough so the endpoint pair remains ordered for editor tuning.
    /// </summary>
    public FloatRange WaveformBrightness;

    /// <summary>Maximum hue shift contributed by the Waveform.</summary>
    [Range(0f, 1f)] public float WaveformHueShift;

    /// <summary>Waveform contribution added to the local frame delta.</summary>
    [Min(0f)] public float WaveformDeltaBoost;

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
        BallVelocity = new FloatRange(
            source.BallVelocity.Min,
            source.BallVelocity.Max,
            source.BallVelocity.LowRail,
            source.BallVelocity.HighRail);
        BallVelocityDivisor = source.BallVelocityDivisor;
        WaveformResponseMode = new IntRange(
            source.WaveformResponseMode.MinInclusive,
            source.WaveformResponseMode.MaxExclusive,
            source.WaveformResponseMode.LowRail,
            source.WaveformResponseMode.HighRail);
        HorizontalBounceMargin = source.HorizontalBounceMargin;
        VerticalBounceMargin = source.VerticalBounceMargin;
        WaveformBrightness = new FloatRange(
            source.WaveformBrightness.Min,
            source.WaveformBrightness.Max,
            source.WaveformBrightness.LowRail,
            source.WaveformBrightness.HighRail);
        WaveformHueShift = source.WaveformHueShift;
        WaveformDeltaBoost = source.WaveformDeltaBoost;
        FillSaturation = source.FillSaturation;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
