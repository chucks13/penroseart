using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using RepertoireFlags = Repertoire;

// Exact line-shape tests for the operator-facing Cue Log. The pure CueLogFormat cases assert the five event
// grammars with no clock or file IO; the sink cases exercise the display-side join and lazy file behavior over
// an in-memory writer; the seam cases drive the real Director/Switcher (feed beats, capture sink lines).

public sealed class CueLogFormatTests
{
    private static readonly GridInfo GridAt13 = new GridInfo(GridState.Locked, 13, 4, 0f);
    private static readonly GridInfo GridAt1 = new GridInfo(GridState.Locked, 1, 1, 0f);

    [Test]
    public void SheetBuiltLineHasTheAgreedShape()
    {
        Assert.That(
            CueLogFormat.SheetBuilt(CueLogSlot.Current, CueLogBuildReason.Build, GridAt13, "chorus", 600, 64, new[] { 16, 48, 64 }),
            Is.EqualTo("SHEET_BUILT slot=current reason=build grid=13/16 bar=4 phrase=\"chorus\" start=600 length=64 marks=[16,48,64]"));
    }

    [Test]
    public void SheetBuiltRendersRebuildAndNextSlotTokens()
    {
        Assert.That(
            CueLogFormat.SheetBuilt(CueLogSlot.Next, CueLogBuildReason.Rebuild, GridAt13, "break", 664, 16, new[] { 16 }),
            Does.StartWith("SHEET_BUILT slot=next reason=rebuild "));
    }

    [Test]
    public void CueCastLineHasTheAgreedShape()
    {
        Assert.That(
            CueLogFormat.CueCast(GridAt1, "chorus", 632, 32, 64, "RingsOfFire", "Sweep", CueFlavor.Fill, accepted: true),
            Is.EqualTo("CUE_CAST grid=1/16 bar=1 phrase=\"chorus\" cue=632[32/64] effect=\"RingsOfFire\" transition=\"Sweep\" flavor=fill result=accepted"));
    }

    [Test]
    public void CueCastRendersDropFlavorAndRejectedResult()
    {
        Assert.That(
            CueLogFormat.CueCast(GridAt1, "chorus", 632, 32, 64, "RingsOfFire", "Sweep", CueFlavor.Drop, accepted: false),
            Does.Contain("flavor=drop result=rejected"));
    }

    [Test]
    public void CueKeptLineHasTheAgreedShape()
    {
        Assert.That(
            CueLogFormat.CueKept(GridAt1, "chorus", 648, 680, 80, 80, "RingsOfFire", "Sweep"),
            Is.EqualTo("CUE_KEPT grid=1/16 bar=1 phrase=\"chorus\" offered=648 loaded=680[80/80] effect=\"RingsOfFire\" transition=\"Sweep\""));
    }

    [Test]
    public void CueLoadedLineHasTheAgreedShapeForAFreshLoad()
    {
        Assert.That(
            CueLogFormat.CueLoaded(GridAt1, "chorus", 632, 32, 64, "RingsOfFire", "Sweep", 628, 627, 4, 2, CueLogUpsert.New, displacedCueMarkBeat: null),
            Is.EqualTo("CUE_LOADED grid=1/16 bar=1 phrase=\"chorus\" cue=632[32/64] effect=\"RingsOfFire\" transition=\"Sweep\" start=628 lock=627 runway=4 tail=2 upsert=new"));
    }

    [Test]
    public void CueLoadedAppendsDisplacedOnAnUpsertReplace()
    {
        Assert.That(
            CueLogFormat.CueLoaded(GridAt1, "chorus", 632, 32, 64, "RingsOfFire", "Sweep", 628, 627, 4, 2, CueLogUpsert.Replaced, displacedCueMarkBeat: 700),
            Does.EndWith("upsert=replaced displaced=700"));
    }

    [Test]
    public void CueLockedRendersViaBeatAndViaRender()
    {
        Assert.That(
            CueLogFormat.CueLocked(GridAt13, "chorus", 632, 32, 64, "RingsOfFire", "Sweep", 627, CueLockVia.Beat),
            Is.EqualTo("CUE_LOCKED grid=13/16 bar=4 phrase=\"chorus\" cue=632[32/64] effect=\"RingsOfFire\" transition=\"Sweep\" lockedAt=627 via=beat"));
        Assert.That(
            CueLogFormat.CueLocked(GridAt13, "chorus", 632, 32, 64, "RingsOfFire", "Sweep", 627, CueLockVia.Render),
            Does.EndWith("via=render"));
    }

