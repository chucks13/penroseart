//using System.Drawing;
using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders nearest-spinner palette fields from moving angular sources.
/// </summary>
[EffectSyncSettings(typeof(VortexSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(VortexStandaloneSettingsAsset))]
public class Vortex : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored inclusive minimum supplied to the unchanged Standalone spinner-count roll.</summary>
    private const int StandaloneSpinnerCountMinInclusive = 1;

    /// <summary>Authored exclusive upper bound supplied to the unchanged Standalone spinner-count roll.</summary>
    private const int StandaloneSpinnerCountMaxExclusive = 5;

    /// <summary>Authored inclusive minimum angular speed in degrees per second for Standalone.</summary>
    private const int StandaloneAngularSpeedDegreesPerSecondMinInclusive = 50;

    /// <summary>Authored exclusive maximum angular speed in degrees per second for Standalone.</summary>
    private const int StandaloneAngularSpeedDegreesPerSecondMaxExclusive = 100;

    /// <summary>Authored inclusive minimum supplied to the unchanged Standalone direction roll.</summary>
    private const int StandaloneDirectionRollMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to the unchanged Standalone direction roll.</summary>
    private const int StandaloneDirectionRollMaxExclusive = 2;

    /// <summary>Authored minimum shared spinner twist for the unchanged Standalone look.</summary>
    private const float StandaloneTwistMin = -0.02f;

    /// <summary>Authored maximum shared spinner twist for the unchanged Standalone look.</summary>
    private const float StandaloneTwistMax = 0.02f;

    /// <summary>Authored inclusive minimum selector supplied to the unchanged Standalone distortion roll.</summary>
    private const int StandaloneDistortionRollMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to the unchanged Standalone distortion roll.</summary>
    private const int StandaloneDistortionRollMaxExclusive = 2;

    /// <summary>Authored fully open ring scale for the unchanged Standalone look.</summary>
    private const float StandaloneRingScaleAtRest = 1f;

    /// <summary>Authored horizontal spinner-orbit radius for the unchanged Standalone look.</summary>
    private const float StandaloneHorizontalRadius = 16f;

    /// <summary>Authored vertical spinner-orbit radius for the unchanged Standalone look.</summary>
    private const float StandaloneVerticalRadius = 8f;

    /// <summary>Authored palette-field arm count for the unchanged Standalone look.</summary>
    private const int StandaloneSpinnerArms = 1;

    /// <summary>Authored neutral brightness for the unchanged Standalone look.</summary>
    private const float StandaloneBrightnessAtRest = 1f;

    /// <summary>Authored frame-time scale used by distortion mode two in the unchanged Standalone look.</summary>
    private const float StandaloneTimeScale = 0.15f;

    // Sync Defaults

    /// <summary>Authored inclusive minimum supplied to a Synced Mode spinner-count roll.</summary>
    private const int SyncSpinnerCountMinInclusive = 1;

    /// <summary>Authored exclusive upper bound supplied to a Synced Mode spinner-count roll.</summary>
    private const int SyncSpinnerCountMaxExclusive = 5;

    /// <summary>Authored inclusive minimum angular speed in degrees per second for Synced Mode.</summary>
    private const int SyncAngularSpeedDegreesPerSecondMinInclusive = 50;

    /// <summary>Authored exclusive maximum angular speed in degrees per second for Synced Mode.</summary>
    private const int SyncAngularSpeedDegreesPerSecondMaxExclusive = 100;

    /// <summary>Authored inclusive minimum supplied to a Synced Mode direction roll.</summary>
    private const int SyncDirectionRollMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to a Synced Mode direction roll.</summary>
    private const int SyncDirectionRollMaxExclusive = 2;

    /// <summary>Authored minimum shared spinner twist for Synced Mode.</summary>
    private const float SyncTwistMin = -0.02f;

    /// <summary>Authored maximum shared spinner twist for Synced Mode.</summary>
    private const float SyncTwistMax = 0.02f;

    /// <summary>Authored inclusive minimum selector supplied to the Synced Mode distortion roll.</summary>
    private const int SyncDistortionRollMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to the Synced Mode distortion roll.</summary>
    private const int SyncDistortionRollMaxExclusive = 2;

    /// <summary>Authored fully open ring scale in Synced Mode when no Drop-closing ramp is active.</summary>
    private const float SyncRingScaleAtRest = 1f;

    /// <summary>Authored horizontal spinner-orbit radius in Synced Mode.</summary>
    private const float SyncHorizontalRadius = 16f;

    /// <summary>Authored vertical spinner-orbit radius in Synced Mode.</summary>
    private const float SyncVerticalRadius = 8f;

    /// <summary>Authored palette-field arm count in Synced Mode.</summary>
    private const int SyncSpinnerArms = 1;

    /// <summary>
    /// Authored neutral brightness in Synced Mode: the fixed brightness of the non-brightness
    /// response modes, and the endpoint distortion mode zero returns to at the Waveform peak.
    /// </summary>
    private const float SyncBrightnessAtRest = 1f;

    /// <summary>Authored frame-time scale used by distortion mode two in Synced Mode.</summary>
    private const float SyncTimeScale = 0.15f;

    /// <summary>Authored brightness reached at the Waveform trough in distortion mode zero.</summary>
    private const float SyncBrightnessAtWaveformTrough = 0.65f;

    /// <summary>Authored hue offset reached at the Waveform peak in distortion mode one.</summary>
    private const float SyncHueShiftAtWaveformPeak = 0.25f;

    /// <summary>Authored extra frame-time step reached at the Waveform peak in distortion mode two.</summary>
    private const float SyncTimeStepAtWaveformPeak = 0.025f;

    /// <summary>Authored Drop decay window used by the spin slam.</summary>
    private const int SyncDropSpinDecayBeats = 8;

    /// <summary>Authored spin speed at the beginning of the Drop slam.</summary>
    private const float SyncDropSpinSpeedAtStart = 5f;

    /// <summary>
    /// Authored Drop countdown threshold where Vortex's steeper-than-stock ring-closing ramp begins.
    /// </summary>
    private const int SyncDropRingCloseThresholdBeatsUntil = 8;

    /// <summary>Authored countdown where the rings are fully shut, one beat before the Drop landing.</summary>
    private const int SyncDropRingClosedBeatsUntil = 1;

    /// <summary>Authored ring scale when the Drop-closing ramp has fully shut the rings.</summary>
    private const float SyncDropRingClosedScale = 0f;

    /// <summary>Authored saturation that makes the active-Fill response black-and-white.</summary>
    private const float SyncFillSaturation = 0f;

    /// <summary>Vortex's swirling motion suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
         Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Vortex's Standalone Defaults.</summary>
    public static VortexStandaloneSettings StandaloneDefaults => new VortexStandaloneSettings
    {
        SpinnerCount = new IntRange(StandaloneSpinnerCountMinInclusive, StandaloneSpinnerCountMaxExclusive),
        AngularSpeedDegreesPerSecond = new IntRange(
            StandaloneAngularSpeedDegreesPerSecondMinInclusive,
            StandaloneAngularSpeedDegreesPerSecondMaxExclusive),
        ReverseDirectionRoll = new IntRange(StandaloneDirectionRollMinInclusive, StandaloneDirectionRollMaxExclusive),
        SpinnerTwist = new FloatRange(StandaloneTwistMin, StandaloneTwistMax),
        DistortionModeRoll = new IntRange(StandaloneDistortionRollMinInclusive, StandaloneDistortionRollMaxExclusive),
        RingScaleAtRest = StandaloneRingScaleAtRest,
        HorizontalRadius = StandaloneHorizontalRadius,
        VerticalRadius = StandaloneVerticalRadius,
        SpinnerArms = StandaloneSpinnerArms,
        BrightnessAtRest = StandaloneBrightnessAtRest,
        TimeScale = StandaloneTimeScale,
    };

    /// <summary>Resolves a fresh copy of Vortex's file-local Sync Defaults.</summary>
    public static VortexSyncSettings SyncDefaults => new VortexSyncSettings
    {
        SpinnerCount = new IntRange(SyncSpinnerCountMinInclusive, SyncSpinnerCountMaxExclusive),
        AngularSpeedDegreesPerSecond = new IntRange(
            SyncAngularSpeedDegreesPerSecondMinInclusive,
            SyncAngularSpeedDegreesPerSecondMaxExclusive),
        ReverseDirectionRoll = new IntRange(SyncDirectionRollMinInclusive, SyncDirectionRollMaxExclusive),
        SpinnerTwist = new FloatRange(SyncTwistMin, SyncTwistMax),
        DistortionModeRoll = new IntRange(SyncDistortionRollMinInclusive, SyncDistortionRollMaxExclusive),
        RingScaleAtRest = SyncRingScaleAtRest,
        HorizontalRadius = SyncHorizontalRadius,
        VerticalRadius = SyncVerticalRadius,
        SpinnerArms = SyncSpinnerArms,
        BrightnessAtRest = SyncBrightnessAtRest,
        TimeScale = SyncTimeScale,
        BrightnessAtWaveformTrough = SyncBrightnessAtWaveformTrough,
        HueShiftAtWaveformPeak = SyncHueShiftAtWaveformPeak,
        TimeStepAtWaveformPeak = SyncTimeStepAtWaveformPeak,
        DropSpinDecayBeats = SyncDropSpinDecayBeats,
        DropSpinSpeedAtStart = SyncDropSpinSpeedAtStart,
        DropRingCloseCountdownBeats = new IntRange(
            SyncDropRingClosedBeatsUntil,
            SyncDropRingCloseThresholdBeatsUntil),
        DropRingClosedScale = SyncDropRingClosedScale,
        FillSaturation = SyncFillSaturation,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private VortexStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private VortexSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The number of spinner sources rolled for the current activation.</summary>
    private int count;

    /// <summary>The signed base angular speed rolled for the current activation.</summary>
    private float angularSpeedDegreesPerSecond;

    /// <summary>The accumulated spinner-orbit angle in degrees.</summary>
    private float angle;

    /// <summary>Which Waveform response this activation applies: brightness, color, or time.</summary>
    private int distortionMode; // 0: Brightness, 1: Color, 2: Time

    /// <summary>The live scale applied to each spinner's elliptical orbit.</summary>
    private float ringScale = StandaloneRingScaleAtRest;

    /// <summary>The moving angular sources used to find each tile's nearest palette field.</summary>
    public spinner[] spinners;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Vortex: {count}\n";
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Vortex),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Vortex),
            SyncDefaults);

        waveform = waveforms.Random();
        bool isSynced = beatManager.IsSynced;
        ringScale = isSynced
            ? SyncSettings.RingScaleAtRest
            : standaloneSettings.RingScaleAtRest;
        IntRange spinnerCount = isSynced ? SyncSettings.SpinnerCount : standaloneSettings.SpinnerCount;
        count = Random.Range(spinnerCount.MinInclusive, spinnerCount.MaxExclusive);
        angle = 0f;
        IntRange angularSpeed = isSynced
            ? SyncSettings.AngularSpeedDegreesPerSecond
            : standaloneSettings.AngularSpeedDegreesPerSecond;
        angularSpeedDegreesPerSecond = Random.Range(
            angularSpeed.MinInclusive,
            angularSpeed.MaxExclusive);
        IntRange reverseDirectionRoll = isSynced
            ? SyncSettings.ReverseDirectionRoll
            : standaloneSettings.ReverseDirectionRoll;
        if (Random.Range(reverseDirectionRoll.MinInclusive, reverseDirectionRoll.MaxExclusive) == 0)
            angularSpeedDegreesPerSecond = -angularSpeedDegreesPerSecond;
        FloatRange spinnerTwist = isSynced ? SyncSettings.SpinnerTwist : standaloneSettings.SpinnerTwist;
        float twist = Random.Range(spinnerTwist.Min, spinnerTwist.Max);
        spinners = new spinner[count];
        for (int i = 0; i < count; i++)
        {
            spinner sample = new spinner();
            //            sample.palette.blend = (Random.Range(0, 2) == 0);
            sample.twist = twist;
            spinners[i] = sample;
            //            spinners[i].palette = spinners[0].palette;          // make palettes the same
        }
        IntRange distortionModeRoll = isSynced
            ? SyncSettings.DistortionModeRoll
            : standaloneSettings.DistortionModeRoll;
        distortionMode = Random.Range(
            distortionModeRoll.MinInclusive,
            distortionModeRoll.MaxExclusive) * 2;      // 0 or 2

        buffer.Clear();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>Advances the vortex rotation, slamming the spin rate while a Drop is active.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public void Update(float delta)
    {
        float deg2rad = (Mathf.PI * 2f) / 360f;
        bool isSynced = beatManager.IsSynced;
        float horizontalRadius = isSynced
            ? SyncSettings.HorizontalRadius
            : standaloneSettings.HorizontalRadius;
        float verticalRadius = isSynced
            ? SyncSettings.VerticalRadius
            : standaloneSettings.VerticalRadius;
        int spinnerArms = isSynced
            ? SyncSettings.SpinnerArms
            : standaloneSettings.SpinnerArms;
        float spinSpeed = angularSpeedDegreesPerSecond * delta;
        if (beatManager.Drop.Active)
            spinSpeed = beatManager.Drop.In.Decay(SyncSettings.DropSpinDecayBeats)
                .Remap(1f, 0f, SyncSettings.DropSpinSpeedAtStart, spinSpeed);

        angle += spinSpeed;

        for (int i = 0; i < count; i++)
        {
            spinner sample = spinners[i];
            float local = angle + (i * 360 / count);
            local *= deg2rad;
            sample.center.x = Mathf.Sin(local) * horizontalRadius * ringScale;
            sample.center.y = Mathf.Cos(local) * verticalRadius * ringScale;
            sample.angle = local;
            sample.arms = spinnerArms;
        }
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // Rings close in across the last eight beats and are fully shut one beat before the landing —
        // Vortex's own ramp, steeper than the stock Before shape, which never reaches zero pre-landing.
        float ringScaleAtRest = beatManager.IsSynced
            ? SyncSettings.RingScaleAtRest
            : standaloneSettings.RingScaleAtRest;
        ringScale = beatManager.Drop.BeatsUntil is { } beatsTilDrop &&
                    beatsTilDrop < SyncSettings.DropRingCloseCountdownBeats.MaxExclusive
            ? ((float)beatsTilDrop).Remap(
                SyncSettings.DropRingCloseCountdownBeats.MaxExclusive,
                SyncSettings.DropRingCloseCountdownBeats.MinInclusive,
                ringScaleAtRest,
                SyncSettings.DropRingClosedScale)
            : ringScaleAtRest;

        float brightnessAtRest = beatManager.IsSynced
            ? SyncSettings.BrightnessAtRest
            : standaloneSettings.BrightnessAtRest;
        float timeScale = beatManager.IsSynced
            ? SyncSettings.TimeScale
            : standaloneSettings.TimeScale;
        float beatBrightness = brightnessAtRest;
        float hueShift = 0.0f;
        float sampleeDelta = effectDelta;

        float rhythm = waveform.Envelope;
        if (distortionMode == 0)
            beatBrightness = waveform.Lerp(SyncSettings.BrightnessAtWaveformTrough, brightnessAtRest);
        else if (distortionMode == 1)
            hueShift = SyncSettings.HueShiftAtWaveformPeak * rhythm;
        else if (distortionMode == 2)
            sampleeDelta = (effectDelta * timeScale) + (SyncSettings.TimeStepAtWaveformPeak * rhythm);

        // Beat pulse scales the nearest-spinner palette result for each tile.
        //        float beatBrightness = waveform.Lerp(0.5f, 1f);
        Update(sampleeDelta);
        for (int i = 0; i < buffer.Length; i++)
        {
            int which = -1;
            float min = 100000f;
            // find the closest
            for (int j = 0; j < spinners.Length; j++)
            {
                Vector2 delta = tiles[i].position - spinners[j].center;
                float d2 = (delta.x * delta.x) + (delta.y * delta.y);
                // Add a micro-bias tied to spinner index and tile index
                // Breaks simultaneous crossing without shifting visible geometry
                d2 += (j * 0.0001f) + (i * 0.000001f);
                if (d2 < min)
                {
                    min = d2;
                    which = j;
                }
            }
            Color c = spinners[which].Draw(i, tiles[i].position) * beatBrightness;
            float h, s, v_col;
            Color.RGBToHSV(c, out h, out s, out v_col);
            if (hueShift > 0)
                h = (h + hueShift) % 1f;
            if (beatManager.Fill.Active)                // go to black and while in a fill
            {
                v_col = (h + s + v_col) % 1f;                   // assure there is brightness variation
                s = SyncSettings.FillSaturation;
            }
            c = Color.HSVToRGB(h, s, v_col);
            buffer[i] = c * beatBrightness;
            // Draw the point
        }
    }
    /// <summary>
    /// Moving angular source used by Vortex to determine nearest palette influence.
    /// </summary>
    [Serializable]
    public class spinner
    {
        /// <summary>The current center of this spinner's palette field.</summary>
        public Vector2 center;

        /// <summary>The number of angular palette arms sampled around this spinner.</summary>
        public int arms = StandaloneSpinnerArms;

        /// <summary>The radial twist applied to this spinner's angular palette coordinate.</summary>
        public float twist = 0.01f;

        /// <summary>The current angular offset applied to this spinner's palette field.</summary>
        public float angle = 0;

        /// <summary>Converts radians into a normalized one-turn palette coordinate.</summary>
        const float rad2once = 1f / (Mathf.PI * 2f);

        /// <summary>Reserved per-spinner speed for unfinished independent-motion support.</summary>
        public float speed = 0.5f;
        //        public GPalette palette = new GPalette();

        /// <summary>
        /// Samples the spinner's palette contribution for a tile position.
        /// </summary>
        public Color Draw(int i, Vector2 position)
        {
            Vector2 vect = position - center;
            float rotate = Mathf.Atan2(vect.y, vect.x);
            float length = Vector2.Distance(center, position);
            rotate += Mathf.PI;
            rotate *= rad2once;
            rotate *= arms;
            rotate += twist * length;
            rotate += angle;
            return APalette.read(rotate % 1f);// Color.HSVToRGB(rotate%1f, 1f, 1f);
        }
    }
}

