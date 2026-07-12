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
/// <item><description>WHEN seeds spawn — Synced with band energy: the bass kick drives the blooms; synced
///   without energy: a bloom on each beat, bigger on the bar's one. Standalone: a self-driven metronome (a
///   steady trickle plus a synthetic downbeat bloom) so several fronts always crawl at once.</description></item>
/// <item><description>THE DROP — beat one of a Drop fires a one-shot flash: a fresh single-color layer is surged
///   across the whole wall as a bright colored wavefront (the luminance lift rides the sweeping leading edge, in
///   palette color, never white), easing back over a couple of bars so the drop lands as one dramatic sweep that
///   resolves into a new crystal field.</description></item>
/// <item><description>HOW FAST the front lunges — <see cref="BeatManager.Pulse"/> surges the spread rate on
///   each hit; Standalone falls back to a self-driven surge so its fronts still lunge on the synthetic downbeats.</description></item>
/// <item><description>OVERALL brightness — <see cref="EffectBase.BeatBrightness"/> pulses the field with the
///   music when synced, holds steady when not.</description></item>
/// <item><description>ENERGY gating — live band <see cref="BeatManager.Levels"/> ease all three off as the
///   track quietens, so a quiet break stops chasing an inaudible beat instead of seeding/surging/strobing to it.</description></item>
/// <item><description>PALETTE — a fresh wall palette is selected at the start of every 16-beat Grid.</description></item>
/// </list>
/// </remarks>
public class CrystalGrowth : EffectBase
{
    /// <summary>Crystal Growth expresses both phrase cues: the fill ratchet builds the strobe/lunge into the change,
    /// and the Drop downbeat fires its one-shot whole-wall surge. Advertise both so the Director can deliberately
    /// cast it into Fill and Drop moments, not only react when it happens to be on-air. Its growth stays calm and
    /// eases off as the track quietens, so it advertises as a Low/Mid-energy Performer.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Front heat below this is treated as cold and stops advancing the rim (the tail dies here).</summary>
    private const float HeatEpsilon = 0.01f;

    /// <summary>Fraction of its heat the front carries into the next ring; near 1 keeps the leading edge bright as it travels.</summary>
    private const float FrontPush = 0.95f;

    /// <summary>Fraction of the wall the current generation must claim before the next layer blooms on top.</summary>
    private const float CoverageToAdvance = 0.85f;

    /// <summary>Cap on ring passes advanced in one frame, so a long frame hitch catches up a little rather than
    /// detonating the front across the whole wall in a single step.</summary>
    private const int MaxFrontPassesPerFrame = 6;

    /// <summary>A grown tile's brightness never fades below this — the crystal lingers as a dim layer instead of going black. Unreached tiles still render pure black.</summary>
    private const float CrystalFloor = 0.5f;

    /// <summary>How fast a grown tile eases its color toward its same-layer neighbors, softening crystal seams.</summary>
    private const float HueRelaxPerSec = 0.6f;

    /// <summary>Low-band level above which the four-on-the-floor bass kick is considered present (a third).</summary>
    private const float KickThreshold = 1f / 3f;

    /// <summary>Average-band energy at/below which the track is treated as fully quiet — beat coupling gates off here.</summary>
    private const float QuietEnergy = 0.233f; // a third minus a soft band

    /// <summary>Average-band energy at/above which the track is fully "driving" — beat coupling at full strength.</summary>
    private const float ActiveEnergy = 0.433f; // a third plus a soft band

    /// <summary>Golden-ratio conjugate: the step that spaces successive seed colors evenly across the palette.</summary>
    private const float GoldenStep = 0.618034f;

    /// <summary>Common-time beats per bar, matching BeatManager's beat-slot model — used to size the Drop flash in bars.</summary>
    private const int BeatsPerBar = 4;

    /// <summary>Bars the Drop sparkle is drawn out over: full machine-gun at the hit, fading back to normal growth across this many bars.</summary>
    private const float DropFadeBars = 2f;

    /// <summary>Sparkle length in seconds when no tempo is on the wire to size it in bars (a Drop without a usable BPM).</summary>
    private const float DropFlashFallbackSeconds = 3.2f;