    [Test]
    public void PhraseTurnoverLineHasTheAgreedShape()
    {
        Assert.That(
            CueLogFormat.PhraseTurnover(GridAt13, outgoingLabel: "Chorus", outgoingLength: 64, incomingLabel: "Drop", incomingLength: 16, instance: 5),
            Is.EqualTo("PHRASE_TURNOVER grid=13/16 bar=4 out=\"Chorus\"/64 in=\"Drop\"/16 instance=5"));
    }

    [Test]
    public void PhraseTurnoverMakesASameAnnouncementWrapPlainlyVisible()
    {
        // The two-Chorus case: a wrap between two Chorus/64 phrases is its own line, both sides identical, and
        // the instance ordinal distinguishes the two real phrases a name comparison would collapse.
        Assert.That(
            CueLogFormat.PhraseTurnover(GridAt1, outgoingLabel: "Chorus", outgoingLength: 64, incomingLabel: "Chorus", incomingLength: 64, instance: 2),
            Is.EqualTo("PHRASE_TURNOVER grid=1/16 bar=1 out=\"Chorus\"/64 in=\"Chorus\"/64 instance=2"));
    }

    [Test]
    public void NextPhraseLineHasTheAgreedShapeForAReplacement()
    {
        Assert.That(
            CueLogFormat.NextPhrase(GridAt1, newLabel: "Drop", newLength: 16, replacedLabel: "Chorus", replacedLength: 64, instance: 6),
            Is.EqualTo("NEXT_PHRASE grid=1/16 bar=1 next=\"Drop\"/16 replaced=\"Chorus\"/64 instance=6"));
    }

    [Test]
    public void NextPhraseRendersReplacedNoneWhenAppearingAfterAbsence()
    {
        Assert.That(
            CueLogFormat.NextPhrase(GridAt1, newLabel: "Chorus", newLength: 64, replacedLabel: null, replacedLength: null, instance: 1),
            Is.EqualTo("NEXT_PHRASE grid=1/16 bar=1 next=\"Chorus\"/64 replaced=none instance=1"));
    }

    [Test]
    public void AnnouncementRendersUnknownLabelAsQuestionMark()
    {
        // House style: a missing label collapses to ? (never empty quotes), like Phrase(null) => phrase=?.
        Assert.That(CueLogFormat.Announcement(null, 64), Is.EqualTo("?/64"));
        Assert.That(CueLogFormat.Announcement(string.Empty, 64), Is.EqualTo("?/64"));
    }

    [Test]
    public void GridPositionRendersOffWhenOffTheGrid()
    {
        var line = CueLogFormat.CueCast(grid: null, "chorus", 632, 32, 64, "RingsOfFire", "Sweep", CueFlavor.None, accepted: true);
        Assert.That(line, Does.Contain("grid=off "));
        Assert.That(line, Does.Not.Contain("bar="));
    }

    [Test]
    public void PhraseRendersUnknownWhenTheWireCarriedNoLabel()
    {
        Assert.That(CueLogFormat.Phrase(null), Is.EqualTo("phrase=?"));
        Assert.That(CueLogFormat.Phrase(string.Empty), Is.EqualTo("phrase=?"));
        Assert.That(
            CueLogFormat.CueCast(GridAt1, phrase: null, 632, 32, 64, "RingsOfFire", "Sweep", CueFlavor.None, accepted: true),
            Does.Contain(" phrase=? cue=632[32/64]"));
    }
}

public sealed class CueLogSinkTests
{
    private static readonly GridInfo GridAt12 = new GridInfo(GridState.Locked, 12, 3, 0f);