/// <summary>
/// The serializable value shape shared by Vortex's Standalone Defaults and saved Standalone Settings.
/// </summary>
[Serializable]
public sealed class VortexStandaloneSettings
{
    /// <summary>Per-activation range for the number of moving spinner sources.</summary>
    public IntRange SpinnerCount;

    /// <summary>Per-activation range for the spinner orbit's angular speed in degrees per second.</summary>
    public IntRange AngularSpeedDegreesPerSecond;

    /// <summary>Per-activation roll whose zero result reverses the spinner orbit.</summary>
    public IntRange ReverseDirectionRoll;

    /// <summary>Per-activation range for the shared radial palette twist.</summary>
    public FloatRange SpinnerTwist;

    /// <summary>Per-activation selector range whose result chooses the Waveform response mode.</summary>
    public IntRange DistortionModeRoll;

    /// <summary>Fully open ring scale used without a live Drop-closing ramp.</summary>
    [Min(0f)] public float RingScaleAtRest;

    /// <summary>Horizontal radius of each spinner's elliptical orbit.</summary>
    [Min(0f)] public float HorizontalRadius;

    /// <summary>Vertical radius of each spinner's elliptical orbit.</summary>
    [Min(0f)] public float VerticalRadius;

    /// <summary>Number of angular palette arms sampled by every spinner.</summary>
    [Min(1)] public int SpinnerArms;

