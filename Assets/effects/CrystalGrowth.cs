using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Grows bright crystal fronts that sweep tile-to-tile across the Penrose adjacency graph, leaving a
/// persistent colored glow behind them.
/// </summary>
/// <remarks>
/// Crystal Growth draws bright, full-value crystals on black negative space — the same range the other wall
/// effects use (lit tiles at full palette value, everything else black), so it reads vivid instead of muddy:
/// <list type="bullet">
/// <item><description><b>Black</b> is the resting state: any tile a crystal has not reached is pure black
///   negative space.</description></item>
/// <item><description>A <b>crystal</b> lights its tiles at the full palette color, brightest at the front and
///   fading behind it to a dim floor (never to black), so earlier growth lingers as a visible layer.</description></item>
/// <item><description>The <b>growing front</b> is the bright leading edge sweeping outward along the real
///   (aperiodic) tile graph, claiming each tile it touches and whitening slightly at the very tip. This is what
///   reads as a crystal growing, and it is what the beat drives.</description></item>
/// </list>
///
/// Behind the front, each grown tile eases its color toward its same-layer neighbors so the many colliding
/// crystal colors relax into gradients rather than muddled seams.
///
/// Color travels with the front: a tile takes the front's generation color the instant the crystal reaches
/// it. A newer generation's front always wins, so a new layer sweeps over and repaints the still-glowing
/// layers beneath it as a visible bright wave.
///
/// Sync (the headliner) vs. Standalone (a sensible default) ride one mechanic — the front always advances
/// off <see cref="EffectBase.effectDelta"/> so the wall never freezes. The beat only modulates:
/// <list type="bullet">
/// <item><description>WHEN seeds spawn — Synced combines the wire-authored On Beat window with a selectable
///   <see cref="BeatManager.Levels"/> Low form used as this Effect's bass-presence proxy. Each window can launch
///   at most one continuously sized burst, with two extra seeds on The One. Standalone keeps its self-driven
///   metronome (a steady trickle plus a synthetic downbeat bloom) so several fronts always crawl at once.</description></item>
/// <item><description>THE DROP — the Drop Active rising edge fires a one-shot flash: a fresh single-color layer is
///   surged across the whole wall as a bright colored wavefront (the luminance lift rides the sweeping leading
///   edge, in palette color, never white). Its response follows the current Grid's Decay and disarms at the next
///   observed Grid boundary, so the Drop lands as one dramatic sweep that resolves into normal growth.</description></item>
/// <item><description>THE FILL — ordinary seeding and propagation pause while the visible current generation
///   retracts newest claim first, revealing the prior generation and hue beneath each Tile. The restored state
///   remains after the Fill while the existing whole-field brightness swell accents the peel-back.</description></item>
/// <item><description>HOW FAST the front moves — selected-form Average Levels and current Energy scale the
///   continuous Synced pace, while <see cref="PulsesValues.Beat"/> supplies an independent accent multiplied only
///   by remapped Low presence. Standalone keeps its Perlin-varied self-driven surge exactly.</description></item>
/// <item><description>OVERALL brightness — selected-form Average Levels retains its brightness-depth role while
///   this Effect evaluates its held Waveform and maps the envelope locally; clockless rendering holds steady.</description></item>
/// <item><description>ACTIVITY — the selected Average form scales Synced continuous growth and its idle-seed
///   clock from the authored low-activity pace to the full-activity baseline. It never decides whether an On Beat
///   exists, and Levels remains a live-set aggregate rather than an absolute loudness meter.</description></item>
/// <item><description>ENERGY — the current Low/Mid/High run scales continuous Synced pace across its authored
///   range; unavailable Energy is neutral and does not multiply the independent beat accent.</description></item>
/// <item><description>PALETTE — a fresh wall palette is selected at every phrase-relative Grid boundary.</description></item>
/// </list>
/// </remarks>
[EffectSyncSettings(typeof(CrystalGrowthSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(CrystalGrowthStandaloneSettingsAsset))]
public class CrystalGrowth : EffectBase
{
    // Standalone Defaults

    /// <summary>Front heat below this is treated as cold and stops advancing the rim (the tail dies here).</summary>
    private const float StandaloneHeatEpsilon = 0.01f;

    /// <summary>Fraction of its heat the front carries into the next ring; near 1 keeps the leading edge bright as it travels.</summary>
    private const float StandaloneFrontPush = 0.95f;

    /// <summary>Fraction of the wall the current generation must claim before the next layer blooms on top.</summary>
    private const float StandaloneCoverageToAdvance = 0.85f;

    /// <summary>Cap on ring passes advanced in one frame, so a long frame hitch catches up a little rather than detonating the front across the whole wall in a single step.</summary>
    private const int StandaloneMaxFrontPassesPerFrame = 6;

    /// <summary>A grown tile's brightness never fades below this — the crystal lingers as a dim layer instead of going black. Unreached tiles still render pure black.</summary>
    private const float StandaloneCrystalFloor = 0.5f;

    /// <summary>How fast a grown tile eases its color toward its same-layer neighbors, softening crystal seams.</summary>
    private const float StandaloneHueRelaxPerSec = 0.6f;

    /// <summary>Maximum hue relaxation applied during one frame, so a long frame hitch can't over-relax in one step.</summary>
    private const float StandaloneHueRelaxMaxPerFrame = 0.5f;

    /// <summary>Smoothed Average Levels value mapped to minimum activity by the self-driven spread machinery (a third minus a soft band).</summary>
    private const float StandaloneActivityLevelMin = 0.233f;

    /// <summary>Smoothed Average Levels value mapped to full activity by the self-driven spread machinery (a third plus a soft band).</summary>
    private const float StandaloneActivityLevelMax = 0.433f;

    /// <summary>Golden-ratio conjugate: the step that spaces successive seed colors evenly across the palette.</summary>
    private const float StandaloneGoldenStep = 0.618034f;

    /// <summary>Authored minimum front spread rolled for each activation.</summary>
    private const float StandaloneSpreadPerSecMin = 12f;

    /// <summary>Authored maximum front spread rolled for each activation.</summary>
    private const float StandaloneSpreadPerSecMax = 20f;

    /// <summary>Authored minimum front leak rolled for each activation.</summary>
    private const float StandaloneLeakPerSecMin = 0.22f;

    /// <summary>Authored maximum front leak rolled for each activation.</summary>
    private const float StandaloneLeakPerSecMax = 0.5f;

    /// <summary>Authored minimum spread-surge multiple rolled for each activation.</summary>
    private const float StandaloneBeatSurgeMin = 1.5f;

    /// <summary>Authored maximum spread-surge multiple rolled for each activation.</summary>
    private const float StandaloneBeatSurgeMax = 3.5f;

    /// <summary>Minimum seconds between idle seeds in Standalone Mode, where the self-metronome drives all growth.</summary>
    private const float StandaloneSeedIntervalMin = 0.18f;

    /// <summary>Maximum seconds between idle seeds in Standalone Mode, where the self-metronome drives all growth.</summary>
    private const float StandaloneSeedIntervalMax = 0.35f;

    /// <summary>Minimum seconds per synthetic Standalone downbeat (the period is re-jittered on every downbeat so it never feels mechanical).</summary>
    private const float StandaloneSelfBeatPeriodMin = 1.2f;

    /// <summary>Maximum seconds per synthetic Standalone downbeat (the period is re-jittered on every downbeat so it never feels mechanical).</summary>
    private const float StandaloneSelfBeatPeriodMax = 2.2f;

    /// <summary>Per-second decay of the Standalone spread surge, so each synthetic downbeat is a lunge, not a sustained sprint.</summary>
    private const float StandaloneSelfPulseDecayPerSec = 2.5f;

    /// <summary>Weakest Perlin-mapped Standalone spread-surge peak, so even a soft synthetic downbeat still advances the front.</summary>
    private const float StandaloneSelfPulsePeakMin = 0.55f;

    /// <summary>Strongest Perlin-mapped Standalone spread-surge peak, preserving the full existing synthetic downbeat lunge.</summary>
    private const float StandaloneSelfPulsePeakMax = 1f;

    /// <summary>Per-second speed through activation-local Perlin space, so adjacent synthetic downbeats vary smoothly instead of jumping independently.</summary>
    private const float StandaloneSelfPulseNoiseSpeed = 0.2f;

    /// <summary>Authored heat threshold at which the crystal tip begins whitening.</summary>
    private const float StandaloneTipThreshold = 0.8f;

    /// <summary>Authored maximum amount of white mixed into the crystal tip.</summary>
    private const float StandaloneTipWhitenAmount = 0.5f;

    /// <summary>Authored base seed count added to every randomly varied bloom.</summary>
    private const int StandaloneBloomCountBase = 3;

    /// <summary>Authored inclusive minimum random offset added to a bloom's base seed count.</summary>
    private const int StandaloneBloomCountOffsetMinInclusive = 0;

    /// <summary>Authored exclusive maximum random offset added to a bloom's base seed count.</summary>
    private const int StandaloneBloomCountOffsetMaxExclusive = 3;

    // Sync Defaults

    /// <summary>The selected-form Low consumer reads Normalized Levels by default.</summary>
    private const CrystalGrowthSyncSettings.LevelsForm SyncLowLevelsForm =
        CrystalGrowthSyncSettings.LevelsForm.Normalized;

    /// <summary>The selected-form Average consumer reads Normalized Levels by default.</summary>
    private const CrystalGrowthSyncSettings.LevelsForm SyncActivityLevelsForm =
        CrystalGrowthSyncSettings.LevelsForm.Normalized;

    /// <summary>Selected-form Low value above which Crystal Growth's bass-presence proxy qualifies an open On Beat window (a third).</summary>
    private const float SyncLowPresenceThreshold = 1f / 3f;

    /// <summary>Selected-form Average Levels value mapped to minimum broad-spectrum activity (a third minus a soft band).</summary>
    private const float SyncActivityLevelMin = 0.233f;

    /// <summary>Selected-form Average Levels value mapped to full broad-spectrum activity (a third plus a soft band).</summary>
    private const float SyncActivityLevelMax = 0.433f;

    /// <summary>Continuous Synced growth pace at minimum selected-form Average activity.</summary>
    private const float SyncQuietGrowthMultiplier = 0.5f;

    /// <summary>Continuous Synced growth pace for a Low Energy run.</summary>
    private const float SyncEnergyPaceLow = 1f;

    /// <summary>Continuous Synced growth pace for a High Energy run; Mid uses the range midpoint.</summary>
    private const float SyncEnergyPaceHigh = 2f;

    /// <summary>Peak luminance gain on the Drop wavefront — weighted by front heat so only the sweeping leading edge brightens, in the tile's own palette color (never toward white). Tune on the DROP RESPONSE readout.</summary>
    private const float SyncDropFlashBrightness = 0.75f;

    /// <summary>Extra spread multiplier at the peak of the Drop flash, so the fresh layer sweeps briskly across the wall. Tune on the DROP FLASH readout.</summary>
    private const float SyncDropFlashSpread = 2f;

    /// <summary>Seeds of the fresh Drop layer planted at the flash onset, sharing one hue so they read as a single colored wave.</summary>
    private const int SyncDropFlashSeeds = 3;

    /// <summary>Minimum seconds between Synced idle seeds; Low-qualified On Beat bursts provide the primary origins.</summary>
    private const float SyncIdleSeedIntervalMin = 0.5f;

    /// <summary>Maximum seconds between Synced idle seeds; Low-qualified On Beat bursts provide the primary origins.</summary>
    private const float SyncIdleSeedIntervalMax = 0.9f;

    /// <summary>Extra spread multiplier added on each sixteenth's on-phase during a Drop — the front lunges in stutters.</summary>
    private const float SyncDropRatchetSpread = 4f;

    /// <summary>How far the whole field is knocked toward black on a Drop sixteenth's off-phase — the hard strobe depth.</summary>
    private const float SyncDropStrobeDepth = 0.9f;

    /// <summary>Maximum seeds planted on each sixteenth onset, fading across the complete Grid-bound Drop response.</summary>
    private const int SyncDropSeedBurst = 12;

    /// <summary>Luminance swell across the whole grown crystal at full Fill, accenting the current generation's retraction. Tune on the FILL readout.</summary>
    private const float SyncFillSwell = 0.35f;

    /// <summary>Authored brightness floor while selected-form Average Levels are at full activity.</summary>
    private const float SyncDrivingBrightnessFloor = 0.8f;

    /// <summary>Authored minimum seed burst produced by qualifying Low presence in an On Beat window.</summary>
    private const float SyncLowSeedBurstMin = 2f;

    /// <summary>Authored maximum seed burst produced by qualifying Low presence in an On Beat window.</summary>
    private const float SyncLowSeedBurstMax = 6f;

    /// <summary>Authored extra seed count added on beat one of a bar.</summary>
    private const int SyncDownbeatSeedBonus = 2;

    /// <summary>Front heat below which the Synced growing rim stops advancing.</summary>
    private const float SyncHeatEpsilon = 0.01f;

    /// <summary>Fraction of front heat carried into the next adjacency ring in Synced Mode.</summary>
    private const float SyncFrontPush = 0.95f;

    /// <summary>Fraction of the wall claimed before the next Synced generation blooms.</summary>
    private const float SyncCoverageToAdvance = 0.85f;

    /// <summary>
    /// Maximum front passes advanced during one Synced frame, so a long frame hitch catches up a
    /// little rather than detonating the front across the whole wall in a single step.
    /// </summary>
    private const int SyncMaxFrontPassesPerFrame = 6;

    /// <summary>Minimum brightness retained by every grown Tile in Synced Mode.</summary>
    private const float SyncCrystalFloor = 0.5f;

    /// <summary>Per-second rate for relaxing same-layer hue seams in Synced Mode.</summary>
    private const float SyncHueRelaxPerSec = 0.6f;

    /// <summary>
    /// Maximum Synced hue relaxation applied during one frame, so a long frame hitch cannot
    /// over-relax in one step.
    /// </summary>
    private const float SyncHueRelaxMaxPerFrame = 0.5f;

    /// <summary>Golden-ratio palette step between successive Synced seed colors.</summary>
    private const float SyncGoldenStep = 0.618034f;

    /// <summary>Authored minimum front spread rolled for a Synced activation.</summary>
    private const float SyncSpreadPerSecMin = 12f;

    /// <summary>Authored maximum front spread rolled for a Synced activation.</summary>
    private const float SyncSpreadPerSecMax = 20f;

    /// <summary>Authored minimum front leak rolled for a Synced activation.</summary>
    private const float SyncLeakPerSecMin = 0.22f;

    /// <summary>Authored maximum front leak rolled for a Synced activation.</summary>
    private const float SyncLeakPerSecMax = 0.5f;

    /// <summary>Authored minimum beat-surge multiple rolled for a Synced activation.</summary>
    private const float SyncBeatSurgeMin = 1.5f;

    /// <summary>Authored maximum beat-surge multiple rolled for a Synced activation.</summary>
    private const float SyncBeatSurgeMax = 3.5f;

    /// <summary>Front heat at which the Synced crystal tip begins whitening.</summary>
    private const float SyncTipThreshold = 0.8f;

    /// <summary>Maximum amount of white mixed into the Synced crystal tip.</summary>
    private const float SyncTipWhitenAmount = 0.5f;

    /// <summary>Base seed count added to every randomly varied Synced bloom.</summary>
    private const int SyncBloomCountBase = 3;

    /// <summary>Inclusive minimum random offset added to a Synced bloom's base seed count.</summary>
    private const int SyncBloomCountOffsetMinInclusive = 0;

    /// <summary>Exclusive maximum random offset added to a Synced bloom's base seed count.</summary>
    private const int SyncBloomCountOffsetMaxExclusive = 3;

    // Runtime mechanism constants

    /// <summary>Drop amount at which normal coverage-based generation advance begins overlapping the release.</summary>
    private const float DropGenerationOverlapAmount = 0.5f;

    /// <summary>Crystal Growth expresses both phrase cues: the Fill retraction and swell build into the change,
    /// and the Drop downbeat fires its one-shot whole-wall surge. Advertise both so the Director can deliberately
    /// cast it into Fill and Drop moments, not only react when it happens to be on-air. Its growth stays calm and
    /// eases off as selected-form Average activity falls, so it advertises as a Low/Mid-energy Performer.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate Crystal Growth's authored
    /// Standalone Defaults.
    /// </summary>
    public static CrystalGrowthStandaloneSettings StandaloneDefaults => new()
    {
        HeatEpsilon = StandaloneHeatEpsilon,
        FrontPush = StandaloneFrontPush,
        CoverageToAdvance = StandaloneCoverageToAdvance,
        MaxFrontPassesPerFrame = StandaloneMaxFrontPassesPerFrame,
        CrystalFloor = StandaloneCrystalFloor,
        HueRelaxPerSec = StandaloneHueRelaxPerSec,
        HueRelaxMaxPerFrame = StandaloneHueRelaxMaxPerFrame,
        ActivityLevel = new FloatRange(StandaloneActivityLevelMin, StandaloneActivityLevelMax),
        GoldenStep = StandaloneGoldenStep,
        SpreadPerSec = new FloatRange(StandaloneSpreadPerSecMin, StandaloneSpreadPerSecMax),
        LeakPerSec = new FloatRange(StandaloneLeakPerSecMin, StandaloneLeakPerSecMax),
        BeatSurge = new FloatRange(StandaloneBeatSurgeMin, StandaloneBeatSurgeMax),
        SeedInterval = new FloatRange(StandaloneSeedIntervalMin, StandaloneSeedIntervalMax),
        SelfBeatPeriod = new FloatRange(StandaloneSelfBeatPeriodMin, StandaloneSelfBeatPeriodMax),
        SelfPulsePeak = new FloatRange(StandaloneSelfPulsePeakMin, StandaloneSelfPulsePeakMax),
        SelfPulseNoiseSpeed = StandaloneSelfPulseNoiseSpeed,
        SelfPulseDecayPerSec = StandaloneSelfPulseDecayPerSec,
        TipThreshold = StandaloneTipThreshold,
        TipWhitenAmount = StandaloneTipWhitenAmount,
        BloomCountBase = StandaloneBloomCountBase,
        BloomCountOffset = new IntRange(
            StandaloneBloomCountOffsetMinInclusive,
            StandaloneBloomCountOffsetMaxExclusive),
    };

    /// <summary>Resolves a fresh copy of Crystal Growth's file-local Sync Defaults.</summary>
    public static CrystalGrowthSyncSettings SyncDefaults => new()
    {
        LowLevelsForm = SyncLowLevelsForm,
        ActivityLevelsForm = SyncActivityLevelsForm,
        LowPresenceThreshold = SyncLowPresenceThreshold,
        ActivityLevel = new FloatRange(SyncActivityLevelMin, SyncActivityLevelMax),
        QuietGrowthMultiplier = SyncQuietGrowthMultiplier,
        EnergyPace = new FloatRange(SyncEnergyPaceLow, SyncEnergyPaceHigh),
        DropFlashBrightness = SyncDropFlashBrightness,
        DropFlashSpread = SyncDropFlashSpread,
        DropFlashSeeds = SyncDropFlashSeeds,
        IdleSeedInterval = new FloatRange(SyncIdleSeedIntervalMin, SyncIdleSeedIntervalMax),
        DropRatchetSpread = SyncDropRatchetSpread,
        DropStrobeDepth = SyncDropStrobeDepth,
        DropSeedBurst = SyncDropSeedBurst,
        FillSwell = SyncFillSwell,
        DrivingBrightnessFloor = SyncDrivingBrightnessFloor,
        LowSeedBurst = new FloatRange(SyncLowSeedBurstMin, SyncLowSeedBurstMax),
        DownbeatSeedBonus = SyncDownbeatSeedBonus,
        HeatEpsilon = SyncHeatEpsilon,
        FrontPush = SyncFrontPush,
        CoverageToAdvance = SyncCoverageToAdvance,
        MaxFrontPassesPerFrame = SyncMaxFrontPassesPerFrame,
        CrystalFloor = SyncCrystalFloor,
        HueRelaxPerSec = SyncHueRelaxPerSec,
        HueRelaxMaxPerFrame = SyncHueRelaxMaxPerFrame,
        GoldenStep = SyncGoldenStep,
        SpreadPerSec = new FloatRange(SyncSpreadPerSecMin, SyncSpreadPerSecMax),
        LeakPerSec = new FloatRange(SyncLeakPerSecMin, SyncLeakPerSecMax),
        BeatSurge = new FloatRange(SyncBeatSurgeMin, SyncBeatSurgeMax),
        TipThreshold = SyncTipThreshold,
        TipWhitenAmount = SyncTipWhitenAmount,
        BloomCountBase = SyncBloomCountBase,
        BloomCountOffset = new IntRange(
            SyncBloomCountOffsetMinInclusive,
            SyncBloomCountOffsetMaxExclusive),
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private CrystalGrowthStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private CrystalGrowthSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Per-tile front heat in [0..1]; the bright moving band. Decays toward 0, but a grown tile still renders at the Standalone Settings floor (keyed on <see cref="gen"/>), so charge is only the bright part above the floor.</summary>
    private float[] charge;

    /// <summary>Per-tile palette position in [0..1]; the color the claiming generation painted onto the tile.</summary>
    private float[] hue;

    /// <summary>Per-tile generation index that has claimed the tile; 0 = never grown (black). Higher always wins.</summary>
    private int[] gen;

    /// <summary>Double-buffer targets for one front pass, swapped in after each step.</summary>
    private float[] nextCharge;
    private float[] nextHue;
    private int[] nextGen;

    /// <summary>Tile indexes in the order the current generation first claimed them.</summary>
    private int[] currentGenerationClaimOrder;

    /// <summary>Generation revealed when the matching current-generation claim is retracted.</summary>
    private int[] priorGenerationByClaim;

    /// <summary>Hue revealed when the matching current-generation claim is retracted.</summary>
    private float[] priorHueByClaim;

    /// <summary>Number of retained claims in the current generation's bounded history.</summary>
    private int currentGenerationClaimCount;

    /// <summary>Current-generation claim count captured at the Fill's rising edge.</summary>
    private int fillStartClaimCount;

    /// <summary>Rings-per-second the front advances; how fast a crystal sweeps across the wall.</summary>
    private float spreadPerSec;

    /// <summary>Fraction of front brightness lost per second; sets how fast the trail fades from the bright front down to the floor.</summary>
    private float leakPerSec;

    /// <summary>Extra spread multiplier applied at the peak of <see cref="PulsesValues.Beat"/>.</summary>
    private float beatSurge;

    /// <summary>
    /// Accumulated fractional front passes carried between frames for framerate-independent spread;
    /// Synced Mode discards stale whole-pass debt after reaching its per-frame cap.
    /// </summary>
    private float spreadBudget;

    /// <summary>The current (newest) generation index. Each new layer increments this.</summary>
    private int generation;

    /// <summary>
    /// Rolling palette position [0..1]. Every seed steps it by the golden ratio before claiming its color, so
    /// successive crystals land on well-separated palette colors and the wall fills with many hues at once.
    /// </summary>
    private float hueCursor;

    /// <summary>Seconds accumulated toward the next mode-specific idle seed.</summary>
    private float seedTimer;

    /// <summary>Target seconds between mode-specific idle seeds.</summary>
    private float seedInterval;

    /// <summary>Standalone-only self-driven metronome phase [0..1); wraps to a synthetic "downbeat" bloom.</summary>
    private float selfBeatPhase;

    /// <summary>Standalone-only seconds per synthetic downbeat (re-jittered each tick so it never feels mechanical).</summary>
    private float selfBeatPeriod;

    /// <summary>Standalone-only Perlin coordinate initialized from the Roll-randomized effect time and advanced from effect delta.</summary>
    private float selfPulseNoisePosition;

    /// <summary>Standalone-only spread surge envelope, reset to a Perlin-mapped peak on each synthetic downbeat and decayed linearly to zero.</summary>
    private float selfPulse;

    /// <summary>Whether the Grid-bound Drop response remains armed until the next observed Grid boundary.</summary>
    private bool dropResponseActive;

    /// <summary>Current Grid Decay while the Drop response is armed; drives its release-shaped consequences.</summary>
    private float dropResponseAmount;

    /// <summary>Previous Drop Active value retained to detect only its rising edge.</summary>
    private bool previousDropActive;

    /// <summary>Previous sixteenth-gate value retained to detect each sixteenth onset.</summary>
    private bool previousSixteenthOn;

    /// <summary>This frame's selected-form Low Levels value used as Crystal Growth's bass-presence proxy.</summary>
    private float lowLevel;

    /// <summary>This frame's selected-form Average Levels value used for broad-spectrum activity.</summary>
    private float averageLevel;

    /// <summary>Selected-form Low remapped from the authored threshold to full bass presence.</summary>
    private float lowPresence;

    /// <summary>Whether the current wire-authored On Beat window already launched its Low-qualified seed burst.</summary>
    private bool seededThisOnBeatWindow;

    /// <summary>Whether this frame is inside a fill, kept for the debug readout.</summary>
    private bool fillActive;

    /// <summary>This frame's continuous linear Fill Build position [0..1], kept for the debug readout.</summary>
    private float fillLevel;

    /// <summary>
    /// Allocates the per-tile state buffers once. Sizes follow <see cref="Penrose.Total"/>.
    /// </summary>
    public override void Init()
    {
        base.Init();
        charge = new float[Penrose.Total];
        hue = new float[Penrose.Total];
        gen = new int[Penrose.Total];
        nextCharge = new float[Penrose.Total];
        nextHue = new float[Penrose.Total];
        nextGen = new int[Penrose.Total];
        currentGenerationClaimOrder = new int[Penrose.Total];
        priorGenerationByClaim = new int[Penrose.Total];
        priorHueByClaim = new float[Penrose.Total];
    }

    /// <summary>
    /// Resets the field and randomizes this run's growth personality, then plants one seed so the first
    /// frame is never blank.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(CrystalGrowth),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(CrystalGrowth),
            SyncDefaults);
        waveform = waveforms.Random();

        Array.Clear(charge, 0, charge.Length);
        Array.Clear(hue, 0, hue.Length);
        Array.Clear(gen, 0, gen.Length);

        // Per-activation variety: faster spread with a sharper leak reads as a crisp racing front; slower
        // spread with a gentler leak reads as a thick, creeping bloom.
        bool isSynced = beatManager.IsSynced;
        FloatRange spreadPerSecRange = isSynced ? SyncSettings.SpreadPerSec : standaloneSettings.SpreadPerSec;
        FloatRange leakPerSecRange = isSynced ? SyncSettings.LeakPerSec : standaloneSettings.LeakPerSec;
        FloatRange beatSurgeRange = isSynced ? SyncSettings.BeatSurge : standaloneSettings.BeatSurge;
        FloatRange seedIntervalRange = isSynced
            ? SyncSettings.IdleSeedInterval
            : standaloneSettings.SeedInterval;
        spreadPerSec = Random.Range(spreadPerSecRange.Min, spreadPerSecRange.Max);
        leakPerSec = Random.Range(leakPerSecRange.Min, leakPerSecRange.Max);
        beatSurge = Random.Range(beatSurgeRange.Min, beatSurgeRange.Max);
        seedInterval = Random.Range(seedIntervalRange.Min, seedIntervalRange.Max);

        spreadBudget = 0f;
        generation = 1;
        currentGenerationClaimCount = 0;
        fillStartClaimCount = 0;
        hueCursor = Random.value;
        seedTimer = 0f;
        selfBeatPhase = 0f;
        selfBeatPeriod = Random.Range(standaloneSettings.SelfBeatPeriod.Min, standaloneSettings.SelfBeatPeriod.Max);
        selfPulseNoisePosition = effectTime;
        selfPulse = 0f;
        lowLevel = 0f;
        averageLevel = 0f;
        lowPresence = 0f;
        seededThisOnBeatWindow = false;
        fillActive = false;
        fillLevel = 0f;
        dropResponseActive = false;
        dropResponseAmount = 0f;
        previousDropActive = beatManager.Drop.Active;
        previousSixteenthOn = beatManager.Pulses.On(Duration.Sixteenth);

        // Seed the very first crystal so Standalone Mode has something growing immediately.
        PlantSeed();
    }

    /// <summary>Reserved deactivation hook. Controller does not currently call this.</summary>
    public override void OnEnd() { }

    /// <summary>
    /// Text appended to the on-screen debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        bool isSynced = beatManager.IsSynced;
        string mode = isSynced ? "Synced" : "Standalone (self-driven)";
        string levels = isSynced
            ? $"Low ({SyncSettings.LowLevelsForm}): {lowLevel:0.00}  " +
                $"Bass presence: {lowPresence:0.00}\n" +
                $"Average ({SyncSettings.ActivityLevelsForm}): {averageLevel:0.00}"
            : $"Average (Standalone Smoothed): {averageLevel:0.00}";
        string fillReadout = fillActive ? $"\nFILL {fillLevel:0.00} (retract + swell)" : "";
        string dropReadout = dropResponseAmount > 0f
            ? $"\nDROP RESPONSE {dropResponseAmount:0.00}"
            : "";
        return $"Crystal Growth\nMode: {mode}\nLayer: {generation}\n{levels}{fillReadout}{dropReadout}";
    }

    /// <summary>
    /// Renders one frame: decides this frame's seeds, advances the front by a framerate-independent number of
    /// ring passes, fades the trailing heat, opens the next layer when the wall is claimed, then writes the
    /// black / colored-glow / hot-rim field into <see cref="EffectBase.buffer"/>.
    /// </summary>
    public override void Draw()
    {
        float deltaTime = effectDelta;
        bool isSynced = beatManager.IsSynced;

        // Read both consumer-selected Levels forms first. Low is this Effect's bass-presence proxy for an
        // already-open On Beat window and its Beat Pulse accent. Average is independent broad-spectrum activity:
        // it scales continuous growth, the idle-seed clock, and Waveform brightness depth without deciding whether
        // the beat exists. Levels is a live-set aggregate, not an absolute loudness meter.
        float activity = ReadLevels();
        float energyPace = isSynced ? ReadEnergyPace() : 1f;
        float continuousPace = isSynced
            ? activity.Lerp(SyncSettings.QuietGrowthMultiplier, 1f) * energyPace
            : 1f;

        // The Fill is the tail of the current Phrase, and it does not always lead to a Drop, so its gesture must
        // build tension AND resolve cleanly on its own. Its stock Build retracts the current generation along the
        // exact order of its claims while the swell charges the remaining crystal glow. The restored prior layers
        // stay revealed after the Phrase boundary instead of snapping back. Because Build is normalized to the
        // Fill's length, short Fills peel quickly and long Fills expose the layer gradually.
        var fill = beatManager.Fill;
        bool inFill = fill.Active;
        float fillAmount = fill.In.Build();
        var sixteenthOn = beatManager.Pulses.On(Duration.Sixteenth);
        float ratchet = sixteenthOn ? 1f : 0f;
        if (inFill && !fillActive)
        {
            fillStartClaimCount = currentGenerationClaimCount;
        }
        fillActive = inFill;
        fillLevel = fillAmount;

        bool dropActive = beatManager.Drop.Active;
        if (dropActive && !previousDropActive)
        {
            dropResponseActive = true;
            BeginNextGeneration();
            PlantUnisonSeeds(SyncSettings.DropFlashSeeds);
        }
        previousDropActive = dropActive;
        dropResponseAmount = dropResponseActive ? beatManager.Grid.Decay() : 0f;

        if (!inFill && sixteenthOn && !previousSixteenthOn && dropResponseActive)
        {
            PlantSeeds(Mathf.CeilToInt(SyncSettings.DropSeedBurst * dropResponseAmount));
        }
        previousSixteenthOn = sixteenthOn;

        if (inFill)
        {
            seededThisOnBeatWindow = false;
            RetractCurrentGeneration(fillAmount);
        }
        else
        {
            SeedThisFrame(deltaTime, continuousPace);

            // Synced composes two independent terms: Average × Energy scales continuous pace, while the wire Beat
            // Pulse is accented only by remapped Low presence. Keeping the terms additive prevents Average or
            // Energy from multiplying the beat accent. Standalone retains its approved self-driven surge
            // arithmetic. The Grid-bound Drop response washes the fresh layer across the wall and adds
            // release-shaped sixteenth lunges.
            float pulse = isSynced
                ? beatManager.Pulses.Beat * lowPresence
                : selfPulse;
            float paceAndAccent = isSynced
                ? continuousPace + (beatSurge * pulse)
                : 1f + (beatSurge * pulse);
            float spread = spreadPerSec
                * paceAndAccent
                * (1f + (SyncSettings.DropRatchetSpread * dropResponseAmount * ratchet))
                * (1f + (SyncSettings.DropFlashSpread * dropResponseAmount));

            // Advance the front by whole rings, accumulating fractional passes so the rate is FPS-independent.
            spreadBudget += spread * deltaTime;
            int passes = 0;
            int maxFrontPassesPerFrame = isSynced
                ? SyncSettings.MaxFrontPassesPerFrame
                : standaloneSettings.MaxFrontPassesPerFrame;
            while (spreadBudget >= 1f && passes < maxFrontPassesPerFrame)
            {
                spreadBudget -= 1f;
                passes++;
                PropagateFrontOnce();
            }
            if (isSynced && passes == maxFrontPassesPerFrame)
            {
                spreadBudget = Mathf.Repeat(spreadBudget, 1f);
            }
        }

        // Fade the trailing heat so the bright band trails off behind the front; grown tiles still render at the
        // CrystalFloor (keyed on gen), so they never go black.
        float keep = Mathf.Clamp01(1f - (leakPerSec * deltaTime));
        for (int i = 0; i < charge.Length; i++)
        {
            charge[i] *= keep;
        }

        // The single-color Drop layer owns the first half of the response. Normal coverage-based generation
        // advance resumes through the second half so ordinary multicolor growth overlaps the release instead
        // of snapping back only after the Grid-bound response reaches zero.
        if (!inFill && (!dropResponseActive || dropResponseAmount <= DropGenerationOverlapAmount))
        {
            CheckGenerationAdvance();
        }

        RelaxHue(deltaTime);

        // Brightness keeps the selected Average Levels form's existing depth role: the configured floor keeps
        // active tiles bright, and minimum broad-spectrum activity lifts the result toward steady.
        float minimumBrightness = activity.Lerp(1f, SyncSettings.DrivingBrightnessFloor);
        float rhythmBrightness = waveform.Lerp(minimumBrightness, 1f);

        // Hard Drop strobe: during the Grid-bound response, every sixteenth's off-phase knocks the whole field
        // toward black, so the wall flashes on each 16th while the response falls linearly. Applied to
        // the final color below — past the CrystalFloor — so the dark phase actually reads dark. Collapses to
        // 1 (no strobe) outside a Drop. The Fill instead swells the glow while the current layer retracts.
        float strobe = 1f - (dropResponseAmount * (1f - ratchet) * SyncSettings.DropStrobeDepth);
        float swell = 1f + (SyncSettings.FillSwell * fillAmount);
        float tipThreshold = isSynced ? SyncSettings.TipThreshold : standaloneSettings.TipThreshold;
        float tipWhitenAmount = isSynced
            ? SyncSettings.TipWhitenAmount
            : standaloneSettings.TipWhitenAmount;
        float crystalFloor = isSynced ? SyncSettings.CrystalFloor : standaloneSettings.CrystalFloor;

        for (int i = 0; i < buffer.Length; i++)
        {
            if (gen[i] == 0)
            {
                buffer[i] = Color.black; // never reached by a crystal — black negative space
                continue;
            }

            float c = charge[i];

            // Full-value palette color, brightest at the leading edge and fading behind it — but a grown tile
            // never drops below CrystalFloor, so the crystal lingers as a dim layer instead of going black. The
            // very tip whitens slightly into a crystalline sparkle.
            Color col = APalette.read(hue[i], true);
            float tip = c.Remap(tipThreshold, 1f, 0f, 1f, clamp: true);
            col = Color.Lerp(col, Color.white, tip * tipWhitenAmount);

            // sqrt widens the bright band: the whole active growth area stays bright and only the oldest tail
            // eases down to the floor, so the crystal reads as a defined glowing region instead of a bright dot.
            // The Drop flash adds a luminance lift weighted by front heat (c) and the linear envelope, so the boost
            // rides the sweeping leading edge — a bright colored wavefront crossing the wall — and trails back to
            // normal behind it, all in the tile's own palette color (never toward white). Collapses to ×1 off a flash.
            float factor = Mathf.Max(Mathf.Sqrt(c) * rhythmBrightness, crystalFloor) *
                (1f + (SyncSettings.DropFlashBrightness * dropResponseAmount * c));
            buffer[i] = col * (factor * strobe * swell);
        }
    }

    /// <summary>
    /// Eases each grown tile's color a little toward the average of its <em>same-layer</em> neighbors, so the
    /// multicolor crystal collisions relax into gradients behind the front instead of reading as muddled,
    /// jagged seams. Hue is blended on the [0,1) palette circle (shortest direction) so it wraps cleanly, and
    /// only same-generation neighbors are averaged so a newer layer's repaint stays crisp. Brightness is never
    /// touched, so the black / floor / bright-front contrast is preserved.
    /// </summary>
    private void RelaxHue(float dt)
    {
        // Clamp the blend rate so a long frame hitch can't over-relax in one step.
        float hueRelaxPerSec = beatManager.IsSynced
            ? SyncSettings.HueRelaxPerSec
            : standaloneSettings.HueRelaxPerSec;
        float hueRelaxMaxPerFrame = beatManager.IsSynced
            ? SyncSettings.HueRelaxMaxPerFrame
            : standaloneSettings.HueRelaxMaxPerFrame;
        float k = Mathf.Min(
            hueRelaxPerSec * dt,
            hueRelaxMaxPerFrame);
        if (k <= 0f)
        {
            return;
        }

        Array.Copy(hue, nextHue, hue.Length);

        for (int i = 0; i < hue.Length; i++)
        {
            int gi = gen[i];
            if (gi == 0)
            {
                continue;
            }

            float h = hue[i];
            float sumOffset = 0f;
            int n = 0;

            Penrose.neighbor[] nb = tiles[i].neighbors;
            for (int j = 0; j < nb.Length; j++)
            {
                int idx = nb[j].tileIdx;
                if (gen[idx] != gi)
                {
                    continue;
                }

                // Shortest signed distance from h to the neighbor's hue on the [0,1) circle.
                sumOffset += Mathf.Repeat(hue[idx] - h + 0.5f, 1f) - 0.5f;
                n++;
            }

            if (n > 0)
            {
                nextHue[i] = Mathf.Repeat(h + (sumOffset / n * k), 1f);
            }
        }

        (hue, nextHue) = (nextHue, hue);
    }

    /// <summary>
    /// Plants this frame's seeds from an On Beat window plus Low presence while Synced, or from the
    /// self-driven Standalone clock.
    /// </summary>
    /// <param name="dt">Current effect delta in seconds.</param>
    /// <param name="continuousPace">Average- and Energy-scaled Synced pace for the idle-seed clock.</param>
    private void SeedThisFrame(float dt, float continuousPace)
    {
        int? beatInBar = beatManager.Timing.BeatInBar;

        if (beatManager.IsSynced && beatInBar is { } bib)
        {
            SeedSynced(dt, bib, continuousPace);
            return;
        }

        SeedSelfDriven(dt);
    }

    /// <summary>
    /// Standalone seeding (no beat clock): a self-driven metronome that mimics the synced liveliness — a steady
    /// trickle of seeds keeps several fronts crawling at once, and a synthetic downbeat periodically blooms a
    /// burst and resets the spread surge so the wall pulses in waves.
    /// </summary>
    private void SeedSelfDriven(float dt)
    {
        selfPulseNoisePosition += dt * standaloneSettings.SelfPulseNoiseSpeed;
        selfBeatPhase += dt / selfBeatPeriod;
        if (selfBeatPhase >= 1f)
        {
            selfBeatPhase -= 1f;
            selfBeatPeriod = Random.Range(
                standaloneSettings.SelfBeatPeriod.Min,
                standaloneSettings.SelfBeatPeriod.Max);
            float accent = Mathf.PerlinNoise(selfPulseNoisePosition, 0f);
            selfPulse = Mathf.Lerp(
                standaloneSettings.SelfPulsePeak.Min,
                standaloneSettings.SelfPulsePeak.Max,
                accent);

            PlantSeeds(BloomCount());
        }

        // Steady fill between downbeats so there are always several live fronts, not one lonely crystal.
        seedTimer += dt;
        if (seedTimer >= seedInterval)
        {
            PlantSeed();
            seedTimer = 0f;
            seedInterval = Random.Range(
                standaloneSettings.SeedInterval.Min,
                standaloneSettings.SeedInterval.Max);
        }

        // Decay the synthetic surge toward 0 so each downbeat is a lunge, not a sustained sprint.
        selfPulse = Mathf.Max(0f, selfPulse - (dt * standaloneSettings.SelfPulseDecayPerSec));
    }

    /// <summary>
    /// Samples the two independently selected <see cref="BeatManager.Levels"/> forms, remaps Low into
    /// Crystal Growth's bass-presence proxy, and returns remapped broad-spectrum Average activity.
    /// Standalone retains its former Smoothed reads and saved-or-default activity range so a Sync Settings
    /// edit cannot move the approved self-driven look. Missing wire Levels read as zero.
    /// </summary>
    private float ReadLevels()
    {
        bool isSynced = beatManager.IsSynced;
        LevelBands lowBands = isSynced
            ? ReadLevelsForm(SyncSettings.LowLevelsForm)
            : beatManager.Levels.Smoothed;
        LevelBands activityBands = isSynced
            ? ReadLevelsForm(SyncSettings.ActivityLevelsForm)
            : beatManager.Levels.Smoothed;
        lowLevel = lowBands.Low;
        averageLevel = activityBands.Average;
        lowPresence = isSynced
            ? lowLevel.Remap(SyncSettings.LowPresenceThreshold, 1f, 0f, 1f, clamp: true)
            : 0f;
        FloatRange activityRange = isSynced
            ? SyncSettings.ActivityLevel
            : standaloneSettings.ActivityLevel;
        return averageLevel.Remap(activityRange.Min, activityRange.Max, 0f, 1f, clamp: true);
    }

    /// <summary>
    /// Reads one selected Levels form directly from BeatManager's read-only Data Surface.
    /// </summary>
    /// <param name="form">The Normalized, Smoothed, or Peak form chosen by this consumer.</param>
    /// <returns>The selected immutable low/mid/high band set.</returns>
    private LevelBands ReadLevelsForm(CrystalGrowthSyncSettings.LevelsForm form) => form switch
    {
        CrystalGrowthSyncSettings.LevelsForm.Smoothed => beatManager.Levels.Smoothed,
        CrystalGrowthSyncSettings.LevelsForm.Peak => beatManager.Levels.Peak,
        _ => beatManager.Levels.Normalized,
    };

    /// <summary>
    /// Maps the current phrase/run-scale Energy to the authored Low/High pace endpoints, with Mid at
    /// their midpoint and unavailable Energy neutral at one.
    /// </summary>
    private float ReadEnergyPace()
    {
        FloatRange pace = SyncSettings.EnergyPace;
        return beatManager.Energy.Level switch
        {
            Energy.Low => pace.Min,
            Energy.Mid => (pace.Min + pace.Max) * 0.5f,
            Energy.High => pace.Max,
            _ => 1f,
        };
    }

    /// <summary>
    /// Selects a fresh wall palette and disarms any prior Drop response at each observed phrase-relative
    /// Grid boundary. On a Drop's opening boundary this hook runs before Draw, where the Drop Active rising
    /// edge arms the new response; the following boundary ends it. The palette cross-fades, so the crystals
    /// recolor smoothly.
    /// </summary>
    protected override void OnNewGrid()
    {
        dropResponseActive = false;
        APalette.Change();
    }

    /// <summary>
    /// Synced seeding combines the current wire-authored On Beat window with selected-form Low presence.
    /// Each open window can launch at most one continuously sized burst, and a Low value arriving after the
    /// window opens can still qualify it. The idle heartbeat keeps the wall growing and follows the same
    /// Average- and Energy-scaled continuous pace as the front.
    /// </summary>
    /// <param name="dt">Current effect delta in seconds.</param>
    /// <param name="beatInBar">Current one-based beat label.</param>
    /// <param name="continuousPace">Average- and Energy-scaled Synced pace.</param>
    private void SeedSynced(float dt, int beatInBar, float continuousPace)
    {
        bool onBeatWindowOpen = beatManager.Beats.OnBeat(beatInBar);
        if (!onBeatWindowOpen)
        {
            seededThisOnBeatWindow = false;
        }
        else if (!seededThisOnBeatWindow && lowLevel > SyncSettings.LowPresenceThreshold)
        {
            int burst = Mathf.RoundToInt(lowPresence.Lerp(
                SyncSettings.LowSeedBurst.Min,
                SyncSettings.LowSeedBurst.Max));
            if (beatInBar == 1)
            {
                burst += SyncSettings.DownbeatSeedBonus;
            }

            // Fill does not create a second seed path here. Drop owns its one-shot unison layer and its
            // Grid-bound sixteenth bursts, so this branch remains only the Low-qualified On Beat response.
            PlantSeeds(burst);
            seededThisOnBeatWindow = true;
        }

        // Idle heartbeat follows the continuous pace, so low Average activity or Low Energy cannot accumulate
        // ordinary-rate origins that all arrive after the musical envelope has moved on.
        seedTimer += dt * continuousPace;
        if (seedTimer >= seedInterval)
        {
            PlantSeed();
            seedTimer = 0f;
            seedInterval = Random.Range(
                SyncSettings.IdleSeedInterval.Min,
                SyncSettings.IdleSeedInterval.Max);
        }
    }

    /// <summary>
    /// Injects a hot front and claims one random tile for the current generation — the origin of a new crystal.
    /// Each seed steps <see cref="hueCursor"/> by the golden ratio so its crystal grows in a fresh, well-separated
    /// palette color; many such crystals collide into a multicolor field within one generation.
    /// </summary>
    private void PlantSeed()
    {
        float goldenStep = beatManager.IsSynced ? SyncSettings.GoldenStep : standaloneSettings.GoldenStep;
        hueCursor = Mathf.Repeat(hueCursor + goldenStep, 1f);

        int t = Random.Range(0, charge.Length);
        if (gen[t] != generation)
        {
            RecordCurrentGenerationClaim(t, gen[t], hue[t]);
        }
        charge[t] = 1f;
        gen[t] = generation;
        hue[t] = hueCursor;
    }

    /// <summary>Plants <paramref name="count"/> seeds at once — one bloom's worth of fresh fronts.</summary>
    private void PlantSeeds(int count)
    {
        for (int s = 0; s < count; s++)
        {
            PlantSeed();
        }
    }

    /// <summary>
    /// Plants several seeds of the current generation that all share one freshly-stepped hue, so they read as a
    /// single colored wave fanning out from a few origins rather than a scatter of separate-colored crystals.
    /// Used by the Drop flash to wash one new layer across the wall.
    /// </summary>
    private void PlantUnisonSeeds(int count)
    {
        float goldenStep = beatManager.IsSynced ? SyncSettings.GoldenStep : standaloneSettings.GoldenStep;
        hueCursor = Mathf.Repeat(hueCursor + goldenStep, 1f);
        for (int s = 0; s < count; s++)
        {
            int t = Random.Range(0, charge.Length);
            if (gen[t] != generation)
            {
                RecordCurrentGenerationClaim(t, gen[t], hue[t]);
            }
            charge[t] = 1f;
            gen[t] = generation;
            hue[t] = hueCursor;
        }
    }

    /// <summary>
    /// Advances the growth front one ring: every hot tile pushes heat into its neighbors and claims any neighbor
    /// held by an older (or no) generation, carrying its color. A higher generation always wins, so the newest
    /// layer's bright front sweeps over and repaints the layers beneath it. Same-generation tiles still relay
    /// heat so the rim keeps moving. Works through the double buffers, then swaps them in.
    /// </summary>
    private void PropagateFrontOnce()
    {
        float heatEpsilon = beatManager.IsSynced ? SyncSettings.HeatEpsilon : standaloneSettings.HeatEpsilon;
        float frontPush = beatManager.IsSynced ? SyncSettings.FrontPush : standaloneSettings.FrontPush;
        Array.Copy(charge, nextCharge, charge.Length);
        Array.Copy(gen, nextGen, gen.Length);
        Array.Copy(hue, nextHue, hue.Length);

        for (int i = 0; i < charge.Length; i++)
        {
            float c = charge[i];
            if (c <= heatEpsilon)
            {
                continue;
            }

            float push = c * frontPush;
            int gi = gen[i];
            float hi = hue[i];

            Penrose.neighbor[] nb = tiles[i].neighbors;
            for (int j = 0; j < nb.Length; j++)
            {
                int idx = nb[j].tileIdx;

                if (gi > nextGen[idx])
                {
                    // Claim/repaint the neighbor for this newer generation and light its front.
                    if (gi == generation)
                    {
                        RecordCurrentGenerationClaim(idx, nextGen[idx], nextHue[idx]);
                    }
                    nextGen[idx] = gi;
                    nextHue[idx] = hi;
                    if (push > nextCharge[idx])
                    {
                        nextCharge[idx] = push;
                    }
                }
                else if (gi == nextGen[idx] && push > nextCharge[idx])
                {
                    // Same generation: don't repaint, but keep the rim advancing through it.
                    nextCharge[idx] = push;
                }
            }
        }

        (charge, nextCharge) = (nextCharge, charge);
        (gen, nextGen) = (nextGen, gen);
        (hue, nextHue) = (nextHue, hue);
    }

    /// <summary>
    /// Opens the next layer on top once the current generation has claimed most of the wall: there is nothing
    /// left for its front to take, so the next palette color blooms immediately instead of dwelling.
    /// </summary>
    private void CheckGenerationAdvance()
    {
        int claimed = 0;
        for (int i = 0; i < gen.Length; i++)
        {
            if (gen[i] == generation)
            {
                claimed++;
            }
        }

        float coverageToAdvance = beatManager.IsSynced
            ? SyncSettings.CoverageToAdvance
            : standaloneSettings.CoverageToAdvance;
        if (claimed >= (int)(coverageToAdvance * Penrose.Total))
        {
            StartNextGeneration();
        }
    }

    /// <summary>
    /// Starts the next generation and blooms several seeds of it (each its own palette color via
    /// <see cref="PlantSeed"/>) whose bright fronts then sweep outward over the still-glowing wall.
    /// </summary>
    private void StartNextGeneration()
    {
        BeginNextGeneration();
        PlantSeeds(BloomCount());
    }

    /// <summary>
    /// Advances the generation index and discards the prior generation's claim history, keeping retained
    /// restoration state bounded to the one generation Fill can retract.
    /// </summary>
    private void BeginNextGeneration()
    {
        generation++;
        currentGenerationClaimCount = 0;
    }

    /// <summary>
    /// Appends one first-time current-generation claim and the prior layer it replaced to the fixed wall-sized
    /// history buffers.
    /// </summary>
    /// <param name="tileIndex">Tile newly claimed by the current generation.</param>
    /// <param name="priorGeneration">Generation visible immediately before the claim.</param>
    /// <param name="priorHue">Hue visible immediately before the claim.</param>
    private void RecordCurrentGenerationClaim(int tileIndex, int priorGeneration, float priorHue)
    {
        currentGenerationClaimOrder[currentGenerationClaimCount] = tileIndex;
        priorGenerationByClaim[currentGenerationClaimCount] = priorGeneration;
        priorHueByClaim[currentGenerationClaimCount] = priorHue;
        currentGenerationClaimCount++;
    }

    /// <summary>
    /// Restores current-generation claims newest first according to the Fill Build captured from BeatManager.
    /// Restored charge is zero so the revealed prior layer returns at its crystal floor without reviving an old
    /// moving front; each restoration mutates the live field, so no end-of-Fill snap-back exists.
    /// </summary>
    /// <param name="fillAmount">Current linear Fill Build in the range zero to one.</param>
    private void RetractCurrentGeneration(float fillAmount)
    {
        int targetClaimCount = Mathf.FloorToInt((1f - fillAmount) * fillStartClaimCount);
        while (currentGenerationClaimCount > targetClaimCount)
        {
            currentGenerationClaimCount--;
            int tileIndex = currentGenerationClaimOrder[currentGenerationClaimCount];
            charge[tileIndex] = 0f;
            gen[tileIndex] = priorGenerationByClaim[currentGenerationClaimCount];
            hue[tileIndex] = priorHueByClaim[currentGenerationClaimCount];
        }
    }

    /// <summary>A bloom is 3–5 seeds — used for a new generation and the Standalone downbeat.</summary>
    private int BloomCount()
    {
        int bloomCountBase = beatManager.IsSynced
            ? SyncSettings.BloomCountBase
            : standaloneSettings.BloomCountBase;
        IntRange bloomCountOffset = beatManager.IsSynced
            ? SyncSettings.BloomCountOffset
            : standaloneSettings.BloomCountOffset;
        return bloomCountBase + Random.Range(
            bloomCountOffset.MinInclusive,
            bloomCountOffset.MaxExclusive);
    }
}