    [Test]
    public void CueLockedJoinsTheRememberedLoadContext()
    {
        var lines = new List<string>();
        var log = new CueLog(() => new LineCapture(lines), () => GridAt12, ownsWriter: false);

        log.CueLoaded("chorus", 632, 32, 64, "RingsOfFire", "Sweep", 628, 627, 4, 2, CueLogUpsert.New, null);
        log.CueLocked(632, 627, CueLockVia.Render);

        Assert.That(
            lines.Last(),
            Does.EndWith("CUE_LOCKED grid=12/16 bar=3 phrase=\"chorus\" cue=632[32/64] effect=\"RingsOfFire\" transition=\"Sweep\" lockedAt=627 via=render"),
            "The Switcher carries no names; the sink joins the lock with the context remembered from CUE_LOADED.");
    }

    [Test]
    public void EachLineIsPrefixedWithAWallClockTimestamp()
    {
        var lines = new List<string>();
        var log = new CueLog(() => new LineCapture(lines), () => GridAt12, ownsWriter: false);

        log.CueLocked(632, 627, CueLockVia.Beat);

        // yyyy-MM-dd HH:mm:ss.fff then a space then the event body.
        Assert.That(lines.Single(), Does.Match(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} CUE_LOCKED "));
    }

    [Test]
    public void TheBackingWriterIsNotOpenedUntilTheFirstEvent()
    {
        var opened = false;
        var log = new CueLog(
            () =>
            {
                opened = true;
                return new LineCapture(new List<string>());
            },
            () => null,
            ownsWriter: false);

        Assert.That(opened, Is.False, "No event, no file: creation is lazy.");
        log.SheetBuilt(CueLogSlot.Current, CueLogBuildReason.Build, "chorus", 600, 32, new[] { 16, 32 });
        Assert.That(opened, Is.True, "The first event opens the writer.");
    }

    private sealed class LineCapture : TextWriter
    {
        private readonly List<string> lines;

        public LineCapture(List<string> lines)
        {
            this.lines = lines;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value) => lines.Add(value);

        public override void Write(char value)
        {
        }
    }
}

// Seam-2 style: the real Director/Switcher pipeline drives a real CueLog capturing to memory. Setup mirrors
// DirectorReducerTests so the beat-feeding grammar and Repertoire fixtures match the reducer's own tests.
public sealed class CueLogSeamTests
{
    private GameObject controllerObject;
    private Controller controller;
    private Switcher switcher;
    private Director director;
    private List<string> lines;

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("CueLogSeamTestsController");
        controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.logDirectorSwitching = false;
        controller.effectTime = 10f;
        controller.beatManager = new BeatManager();
        controller.beatManager.SetLiveBeatSource(true);
        controller.effects = new EffectBase[]
        {
            new TestEffect(),
            new TestEffect(),
            new TestEffect(Repertoire.HandlesDrop),
            new TestEffect(Repertoire.HandlesFill),
        };
        controller.transitions = new TransitionBase[]
        {
            new TailedTransition(),
            new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 4, tailBeats: 0),
            new EventTransition(RepertoireFlags.HandlesFill, runwayBeats: 4, tailBeats: 0),
        };
        foreach (var transition in controller.transitions)
        {
            transition.BindController(controller);
            transition.Init();
        }

        controller.effectDeck = new[] { 0, 1, 2, 3 };
        controller.transitionDeck = new[] { 0, 1, 2 };
        controller.currentTransition = 0;
        controller.timer = new Timer(controller.effectTime, false);

        lines = new List<string>();
        controller.cueLog = new CueLog(() => new LineCapture(lines), () => controller.beatManager.GridQuery, ownsWriter: false);

        switcher = new Switcher(controller, controller.effects, controller.transitions, controller.cueLog);
        switcher.SetInitialEffect(0, controller.currentTransition);
        controller.switcher = switcher;

