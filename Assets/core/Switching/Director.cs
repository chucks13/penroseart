using System;
using System.Collections.Generic;
using System.Linq;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>High-level cadence source currently driving the Director.</summary>
public enum DirectorMode
{
    NotReady,
    Standalone,
    Synced,
    Hold
}

/// <summary>Read-only snapshot of the Director reducer's real state for the HUD and Unity Inspector.</summary>
public readonly struct DirectorStatus
{
    /// <summary>The snapshot shown before the Director exists: no mode, nothing staged, nothing held.</summary>
    public static DirectorStatus NotReady { get; } = new DirectorStatus(
        DirectorMode.NotReady,
        false,
        -1,
        string.Empty,
        -1,
        string.Empty,
        false,
        false);

    /// <summary>Which operating mode the Director is in this frame.</summary>
    public readonly DirectorMode Mode;

    /// <summary>True when the wall is in Synced Mode (the reducer is live).</summary>
    public readonly bool IsSyncedMode;

    /// <summary>
    /// Staged effect index for the next A-to-B move, or -1 when nothing is staged. It serves the next
    /// Standalone move and, as a one-shot override, the next Synced cue the Director answers.
    /// </summary>
    public readonly int NextEffectIndex;

    /// <summary>Display name of the staged effect, or empty when nothing is staged.</summary>
    public readonly string NextEffectName;

    /// <summary>
    /// Staged transition index for the next A-to-B move, or -1 before the Director is ready. Like
    /// <see cref="NextEffectIndex"/> it serves both the next Standalone move and the next Synced override.
    /// </summary>
    public readonly int NextTransitionIndex;

    /// <summary>Display name of the staged transition, or empty before the Director is ready.</summary>
    public readonly string NextTransitionName;

    /// <summary>Whether the staged Effect is kept after each completed move.</summary>
    public readonly bool HoldSelectedEffect;

    /// <summary>Whether the staged Transition is kept after each completed move.</summary>
    public readonly bool HoldSelectedTransition;

    /// <summary>Captures one Director snapshot for downstream HUDs and inspectors.</summary>
    /// <param name="mode">Which operating mode the Director is in this frame.</param>
    /// <param name="isSyncedMode">Whether a usable beat clock is running.</param>
    /// <param name="nextEffectIndex">Staged Effect catalog index, or -1 when nothing is staged.</param>
    /// <param name="nextEffectName">Display name of the staged Effect.</param>
    /// <param name="nextTransitionIndex">Staged Transition catalog index, or -1 before the Director is ready.</param>
    /// <param name="nextTransitionName">Display name of the staged Transition.</param>
    /// <param name="holdSelectedEffect">Whether the staged Effect is kept after each completed move.</param>
    /// <param name="holdSelectedTransition">Whether the staged Transition is kept after each completed move.</param>
    public DirectorStatus(
        DirectorMode mode,
        bool isSyncedMode,
        int nextEffectIndex,
        string nextEffectName,
        int nextTransitionIndex,
        string nextTransitionName,
        bool holdSelectedEffect,
        bool holdSelectedTransition)
    {
        Mode = mode;
        IsSyncedMode = isSyncedMode;
        NextEffectIndex = nextEffectIndex;
        NextEffectName = nextEffectName ?? string.Empty;
        NextTransitionIndex = nextTransitionIndex;
        NextTransitionName = nextTransitionName ?? string.Empty;
        HoldSelectedEffect = holdSelectedEffect;
        HoldSelectedTransition = holdSelectedTransition;
    }
}

/// <summary>
/// The Director's answer to a Switcher question (ADR-0009): whether to perform at all, and with which
/// Effect and Transition after override masking. Commands go down, questions go up — the Switcher asks,
/// the Director answers, and the Switcher executes the answer on its own timeline.
/// </summary>
public readonly struct CueDecision
{
    /// <summary>The frozen answer: the wall is under Hold (an inspection freeze) and nothing is performed.</summary>
    public static CueDecision Frozen => default;

    /// <summary>Whether the Switcher should perform this cue at all.</summary>
    public readonly bool Perform;

    /// <summary>Effect catalog index to perform, after override masking; meaningless unless <see cref="Perform"/>.</summary>
    public readonly int EffectIndex;

    /// <summary>Transition catalog index to perform, after override masking; meaningless unless <see cref="Perform"/>.</summary>
    public readonly int TransitionIndex;

    /// <summary>A performing decision carrying the masked Effect and Transition indices.</summary>
    public CueDecision(int effectIndex, int transitionIndex)
    {
        Perform = true;
        EffectIndex = effectIndex;
        TransitionIndex = transitionIndex;
    }
}

