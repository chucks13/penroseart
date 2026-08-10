using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders full-width falling streams and soft droplets in value space while the animated palette
/// supplies their hue.
/// </summary>
[EffectSyncSettings(typeof(WaterfallSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(WaterfallStandaloneSettingsAsset))]
public class Waterfall : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored inclusive minimum number of droplets rolled for the Standalone water.</summary>
    private const int StandaloneDropletCountMin = 32;

    /// <summary>Authored exclusive maximum number of droplets rolled for the Standalone water.</summary>
    private const int StandaloneDropletCountMaxExclusive = 49;

    /// <summary>Authored minimum palette spread down the Standalone water's rows.</summary>
    private const float StandalonePaletteSpreadMin = 0.004f;

    /// <summary>Authored maximum palette spread down the Standalone water's rows.</summary>
    private const float StandalonePaletteSpreadMax = 0.02f;

    /// <summary>Authored minimum palette-wash speed for the Standalone water.</summary>
    private const float StandalonePaletteSpeedMin = 0.01f;

    /// <summary>Authored maximum palette-wash speed for the Standalone water.</summary>
    private const float StandalonePaletteSpeedMax = 0.08f;

    /// <summary>Authored minimum width of the overlapping Standalone streams, in screen pixels.</summary>
    private const float StandaloneStreamWidthMin = 4.5f;

    /// <summary>Authored maximum width of the overlapping Standalone streams, in screen pixels.</summary>
    private const float StandaloneStreamWidthMax = 9f;

    /// <summary>Authored minimum downward travel speed of the Standalone streams, in screen pixels per second.</summary>
    private const float StandaloneStreamFallSpeedMin = 5f;

    /// <summary>Authored maximum downward travel speed of the Standalone streams, in screen pixels per second.</summary>
    private const float StandaloneStreamFallSpeedMax = 14f;

    /// <summary>Authored maximum value of the Standalone water body before droplets brighten it.</summary>
    private const float StandaloneWaterBrightness = 0.72f;

    /// <summary>Authored luminance separation between bright and dark Standalone streams.</summary>
    private const float StandaloneStreamContrast = 0.7f;

    /// <summary>
    /// Authored strength of the dark seams carved where the Standalone stream field changes
    /// fastest — the same darkness-outlines-shape device as MazeFlyer's edge lines.
    /// </summary>
    private const float StandaloneStreamEdgeShade = 0.35f;

    /// <summary>Authored floor under the Standalone water's value so no roll goes pit-dark.</summary>
    private const float StandaloneWaterMinBrightness = 0.15f;

    /// <summary>Authored inclusive minimum screen-height multiplier for Standalone droplet spawns.</summary>
    private const int StandaloneDropletSpawnHeightMinMultiplier = 1;

    /// <summary>Authored exclusive maximum screen-height multiplier for Standalone droplet spawns.</summary>
    private const int StandaloneDropletSpawnHeightMaxMultiplier = 5;

    /// <summary>Authored minimum radius for Standalone droplet rolls.</summary>
    private const float StandaloneDropletRadiusMin = 1.25f;

    /// <summary>Authored maximum radius for Standalone droplet rolls.</summary>
    private const float StandaloneDropletRadiusMax = 2.75f;

    /// <summary>Authored minimum falling speed for Standalone droplet rolls, in screen pixels per second.</summary>
    private const float StandaloneDropletSpeedMin = 8f;

    /// <summary>Authored maximum falling speed for Standalone droplet rolls, in screen pixels per second.</summary>
    private const float StandaloneDropletSpeedMax = 24f;

    /// <summary>Authored minimum value-space brightness for Standalone droplet rolls.</summary>
    private const float StandaloneDropletBrightnessMin = 0.35f;

    /// <summary>Authored maximum value-space brightness for Standalone droplet rolls.</summary>
    private const float StandaloneDropletBrightnessMax = 0.75f;

    /// <summary>Authored distance over which each Standalone droplet trail tapers to darkness.</summary>
    private const float StandaloneDropletTrailLength = 14f;

    /// <summary>Authored fraction of a Standalone droplet's brightness at the head of its trail.</summary>
    private const float StandaloneDropletTrailBrightness = 0.55f;

    /// <summary>Authored screen-height multiplier below the wall where Standalone droplets respawn.</summary>
    private const int StandaloneDropletRespawnHeightMultiplier = -2;

    // Sync Defaults

    /// <summary>
    /// Authored tempo at which BPM contributes a neutral one-times multiplier, keeping the rolled
    /// StreamFallSpeed as the baseline before Energy shapes it.
    /// </summary>
    private const float SyncReferenceBpm = 124f;

    /// <summary>
    /// Authored Low Energy multiplier applied to the BPM-scaled stream speed. The Low/High spread
    /// is wide enough that an Energy tier change reads from across the room, not as a nuance.
    /// </summary>
    private const float SyncEnergySpeedMultiplierMin = 0.6f;

    /// <summary>
    /// Authored High Energy multiplier applied to the BPM-scaled stream speed; Mid is the midpoint
    /// so the closed three-step Energy ladder stays evenly spaced.
    /// </summary>
    private const float SyncEnergySpeedMultiplierMax = 1.5f;

    /// <summary>
    /// Authored multiplicative value-space lift at the center of a Synced stream surge, leaving
    /// palette hue and saturation untouched. The authored value carries the dimmed Synced resting
    /// water to full white with a visible gradient instead of barely grazing the 1.0 clamp.
    /// </summary>
    private const float SyncStreamSurgeBrightness = 2.5f;

    /// <summary>
    /// Authored full vertical length of a Synced stream surge, in screen pixels. Kept short so each
    /// beat reads as a distinct packet rather than a long overlapping wash.
    /// </summary>
    private const float SyncStreamSurgeLength = 12f;

    /// <summary>
    /// Authored surge fall speed relative to the water's BPM × Energy speed. Above one, each beat
    /// strike visibly outruns the stream it brightens while the body keeps its normal flow.
    /// </summary>
    private const float SyncStreamSurgeSpeedMultiplier = 6f;

    /// <summary>
    /// Authored surge width relative to the rolled stream width. The authored value spans several
    /// streams so a strike reads as a broad wall hit (wall-judged); the underlying stream profile
    /// and edge seams keep the structure visible inside the swell.
    /// </summary>
    private const float SyncStreamSurgeWidthMultiplier = 8f;

    /// <summary>
    /// Authored minimum low-band level that admits a beat surge, read from the Levels reading
    /// SyncStreamSurgeLowLevelReading selects. Below it the count's window passes without a
    /// launch, so surges disappear with the bass drum during breakdowns.
    /// </summary>
    private const float SyncStreamSurgeLowLevelThreshold = 0.35f;

    /// <summary>
    /// Authored Levels reading the low-band surge gate consults. Normalized is the instantaneous
    /// wire value — the honest answer to "is a kick sounding in this window"; Smoothed lags
    /// through the attack/release follower and Peak lingers after the hit, so both stay
    /// selectable for wall judgment rather than being the default.
    /// </summary>
    private const WaterfallSyncSettings.SurgeLevelReading SyncStreamSurgeLowLevelReading =
        WaterfallSyncSettings.SurgeLevelReading.Normalized;

    /// <summary>
    /// Authored fraction of the screen width, at each edge, where no surge stream center may sit.
    /// A surge centered that far out falls mostly off the wall and reads as a missed beat.
    /// </summary>
    private const float SyncStreamSurgeEdgeMargin = 0.15f;

    /// <summary>Authored inclusive minimum number of droplets rolled in Synced Mode.</summary>
    private const int SyncDropletCountMin = 32;

    /// <summary>Authored exclusive maximum number of droplets rolled in Synced Mode.</summary>
    private const int SyncDropletCountMaxExclusive = 49;

    /// <summary>Authored minimum palette spread down the Synced water's rows.</summary>
    private const float SyncPaletteSpreadMin = 0.004f;

    /// <summary>Authored maximum palette spread down the Synced water's rows.</summary>
    private const float SyncPaletteSpreadMax = 0.02f;

    /// <summary>Authored minimum palette-wash speed in Synced Mode.</summary>
    private const float SyncPaletteSpeedMin = 0.01f;

    /// <summary>Authored maximum palette-wash speed in Synced Mode.</summary>
    private const float SyncPaletteSpeedMax = 0.08f;

    /// <summary>Authored minimum width of the overlapping Synced streams, in screen pixels.</summary>
    private const float SyncStreamWidthMin = 4.5f;

    /// <summary>Authored maximum width of the overlapping Synced streams, in screen pixels.</summary>
    private const float SyncStreamWidthMax = 9f;

    /// <summary>
    /// Authored minimum downward travel speed of the Synced streams, in screen pixels per second.
    /// The Synced roll range sits deliberately tighter than Standalone's so BPM and Energy, not
    /// the roll, carry the speed differences between activations.
    /// </summary>
    private const float SyncStreamFallSpeedMin = 8f;

    /// <summary>Authored maximum downward travel speed of the Synced streams, in screen pixels per second.</summary>
    private const float SyncStreamFallSpeedMax = 11f;

    /// <summary>
    /// Authored maximum value of the Synced water body before droplets or surges brighten it.
    /// Rests dimmer than the Standalone body so beat surges have real headroom to strike into;
    /// at the Standalone 0.72 a surge grazed the 1.0 clamp and read as muted shimmer.
    /// </summary>
    private const float SyncWaterBrightness = 0.55f;

    /// <summary>Authored luminance separation between bright and dark Synced streams.</summary>
    private const float SyncStreamContrast = 0.7f;

    /// <summary>
    /// Authored strength of the dark seams carved where the Synced stream field changes
    /// fastest — the same darkness-outlines-shape device as MazeFlyer's edge lines.
    /// </summary>
    private const float SyncStreamEdgeShade = 0.35f;

    /// <summary>Authored floor under the Synced water's value so no roll goes pit-dark.</summary>
    private const float SyncWaterMinBrightness = 0.15f;

    /// <summary>Authored inclusive minimum screen-height multiplier for Synced droplet spawns.</summary>
    private const int SyncDropletSpawnHeightMinMultiplier = 1;

    /// <summary>Authored exclusive maximum screen-height multiplier for Synced droplet spawns.</summary>
    private const int SyncDropletSpawnHeightMaxMultiplier = 5;

    /// <summary>Authored minimum radius for Synced droplet rolls.</summary>
    private const float SyncDropletRadiusMin = 1.25f;

    /// <summary>Authored maximum radius for Synced droplet rolls.</summary>
    private const float SyncDropletRadiusMax = 2.75f;

    /// <summary>Authored minimum falling speed for Synced droplet rolls, in screen pixels per second.</summary>
    private const float SyncDropletSpeedMin = 8f;

    /// <summary>Authored maximum falling speed for Synced droplet rolls, in screen pixels per second.</summary>
    private const float SyncDropletSpeedMax = 24f;

    /// <summary>Authored minimum value-space brightness for Synced droplet rolls.</summary>
    private const float SyncDropletBrightnessMin = 0.35f;

    /// <summary>Authored maximum value-space brightness for Synced droplet rolls.</summary>
    private const float SyncDropletBrightnessMax = 0.75f;

    /// <summary>Authored distance over which each Synced droplet trail tapers to darkness.</summary>
    private const float SyncDropletTrailLength = 14f;

    /// <summary>Authored fraction of a Synced droplet's brightness at the head of its trail.</summary>
    private const float SyncDropletTrailBrightness = 0.55f;

    /// <summary>Authored screen-height multiplier below the wall where Synced droplets respawn.</summary>
    private const int SyncDropletRespawnHeightMultiplier = -2;

    /// <summary>
    /// Number of complete stream-selection cycles an identity may miss before oldest-first
    /// selection guarantees that it cannot starve.
    /// </summary>
    private const int SurgeStarvationStreamCycles = 2;

    /// <summary>Waterfall's falling streams and droplets suit Low-, Mid-, and High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Waterfall's file-local Standalone Defaults.</summary>
    public static WaterfallStandaloneSettings StandaloneDefaults => new WaterfallStandaloneSettings
    {
        DropletCount = new IntRange(
            StandaloneDropletCountMin,
            StandaloneDropletCountMaxExclusive),
        PaletteSpread = new FloatRange(StandalonePaletteSpreadMin, StandalonePaletteSpreadMax),
        PaletteSpeed = new FloatRange(StandalonePaletteSpeedMin, StandalonePaletteSpeedMax),
        StreamWidth = new FloatRange(StandaloneStreamWidthMin, StandaloneStreamWidthMax),
        StreamFallSpeed = new FloatRange(
            StandaloneStreamFallSpeedMin,
            StandaloneStreamFallSpeedMax),
        WaterBrightness = StandaloneWaterBrightness,
        StreamContrast = StandaloneStreamContrast,
        StreamEdgeShade = StandaloneStreamEdgeShade,
        WaterMinBrightness = StandaloneWaterMinBrightness,
        DropletSpawnHeightMultiplier = new IntRange(
            StandaloneDropletSpawnHeightMinMultiplier,
            StandaloneDropletSpawnHeightMaxMultiplier),
        DropletRadius = new FloatRange(StandaloneDropletRadiusMin, StandaloneDropletRadiusMax),
        DropletSpeed = new FloatRange(StandaloneDropletSpeedMin, StandaloneDropletSpeedMax),
        DropletBrightness = new FloatRange(
            StandaloneDropletBrightnessMin,
            StandaloneDropletBrightnessMax),
        DropletTrailLength = StandaloneDropletTrailLength,
        DropletTrailBrightness = StandaloneDropletTrailBrightness,
        DropletRespawnHeightMultiplier = StandaloneDropletRespawnHeightMultiplier,
    };

    /// <summary>Resolves a fresh copy of Waterfall's file-local Sync Defaults.</summary>
    public static WaterfallSyncSettings SyncDefaults => new WaterfallSyncSettings
    {
        ReferenceBpm = SyncReferenceBpm,
        EnergySpeedMultiplier = new FloatRange(
            SyncEnergySpeedMultiplierMin,
            SyncEnergySpeedMultiplierMax),
        StreamSurgeBrightness = SyncStreamSurgeBrightness,
        StreamSurgeLength = SyncStreamSurgeLength,
        StreamSurgeWidthMultiplier = SyncStreamSurgeWidthMultiplier,
        StreamSurgeSpeedMultiplier = SyncStreamSurgeSpeedMultiplier,
        StreamSurgeLowLevelThreshold = SyncStreamSurgeLowLevelThreshold,
        StreamSurgeLowLevelReading = SyncStreamSurgeLowLevelReading,
        StreamSurgeEdgeMargin = SyncStreamSurgeEdgeMargin,
        DropletCount = new IntRange(SyncDropletCountMin, SyncDropletCountMaxExclusive),
        PaletteSpread = new FloatRange(SyncPaletteSpreadMin, SyncPaletteSpreadMax),
        PaletteSpeed = new FloatRange(SyncPaletteSpeedMin, SyncPaletteSpeedMax),
        StreamWidth = new FloatRange(SyncStreamWidthMin, SyncStreamWidthMax),
        StreamFallSpeed = new FloatRange(SyncStreamFallSpeedMin, SyncStreamFallSpeedMax),
        WaterBrightness = SyncWaterBrightness,
        StreamContrast = SyncStreamContrast,
        StreamEdgeShade = SyncStreamEdgeShade,
        WaterMinBrightness = SyncWaterMinBrightness,
        DropletSpawnHeightMultiplier = new IntRange(
            SyncDropletSpawnHeightMinMultiplier,
            SyncDropletSpawnHeightMaxMultiplier),
        DropletRadius = new FloatRange(SyncDropletRadiusMin, SyncDropletRadiusMax),
        DropletSpeed = new FloatRange(SyncDropletSpeedMin, SyncDropletSpeedMax),
        DropletBrightness = new FloatRange(SyncDropletBrightnessMin, SyncDropletBrightnessMax),
        DropletTrailLength = SyncDropletTrailLength,
        DropletTrailBrightness = SyncDropletTrailBrightness,
        DropletRespawnHeightMultiplier = SyncDropletRespawnHeightMultiplier,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private WaterfallStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private WaterfallSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The droplets currently falling through screen space.</summary>
    private Droplet[] droplets;

    /// <summary>The number of droplets rolled for the current activation.</summary>
    private int dropletCount;

    /// <summary>The palette spread rolled across the water's columns.</summary>
    private float paletteSpread;

    /// <summary>The palette-wash speed rolled for the current activation.</summary>
    private float paletteSpeed;

    /// <summary>The width rolled for the overlapping vertical streams.</summary>
    private float streamWidth;

    /// <summary>The downward screen-space speed rolled for the stream field.</summary>
    private float streamFallSpeed;

    /// <summary>The value-space frame composed before palette hue is applied.</summary>
    private float[] waterValueBuffer;

    /// <summary>The cross-stream profile computed once per column, kept for edge-seam gradients.</summary>
    private float[] streamProfileByColumn;

    /// <summary>The palette hue sampled once per screen row for the current frame.</summary>
    private float[] paletteHueByRow;

    /// <summary>The palette saturation sampled once per screen row for the current frame.</summary>
    private float[] paletteSaturationByRow;

    /// <summary>
    /// The bounded downward phase of the stream texture. Integrating speed here prevents BPM or
    /// Energy changes from multiplying Waterfall's large randomized effectTime into a phase jump.
    /// </summary>
    private float streamFallOffset;

    /// <summary>
    /// Stable stream centers derived from maxima of the pinned primary harmonic. Minor harmonics
    /// can reshape and merge the field without moving these selection identities.
    /// </summary>
    private float[] streamCenterById;

    /// <summary>The number of beat selections each stable stream identity has gone unpicked.</summary>
    private int[] beatsSinceSurgeByStream;

    /// <summary>The overlapping beat-launched luminance swells currently crossing the wall.</summary>
    private StreamSurge[] streamSurges;

    /// <summary>The number of stable primary-harmonic stream identities in the current Roll.</summary>
    private int streamCount;

    /// <summary>The number of active entries in the fixed-capacity surge array.</summary>
    private int activeSurgeCount;

    /// <summary>The first surge slot considered when the next musical count launches a swell.</summary>
    private int nextSurgeSlot;

    /// <summary>
    /// Whether the current quarter-beat window already launched its surge. Scoping the launch to
    /// the open window instead of its rising edge lets the low-band gate admit a count as soon as
    /// the bass confirms it, while still launching at most one surge per musical count.
    /// </summary>
    private bool surgeLaunchedThisWindow;

    /// <summary>Reports the current droplet count and rolled water-motion values for debug UI.</summary>
    /// <returns>A multi-line description of the current activation.</returns>
    public override string DebugText()
    {
        return $"Droplets: {dropletCount}\n" +
            $"Palette spread: {paletteSpread}\n" +
            $"Palette speed: {paletteSpeed}\n" +
            $"Stream width: {streamWidth}\n" +
            $"Stream fall speed: {streamFallSpeed}\n";
    }

    /// <summary>
    /// Allocates Waterfall's reusable value, palette, stream-identity, and surge buffers after
    /// screen setup so Draw performs no managed allocation.
    /// </summary>
    public override void Init()
    {
        base.Init();
        waterValueBuffer = new float[screenBuffer.Length];
        streamProfileByColumn = new float[width];
        paletteHueByRow = new float[height];
        paletteSaturationByRow = new float[height];
        streamCenterById = new float[width];
        beatsSinceSurgeByStream = new int[width];
        streamSurges = new StreamSurge[height];
    }

    /// <summary>Resolves settings, performs the activation Roll, and creates the droplet field.</summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Waterfall),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Waterfall),
            SyncDefaults);

        bool isSynced = beatManager.IsSynced;
        IntRange dropletCountRange = isSynced
            ? SyncSettings.DropletCount
            : standaloneSettings.DropletCount;
        FloatRange paletteSpreadRange = isSynced
            ? SyncSettings.PaletteSpread
            : standaloneSettings.PaletteSpread;
        FloatRange paletteSpeedRange = isSynced
            ? SyncSettings.PaletteSpeed
            : standaloneSettings.PaletteSpeed;
        FloatRange streamWidthRange = isSynced
            ? SyncSettings.StreamWidth
            : standaloneSettings.StreamWidth;
        FloatRange streamFallSpeedRange = isSynced
            ? SyncSettings.StreamFallSpeed
            : standaloneSettings.StreamFallSpeed;

        dropletCount = Random.Range(
            dropletCountRange.MinInclusive,
            dropletCountRange.MaxExclusive);
        paletteSpread = Random.Range(paletteSpreadRange.Min, paletteSpreadRange.Max);
        paletteSpeed = Random.Range(paletteSpeedRange.Min, paletteSpeedRange.Max);
        streamWidth = Random.Range(streamWidthRange.Min, streamWidthRange.Max);
        streamFallSpeed = Random.Range(streamFallSpeedRange.Min, streamFallSpeedRange.Max);
        streamFallOffset = Mathf.Repeat(
            effectTime * streamFallSpeed,
            streamWidth * 4f);
        InitializeStreamIdentities();
        Array.Clear(streamSurges, 0, streamSurges.Length);
        activeSurgeCount = 0;
        nextSurgeSlot = 0;
        surgeLaunchedThisWindow = false;
        buffer.Clear();

        IntRange dropletSpawnHeightMultiplierRange = isSynced
            ? SyncSettings.DropletSpawnHeightMultiplier
            : standaloneSettings.DropletSpawnHeightMultiplier;
        FloatRange dropletRadiusRange = isSynced
            ? SyncSettings.DropletRadius
            : standaloneSettings.DropletRadius;
        FloatRange dropletSpeedRange = isSynced
            ? SyncSettings.DropletSpeed
            : standaloneSettings.DropletSpeed;
        FloatRange dropletBrightnessRange = isSynced
            ? SyncSettings.DropletBrightness
            : standaloneSettings.DropletBrightness;

        droplets = new Droplet[dropletCount];
        for (int i = 0; i < droplets.Length; i++)
        {
            droplets[i] = new Droplet(
                dropletSpawnHeightMultiplierRange,
                dropletRadiusRange,
                dropletSpeedRange,
                dropletBrightnessRange);
        }
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd()
    {
    }

    /// <summary>
    /// Composes falling value structure, droplets, and beat-launched stream surges, applies the
    /// unchanged palette wash, then maps the screen buffer to tiles.
    /// </summary>
    public override void Draw()
    {
        bool isSynced = beatManager.IsSynced;
        IntRange dropletSpawnHeightMultiplierRange = isSynced
            ? SyncSettings.DropletSpawnHeightMultiplier
            : standaloneSettings.DropletSpawnHeightMultiplier;
        int dropletRespawnHeightMultiplier = isSynced
            ? SyncSettings.DropletRespawnHeightMultiplier
            : standaloneSettings.DropletRespawnHeightMultiplier;
        float dropletTrailLength = isSynced
            ? SyncSettings.DropletTrailLength
            : standaloneSettings.DropletTrailLength;
        float dropletTrailBrightness = isSynced
            ? SyncSettings.DropletTrailBrightness
            : standaloneSettings.DropletTrailBrightness;
        float waterBrightness = isSynced
            ? SyncSettings.WaterBrightness
            : standaloneSettings.WaterBrightness;
        float streamContrast = isSynced
            ? SyncSettings.StreamContrast
            : standaloneSettings.StreamContrast;
        float streamEdgeShade = isSynced
            ? SyncSettings.StreamEdgeShade
            : standaloneSettings.StreamEdgeShade;
        float waterMinBrightness = isSynced
            ? SyncSettings.WaterMinBrightness
            : standaloneSettings.WaterMinBrightness;
        float streamSpeedMultiplier = CurrentStreamSpeedMultiplier(isSynced);
        float effectiveStreamFallSpeed = streamFallSpeed * streamSpeedMultiplier;

        // The flow phase is integrated rather than recomputed from effectTime: changing BPM or
        // Energy changes velocity without teleporting the water. Four stream widths is exactly one
        // vertical flow period because the falling sine uses one quarter of streamFrequency.
        streamFallOffset = Mathf.Repeat(
            streamFallOffset + (effectiveStreamFallSpeed * effectDelta),
            streamWidth * 4f);
        UpdateStreamSurges(
            isSynced,
            effectiveStreamFallSpeed * SyncSettings.StreamSurgeSpeedMultiplier,
            SyncSettings.StreamSurgeLength);

        // Droplet physics advances exactly once per frame. Respawn remains the one permitted
        // per-frame Roll and reads the currently active mode's authored position range. Synced
        // droplets share the water body's BPM × Energy multiplier so the two falling layers do not
        // drift apart; the multiplier rests at one in Standalone or without BPM.
        for (int i = 0; i < droplets.Length; i++)
        {
            droplets[i].Advance(
                effectDelta * streamSpeedMultiplier,
                dropletSpawnHeightMultiplierRange,
                dropletRespawnHeightMultiplier);
        }

        float streamFrequency = Mathf.PI * 2f / streamWidth;
        float darkestStream = waterBrightness * (1f - streamContrast);

        // Only the two weaker harmonics wander, phase-oscillating in opposite directions. The
        // dominant harmonic stays pinned: when its phase wandered too, the whole field visibly
        // translated with it — every activation swept right-to-left, which killed the effect.
        // The counterposed minor drifts still move the interference peaks, so individual streams
        // breathe in width and wobble in place without any shared direction of travel.
        // The minor drift and merge amplitudes are kept small: their mid-oscillation sweeps are
        // the one remaining lateral motion, and larger amplitudes read as right-to-left banding.
        float secondaryDrift = Mathf.Sin(effectTime * 0.059f + 1.3f) * 1.6f;
        float groupingDrift = Mathf.Sin(effectTime * 0.107f + 4.1f) * -1.1f;

        // The merge envelope slowly fades the dominant comb in and out across the width. Where
        // it runs weak the comb's dark gaps fill in and neighboring streams join into one broad
        // mass; where strong, streams stay crisp — real falls do both. Its phase oscillates
        // (zero-mean) like the minor drifts, so merge zones wobble in place, never travel.
        float mergeDrift = Mathf.Sin(effectTime * 0.047f + 2.6f) * 1.3f;
        for (int x = 0; x < width; x++)
        {
            // The cross-stream profile depends on x (and the slow minor drifts) alone, never the
            // fall time — a phase that mixes x with the fall term slides sideways. Three
            // harmonics at irrational frequency ratios (1 : golden ratio : √2−1) interfere
            // aperiodically, so stream spacing is never even; the long 0.414 wavelength unevens
            // the grouping.
            float primaryStrength = 0.55f +
                (0.45f * Mathf.Sin(x * streamFrequency * 0.23f + mergeDrift));
            streamProfileByColumn[x] = 0.5f +
                (0.24f * primaryStrength * Mathf.Sin(x * streamFrequency)) +
                (0.16f * Mathf.Sin(x * streamFrequency * 1.618f + 1.7f + secondaryDrift)) +
                (0.10f * Mathf.Sin(x * streamFrequency * 0.414f + groupingDrift));
        }

        for (int x = 0; x < width; x++)
        {
            float streamProfile = streamProfileByColumn[x];

            // Dark seams are carved where the cross-stream slope is steepest — the boundary
            // between a stream and its neighbor — so darkness outlines every stream the way
            // MazeFlyer's edge lines outline its walls. Shading follows the field, so seams
            // hold as still as the streams they separate.
            float slope = streamProfileByColumn[Mathf.Min(x + 1, width - 1)] -
                streamProfileByColumn[Mathf.Max(x - 1, 0)];
            float edgeFactor = 1f - (streamEdgeShade * Mathf.Clamp01(Mathf.Abs(slope) * 1.5f));

            // The per-stream phase stagger is constant in time: it keeps neighboring streams'
            // falling texture out of step so the flow never lines up into horizontal bars.
            float streamPhase = x * streamFrequency * 0.7f;
            int screenIndex = x;

            for (int y = 0; y < height; y++)
            {
                // Time lives only in the y term, so texture moves straight down each stream.
                // The 0.25 factor stretches the falling features to several stream-widths tall.
                float flow = 0.5f + (0.5f * Mathf.Sin(
                    ((y + streamFallOffset) * streamFrequency * 0.25f) + streamPhase));
                float stream = streamProfile * (0.55f + (0.45f * flow));
                float value = Mathf.Lerp(darkestStream, waterBrightness, stream) * edgeFactor;
                waterValueBuffer[screenIndex] = Mathf.Max(value, waterMinBrightness);
                screenIndex += width;
            }
        }

        // Surges multiply the existing value field after its stream and edge shaping. Their x/y
        // falloffs remain inside one stable stream identity, and multiplicative composition keeps
        // the field's internal contrast and dark seams visible instead of painting a flat band.
        if (isSynced && activeSurgeCount > 0)
        {
            for (int i = 0; i < streamSurges.Length; i++)
            {
                if (streamSurges[i].Active)
                {
                    AddStreamSurgeValue(
                        streamSurges[i],
                        SyncSettings.StreamSurgeBrightness,
                        SyncSettings.StreamSurgeLength,
                        SyncSettings.StreamSurgeWidthMultiplier,
                        streamEdgeShade);
                }
            }
        }

        // Bounded splats replace the former screen-pixel × droplet pass. Each droplet now touches
        // only the core and trail pixels whose value it can actually change.
        for (int i = 0; i < droplets.Length; i++)
        {
            AddDropletValue(droplets[i], dropletTrailLength, dropletTrailBrightness);
        }

        // Hue is sampled per row and its time offset carries constant-hue features toward lower
        // y — the color wash falls with the water instead of sliding sideways across it.
        float paletteOffset = effectTime * paletteSpeed;
        for (int y = 0; y < height; y++)
        {
            Color paletteColor = APalette.read(y * paletteSpread + paletteOffset, true);
            Color.RGBToHSV(
                paletteColor,
                out paletteHueByRow[y],
                out paletteSaturationByRow[y],
                out _);
        }

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            float rowHue = paletteHueByRow[y];
            float rowSaturation = paletteSaturationByRow[y];
            for (int x = 0; x < width; x++)
            {
                int screenIndex = rowStart + x;
                float h = rowHue;
                float s = rowSaturation;
                float v = waterValueBuffer[screenIndex];

                screenBuffer[screenIndex] = Color.HSVToRGB(h % 1f, s, v);
            }
        }

        // convert the 2D Matrix buffer to a tile buffer
        ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in buffer);
    }

    /// <summary>
    /// Composes the rolled stream speed with live BPM and Energy while keeping one as the exact
    /// Standalone and BPM-unavailable multiplier.
    /// </summary>
    /// <param name="isSynced">Whether the running one-through-four count is available.</param>
    /// <returns>The multiplier shared by the water body, droplets, and active surges.</returns>
    private float CurrentStreamSpeedMultiplier(bool isSynced)
    {
        if (!isSynced || beatManager.Timing.Bpm is not { } bpm)
        {
            return 1f;
        }

        float energyPosition = beatManager.Energy.Level switch
        {
            Energy.Low => 0f,
            Energy.Mid => 0.5f,
            Energy.High => 1f,
            _ => 0.5f,
        };

        // Energy's Trend names the next step's direction while Progress supplies continuous
        // position through the current run. One step spans half of the normalized three-tier
        // ladder, so the rate reaches the adjacent tier at the run boundary instead of snapping.
        if (beatManager.Energy.Progress is { } energyProgress)
        {
            energyPosition += beatManager.Energy.Trend switch
            {
                EnergyTrend.Rising => energyProgress * 0.5f,
                EnergyTrend.Falling => energyProgress * -0.5f,
                _ => 0f,
            };
        }

        float energyMultiplier = Mathf.Lerp(
            SyncSettings.EnergySpeedMultiplier.Min,
            SyncSettings.EnergySpeedMultiplier.Max,
            Mathf.Clamp01(energyPosition));
        return bpm / SyncSettings.ReferenceBpm * energyMultiplier;
    }

    /// <summary>
    /// Derives stable stream identities from the pinned primary harmonic's maxima. The minor
    /// harmonics can wander and merge the visible field, but their motion never changes these
    /// centers or transfers a surge between identities. Centers inside the authored edge margin
    /// are excluded — a surge that far out falls mostly off the wall and reads as a missed beat —
    /// so the margin is applied here, at Roll, and a live edit takes effect on the next
    /// activation.
    /// </summary>
    private void InitializeStreamIdentities()
    {
        Array.Clear(beatsSinceSurgeByStream, 0, beatsSinceSurgeByStream.Length);
        streamCount = 0;

        float minCenter = width * SyncSettings.StreamSurgeEdgeMargin;
        float maxCenter = width - minCenter;
        for (float center = streamWidth * 0.25f; center < width; center += streamWidth)
        {
            if (center < minCenter || center > maxCenter)
            {
                continue;
            }

            streamCenterById[streamCount] = center;
            streamCount++;
        }

        if (streamCount == 0)
        {
            // A stretched margin rail can exclude every derived center; the center closest to
            // mid-screen stays eligible so a beat always has a stream to strike.
            float closestCenter = streamWidth * 0.25f;
            for (float center = streamWidth * 0.25f; center < width; center += streamWidth)
            {
                if (Mathf.Abs(center - (width * 0.5f)) <
                    Mathf.Abs(closestCenter - (width * 0.5f)))
                {
                    closestCenter = center;
                }
            }

            streamCenterById[0] = closestCenter;
            streamCount = 1;
        }
    }

    /// <summary>
    /// Advances existing surges and launches at most one luminance swell per musical count: the
    /// count's quarter-beat window opens the opportunity and the Smoothed low band must confirm
    /// it. Losing Sync clears the moving response immediately so Standalone remains the approved
    /// water-only look.
    /// </summary>
    /// <param name="isSynced">Whether the running one-through-four count is available.</param>
    /// <param name="fallSpeed">Current downward surge speed in screen pixels per second: the
    /// water's BPM × Energy speed times the authored surge speed multiplier.</param>
    /// <param name="surgeLength">Current authored full surge length in screen pixels.</param>
    private void UpdateStreamSurges(bool isSynced, float fallSpeed, float surgeLength)
    {
        if (!isSynced)
        {
            surgeLaunchedThisWindow = false;
            if (activeSurgeCount > 0)
            {
                Array.Clear(streamSurges, 0, streamSurges.Length);
                activeSurgeCount = 0;
                nextSurgeSlot = 0;
            }
            return;
        }

        if (activeSurgeCount > 0)
        {
            float halfSurgeLength = surgeLength * 0.5f;
            for (int i = 0; i < streamSurges.Length; i++)
            {
                if (!streamSurges[i].Active)
                {
                    continue;
                }

                streamSurges[i].PositionY -= fallSpeed * effectDelta;
                if (streamSurges[i].PositionY + halfSurgeLength < 0f)
                {
                    streamSurges[i].Active = false;
                    activeSurgeCount--;
                }
            }
        }

        bool beatWindowOpen = beatManager.Beats.OnBeat(1)
            || beatManager.Beats.OnBeat(2)
            || beatManager.Beats.OnBeat(3)
            || beatManager.Beats.OnBeat(4);
        if (!beatWindowOpen)
        {
            surgeLaunchedThisWindow = false;
            return;
        }

        // The low band gates the launch: a count without bass presence spawns no surge, so surges
        // vanish with the kick during breakdowns. Checking every open-window frame instead of only
        // the window's rising edge lets a kick that registers a frame or two late still fire its
        // count's surge.
        float gateLow = SyncSettings.StreamSurgeLowLevelReading switch
        {
            WaterfallSyncSettings.SurgeLevelReading.Smoothed => beatManager.Levels.Smoothed.Low,
            WaterfallSyncSettings.SurgeLevelReading.Peak => beatManager.Levels.Peak.Low,
            _ => beatManager.Levels.Normalized.Low,
        };
        if (!surgeLaunchedThisWindow &&
            gateLow >= SyncSettings.StreamSurgeLowLevelThreshold)
        {
            StartStreamSurge();
            surgeLaunchedThisWindow = true;
        }
    }

    /// <summary>
    /// Launches one surge at the top of its selected stable stream. The fixed array retains
    /// overlapping beat swells without per-frame or per-event managed allocation.
    /// </summary>
    private void StartStreamSurge()
    {
        int slot = nextSurgeSlot;
        if (activeSurgeCount < streamSurges.Length)
        {
            for (int offset = 0; offset < streamSurges.Length; offset++)
            {
                int candidate = (nextSurgeSlot + offset) % streamSurges.Length;
                if (!streamSurges[candidate].Active)
                {
                    slot = candidate;
                    break;
                }
            }
            activeSurgeCount++;
        }

        streamSurges[slot].Active = true;
        streamSurges[slot].StreamIndex = SelectSurgeStream();
        streamSurges[slot].PositionY = height - 1f;
        nextSurgeSlot = (slot + 1) % streamSurges.Length;
    }

    /// <summary>
    /// Selects a stable stream by squared time-since-picked weight. A recently picked stream keeps
    /// a small chance for natural grouping, while an identity missed for two full stream cycles is
    /// selected oldest-first so no stream can starve over a show.
    /// </summary>
    /// <returns>The zero-based stable stream identity selected for the next surge.</returns>
    private int SelectSurgeStream()
    {
        int oldestAge = 0;
        for (int i = 0; i < streamCount; i++)
        {
            beatsSinceSurgeByStream[i]++;
            oldestAge = Mathf.Max(oldestAge, beatsSinceSurgeByStream[i]);
        }

        int selectedStream = streamCount - 1;
        if (oldestAge >= streamCount * SurgeStarvationStreamCycles)
        {
            int oldestCount = 0;
            for (int i = 0; i < streamCount; i++)
            {
                if (beatsSinceSurgeByStream[i] == oldestAge)
                {
                    oldestCount++;
                }
            }

            int oldestPick = Random.Range(0, oldestCount);
            for (int i = 0; i < streamCount; i++)
            {
                if (beatsSinceSurgeByStream[i] == oldestAge && oldestPick-- == 0)
                {
                    selectedStream = i;
                    break;
                }
            }
        }
        else
        {
            float totalWeight = 0f;
            for (int i = 0; i < streamCount; i++)
            {
                float ageWeight = beatsSinceSurgeByStream[i] + 1f;
                totalWeight += ageWeight * ageWeight;
            }

            float weightPick = Random.value * totalWeight;
            for (int i = 0; i < streamCount; i++)
            {
                float ageWeight = beatsSinceSurgeByStream[i] + 1f;
                weightPick -= ageWeight * ageWeight;
                if (weightPick <= 0f)
                {
                    selectedStream = i;
                    break;
                }
            }
        }

        beatsSinceSurgeByStream[selectedStream] = 0;
        return selectedStream;
    }

    /// <summary>
    /// Multiplies one moving surge into the existing water value while reusing the selected
    /// stream's profile and edge shade, so the swell brightens structure instead of painting over
    /// it or changing palette color.
    /// </summary>
    /// <param name="surge">The active stream identity and vertical center to render.</param>
    /// <param name="brightness">Multiplicative value-space lift at the surge center.</param>
    /// <param name="length">Full vertical surge length in screen pixels.</param>
    /// <param name="widthMultiplier">Surge width relative to the rolled stream width.</param>
    /// <param name="edgeShade">Current authored strength of the field's dark edge seams.</param>
    private void AddStreamSurgeValue(
        StreamSurge surge,
        float brightness,
        float length,
        float widthMultiplier,
        float edgeShade)
    {
        float centerX = streamCenterById[surge.StreamIndex];
        float halfWidth = streamWidth * widthMultiplier * 0.5f;
        float halfLength = length * 0.5f;
        int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - halfWidth));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centerX + halfWidth));
        int minY = Mathf.Max(0, Mathf.FloorToInt(surge.PositionY - halfLength));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(surge.PositionY + halfLength));

        for (int x = minX; x <= maxX; x++)
        {
            float horizontalRemaining = 1f - (Mathf.Abs(x - centerX) / halfWidth);
            if (horizontalRemaining <= 0f)
            {
                continue;
            }

            float slope = streamProfileByColumn[Mathf.Min(x + 1, width - 1)] -
                streamProfileByColumn[Mathf.Max(x - 1, 0)];
            float edgeFactor = 1f - (edgeShade * Mathf.Clamp01(Mathf.Abs(slope) * 1.5f));
            float crossStreamStrength = SoftFalloff(horizontalRemaining) *
                streamProfileByColumn[x] * edgeFactor;

            for (int y = minY; y <= maxY; y++)
            {
                float verticalRemaining = 1f - (Mathf.Abs(y - surge.PositionY) / halfLength);
                if (verticalRemaining <= 0f)
                {
                    continue;
                }

                float swell = brightness * crossStreamStrength * SoftFalloff(verticalRemaining);
                int screenIndex = x + (y * width);
                waterValueBuffer[screenIndex] = Mathf.Min(
                    1f,
                    waterValueBuffer[screenIndex] * (1f + swell));
            }
        }
    }

    /// <summary>Adds one droplet's soft core and tapered trail to the value-space frame.</summary>
    /// <param name="droplet">The rolled droplet to render.</param>
    /// <param name="trailLength">The screen-space distance over which the trail reaches zero.</param>
    /// <param name="trailBrightness">The fraction of droplet brightness at the trail head.</param>
    private void AddDropletValue(
        Droplet droplet,
        float trailLength,
        float trailBrightness)
    {
        Vector2 position = droplet.Position;
        float radius = droplet.Radius;
        float brightness = droplet.Brightness;
        float radiusSquared = radius * radius;
        int minX = Mathf.Max(0, Mathf.FloorToInt(position.x - radius));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(position.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(position.y - radius));
        int maxY = Mathf.Min(
            height - 1,
            Mathf.CeilToInt(position.y + Mathf.Max(radius, trailLength)));

        for (int x = minX; x <= maxX; x++)
        {
            float horizontalDistance = Mathf.Abs(x - position.x);
            float horizontalRemaining = 1f - (horizontalDistance / radius);

            for (int y = minY; y <= maxY; y++)
            {
                float verticalDistance = y - position.y;
                float distanceSquared =
                    (horizontalDistance * horizontalDistance) +
                    (verticalDistance * verticalDistance);
                float contribution = 0f;

                if (distanceSquared < radiusSquared)
                {
                    float radialRemaining = 1f - (Mathf.Sqrt(distanceSquared) / radius);
                    contribution = brightness * SoftFalloff(radialRemaining);
                }

                if (verticalDistance > 0f &&
                    verticalDistance < trailLength &&
                    horizontalRemaining > 0f)
                {
                    float trailRemaining = 1f - (verticalDistance / trailLength);
                    float trailContribution = brightness * trailBrightness *
                        SoftFalloff(horizontalRemaining) * trailRemaining * trailRemaining;
                    contribution = Mathf.Max(contribution, trailContribution);
                }

                int screenIndex = x + (y * width);
                waterValueBuffer[screenIndex] = Mathf.Min(
                    1f,
                    waterValueBuffer[screenIndex] + contribution);
            }
        }
    }

    /// <summary>Shapes a normalized remaining-distance value into an edge-soft cubic falloff.</summary>
    /// <param name="remaining">A normalized value that is one at the core and zero at the edge.</param>
    /// <returns>The smoothed brightness contribution.</returns>
    private static float SoftFalloff(float remaining)
    {
        return remaining * remaining * (3f - (2f * remaining));
    }

    /// <summary>
    /// One beat-launched luminance swell moving down a stable primary-harmonic stream identity.
    /// </summary>
    private struct StreamSurge
    {
        /// <summary>Whether this fixed-capacity entry currently contributes to the value field.</summary>
        public bool Active;

        /// <summary>The stable primary-harmonic stream identity selected for this swell.</summary>
        public int StreamIndex;

        /// <summary>The current vertical center of the swell in screen space.</summary>
        public float PositionY;
    }

    /// <summary>One rolled, falling screen-space droplet used by Waterfall.</summary>
    private sealed class Droplet
    {
        /// <summary>The current screen-space position of this droplet.</summary>
        private Vector2 position;

        /// <summary>The radius rolled for this droplet at activation.</summary>
        private readonly float radius;

        /// <summary>The falling speed rolled for this droplet at activation.</summary>
        private readonly float speed;

        /// <summary>The value-space brightness rolled for this droplet at activation.</summary>
        private readonly float brightness;

        /// <summary>The current screen-space position read by Waterfall's value compositor.</summary>
        public Vector2 Position => position;

        /// <summary>The rolled radius read by Waterfall's value compositor.</summary>
        public float Radius => radius;

        /// <summary>The rolled value-space brightness read by Waterfall's value compositor.</summary>
        public float Brightness => brightness;

        /// <summary>Creates a droplet whose initial position and fixed radius, speed, and brightness are rolled at activation.</summary>
        /// <param name="spawnHeightMultiplierRange">Inclusive-minimum/exclusive-maximum screen-height multipliers for the position roll.</param>
        /// <param name="radiusRange">Endpoints supplied to the radius roll.</param>
        /// <param name="speedRange">Endpoints supplied to the speed roll.</param>
        /// <param name="brightnessRange">Endpoints supplied to the value-space brightness roll.</param>
        public Droplet(
            IntRange spawnHeightMultiplierRange,
            FloatRange radiusRange,
            FloatRange speedRange,
            FloatRange brightnessRange)
        {
            Respawn(spawnHeightMultiplierRange);
            radius = Random.Range(radiusRange.Min, radiusRange.Max);
            speed = Random.Range(speedRange.Min, speedRange.Max);
            brightness = Random.Range(brightnessRange.Min, brightnessRange.Max);
        }

        /// <summary>Advances the droplet once and respawns it after it falls below the wall.</summary>
        /// <param name="deltaTime">The current frame delta.</param>
        /// <param name="spawnHeightMultiplierRange">Inclusive-minimum/exclusive-maximum screen-height multipliers for a respawn roll.</param>
        /// <param name="respawnHeightMultiplier">Screen-height multiplier that triggers a respawn.</param>
        public void Advance(
            float deltaTime,
            IntRange spawnHeightMultiplierRange,
            int respawnHeightMultiplier)
        {
            position.y -= speed * deltaTime;
            if (position.y < height * respawnHeightMultiplier)
            {
                Respawn(spawnHeightMultiplierRange);
            }
        }

        /// <summary>Rolls the droplet's next position above the wall while retaining its shape and speed.</summary>
        /// <param name="spawnHeightMultiplierRange">Inclusive-minimum/exclusive-maximum screen-height multipliers for the position roll.</param>
        private void Respawn(IntRange spawnHeightMultiplierRange)
        {
            position = new Vector2(
                Random.Range(0, width),
                Random.Range(
                    height * spawnHeightMultiplierRange.MinInclusive,
                    height * spawnHeightMultiplierRange.MaxExclusive));
        }
    }
}