/// <summary>
/// The serializable value shape shared by Crystal Growth's fully populated Standalone Defaults and
/// saved Standalone Settings; Unity may create an empty instance before serialized values are applied.
/// </summary>
[Serializable]
public sealed class CrystalGrowthStandaloneSettings
{
    /// <summary>Front heat below which the growing rim stops advancing.</summary>
    public float HeatEpsilon;

    /// <summary>Fraction of front heat carried into the next adjacency ring.</summary>
    public float FrontPush;

    /// <summary>Fraction of the wall claimed before the next generation blooms.</summary>
    public float CoverageToAdvance;

    /// <summary>Maximum front passes advanced during one frame.</summary>
    public int MaxFrontPassesPerFrame;

    /// <summary>Minimum brightness retained by every grown tile.</summary>
    public float CrystalFloor;

    /// <summary>Per-second rate for relaxing same-layer hue seams.</summary>
    public float HueRelaxPerSec;

    /// <summary>Maximum hue relaxation applied during one frame.</summary>
    public float HueRelaxMaxPerFrame;

    /// <summary>
    /// Smoothed Average Levels range mapped from minimum to full activity by the Standalone spread
    /// machinery.
    /// </summary>
    public FloatRange ActivityLevel;

    /// <summary>Golden-ratio palette step between successive seed colors.</summary>
    public float GoldenStep;