    /// <summary>Peak luminance gain on the Drop wavefront — weighted by front heat so only the sweeping leading edge brightens, in the tile's own palette color (never toward white). Tune on the DROP FLASH readout.</summary>
    private const float DropFlashBrightness = 1.2f;

    /// <summary>Extra spread multiplier at the peak of the Drop flash, so the fresh layer sweeps briskly across the wall. Tune on the DROP FLASH readout.</summary>
    private const float DropFlashSpread = 1.5f;

    /// <summary>Seeds of the fresh Drop layer planted at the flash onset, sharing one hue so they read as a single colored wave.</summary>
    private const int DropFlashSeeds = 3;

    /// <summary>Seconds between idle seeds in Standalone Mode, where the self-metronome drives all growth.</summary>
    private const float StandaloneSeedMin = 0.18f;
    private const float StandaloneSeedMax = 0.35f;

    /// <summary>Seconds between idle "heartbeat" seeds when synced — slower, because the bass kick does the heavy seeding.</summary>
    private const float SyncedIdleSeedMin = 0.5f;
    private const float SyncedIdleSeedMax = 0.9f;

    /// <summary>Seconds per synthetic Standalone downbeat (re-jittered each tick so it never feels mechanical).</summary>
    private const float SelfBeatPeriodMin = 1.2f;
    private const float SelfBeatPeriodMax = 2.2f;

    /// <summary>Per-second decay of the Standalone spread surge, so each synthetic downbeat is a lunge, not a sustained sprint.</summary>
    private const float SelfPulseDecayPerSec = 2.5f;

    /// <summary>Extra spread multiplier added on each sixteenth's on-phase during a Drop — the front lunges in stutters.</summary>
    private const float DropRatchetSpread = 4f;

    /// <summary>How far the whole field is knocked toward black on a Drop sixteenth's off-phase — the hard strobe depth.</summary>
    private const float DropStrobeDepth = 0.9f;

    /// <summary>Seeds planted on each sixteenth onset during a Drop, so the wall machine-guns for the whole drop window.</summary>
    private const int DropSeedBurst = 12;

    /// <summary>How far the base spread is reined in at full Fill — the crystal visibly tenses and compresses going
    /// into the phrase change. 0.65 means growth drops to 35% of normal speed at the fill's peak.</summary>
    private const float FillHoldback = 0.65f;

    /// <summary>Luminance swell across the whole grown crystal at full Fill, so the hold-back reads as charging
    /// up rather than stalling. Tune on the FILL readout.</summary>
    private const float FillSwell = 0.35f;

    /// <summary>Per-tile front heat in [0..1]; the bright moving band. Decays toward 0, but a grown tile still renders at <see cref="CrystalFloor"/> (keyed on <see cref="gen"/>), so charge is only the bright part above the floor.</summary>
    private float[] charge;

    /// <summary>Per-tile palette position in [0..1]; the color the claiming generation painted onto the tile.</summary>
    private float[] hue;

    /// <summary>Per-tile generation index that has claimed the tile; 0 = never grown (black). Higher always wins.</summary>
    private int[] gen;

    /// <summary>Double-buffer targets for one front pass, swapped in after each step.</summary>
    private float[] nextCharge;
    private float[] nextHue;
    private int[] nextGen;

    /// <summary>Rings-per-second the front advances; how fast a crystal sweeps across the wall.</summary>
    private float spreadPerSec;

    /// <summary>Fraction of front brightness lost per second; sets how fast the trail fades from the bright front down to the floor.</summary>
    private float leakPerSec;

    /// <summary>Extra spread multiplier applied at the peak of <see cref="BeatManager.Pulse"/>.</summary>
    private float beatSurge;

    /// <summary>Accumulated fractional front passes carried between frames (framerate-independent spread).</summary>
    private float spreadBudget;

    /// <summary>The current (newest) generation index. Each new layer increments this.</summary>
    private int generation;

    /// <summary>
    /// Rolling palette position [0..1]. Every seed steps it by the golden ratio before claiming its color, so
    /// successive crystals land on well-separated palette colors and the wall fills with many hues at once.
    /// </summary>
    private float hueCursor;

