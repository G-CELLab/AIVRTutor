using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Logs player head position and what the player is looking at via raycast.
/// 
/// Logs to console and Player_Position.csv with columns:
/// Time(s), XPos, YPos, ZPos, Raycast
/// </summary>
public class PlayerPositionLogger : MonoBehaviour
{
    [Header("Player Head Reference")]
    [SerializeField] private Transform playerHeadTransform;
    
    [Header("Raycast Settings")]
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private LayerMask raycastLayerMask = -1; // All layers
    
    [Header("Logging Settings")]
    [SerializeField] private float loggingInterval = 0.1f;
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private bool logToCSV = true;

    // CSV and timing
    private string csvFilePath;
    private float timeSinceLastLog = 0f;
    private float sessionStartTime;
    
    // Cycle tracking
    private int currentCycle = 0;
    private int lastCycle = -1;

    private void Start()
    {
        Debug.Log("[PlayerPositionLogger] START called");
        sessionStartTime = Time.time;

        // Auto-find player head if not assigned
        if (playerHeadTransform == null)
        {
            GameObject mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCameraObj != null)
            {
                playerHeadTransform = mainCameraObj.transform;
                Debug.Log("[PlayerPositionLogger] Auto-found Main Camera");
            }
            else
            {
                Debug.LogError("[PlayerPositionLogger] Player Head Transform not assigned and Main Camera not found!");
                enabled = false;
                return;
            }
        }

        // Setup initial CSV file path
        if (logToCSV)
        {
            currentCycle = GameManager.GetHealingCycleCount();
            lastCycle = currentCycle;
            InitializeCSVFile();
        }

        Debug.Log("[PlayerPositionLogger] Initialized. Logging to: " + csvFilePath);
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
                Debug.Log($"[PlayerPositionLogger] Started new cycle {currentCycle + 1}");
            }
        }
        
        timeSinceLastLog += Time.deltaTime;

        if (timeSinceLastLog >= loggingInterval)
        {
            LogPositionFrame();
            timeSinceLastLog = 0f;
        }
    }

    private void LogPositionFrame()
    {
        float elapsed = Time.time - sessionStartTime;
        
        // Get player head position
        Vector3 headPos = playerHeadTransform.position;
        float xPos = headPos.x;
        float yPos = headPos.y;
        float zPos = headPos.z;
        
        // Cast raycast forward from player head
        string raycastHit = GetRaycastTarget();
        
        // Console logging
        if (logToConsole)
        {
            Debug.Log($"[PlayerPosLog] T={elapsed:F2}s | X:{xPos:F2} | Y:{yPos:F2} | Z:{zPos:F2} | Looking at:{raycastHit}");
        }

        // CSV logging
        if (logToCSV)
        {
            WriteToCSV(elapsed, xPos, yPos, zPos, raycastHit);
        }
    }

    private string GetRaycastTarget()
    {
        Ray ray = new Ray(playerHeadTransform.position, playerHeadTransform.TransformDirection(Vector3.forward));
        
        if (Physics.Raycast(ray, out RaycastHit hitInfo, raycastDistance, raycastLayerMask))
        {
            Debug.DrawRay(playerHeadTransform.position, playerHeadTransform.TransformDirection(Vector3.forward) * hitInfo.distance, Color.yellow);
            return hitInfo.collider.gameObject.name;
        }
        else
        {
            Debug.DrawRay(playerHeadTransform.position, playerHeadTransform.TransformDirection(Vector3.forward) * raycastDistance, Color.cyan);
            return "";
        }
    }

    private void InitializeCSVFile()
    {
        try
        {
            // Create filename with cycle number (1-indexed for user readability)
            string cycleNumber = (currentCycle + 1).ToString();
            csvFilePath = Path.Combine(Application.persistentDataPath, $"Player_Position_Cycle{cycleNumber}.csv");
            
            string directory = Path.GetDirectoryName(csvFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = new StreamWriter(csvFilePath, false))
            {
                writer.WriteLine("Time(s),XPos,YPos,ZPos,Raycast");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[PlayerPositionLogger] Failed to initialize CSV: " + ex.Message);
        }
    }

    private void WriteToCSV(float elapsed, float xPos, float yPos, float zPos, string raycastHit)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, true))
            {
                writer.WriteLine($"{elapsed:F3},{xPos:F3},{yPos:F3},{zPos:F3},{raycastHit}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[PlayerPositionLogger] Failed to write to CSV: " + ex.Message);
        }
    }

    public string GetCSVFilePath()
    {
        return csvFilePath;
    }
}
