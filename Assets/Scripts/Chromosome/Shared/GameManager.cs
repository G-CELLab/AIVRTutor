using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Intro,
        Interphase,
        InterphasePart2,
        Prophase,
        Metaphase,
        Anaphase,
        Telophase,
        ResetGame,
        GameOver
    }
    public static GameState eGameStatus;

    //public delegate void AsteroidHandler();
    //public static event AsteroidHandler AsteroidDestroyed;

    public UnityEvent onIntro;
    public UnityEvent onInterphase;
    public UnityEvent onInterphasePart2;
    public UnityEvent onProphase;
    public UnityEvent onMetaphase;
    public UnityEvent onAnaphase;
    public UnityEvent onTelophase;
    public UnityEvent onGameReset;
    public UnityEvent onGameOver;

    public bool proPhase = true;
    
    [Header("AI Agent Integration")]
    public GPTConnector gptConnector;
    public TextToSpeechPlayer ttsPlayer;

    [Header("The Slider Components")]
    public Image ATPsliderImg;
    public Image HPsliderImg;

    public static int playerScore = 0;

    [Header("Healing Cycle Tracking")]
    [Tooltip("Number of times the player has touched the hand to heal and entered the simulation")]
    public static int healingCycleCount = 0;
    
    [Tooltip("Maximum number of cycles before the wound is fully healed")]
    public const int MAX_HEALING_CYCLES = 3;
    
    /// <summary>
    /// Increments the healing cycle counter when the player enters the simulation
    /// </summary>
    public static void IncrementHealingCycle()
    {
        healingCycleCount++;
        Debug.Log($"Healing Cycle Count: {healingCycleCount}/{MAX_HEALING_CYCLES}");
    }
    
    /// <summary>
    /// Checks if the wound is fully healed (player has completed all required cycles)
    /// </summary>
    /// <returns>True if all healing cycles are complete</returns>
    public static bool IsWoundHealed()
    {
        return healingCycleCount >= MAX_HEALING_CYCLES;
    }
    
    /// <summary>
    /// Gets the current healing cycle count for AI agent and UI
    /// </summary>
    /// <returns>Current healing cycle count</returns>
    public static int GetHealingCycleCount()
    {
        return healingCycleCount;
    }
    
    /// <summary>
    /// Resets the healing cycle counter (for game restart)
    /// </summary>
    public static void ResetHealingCycles()
    {
        healingCycleCount = 0;
        Debug.Log("Healing cycles reset");
    }
    
    /// <summary>
    /// Gets a formatted status message about the healing progress
    /// </summary>
    /// <returns>Status message for UI or AI agent</returns>
    public static string GetHealingStatusMessage()
    {
        if (IsWoundHealed())
        {
            return "Wound fully healed! Touch the healed hand to complete the simulation.";
        }
        else
        {
            return $"Healing Progress: {healingCycleCount}/{MAX_HEALING_CYCLES} cycles completed. " +
                   $"{MAX_HEALING_CYCLES - healingCycleCount} more cycle(s) needed.";
        }
    }

    public void FoodCollision()
    {
        LogEventHelper.LogATPCharged();
        Debug.Log("ATP_Charged");
        ATPsliderImg.fillAmount += 0.3f;
    }

    public void CellDivided()
    {
        Debug.Log("HP_Charged");
        HPsliderImg.fillAmount += 0.3f;
    }

    private void Start()
    {
        // Log the current healing cycle for debugging and AI agent tracking
        Debug.Log($"[GameManager] Starting - Healing Cycle: {healingCycleCount}/{MAX_HEALING_CYCLES}, HPtracking: {ScoreManager.HPtracking}");
        
        // Reset phase announcement counters when a new cycle begins (new scene load)
        if (gptConnector != null)
        {
            gptConnector.ResetPhaseAnnouncementCounters();
        }
        
        // Start at Intro only for the very first time (cycle 0)
        // After first completion (cycle 1+), always start at Interphase
        if(healingCycleCount == 0)
        {
            eGameStatus = GameState.Intro;
            onIntro.Invoke();
            Debug.Log("Game_Started - First time, starting at Intro");
        }
        else
        {
            eGameStatus = GameState.Interphase;
            onInterphase.Invoke();
            Debug.Log($"Game_Started - Cycle {healingCycleCount + 1} (of {MAX_HEALING_CYCLES}), skipping Intro");
        }
    }

    public void Intro()
    {
        eGameStatus = GameState.Intro;
        onIntro.Invoke();
        Debug.Log("Intro");
    }

    public void Interphase()
    {
        eGameStatus = GameState.Interphase;
        onInterphase.Invoke();
        Debug.Log("Interphase");
    }

    public void InterphasePart2()
    {
        eGameStatus = GameState.InterphasePart2;
        onInterphasePart2.Invoke();
        Debug.Log("Interphase Part 2");
    }

    public void Prophase()
    {
        eGameStatus = GameState.Prophase;
        onProphase.Invoke();
        proPhase = true;
        Debug.Log("Prophase");
    }

    public void Metaphase()
    {
        eGameStatus = GameState.Metaphase;
        onMetaphase.Invoke();
        Debug.Log("Metaphase");
    }

    public void Anaphase()
    {
        eGameStatus = GameState.Anaphase;
        onAnaphase.Invoke();
        Debug.Log("Anaphase");
    }

    public void Telophase()
    {
        eGameStatus = GameState.Telophase;
        onTelophase.Invoke();
        Debug.Log("Telophase");

        // Rigorous cleanup of previous phase objects
        CleanupPreviousPhases();
        
        // Trigger proactive AI speech based on healing cycle (with slight delay)
        StartCoroutine(DelayedTelophaseAITrigger());
    }
    
    /// <summary>
    /// Delays the AI trigger slightly to ensure scene is stable
    /// </summary>
    private IEnumerator DelayedTelophaseAITrigger()
    {
        // Wait a moment for the scene to settle
        yield return new WaitForSeconds(0.5f);
        TriggerTelophaseAIResponse();
    }
    
    /// <summary>
    /// Triggers proactive AI speech when entering Telophase based on which healing cycle we're in
    /// </summary>
    private void TriggerTelophaseAIResponse()
    {
        if (ttsPlayer == null)
        {
            Debug.LogWarning("[GameManager] TextToSpeechPlayer not assigned. Cannot trigger AI response.");
            return;
        }
        
        int currentCycle = healingCycleCount;
        string aiMessage = "";
        
        if (currentCycle == 0)
        {
            // First cycle completion
            aiMessage = "Great work! You've completed the first round of cell division. However, the wound isn't fully healed yet. " +
                       "In real tissue, cell division is a continuous process—many cells must divide multiple times to fully repair the damage. " +
                       "Touch the wound again to repeat the process.";
        }
        else if (currentCycle == 1)
        {
            // Second cycle completion
            aiMessage = "Excellent! You've now completed two rounds of cell division. The wound is healing nicely, but we need one more cycle " +
                       "to fully restore the tissue. In your body, cells divide continuously—sometimes hundreds or thousands of times—to repair injuries. " +
                       "Let's complete the final round!";
        }
        else if (currentCycle == 2)
        {
            // Third cycle completion
            aiMessage = "Congratulations! You've successfully completed all three rounds of mitosis! By simulating accelerated cell division, " +
                       "we've healed the wound. In reality, this process would take hours or days, but you've just witnessed how cells " +
                       "work together to repair tissue. Touch the healed hand to complete the simulation.";
        }
        
        if (!string.IsNullOrEmpty(aiMessage))
        {
            Debug.Log($"[Telophase AI] Speaking proactive message for cycle {currentCycle + 1}");
            ttsPlayer.Speak(aiMessage, () => 
            {
                Debug.Log("[Telophase AI] Speech completed.");
            });
        }
    }

    private void CleanupPreviousPhases()
    {
        // Disable individual objects by tag if they are in the scene
        string[] tagsToDisable = { "Centrosome1", "Centrosome2", "Meta", "Pro", "Finish" };
        foreach (string tag in tagsToDisable)
        {
            try
            {
                GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);
                foreach (var obj in objs)
                {
                    obj.SetActive(false);
                }
            }
            catch (UnityException)
            {
                // Tag doesn't exist - skip it
                Debug.Log($"[GameManager] Tag '{tag}' not defined - skipping cleanup for this tag.");
            }
        }

        // We use Resources.FindObjectsOfTypeAll to find objects even if they are already partially disabled
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in allObjects)
        {
            // Disable the DNA and any remaining chromatids
            if (obj.name == "Player_DNA" || obj.name == "P_Chromotid_L " || obj.name == "P_Chromotid_R")
            {
                // Only disable objects that are actually in a scene (not prefabs in the project)
                if (obj.scene.name != null) 
                {
                    obj.SetActive(false);
                }
            }
        }
    }

    public void GameEnd()
    {
        eGameStatus = GameState.GameOver;
        onGameOver.Invoke();
        Debug.Log("Game_End - Wound fully healed! Ending screen displayed. User can now touch the healed wound to close the app.");
    }
}