/// <summary>
/// Editable no-music values saved as Waterfall's Standalone Settings and restored from its authored
/// Standalone Defaults.
/// </summary>
[Serializable]
public sealed class WaterfallStandaloneSettings
{
    /// <summary>Inclusive-minimum/exclusive-maximum number of droplets rolled per activation.</summary>
    public IntRange DropletCount;

    /// <summary>Per-activation palette spread across screen columns.</summary>
    public FloatRange PaletteSpread;

    /// <summary>Per-activation speed range for the palette wash.</summary>
    public FloatRange PaletteSpeed;

    /// <summary>Per-activation width range for the overlapping vertical streams.</summary>
    public FloatRange StreamWidth;

    /// <summary>Per-activation downward speed range for the flowing stream structure.</summary>
    public FloatRange StreamFallSpeed;

    /// <summary>Maximum water-body value before droplets brighten it.</summary>
    [Range(0f, 1f)]
    public float WaterBrightness;

    /// <summary>Luminance separation between the bright and dark streams.</summary>
    [Range(0f, 1f)]
    public float StreamContrast;

    /// <summary>Strength of the dark seams carved along the stream boundaries.</summary>
    [Range(0f, 1f)]
    public float StreamEdgeShade;

    /// <summary>Floor under the water's composed value so no roll goes pit-dark.</summary>
    [Range(0f, 1f)]
    public float WaterMinBrightness;