        director = new Director(
            controller,
            switcher,
            controller.timer,
            controller.effectDeck,
            controller.transitionDeck,
            controller.currentTransition,
            controller.cueLog);
        controller.director = director;
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void BuildingASheetAndCastingACueEmitsTheAgreedLines()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "Setup: the new Grid casts a cue at beat 616.");

        Assert.That(
            lines.Any(l => l.Contains("SHEET_BUILT slot=current reason=build") && l.Contains("phrase=\"Phrase\" start=600 length=32")),
            Is.True,
            "The current sheet build is logged with the wire's Phrase label.");
        Assert.That(
            lines.Any(l => l.Contains("CUE_CAST grid=1/16 bar=1 phrase=\"Phrase\" cue=632[32/32]") && l.Contains("result=accepted")),
            Is.True,
            "The cast toward the Phrase-end mark 632 is logged, accepted.");
        Assert.That(
            lines.Any(l => l.Contains("CUE_LOADED grid=1/16 bar=1 phrase=\"Phrase\" cue=632[32/32]") && l.Contains("start=628 lock=627 runway=4 tail=4 upsert=new")),
            Is.True,
            "The accepted load is logged with the Switcher's own window arithmetic.");
    }

    [Test]
    public void ASameMarkReOfferEmitsCueKept()
    {
        // Cast a cue for the Phrase-end mark 632 when its Grid [616, 632) begins, then loop the grid back onto
        // the SAME Boundary so the SAME Cue Mark is re-offered. The Switcher answers "kept" and the Director
        // logs CUE_KEPT.
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "Setup: the cue is loaded for the Phrase-end mark.");

        FeedBeat(beat: 617, gridBeat: 2, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(
            lines.Any(l => l.Contains("CUE_KEPT") && l.Contains("offered=632") && l.Contains("loaded=632[32/32]")),
            Is.True,
            "A same-mark re-offer the Switcher keeps is logged as CUE_KEPT.");
    }

    [Test]
    public void LatchingTheLockEmitsCueLockedExactlyOnceJoinedWithTheLoadContext()
    {
        // Load the Phrase-end cue (Director emits CUE_LOADED, remembering the "Phrase" context), then re-offer
        // at the Lock Point so LatchLockAtBeat fires. The lock notification joins the remembered context.
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        var loaded = switcher.LoadedCueStatus;
        Assert.That(loaded.HasCue && !loaded.IsLocked, Is.True, "Setup: a cue is loaded and still unlocked.");

        var atLockPoint = new SwitcherClockSnapshot(currentBeat: loaded.LockPointBeat, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 0f);
        switcher.UpsertLoadedCue(
            new SwitcherCueDirection(loaded.CueMarkBeat, loaded.TargetEffectIndex, loaded.TransitionIndex, controller.transitions[loaded.TransitionIndex].Repertoire),
            atLockPoint);
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.True, "Setup: the re-offer at the Lock Point latched the lock.");

        var lockLines = lines.Where(l => l.Contains("CUE_LOCKED")).ToList();
        Assert.That(lockLines.Count, Is.EqualTo(1), "The lock fires exactly once per loaded cue.");
        Assert.That(lockLines.Single(), Does.Contain($"phrase=\"Phrase\" cue=632[32/32]"), "CUE_LOCKED joins the remembered load context.");
        Assert.That(lockLines.Single(), Does.Contain($"lockedAt={loaded.LockPointBeat} via=beat"));
    }

    [Test]
    public void TheTwoChorusPhraseShapeLogsBothLanesAsFirstClassEvents()
    {
        // The 12:44 session shape the frozen five missed: two consecutive Chorus/64 phrases, then a Drop. The
        // second Chorus is announced as next (a NEXT_PHRASE for instance 1), the two Choruses wrap into each
        // other (a PHRASE_TURNOVER into instance 1 whose sides are identical), and the Drop is then announced
        // (a NEXT_PHRASE for instance 2). Instance ordinals distinguish the two real Choruses.
        FeedBeat(beat: 660, phraseStartBeat: 600, phraseLengthBeats: 64, phraseLabel: "Chorus",
            nextPhraseStartBeat: 664, nextPhraseLengthBeats: 64, nextPhraseLabel: "Chorus");
        FeedBeat(beat: 664, phraseStartBeat: 664, phraseLengthBeats: 64, phraseLabel: "Chorus",
            nextPhraseStartBeat: 728, nextPhraseLengthBeats: 16, nextPhraseLabel: "Drop");

        Assert.That(
            lines.Any(l => l.Contains("NEXT_PHRASE") && l.Contains("next=\"Chorus\"/64 replaced=none instance=1")),
            Is.True,
            "The second Chorus announced as next is a NEXT_PHRASE for instance 1.");
        Assert.That(
            lines.Any(l => l.Contains("PHRASE_TURNOVER") && l.Contains("out=\"Chorus\"/64 in=\"Chorus\"/64 instance=1")),
            Is.True,
            "The wrap between the two Choruses is a PHRASE_TURNOVER into instance 1, plainly visible with identical sides.");
        Assert.That(
            lines.Any(l => l.Contains("NEXT_PHRASE") && l.Contains("next=\"Drop\"/16 replaced=\"Chorus\"/64 instance=2")),
            Is.True,
            "The Drop announced as next is a NEXT_PHRASE replacing the Chorus, for instance 2.");

        // The second Chorus gets its own next sheet (built for instance 1) that promotes at the wrap, so the
        // only slot=current build is the startup build of the first Chorus — no heal rebuild after the wrap.
        Assert.That(
            lines.Count(l => l.Contains("SHEET_BUILT slot=current reason=build")),
            Is.EqualTo(1),
            "Instance ordinals promote a real next sheet at the wrap, so there is no slot=current reason=build heal line.");
        Assert.That(
            lines.Any(l => l.Contains("SHEET_BUILT slot=next reason=build") && l.Contains("phrase=\"Chorus\" ") && l.Contains("length=64")),
            Is.True,
            "The second Chorus is built as a real next sheet the duplicate-current guard used to suppress.");
    }

    private void FeedBeat(
        int beat,
        int phraseStartBeat,
        int phraseLengthBeats,
        int gridBeat = -1,
        int? nextPhraseStartBeat = null,
        int? nextPhraseLengthBeats = null,
        string phraseLabel = "Phrase",
        string nextPhraseLabel = "Next")
    {
        var snapshot = controller.beatManager.WireSnapshot;
        snapshot.bpm = 120f;
        snapshot.beat = new BeatPosition { current = beat, total = -1 };
        snapshot.beatInBar = ((beat - 1) % 4) + 1;
        snapshot.timingGrid = gridBeat >= 1
            ? new TimingGrid { beat = gridBeat, bar = ((gridBeat - 1) / 4) + 1, state = "locked" }
            : TimingGrid.Unavailable;
        snapshot.phraseState = new PhraseState
        {
            label = phraseLabel,
            countBeats = phraseStartBeat + phraseLengthBeats - beat,
            lengthBeats = phraseLengthBeats,
            irregular = 0,
        };
        snapshot.nextPhraseState = nextPhraseStartBeat is { } nextStart && nextPhraseLengthBeats is { } nextLength
            ? new LabeledCountdown { label = nextPhraseLabel, countBeats = nextStart - beat, lengthBeats = nextLength }
            : LabeledCountdown.Unavailable;
        snapshot.dropState = CountdownState.Unavailable;
        snapshot.fillState = CountdownState.Unavailable;
        snapshot.energyState = LabeledCountdown.Unavailable;
        director.Tick(0f);
    }

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    private sealed class LineCapture : TextWriter
    {
        private readonly List<string> lines;

        public LineCapture(List<string> lines)
        {
            this.lines = lines;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value) => lines.Add(value);

        public override void Write(char value)
        {
        }
    }

    private sealed class TestEffect : EffectBase
    {
        private readonly Repertoire repertoire;

        public TestEffect(Repertoire repertoire = Repertoire.None)
        {
            this.repertoire = repertoire;
            buffer = new Color[Penrose.Total];
        }

        public override Repertoire Repertoire => repertoire;

        public override string DebugText() => string.Empty;

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
        }
    }

    private sealed class TailedTransition : TransitionBase
    {
        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                RepertoireFlags.None,
                runwayBeats: 4,
                tailBeats: 4,
                TransitionShape.Dissolve,
                TransitionIntensity.High,
                defaultDurationSeconds: 4f));
        }

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
        }
    }

    private sealed class EventTransition : TransitionBase
    {
        private readonly RepertoireFlags tags;
        private readonly int runwayBeats;
        private readonly int tailBeats;

        public EventTransition(RepertoireFlags tags, int runwayBeats, int tailBeats)
        {
            this.tags = tags;
            this.runwayBeats = runwayBeats;
            this.tailBeats = tailBeats;
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                tags,
                runwayBeats,
                tailBeats,
                TransitionShape.Dissolve,
                TransitionIntensity.High,
                defaultDurationSeconds: 4f));
        }

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
        }
    }
}