    /// <summary>Per-activation range for front spread in rings per second.</summary>
    public FloatRange SpreadPerSec;

    /// <summary>Per-activation range for the fraction of front brightness lost each second.</summary>
    public FloatRange LeakPerSec;

    /// <summary>Per-activation range for the synthetic spread-surge multiplier.</summary>
    public FloatRange BeatSurge;

    /// <summary>Range in seconds between free-running Standalone seeds.</summary>
    public FloatRange SeedInterval;

    /// <summary>Range in seconds between synthetic Standalone downbeats.</summary>
    public FloatRange SelfBeatPeriod;

    /// <summary>Weakest and strongest Perlin-mapped peak of the synthetic Standalone spread surge.</summary>
    public FloatRange SelfPulsePeak;

    /// <summary>Per-second speed through the activation-local Perlin coordinate sampled on synthetic downbeats.</summary>
    public float SelfPulseNoiseSpeed;

    /// <summary>Per-second decay of the synthetic Standalone spread surge.</summary>
    public float SelfPulseDecayPerSec;

    /// <summary>Front heat at which the crystal tip begins whitening.</summary>
    public float TipThreshold;

    /// <summary>Maximum amount of white mixed into the crystal tip.</summary>
    public float TipWhitenAmount;

    /// <summary>Base seed count added to every randomly varied bloom.</summary>
    public int BloomCountBase;

