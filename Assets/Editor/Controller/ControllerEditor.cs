using UnityEditor;
using UnityEngine;

/// <summary>
/// Default Controller inspector that repaints every editor frame during Play Mode.
/// </summary>
/// <remarks>
/// Controller.beatManager and Director state change every frame at runtime, while a default inspector only repaints
/// on the editor's idle tick. Requesting constant repaints while playing lets the BeatManager dashboard and the
/// read-only Director, Cue Sheet, Loaded Cue, and Switcher execution panel stay live without moving Director onto
/// a separate scene object.
/// </remarks>
[CustomEditor(typeof(Controller))]
public sealed class ControllerEditor : Editor
{
    private static readonly Color CurrentBeatColor = new Color(1f, 0.9f, 0.35f);
    private static readonly Color CueMarkColor = new Color(0.25f, 0.95f, 1f);
    private static readonly Color LockPointColor = new Color(1f, 0.55f, 0.2f);
    private static readonly Color RunwayColor = new Color(1f, 0.35f, 0.85f);
    private static readonly Color TailColor = new Color(0.25f, 0.8f, 0.45f);

    private static bool showDirectorObservatory = false;
    private static bool showAdvancedTiming = false;

    /// <summary>Draws live HUD/debug state first, then the optional Director Observatory before serialized fields.</summary>
    public override void OnInspectorGUI()
    {
        var controller = (Controller)target;
        DrawRuntimeDebug(controller);
        EditorGUILayout.Space(6f);
        DrawDirectorObservatory(controller);
        EditorGUILayout.Space(8f);
        DrawDefaultInspector();
    }

    /// <summary>Repaints continuously only while playing, where BeatData and Director status change every frame.</summary>
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    private readonly struct CueTimingOverlay
    {
        public static CueTimingOverlay None { get; } = new CueTimingOverlay(false, false, false, -1, -1, -1, -1, 0, 0);

        public readonly bool HasCue;
        public readonly bool IsLoaded;
        public readonly bool IsLocked;
        public readonly int CueMarkBeat;
        public readonly int LockPointBeat;
        public readonly int StartBeat;
        public readonly int CompleteBeat;
        public readonly int RunwayBeats;
        public readonly int TailBeats;

        private CueTimingOverlay(
            bool hasCue,
            bool isLoaded,
            bool isLocked,
            int cueMarkBeat,
            int lockPointBeat,
            int startBeat,
            int completeBeat,
            int runwayBeats,
            int tailBeats)
        {
            HasCue = hasCue;
            IsLoaded = isLoaded;
            IsLocked = isLocked;
            CueMarkBeat = cueMarkBeat;
            LockPointBeat = lockPointBeat;
            StartBeat = startBeat;
            CompleteBeat = completeBeat;
            RunwayBeats = runwayBeats;
            TailBeats = tailBeats;
        }

        public static CueTimingOverlay Loaded(SwitcherCueStatus status)
        {
            return new CueTimingOverlay(
                true,
                true,
                status.IsLocked,
                status.CueMarkBeat,
                status.LockPointBeat,
                status.StartBeat,
                status.CompleteBeat,
                status.RunwayBeats,
                status.TailBeats);
        }

        public static CueTimingOverlay Candidate(int cueMarkBeat, TransitionRepertoire repertoire)
        {
            var beatPlan = TransitionBeatPlan.FromCueMark(cueMarkBeat, repertoire);
            return new CueTimingOverlay(
                true,
                false,
                false,
                cueMarkBeat,
                beatPlan.StartBeat - 1,
                beatPlan.StartBeat,
                beatPlan.CompleteBeat,
                repertoire.RunwayBeats,
                repertoire.TailBeats);
        }
    }

    private enum GridSlotRole
    {
        None,
        Current,
        CueMark,
        LockPoint,
        Runway,
        Tail,
    }

