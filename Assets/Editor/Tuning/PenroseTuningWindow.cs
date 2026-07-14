using System;
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

/// <summary>
/// Canonical authoring window for live sequencing, rhythm observation, and saved Transition tuning.
/// </summary>
public sealed class PenroseTuningWindow : EditorWindow
{
    /// <summary>Minimum width that keeps Transition navigation beside its settings.</summary>
    private const float SplitViewWidth = 760f;

    /// <summary>The focused workspaces presented by the canonical Tuning Window.</summary>
    internal static readonly string[] WorkspaceTabs = { "Live", "Rhythm", "Transitions" };

    private Type[] transitionTypes = Array.Empty<Type>();
    private string[] transitionNames = Array.Empty<string>();
    private int selectedTransitionIndex = -1;
    private int selectedTab;
    private Vector2 transitionListScroll;
    /// <summary>Scroll position for the selected Transition Settings editor.</summary>
    private Vector2 settingsScroll;
    /// <summary>Scroll position for the live sequencing timeline.</summary>
    private Vector2 liveTimelineScroll;
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
        BeatManagerDrawer.DrawDashboard(dashboardRect, controller.beatManager, layoutWidth);
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

    /// <summary>Draws the next Transition's countdowns and rolling current/following Grid rows.</summary>
    private void DrawLiveTab()
    {
        if (!LiveControllerAccess.TryGet(out var liveController))
        {
            EditorGUILayout.HelpBox(
                "Live timeline unavailable. Enter Play Mode and wait for the Controller to initialize.",
                MessageType.Info);
            return;
        }

        using var scroll = new EditorGUILayout.ScrollViewScope(liveTimelineScroll);
        liveTimelineScroll = scroll.scrollPosition;

        var timelineInput = CaptureTimelineInput(liveController);
        EditorGUILayout.LabelField("LIVE TRANSITION", EditorStyles.boldLabel);
        LiveTimelineRenderer.Draw(
            LiveTimelineProjection.Build(timelineInput),
            ControllerStatusText.FormatSwitcherCue(liveController, timelineInput.Cue));
    }

    /// <summary>Captures one frame-coherent set of facts for the rolling live Transition display.</summary>
    private static LiveTimelineInput CaptureTimelineInput(Controller controller)
    {
        var director = controller.DirectorStatus;
        var beatManager = controller.beatManager;
        return new LiveTimelineInput(
            director.IsSyncedMode,
            beatManager?.Timing.Beat,
            beatManager?.Grid.Beat,
            controller.SwitcherPendingOrActiveCueStatus);
    }

    /// <summary>Draws Transition navigation and settings in a width-appropriate flow.</summary>
    private void DrawTransitionsTab()
    {
        LiveControllerAccess.TryGet(out var liveController);
        if (liveController != null)
        {
            SyncSelectedTransitionFromDirector(liveController);
        }

        DrawTransitionToolbar();
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

    /// <summary>Draws Transition-specific catalog actions inside the Transitions workspace.</summary>
    private void DrawTransitionToolbar()
    {
        using var toolbar = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
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
            if (liveController != null && liveController.director != null)
            {
                EditorGUILayout.HelpBox("Play Mode: selection mirrors Director Next Transition. Click a Transition to stage it through the Director.", MessageType.None);
            }
            else if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode is running, but the live Director is not ready yet. Settings authoring still works.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Edit Mode: select a Transition to edit its saved settings asset.", MessageType.None);
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(transitionListScroll, GUI.skin.box))
            {
                transitionListScroll = scroll.scrollPosition;
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
                        SelectTransition(i, liveController);
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

            EditorGUILayout.LabelField(transitionNames[selectedTransitionIndex], EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Settings Asset", selectedAsset, typeof(TransitionSettingsAsset), false);
                }

                if (GUILayout.Button("Restore Defaults", GUILayout.Width(130f)))
                {
                    RestoreSelectedDefaults();
                }
            }

            EditorGUILayout.Space();
            using (var scroll = new EditorGUILayout.ScrollViewScope(settingsScroll))
            {
                settingsScroll = scroll.scrollPosition;
                selectedSerializedObject.Update();
                var settingsProperty = selectedSerializedObject.FindProperty("settings");
                var runwayProperty = settingsProperty.FindPropertyRelative(nameof(TransitionSettings.RunwayBeats));
                var tailProperty = settingsProperty.FindPropertyRelative(nameof(TransitionSettings.TailBeats));
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(settingsProperty, includeChildren: true);
                EditorGUILayout.HelpBox(
                    TransitionSettings.DurationValidationMessage(runwayProperty.intValue, tailProperty.intValue),
                    MessageType.Info);
                if (EditorGUI.EndChangeCheck())
                {
                    TransitionSettingsAssetUtility.ApplyConstrainedSettings(selectedSerializedObject);
                    settingsChangedSinceLastSave = true;
                }
            }
        }
    }

    /// <summary>Draws distinct Director Next, Switcher Active, Hold Selected, and Held Effect state.</summary>
    private void DrawPlayModeTransitionSteering(Controller liveController)
    {
        var directorReady = liveController.director != null;
        var directorStatus = liveController.DirectorStatus;
        var switcherStatus = liveController.SwitcherStatus;

        EditorGUILayout.LabelField("Play Mode Steering", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField(
                "Director Next",
                ControllerStatusText.FormatDirectorNext(directorStatus));

            var switcherShowingTransition = switcherStatus.Ready && switcherStatus.CurrentEffectIndex < 0;
            EditorGUILayout.LabelField("Switcher Active", ControllerStatusText.FormatSwitcherActive(switcherStatus));
            EditorGUILayout.LabelField("Switcher Stage", string.IsNullOrEmpty(switcherStatus.StageName) ? "Not Ready" : switcherStatus.StageName);
            EditorGUILayout.LabelField("Held Effect", ControllerStatusText.FormatHeldEffect(liveController));
            if (switcherShowingTransition)
            {
                EditorGUILayout.LabelField("Switcher Target", ControllerStatusText.FormatCatalogChoice(switcherStatus.TargetEffectIndex, switcherStatus.TargetEffectName));
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

    private void ReloadTransitions()
    {
        var factory = new Factory<TransitionBase>();
        transitionTypes = factory.Types;
        transitionNames = factory.Names;
        if (transitionTypes.Length == 0)
        {
            selectedTransitionIndex = -1;
            selectedAsset = null;
            selectedSerializedObject = null;
            return;
        }

        SetSelectedTransitionIndex(Mathf.Clamp(selectedTransitionIndex, 0, transitionTypes.Length - 1));
    }

    private void SelectTransition(int index, Controller liveController)
    {
        if (liveController != null && liveController.director != null)
        {
            liveController.director.SetNextTransition(index);
        }

        SetSelectedTransitionIndex(index);
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

    private void SyncSelectedTransitionFromDirector(Controller liveController)
    {
        if (liveController.director == null)
        {
            return;
        }

        var nextTransitionIndex = liveController.DirectorStatus.NextTransitionIndex;
        if (!IsValidTransitionIndex(nextTransitionIndex) || nextTransitionIndex == selectedTransitionIndex)
        {
            return;
        }

        SetSelectedTransitionIndex(nextTransitionIndex);
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
