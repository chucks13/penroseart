using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Describes how the Transitions workspace arranges navigation and settings.</summary>
internal enum TuningWorkspaceFlow
{
    /// <summary>Places Transition navigation above the selected settings.</summary>
    Stacked,

    /// <summary>Places Transition navigation beside the selected settings.</summary>
    Split,
}

/// <summary>Chooses whether Transition authoring follows live Director intent or a stable saved selection.</summary>
internal enum TransitionSelectionMode
{
    /// <summary>Tracks the Director's staged Next Transition when one is available.</summary>
    FollowDirector,

    /// <summary>Keeps the author's saved Transition Settings selection stable.</summary>
    PinSelection,
}

/// <summary>Pure selection policy separating authoring state from Director and Switcher runtime state.</summary>
internal readonly struct TransitionAuthoringSelection
{
    /// <summary>Creates one immutable authoring selection state.</summary>
    private TransitionAuthoringSelection(
        TransitionSelectionMode mode,
        int authoringIndex,
        int pinnedIndex)
    {
        Mode = mode;
        AuthoringIndex = authoringIndex;
        PinnedIndex = pinnedIndex;
    }

    /// <summary>The active authoring selection mode.</summary>
    public TransitionSelectionMode Mode { get; }

    /// <summary>The catalog index whose saved settings are currently being edited.</summary>
    public int AuthoringIndex { get; }

    /// <summary>The stable author choice retained independently of Director observation.</summary>
    public int PinnedIndex { get; }

    /// <summary>Restores serialized Editor window state and normalizes it to the current catalog.</summary>
    public static TransitionAuthoringSelection Restore(
        TransitionSelectionMode mode,
        int authoringIndex,
        int pinnedIndex,
        int catalogCount)
    {
        return new TransitionAuthoringSelection(mode, authoringIndex, pinnedIndex)
            .WithCatalogCount(catalogCount);
    }

    /// <summary>Switches modes without mutating any runtime or settings asset.</summary>
    public TransitionAuthoringSelection SetMode(
        TransitionSelectionMode mode,
        int directorNextIndex,
        int catalogCount)
    {
        var normalized = WithCatalogCount(catalogCount);
        if (mode == TransitionSelectionMode.PinSelection)
        {
            return new TransitionAuthoringSelection(mode, normalized.AuthoringIndex, normalized.AuthoringIndex);
        }

        var followedIndex = IsValidIndex(directorNextIndex, catalogCount)
            ? directorNextIndex
            : normalized.AuthoringIndex;
        return new TransitionAuthoringSelection(mode, followedIndex, normalized.PinnedIndex);
    }

    /// <summary>Updates the authoring target only while Follow Director is active.</summary>
    public TransitionAuthoringSelection ObserveDirector(int directorNextIndex, int catalogCount)
    {
        var normalized = WithCatalogCount(catalogCount);
        if (normalized.Mode != TransitionSelectionMode.FollowDirector ||
            !IsValidIndex(directorNextIndex, catalogCount))
        {
            return normalized;
        }

        return new TransitionAuthoringSelection(normalized.Mode, directorNextIndex, normalized.PinnedIndex);
    }

    /// <summary>Changes the authoring target only through the explicit Pin Selection mode.</summary>
    public TransitionAuthoringSelection SelectPinned(int authoringIndex, int catalogCount)
    {
        var normalized = WithCatalogCount(catalogCount);
        if (normalized.Mode != TransitionSelectionMode.PinSelection ||
            !IsValidIndex(authoringIndex, catalogCount))
        {
            return normalized;
        }

        return new TransitionAuthoringSelection(normalized.Mode, authoringIndex, authoringIndex);
    }

    /// <summary>Normalizes retained indices after catalog reload without choosing runtime behavior.</summary>
    public TransitionAuthoringSelection WithCatalogCount(int catalogCount)
    {
        if (catalogCount <= 0)
        {
            return new TransitionAuthoringSelection(Mode, -1, -1);
        }

        var authoringIndex = IsValidIndex(AuthoringIndex, catalogCount) ? AuthoringIndex : 0;
        var pinnedIndex = IsValidIndex(PinnedIndex, catalogCount) ? PinnedIndex : authoringIndex;
        return new TransitionAuthoringSelection(Mode, authoringIndex, pinnedIndex);
    }

    /// <summary>Reports whether an index belongs to the current Transition catalog.</summary>
    private static bool IsValidIndex(int index, int catalogCount)
    {
        return index >= 0 && index < catalogCount;
    }
}