    /// <summary>
    /// Random offset added to a bloom's base seed count; the minimum is inclusive and the maximum
    /// is exclusive.
    /// </summary>
    public IntRange BloomCountOffset;

    /// <summary>
    /// Copies every Crystal Growth Standalone Setting, including range endpoints and Rails.
    /// </summary>
    public void CopyFrom(CrystalGrowthStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        HeatEpsilon = source.HeatEpsilon;
        FrontPush = source.FrontPush;
        CoverageToAdvance = source.CoverageToAdvance;
        MaxFrontPassesPerFrame = source.MaxFrontPassesPerFrame;
        CrystalFloor = source.CrystalFloor;
        HueRelaxPerSec = source.HueRelaxPerSec;
        HueRelaxMaxPerFrame = source.HueRelaxMaxPerFrame;
        ActivityLevel = new FloatRange(
            source.ActivityLevel.Min,
            source.ActivityLevel.Max,
            source.ActivityLevel.LowRail,
            source.ActivityLevel.HighRail);
        GoldenStep = source.GoldenStep;
        SpreadPerSec = new FloatRange(
            source.SpreadPerSec.Min,
            source.SpreadPerSec.Max,
            source.SpreadPerSec.LowRail,
            source.SpreadPerSec.HighRail);
        LeakPerSec = new FloatRange(
            source.LeakPerSec.Min,
            source.LeakPerSec.Max,
            source.LeakPerSec.LowRail,
            source.LeakPerSec.HighRail);
        BeatSurge = new FloatRange(
            source.BeatSurge.Min,
            source.BeatSurge.Max,
            source.BeatSurge.LowRail,
            source.BeatSurge.HighRail);
        SeedInterval = new FloatRange(
            source.SeedInterval.Min,
            source.SeedInterval.Max,
            source.SeedInterval.LowRail,
            source.SeedInterval.HighRail);
        SelfBeatPeriod = new FloatRange(
            source.SelfBeatPeriod.Min,
            source.SelfBeatPeriod.Max,
            source.SelfBeatPeriod.LowRail,
            source.SelfBeatPeriod.HighRail);
        SelfPulsePeak = new FloatRange(
            source.SelfPulsePeak.Min,
            source.SelfPulsePeak.Max,
            source.SelfPulsePeak.LowRail,
            source.SelfPulsePeak.HighRail);
        SelfPulseNoiseSpeed = source.SelfPulseNoiseSpeed;
        SelfPulseDecayPerSec = source.SelfPulseDecayPerSec;
        TipThreshold = source.TipThreshold;
        TipWhitenAmount = source.TipWhitenAmount;
        BloomCountBase = source.BloomCountBase;
        BloomCountOffset = new IntRange(
            source.BloomCountOffset.MinInclusive,
            source.BloomCountOffset.MaxExclusive,
            source.BloomCountOffset.LowRail,
            source.BloomCountOffset.HighRail);
    }
}

