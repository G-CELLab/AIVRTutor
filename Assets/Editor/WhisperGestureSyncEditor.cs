#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Custom Editor for WhisperGestureSync - provides enhanced Inspector UI and testing tools
/// </summary>
[CustomEditor(typeof(WhisperGestureSync))]
public class WhisperGestureSyncEditor : Editor
{
    private SerializedProperty whisperModelPath;
    private SerializedProperty language;
    private SerializedProperty enableWordTimestamps;
    private SerializedProperty animatorDriver;
    private SerializedProperty gptConnector;
    private SerializedProperty ttsAudioSource;
    private SerializedProperty gestureTimingOffset;
    private SerializedProperty confidenceThreshold;
    private SerializedProperty enablePhaseGestures;
    private SerializedProperty verboseLogging;
    private SerializedProperty logWordTimestamps;
    
    private AudioClip testClip;
    private bool showTestingTools = true;
    private bool showAdvancedSettings = false;
    
    void OnEnable()
    {
        // Cache serialized properties
        whisperModelPath = serializedObject.FindProperty("whisperModelPath");
        language = serializedObject.FindProperty("language");
        enableWordTimestamps = serializedObject.FindProperty("enableWordTimestamps");
        animatorDriver = serializedObject.FindProperty("animatorDriver");
        gptConnector = serializedObject.FindProperty("gptConnector");
        ttsAudioSource = serializedObject.FindProperty("ttsAudioSource");
        gestureTimingOffset = serializedObject.FindProperty("gestureTimingOffset");
        confidenceThreshold = serializedObject.FindProperty("confidenceThreshold");
        enablePhaseGestures = serializedObject.FindProperty("enablePhaseGestures");
        verboseLogging = serializedObject.FindProperty("verboseLogging");
        logWordTimestamps = serializedObject.FindProperty("logWordTimestamps");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        WhisperGestureSync sync = (WhisperGestureSync)target;
        
        // Header
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Whisper Gesture Synchronization", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Automatically synchronizes AI agent gestures with speech using Whisper word-level timestamps.", MessageType.Info);
        
        // Setup Check
        DrawSetupCheck(sync);
        
        EditorGUILayout.Space();
        
