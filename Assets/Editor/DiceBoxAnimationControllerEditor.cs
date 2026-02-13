using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

#if UNITY_EDITOR
[CustomEditor(typeof(DiceBoxAnimationController))]
public class DiceBoxAnimationControllerEditor : Editor
{
    private SerializedProperty shakeSequenceProp;
    private SerializedProperty idleSequenceProp;
    private SerializedProperty zoomInSequenceProp;
    private SerializedProperty openingSequenceProp;
    private SerializedProperty closingSequenceProp;

    private SerializedProperty animationImageProp;
    private SerializedProperty diceBoxContainerProp;

    private SerializedProperty shakeDurationProp;
    private SerializedProperty idleDurationProp;
    private SerializedProperty zoomInDurationProp;
    private SerializedProperty openingDurationProp;
    private SerializedProperty holdOpenDurationProp;
    private SerializedProperty closingDurationProp;
    private SerializedProperty zoomOutDurationProp;

    private SerializedProperty diceVisibleAtOpeningFrameProp;
    private SerializedProperty diceHiddenAtClosingFrameProp;
    private SerializedProperty diceContainerProp;

    private bool showShakeSequence = true;
    private bool showIdleSequence = true;
    private bool showZoomInSequence = true;
    private bool showOpeningSequence = true;
    private bool showClosingSequence = true;

    private void OnEnable()
    {
        // Find all serialized properties
        shakeSequenceProp = serializedObject.FindProperty("shakeSequence");
        idleSequenceProp = serializedObject.FindProperty("idleSequence");
        zoomInSequenceProp = serializedObject.FindProperty("zoomInSequence");
        openingSequenceProp = serializedObject.FindProperty("openingSequence");
        closingSequenceProp = serializedObject.FindProperty("closingSequence");

        animationImageProp = serializedObject.FindProperty("animationImage");
        diceBoxContainerProp = serializedObject.FindProperty("diceBoxContainer");

        shakeDurationProp = serializedObject.FindProperty("shakeDuration");
        idleDurationProp = serializedObject.FindProperty("idleDuration");
        zoomInDurationProp = serializedObject.FindProperty("zoomInDuration");
        openingDurationProp = serializedObject.FindProperty("openingDuration");
        holdOpenDurationProp = serializedObject.FindProperty("holdOpenDuration");
        closingDurationProp = serializedObject.FindProperty("closingDuration");
        zoomOutDurationProp = serializedObject.FindProperty("zoomOutDuration");

        diceVisibleAtOpeningFrameProp = serializedObject.FindProperty("diceVisibleAtOpeningFrame");
        diceHiddenAtClosingFrameProp = serializedObject.FindProperty("diceHiddenAtClosingFrame");
        diceContainerProp = serializedObject.FindProperty("diceContainer");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Header
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dice Box Animation Controller", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use the folder buttons to automatically load sprite sequences from folders.", MessageType.Info);
        EditorGUILayout.Space();