/// <summary>The saved-or-default musical-response settings used by Crystal Growth in Synced Mode.</summary>
[Serializable]
public sealed class CrystalGrowthSyncSettings
{
    /// <summary>The three BeatManager Levels forms independently selectable by Crystal Growth consumers.</summary>
    public enum LevelsForm
    {
        /// <summary>The instantaneous wire-authored live-set aggregate.</summary>
        Normalized,

        /// <summary>The attack/release follower.</summary>
        Smoothed,

        /// <summary>Instant rise with a tempo-based linear fall.</summary>
        Peak,
    }

    /// <summary>Levels form used by the Low bass-presence consumer.</summary>
    public LevelsForm LowLevelsForm;

    /// <summary>Levels form used by the broad-spectrum Average activity consumer.</summary>
    public LevelsForm ActivityLevelsForm;

    /// <summary>Selected-form Low threshold for qualifying an open On Beat window.</summary>
    [Range(0f, 1f)] public float LowPresenceThreshold;

    /// <summary>Selected-form Average Levels range mapped from minimum to full broad-spectrum activity.</summary>
    public FloatRange ActivityLevel;

    /// <summary>Continuous growth multiplier at minimum selected-form Average activity.</summary>
    [Min(0f)] public float QuietGrowthMultiplier;

    /// <summary>Continuous pace at Low and High Energy; Mid uses the range midpoint.</summary>
    public FloatRange EnergyPace;