        // Whisper Configuration
        EditorGUILayout.LabelField("Whisper Configuration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(whisperModelPath, new GUIContent("Model Path", "Path to Whisper .bin model file"));
        
        // Quick setup button
        if (GUILayout.Button("📁 Select Whisper Model", GUILayout.Height(25)))
        {
            string path = EditorUtility.OpenFilePanel("Select Whisper Model", Application.dataPath, "bin");
            if (!string.IsNullOrEmpty(path))
            {
                string normalizedSelected = path.Replace('\\', '/');
                string normalizedStreaming = Application.streamingAssetsPath.Replace('\\', '/');

                if (normalizedSelected.StartsWith(normalizedStreaming + "/", System.StringComparison.OrdinalIgnoreCase))
                {
                    path = normalizedSelected.Substring(normalizedStreaming.Length + 1);
                }
                else if (normalizedSelected.StartsWith(Application.dataPath.Replace('\\', '/') + "/", System.StringComparison.OrdinalIgnoreCase))
                {
                    path = "Assets" + normalizedSelected.Substring(Application.dataPath.Replace('\\', '/').Length);
                }
                whisperModelPath.stringValue = path;
            }
        }
        
        EditorGUILayout.PropertyField(language, new GUIContent("Language", "Leave empty for auto-detect, or use 'en' for English"));
        EditorGUILayout.PropertyField(enableWordTimestamps, new GUIContent("Enable Word Timestamps", "Required for precise gesture timing"));
        
        EditorGUILayout.Space();
        
        // Component References
        EditorGUILayout.LabelField("Component References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(animatorDriver, new GUIContent("Animator Driver", "TTSAnimatorDriver that controls gestures"));
        EditorGUILayout.PropertyField(gptConnector, new GUIContent("GPT Connector", "For phase awareness"));
        EditorGUILayout.PropertyField(ttsAudioSource, new GUIContent("TTS Audio Source", "AudioSource playing TTS audio"));
        
        // Auto-find button
        if (GUILayout.Button("🔍 Auto-Find Components", GUILayout.Height(25)))
        {
            if (animatorDriver.objectReferenceValue == null)
                animatorDriver.objectReferenceValue = GameObject.FindAnyObjectByType<TTSAnimatorDriver>();
            if (gptConnector.objectReferenceValue == null)
                gptConnector.objectReferenceValue = GameObject.FindAnyObjectByType<GPTConnector>();
        }
        
        EditorGUILayout.Space();
        
        // Timing Settings
        EditorGUILayout.LabelField("Timing Adjustments", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gestureTimingOffset, new GUIContent("Gesture Timing Offset", "Global timing offset (positive = delay, negative = earlier)"));
        EditorGUILayout.HelpBox("Typical values: 0.0 to 0.3 seconds. Adjust if gestures are out of sync.", MessageType.None);
        
        EditorGUILayout.Space();
        
        // Gesture Control
        EditorGUILayout.LabelField("Gesture Control", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enablePhaseGestures, new GUIContent("Enable Phase Gestures", "Use phase-specific gesture mappings"));
        EditorGUILayout.PropertyField(confidenceThreshold, new GUIContent("Confidence Threshold", "Minimum confidence for word detection (0-1)"));
        
        EditorGUILayout.Space();
        
        // Debug Settings
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(verboseLogging, new GUIContent("Verbose Logging", "Enable detailed console logs"));
        EditorGUILayout.PropertyField(logWordTimestamps, new GUIContent("Log Word Timestamps", "Log individual word timestamps from Whisper"));
        
        // Advanced Settings (collapsible)
        EditorGUILayout.Space();
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
        if (showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox("Coming soon: Custom gesture curves, multi-language support, streaming mode", MessageType.Info);
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        
        // Testing Tools
        showTestingTools = EditorGUILayout.Foldout(showTestingTools, "🧪 Testing Tools", true);
        if (showTestingTools)
        {
            EditorGUI.indentLevel++;
            DrawTestingTools(sync);
            EditorGUI.indentLevel--;
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    void DrawSetupCheck(WhisperGestureSync sync)
    {
        string resolvedModelPath = ResolveModelFilePath(sync.whisperModelPath);
        bool modelExists = !string.IsNullOrEmpty(resolvedModelPath) && File.Exists(resolvedModelPath);
        bool hasAnimator = sync.animatorDriver != null;
        bool hasGPT = sync.gptConnector != null;
        
        bool allGood = modelExists && hasAnimator && hasGPT;
        
        if (allGood)
        {
            EditorGUILayout.HelpBox("✅ Setup Complete! Ready to use.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("⚠️ Setup Incomplete", MessageType.Warning);
            
            if (!modelExists)
                EditorGUILayout.HelpBox("❌ Whisper model not found. Download and place in project.", MessageType.Error);
            else
                EditorGUILayout.HelpBox($"✅ Model found: {resolvedModelPath}", MessageType.None);
            if (!hasAnimator)
                EditorGUILayout.HelpBox("❌ TTSAnimatorDriver not assigned. Click Auto-Find or assign manually.", MessageType.Error);
            if (!hasGPT)
                EditorGUILayout.HelpBox("⚠️ GPTConnector not assigned. Phase detection won't work.", MessageType.Warning);
        }
    }

    string ResolveModelFilePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return null;

        string normalized = configuredPath.Replace('\\', '/').Trim();

        if (Path.IsPathRooted(normalized))
            return normalized;

        const string assetsStreamingPrefix = "Assets/StreamingAssets/";
        if (normalized.StartsWith(assetsStreamingPrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            string suffix = normalized.Substring(assetsStreamingPrefix.Length);
            return Path.Combine(Application.streamingAssetsPath, suffix).Replace('\\', '/');
        }

        if (normalized.StartsWith("Whisper/", System.StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Application.streamingAssetsPath, normalized).Replace('\\', '/');

        if (normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, normalized).Replace('\\', '/');
        }

        return Path.Combine(Application.streamingAssetsPath, normalized).Replace('\\', '/');
    }
    
    void DrawTestingTools(WhisperGestureSync sync)
    {
        EditorGUILayout.LabelField("Quick Test Audio", EditorStyles.boldLabel);
        testClip = (AudioClip)EditorGUILayout.ObjectField("Test Audio Clip", testClip, typeof(AudioClip), false);
        
        EditorGUI.BeginDisabledGroup(!Application.isPlaying || testClip == null);
        if (GUILayout.Button("▶️ Test Transcription", GUILayout.Height(30)))
        {
            if (Application.isPlaying && testClip != null)
            {
                sync.TranscribeAndScheduleGestures(testClip);
                Debug.Log($"[Editor] Testing transcription with clip: {testClip.name}");
            }
        }
        EditorGUI.EndDisabledGroup();
        
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test transcription", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        
        // Direct gesture testing
        EditorGUILayout.LabelField("Direct Gesture Tests (Bypass Whisper)", EditorStyles.boldLabel);
        
        EditorGUI.BeginDisabledGroup(!Application.isPlaying || sync.animatorDriver == null);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("DZ13\n(Eat)", GUILayout.Height(40)))
        {
            if (Application.isPlaying) sync.animatorDriver?.TriggerDZ13();
        }
        if (GUILayout.Button("DZ18\n(X-Shape)", GUILayout.Height(40)))
        {
            if (Application.isPlaying) sync.animatorDriver?.TriggerDZ18();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("DZ12\n(Line Up)", GUILayout.Height(40)))
        {
            if (Application.isPlaying) sync.animatorDriver?.TriggerDZ12();
        }
        if (GUILayout.Button("DZ22\n(Split)", GUILayout.Height(40)))
        {
            if (Application.isPlaying) sync.animatorDriver?.TriggerDZ22();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space();
        
        // Playback control
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("⏹️ Stop Gesture Playback", GUILayout.Height(25)))
        {
            if (Application.isPlaying)
            {
                sync.StopGesturePlayback();
                Debug.Log("[Editor] Stopped gesture playback");
            }
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space();
        
        // Phase info
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox($"Current Phase: {GameManager.eGameStatus}", MessageType.None);
        }
    }
}
#endif
