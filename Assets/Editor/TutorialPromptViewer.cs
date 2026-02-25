using UnityEditor;
using UnityEngine;
using AI.Prompts;

public class TutorialPromptViewer : EditorWindow
{
    private Vector2 scrollPosition = Vector2.zero;
    private int selectedPromptIndex = 0;
    
    private string[] promptNames = new string[]
    {
        "Tutorial",
        "Tutorial: Content Questions",
        "Tutorial: Visual Reference Questions",
        "Tutorial: Manipulation Questions"
    };

    private string[] promptTexts;

    [MenuItem("Window/AI/Tutorial Prompt Viewer")]
    public static void ShowWindow()
    {
        GetWindow<TutorialPromptViewer>("Tutorial Prompts");
    }

    private void OnEnable()
    {
        LoadPrompts();
    }

    private void LoadPrompts()
    {
        promptTexts = new string[]
        {
            TutorialPrompts.Tutorial,
            TutorialPrompts.TutorialContentQuestions,
            TutorialPrompts.TutorialVisualQuestions,
            TutorialPrompts.TutorialManipulationQuestions
        };
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Tutorial Prompt Library", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Prompt selection buttons
        EditorGUILayout.LabelField("Select Prompt:", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        for (int i = 0; i < promptNames.Length; i++)
        {
            if (GUILayout.Button(promptNames[i], EditorStyles.toolbarButton))
            {
                selectedPromptIndex = i;
            }
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(promptNames[selectedPromptIndex], EditorStyles.boldLabel);
        EditorGUILayout.Separator();

        // Display prompt text
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.TextArea(promptTexts[selectedPromptIndex], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        
        // Copy button
        if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(30)))
        {
            EditorGUIUtility.systemCopyBuffer = promptTexts[selectedPromptIndex];
            EditorUtility.DisplayDialog("Success", "Prompt copied to clipboard!", "OK");
        }
    }
}