    /// <summary>Standalone-only seed clock: seconds since the last free-running seed.</summary>
    private float seedTimer;

    /// <summary>Standalone-only seed clock: target seconds between free-running seeds.</summary>
    private float seedInterval;

    /// <summary>Standalone-only self-driven metronome phase [0..1); wraps to a synthetic "downbeat" bloom.</summary>
    private float selfBeatPhase;

    /// <summary>Standalone-only seconds per synthetic downbeat (re-jittered each tick so it never feels mechanical).</summary>
    private float selfBeatPeriod;

    /// <summary>Standalone-only spread surge envelope (1 at a synthetic downbeat, decaying to 0) — stands in for <see cref="BeatManager.Pulse"/>.</summary>
    private float selfPulse;

    /// <summary>Last frame's <see cref="BeatManager.BeatInBar"/>, so a new beat can be edge-detected. Null = off-beat last frame.</summary>
    private int? lastBeatInBar;

    /// <summary>Last frame's Drop in-progress state, so the drop's beat one can be edge-detected into exactly one flash.</summary>
    private bool wasDropActive;

    /// <summary>One-shot Drop flash envelope in [0..1]: 1 at the drop's onset, eased back to 0 across <see cref="dropFlashSeconds"/>. Drives the wavefront luminance lift and the spread surge that washes a fresh layer across the wall.</summary>
    private float dropFlash;

    /// <summary>Seconds elapsed since the current Drop flash onset.</summary>
    private float dropFlashElapsed;

    /// <summary>Seconds the current Drop flash is drawn out over (~<see cref="DropFlashBars"/> bars, sized from the tempo at onset).</summary>
    private float dropFlashSeconds;

    /// <summary>Whether live <see cref="BeatManager.Levels"/> were available this frame (false → no energy info, assume active).</summary>
    private bool hasLevels;

    /// <summary>This frame's smoothed low-band (bass kick) level in [0..1]; 0 when no Levels.</summary>
    private float kickLow;

    /// <summary>This frame's average-band energy in [0..1], or -1 when no Levels (for the debug readout).</summary>
    private float energyNow;

    /// <summary>Last frame's "kick present" state, so each bass hit is edge-detected into one bloom.</summary>
    private bool lastKick;

    /// <summary>Last frame's sixteenth-gate state, so each onset of the fill ratchet seeds exactly one burst.</summary>
    private bool lastSixteenthOn;

    /// <summary>Whether this frame is inside a fill, kept for the debug readout.</summary>
    private bool fillActive;

