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
/// <remarks>
/// The Observatory renders from the Director reducer's real state only (ADR-0011): the two Cue Sheets with their
/// coming Cue Marks, the cast for the coming mark (the Switcher's loaded cue), the Switcher's execution stage, and
/// the wire-fed timing lanes. It is a downstream debug view — it owns its own preview math and reads no decision
/// memory, because the reducer records no verdicts to read.
/// </remarks>
[CustomEditor(typeof(Controller))]
public sealed class ControllerEditor : Editor
{
    private static readonly Color CueMarkColor = new Color(0.25f, 0.95f, 1f);
    private static readonly Color LiveBeatColor = new Color(1f, 0.9f, 0.35f);
    private static readonly Color PassedMarkColor = new Color(0.42f, 0.42f, 0.42f);
    private static readonly Color UpcomingMarkColor = new Color(0.18f, 0.5f, 0.55f);

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

    private static void DrawRuntimeDebug(Controller controller)
    {
        EditorGUILayout.LabelField("Runtime Debug", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawHudLines(controller);
        EditorGUILayout.EndVertical();
    }

    /// <summary>Draws the live Director, Cue Sheet, and Switcher state as a downstream observatory.</summary>
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
        var elapsed = ElapsedPhraseBeats(controller.beatManager?.Phrase.Span.Current);

        DrawDirectorIntent(controller, directorStatus);
        EditorGUILayout.Space(6f);
        DrawGridStrip(controller, directorStatus, elapsed);
        EditorGUILayout.Space(6f);
        DrawCueSheets(directorStatus, elapsed);
        EditorGUILayout.Space(6f);
        DrawBeatsUntilCue(controller, directorStatus, elapsed);
        EditorGUILayout.Space(6f);
        DrawLoadedCue(controller, directorStatus, cueStatus);
        EditorGUILayout.Space(6f);
        DrawSwitcherExecution(switcherStatus);
        EditorGUILayout.Space(6f);
        DrawAdvancedTiming(controller, directorStatus);

        EditorGUILayout.EndVertical();
    }

    /// <summary>Draws the Director's current mode, hub position, staged choices, and hold flags.</summary>
    private static void DrawDirectorIntent(Controller controller, DirectorStatus status)
    {
        var gridCount = controller.beatManager?.Grid.Current?.Beat ?? -1;
        EditorGUILayout.LabelField("DIRECTOR", EditorStyles.boldLabel);
        DrawRow("Mode", status.Mode.ToString());
        DrawRow("Now", $"Beat {FormatBeat(status.CurrentBeat)} · Grid Count {FormatGridCount(gridCount)}");
        DrawRow("Staged Effect", FormatIndexedName(status.NextEffectIndex, status.NextEffectName));
        DrawRow("Staged Transition", FormatIndexedName(status.NextTransitionIndex, status.NextTransitionName));
        DrawRow("Hold Selected Effect", status.HoldSelectedEffect ? "On" : "Off");
        DrawRow("Hold Selected Transition", status.HoldSelectedTransition ? "On" : "Off");
    }