/// <summary>
/// Canonical authoring window for live sequencing, rhythm observation, and saved Transition tuning.
/// </summary>
public sealed class PenroseTuningWindow : EditorWindow
{
    /// <summary>Minimum width that keeps Transition navigation beside its settings.</summary>
    private const float SplitViewWidth = 760f;

    /// <summary>The focused workspaces presented by the canonical Tuning Window.</summary>
    internal static readonly string[] WorkspaceTabs = { "Live", "Rhythm", "Transitions" };

    /// <summary>Labels for the explicit Transition authoring selection modes.</summary>
    private static readonly string[] TransitionSelectionModeLabels = { "Follow Director", "Pin Selection" };

    private Type[] transitionTypes = Array.Empty<Type>();
    private string[] transitionNames = Array.Empty<string>();
    /// <summary>The catalog index whose saved Transition Settings are currently being edited.</summary>
    [SerializeField]
    private int selectedTransitionIndex = -1;
    /// <summary>The explicit authoring mode retained across Editor window reloads.</summary>
    [SerializeField]
    private TransitionSelectionMode transitionSelectionMode;
    /// <summary>The author's stable Transition catalog choice retained independently of live intent.</summary>
    [SerializeField]
    private int pinnedTransitionIndex = -1;
    private int selectedTab;
    private Vector2 transitionListScroll;
    /// <summary>Scroll position for the selected Transition Settings editor.</summary>
    private Vector2 settingsScroll;
    /// <summary>Scroll position for the live sequencing timeline.</summary>
    private Vector2 liveTimelineScroll;
    /// <summary>Player slot pinned in the Cue Sheet tracker; -1 follows the on-air focus player.</summary>
    private int selectedSheetSlot = -1;
    /// <summary>Playhead row the tracker last saw, so auto-scroll reacts only to row changes.</summary>
    private int lastPlayheadRow = -1;
    /// <summary>Visible height of the tracker scroll view, measured on the last repaint.</summary>
    private float liveViewHeight;
    /// <summary>Scroll position for the rhythm workspace.</summary>
    private Vector2 rhythmScroll;
    private TransitionSettingsAsset selectedAsset;
    private SerializedObject selectedSerializedObject;
    private bool settingsChangedSinceLastSave;

    /// <summary>Opens or focuses the canonical Penrose Tuning Window.</summary>
    [MenuItem("Window/Penrose/Tuning")]
    public static void Open()
    {
        var window = GetWindow<PenroseTuningWindow>();
        window.titleContent = new GUIContent("Penrose Tuning");
        window.minSize = new Vector2(520f, 440f);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Penrose Tuning");
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        ReloadTransitions();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        SavePendingSettingsAssets();
    }

    private void OnInspectorUpdate()
    {
        LiveControllerAccess.RepaintDuringPlayMode(this);
    }

    /// <summary>Draws the active workspace tab without mutating runtime sequencing state.</summary>
    private void OnGUI()
    {
        selectedTab = GUILayout.Toolbar(selectedTab, WorkspaceTabs, EditorStyles.toolbarButton);

        EditorGUILayout.Space();
        switch (selectedTab)
        {
            case 0:
                DrawLiveTab();
                break;
            case 1:
                DrawRhythmTab();
                break;
            case 2:
                DrawTransitionsTab();
                break;
        }
    }

