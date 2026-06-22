using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Standalone authoring window for Penrose runtime tuning. The Transitions tab edits saved Transition Settings
/// and, in Play Mode, steers the Director's staged Next Transition.
/// </summary>
public sealed class PenroseTuningWindow : EditorWindow
{
    private static readonly string[] Tabs = { "Transitions", "Effects" };

    private Type[] transitionTypes = Array.Empty<Type>();
    private string[] transitionNames = Array.Empty<string>();
    private int selectedTransitionIndex = -1;
    private int selectedTab;
    private Vector2 transitionListScroll;
    private Vector2 settingsScroll;
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

    private void OnGUI()
    {
        DrawToolbar();
        selectedTab = GUILayout.Toolbar(selectedTab, Tabs, EditorStyles.toolbarButton);

        EditorGUILayout.Space();
        switch (selectedTab)
        {
            case 0:
                DrawTransitionsTab();
                break;
            case 1:
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

            EnsureSelectedAsset();
            if (selectedAsset == null || selectedSerializedObject == null)
            {
                EditorGUILayout.HelpBox("Could not create or load the selected Transition Settings asset.", MessageType.Error);
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
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(settingsProperty, includeChildren: true);
                EditorGUILayout.HelpBox(
                    TransitionSettings.DurationValidationMessage(selectedAsset.Settings.RunwayBeats, selectedAsset.Settings.TailBeats),
                    MessageType.Info);
                if (EditorGUI.EndChangeCheck())
                {
                    selectedSerializedObject.ApplyModifiedProperties();
                    if (TransitionSettingsAssetUtility.ConstrainDuration(selectedAsset.Settings))
                    {
                        selectedSerializedObject.Update();
                    }

                    EditorUtility.SetDirty(selectedAsset);
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

    private void SetSelectedTransitionIndex(int index)
    {
        selectedTransitionIndex = index;
        settingsScroll = Vector2.zero;
        selectedAsset = null;
        selectedSerializedObject = null;
        EnsureSelectedAsset();
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

    private void SavePendingSettingsAssets()
    {
        if (!settingsChangedSinceLastSave)
        {
            return;
        }

        if (selectedAsset != null && TransitionSettingsAssetUtility.ConstrainDuration(selectedAsset.Settings))
        {
            EditorUtility.SetDirty(selectedAsset);
        }

        AssetDatabase.SaveAssets();
        settingsChangedSinceLastSave = false;
    }

    private void EnsureSelectedAsset()
    {
        if (selectedAsset != null && selectedSerializedObject != null)
        {
            return;
        }

        var transition = CreateSelectedTransition();
        if (transition == null)
        {
            return;
        }

        selectedAsset = TransitionSettingsAssetUtility.EnsureAsset(
            transitionTypes[selectedTransitionIndex],
            transition.CodeDefaults);
        selectedSerializedObject = new SerializedObject(selectedAsset);
    }

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