    /// <summary>Peak luminance gain on the Drop wavefront.</summary>
    [Min(0f)] public float DropFlashBrightness;

    /// <summary>Extra spread multiplier at the peak of the Drop flash.</summary>
    [Min(0f)] public float DropFlashSpread;

    /// <summary>Unison seed count planted at Drop-flash onset.</summary>
    [Min(0)] public int DropFlashSeeds;

    /// <summary>Range in seconds between Synced idle-heartbeat seeds.</summary>
    public FloatRange IdleSeedInterval;

    /// <summary>Drop sixteenth-ratchet spread multiplier.</summary>
    [Min(0f)] public float DropRatchetSpread;

    /// <summary>Depth of the Drop sixteenth strobe.</summary>
    [Range(0f, 1f)] public float DropStrobeDepth;

    /// <summary>Maximum seed count planted on a Drop sixteenth, scaled by the Grid-bound Drop amount.</summary>
    [Min(0)] public int DropSeedBurst;

    /// <summary>Whole-field luminance swell at a full Fill.</summary>
    [Min(0f)] public float FillSwell;

    /// <summary>Brightness floor while selected-form Average Levels are at full activity.</summary>
    [Range(0f, 1f)] public float DrivingBrightnessFloor;

    /// <summary>Seed-count range interpolated from Crystal Growth's remapped Low presence.</summary>
    public FloatRange LowSeedBurst;