    /// <summary>Neutral brightness used at rest and by non-brightness response modes.</summary>
    [Range(0f, 1f)] public float BrightnessAtRest;

    /// <summary>Frame-time multiplier used by distortion mode two.</summary>
    public float TimeScale;

    /// <summary>Copies every Vortex Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(VortexStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        SpinnerCount = Copy(source.SpinnerCount);
        AngularSpeedDegreesPerSecond = Copy(source.AngularSpeedDegreesPerSecond);
        ReverseDirectionRoll = Copy(source.ReverseDirectionRoll);
        SpinnerTwist = Copy(source.SpinnerTwist);
        DistortionModeRoll = Copy(source.DistortionModeRoll);
        RingScaleAtRest = source.RingScaleAtRest;
        HorizontalRadius = source.HorizontalRadius;
        VerticalRadius = source.VerticalRadius;
        SpinnerArms = source.SpinnerArms;
        BrightnessAtRest = source.BrightnessAtRest;
        TimeScale = source.TimeScale;
    }

    /// <summary>Creates an independent copy of an integer settings range and its Rails.</summary>
    private static IntRange Copy(IntRange source) => new(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);

    /// <summary>Creates an independent copy of a floating-point settings range and its Rails.</summary>
    private static FloatRange Copy(FloatRange source) => new(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);
}