/// <summary>
/// The kind of anomaly the Switcher sights at a Grid start — the condition
/// an <see cref="OffPlanSighting"/> reports through the one anomaly doorway,
/// <see cref="Director.DecideOffPlanCue"/>. Diagnostic forever: it feeds the decision-site trace and the
/// Live tab, never the deal — two Sightings identical except for this kind get the identical answer.
/// </summary>
public enum OffPlanAnomaly
{
    /// <summary>The mark at the coming boundary has already fired, so its permanent check-off prevents replay (ADR-0011).</summary>
    FiredMark,

    /// <summary>
    /// The mark at the coming boundary blends into the Effect the wall is showing — or already moving
    /// toward mid-flight — and a Transition from an Effect into itself moves nothing.
    /// </summary>
    SelfBlend,

    /// <summary>Stillness is up: three whole Grids passed without a fired cue.</summary>
    StillnessUp,
}

/// <summary>
/// The Switcher's whole report through the anomaly doorway — the one self-describing question
/// <see cref="Director.DecideOffPlanCue"/> answers from alone: which anomaly it saw, the Grid Boundary at
/// hand, the Stillness gap, which ask this is, and what the wall is showing and moving toward.
/// Pure data: constructing one has no side effects, and the counters it snapshots stay the Switcher's own.
/// </summary>
public readonly struct OffPlanSighting
{
    /// <summary>
    /// The anomaly the Switcher saw. Diagnostic forever: it feeds the decision-site trace and the Live tab,
    /// never the deal — Stillness pressure already rides in <see cref="GapGrids"/>.
    /// </summary>
    public readonly OffPlanAnomaly Anomaly;

    /// <summary>
    /// Absolute Grid Boundary beat being asked about — the Grid start the Switcher is standing on, where a
    /// cue taken here starts its blend.
    /// </summary>
    public readonly int BoundaryBeat;

    /// <summary>
    /// The gap in whole Grids since the last fired cue. At
    /// <see cref="TrackCueSheet.MaximumGapGrids"/> the deal is taken no matter what.
    /// </summary>
    public readonly int GapGrids;

    /// <summary>
    /// Which off-plan ask this is on the current run. The Director remembers nothing across asks, so this
    /// is what separates one deal from the next when the same boundary is sighted again.
    /// </summary>
    public readonly int Ask;

    /// <summary>Index of the Effect the wall is showing; a dealt cue is never this.</summary>
    public readonly int OnWallEffectIndex;

    /// <summary>
    /// Index of the Effect a mid-flight Transition is moving toward — the on-wall index again when nothing
    /// is in flight; a dealt cue is never this either.
    /// </summary>
    public readonly int MovingTowardEffectIndex;

    /// <summary>Captures one report through the anomaly doorway.</summary>
    /// <param name="anomaly">The anomaly the Switcher saw; diagnostic only, never a decision input.</param>
    /// <param name="boundaryBeat">Absolute Grid Boundary beat being asked about.</param>
    /// <param name="gapGrids">The gap in whole Grids riding through this boundary would let the wall reach.</param>
    /// <param name="ask">Which off-plan ask this is on the current run.</param>
    /// <param name="onWallEffectIndex">Index of the Effect the wall is showing.</param>
    /// <param name="movingTowardEffectIndex">Index of the Effect a mid-flight Transition is moving toward.</param>
    public OffPlanSighting(
        OffPlanAnomaly anomaly,
        int boundaryBeat,
        int gapGrids,
        int ask,
        int onWallEffectIndex,
        int movingTowardEffectIndex)
    {
        Anomaly = anomaly;
        BoundaryBeat = boundaryBeat;
        GapGrids = gapGrids;
        Ask = ask;
        OnWallEffectIndex = onWallEffectIndex;
        MovingTowardEffectIndex = movingTowardEffectIndex;
    }
}

/// <summary>
/// Decides what plays; it never times a fire (ADR-0009). In Synced Mode it builds one
/// track-scoped <see cref="TrackCueSheet"/> per player the moment that player's structure generation
/// changes, hands the on-air focus player's sheet to the <see cref="Switcher"/> every tick (an idempotent
/// Cast), and answers the Switcher's two questions — what a planned mark plays, and whether an anomaly at a
/// Grid start gets a fresh cue or is ridden through. It holds no Runway arithmetic, follows no
/// position, observes no Grid, and keeps no cast memory: execution belongs wholly to the Switcher.
/// Standalone Mode (timer-driven, no wire) is unchanged.
/// </summary>
public sealed class Director
{
    private readonly Controller controller;
    private readonly Switcher switcher;
    private readonly Timer standaloneTimer;
    private readonly int[] effectDeck;
    private readonly int[] transitionDeck;

    private int nextEffectIndex = -1;
    private int nextTransitionIndex;
    private bool holdSelectedEffect;
    private bool holdSelectedTransition;