    /// <summary>Inclusive-lower/exclusive-upper screen-height multiplier range for droplet spawns.</summary>
    public IntRange DropletSpawnHeightMultiplier;

    /// <summary>Per-droplet radius range.</summary>
    public FloatRange DropletRadius;

    /// <summary>Per-droplet falling-speed range.</summary>
    public FloatRange DropletSpeed;

    /// <summary>Per-droplet value-space brightness range.</summary>
    public FloatRange DropletBrightness;

    /// <summary>Distance over which each droplet trail tapers to darkness.</summary>
    [Min(0.0001f)]
    public float DropletTrailLength;

    /// <summary>Fraction of droplet brightness applied at the head of each tapered trail.</summary>
    [Range(0f, 1f)]
    public float DropletTrailBrightness;

    /// <summary>Screen-height multiplier below the wall where a droplet respawns.</summary>
    public int DropletRespawnHeightMultiplier;

    /// <summary>Copies every Waterfall Standalone Setting from another value.</summary>
    public void CopyFrom(WaterfallStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        DropletCount = new IntRange(
            source.DropletCount.MinInclusive,
            source.DropletCount.MaxExclusive,
            source.DropletCount.LowRail,
            source.DropletCount.HighRail);
        PaletteSpread = new FloatRange(
            source.PaletteSpread.Min,
            source.PaletteSpread.Max,
            source.PaletteSpread.LowRail,
            source.PaletteSpread.HighRail);
        PaletteSpeed = new FloatRange(
            source.PaletteSpeed.Min,
            source.PaletteSpeed.Max,
            source.PaletteSpeed.LowRail,
            source.PaletteSpeed.HighRail);
        StreamWidth = new FloatRange(
            source.StreamWidth.Min,
            source.StreamWidth.Max,
            source.StreamWidth.LowRail,
            source.StreamWidth.HighRail);
        StreamFallSpeed = new FloatRange(
            source.StreamFallSpeed.Min,
            source.StreamFallSpeed.Max,
            source.StreamFallSpeed.LowRail,
            source.StreamFallSpeed.HighRail);
        WaterBrightness = source.WaterBrightness;
        StreamContrast = source.StreamContrast;
        StreamEdgeShade = source.StreamEdgeShade;
        WaterMinBrightness = source.WaterMinBrightness;
        DropletSpawnHeightMultiplier = new IntRange(
            source.DropletSpawnHeightMultiplier.MinInclusive,
            source.DropletSpawnHeightMultiplier.MaxExclusive,
            source.DropletSpawnHeightMultiplier.LowRail,
            source.DropletSpawnHeightMultiplier.HighRail);
        DropletRadius = new FloatRange(
            source.DropletRadius.Min,
            source.DropletRadius.Max,
            source.DropletRadius.LowRail,
            source.DropletRadius.HighRail);
        DropletSpeed = new FloatRange(
            source.DropletSpeed.Min,
            source.DropletSpeed.Max,
            source.DropletSpeed.LowRail,
            source.DropletSpeed.HighRail);
        DropletBrightness = new FloatRange(
            source.DropletBrightness.Min,
            source.DropletBrightness.Max,
            source.DropletBrightness.LowRail,
            source.DropletBrightness.HighRail);
        DropletTrailLength = source.DropletTrailLength;
        DropletTrailBrightness = source.DropletTrailBrightness;
        DropletRespawnHeightMultiplier = source.DropletRespawnHeightMultiplier;
    }
}

