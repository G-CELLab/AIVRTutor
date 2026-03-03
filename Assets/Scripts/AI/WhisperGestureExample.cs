using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Example script demonstrating Whisper gesture synchronization.
/// Attach to a test GameObject to try different phrases and timing.
/// </summary>
public class WhisperGestureExample : MonoBehaviour
{
    [Header("Test Configuration")]
    [Tooltip("Test audio clips with different phrases")]
    public AudioClip[] testClips;
    
    [Header("Components (Auto-found)")]
    public WhisperGestureSync gestureSync;
    public TTSAnimatorDriver animatorDriver;
    public GPTConnector gptConnector;
    
    [Header("UI (Optional)")]
    public Button testButton;
    public Text statusText;
    public Dropdown clipDropdown;
    
    [Header("Test Settings")]
    [Tooltip("Which test clip to use (if testClips array is populated)")]
    public int currentClipIndex = 0;
    
    [Tooltip("Test phrases for each phase (used if no audio clips provided)")]
    [TextArea(2, 4)]
    public string interphaseTestPhrase = "The cell needs to eat food to get energy for growth.";
     
    [TextArea(2, 4)]
    public string prophaseTestPhrase = "The chromosomes condense into an X-shape during prophase.";
    
    [TextArea(2, 4)]
    public string metaphaseTestPhrase = "The chromosomes line up at the center of the cell in a row.";
    
    [TextArea(2, 4)]
    public string anaphaseTestPhrase = "The sister chromatids move to opposite ends and pull apart.";
    
    [TextArea(2, 4)]
    public string telophaseTestPhrase = "The cell splits and divides into two daughter cells.";
    
    void Start()
    {
        // Auto-find components
        if (!gestureSync) gestureSync = FindAnyObjectByType<WhisperGestureSync>();
        if (!animatorDriver) animatorDriver = FindAnyObjectByType<TTSAnimatorDriver>();
        if (!gptConnector) gptConnector = FindAnyObjectByType<GPTConnector>();
        
        // Setup UI
        if (testButton) testButton.onClick.AddListener(RunTest);
        if (clipDropdown) SetupDropdown();
        
        UpdateStatus("Ready to test");
    }
    
    void SetupDropdown()
    {
        if (testClips == null || testClips.Length == 0) return;
        
        clipDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>();
        
        foreach (var clip in testClips)
        {
            options.Add(clip != null ? clip.name : "None");
        }
        
        clipDropdown.AddOptions(options);
        clipDropdown.value = currentClipIndex;
        clipDropdown.onValueChanged.AddListener(OnClipSelected);
    }
    
    void OnClipSelected(int index)
    {
        currentClipIndex = Mathf.Clamp(index, 0, testClips.Length - 1);
        UpdateStatus($"Selected: {testClips[currentClipIndex]?.name ?? "None"}");
    }
    
    /// <summary>
    /// Run a test transcription with the current settings
    /// </summary>
    [ContextMenu("Run Test")]
    public void RunTest()
    {
        if (!gestureSync)
        {
            Debug.LogError("[WhisperExample] WhisperGestureSync not found!");
            UpdateStatus("Error: WhisperGestureSync not found");
            return;
        }
        
        // Use test clip if available
        if (testClips != null && currentClipIndex < testClips.Length && testClips[currentClipIndex] != null)
        {
            AudioClip clip = testClips[currentClipIndex];
            UpdateStatus($"Testing with clip: {clip.name}");
            gestureSync.TranscribeAndScheduleGestures(clip);
        }
        else
        {
            // Generate TTS from current phase phrase (if TTS player available)
            string testPhrase = GetCurrentPhaseTestPhrase();
            UpdateStatus($"Testing with phrase: {testPhrase.Substring(0, Mathf.Min(50, testPhrase.Length))}...");
            
            // Note: This requires integration with your TTS system
            Debug.Log($"[WhisperExample] Test phrase (current phase): {testPhrase}");
            Debug.LogWarning("[WhisperExample] No test audio clip provided. Add audio clips or integrate with TTS.");
        }
    }
    
    string GetCurrentPhaseTestPhrase()
    {
        switch (GameManager.eGameStatus)
        {
            case GameManager.GameState.Interphase:
                return interphaseTestPhrase;
            case GameManager.GameState.Prophase:
                return prophaseTestPhrase;
            case GameManager.GameState.Metaphase:
                return metaphaseTestPhrase;
            case GameManager.GameState.Anaphase:
                return anaphaseTestPhrase;
            case GameManager.GameState.Telophase:
                return telophaseTestPhrase;
            default:
                return "Test phrase for current phase.";
        }
    }
    