/// <summary>Editable music-response values saved as Vortex's Sync Settings.</summary>
[Serializable]
public sealed class VortexSyncSettings
{
    /// <summary>Per-Roll range for the number of moving spinner sources.</summary>
    public IntRange SpinnerCount;

    /// <summary>Per-Roll range for the spinner orbit's angular speed in degrees per second.</summary>
    public IntRange AngularSpeedDegreesPerSecond;

    /// <summary>Per-Roll range whose zero result reverses the spinner orbit.</summary>
    public IntRange ReverseDirectionRoll;

    /// <summary>Per-Roll range for the shared radial palette twist.</summary>
    public FloatRange SpinnerTwist;

    /// <summary>
    /// Per-Roll selector whose result is doubled onto modes zero and two; its upper endpoint stays
    /// at two so randomization cannot reach an inert response mode.
    /// </summary>
    public IntRange DistortionModeRoll;

    /// <summary>Fully open ring scale used without a live Drop-closing ramp.</summary>
    [Min(0f)] public float RingScaleAtRest;

    /// <summary>Horizontal radius of each spinner's elliptical orbit.</summary>
    [Min(0f)] public float HorizontalRadius;

    /// <summary>Vertical radius of each spinner's elliptical orbit.</summary>
    [Min(0f)] public float VerticalRadius;