/// <summary>Editable music-response and Synced Mode values saved as Waterfall's Sync Settings.</summary>
[Serializable]
public sealed class WaterfallSyncSettings
{
    /// <summary>
    /// Tempo at which BPM contributes a neutral one-times multiplier to the rolled stream speed.
    /// </summary>
    [Min(0.0001f)] public float ReferenceBpm;

    /// <summary>
    /// Low- and High-Energy stream-speed multipliers; Mid uses their midpoint, and the current
    /// Energy run's Trend and Progress move continuously between adjacent tiers.
    /// </summary>
    public FloatRange EnergySpeedMultiplier;

    /// <summary>
    /// Multiplicative value-space lift at a stream surge's center; color stays owned by the
    /// unchanged palette wash.
    /// </summary>
    [Min(0f)] public float StreamSurgeBrightness;

    /// <summary>Full vertical length of each moving stream surge, in screen pixels.</summary>
    [Min(0.0001f)] public float StreamSurgeLength;

    /// <summary>
    /// Surge width relative to rolled StreamWidth. Above one the strike spans several streams as a
    /// broad wall hit; the profiled field and edge seams keep the structure visible inside it.
    /// </summary>
    [Min(0.0001f)] public float StreamSurgeWidthMultiplier;

    /// <summary>
    /// Surge fall speed relative to the water's current BPM × Energy speed; above one, each beat
    /// strike visibly outruns the stream it brightens.
    /// </summary>
    [Min(0.0001f)] public float StreamSurgeSpeedMultiplier;