    /// <summary>
    /// Test specific gesture trigger directly (bypass Whisper)
    /// </summary>
    [ContextMenu("Test Direct Gesture - DZ13 (Eat)")]
    public void TestDZ13()
    {
        if (animatorDriver) animatorDriver.TriggerDZ13();
        UpdateStatus("Triggered: DZ13 (Eat)");
    }
    
    [ContextMenu("Test Direct Gesture - DZ18 (X-Shape)")]
    public void TestDZ18()
    {
        if (animatorDriver) animatorDriver.TriggerDZ18();
        UpdateStatus("Triggered: DZ18 (X-Shape)");
    }
    
    [ContextMenu("Test Direct Gesture - DZ20 (Line Up)")]
    public void TestDZ20()
    {
        if (animatorDriver) animatorDriver.TriggerDZ20();
        UpdateStatus("Triggered: DZ20 (Line Up)");
    }
    
    [ContextMenu("Test Direct Gesture - DZ22 (Split Outward)")]
    public void TestDZ22()
    {
        if (animatorDriver) animatorDriver.TriggerDZ22();
        UpdateStatus("Triggered: DZ22 (Split Outward)");
    }
    
    /// <summary>
    /// Test all phase gestures in sequence
    /// </summary>
    [ContextMenu("Test All Gestures Sequence")]
    public void TestAllGesturesSequence()
    {
        StartCoroutine(TestGestureSequence());
    }
    
    System.Collections.IEnumerator TestGestureSequence()
    {
        if (!animatorDriver)
        {
            Debug.LogError("[WhisperExample] No animator driver");
            yield break;
        }
        
        UpdateStatus("Testing gesture sequence...");
        
        yield return new WaitForSeconds(1f);
        animatorDriver.TriggerDZ13();
        UpdateStatus("DZ13 (Eat)");
        
        yield return new WaitForSeconds(2f);
        animatorDriver.TriggerDZ18();
        UpdateStatus("DZ18 (X-Shape)");
        
        yield return new WaitForSeconds(2f);
        animatorDriver.TriggerDZ20();
        UpdateStatus("DZ20 (Line Up)");
        
        yield return new WaitForSeconds(2f);
        animatorDriver.TriggerDZ22();
        UpdateStatus("DZ22 (Split)");
        
        yield return new WaitForSeconds(2f);
        UpdateStatus("Sequence complete");
    }
    
    /// <summary>
    /// Stop any ongoing gesture playback
    /// </summary>
    [ContextMenu("Stop Gesture Playback")]
    public void StopPlayback()
    {
        if (gestureSync) gestureSync.StopGesturePlayback();
        UpdateStatus("Playback stopped");
    }
    
    void UpdateStatus(string message)
    {
        if (statusText) statusText.text = message;
        Debug.Log($"[WhisperExample] {message}");
    }
    
    void Update()
    {
        // Keyboard shortcuts for testing
        if (Input.GetKeyDown(KeyCode.T))
        {
            RunTest();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TestDZ13();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TestDZ18();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TestDZ20();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            TestDZ22();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            StopPlayback();
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            TestAllGesturesSequence();
        }
    }
    
    void OnGUI()
    {
        // Simple on-screen controls if no UI is set up
        if (testButton != null) return; // Skip if UI exists
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Label("Whisper Gesture Test Controls", GUI.skin.box);
        
        if (GUILayout.Button("Run Test (T)"))
        {
            RunTest();
        }
        
        GUILayout.Space(10);
        GUILayout.Label("Direct Gesture Tests:");
        
        if (GUILayout.Button("DZ13 - Eat (1)"))
        {
            TestDZ13();
        }
        
        if (GUILayout.Button("DZ18 - X-Shape (2)"))
        {
            TestDZ18();
        }
        
        if (GUILayout.Button("DZ20 - Line Up (3)"))
        {
            TestDZ20();
        }
        
        if (GUILayout.Button("DZ22 - Split (4)"))
        {
            TestDZ22();
        }
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Test All Sequence (A)"))
        {
            TestAllGesturesSequence();
        }
        
        if (GUILayout.Button("Stop Playback (S)"))
        {
            StopPlayback();
        }
        
        GUILayout.Space(10);
        GUILayout.Label($"Current Phase: {GameManager.eGameStatus}");
        
        GUILayout.EndArea();
    }
}