    /// <summary>Extra seed count added on beat one of a bar.</summary>
    [Min(0)] public int DownbeatSeedBonus;

    /// <summary>Front heat below which the growing rim stops advancing.</summary>
    public float HeatEpsilon;

    /// <summary>Fraction of front heat carried into the next adjacency ring.</summary>
    public float FrontPush;

    /// <summary>Fraction of the wall claimed before the next generation blooms.</summary>
    public float CoverageToAdvance;

    /// <summary>Maximum front passes advanced during one frame.</summary>
    public int MaxFrontPassesPerFrame;

    /// <summary>Minimum brightness retained by every grown Tile.</summary>
    public float CrystalFloor;

    /// <summary>Per-second rate for relaxing same-layer hue seams.</summary>
    public float HueRelaxPerSec;

    /// <summary>Maximum hue relaxation applied during one frame.</summary>
    public float HueRelaxMaxPerFrame;

    /// <summary>Golden-ratio palette step between successive seed colors.</summary>
    public float GoldenStep;

    /// <summary>Per-activation range for front spread in adjacency rings per second.</summary>
    public FloatRange SpreadPerSec;

    /// <summary>Per-activation range for the fraction of front brightness lost each second.</summary>
    public FloatRange LeakPerSec;

    /// <summary>Per-activation range for the beat-driven spread-surge multiplier.</summary>
    public FloatRange BeatSurge;