    /// <summary>
    /// Which form of the Levels reading the low-band surge gate consults; renders as an Inspector
    /// dropdown so the reading can be flipped live on the wall.
    /// </summary>
    public enum SurgeLevelReading
    {
        /// <summary>The instantaneous wire value — high only while the hit is actually sounding.</summary>
        Normalized,

        /// <summary>The attack/release follower — steadier, but lags the hit.</summary>
        Smoothed,

        /// <summary>Instant rise with a tempo-based linear fall — holds on after the hit fades.</summary>
        Peak,
    }

    /// <summary>
    /// Minimum low-band level, in the reading StreamSurgeLowLevelReading selects, that admits a
    /// beat surge; counts without bass presence launch nothing.
    /// </summary>
    [Range(0f, 1f)] public float StreamSurgeLowLevelThreshold;

    /// <summary>Levels reading the low-band surge gate reads its value from.</summary>
    public SurgeLevelReading StreamSurgeLowLevelReading;

    /// <summary>
    /// Fraction of the screen width at each edge where no surge stream may sit; keeps strikes
    /// visible on the wall. Applied when stream identities are derived at Roll, so a live edit
    /// takes effect on the next activation.
    /// </summary>
    [Range(0f, 0.4f)] public float StreamSurgeEdgeMargin;