    /// <summary>
    /// Whether a manually staged Effect is waiting to mask exactly one cue (ADR-0009). A manual
    /// <see cref="SetNextEffect"/> raises it; answering one cue consumes it, so the plan resumes verbatim.
    /// Auto-staging and enabling a Hold both clear it. Overrides mask, never mutate — the sheet stays a pure
    /// function of (structure, seed).
    /// </summary>
    private bool overrideEffectPending;

    /// <summary>The same one-shot mask for a manually staged Transition; see <see cref="overrideEffectPending"/>.</summary>
    private bool overrideTransitionPending;

    /// <summary>The last mode written to the trace, so a steady mode is logged once rather than every frame.</summary>
    private DirectorMode lastLoggedMode = DirectorMode.NotReady;

    /// <summary>How many physical players the wire carries, and so how many sheet slots the Director keeps.</summary>
    private const int PlayerCount = RaveWireSnapshot.PlayerCount;

    /// <summary>
    /// One track-scoped Cue Sheet per physical player, mirroring the Players group. A slot is (re)built when
    /// that player's structure generation changes (inequality only, never ordering); the seed is (generation,
    /// player number) and Track ID plays no role. A slot whose sheet carries a
    /// <see cref="TrackCueSheet.StructureGeneration"/> of zero holds no plan.
    /// </summary>
    private readonly TrackCueSheet[] sheets = new TrackCueSheet[PlayerCount];

    /// <summary>
    /// Run-scoped seed salt folded into every sheet deal (ADR-0008). Drawn once when the Director is
    /// created, so within the run every rebuild stays deterministic and handover identity is untouched,
    /// while each run deals a fresh show even when the wire's generation counters restart identically.
    /// Traced with every SHEET_BUILT line, and settable so a logged run can be reproduced — or a test
    /// pinned — by assigning the salt before any structure arrives.
    /// </summary>
    public int SheetSalt { get; set; } = Guid.NewGuid().GetHashCode();

    /// <summary>
    /// The Cue Sheet built for each physical player slot, indexed by player number minus one. A slot whose
    /// <see cref="TrackCueSheet.StructureGeneration"/> is zero has no sheet. Exposed read-only so debug views
    /// can show what the Director planned for every loaded track, not just the one on air.
    /// </summary>
    public IReadOnlyList<TrackCueSheet> Sheets => sheets;