    /// <summary>
    /// A 16-slot strip of the live Grid, one slot per beat, with the live beat lit and the current
    /// Phrase's Cue Marks overlaid where they fall inside the visible window.
    /// </summary>
    /// <remarks>
    /// A Cue Mark at Phrase offset O sits <c>O - elapsed</c> beats from the live beat, so it lands on
    /// grid slot <c>liveBeat + (O - elapsed)</c>. The overlay anchors to the live grid slot only — no
    /// absolute Phrase-start beat (fddb1882) — and never wraps modulo 16, so an irregular Phrase that
    /// runs past a 16-beat multiple never paints a mark where it does not belong. Marks outside 1..16
    /// are simply not shown; nothing is synthesized for unloaded cues.
    /// </remarks>
    private static void DrawGridStrip(Controller controller, DirectorStatus status, int? elapsed)
    {
        EditorGUILayout.LabelField("GRID", EditorStyles.boldLabel);

        var liveBeat = controller.beatManager?.Grid.Current?.Beat ?? -1;
        var cueSlots = CueMarkGridSlots(status.CurrentSheet, liveBeat, elapsed);

        EditorGUILayout.BeginHorizontal();
        var previousColor = GUI.backgroundColor;
        for (var slot = 1; slot <= CueSheet.GridBeats; slot++)
        {
            GUI.backgroundColor = slot == liveBeat
                ? LiveBeatColor
                : cueSlots.Contains(slot) ? CueMarkColor : previousColor;
            GUILayout.Label(slot.ToString(), EditorStyles.miniButton, GUILayout.MinWidth(28f));
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawCueSheets(DirectorStatus status, int? elapsed)
    {
        EditorGUILayout.LabelField("CUE SHEETS", EditorStyles.boldLabel);
        DrawCueSheet("Current", status.CurrentSheet, elapsed);
        DrawCueSheet("Next", status.NextSheet, null);
    }

    /// <summary>
    /// Draws a sheet's identity line and its Cue Mark row. When <paramref name="elapsed"/> is known
    /// (the Current sheet), each mark is colored passed / next / upcoming against the elapsed Phrase
    /// beat; the Next sheet passes null and every mark stays neutral, since it has no live position yet.
    /// </summary>
    private static void DrawCueSheet(string label, CueSheetView cueSheet, int? elapsed)
    {
        if (!cueSheet.HasSheet)
        {
            DrawRow(label, "—");
            return;
        }

        var identity = string.IsNullOrEmpty(cueSheet.PhraseLabel)
            ? $"{cueSheet.PhraseLengthBeats}b"
            : $"\"{cueSheet.PhraseLabel}\" · {cueSheet.PhraseLengthBeats}b";
        DrawRow(label, identity);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Cue Marks");
        var previousColor = GUI.backgroundColor;
        var offsets = cueSheet.CueMarkOffsets ?? System.Array.Empty<int>();
        var nextIndex = NextCueMarkIndex(offsets, elapsed);
        for (var i = 0; i < offsets.Length; i++)
        {
            GUI.backgroundColor = CueMarkStateColor(i, nextIndex, elapsed.HasValue);
            GUILayout.Label(FormatCueSheetOffset(offsets[i], cueSheet.PhraseLengthBeats), EditorStyles.miniButton, GUILayout.MinWidth(44f));
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Countdown to the next Cue Mark (its offset minus the elapsed Phrase beat) and the beats left in
    /// the Phrase. Says so plainly when no upcoming mark or Phrase position is known rather than showing
    /// a stale number.
    /// </summary>
    private static void DrawBeatsUntilCue(Controller controller, DirectorStatus status, int? elapsed)
    {
        EditorGUILayout.LabelField("BEATS UNTIL CUE", EditorStyles.boldLabel);

        var sheet = status.CurrentSheet;
        var offsets = sheet.HasSheet ? sheet.CueMarkOffsets ?? System.Array.Empty<int>() : System.Array.Empty<int>();
        var nextIndex = NextCueMarkIndex(offsets, elapsed);
        if (nextIndex >= 0 && elapsed is { } now)
        {
            var mark = FormatCueSheetOffset(offsets[nextIndex], sheet.PhraseLengthBeats);
            DrawRow("Next Cue", $"{mark} · {offsets[nextIndex] - now}b");
        }
        else
        {
            DrawRow("Next Cue", "no upcoming cue");
        }

        var phrase = controller.beatManager?.Phrase.Span.Current;
        DrawRow("Phrase Remaining", phrase is { BeatsRemaining: { } left } ? $"{left}b" : "—");
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

    /// <summary>Draws optional timing detail directly from the canonical rhythm doorways.</summary>
    private static void DrawAdvancedTiming(Controller controller, DirectorStatus status)
    {
        showAdvancedTiming = EditorGUILayout.Foldout(showAdvancedTiming, "Advanced Wire Timing", true);
        if (!showAdvancedTiming)
        {
            return;
        }

        var beatManager = controller.beatManager;
        var grid = beatManager?.Grid.Current;
        DrawRow("Grid State", grid is { } g ? g.State.ToString() : "unlocked");
        DrawRow("Grid Beat", grid is { Beat: { } gridBeat } ? $"{gridBeat}/{CueSheet.GridBeats}" : "—");
        DrawRow("Grid Bar", grid is { Bar: { } gridBar } ? $"{gridBar}/4" : "—");
        DrawRow("Phrase", FormatPhraseRow(beatManager?.Phrase.Span.Current));
        DrawRow("Next Phrase", FormatNextPhraseRow(beatManager != null ? beatManager.Phrase : default));
        DrawRow("Irregular Phrase", beatManager?.Phrase.Span.Current?.Irregular == true ? "yes" : "no");
        DrawRow("Energy", FormatEnergyRow(beatManager != null ? beatManager.Energy : default));
        DrawRow("Loop", FormatLoopRow(beatManager != null ? beatManager.Loop : default));
    }

    /// <summary>Current phrase as <c>label · elapsed/length</c>, from the canonical Phrase doorway.</summary>
    private static string FormatPhraseRow(PhraseFacts? info)
    {
        if (!(info is { } phrase))
        {
            return "—";
        }

        var span = ElapsedPhraseBeats(info) is { } elapsed && phrase.LengthBeats is { } length
            ? $" · {elapsed}/{length}"
            : string.Empty;
        return $"{phrase.Name}{span}";
    }

    /// <summary>
    /// Beats elapsed into the current Phrase: <c>lengthBeats - beatsUntilNext</c>, clamped at zero.
    /// Null when the wire gave no length or boundary. This is the one place the Observatory turns Phrase
    /// timing into an elapsed position; the Grid Strip overlay, Cue Mark states, and the beats-until-cue
    /// countdown all read from it.
    /// </summary>
    private static int? ElapsedPhraseBeats(PhraseFacts? info)
    {
        return info is { LengthBeats: { } length, BeatsRemaining: { } untilNext }
            ? Mathf.Max(0, length - untilNext)
            : (int?)null;
    }

    /// <summary>
    /// Index of the next Cue Mark: the first offset at or after the elapsed Phrase beat (offsets are
    /// phrase-relative, ascending). A mark exactly at the elapsed beat counts as next (0 beats away), not
    /// passed. Returns -1 when there is no sheet, elapsed is unknown, or every mark is already passed.
    /// </summary>
    private static int NextCueMarkIndex(int[] offsets, int? elapsed)
    {
        if (offsets == null || !(elapsed is { } now))
        {
            return -1;
        }

        for (var i = 0; i < offsets.Length; i++)
        {
            if (offsets[i] >= now)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Passed / next / upcoming color for a Cue Mark; neutral when there is no elapsed Phrase position.</summary>
    private static Color CueMarkStateColor(int index, int nextIndex, bool hasElapsed)
    {
        if (!hasElapsed || index == nextIndex)
        {
            return CueMarkColor;
        }

        return nextIndex < 0 || index < nextIndex ? PassedMarkColor : UpcomingMarkColor;
    }

    /// <summary>
    /// Grid slots (1..16) holding a current-Phrase Cue Mark this frame, mapped phrase-relative to the live
    /// beat as <c>liveBeat + (offset - elapsed)</c>. Empty when the position or sheet is unknown; marks that
    /// land outside the visible window are dropped, never wrapped.
    /// </summary>
    private static System.Collections.Generic.HashSet<int> CueMarkGridSlots(CueSheetView sheet, int liveBeat, int? elapsed)
    {
        var slots = new System.Collections.Generic.HashSet<int>();
        if (!sheet.HasSheet || liveBeat < 1 || !(elapsed is { } now))
        {
            return slots;
        }

        foreach (var offset in sheet.CueMarkOffsets ?? System.Array.Empty<int>())
        {
            var slot = liveBeat + (offset - now);
            if (slot >= 1 && slot <= CueSheet.GridBeats)
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>Upcoming phrase as <c>label · in Nb · Nb long</c>, from the canonical Phrase doorway.</summary>
    private static string FormatNextPhraseRow(PhraseView phrase)
    {
        if (!(phrase.NextName is { } nextName))
        {
            return "—";
        }

        var untilChange = phrase.NextInBeats is { } beats ? $" · in {beats}b" : string.Empty;
        var length = phrase.NextLengthBeats is { } len ? $" · {len}b long" : string.Empty;
        return $"{nextName}{untilChange}{length}";
    }

    /// <summary>Energy as <c>level (→ next in Nb)</c>, from the canonical Energy doorway.</summary>
    private static string FormatEnergyRow(EnergyView energy)
    {
        if (!(energy.Run.Current is { } current))
        {
            return "—";
        }

        if (energy.NextLevel is { } next)
        {
            var untilChange = energy.NextChangeInBeats is { } beats ? $" in {beats}b" : string.Empty;
            return $"{current.Level} (→ {next}{untilChange})";
        }

        return current.Level.ToString();
    }

    /// <summary>Loop as <c>rolling/set/off · Nb</c>, from the canonical Loop doorway.</summary>
    private static string FormatLoopRow(LoopView loop)
    {
        if (loop.Rolling == null && loop.RegionSet == null && loop.LengthBeats == null)
        {
            return "—";
        }

        var state = loop.Rolling == true ? "rolling" : loop.RegionSet == true ? "set" : "off";
        var length = loop.LengthBeats is { } beats ? $" · {beats:0.##}b" : string.Empty;
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

    private static string FormatGridCount(int gridPosition)
    {
        return gridPosition > 0 ? $"{gridPosition} / {CueSheet.GridBeats}" : "—";
    }

    private static string FormatBeat(int beat)
    {
        return beat >= 0 && beat != int.MinValue ? beat.ToString() : "—";
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
