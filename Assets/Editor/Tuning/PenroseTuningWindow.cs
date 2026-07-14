using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Canonical wide authoring window for read-only live sequencing observation and saved Transition tuning.
/// </summary>
public sealed class PenroseTuningWindow : EditorWindow
{
    /// <summary>Current workspace tabs; Rhythm and final placeholder removal land in their owning tickets.</summary>
    private static readonly string[] Tabs = { "Live", "Transitions", "Effects" };

    private Type[] transitionTypes = Array.Empty<Type>();
    private string[] transitionNames = Array.Empty<string>();
    private int selectedTransitionIndex = -1;
    private int selectedTab;
    private Vector2 transitionListScroll;
    /// <summary>Scroll position for the selected Transition Settings editor.</summary>
    private Vector2 settingsScroll;
    /// <summary>Scroll position for the live sequencing timeline.</summary>
    private Vector2 liveTimelineScroll;
    private TransitionSettingsAsset selectedAsset;
    private SerializedObject selectedSerializedObject;
    private bool settingsChangedSinceLastSave;

    [MenuItem("Window/Penrose/Tuning")]
    public static void Open()
    {
        var window = GetWindow<PenroseTuningWindow>();
        window.titleContent = new GUIContent("Penrose Tuning");
        window.minSize = new Vector2(720f, 440f);
        window.Show();
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
        DrawToolbar();
        selectedTab = GUILayout.Toolbar(selectedTab, Tabs, EditorStyles.toolbarButton);

        EditorGUILayout.Space();
        switch (selectedTab)
        {
            case 0:
                DrawLiveTab();
                break;
            case 1:
                DrawTransitionsTab();
                break;
            case 2:
                DrawEffectsTab();
                break;
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
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
            GUILayout.Label("Window > Penrose > Tuning", EditorStyles.miniLabel);
        }
    }