    /// <summary>Inclusive-minimum/exclusive-maximum number of droplets rolled per activation.</summary>
    public IntRange DropletCount;

    /// <summary>Palette-spread range rolled across screen columns.</summary>
    public FloatRange PaletteSpread;

    /// <summary>Palette-wash speed range rolled per activation.</summary>
    public FloatRange PaletteSpeed;

    /// <summary>Width range rolled for the overlapping vertical streams.</summary>
    public FloatRange StreamWidth;

    /// <summary>Downward speed range rolled for the flowing stream structure.</summary>
    public FloatRange StreamFallSpeed;

    /// <summary>Maximum water-body value before droplets or surges brighten it.</summary>
    [Range(0f, 1f)] public float WaterBrightness;

    /// <summary>Luminance separation between the bright and dark streams.</summary>
    [Range(0f, 1f)] public float StreamContrast;

    /// <summary>Strength of the dark seams carved along the stream boundaries.</summary>
    [Range(0f, 1f)] public float StreamEdgeShade;

    /// <summary>Floor under the water's composed value so no roll goes pit-dark.</summary>
    [Range(0f, 1f)] public float WaterMinBrightness;

    /// <summary>Inclusive-lower/exclusive-upper screen-height multiplier range for droplet spawns.</summary>
    public IntRange DropletSpawnHeightMultiplier;

