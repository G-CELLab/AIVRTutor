using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Logs left and right hand positions to console and CSV file.
/// Tracks the actual XR hand objects directly from the XROrigin.
/// </summary>
public class HandPositionLogger : MonoBehaviour
{
    [Header("Hand References")]
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;
    
    [Header("Logging Settings")]
    [SerializeField] private float loggingInterval = 0.1f; // Log every 0.1 seconds
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private bool logToCSV = true;
    
    private string csvFilePath;
    private float timeSinceLastLog = 0f;
    private float sessionStartTime;
    
    // Cycle tracking
    private int currentCycle = 0;
    private int lastCycle = -1;

    private void Start()
    {
        Debug.Log("[HandPositionLogger] START called");
        
        if (leftHandTransform == null || rightHandTransform == null)
        {
            Debug.LogError("[HandPositionLogger] Left Hand Transform or Right Hand Transform not assigned in Inspector!");
            enabled = false;
            return;
        }
        
        sessionStartTime = Time.time;
        
        Debug.Log("[HandPositionLogger] Found Left Hand: " + leftHandTransform.name);
        Debug.Log("[HandPositionLogger] Found Right Hand: " + rightHandTransform.name);

        // Setup initial CSV file path
        if (logToCSV)
        {
            currentCycle = GameManager.GetHealingCycleCount();
            lastCycle = currentCycle;
            InitializeCSVFile();
        }

        Debug.Log("[HandPositionLogger] Initialized. Logging to: " + csvFilePath);
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
                Debug.Log($"[HandPositionLogger] Started new cycle {currentCycle + 1}");
            }
        }
        
        timeSinceLastLog += Time.deltaTime;

        if (timeSinceLastLog >= loggingInterval)
        {
            LogHandPositions();
            timeSinceLastLog = 0f;
        }
    }

    private void LogHandPositions()
    {
        if (leftHandTransform == null || rightHandTransform == null)
            return;

        float elapsed = Time.time - sessionStartTime;

        // Get hand positions
        Vector3 leftPos = leftHandTransform.position;
        Vector3 rightPos = rightHandTransform.position;

        // Console logging
        if (logToConsole)
        {
            Debug.Log($"[Hand Position] T={elapsed:F2}s | Left:({leftPos.x:F3},{leftPos.y:F3},{leftPos.z:F3}) | Right:({rightPos.x:F3},{rightPos.y:F3},{rightPos.z:F3})");
        }

        // CSV logging
        if (logToCSV)
        {
            WriteToCSV(elapsed, leftPos, rightPos);
        }
    }

    private void InitializeCSVFile()
    {
        try
        {
            // Create filename with cycle number (1-indexed for user readability)
            string cycleNumber = (currentCycle + 1).ToString();
            csvFilePath = Path.Combine(Application.persistentDataPath, $"Hand_Position_Cycle{cycleNumber}.csv");
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(csvFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Write header
            using (StreamWriter writer = new StreamWriter(csvFilePath, false))
            {
                writer.WriteLine("Time(s),LeftX,LeftY,LeftZ,RightX,RightY,RightZ");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[HandPositionLogger] Failed to initialize CSV: " + ex.Message);
        }
    }

    private void WriteToCSV(float elapsed, Vector3 leftPos, Vector3 rightPos)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, true))
            {
                writer.WriteLine($"{elapsed:F3},{leftPos.x:F6},{leftPos.y:F6},{leftPos.z:F6},{rightPos.x:F6},{rightPos.y:F6},{rightPos.z:F6}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[HandPositionLogger] Failed to write to CSV: " + ex.Message);
        }
    }

    public void SetLoggingInterval(float interval)
    {
        loggingInterval = Mathf.Max(0.01f, interval); // Minimum 0.01s
    }

    public string GetCSVFilePath()
    {
        return csvFilePath;
    }
    
    private Transform FindHandByName(string handName)
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        
        // Search for exact match first
        foreach (Transform t in allTransforms)
        {
            if (t.name == handName)
            {
                Debug.Log("[HandPositionLogger] Found exact match: " + t.name);
                return t;
            }
        }
        
        Debug.Log("[HandPositionLogger] No exact match for '" + handName + "'");
        return null;
    }
}