        // UI References Section
        EditorGUILayout.LabelField("UI References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(animationImageProp, new GUIContent("Animation Image"));
        EditorGUILayout.PropertyField(diceBoxContainerProp, new GUIContent("Dice Box Container"));
        EditorGUILayout.Space();

        // Timing Configuration Section
        EditorGUILayout.LabelField("Timing Configuration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shakeDurationProp, new GUIContent("Shake Duration (seconds)"));
        EditorGUILayout.PropertyField(idleDurationProp, new GUIContent("Idle Duration (seconds)"));
        EditorGUILayout.PropertyField(zoomInDurationProp, new GUIContent("Zoom In Duration (seconds)"));
        EditorGUILayout.PropertyField(openingDurationProp, new GUIContent("Opening Duration (seconds)"));
        EditorGUILayout.PropertyField(holdOpenDurationProp, new GUIContent("Hold Open Duration (seconds)"));
        EditorGUILayout.PropertyField(closingDurationProp, new GUIContent("Closing Duration (seconds)"));
        EditorGUILayout.PropertyField(zoomOutDurationProp, new GUIContent("Zoom Out Duration (seconds)"));
        EditorGUILayout.Space();

        // Dice Control Section
        EditorGUILayout.LabelField("Dice Visibility Control", EditorStyles.boldLabel);

        // Dice Container field
        EditorGUILayout.PropertyField(diceContainerProp, new GUIContent("Dice Container"));

        if (diceContainerProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("⚠ Dice Container not assigned! Dice visibility control will not work.", MessageType.Warning);
        }

        EditorGUILayout.Space(5);

        // Show Dice Frame
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(diceVisibleAtOpeningFrameProp, new GUIContent("Show Dice at Opening Frame"));
        if (openingSequenceProp.arraySize > 0)
        {
            EditorGUILayout.LabelField($"(0-{openingSequenceProp.arraySize - 1})", GUILayout.Width(80));
        }
        EditorGUILayout.EndHorizontal();

        // Validate frame number
        if (openingSequenceProp.arraySize > 0 &&
            diceVisibleAtOpeningFrameProp.intValue >= openingSequenceProp.arraySize)
        {
            EditorGUILayout.HelpBox($"⚠ Frame {diceVisibleAtOpeningFrameProp.intValue} is beyond opening sequence length ({openingSequenceProp.arraySize} frames)", MessageType.Warning);
        }

        // Hide Dice Frame
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(diceHiddenAtClosingFrameProp, new GUIContent("Hide Dice at Closing Frame"));
        if (closingSequenceProp.arraySize > 0)
        {
            EditorGUILayout.LabelField($"(0-{closingSequenceProp.arraySize - 1})", GUILayout.Width(80));
        }
        EditorGUILayout.EndHorizontal();

        // Validate frame number
        if (closingSequenceProp.arraySize > 0 &&
            diceHiddenAtClosingFrameProp.intValue >= closingSequenceProp.arraySize)
        {
            EditorGUILayout.HelpBox($"⚠ Frame {diceHiddenAtClosingFrameProp.intValue} is beyond closing sequence length ({closingSequenceProp.arraySize} frames)", MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "Dice will become visible at the specified frame during the opening animation, " +
            "and will be hidden at the specified frame during the closing animation.",
            MessageType.Info);
        EditorGUILayout.Space();

        // Animation Sequences Section
        EditorGUILayout.LabelField("Animation Sequences", EditorStyles.boldLabel);

        // Shake Sequence
        DrawSequenceWithFolderButton("Shake Sequence", shakeSequenceProp, ref showShakeSequence);
        EditorGUILayout.Space();

        // Idle Sequence
        DrawSequenceWithFolderButton("Idle Sequence", idleSequenceProp, ref showIdleSequence);
        EditorGUILayout.Space();

        // Zoom In Sequence
        DrawSequenceWithFolderButton("Zoom In Sequence", zoomInSequenceProp, ref showZoomInSequence);
        EditorGUILayout.Space();

        // Opening Sequence
        DrawSequenceWithFolderButton("Opening Sequence", openingSequenceProp, ref showOpeningSequence);
        EditorGUILayout.Space();

        // Closing Sequence
        DrawSequenceWithFolderButton("Closing Sequence", closingSequenceProp, ref showClosingSequence);
        EditorGUILayout.Space();

        // Status Summary
        DrawStatusSummary();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStatusSummary()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Setup Status", EditorStyles.boldLabel);

        bool allGood = true;
        List<string> issues = new List<string>();

        // Check UI references
        if (animationImageProp.objectReferenceValue == null)
        {
            issues.Add("Animation Image not assigned");
            allGood = false;
        }

        if (diceContainerProp.objectReferenceValue == null)
        {
            issues.Add("Dice Container not assigned");
            allGood = false;
        }

        // Check sequences
        if (shakeSequenceProp.arraySize == 0) { issues.Add("Shake sequence empty"); allGood = false; }
        if (idleSequenceProp.arraySize == 0) { issues.Add("Idle sequence empty"); allGood = false; }
        if (zoomInSequenceProp.arraySize == 0) { issues.Add("Zoom In sequence empty"); allGood = false; }
        if (openingSequenceProp.arraySize == 0) { issues.Add("Opening sequence empty"); allGood = false; }
        if (closingSequenceProp.arraySize == 0) { issues.Add("Closing sequence empty"); allGood = false; }

        if (allGood)
        {
            EditorGUILayout.HelpBox("✅ All sequences loaded and references assigned!", MessageType.Info);

            // Calculate total animation time
            float totalTime = shakeDurationProp.floatValue +
                            zoomInDurationProp.floatValue +
                            openingDurationProp.floatValue +
                            holdOpenDurationProp.floatValue +
                            closingDurationProp.floatValue +
                            zoomOutDurationProp.floatValue;

            EditorGUILayout.HelpBox($"Total animation cycle time: {totalTime:F1} seconds (excluding idle loop)", MessageType.None);
        }
        else
        {
            string issueText = "⚠ Issues found:\n" + string.Join("\n", issues);
            EditorGUILayout.HelpBox(issueText, MessageType.Warning);
        }
    }

    private void DrawSequenceWithFolderButton(string label, SerializedProperty sequenceProp, ref bool foldout)
    {
        EditorGUILayout.BeginHorizontal();

        // Foldout with count
        int count = sequenceProp.arraySize;
        string labelWithCount = $"{label} ({count} frames)";
        foldout = EditorGUILayout.Foldout(foldout, labelWithCount, true);

        // Folder selection button
        if (GUILayout.Button("📁 Select Folder", GUILayout.Width(120)))
        {
            string folderPath = EditorUtility.OpenFolderPanel($"Select {label} Folder", "Assets", "");
            if (!string.IsNullOrEmpty(folderPath))
            {
                LoadSpritesFromFolder(folderPath, sequenceProp);
            }
        }

        // Clear button
        if (GUILayout.Button("Clear", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("Clear Sequence",
                $"Are you sure you want to clear {label}?", "Yes", "No"))
            {
                sequenceProp.ClearArray();
            }
        }

        EditorGUILayout.EndHorizontal();

        // Show sequence list if foldout is open
        if (foldout)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(sequenceProp, true);
            EditorGUI.indentLevel--;
        }
    }

    private void LoadSpritesFromFolder(string folderPath, SerializedProperty sequenceProp)
    {
        // Convert absolute path to relative path
        string relativePath = GetRelativePath(folderPath);

        if (string.IsNullOrEmpty(relativePath))
        {
            EditorUtility.DisplayDialog("Invalid Folder",
                "Please select a folder inside the Assets directory.", "OK");
            return;
        }

        // Find all sprite assets in the folder
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { relativePath });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("No Sprites Found",
                "No sprites were found in the selected folder. Make sure the folder contains sprite assets.", "OK");
            return;
        }

        // Load sprites and sort them by name
        List<Sprite> sprites = new List<Sprite>();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        // Sort sprites by name (assumes numerical naming like frame_001, frame_002, etc.)
        sprites = sprites.OrderBy(s => s.name).ToList();

        // Clear existing sequence and add new sprites
        sequenceProp.ClearArray();
        for (int i = 0; i < sprites.Count; i++)
        {
            sequenceProp.InsertArrayElementAtIndex(i);
            sequenceProp.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        serializedObject.ApplyModifiedProperties();

        Debug.Log($"[DiceBoxEditor] Loaded {sprites.Count} sprites from {relativePath}");
        EditorUtility.DisplayDialog("Sprites Loaded",
            $"Successfully loaded {sprites.Count} sprites!\n\nSprites are sorted alphabetically by name.", "OK");
    }

    private string GetRelativePath(string absolutePath)
    {
        // Get the Unity project path
        string projectPath = Application.dataPath.Replace("/Assets", "");

        // Check if the path is within the project
        if (!absolutePath.StartsWith(projectPath))
        {
            return null;
        }

        // Convert to relative path starting with "Assets"
        string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
        return relativePath;
    }
}
#endif