    /// <summary>Draws the scene Controller's read-only BeatManager dashboard.</summary>
    private void DrawRhythmTab()
    {
        using var scroll = new EditorGUILayout.ScrollViewScope(rhythmScroll);
        rhythmScroll = scroll.scrollPosition;

        EditorGUILayout.LabelField("RHYTHM", EditorStyles.boldLabel);
        if (!TryGetWorkspaceController(out var controller))
        {
            EditorGUILayout.HelpBox(
                "No compatible scene Controller is available. Open the wall scene to inspect rhythm state.",
                MessageType.Info);
            return;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Edit Mode has no live musical facts. Enter Play Mode to observe rhythm state.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Controller", controller, typeof(Controller), true);
        }

        if (controller.beatManager == null)
        {
            EditorGUILayout.HelpBox("This Controller has no Beat Manager.", MessageType.Warning);
            return;
        }

        var layoutWidth = position.width;
        var dashboardRect = EditorGUILayout.GetControlRect(
            hasLabel: false,
            height: BeatManagerDashboardRenderer.DashboardHeightForWidth(layoutWidth),
            GUILayout.ExpandWidth(true));
        BeatManagerDrawer.DrawDashboard(dashboardRect, controller, layoutWidth);
        // OnInspectorUpdate is capped at 10 Hz. A visible Rhythm repaint schedules the next one so live
        // pulses stay smooth; hidden tabs never enter this branch and keep the cheaper Inspector cadence.
        if (Application.isPlaying && Event.current.type == EventType.Repaint)
        {
            Repaint();
        }
    }