    /// <summary>Front heat at which the crystal tip begins whitening.</summary>
    public float TipThreshold;

    /// <summary>Maximum amount of white mixed into the crystal tip.</summary>
    public float TipWhitenAmount;

    /// <summary>Base seed count added to every randomly varied bloom.</summary>
    public int BloomCountBase;

    /// <summary>
    /// Random offset added to a bloom's base seed count; the minimum is inclusive and the maximum
    /// is exclusive.
    /// </summary>
    public IntRange BloomCountOffset;

    /// <summary>Copies every Crystal Growth Sync Setting from another value.</summary>
    public void CopyFrom(CrystalGrowthSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        LowLevelsForm = source.LowLevelsForm;
        ActivityLevelsForm = source.ActivityLevelsForm;
        LowPresenceThreshold = source.LowPresenceThreshold;
        ActivityLevel = new FloatRange(
            source.ActivityLevel.Min,
            source.ActivityLevel.Max,
            source.ActivityLevel.LowRail,
            source.ActivityLevel.HighRail);
        QuietGrowthMultiplier = source.QuietGrowthMultiplier;
        EnergyPace = new FloatRange(
            source.EnergyPace.Min,
            source.EnergyPace.Max,
            source.EnergyPace.LowRail,
            source.EnergyPace.HighRail);
        DropFlashBrightness = source.DropFlashBrightness;
        DropFlashSpread = source.DropFlashSpread;
        DropFlashSeeds = source.DropFlashSeeds;
        IdleSeedInterval = new FloatRange(
            source.IdleSeedInterval.Min,
            source.IdleSeedInterval.Max,
            source.IdleSeedInterval.LowRail,
            source.IdleSeedInterval.HighRail);
        DropRatchetSpread = source.DropRatchetSpread;
        DropStrobeDepth = source.DropStrobeDepth;
        DropSeedBurst = source.DropSeedBurst;
        FillSwell = source.FillSwell;
        DrivingBrightnessFloor = source.DrivingBrightnessFloor;
        LowSeedBurst = new FloatRange(
            source.LowSeedBurst.Min,
            source.LowSeedBurst.Max,
            source.LowSeedBurst.LowRail,
            source.LowSeedBurst.HighRail);
        DownbeatSeedBonus = source.DownbeatSeedBonus;
        HeatEpsilon = source.HeatEpsilon;
        FrontPush = source.FrontPush;
        CoverageToAdvance = source.CoverageToAdvance;
        MaxFrontPassesPerFrame = source.MaxFrontPassesPerFrame;
        CrystalFloor = source.CrystalFloor;
        HueRelaxPerSec = source.HueRelaxPerSec;
        HueRelaxMaxPerFrame = source.HueRelaxMaxPerFrame;
        GoldenStep = source.GoldenStep;
        SpreadPerSec = new FloatRange(
            source.SpreadPerSec.Min,
            source.SpreadPerSec.Max,
            source.SpreadPerSec.LowRail,
            source.SpreadPerSec.HighRail);
        LeakPerSec = new FloatRange(
            source.LeakPerSec.Min,
            source.LeakPerSec.Max,
            source.LeakPerSec.LowRail,
            source.LeakPerSec.HighRail);
        BeatSurge = new FloatRange(
            source.BeatSurge.Min,
            source.BeatSurge.Max,
            source.BeatSurge.LowRail,
            source.BeatSurge.HighRail);
        TipThreshold = source.TipThreshold;
        TipWhitenAmount = source.TipWhitenAmount;
        BloomCountBase = source.BloomCountBase;
        BloomCountOffset = new IntRange(
            source.BloomCountOffset.MinInclusive,
            source.BloomCountOffset.MaxExclusive,
            source.BloomCountOffset.LowRail,
            source.BloomCountOffset.HighRail);
    }
}