    /// <summary>Draws read-only Director, Cue Sheet, Switcher, and live musical placement in one timeline.</summary>
    private void DrawLiveTab()
    {
        if (!LiveControllerAccess.TryGet(out var liveController))
        {
            EditorGUILayout.HelpBox(
                "Live timeline unavailable. Enter Play Mode and wait for the Controller to initialize.",
                MessageType.Info);
            return;
        }

        var director = liveController.DirectorStatus;
        var switcher = liveController.SwitcherStatus;
        var loadedCue = liveController.SwitcherLoadedCueStatus;
        var mode = director.IsSyncedMode ? "Synced Mode" : "Standalone Mode";

        using (var scroll = new EditorGUILayout.ScrollViewScope(liveTimelineScroll))
        {
            liveTimelineScroll = scroll.scrollPosition;

            EditorGUILayout.LabelField("LIVE SEQUENCING", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mode", mode);
            EditorGUILayout.LabelField("Director State", director.Mode.ToString());
            EditorGUILayout.LabelField(
                "Director Next Effect",
                FormatCatalogChoice(director.NextEffectIndex, director.NextEffectName));
            EditorGUILayout.LabelField(
                "Director Next Transition",
                FormatCatalogChoice(director.NextTransitionIndex, director.NextTransitionName));
            EditorGUILayout.LabelField(
                "Hold Selected",
                $"Effect {(director.HoldSelectedEffect ? "On" : "Off")} · Transition {(director.HoldSelectedTransition ? "On" : "Off")}");
            EditorGUILayout.LabelField("Switcher Active", FormatSwitcherActive(switcher));
            EditorGUILayout.LabelField("Loaded Cue", FormatLoadedCue(loadedCue));

            EditorGUILayout.Space(10f);
            LiveTimelineRenderer.Draw(LiveTimelineProjection.Build(CaptureTimelineInput(liveController)));
        }
    }

    /// <summary>Captures one frame of existing read-only runtime status for the pure timeline projection.</summary>
    private static LiveTimelineInput CaptureTimelineInput(Controller controller)
    {
        var director = controller.DirectorStatus;
        var switcher = controller.SwitcherStatus;
        var beatManager = controller.beatManager;
        var phrase = beatManager != null ? beatManager.Phrase : default;

        int? phraseBeat = null;
        if (phrase.LengthBeats is { } phraseLength &&
            phrase.BeatsRemaining is { } beatsRemaining &&
            phraseLength > 0 &&
            beatsRemaining >= 1 &&
            beatsRemaining <= phraseLength)
        {
            phraseBeat = phraseLength - beatsRemaining + 1;
        }

        float? executionProgress = switcher.CurrentTransitionIndex >= 0
            ? switcher.TransitionProgress
            : null;

        return new LiveTimelineInput(
            director.IsSyncedMode,
            director.CurrentSheet,
            director.NextSheet,
            beatManager?.Timing.Beat,
            phraseBeat,
            beatManager?.Grid.Beat,
            beatManager?.NextPhrase.BeatsUntil,
            controller.SwitcherLoadedCueStatus,
            executionProgress);
    }

    /// <summary>Formats the Switcher's active Effect or Transition without confusing it with Director intent.</summary>
    private static string FormatSwitcherActive(SwitcherStatus status)
    {
        if (!status.Ready)
        {
            return "Unavailable";
        }

        return status.CurrentTransitionIndex >= 0
            ? $"{FormatCatalogChoice(status.CurrentTransitionIndex, status.CurrentTransitionName)} · {status.TransitionProgress:P0}"
            : FormatCatalogChoice(status.CurrentEffectIndex, status.CurrentEffectName);
    }

    /// <summary>Formats the Switcher's Loaded Cue lifecycle separately from active execution.</summary>
    private static string FormatLoadedCue(SwitcherCueStatus cue)
    {
        if (!cue.HasCue)
        {
            return "None";
        }

        return $"Beat {cue.CueMarkBeat} · {(cue.IsLocked ? "Locked" : "Loaded")} · {cue.RunwayBeats}b Runway / {cue.TailBeats}b Tail";
    }

    private void DrawTransitionsTab()
    {
        LiveControllerAccess.TryGet(out var liveController);
        if (liveController != null)
        {
            SyncSelectedTransitionFromDirector(liveController);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTransitionList(liveController);
            DrawSelectedTransitionSettings(liveController);
        }
    }

    private void DrawTransitionList(Controller liveController)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(260f)))
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

    private void DrawPlayModeTransitionSteering(Controller liveController)
    {
        var directorReady = liveController.director != null;
        var directorStatus = liveController.DirectorStatus;
        var switcherStatus = liveController.SwitcherStatus;

        EditorGUILayout.LabelField("Play Mode Steering", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("Director Next Transition", FormatCatalogChoice(directorStatus.NextTransitionIndex, directorStatus.NextTransitionName));
            EditorGUILayout.LabelField("Director Next Effect", FormatCatalogChoice(directorStatus.NextEffectIndex, directorStatus.NextEffectName));

            var switcherShowingTransition = switcherStatus.Ready && switcherStatus.CurrentEffectIndex < 0;
            var activeTransition = !switcherStatus.Ready
                ? "Not Ready"
                : switcherShowingTransition
                    ? FormatCatalogChoice(switcherStatus.CurrentTransitionIndex, switcherStatus.CurrentTransitionName)
                    : "None — Mechanical Switcher is showing an Effect";
            EditorGUILayout.LabelField("Mechanical Switcher Active Transition", activeTransition);
            EditorGUILayout.LabelField("Mechanical Switcher Stage", string.IsNullOrEmpty(switcherStatus.StageName) ? "Not Ready" : switcherStatus.StageName);
            EditorGUILayout.LabelField("Current Effect", FormatCatalogChoice(switcherStatus.CurrentEffectIndex, switcherStatus.CurrentEffectName));
            if (switcherShowingTransition)
            {
                EditorGUILayout.LabelField("Transition Target Effect", FormatCatalogChoice(switcherStatus.TargetEffectIndex, switcherStatus.TargetEffectName));
            }

            using (new EditorGUI.DisabledScope(!directorReady || !IsValidTransitionIndex(selectedTransitionIndex)))
            {
                EditorGUI.BeginChangeCheck();
                var holdSelected = EditorGUILayout.ToggleLeft("Hold Selected Transition", directorStatus.HoldSelectedTransition);
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

    private void DrawEffectsTab()
    {
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Effects tuning will follow the same selection and Hold Selected pattern later. " +
            "This slice implements Transitions settings authoring and Play Mode steering.",
            MessageType.Info);
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

    private static string FormatCatalogChoice(int index, string name)
    {
        if (index < 0)
        {
            return "None";
        }

        return string.IsNullOrEmpty(name) ? $"#{index}" : $"{name} (#{index})";
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