    /// <summary>Finds the live Controller first, then an inactive-compatible scene Controller for Edit Mode.</summary>
    private static bool TryGetWorkspaceController(out Controller controller)
    {
        if (LiveControllerAccess.TryGet(out controller))
        {
            return true;
        }

        var controllers = UnityEngine.Object.FindObjectsByType<Controller>(FindObjectsInactive.Include);
        for (var i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i].gameObject.scene.IsValid())
            {
                controller = controllers[i];
                return true;
            }
        }

        controller = null;
        return false;
    }

    /// <summary>Draws live switching state, the shared Effect / Hold control, and the sticky Cue Sheet tracker.</summary>
    private void DrawLiveTab()
    {
        if (!LiveControllerAccess.TryGet(out var controller))
        {
            EditorGUILayout.HelpBox(
                "Cue Sheet tracker unavailable. Enter Play Mode and wait for the Controller to initialize.",
                MessageType.Info);
            return;
        }

        var director = controller.director;
        var switcher = controller.switcher;
        var beatManager = controller.beatManager;
        if (director == null || switcher == null || beatManager == null)
        {
            EditorGUILayout.HelpBox("Sequencing runtime is still initializing.", MessageType.Info);
            return;
        }

        TransitionBarRenderer.Draw(switcher.Status);
        DrawOffPlanReadout(switcher.Status, controller);
        EffectHoldRenderer.Draw(controller, controller.DirectorStatus);

        var viewWidth = position.width - 20f;
        var activeSlot = DrawSheetSlotToolbar(director.Sheets, beatManager);
        CueSheetTimelineRenderer.DrawHeader(viewWidth);

        IReadOnlyList<CueSheetGridRow> rows = Array.Empty<CueSheetGridRow>();
        int? playheadRow = null;
        if (activeSlot >= 0)
        {
            var sheet = director.Sheets[activeSlot];
            var player = beatManager.Players[activeSlot];
            rows = CueSheetTimeline.Build(
                sheet, player.Structure, TransitionRepertoiresOf(controller), player.Beat);
            var row = player.Beat is { } beat ? CueSheetTimeline.RowContaining(rows, beat) : -1;
            if (row >= 0)
            {
                playheadRow = row;
            }
        }

        liveTimelineScroll = CueSheetTimelineRenderer.AutoScroll(
            liveTimelineScroll,
            playheadRow,
            lastPlayheadRow >= 0 ? lastPlayheadRow : null,
            liveViewHeight);
        lastPlayheadRow = playheadRow ?? -1;

        using (var scroll = new EditorGUILayout.ScrollViewScope(liveTimelineScroll))
        {
            liveTimelineScroll = scroll.scrollPosition;
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No Cue Sheet to present. Sheets build once a live player holds a complete track structure.",
                    MessageType.Info);
            }
            else
            {
                CueSheetTimelineRenderer.DrawRows(
                    rows, EffectNamesOf(controller), TransitionNamesOf(controller), viewWidth);
            }
        }

        if (Event.current.type == EventType.Repaint)
        {
            liveViewHeight = GUILayoutUtility.GetLastRect().height;
            // Same reason as the Rhythm tab: OnInspectorUpdate's 10 Hz cap would render the
            // transition rail and playhead in visible steps, so a visible Live repaint schedules
            // the next one.
            if (Application.isPlaying)
            {
                Repaint();
            }
        }
    }

    /// <summary>
    /// One-line readout of the last off-plan question and answer, straight from the Switcher's read-only
    /// status snapshot (ADR-0006: the editor reads, never originates). Empty until the first ask; last
    /// value only — history stays in the Cue Log and traces.
    /// </summary>
    /// <param name="status">The Switcher snapshot carrying the last Off-Plan Sighting and answer.</param>
    /// <param name="controller">Live Controller, used only to name the dealt Effect and Transition.</param>
    private static void DrawOffPlanReadout(SwitcherStatus status, Controller controller)
    {
        if (status.LastOffPlanSighting is not { } sighting)
        {
            EditorGUILayout.LabelField("Off-Plan", "—");
            return;
        }

        var answer = status.LastOffPlanAnswer;
        var verdict = answer.Perform
            ? $"TAKE {CueSheetTimelineRenderer.NameOf(EffectNamesOf(controller), answer.EffectIndex)}" +
              $" / {CueSheetTimelineRenderer.NameOf(TransitionNamesOf(controller), answer.TransitionIndex)}"
            : "RIDE";
        EditorGUILayout.LabelField(
            "Off-Plan",
            $"{sighting.Anomaly} @ {sighting.BoundaryBeat} · gap {sighting.GapGrids} · ask {sighting.Ask} → {verdict}");
    }

    /// <summary>
    /// Draws the slot selector for every player slot holding a sheet, and returns the slot whose sheet the
    /// tracker presents (-1 when none). Defaults to the on-air focus player until the user pins a slot.
    /// </summary>
    private int DrawSheetSlotToolbar(IReadOnlyList<TrackCueSheet> sheets, BeatManager beatManager)
    {
        var slots = new List<int>();
        for (var slot = 0; slot < sheets.Count; slot++)
        {
            if (sheets[slot].StructureGeneration != 0)
            {
                slots.Add(slot);
            }
        }

        var activeSlot = -1;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (slots.Count == 0)
            {
                GUILayout.Label("No Cue Sheets", EditorStyles.miniLabel);
            }
            else
            {
                var labels = new string[slots.Count];
                for (var i = 0; i < slots.Count; i++)
                {
                    var sheet = sheets[slots[i]];
                    labels[i] = $"P{sheet.PlayerNumber} · g{sheet.StructureGeneration}";
                }

                var focusSlot = beatManager.LiveOrder.Focus is { } focus ? focus - 1 : -1;
                var current = slots.Contains(selectedSheetSlot) ? selectedSheetSlot
                    : slots.Contains(focusSlot) ? focusSlot
                    : slots[0];
                var clicked = GUILayout.Toolbar(
                    slots.IndexOf(current), labels, EditorStyles.miniButton, GUILayout.ExpandWidth(false));
                activeSlot = slots[clicked];
                if (activeSlot != current)
                {
                    selectedSheetSlot = activeSlot;
                }
            }

        }

        return activeSlot;
    }

    /// <summary>The Transition catalog's repertoires by catalog index, for Runway/Tail lengths.</summary>
    private static TransitionRepertoire[] TransitionRepertoiresOf(Controller controller)
    {
        var transitions = controller.transitions ?? Array.Empty<TransitionBase>();
        var repertoires = new TransitionRepertoire[transitions.Length];
        for (var i = 0; i < transitions.Length; i++)
        {
            repertoires[i] = transitions[i] != null ? transitions[i].Repertoire : default;
        }

        return repertoires;
    }

    /// <summary>Effect catalog display names by catalog index.</summary>
    private static string[] EffectNamesOf(Controller controller)
    {
        var effects = controller.effects ?? Array.Empty<EffectBase>();
        var names = new string[effects.Length];
        for (var i = 0; i < effects.Length; i++)
        {
            names[i] = effects[i] != null ? effects[i].Name : "?";
        }

        return names;
    }

    /// <summary>Transition catalog display names by catalog index.</summary>
    private static string[] TransitionNamesOf(Controller controller)
    {
        var transitions = controller.transitions ?? Array.Empty<TransitionBase>();
        var names = new string[transitions.Length];
        for (var i = 0; i < transitions.Length; i++)
        {
            names[i] = transitions[i] != null ? transitions[i].Name : "?";
        }

        return names;
    }

    /// <summary>Draws Transition navigation and settings in a width-appropriate flow.</summary>
    private void DrawTransitionsTab()
    {
        LiveControllerAccess.TryGet(out var liveController);
        if (liveController != null)
        {
            SyncSelectedTransitionFromDirector(liveController);
        }

        DrawTransitionToolbar(liveController);
        if (FlowForWidth(position.width) == TuningWorkspaceFlow.Split)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            DrawTransitionList(liveController, GUILayout.Width(260f));
            DrawSelectedTransitionSettings(liveController);
            return;
        }

        var listHeight = Mathf.Clamp(position.height * 0.32f, 150f, 260f);
        DrawTransitionList(liveController, GUILayout.Height(listHeight));
        EditorGUILayout.Space(6f);
        DrawSelectedTransitionSettings(liveController);
    }

    /// <summary>Chooses stacked or split Transition flow for the current workspace width.</summary>
    internal static TuningWorkspaceFlow FlowForWidth(float width)
    {
        return width >= SplitViewWidth ? TuningWorkspaceFlow.Split : TuningWorkspaceFlow.Stacked;
    }

    /// <summary>Draws explicit authoring selection modes and Transition catalog actions.</summary>
    private void DrawTransitionToolbar(Controller liveController)
    {
        using var toolbar = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
        var requestedMode = (TransitionSelectionMode)GUILayout.Toolbar(
            (int)transitionSelectionMode,
            TransitionSelectionModeLabels,
            EditorStyles.toolbarButton,
            GUILayout.Width(220f));
        if (requestedMode != transitionSelectionMode)
        {
            SetTransitionSelectionMode(requestedMode, liveController);
        }

        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(64f)))
        {
            ReloadTransitions();
        }

        if (GUILayout.Button("Create Missing Settings", EditorStyles.toolbarButton, GUILayout.Width(150f)))
        {
            TransitionSettingsAssetUtility.EnsureCatalogAssets();
            ReloadTransitions();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Saved Transition Settings", EditorStyles.miniLabel);
    }

    /// <summary>Draws the Transition catalog within the supplied responsive layout constraints.</summary>
    private void DrawTransitionList(Controller liveController, params GUILayoutOption[] layoutOptions)
    {
        using (new EditorGUILayout.VerticalScope(layoutOptions))
        {
            EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);
            if (transitionSelectionMode == TransitionSelectionMode.FollowDirector &&
                liveController != null &&
                liveController.director != null)
            {
                EditorGUILayout.HelpBox(
                    "Follow Director: the authoring selection mirrors Director Next. Switch to Pin Selection to choose a stable settings asset.",
                    MessageType.None);
            }
            else if (transitionSelectionMode == TransitionSelectionMode.PinSelection)
            {
                EditorGUILayout.HelpBox(
                    "Pin Selection: choose one saved settings asset while Director Next and Switcher Active continue updating separately.",
                    MessageType.None);
            }
            else if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Follow Director is waiting for the live Director. Switch to Pin Selection to author settings now.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Follow Director has no live source in Edit Mode. Switch to Pin Selection to choose a saved settings asset.",
                    MessageType.None);
            }

            using var scroll = new EditorGUILayout.ScrollViewScope(transitionListScroll, GUI.skin.box);
            transitionListScroll = scroll.scrollPosition;
            using (new EditorGUI.DisabledScope(transitionSelectionMode != TransitionSelectionMode.PinSelection))
            {
                for (var i = 0; i < transitionTypes.Length; i++)
                {
                    var isSelected = i == selectedTransitionIndex;
                    var previousColor = GUI.backgroundColor;
                    if (isSelected)
                    {
                        GUI.backgroundColor = new Color(0.3f, 0.55f, 0.7f);
                    }

                    var label = isSelected ? $"▶ {transitionNames[i]}" : transitionNames[i];
                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        SelectTransition(i);
                        GUI.FocusControl(null);
                    }

                    GUI.backgroundColor = previousColor;
                }
            }
        }
    }

    /// <summary>Draws the selected saved Transition Settings asset without creating one during observation.</summary>
    private void DrawSelectedTransitionSettings(Controller liveController)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            DrawTransitionSelectionSummary(liveController);
            EditorGUILayout.Space();
            if (liveController != null)
            {
                DrawPlayModeTransitionSteering(liveController);
                EditorGUILayout.Space();
            }
            else if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("No live Controller is ready yet. The saved settings workflow is still available.", MessageType.Warning);
                EditorGUILayout.Space();
            }

            if (selectedTransitionIndex < 0 || selectedTransitionIndex >= transitionTypes.Length)
            {
                EditorGUILayout.HelpBox("Select a Transition to edit its saved settings asset.", MessageType.Info);
                return;
            }

            LoadSelectedAsset();
            if (selectedAsset == null || selectedSerializedObject == null)
            {
                EditorGUILayout.HelpBox(
                    "No saved Transition Settings asset exists for this Transition. Observation never creates one.",
                    MessageType.Warning);
                if (GUILayout.Button("Create Settings Asset", GUILayout.Width(160f)))
                {
                    CreateSelectedSettingsAsset();
                }

                return;
            }

            EditorGUILayout.LabelField("Saved Transition Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Authoring Asset", selectedAsset, typeof(TransitionSettingsAsset), false);
                }

                if (GUILayout.Button("Restore Defaults", GUILayout.Width(130f)))
                {
                    RestoreSelectedDefaults();
                }
            }

            EditorGUILayout.Space();
            using var scroll = new EditorGUILayout.ScrollViewScope(settingsScroll);
            settingsScroll = scroll.scrollPosition;
            selectedSerializedObject.Update();
            var settingsProperty = selectedSerializedObject.FindProperty("settings");
            var runwayProperty = settingsProperty.FindPropertyRelative(nameof(TransitionSettings.RunwayBeats));
            var tailProperty = settingsProperty.FindPropertyRelative(nameof(TransitionSettings.TailBeats));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Shared Timing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(runwayProperty, new GUIContent("Runway (beats)"));
            EditorGUILayout.PropertyField(tailProperty, new GUIContent("Tail (beats)"));
            EditorGUILayout.HelpBox(
                TransitionSettings.DurationValidationMessage(runwayProperty.intValue, tailProperty.intValue),
                MessageType.Info);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Transition-Specific Settings", EditorStyles.boldLabel);
            DrawTransitionSpecificProperties(settingsProperty);
            if (EditorGUI.EndChangeCheck())
            {
                TransitionSettingsAssetUtility.ApplyConstrainedSettings(selectedSerializedObject);
                settingsChangedSinceLastSave = true;
                selectedSerializedObject.Update();
            }
        }
    }

    /// <summary>Draws the distinct authoring, pinned, Director Next, and Switcher Active identities.</summary>
    private void DrawTransitionSelectionSummary(Controller liveController)
    {
        using var panel = new EditorGUILayout.VerticalScope(GUI.skin.box);
        EditorGUILayout.LabelField("Selection and Live Status", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Authoring Selection", FormatTransitionName(selectedTransitionIndex));
        var pinnedLabel = FormatTransitionName(pinnedTransitionIndex);
        EditorGUILayout.LabelField(
            "Pinned Selection",
            transitionSelectionMode == TransitionSelectionMode.PinSelection
                ? pinnedLabel
                : $"Inactive · {pinnedLabel}");

        if (liveController == null)
        {
            EditorGUILayout.LabelField("Director Next", "Unavailable outside live Play Mode");
            EditorGUILayout.LabelField("Switcher Active", "Unavailable outside live Play Mode");
            return;
        }

        var directorStatus = liveController.DirectorStatus;
        var switcherStatus = liveController.SwitcherStatus;
        EditorGUILayout.LabelField("Director Next", ControllerStatusText.FormatDirectorNext(directorStatus));
        EditorGUILayout.LabelField("Switcher Active", ControllerStatusText.FormatSwitcherActive(switcherStatus));
        EditorGUILayout.LabelField(
            "Switcher Stage",
            string.IsNullOrEmpty(switcherStatus.StageName) ? "Not Ready" : switcherStatus.StageName);
    }

    /// <summary>Draws every serialized Transition setting except the shared Runway and Tail controls.</summary>
    private static void DrawTransitionSpecificProperties(SerializedProperty settingsProperty)
    {
        var property = settingsProperty.Copy();
        var endProperty = property.GetEndProperty();
        var enterChildren = true;
        while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, endProperty))
        {
            enterChildren = false;
            if (property.name == nameof(TransitionSettings.RunwayBeats) ||
                property.name == nameof(TransitionSettings.TailBeats))
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, includeChildren: true);
        }
    }

    /// <summary>Draws explicit Director staging and Hold Selected controls.</summary>
    private void DrawPlayModeTransitionSteering(Controller liveController)
    {
        var directorReady = liveController.director != null;
        var directorStatus = liveController.DirectorStatus;

        EditorGUILayout.LabelField("Director Steering", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUI.DisabledScope(!directorReady || !IsValidTransitionIndex(selectedTransitionIndex)))
            {
                if (GUILayout.Button("Stage Authoring Selection as Director Next"))
                {
                    liveController.director.SetNextTransition(selectedTransitionIndex);
                    Repaint();
                }
            }

            using (new EditorGUI.DisabledScope(!directorReady || !IsValidTransitionIndex(selectedTransitionIndex)))
            {
                EditorGUI.BeginChangeCheck();
                var holdSelected = EditorGUILayout.ToggleLeft(
                    new GUIContent("Hold Selected", "Keep this Transition as Director Next after each move."),
                    directorStatus.HoldSelectedTransition);
                if (EditorGUI.EndChangeCheck() && directorReady)
                {
                    if (holdSelected)
                    {
                        liveController.director.SetNextTransition(selectedTransitionIndex);
                    }

                    liveController.director.SetHoldSelectedTransition(holdSelected);
                    Repaint();
                }
            }
        }
    }

    /// <summary>Formats a Transition catalog index without inventing a missing selection.</summary>
    private string FormatTransitionName(int index)
    {
        return IsValidTransitionIndex(index) ? transitionNames[index] : "Unavailable";
    }

    /// <summary>Reloads the runtime Transition catalog and restores normalized authoring selection state.</summary>
    private void ReloadTransitions()
    {
        var factory = new Factory<TransitionBase>();
        transitionTypes = factory.Types;
        transitionNames = factory.Names;
        var selection = CurrentSelection();
        transitionSelectionMode = selection.Mode;
        pinnedTransitionIndex = selection.PinnedIndex;
        SetSelectedTransitionIndex(selection.AuthoringIndex);
    }

    /// <summary>Changes the authoring target only when the author has explicitly chosen Pin Selection.</summary>
    private void SelectTransition(int index)
    {
        ApplySelection(CurrentSelection().SelectPinned(index, transitionTypes.Length));
    }

    /// <summary>Changes the observed Transition selection and loads only an already-saved settings asset.</summary>
    private void SetSelectedTransitionIndex(int index)
    {
        selectedTransitionIndex = index;
        settingsScroll = Vector2.zero;
        selectedAsset = null;
        selectedSerializedObject = null;
        LoadSelectedAsset();
    }

    /// <summary>Switches the explicit authoring mode without changing Director or settings state.</summary>
    private void SetTransitionSelectionMode(TransitionSelectionMode mode, Controller liveController)
    {
        var directorNextIndex = liveController == null ? -1 : liveController.DirectorStatus.NextTransitionIndex;
        ApplySelection(CurrentSelection().SetMode(mode, directorNextIndex, transitionTypes.Length));
    }

    /// <summary>Restores the pure selection state from the Editor window's serialized fields.</summary>
    private TransitionAuthoringSelection CurrentSelection()
    {
        return TransitionAuthoringSelection.Restore(
            transitionSelectionMode,
            selectedTransitionIndex,
            pinnedTransitionIndex,
            transitionTypes.Length);
    }

    /// <summary>Applies pure selection output and reloads only the observed saved asset.</summary>
    private void ApplySelection(TransitionAuthoringSelection selection)
    {
        transitionSelectionMode = selection.Mode;
        pinnedTransitionIndex = selection.PinnedIndex;
        if (selection.AuthoringIndex != selectedTransitionIndex)
        {
            SetSelectedTransitionIndex(selection.AuthoringIndex);
        }
    }

    /// <summary>Observes Director Next only while Follow Director is active.</summary>
    private void SyncSelectedTransitionFromDirector(Controller liveController)
    {
        if (liveController.director == null)
        {
            return;
        }

        ApplySelection(CurrentSelection().ObserveDirector(
            liveController.DirectorStatus.NextTransitionIndex,
            transitionTypes.Length));
    }

    private bool IsValidTransitionIndex(int index)
    {
        return index >= 0 && index < transitionTypes.Length;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SavePendingSettingsAssets();
        }
    }

    /// <summary>Persists serialized Transition Settings edits that are already constrained and dirty.</summary>
    private void SavePendingSettingsAssets()
    {
        if (!settingsChangedSinceLastSave)
        {
            return;
        }

        AssetDatabase.SaveAssets();
        settingsChangedSinceLastSave = false;
    }

    /// <summary>Loads the selected Transition's saved settings asset without creating project state.</summary>
    private void LoadSelectedAsset()
    {
        if (selectedAsset != null && selectedSerializedObject != null)
        {
            return;
        }

        if (!IsValidTransitionIndex(selectedTransitionIndex))
        {
            return;
        }

        selectedAsset = TransitionSettingsAssetUtility.LoadAsset(transitionTypes[selectedTransitionIndex]);
        selectedSerializedObject = selectedAsset == null ? null : new SerializedObject(selectedAsset);
    }

    /// <summary>Creates the selected Transition's missing settings asset after an explicit author action.</summary>
    private void CreateSelectedSettingsAsset()
    {
        var transition = CreateSelectedTransition();
        if (transition == null)
        {
            return;
        }

        selectedAsset = TransitionSettingsAssetUtility.EnsureAsset(
            transitionTypes[selectedTransitionIndex],
            transition.CodeDefaults);
        selectedSerializedObject = new SerializedObject(selectedAsset);
        Repaint();
    }

    /// <summary>Restores every saved field on the selected Transition Settings asset through Unity serialization.</summary>
    private void RestoreSelectedDefaults()
    {
        var transition = CreateSelectedTransition();
        if (transition == null)
        {
            return;
        }

        selectedAsset = TransitionSettingsAssetUtility.RestoreDefaults(
            transitionTypes[selectedTransitionIndex],
            transition.CodeDefaults);
        selectedSerializedObject = new SerializedObject(selectedAsset);
        settingsChangedSinceLastSave = false;
        Repaint();
    }

    private TransitionBase CreateSelectedTransition()
    {
        if (selectedTransitionIndex < 0 || selectedTransitionIndex >= transitionTypes.Length)
        {
            return null;
        }

        var factory = new Factory<TransitionBase>();
        return factory.Create(transitionTypes[selectedTransitionIndex]);
    }
}
