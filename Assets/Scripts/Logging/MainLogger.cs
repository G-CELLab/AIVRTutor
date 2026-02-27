using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Comprehensive logging system that tracks:
/// - Gestures (left/right pinch)
/// - Touches (what objects hands are touching)
/// - Info Panel state (which tutorial panel is active)
/// - Phase changes (game phase transitions)
/// - Other events (wound trigger, ATP charged, etc.)
/// 
/// Logs to console and MainLog.csv with columns:
/// Time, Left Gesture, Right Gesture, Left Touch, Right Touch, Info Panel, Phase, Other
/// </summary>
public class MainLogger : MonoBehaviour
{
    [Header("Hand References")]
    [SerializeField] private LeftHandManager leftHandManager;
    [SerializeField] private RightHandManager rightHandManager;
    
    [Header("Game References")]
    [SerializeField] private GameManager gameManager;
    
    [Header("Logging Settings")]
    [SerializeField] private float loggingInterval = 0.1f;
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private bool logToCSV = true;
    
    // CSV and timing
    private string csvFilePath;
    private float timeSinceLastFrameLog = 0f;
    private float sessionStartTime;

    // Keep one time anchor across cycle file rollovers/re-creations
    private static bool hasGlobalSessionStartTime;
    private static float globalSessionStartTime;
    
    // Cycle tracking
    private int currentCycle = 0;
    private int lastCycle = -1;
    
    // Event queue for "Other" column
    private Queue<string> otherEventQueue = new Queue<string>();
    
    public static MainLogger instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Start()
    {
        Debug.Log("[MainLogger] START called");
        if (!hasGlobalSessionStartTime)
        {
            globalSessionStartTime = Time.time;
            hasGlobalSessionStartTime = true;
        }
        sessionStartTime = globalSessionStartTime;
        
        // Validate references
        if (leftHandManager == null)
        {
            Debug.LogError("[MainLogger] LeftHandManager not assigned!");
            enabled = false;
            return;
        }
        if (rightHandManager == null)
        {
            Debug.LogError("[MainLogger] RightHandManager not assigned!");
            enabled = false;
            return;
        }
        
        Debug.Log("[MainLogger] Found Left Hand Manager");
        Debug.Log("[MainLogger] Found Right Hand Manager");

        // Setup initial CSV file path
        if (logToCSV)
        {
            currentCycle = GameManager.GetHealingCycleCount();
            lastCycle = currentCycle;
            InitializeCSVFile();
        }

        Debug.Log("[MainLogger] Initialized. Logging to: " + csvFilePath);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            hasGlobalSessionStartTime = false;
            globalSessionStartTime = 0f;
        }
    }

    private void Update()
    {
        // Check if cycle changed and create new CSV file if needed
        currentCycle = GameManager.GetHealingCycleCount();
        if (currentCycle != lastCycle && currentCycle < GameManager.MAX_HEALING_CYCLES)
        {
            lastCycle = currentCycle;
            if (logToCSV)
            {
                InitializeCSVFile();
                Debug.Log($"[MainLogger] Started new cycle {currentCycle + 1}");
            }
        }
        
        timeSinceLastFrameLog += Time.deltaTime;

        if (timeSinceLastFrameLog >= loggingInterval)
        {
            LogFrame();
            timeSinceLastFrameLog = 0f;
        }
    }

    private void LogFrame()
    {
        float elapsed = Time.time - sessionStartTime;
        
        // Get current states
        string leftGesture = GetLeftGesture();
        string rightGesture = GetRightGesture();
        string leftTouch = GetLeftTouch();
        string rightTouch = GetRightTouch();
        string phase = GetCurrentPhase();
        string aiSpeech = GetAISpeech();
        string otherEvent = otherEventQueue.Count > 0 ? otherEventQueue.Dequeue() : "";
        
        // Console logging
        if (logToConsole)
        {
            Debug.Log($"[MainLog] T={elapsed:F2}s | L_Ges:{leftGesture} | R_Ges:{rightGesture} | L_Touch:{leftTouch} | R_Touch:{rightTouch} | Phase:{phase} | AI:{aiSpeech} | Other:{otherEvent}");
        }

        // CSV logging - every frame
        if (logToCSV)
        {
            WriteToCSV(elapsed, leftGesture, rightGesture, leftTouch, rightTouch, phase, aiSpeech, otherEvent);
        }

        // Clear AI speech after logging so it only appears once
        if (!string.IsNullOrEmpty(aiSpeech))
        {
            TextToSpeechPlayer.ClearCurrentSpeech();
        }
    }

    private string GetLeftGesture()
    {
        if (leftHandManager == null || !leftHandManager.isGrabbed_left)
            return "";
        return "Left_Pinch";
    }

    private string GetRightGesture()
    {
        if (rightHandManager == null || !rightHandManager.isGrabbed_right)
            return "";
        return "Right_Pinch";
    }

    private string GetLeftTouch()
    {
        // Get from TouchTracker - will be implemented separately
        return TouchTracker.GetLeftTouchObject();
    }

    private string GetRightTouch()
    {
        // Get from TouchTracker - will be implemented separately
        return TouchTracker.GetRightTouchObject();
    }

    private string GetCurrentPhase()
    {
        if (gameManager == null)
            return "";
        
        return GameManager.eGameStatus.ToString();
    }

    private string GetAISpeech()
    {
        string speech = TextToSpeechPlayer.GetCurrentSpeech();
        if (string.IsNullOrEmpty(speech))
            return "";

        if (ShouldIgnoreTutorialSpeech(speech))
            return "";

        return speech;
    }

    private bool ShouldIgnoreTutorialSpeech(string speech)
    {
        if (string.IsNullOrEmpty(speech))
            return false;

        string normalized = speech.Trim().ToLowerInvariant();

        return normalized.Contains("let's start by touching and holding the sphere") ||
               normalized.Contains("im your ai tutor, here to guide you") ||
               normalized.Contains("i'm your ai tutor, here to guide you");
    }

    private void InitializeCSVFile()
    {
        try
        {
            // Create filename with cycle number (1-indexed for user readability)
            string cycleNumber = (currentCycle + 1).ToString();
            csvFilePath = Path.Combine(Application.persistentDataPath, $"MainLog_Cycle{cycleNumber}.csv");
            
            string directory = Path.GetDirectoryName(csvFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = new StreamWriter(csvFilePath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Time(s),Left_Gesture,Right_Gesture,Left_Touch,Right_Touch,Phase,AI_Speech,Other");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainLogger] Failed to initialize CSV: " + ex.Message);
        }
    }

    private void WriteToCSV(float elapsed, string leftGesture, string rightGesture, string leftTouch, 
                           string rightTouch, string phase, string aiSpeech, string otherEvent)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, true, new UTF8Encoding(true)))
            {
                writer.WriteLine(
                    $"{elapsed:F3}," +
                    $"{EscapeCsvField(leftGesture)}," +
                    $"{EscapeCsvField(rightGesture)}," +
                    $"{EscapeCsvField(leftTouch)}," +
                    $"{EscapeCsvField(rightTouch)}," +
                    $"{EscapeCsvField(phase)}," +
                    $"{EscapeCsvField(aiSpeech)}," +
                    $"{EscapeCsvField(otherEvent)}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainLogger] Failed to write to CSV: " + ex.Message);
        }
    }

    private string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    /// <summary>
    /// Public method to log "Other" events
    /// </summary>
    public static void LogOtherEvent(string eventName)
    {
        if (instance != null)
        {
            instance.otherEventQueue.Enqueue(eventName);
        }
    }

    public string GetCSVFilePath()
    {
        return csvFilePath;
    }
}