    /// <summary>Number of angular palette arms sampled by every spinner.</summary>
    [Min(1)] public int SpinnerArms;

    /// <summary>
    /// Neutral brightness for the non-brightness response modes, and the endpoint distortion mode
    /// zero returns to at the Waveform peak.
    /// </summary>
    [Range(0f, 1f)] public float BrightnessAtRest;

    /// <summary>Frame-time multiplier used by distortion mode two.</summary>
    public float TimeScale;

    /// <summary>Brightness reached at the Waveform trough in distortion mode zero.</summary>
    [Range(0f, 1f)] public float BrightnessAtWaveformTrough;

    /// <summary>Hue offset reached at the Waveform peak in distortion mode one.</summary>
    [Range(0f, 1f)] public float HueShiftAtWaveformPeak;

    /// <summary>Extra frame-time step reached at the Waveform peak in distortion mode two.</summary>
    public float TimeStepAtWaveformPeak;

    /// <summary>Drop decay window used by the spin slam.</summary>
    [Min(1)] public int DropSpinDecayBeats;

    /// <summary>Spin speed reached at the beginning of the Drop slam.</summary>
    public float DropSpinSpeedAtStart;

    /// <summary>
    /// Countdown endpoints interpolated by the Drop ring-closing ramp: the upper endpoint starts
    /// the close and the lower endpoint is fully shut.
    /// </summary>
    public IntRange DropRingCloseCountdownBeats;