    private static void DrawRuntimeDebug(Controller controller)
    {
        EditorGUILayout.LabelField("Runtime Debug", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawHudLines(controller);
        EditorGUILayout.EndVertical();
    }

    private static void DrawDirectorObservatory(Controller controller)
    {
        showDirectorObservatory = EditorGUILayout.Foldout(showDirectorObservatory, "Director Observatory", true);
        if (!showDirectorObservatory)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see live Director, Cue Sheet, Loaded Cue, and Switcher state.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        var directorStatus = controller.DirectorStatus;
        var switcherStatus = controller.SwitcherStatus;
        var cueStatus = controller.SwitcherLoadedCueStatus;
        var cueOverlay = ResolveCueTimingOverlay(controller, directorStatus, cueStatus);

        DrawTimingAndCueSheet(controller, directorStatus, cueOverlay);
        EditorGUILayout.Space(6f);
        DrawDirectorIntent(directorStatus);
        EditorGUILayout.Space(6f);
        DrawLastCueDecision(directorStatus);
        EditorGUILayout.Space(6f);
        DrawLoadedCue(controller, directorStatus, cueStatus);
        EditorGUILayout.Space(6f);
        DrawSwitcherExecution(switcherStatus);
        EditorGUILayout.Space(6f);
        DrawAdvancedTiming(controller, directorStatus);

        EditorGUILayout.EndVertical();
    }

    private static CueTimingOverlay ResolveCueTimingOverlay(Controller controller, DirectorStatus directorStatus, SwitcherCueStatus cueStatus)
    {
        if (cueStatus.HasCue)
        {
            return CueTimingOverlay.Loaded(cueStatus);
        }

        if (directorStatus.HasCueMark
            && TryGetTransitionRepertoire(controller, directorStatus.NextTransitionIndex, out var repertoire))
        {
            return CueTimingOverlay.Candidate(directorStatus.CueMarkBeat, repertoire);
        }

        return CueTimingOverlay.None;
    }

    private static void DrawTimingAndCueSheet(Controller controller, DirectorStatus status, CueTimingOverlay cueOverlay)
    {
        var gridCount = controller.beatManager?.Grid is { } grid ? grid.Count : -1;
        EditorGUILayout.LabelField("TIMING / CUE SHEET", EditorStyles.boldLabel);
        DrawRow("Now", $"Beat {FormatBeat(status.CurrentBeat)} · Grid Count {FormatGridCount(gridCount)}");
        DrawRow("Timing Source", FormatTimingSource(status));
        DrawRow("Beats Until Cue", FormatBeats(status.BeatsUntilLanding));
        DrawCueTimingRows(status, cueOverlay);
        DrawGridStrip(gridCount, cueOverlay);
        DrawCueSheet(status.CueSheet, status.CurrentBeat);
    }

    private static void DrawCueTimingRows(DirectorStatus status, CueTimingOverlay cueOverlay)
    {
        if (!cueOverlay.HasCue)
        {
            DrawRow("Cue Timing", "—");
            return;
        }

        var source = cueOverlay.IsLoaded ? "Loaded Cue" : "Candidate Cue";
        DrawRow(source, $"Cue Mark {FormatBeat(cueOverlay.CueMarkBeat)} ({FormatBeatDelta(status.CurrentBeat, cueOverlay.CueMarkBeat)})");
        DrawRow("Lock / Fire", $"Lock {FormatBeat(cueOverlay.LockPointBeat)} ({FormatBeatDelta(status.CurrentBeat, cueOverlay.LockPointBeat)}) · Fire {FormatBeat(cueOverlay.StartBeat)} ({FormatBeatDelta(status.CurrentBeat, cueOverlay.StartBeat)})");
        DrawRow("Runway / Tail", $"{cueOverlay.RunwayBeats}b / {cueOverlay.TailBeats}b");
    }

    private static void DrawCueSheet(CueSheetStatus cueSheet, int currentBeat)
    {
        if (!cueSheet.HasSheet)
        {
            DrawRow("Cue Sheet", "—");
            return;
        }

        DrawRow("Phrase", $"{FormatBeat(cueSheet.PhraseStartBeat)} → {FormatBeat(cueSheet.PhraseEndBeat)} · {cueSheet.PhraseLengthBeats}b");
        DrawRow("Current Cue Mark", $"{FormatBeat(cueSheet.CurrentCueMarkBeat)} ({FormatBeatDelta(currentBeat, cueSheet.CurrentCueMarkBeat)})");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Cue Marks");
        var previousColor = GUI.backgroundColor;
        var offsets = cueSheet.CueMarkOffsets ?? System.Array.Empty<int>();
        for (var i = 0; i < offsets.Length; i++)
        {
            var offset = offsets[i];
            var absoluteBeat = cueSheet.PhraseStartBeat + offset;
            GUI.backgroundColor = i == cueSheet.CurrentCueMarkIndex
                ? CueMarkColor
                : absoluteBeat == currentBeat
                    ? CurrentBeatColor
                    : previousColor;
            GUILayout.Label(FormatCueSheetOffset(offset, cueSheet.PhraseLengthBeats), EditorStyles.miniButton, GUILayout.MinWidth(44f));
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawDirectorIntent(DirectorStatus status)
    {
        EditorGUILayout.LabelField("DIRECTOR INTENT", EditorStyles.boldLabel);
        DrawRow("Mode", status.Mode.ToString());
        DrawRow("Decision", status.Decision.ToString());
        DrawRow("Staged Effect", FormatIndexedName(status.NextEffectIndex, status.NextEffectName));
        DrawRow("Staged Transition", FormatIndexedName(status.NextTransitionIndex, status.NextTransitionName));
        DrawRow("Hold Selected Effect", status.HoldSelectedEffect ? "On" : "Off");
        DrawRow("Hold Selected Transition", status.HoldSelectedTransition ? "On" : "Off");
        DrawRow("Last Change", FormatBeat(status.LastChangeBeat));
        DrawRow("Last Sent Cue Mark", FormatBeat(status.TransitionLandingBeat));
        DrawRow("Cadence Ready In", FormatBeats(status.BeatsUntilCadenceReady));
    }

    private static void DrawLastCueDecision(DirectorStatus status)
    {
        EditorGUILayout.LabelField("LAST CUE DECISION", EditorStyles.boldLabel);
        var decision = status.LastCue;
        if (decision.Outcome == CueDecisionOutcome.None)
        {
            DrawRow("Outcome", "—");
            return;
        }

        DrawRow("Outcome", $"{FormatCueOutcome(decision.Outcome)} @ beat {decision.Beat}");
        DrawRow("Cue Event", FormatCueEvent(decision));
        DrawRow("Impact", FormatCueImpact(decision));
        if (decision.EffectIndex >= 0)
        {
            DrawRow(
                decision.Outcome == CueDecisionOutcome.DropProtected ? "Protected Effect" : "Cast Effect",
                FormatIndexedName(decision.EffectIndex, decision.EffectName) + FormatCastSource(decision.EffectSource, decision.PreferredRepertoire));
        }

        if (decision.TransitionIndex >= 0)
        {
            DrawRow(
                "Cast Transition",
                FormatIndexedName(decision.TransitionIndex, decision.TransitionName) + FormatCastSource(decision.TransitionSource, decision.PreferredRepertoire));
        }
    }

    private static string FormatCueOutcome(CueDecisionOutcome outcome)
    {
        switch (outcome)
        {
            case CueDecisionOutcome.Sent: return "Sent";
            case CueDecisionOutcome.Held: return "Held (nothing sent)";
            case CueDecisionOutcome.BlockedByCadence: return "Blocked by cadence";
            case CueDecisionOutcome.DropProtected: return "Drop protected (no cue)";
            default: return outcome.ToString();
        }
    }

    private static string FormatCueEvent(CueDecision decision)
    {
        return decision.PreferredRepertoire == Repertoire.None
            ? decision.EventIntent.ToString()
            : $"{decision.EventIntent} (wants {decision.PreferredRepertoire})";
    }

    private static string FormatCueImpact(CueDecision decision)
    {
        if (decision.Outcome != CueDecisionOutcome.Sent && decision.Outcome != CueDecisionOutcome.Held)
        {
            return $"beat {decision.ImpactBeat}";
        }

        var beatsBeforeImpact = decision.BeatsBeforeImpact;
        if (beatsBeforeImpact > 0)
        {
            return $"beat {decision.ImpactBeat} — sent {beatsBeforeImpact}b before impact";
        }

        return beatsBeforeImpact == 0
            ? $"beat {decision.ImpactBeat} — sent on impact (hard cut)"
            : $"beat {decision.ImpactBeat} — sent {-beatsBeforeImpact}b after impact (hard cut)";
    }

    private static string FormatCastSource(CueCastSource source, Repertoire preferredRepertoire)
    {
        switch (source)
        {
            case CueCastSource.Staged: return " (staged)";
            case CueCastSource.DeckFind: return " (deck find)";
            case CueCastSource.NoPreferredAvailable: return $" (no {preferredRepertoire} candidate)";
            default: return string.Empty;
        }
    }

    private static void DrawLoadedCue(Controller controller, DirectorStatus directorStatus, SwitcherCueStatus cueStatus)
    {
        EditorGUILayout.LabelField("SWITCHER LOADED CUE", EditorStyles.boldLabel);
        if (!cueStatus.HasCue)
        {
            DrawRow("Loaded Cue", "none");
            return;
        }

        DrawRow("Loaded Cue", cueStatus.IsLocked ? "locked" : "mutable");
        DrawRow("Can Update", cueStatus.CanUpdate ? "yes" : "no");
        DrawRow("Cue Mark", $"{FormatBeat(cueStatus.CueMarkBeat)} ({FormatBeatDelta(directorStatus.CurrentBeat, cueStatus.CueMarkBeat)})");
        DrawRow("Lock Point", $"{FormatBeat(cueStatus.LockPointBeat)} ({FormatBeatDelta(directorStatus.CurrentBeat, cueStatus.LockPointBeat)})");
        DrawRow("Fire / Complete", $"{FormatBeat(cueStatus.StartBeat)} → {FormatBeat(cueStatus.CompleteBeat)}");
        DrawRow("Runway / Tail", $"{cueStatus.RunwayBeats}b / {cueStatus.TailBeats}b");
        DrawRow("Target Effect", FormatIndexedName(cueStatus.TargetEffectIndex, EffectName(controller, cueStatus.TargetEffectIndex)));
        DrawRow("Transition", FormatIndexedName(cueStatus.TransitionIndex, TransitionName(controller, cueStatus.TransitionIndex)));
    }

    private static void DrawSwitcherExecution(SwitcherStatus switcherStatus)
    {
        EditorGUILayout.LabelField("SWITCHER EXECUTION", EditorStyles.boldLabel);
        DrawRow("Stage", switcherStatus.StageName);
        if (switcherStatus.CurrentEffectIndex >= 0)
        {
            DrawRow("Current Effect", FormatIndexedName(switcherStatus.CurrentEffectIndex, switcherStatus.CurrentEffectName));
        }
        else
        {
            DrawRow("Source → Target", $"{FormatIndexedName(switcherStatus.SourceEffectIndex, switcherStatus.SourceEffectName)} → {FormatIndexedName(switcherStatus.TargetEffectIndex, switcherStatus.TargetEffectName)}");
            DrawRow("Active Transition", FormatIndexedName(switcherStatus.CurrentTransitionIndex, switcherStatus.CurrentTransitionName));
        }

        DrawProgress("Transition Progress", switcherStatus.TransitionProgress);
    }

    private static void DrawAdvancedTiming(Controller controller, DirectorStatus status)
    {
        showAdvancedTiming = EditorGUILayout.Foldout(showAdvancedTiming, "Advanced On-Air Timing", true);
        if (!showAdvancedTiming)
        {
            return;
        }

        var beatManager = controller.beatManager;
        var grid = beatManager?.Grid;
        DrawRow("Cue Mark", status.HasCueMark ? "locked" : "none");
        DrawRow("Grid Confidence", grid is { } g ? g.Confidence.ToString() : "unlocked");
        DrawRow("Grid Count", grid is { } gc ? $"{gc.Count}/{PhraseWindow.DefaultGridBeats}" : "—");
        DrawRow("Grid Bar", grid is { } gb ? $"{gb.Bar}/4" : "—");
        DrawRow("Phrase", FormatPhraseRow(beatManager?.Phrase));
        DrawRow("Next Phrase", FormatNextPhraseRow(beatManager?.NextPhrase));
        DrawRow("Irregular Phrase", beatManager?.Phrase?.irregular == true ? "yes" : "no");
        DrawRow("Energy", FormatEnergyRow(beatManager?.Energy));
        DrawRow("Loop", FormatLoopRow(beatManager?.Loop));
    }

    /// <summary>Current phrase as <c>label · elapsed/length</c>, from the wire-fed <see cref="BeatManager.Phrase"/>.</summary>
    private static string FormatPhraseRow(PhraseInfo? info)
    {
        if (!(info is { } phrase))
        {
            return "—";
        }

        var span = phrase.lengthBeats is { } length && phrase.beatsUntilNext is { } untilNext
            ? $" · {Mathf.Max(0, length - untilNext)}/{length}"
            : string.Empty;
        return $"{phrase.label}{span}";
    }

    /// <summary>Upcoming phrase as <c>label · in Nb · Nb long</c>, from <see cref="BeatManager.NextPhrase"/>.</summary>
    private static string FormatNextPhraseRow(NextPhraseInfo? info)
    {
        if (!(info is { } next))
        {
            return "—";
        }

        var untilChange = next.beatsUntilChange is { } beats ? $" · in {beats}b" : string.Empty;
        var length = next.lengthBeats is { } len ? $" · {len}b long" : string.Empty;
        return $"{next.label}{untilChange}{length}";
    }

    /// <summary>Energy as <c>level (→ next in Nb)</c>, from <see cref="BeatManager.Energy"/>.</summary>
    private static string FormatEnergyRow(EnergyInfo? info)
    {
        if (!(info is { } energy))
        {
            return "—";
        }

        if (energy.next is { } next)
        {
            var untilChange = energy.beatsUntilChange is { } beats ? $" in {beats}b" : string.Empty;
            return $"{energy.level} (→ {next}{untilChange})";
        }

        return energy.level.ToString();
    }

    /// <summary>Loop as <c>rolling/set/off · Nb</c>, from the display-only <see cref="BeatManager.Loop"/>.</summary>
    private static string FormatLoopRow(LoopInfo? info)
    {
        if (!(info is { } loop))
        {
            return "—";
        }

        var state = loop.looping ? "rolling" : loop.regionSet ? "set" : "off";
        var length = loop.lengthBeats is { } beats ? $" · {beats:0.##}b" : string.Empty;
        return $"{state}{length}";
    }

    private static void DrawHudLines(Controller controller)
    {
        EditorGUILayout.LabelField("SCREEN HUD", EditorStyles.boldLabel);
        DrawRow("Top Line", controller.LastRuntimeHudLine);
        DrawRow("Detail Line", controller.LastRuntimeDetailLine);
        EditorGUILayout.LabelField("Render Debug", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextArea(string.IsNullOrWhiteSpace(controller.LastRenderDebugText)
                ? "—"
                : controller.LastRenderDebugText, GUILayout.MinHeight(72f));
        }
    }

    private static void DrawGridStrip(int gridPosition, CueTimingOverlay cueOverlay)
    {
        EditorGUILayout.BeginHorizontal();
        var previousColor = GUI.backgroundColor;
        for (var i = 1; i <= PhraseWindow.DefaultGridBeats; i++)
        {
            var role = RoleForGridSlot(i, gridPosition, cueOverlay);
            GUI.backgroundColor = ColorForRole(role, i == gridPosition, previousColor);
            GUILayout.Label(FormatGridSlotLabel(i, gridPosition, role), EditorStyles.miniButton, GUILayout.MinWidth(28f));
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    private static GridSlotRole RoleForGridSlot(int slot, int gridPosition, CueTimingOverlay cueOverlay)
    {
        if (!cueOverlay.HasCue)
        {
            return slot == gridPosition ? GridSlotRole.Current : slot == 1 ? GridSlotRole.CueMark : GridSlotRole.None;
        }

        if (GridSlotForBeat(cueOverlay.CueMarkBeat, cueOverlay.CueMarkBeat) == slot)
        {
            return GridSlotRole.CueMark;
        }

        if (GridSlotForBeat(cueOverlay.LockPointBeat, cueOverlay.CueMarkBeat) == slot)
        {
            return GridSlotRole.LockPoint;
        }

        if (SlotContainsBeatRange(slot, cueOverlay.StartBeat, cueOverlay.CueMarkBeat - 1, cueOverlay.CueMarkBeat))
        {
            return GridSlotRole.Runway;
        }

        if (SlotContainsBeatRange(slot, cueOverlay.CueMarkBeat + 1, cueOverlay.CompleteBeat, cueOverlay.CueMarkBeat))
        {
            return GridSlotRole.Tail;
        }

        return slot == gridPosition ? GridSlotRole.Current : GridSlotRole.None;
    }

    private static bool SlotContainsBeatRange(int slot, int startBeat, int endBeat, int cueMarkBeat)
    {
        if (startBeat > endBeat)
        {
            return false;
        }

        for (var beat = startBeat; beat <= endBeat; beat++)
        {
            if (GridSlotForBeat(beat, cueMarkBeat) == slot)
            {
                return true;
            }
        }

        return false;
    }

    private static int GridSlotForBeat(int beat, int cueMarkBeat)
    {
        var offset = (beat - cueMarkBeat) % PhraseWindow.DefaultGridBeats;
        if (offset < 0)
        {
            offset += PhraseWindow.DefaultGridBeats;
        }

        return offset + 1;
    }

    private static Color ColorForRole(GridSlotRole role, bool isCurrent, Color fallback)
    {
        // The current beat always wins: it must stay trackable on top of any cue role color
        // (runway / tail / lock point / cue mark), which would otherwise paint over it.
        if (isCurrent)
        {
            return CurrentBeatColor;
        }

        switch (role)
        {
            case GridSlotRole.CueMark:
                return CueMarkColor;
            case GridSlotRole.LockPoint:
                return LockPointColor;
            case GridSlotRole.Runway:
                return RunwayColor;
            case GridSlotRole.Tail:
                return TailColor;
            default:
                return fallback;
        }
    }

    private static string FormatGridSlotLabel(int slot, int gridPosition, GridSlotRole role)
    {
        return role == GridSlotRole.LockPoint ? "L" : slot == 1 ? "X" : slot.ToString();
    }

    private static void DrawProgress(string label, float value)
    {
        var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        var labelRect = rect;
        labelRect.width = EditorGUIUtility.labelWidth;
        EditorGUI.LabelField(labelRect, label);

        var barRect = rect;
        barRect.xMin += EditorGUIUtility.labelWidth;
        EditorGUI.ProgressBar(barRect, Mathf.Clamp01(value), $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%");
    }

    private static void DrawRow(string label, string value)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(label, string.IsNullOrWhiteSpace(value) ? "—" : value);
        }
    }

    private static bool TryGetTransitionRepertoire(Controller controller, int index, out TransitionRepertoire repertoire)
    {
        if (controller.transitions != null && index >= 0 && index < controller.transitions.Length && controller.transitions[index] != null)
        {
            repertoire = controller.transitions[index].Repertoire;
            return true;
        }

        repertoire = default;
        return false;
    }

    private static string FormatIndexedName(int index, string name)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? "—" : name;
        return index >= 0 ? $"{index}: {displayName}" : "—";
    }

    private static string EffectName(Controller controller, int index)
    {
        return controller.effects != null && index >= 0 && index < controller.effects.Length && controller.effects[index] != null
            ? controller.effects[index].Name
            : string.Empty;
    }

    private static string TransitionName(Controller controller, int index)
    {
        return controller.transitions != null && index >= 0 && index < controller.transitions.Length && controller.transitions[index] != null
            ? controller.transitions[index].Name
            : string.Empty;
    }

    private static string FormatTimingSource(DirectorStatus status)
    {
        return status.TimingSource.ToString();
    }

    private static string FormatGridCount(int gridPosition)
    {
        return gridPosition > 0 ? $"{gridPosition} / {PhraseWindow.DefaultGridBeats}" : "—";
    }

    private static string FormatBeat(int beat)
    {
        return beat >= 0 && beat != int.MinValue ? beat.ToString() : "—";
    }

    private static string FormatBeats(int beats)
    {
        return beats >= 0 ? $"{beats}b" : "—";
    }

    private static string FormatBeatDelta(int currentBeat, int targetBeat)
    {
        if (currentBeat < 0 || targetBeat < 0 || currentBeat == int.MinValue || targetBeat == int.MinValue)
        {
            return "—";
        }

        var delta = targetBeat - currentBeat;
        return delta >= 0 ? $"+{delta}b" : $"{delta}b";
    }

    private static string FormatCueSheetOffset(int offset, int phraseLengthBeats)
    {
        if (offset == 0)
        {
            return "X";
        }

        return offset == phraseLengthBeats ? "End" : $"+{offset}";
    }
}