    /// <summary>This frame's fill build level [0..1] (the fast-attack ramp, plateaued at full), kept for the debug readout.</summary>
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
    }

    /// <summary>
    /// Resets the field and randomizes this run's growth personality, then plants one seed so the first
    /// frame is never blank.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();

        Array.Clear(charge, 0, charge.Length);
        Array.Clear(hue, 0, hue.Length);
        Array.Clear(gen, 0, gen.Length);

        // Per-activation variety: faster spread with a sharper leak reads as a crisp racing front; slower
        // spread with a gentler leak reads as a thick, creeping bloom.
        spreadPerSec = Random.Range(12f, 20f);
        leakPerSec = Random.Range(0.22f, 0.5f);
        beatSurge = Random.Range(1.5f, 3.5f);
        seedInterval = Random.Range(StandaloneSeedMin, StandaloneSeedMax);

        spreadBudget = 0f;
        generation = 1;
        hueCursor = Random.value;
        seedTimer = 0f;
        selfBeatPhase = 0f;
        selfBeatPeriod = Random.Range(SelfBeatPeriodMin, SelfBeatPeriodMax);
        selfPulse = 0f;
        lastKick = false;
        energyNow = -1f;
        fillActive = false;
        fillLevel = 0f;
        lastSixteenthOn = false;
        lastBeatInBar = beatManager.BeatInBar;

        // Arm the Drop edge to the current state so activating mid-drop doesn't fire a false flash; the flash is for
        // the drop's onset, not for being inside one.
        wasDropActive = beatManager.DropQuery is { inProgress: true };
        dropFlash = 0f;
        dropFlashElapsed = 0f;
        dropFlashSeconds = DropFlashFallbackSeconds;

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
        string mode = IsBeatActive ? "Synced" : "Standalone (self-driven)";
        string levels = energyNow < 0f
            ? "Levels: n/a"
            : $"Energy: {energyNow:0.00}{(energyNow < KickThreshold ? " (quiet)" : "")}  Kick: {(kickLow > KickThreshold ? "ON" : "--")}";
        string fillReadout = fillActive ? $"\nFILL {fillLevel:0.00} (hold-back + swell)" : "";
        string dropReadout = dropFlash > 0f ? $"\nDROP SPARKLE {dropFlash:0.00}" : "";
        return $"Crystal Growth\nMode: {mode}\nLayer: {generation}\n{levels}{fillReadout}{dropReadout}";
    }

    /// <summary>
    /// Renders one frame: decides this frame's seeds, advances the front by a framerate-independent number of
    /// ring passes, fades the trailing heat, opens the next layer when the wall is claimed, then writes the
    /// black / colored-glow / hot-rim field into <see cref="EffectBase.buffer"/>.
    /// </summary>
    public override void Draw()
    {
        float dt = effectDelta;

        // Read the live band energy first: it gates how much the wall couples to the beat. The crucial case is
        // a quiet break — when the average energy falls below a third, the audible beat is gone, so seeding, the
        // spread surge, and the brightness pulse all gate off and the crystal just keeps growing calmly instead
        // of chasing a beat nobody can hear. 'activity' is 1 (full coupling) when no Levels are available.
        float activity = ReadLevels();

        // The fill is the short transition that leads into the next phrase — and it does not always land on a
        // drop, so its gesture must build tension AND resolve cleanly on its own. The hold-back reins the base
        // spread in as the fill builds while the swell charges the crystal's glow, then both snap back the
        // moment the fill ends — into the Drop sparkle when one lands, or back to normal growth when not.
        // Because Fill.progress is normalized to the fill's length, the arc scales with it — short fills snap,
        // long fills lean in.
        PhraseEventInfo? fillInfo = beatManager.FillQuery;
        bool inFill = fillInfo is { inProgress: true };
        float progress = inFill ? (fillInfo.Value.progress ?? 1f) : 0f;
        float fillAmount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        float ratchet = beatManager.GateOf(Subdivision.Sixteenth) ?? 0f;
        fillActive = inFill;
        fillLevel = fillAmount;

        SeedThisFrame(dt);
        TriggerDropFlash(dt);
        SeedDropRatchet(ratchet);

        // The beat surges how far the front lunges this frame. Synced rides the live Pulse; Standalone falls
        // back to the self-driven surge. The surge is scaled by 'activity' so the front stops lunging to an
        // inaudible beat during a quiet break. A fill reins the whole thing in (tension); a Drop washes the
        // fresh layer across the wall and machine-gun lunges on every sixteenth for the drop's whole window.
        float pulse = (beatManager.Pulse ?? selfPulse) * activity;
        float spread = spreadPerSec
            * (1f + (beatSurge * pulse))
            * Mathf.Lerp(1f, 1f - FillHoldback, fillAmount)
            * (1f + (DropRatchetSpread * dropFlash * ratchet))
            * (1f + (DropFlashSpread * dropFlash));

        // Advance the front by whole rings, accumulating fractional passes so the rate is FPS-independent.
        spreadBudget += spread * dt;
        int passes = 0;
        while (spreadBudget >= 1f && passes < MaxFrontPassesPerFrame)
        {
            spreadBudget -= 1f;
            passes++;
            PropagateFrontOnce();
        }

        // Fade the trailing heat so the bright band trails off behind the front; grown tiles still render at the
        // CrystalFloor (keyed on gen), so they never go black.
        float keep = Mathf.Clamp01(1f - (leakPerSec * dt));
        for (int i = 0; i < charge.Length; i++)
        {
            charge[i] *= keep;
        }

        // Hold the drop layer while its flash eases: don't let coverage auto-advance to a new (multicolor)
        // generation mid-flash, so the single drop color owns the wall until the wavefront settles.
        if (dropFlash <= 0f)
        {
            CheckGenerationAdvance();
        }

        RelaxHue(dt);

        // Brightness pulses with the music; the floor is shallow (0.8) so lit tiles stay bright, and it lifts
        // toward steady as the track quietens so a quiet break never strobes to an inaudible beat.
        float bright = BeatBrightness(Mathf.Lerp(1f, 0.8f, activity));

        // Hard Drop strobe: during a Drop, every sixteenth's off-phase knocks the whole field toward black, so
        // the wall flashes on each 16th for the drop's whole window (easing out with the release). Applied to
        // the final color below — past the CrystalFloor — so the dark phase actually reads dark. Collapses to
        // 1 (no strobe) outside a Drop. The Fill instead swells the glow while the hold-back compresses growth.
        float strobe = 1f - (dropFlash * (1f - ratchet) * DropStrobeDepth);
        float swell = 1f + (FillSwell * fillAmount);

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
            float tip = Mathf.InverseLerp(0.8f, 1f, c);
            col = Color.Lerp(col, Color.white, tip * 0.5f);

            // sqrt widens the bright band: the whole active growth area stays bright and only the oldest tail
            // eases down to the floor, so the crystal reads as a defined glowing region instead of a bright dot.
            // The Drop flash adds a luminance lift weighted by front heat (c) and the eased envelope, so the boost
            // rides the sweeping leading edge — a bright colored wavefront crossing the wall — and trails back to
            // normal behind it, all in the tile's own palette color (never toward white). Collapses to ×1 off a flash.
            float factor = Mathf.Max(Mathf.Sqrt(c) * bright, CrystalFloor) * (1f + DropFlashBrightness * dropFlash * c);
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
        float k = Mathf.Min(HueRelaxPerSec * dt, 0.5f);
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
    /// Decides and plants this frame's seeds: Synced delegates to <see cref="SeedFromEnergy"/> when live band
    /// energy is available, else to <see cref="SeedFromBeat"/>; Standalone runs a self-driven metronome.
    /// </summary>
    private void SeedThisFrame(float dt)
    {
        int? beatInBar = beatManager.BeatInBar;

        if (IsBeatActive && beatInBar is { } bib)
        {
            // With live Levels, let the bass kick drive the blooms (and gate them off in a quiet break). Without
            // Levels there is no energy feed, so fall back to seeding on the beat counter itself.
            if (hasLevels)
            {
                SeedFromEnergy(dt, bib);
            }
            else
            {
                SeedFromBeat(bib);
            }

            lastBeatInBar = beatInBar;
            return;
        }

        SeedSelfDriven(dt);
    }

    /// <summary>
    /// Standalone seeding (no beat clock): a self-driven metronome that mimics the synced liveliness — a steady
    /// trickle of seeds keeps several fronts crawling at once, and a synthetic downbeat periodically blooms a
    /// burst and kicks the spread surge so the wall pulses in waves.
    /// </summary>
    private void SeedSelfDriven(float dt)
    {
        lastBeatInBar = beatManager.BeatInBar; // tracks null here; re-arms cleanly when the beat returns

        selfBeatPhase += dt / selfBeatPeriod;
        if (selfBeatPhase >= 1f)
        {
            selfBeatPhase -= 1f;
            selfBeatPeriod = Random.Range(SelfBeatPeriodMin, SelfBeatPeriodMax);
            selfPulse = 1f; // peak surge, decays below

            PlantSeeds(BloomCount());
        }

        // Steady fill between downbeats so there are always several live fronts, not one lonely crystal.
        seedTimer += dt;
        if (seedTimer >= seedInterval)
        {
            PlantSeed();
            seedTimer = 0f;
            seedInterval = Random.Range(StandaloneSeedMin, StandaloneSeedMax);
        }

        // Decay the synthetic surge toward 0 so each downbeat is a lunge, not a sustained sprint.
        selfPulse = Mathf.Max(0f, selfPulse - (dt * SelfPulseDecayPerSec));
    }

    /// <summary>
    /// Samples the live band <see cref="BeatManager.Levels"/> into this frame's <see cref="kickLow"/> /
    /// <see cref="energyNow"/> / <see cref="hasLevels"/> and returns the beat-coupling "activity" in [0..1]:
    /// 0 in a fully quiet break, 1 while the track drives, ramped across <see cref="QuietEnergy"/>..
    /// <see cref="ActiveEnergy"/> (straddling a third). Returns 1 when no Levels are available, so beat-driven
    /// behavior is unchanged wherever there is no energy feed.
    /// </summary>
    private float ReadLevels()
    {
        if (beatManager.Levels is { } levels)
        {
            hasLevels = true;
            kickLow = levels.low;
            energyNow = (levels.low + levels.mid + levels.high) / 3f;
            return Mathf.InverseLerp(QuietEnergy, ActiveEnergy, energyNow);
        }

        hasLevels = false;
        kickLow = 0f;
        energyNow = -1f;
        lastKick = false; // re-arm the kick edge detector for when Levels return
        return 1f;
    }

    /// <summary>
    /// Selects a fresh wall palette at the start of every 16-beat Grid (the base <see cref="EffectBase.OnNewGrid"/>
    /// edge — Count wraps to 1 on a Locked grid). The palette cross-fades, so the crystals recolor smoothly.
    /// </summary>
    protected override void OnNewGrid() => APalette.Change();

    /// <summary>
    /// Energy-aware seeding (used when live Levels exist): each rising edge of the bass kick blooms a burst sized
    /// by how hard it hits, while a gentle idle heartbeat keeps the wall growing at all times. In a quiet break
    /// the kick never crosses its threshold, so only the calm heartbeat runs — the crystal stops chasing the beat.
    /// </summary>
    private void SeedFromEnergy(float dt, int bib)
    {
        bool kickNow = kickLow > KickThreshold;
        if (kickNow && !lastKick)
        {
            int burst = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, Mathf.InverseLerp(KickThreshold, 1f, kickLow)));
            if (bib == 1)
            {
                burst += 2; // extra weight on the bar's one
            }

            // The fill is NOT seeded here — it gets its own drastic sixteenth ratchet in SeedFillRatchet, and the
            // Drop gets its own one-shot flash in TriggerDropFlash, so neither scatters extra seeds here.
            PlantSeeds(burst);
        }

        lastKick = kickNow;

        // Idle heartbeat: the only thing seeding during a quiet break, so the wall keeps growing calmly.
        seedTimer += dt;
        if (seedTimer >= seedInterval)
        {
            PlantSeed();
            seedTimer = 0f;
            seedInterval = Random.Range(SyncedIdleSeedMin, SyncedIdleSeedMax);
        }
    }

    /// <summary>
    /// Beat-counter seeding (used when synced but no Levels feed exists): a fresh bloom on each new beat, bigger
    /// on the bar's one. The Drop gets its own one-shot flash in <see cref="TriggerDropFlash"/>, not extra seeds
    /// here. This is the original behavior, kept for sources that carry a beat but no band energy.
    /// </summary>
    private void SeedFromBeat(int bib)
    {
        // Edge-detect a new beat: BeatInBar steps 1→2→3→4→1… so any change is a fresh beat.
        if (bib == lastBeatInBar)
        {
            return;
        }

        int seeds = bib == 1 ? 3 : 1; // the bar's one blooms several fronts at once
        PlantSeeds(seeds);
    }

    /// <summary>
    /// The Drop's sparkle seeding: on each rising edge of the sixteenth ratchet while the Drop envelope is hot,
    /// plant a fixed <see cref="DropSeedBurst"/> of fresh fronts so the wall machine-guns for the drop's whole
    /// window. Edge-detected off the hard gate so exactly one burst lands per sixteenth, not one per frame.
    /// No-op outside a Drop and in Standalone (ratchet is 0 there).
    /// </summary>
    private void SeedDropRatchet(float ratchet)
    {
        bool on = ratchet > 0.5f;
        if (on && !lastSixteenthOn && dropFlash > 0.5f)
        {
            PlantSeeds(DropSeedBurst);
        }

        lastSixteenthOn = on;
    }

    /// <summary>
    /// Injects a hot front and claims one random tile for the current generation — the origin of a new crystal.
    /// Each seed steps <see cref="hueCursor"/> by the golden ratio so its crystal grows in a fresh, well-separated
    /// palette color; many such crystals collide into a multicolor field within one generation.
    /// </summary>
    private void PlantSeed()
    {
        hueCursor = Mathf.Repeat(hueCursor + GoldenStep, 1f);

        int t = Random.Range(0, charge.Length);
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
        hueCursor = Mathf.Repeat(hueCursor + GoldenStep, 1f);
        for (int s = 0; s < count; s++)
        {
            int t = Random.Range(0, charge.Length);
            charge[t] = 1f;
            gen[t] = generation;
            hue[t] = hueCursor;
        }
    }

    /// <summary>
    /// Drives the Drop sparkle envelope. On the rising edge of <see cref="BeatManager.DropQuery"/> in-progress — the
    /// drop's beat one — it opens a fresh generation seeded in one unison hue (which the spread surge then washes
    /// across the whole wall) and snaps the envelope to 1. The envelope then eases back to 0 over
    /// <see cref="DropFadeBars"/> bars (sized from the live tempo, or <see cref="DropFlashFallbackSeconds"/> when
    /// no usable BPM is on the wire), so the sparkle is ferocious at the hit and fades back to normal growth even
    /// while the drop plays on. Edge-detected so it lands exactly once per drop.
    /// </summary>
    private void TriggerDropFlash(float dt)
    {
        bool dropActive = beatManager.DropQuery is { inProgress: true };

        if (dropActive && !wasDropActive)
        {
            // New layer in one color, surged across the wall by DropFlashSpread below.
            generation++;
            PlantUnisonSeeds(DropFlashSeeds);

            dropFlashSeconds = beatManager.Bpm is { } bpm && bpm > 0f
                ? 60f / bpm * BeatsPerBar * DropFadeBars
                : DropFlashFallbackSeconds;
            dropFlashElapsed = 0f;
            dropFlash = 1f;
        }
        else if (dropFlash > 0f)
        {
            // Ease the sparkle back to 0 across the window. SmoothStep holds near full briefly, then fades — a
            // drawn-out release rather than a linear ramp.
            dropFlashElapsed += dt;
            float u = dropFlashSeconds > 0f ? Mathf.Clamp01(dropFlashElapsed / dropFlashSeconds) : 1f;
            dropFlash = 1f - Mathf.SmoothStep(0f, 1f, u);
        }

        wasDropActive = dropActive;
    }

    /// <summary>
    /// Advances the growth front one ring: every hot tile pushes heat into its neighbors and claims any neighbor
    /// held by an older (or no) generation, carrying its color. A higher generation always wins, so the newest
    /// layer's bright front sweeps over and repaints the layers beneath it. Same-generation tiles still relay
    /// heat so the rim keeps moving. Works through the double buffers, then swaps them in.
    /// </summary>
    private void PropagateFrontOnce()
    {
        Array.Copy(charge, nextCharge, charge.Length);
        Array.Copy(gen, nextGen, gen.Length);
        Array.Copy(hue, nextHue, hue.Length);

        for (int i = 0; i < charge.Length; i++)
        {
            float c = charge[i];
            if (c <= HeatEpsilon)
            {
                continue;
            }

            float push = c * FrontPush;
            int gi = gen[i];
            float hi = hue[i];

            Penrose.neighbor[] nb = tiles[i].neighbors;
            for (int j = 0; j < nb.Length; j++)
            {
                int idx = nb[j].tileIdx;

                if (gi > nextGen[idx])
                {
                    // Claim/repaint the neighbor for this newer generation and light its front.
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

        if (claimed >= (int)(CoverageToAdvance * Penrose.Total))
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
        generation++;
        PlantSeeds(BloomCount());
    }

    /// <summary>A bloom is 3–5 seeds — used for the bar's-one bloom, a new generation, and the Standalone downbeat.</summary>
    private int BloomCount() => 3 + Random.Range(0, 3);
}