    /// <summary>Ring scale reached when the Drop-closing ramp has fully shut the rings.</summary>
    [Min(0f)] public float DropRingClosedScale;

    /// <summary>Saturation assigned while a Fill is active.</summary>
    [Range(0f, 1f)] public float FillSaturation;

    /// <summary>Copies every Vortex Sync Setting from another value.</summary>
    public void CopyFrom(VortexSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        SpinnerCount = Copy(source.SpinnerCount);
        AngularSpeedDegreesPerSecond = Copy(source.AngularSpeedDegreesPerSecond);
        ReverseDirectionRoll = Copy(source.ReverseDirectionRoll);
        SpinnerTwist = Copy(source.SpinnerTwist);
        DistortionModeRoll = Copy(source.DistortionModeRoll);
        RingScaleAtRest = source.RingScaleAtRest;
        HorizontalRadius = source.HorizontalRadius;
        VerticalRadius = source.VerticalRadius;
        SpinnerArms = source.SpinnerArms;
        BrightnessAtRest = source.BrightnessAtRest;
        TimeScale = source.TimeScale;
        BrightnessAtWaveformTrough = source.BrightnessAtWaveformTrough;
        HueShiftAtWaveformPeak = source.HueShiftAtWaveformPeak;
        TimeStepAtWaveformPeak = source.TimeStepAtWaveformPeak;
        DropSpinDecayBeats = source.DropSpinDecayBeats;
        DropSpinSpeedAtStart = source.DropSpinSpeedAtStart;
        DropRingCloseCountdownBeats = Copy(source.DropRingCloseCountdownBeats);
        DropRingClosedScale = source.DropRingClosedScale;
        FillSaturation = source.FillSaturation;
    }

    /// <summary>Creates an independent copy of an integer settings range and its Rails.</summary>
    private static IntRange Copy(IntRange source) => new(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);

    /// <summary>Creates an independent copy of a floating-point settings range and its Rails.</summary>
    private static FloatRange Copy(FloatRange source) => new(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);
}