    /// <summary>
    /// Binds the one Director to everything it decides with and stages its opening choices. Every dependency is
    /// required, so a constructed Director is always ready to answer.
    /// </summary>
    /// <param name="controller">The runtime hub owning the Effect and Transition catalogs and the beat clock.</param>
    /// <param name="switcher">The Switcher this Director hands plans to and pushes immediate moves down into.</param>
    /// <param name="standaloneTimer">The cadence clock driving Standalone Mode.</param>
    /// <param name="effectDeck">Catalog indices the automatic Effect selection pulls from.</param>
    /// <param name="transitionDeck">Catalog indices the automatic Transition selection pulls from.</param>
    /// <param name="initialTransitionIndex">Transition staged before the first move.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public Director(
        Controller controller,
        Switcher switcher,
        Timer standaloneTimer,
        int[] effectDeck,
        int[] transitionDeck,
        int initialTransitionIndex)
    {
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        this.controller = controller;
        this.switcher = switcher ?? throw new ArgumentNullException(nameof(switcher));
        this.standaloneTimer = standaloneTimer ?? throw new ArgumentNullException(nameof(standaloneTimer));
        this.effectDeck = effectDeck ?? throw new ArgumentNullException(nameof(effectDeck));
        this.transitionDeck = transitionDeck ?? throw new ArgumentNullException(nameof(transitionDeck));
        SetNextTransition(initialTransitionIndex);
        // Initial seeding is not an operator override: clear the pending mask SetNextTransition just raised.
        overrideTransitionPending = false;
        AutoStageNextEffect(switcher.TransitionTargetEffectIndex);
    }

    /// <summary>
    /// Whether the wall is in Synced Mode: a usable beat clock is running. Reads the single mode authority
    /// (<see cref="BeatManager.IsSynced"/>), not OSC transport liveness (ADR-0003).
    /// </summary>
    public bool IsSyncedMode => controller != null && controller.beatManager != null && controller.beatManager.IsSynced;

    /// <summary>Current read-only reducer snapshot for runtime HUDs and inspector diagnostics.</summary>
    public DirectorStatus Status => BuildStatus();

    /// <summary>Index of the Effect staged for the next A-to-B move — Standalone, or a one-shot Synced override.</summary>
    public int NextEffectIndex => nextEffectIndex;

    /// <summary>Index of the Transition staged for the next A-to-B move — Standalone, or a one-shot Synced override.</summary>
    public int NextTransitionIndex => nextTransitionIndex;

    /// <summary>Whether the staged Effect should be kept after each completed move.</summary>
    public bool HoldSelectedEffect => holdSelectedEffect;

    /// <summary>Whether the staged Transition should be kept after each completed move.</summary>
    public bool HoldSelectedTransition => holdSelectedTransition;

    /// <summary>
    /// Stages the Effect for the next A-to-B move: the next Standalone move, and — as a one-shot ADR-0009
    /// override — exactly the next synced cue, which plays this pick instead of its dealt card before the
    /// plan resumes verbatim. Masks, never mutates the sheet.
    /// </summary>
    /// <param name="effectIndex">Effect catalog index to stage.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="effectIndex"/> is outside the catalog.</exception>
    public void SetNextEffect(int effectIndex)
    {
        ValidateEffectIndex(effectIndex);
        nextEffectIndex = effectIndex;
        overrideEffectPending = true;
        Trace(() => $"NEXT_EFFECT_SET nextEffect={FormatEffect(nextEffectIndex)} hold={holdSelectedEffect}");
    }

    /// <summary>
    /// Stages the Transition for the next A-to-B move: the next Standalone move, and — as a one-shot ADR-0009
    /// override — exactly the next synced cue, before the plan resumes verbatim. Masks, never mutates.
    /// </summary>
    /// <param name="transitionIndex">Transition catalog index to stage.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="transitionIndex"/> is outside the catalog.</exception>
    public void SetNextTransition(int transitionIndex)
    {
        ValidateTransitionIndex(transitionIndex);
        nextTransitionIndex = transitionIndex;
        overrideTransitionPending = true;
        controller.currentTransition = nextTransitionIndex;
        Trace(() => $"NEXT_TRANSITION_SET nextTransition={FormatTransition(nextTransitionIndex)} hold={holdSelectedTransition}");
    }

    /// <summary>
    /// Holds the staged Effect: it is re-staged after each Standalone move, and in Synced Mode it trumps every
    /// dealt card while held (marks keep firing on cadence with the held pick). Enabling a Hold clears any
    /// pending one-shot override so releasing it lands on exactly what the sheet says.
    /// </summary>
    public void SetHoldSelectedEffect(bool hold)
    {
        holdSelectedEffect = hold;
        if (hold)
        {
            overrideEffectPending = false;
        }

        Trace(() => $"NEXT_EFFECT_HOLD_SET hold={holdSelectedEffect} nextEffect={FormatEffect(nextEffectIndex)}");
    }

    /// <summary>
    /// Holds the staged Transition: re-staged after each Standalone move, and in Synced Mode it trumps every
    /// dealt card while held. Enabling a Hold clears any pending one-shot override so release lands on the plan.
    /// </summary>
    public void SetHoldSelectedTransition(bool hold)
    {
        holdSelectedTransition = hold;
        if (hold)
        {
            overrideTransitionPending = false;
        }

        Trace(() => $"NEXT_TRANSITION_HOLD_SET hold={holdSelectedTransition} nextTransition={FormatTransition(nextTransitionIndex)}");
    }

    /// <summary>Advances the Director's Standalone cadence clock or, in Synced Mode, the plan-driven reducer.</summary>
    public void Tick(float deltaTime)
    {
        LogModeIfChanged();

        // The mode authority alone owns the Synced/Standalone fallthrough (ADR-0003).
        if (IsSyncedMode)
        {
            TickSyncedMode();
        }
        else
        {
            TickStandaloneMode(deltaTime);
        }
    }

    /// <summary>
    /// The operator's immediate pick — a keyboard jump, an OSC button, or engaging a Held Effect.
    /// Performs <paramref name="effectIndex"/> as a real Transition (the staged card, started at this
    /// instant with no Runway) rather than cutting to it. Fire-and-forget: the plan in force and its
    /// check-offs are left alone, so the sheet simply resumes at its next incoming Cue Mark.
    /// <paramref name="durationSeconds"/> re-arms the Standalone cadence.
    /// </summary>
    public void ShowNow(int effectIndex, float durationSeconds)
    {
        ValidateEffectIndex(effectIndex);
        var transitionIndex = nextTransitionIndex;
        ValidateTransitionIndex(transitionIndex);

        Trace(() => $"SHOW_NOW effect={FormatEffect(effectIndex)} via={FormatTransition(transitionIndex)} durationSeconds={durationSeconds:0.###} synced={controller.beatManager.IsSynced} beat={FormatNullableBeat(controller.beatManager.Timing.Beat)}");
        switcher.StartTransition(effectIndex, transitionIndex, Time.time);
        standaloneTimer.Set(durationSeconds);
        standaloneTimer.Reset();
        // Restages the following pick and, with it, the Controller's transition mirror.
        StageNextChoices();
    }

    /// <summary>Applies Hold as an inspection freeze: keeps the held effect on stage, suspending rotation.</summary>
    public void ApplyHold()
    {
        if (!controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            return;
        }

        if (switcher.TransitionTargetEffectIndex != heldEffectIndex)
        {
            ShowNow(heldEffectIndex, controller.effectTime);
        }
        else
        {
            standaloneTimer.Reset();
        }
    }

    /// <summary>Standalone Mode timer callback.</summary>
    public void OnTimerFinished()
    {
        if (IsSyncedMode)
        {
            Trace(() => $"TIMER_IGNORED_SYNC beat={FormatNullableBeat(controller.beatManager.Timing.Beat)}");
            return;
        }

        Trace(() => "TIMER_FINISHED_STANDALONE");
        RunStandaloneTimerDecision();
    }

    /// <summary>Advances Standalone cadence after clearing synchronized reducer state.</summary>
    private void TickStandaloneMode(float deltaTime)
    {
        // The mode boundary clears the sheet slots and the Switcher's in-force sheet so no stale plan
        // crosses a Standalone gap. Idempotent every frame (ADR-0003).
        ResetReducerMemory();
        standaloneTimer.Update(deltaTime);
    }

    /// <summary>
    /// Runs the decider for one frame: maintains the six sheet slots, then hands the on-air focus
    /// player's sheet over. <see cref="Switcher.Cast"/> is idempotent on the sheet's identity, so casting
    /// every tick keeps the Director free of handover memory; no focus or no built sheet casts a default
    /// sheet, which clears the plan in force.
    /// </summary>
    private void TickSyncedMode()
    {
        MaintainSheets();
        switcher.Cast(TryResolveFocusSheet(out var sheet) ? sheet : default);
    }

    /// <summary>
    /// Rebuilds each player's Cue Sheet slot when that player's structure generation changes. The built sheet
    /// carries the generation it was built from, so the slot is its own record of what has been planned. A sheet
    /// is built only from a complete structure (visible phrase list equals the announced count); until it
    /// converges the slot is left alone so the build retries. Generation zero (no structure) clears the slot.
    /// Determinism replaces caching: the seed is (generation, player number).
    /// </summary>
    private void MaintainSheets()
    {
        var players = controller.beatManager.Players;
        for (var slot = 0; slot < PlayerCount; slot++)
        {
            var structure = players[slot].Structure;
            var generation = structure.Generation;
            if (generation == sheets[slot].StructureGeneration)
            {
                continue;
            }

            if (generation <= 0)
            {
                sheets[slot] = default;
                continue;
            }

            if (structure.PhraseCount <= 0 || structure.Phrases.Count != structure.PhraseCount)
            {
                // Structure not yet complete: keep playing and retry next frame, leaving the slot as it was.
                continue;
            }

            var playerNumber = slot + 1;
            var built = TrackCueSheet.Build(
                structure,
                BuildEffectDescriptors(),
                BuildTransitionDescriptors(),
                generation,
                playerNumber,
                SheetSalt);
            sheets[slot] = built;
            // The mark-beat list makes a session log self-describing: whether a boundary carried a mark is
            // a grep, not a reconstruction (2026-07-31: a silent 81..145 stretch could not say either way).
            Trace(() => $"SHEET_BUILT player={playerNumber} generation={generation} salt={SheetSalt} marks={built.Marks.Count} beats={string.Join("|", built.Marks.Select(m => m.Beat))}");
        }
    }

    /// <summary>
    /// Resolves the on-air focus player's built Cue Sheet. False when there is no focus or no built sheet
    /// for it, each degraded silently (the wall keeps playing); <paramref name="sheet"/> is then default.
    /// </summary>
    private bool TryResolveFocusSheet(out TrackCueSheet sheet)
    {
        sheet = default;
        if (controller.beatManager.LiveOrder.Focus is not { } focus)
        {
            return false;
        }

        var slot = focus - 1;
        if (slot < 0 || slot >= PlayerCount || sheets[slot].StructureGeneration <= 0)
        {
            return false;
        }

        sheet = sheets[slot];
        return true;
    }

    /// <summary>
    /// Answers what the Switcher should perform for a planned Cue Mark (ADR-0009): frozen under Hold — an
    /// inspection freeze, so nothing performs and the mark is left unfired — otherwise the mark's baked cards
    /// with the one-shot override masks applied. Releasing a Hold does not chase a mark that came due while
    /// frozen: a Transition only ever leaves on a Runway beat, so the wall waits for the next think. The
    /// sheet is never mutated; overrides mask. The Switcher asks this once, at the Grid-start think, so
    /// this is where a staged one-shot override is consumed — staged overrides apply from the next think
    /// onward, and the decided cue then runs fire-and-forget through its scheduled act.
    /// </summary>
    /// <param name="mark">The planned Cue Mark on the thinking Grid's closing boundary.</param>
    /// <returns>What to perform, or <see cref="CueDecision.Frozen"/> when the wall is held.</returns>
    public CueDecision DecideCue(CuePlanMark mark)
    {
        return Decide(mark.EffectIndex, mark.TransitionIndex);
    }

    /// <summary>
    /// The one anomaly doorway. Whenever the Switcher sights a fired mark, self-blend, or Stillness at a Grid
    /// start, it reports one self-describing <see cref="OffPlanSighting"/>. The Director answers from that
    /// argument alone: ride through (no-perform), or a fresh Off-Plan Cue, Director-cast as always. A fresh cue
    /// lands at the closing Grid Boundary through the Switcher's normal scheduler. Taking becomes certain once
    /// the Sighting reaches <see cref="TrackCueSheet.MaximumGapGrids"/>. A dealt cue is never the Effect on the
    /// wall or the one being moved toward, and it leaves the Cue Sheet exactly as it was. Frozen under Hold,
    /// and when there is no focus or no built sheet.
    /// </summary>
    /// <param name="sighting">
    /// The Switcher's whole report — everything this answer may draw on. Its anomaly kind is diagnostic
    /// only: it feeds the trace lines here, never the deal.
    /// </param>
    /// <returns>What to perform, or <see cref="CueDecision.Frozen"/> for a ride-through or a held wall.</returns>
    public CueDecision DecideOffPlanCue(OffPlanSighting sighting)
    {
        if (!TryDealOffPlan(sighting, out var dealt))
        {
            return CueDecision.Frozen;
        }

        if (!dealt.Take)
        {
            Trace(() => $"DECIDE_OFF_PLAN_RIDE anomaly={sighting.Anomaly} beat={sighting.BoundaryBeat} gapGrids={sighting.GapGrids} ask={sighting.Ask} onWall={FormatEffect(sighting.OnWallEffectIndex)} toward={FormatEffect(sighting.MovingTowardEffectIndex)}");
            return CueDecision.Frozen;
        }

        // The take is traced with the gap and ask that produced it so a log alone can tell a certain-row
        // take from a lucky low-gap roll (2026-07-28: two sessions were indistinguishable without this).
        // A take can still be refused below by Hold, so this line records the deal, not the perform.
        Trace(() => $"DECIDE_OFF_PLAN_TAKE anomaly={sighting.Anomaly} beat={sighting.BoundaryBeat} gapGrids={sighting.GapGrids} ask={sighting.Ask} onWall={FormatEffect(sighting.OnWallEffectIndex)} toward={FormatEffect(sighting.MovingTowardEffectIndex)}");
        return Decide(dealt.EffectIndex, dealt.TransitionIndex);
    }

    /// <summary>
    /// The single deal <see cref="DecideOffPlanCue"/> reads, fed entirely from the Sighting — the Director
    /// reads nothing back from the Switcher to answer. The card is seeded by the boundary and ask alone and
    /// never hands back the Effect the wall is showing or moving toward — an off-plan cue is asked for
    /// precisely because the wall must change. False when no focus sheet is in force to deal from.
    /// </summary>
    /// <param name="sighting">The Switcher's report; supplies every scalar the sheet's deal takes.</param>
    /// <param name="dealt">The dealt card, when a focus sheet was in force.</param>
    /// <returns>Whether a focus sheet was in force to deal from.</returns>
    private bool TryDealOffPlan(OffPlanSighting sighting,
        out (int EffectIndex, int TransitionIndex, bool Take) dealt)
    {
        if (!TryResolveFocusSheet(out var sheet))
        {
            dealt = default;
            return false;
        }

        dealt = sheet.DealOffPlanCueAt(
            sighting.BoundaryBeat,
            sighting.GapGrids,
            sighting.Ask,
            sighting.OnWallEffectIndex,
            sighting.MovingTowardEffectIndex);
        return true;
    }

    /// <summary>
    /// The one decision policy behind both questions (ADR-0009): Hold freezes the wall and answers
    /// no-perform; otherwise the plan-dealt cards pass through the one-shot override masks.
    /// </summary>
    private CueDecision Decide(int planEffectIndex, int planTransitionIndex)
    {
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            Trace(() => $"DECIDE_FROZEN held={FormatEffect(heldEffectIndex)} planned={FormatEffect(planEffectIndex)}/{FormatTransition(planTransitionIndex)}");
            return CueDecision.Frozen;
        }

        var decision = new CueDecision(ResolveEffectOverride(planEffectIndex), ResolveTransitionOverride(planTransitionIndex));
        if (decision.EffectIndex != planEffectIndex || decision.TransitionIndex != planTransitionIndex)
        {
            Trace(() => $"DECIDE_MASKED planned={FormatEffect(planEffectIndex)}/{FormatTransition(planTransitionIndex)} decided={FormatEffect(decision.EffectIndex)}/{FormatTransition(decision.TransitionIndex)} holdEffect={holdSelectedEffect} holdTransition={holdSelectedTransition}");
        }

        return decision;
    }

    /// <summary>
    /// Applies override masking to a plan-dealt Effect (ADR-0009): a Hold trumps every deal and returns the
    /// held pick; otherwise a one-shot staged pick replaces exactly this cue and is then consumed; with neither,
    /// the plan's card plays. A masking read only — the sheet is never mutated.
    /// </summary>
    private int ResolveEffectOverride(int planEffectIndex)
    {
        if (holdSelectedEffect)
        {
            return nextEffectIndex;
        }

        if (overrideEffectPending)
        {
            overrideEffectPending = false;
            return nextEffectIndex;
        }

        return planEffectIndex;
    }

    /// <summary>
    /// Applies override masking to a plan-dealt Transition (ADR-0009): a Hold trumps every deal; otherwise a
    /// one-shot staged pick replaces exactly this cue and is then consumed; with neither, the plan's card plays.
    /// </summary>
    private int ResolveTransitionOverride(int planTransitionIndex)
    {
        if (holdSelectedTransition)
        {
            return nextTransitionIndex;
        }

        if (overrideTransitionPending)
        {
            overrideTransitionPending = false;
            return nextTransitionIndex;
        }

        return planTransitionIndex;
    }

    /// <summary>Builds the Effect catalog as descriptors — one repertoire per position — for the sheet builder.</summary>
    private IReadOnlyList<EffectDescriptor> BuildEffectDescriptors()
    {
        var effects = controller.effects;
        var descriptors = new EffectDescriptor[effects.Length];
        for (var i = 0; i < effects.Length; i++)
        {
            descriptors[i] = new EffectDescriptor(effects[i].Repertoire);
        }

        return descriptors;
    }

    /// <summary>Builds the Transition catalog as descriptors — one repertoire per position — for the sheet builder.</summary>
    private IReadOnlyList<TransitionDescriptor> BuildTransitionDescriptors()
    {
        var transitions = controller.transitions;
        var descriptors = new TransitionDescriptor[transitions.Length];
        for (var i = 0; i < transitions.Length; i++)
        {
            descriptors[i] = new TransitionDescriptor(transitions[i].Repertoire);
        }

        return descriptors;
    }

    /// <summary>
    /// Clears the sheet slots and the Switcher's in-force sheet across a mode boundary, forcing a fresh
    /// build and handover on return to Synced Mode.
    /// </summary>
    private void ResetReducerMemory()
    {
        Array.Clear(sheets, 0, sheets.Length);
        switcher.Cast(default);
    }

    /// <summary>Builds the downstream read-only view of the Director's current state.</summary>
    private DirectorStatus BuildStatus()
    {
        var isSynced = IsSyncedMode;
        var isHeld = controller.TryGetHeldEffectIndex(out _);
        var mode = isHeld ? DirectorMode.Hold : isSynced ? DirectorMode.Synced : DirectorMode.Standalone;

        return new DirectorStatus(
            mode,
            isSynced,
            nextEffectIndex,
            EffectName(nextEffectIndex),
            nextTransitionIndex,
            TransitionName(nextTransitionIndex),
            holdSelectedEffect,
            holdSelectedTransition);
    }

    /// <summary>Emits one deferred trace when the Director's displayed mode changes.</summary>
    private void LogModeIfChanged()
    {
        var mode = IsSyncedMode ? DirectorMode.Synced : DirectorMode.Standalone;
        if (controller.TryGetHeldEffectIndex(out _))
        {
            mode = DirectorMode.Hold;
        }

        if (mode == lastLoggedMode)
        {
            return;
        }

        Trace(() => $"MODE {lastLoggedMode}->{mode} synced={controller.beatManager.IsSynced} beat={FormatNullableBeat(controller.beatManager.Timing.Beat)}");
        lastLoggedMode = mode;
    }

    /// <summary>Consumes one Standalone timer wake, honoring Effect Hold or starting the staged move.</summary>
    private void RunStandaloneTimerDecision()
    {
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            Trace(() => $"STANDALONE_HOLD held={FormatEffect(heldEffectIndex)} current={FormatEffect(switcher.TransitionTargetEffectIndex)}");
            if (switcher.TransitionTargetEffectIndex != heldEffectIndex)
            {
                ShowNow(heldEffectIndex, controller.effectTime);
            }
            else
            {
                standaloneTimer.Reset();
            }

            return;
        }

        var transitionIndex = nextTransitionIndex;
        var targetEffectIndex = nextEffectIndex;
        ValidateTransitionIndex(transitionIndex);
        ValidateEffectIndex(targetEffectIndex);
        var transitionRepertoire = controller.transitions[transitionIndex].Repertoire;
        var transitionDurationSeconds = transitionRepertoire.DefaultDurationSeconds;
        Trace(() => $"STANDALONE_TRANSITION_START transition={FormatTransition(transitionIndex)} target={FormatEffect(targetEffectIndex)} durationSeconds={transitionDurationSeconds:0.###}");
        switcher.StartTransition(targetEffectIndex, transitionIndex, Time.time);
        controller.currentTransition = transitionIndex;
        StageNextChoices();
        standaloneTimer.Set(transitionDurationSeconds + controller.effectTime);
        standaloneTimer.Reset();
    }

    /// <summary>Stages the next Standalone choices from the stage's current destination effect.</summary>
    private void StageNextChoices()
    {
        AutoStageNextEffect(switcher.TransitionTargetEffectIndex);
        StageNextTransition();
    }

    /// <summary>
    /// Pulls the next Standalone Effect off the deck unless the existing staged choice is held. Automatic, so
    /// unlike the operator's <see cref="SetNextEffect"/> it never raises a one-shot override.
    /// </summary>
    /// <param name="currentEffectIndex">Effect the stage is heading to, excluded from the pull; negative excludes nothing.</param>
    private void AutoStageNextEffect(int currentEffectIndex)
    {
        if (holdSelectedEffect)
        {
            Trace(() => $"NEXT_EFFECT_HELD nextEffect={FormatEffect(nextEffectIndex)}");
            return;
        }

        nextEffectIndex = Deck.PullRandom(
            effectDeck,
            candidateIndex => currentEffectIndex < 0 || candidateIndex != currentEffectIndex,
            (minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive));
        // An auto-staged pick is not an operator override, so it never masks a synced cast.
        overrideEffectPending = false;
        Trace(() => $"NEXT_EFFECT_STAGED nextEffect={FormatEffect(nextEffectIndex)}");
    }

    /// <summary>Stages the next Standalone Transition unless the existing staged choice is held.</summary>
    private void StageNextTransition()
    {
        if (holdSelectedTransition)
        {
            controller.currentTransition = nextTransitionIndex;
            Trace(() => $"NEXT_TRANSITION_HELD nextTransition={FormatTransition(nextTransitionIndex)}");
            return;
        }

        nextTransitionIndex = Deck.PullRandom(
            transitionDeck,
            _ => true,
            (minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive));
        // An auto-staged pick is not an operator override, so it never masks a synced cast.
        overrideTransitionPending = false;
        controller.currentTransition = nextTransitionIndex;
        Trace(() => $"NEXT_TRANSITION_STAGED nextTransition={FormatTransition(nextTransitionIndex)}");
    }

    private bool IsValidEffectIndex(int effectIndex)
    {
        return controller.effects != null && effectIndex >= 0 && effectIndex < controller.effects.Length;
    }

    private bool IsValidTransitionIndex(int transitionIndex)
    {
        return controller.transitions != null && transitionIndex >= 0 && transitionIndex < controller.transitions.Length;
    }

    private void ValidateEffectIndex(int effectIndex)
    {
        if (!IsValidEffectIndex(effectIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(effectIndex), effectIndex, "Effect index is outside the runtime catalog.");
        }
    }

    private void ValidateTransitionIndex(int transitionIndex)
    {
        if (!IsValidTransitionIndex(transitionIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(transitionIndex), transitionIndex, "Transition index is outside the runtime catalog.");
        }
    }

    private string EffectName(int effectIndex)
    {
        return IsValidEffectIndex(effectIndex) ? controller.effects[effectIndex].Name : string.Empty;
    }

    private string TransitionName(int transitionIndex)
    {
        return IsValidTransitionIndex(transitionIndex) ? controller.transitions[transitionIndex].Name : string.Empty;
    }

    /// <summary>Writes a Director trace whose display values are resolved only when tracing is enabled.</summary>
    private void Trace(Func<string> message)
    {
        controller.LogDirectorSwitching(() => $"Director {message()}");
    }

    private string FormatEffect(int effectIndex)
    {
        return effectIndex >= 0 && effectIndex < controller.effects.Length
            ? $"{effectIndex}:{controller.effects[effectIndex].Name}"
            : $"{effectIndex}:<none>";
    }

    private string FormatTransition(int transitionIndex)
    {
        return transitionIndex >= 0 && transitionIndex < controller.transitions.Length
            ? $"{transitionIndex}:{controller.transitions[transitionIndex].Name}"
            : $"{transitionIndex}:<none>";
    }

    private static string FormatNullableBeat(int? beat)
    {
        return beat is { } value ? value.ToString() : "none";
    }
}
