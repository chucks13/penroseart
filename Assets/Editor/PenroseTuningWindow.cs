using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Standalone authoring window for Penrose runtime tuning. The Transitions tab edits saved Transition Settings;
/// Play Mode steering is wired in a later slice.
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
        ReloadTransitions();
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
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTransitionList();
            DrawSelectedTransitionSettings();
        }
    }

    private void DrawTransitionList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(240f)))
        {
            EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edit Mode settings authoring. Play Mode steering is added in the next slice.", MessageType.None);

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

                    if (GUILayout.Button(transitionNames[i], EditorStyles.miniButton))
                    {
                        SelectTransition(i);
                        GUI.FocusControl(null);
                    }

                    GUI.backgroundColor = previousColor;
                }
            }
        }
    }

    private void DrawSelectedTransitionSettings()
    {
        using (new EditorGUILayout.VerticalScope())
        {
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
                if (EditorGUI.EndChangeCheck())
                {
                    selectedSerializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(selectedAsset);
                }
            }
        }
    }

    private void DrawEffectsTab()
    {
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Effects tuning will follow the same selection and Hold Selected pattern later. " +
            "This slice only implements Transition Settings authoring.",
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

        selectedTransitionIndex = Mathf.Clamp(selectedTransitionIndex, 0, transitionTypes.Length - 1);
        SelectTransition(selectedTransitionIndex);
    }

    private void SelectTransition(int index)
    {
        selectedTransitionIndex = index;
        settingsScroll = Vector2.zero;
        selectedAsset = null;
        selectedSerializedObject = null;
        EnsureSelectedAsset();
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
