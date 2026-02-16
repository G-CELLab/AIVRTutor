using UnityEditor;
using UnityEngine;
using AI.Prompts;
using System.Reflection;
using System.Collections.Generic;

public class PromptLibraryViewer : EditorWindow
{
    private Vector2 scrollPosition;
    private string selectedPromptName = "";
    private string selectedPromptText = "";
    private List<string> promptNames = new List<string>();
    private int selectedPromptIndex = 0;

    [MenuItem("Window/Prompt Library Viewer")]
    public static void ShowWindow()
    {
        GetWindow<PromptLibraryViewer>("Prompt Library");
    }

    private void OnEnable()
    {
        LoadPromptNames();
    }

    private void LoadPromptNames()
    {
        promptNames.Clear();
        var properties = typeof(PromptLibrary).GetProperties(BindingFlags.Public | BindingFlags.Static);
        var fields = typeof(PromptLibrary).GetFields(BindingFlags.Public | BindingFlags.Static);

        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(string))
            {
                promptNames.Add(prop.Name);
            }
        }

        foreach (var field in fields)
        {
            if (field.FieldType == typeof(string))
            {
                promptNames.Add(field.Name);
            }
        }

        promptNames.Sort();

        if (promptNames.Count > 0 && string.IsNullOrEmpty(selectedPromptName))
        {
            SelectPrompt(0);
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Prompt Library Viewer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select a prompt to view and copy it to clipboard", MessageType.Info);

        EditorGUILayout.Space();

        // Prompt selector dropdown
        int newIndex = EditorGUILayout.Popup("Select Prompt", selectedPromptIndex, promptNames.ToArray());
        if (newIndex != selectedPromptIndex)
        {
            selectedPromptIndex = newIndex;
            SelectPrompt(newIndex);
        }

        EditorGUILayout.Space();

        // Display selected prompt
        if (!string.IsNullOrEmpty(selectedPromptText))
        {
            GUILayout.Label($"Prompt: {selectedPromptName}", EditorStyles.boldLabel);

            // Scrollable text area
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            GUILayout.TextArea(selectedPromptText, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Copy button
            if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(30)))
            {
                EditorGUIUtility.systemCopyBuffer = selectedPromptText;
                EditorUtility.DisplayDialog("Success", $"'{selectedPromptName}' copied to clipboard!", "OK");
            }

            EditorGUILayout.Space();

            // Paste to Phase Prompt Override
            if (GUILayout.Button("Paste to Phase Prompt Override", GUILayout.Height(30)))
            {
                var gptConnector = Object.FindFirstObjectByType<GPTConnector>();
                if (gptConnector != null)
                {
                    gptConnector.phasePromptOverride = selectedPromptText;
                    gptConnector.usePhasePromptOverride = true;
                    EditorUtility.SetDirty(gptConnector);
                    EditorUtility.DisplayDialog("Success", $"Pasted to GPTConnector Phase Prompt Override!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "No GPTConnector found in scene!", "OK");
                }
            }
        }

        // Refresh button
        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Prompts", GUILayout.Height(25)))
        {
            LoadPromptNames();
        }
    }

    private void SelectPrompt(int index)
    {
        if (index < 0 || index >= promptNames.Count)
            return;

        selectedPromptIndex = index;
        selectedPromptName = promptNames[index];

        // Get the prompt text via reflection
        var properties = typeof(PromptLibrary).GetProperties(BindingFlags.Public | BindingFlags.Static);
        var fields = typeof(PromptLibrary).GetFields(BindingFlags.Public | BindingFlags.Static);

        foreach (var prop in properties)
        {
            if (prop.Name == selectedPromptName && prop.PropertyType == typeof(string))
            {
                selectedPromptText = (string)prop.GetValue(null);
                return;
            }
        }

        foreach (var field in fields)
        {
            if (field.Name == selectedPromptName && field.FieldType == typeof(string))
            {
                selectedPromptText = (string)field.GetValue(null);
                return;
            }
        }

        selectedPromptText = "";
    }
}