    /// <summary>Radius range rolled for each droplet.</summary>
    public FloatRange DropletRadius;

    /// <summary>Falling-speed range rolled for each droplet.</summary>
    public FloatRange DropletSpeed;

    /// <summary>Value-space brightness range rolled for each droplet.</summary>
    public FloatRange DropletBrightness;

    /// <summary>Distance over which each droplet trail tapers to darkness.</summary>
    [Min(0.0001f)] public float DropletTrailLength;

    /// <summary>Fraction of droplet brightness applied at the head of each tapered trail.</summary>
    [Range(0f, 1f)] public float DropletTrailBrightness;

    /// <summary>Screen-height multiplier below the wall where a droplet respawns.</summary>
    public int DropletRespawnHeightMultiplier;

    /// <summary>Copies every Waterfall Sync Setting from another value.</summary>
    public void CopyFrom(WaterfallSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ReferenceBpm = source.ReferenceBpm;
        EnergySpeedMultiplier = new FloatRange(
            source.EnergySpeedMultiplier.Min,
            source.EnergySpeedMultiplier.Max,
            source.EnergySpeedMultiplier.LowRail,
            source.EnergySpeedMultiplier.HighRail);
        StreamSurgeBrightness = source.StreamSurgeBrightness;
        StreamSurgeLength = source.StreamSurgeLength;
        StreamSurgeWidthMultiplier = source.StreamSurgeWidthMultiplier;
        StreamSurgeSpeedMultiplier = source.StreamSurgeSpeedMultiplier;
        StreamSurgeLowLevelThreshold = source.StreamSurgeLowLevelThreshold;
        StreamSurgeLowLevelReading = source.StreamSurgeLowLevelReading;
        StreamSurgeEdgeMargin = source.StreamSurgeEdgeMargin;
        DropletCount = new IntRange(
            source.DropletCount.MinInclusive,
            source.DropletCount.MaxExclusive,
            source.DropletCount.LowRail,
            source.DropletCount.HighRail);
        PaletteSpread = new FloatRange(
            source.PaletteSpread.Min,
            source.PaletteSpread.Max,
            source.PaletteSpread.LowRail,
            source.PaletteSpread.HighRail);
        PaletteSpeed = new FloatRange(
            source.PaletteSpeed.Min,
            source.PaletteSpeed.Max,
            source.PaletteSpeed.LowRail,
            source.PaletteSpeed.HighRail);
        StreamWidth = new FloatRange(
            source.StreamWidth.Min,
            source.StreamWidth.Max,
            source.StreamWidth.LowRail,
            source.StreamWidth.HighRail);
        StreamFallSpeed = new FloatRange(
            source.StreamFallSpeed.Min,
            source.StreamFallSpeed.Max,
            source.StreamFallSpeed.LowRail,
            source.StreamFallSpeed.HighRail);
        WaterBrightness = source.WaterBrightness;
        StreamContrast = source.StreamContrast;
        StreamEdgeShade = source.StreamEdgeShade;
        WaterMinBrightness = source.WaterMinBrightness;
        DropletSpawnHeightMultiplier = new IntRange(
            source.DropletSpawnHeightMultiplier.MinInclusive,
            source.DropletSpawnHeightMultiplier.MaxExclusive,
            source.DropletSpawnHeightMultiplier.LowRail,
            source.DropletSpawnHeightMultiplier.HighRail);
        DropletRadius = new FloatRange(
            source.DropletRadius.Min,
            source.DropletRadius.Max,
            source.DropletRadius.LowRail,
            source.DropletRadius.HighRail);
        DropletSpeed = new FloatRange(
            source.DropletSpeed.Min,
            source.DropletSpeed.Max,
            source.DropletSpeed.LowRail,
            source.DropletSpeed.HighRail);
        DropletBrightness = new FloatRange(
            source.DropletBrightness.Min,
            source.DropletBrightness.Max,
            source.DropletBrightness.LowRail,
            source.DropletBrightness.HighRail);
        DropletTrailLength = source.DropletTrailLength;
        DropletTrailBrightness = source.DropletTrailBrightness;
        DropletRespawnHeightMultiplier = source.DropletRespawnHeightMultiplier;
    }
}
