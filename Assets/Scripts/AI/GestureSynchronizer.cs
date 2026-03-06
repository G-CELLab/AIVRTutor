using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Improved gesture timing system that synchronizes gestures with actual audio playback.
/// Solves the timing mismatch problem by:
/// 1. Estimating keyword position in speech based on character counts
/// 2. Tracking actual audio playback time
/// 3. Triggering gestures at the correct moment during audio playback
/// 4. Providing fallback mechanisms for reliability
/// </summary>
public class GestureSynchronizer : MonoBehaviour
{
    [Header("References")]
    public TTSAnimatorDriver ttsDriver;
    public TextToSpeechPlayer ttsPlayer;

    [Header("Timing Configuration")]
    [Tooltip("Characters per second - average speaking rate for English (adjust based on voice speed)")]
    public float charactersPerSecond = 15f; // ~180 words per minute standard rate
    
    [Tooltip("Safety margin - add this delay after calculated time to ensure audio has progressed")]
    public float timingSafetyMarginSec = 0.1f;
    
    [Tooltip("Maximum gesture delay - if calculated delay exceeds this, use fallback timing")]
    public float maxGestureDelaySec = 10f;

    [Header("Per-Phase Timing Compensation")]
    [Tooltip("Interphase timing multiplier. Keep at 1.0 if eat timing is already correct.")]
    public float interphaseTimeScale = 1.0f;
    [Tooltip("Prophase timing multiplier. Lower values trigger earlier.")]
    public float prophaseTimeScale = 0.85f;
    [Tooltip("Metaphase timing multiplier. Lower values trigger earlier.")]
    public float metaphaseTimeScale = 0.60f;
    [Tooltip("Anaphase timing multiplier. Lower values trigger earlier.")]
    public float anaphaseTimeScale = 0.65f;

    [Tooltip("Interphase fixed advance (seconds). Positive value triggers earlier.")]
    public float interphaseAdvanceSec = 0f;
    [Tooltip("Prophase fixed advance (seconds). Positive value triggers earlier.")]
    public float prophaseAdvanceSec = 0.25f;
    [Tooltip("Metaphase fixed advance (seconds). Positive value triggers earlier.")]
    public float metaphaseAdvanceSec = 0.90f;

    [Tooltip("Extra advance specifically for DZ12 line-up in Metaphase to compensate animation startup latency.")]
    public float metaphaseLineUpExtraAdvanceSec = 0.50f;
    [Tooltip("Anaphase fixed advance (seconds). Positive value triggers earlier.")]
    public float anaphaseAdvanceSec = 0.75f;

    [Header("Fallback Delays (Used if calculation fails)")]
    public float fallbackInterphaseDelay = 2.0f;
    public float fallbackProphaseDelay = 2.5f;
    public float fallbackMetaphaseDelay = 2.0f;
    public float fallbackAnaphaseDelay = 3.0f;

    [Header("Debug")]
    public bool verboseDebug = true;
    public bool showTimingCalculations = true;

    // Active gesture tracking
    private Coroutine _activeGestureCoroutine;
    private string _currentTranscript = "";
    private float _audioStartTime = -1f;
    private bool _gestureTriggeredThisResponse = false;

    private void Awake()
    {
        if (ttsDriver == null) ttsDriver = FindAnyObjectByType<TTSAnimatorDriver>();
        if (ttsPlayer == null) ttsPlayer = FindAnyObjectByType<TextToSpeechPlayer>();
    }

    /// <summary>
    /// Call this when a new AI response begins
    /// </summary>
    public void OnResponseStart()
    {
        _currentTranscript = "";
        _audioStartTime = -1f;
        _gestureTriggeredThisResponse = false;
        CancelActiveGesture();
        
        if (verboseDebug)
            Debug.Log("[GestureSynchronizer] 🆕 New response started - reset state");
    }

    /// <summary>
    /// Call this when audio playback begins
    /// </summary>
    public void OnAudioPlaybackStart()
    {
        _audioStartTime = Time.realtimeSinceStartup;
        
        if (verboseDebug)
            Debug.Log($"[GestureSynchronizer] 🔊 Audio playback started at {_audioStartTime:F2}");
    }

    /// <summary>
    /// Process streaming transcript delta and schedule gesture if keyword detected
    /// </summary>
    public bool ProcessStreamingDelta(string accumulatedTranscript, GameManager.GameState currentPhase)
    {
        if (_gestureTriggeredThisResponse)
            return false; // Already triggered for this response

        _currentTranscript = accumulatedTranscript ?? "";
        string normalized = NormalizeText(_currentTranscript);

        // Check for phase-specific keywords
        GestureInfo gestureInfo = DetectGestureKeyword(normalized, currentPhase);
        if (gestureInfo == null)
            return false;

        // Calculate timing based on keyword position in text
        float gestureDelay = CalculateGestureDelay(
            normalized,
            gestureInfo.KeywordPosition,
            gestureInfo.FallbackDelay,
            gestureInfo.TimeScale,
            gestureInfo.AdvanceSec
        );

        // Schedule the gesture
        _gestureTriggeredThisResponse = true;
        _activeGestureCoroutine = StartCoroutine(TriggerGestureAfterDelay(gestureInfo, gestureDelay));

        return true;
    }

    /// <summary>
    /// Fallback: Trigger gesture based on complete transcript (for non-streaming mode)
    /// </summary>
    public bool ProcessCompleteTranscript(string completeTranscript, GameManager.GameState currentPhase)
    {
        if (_gestureTriggeredThisResponse)
            return false;

        _currentTranscript = completeTranscript ?? "";
        string normalized = NormalizeText(_currentTranscript);

        GestureInfo gestureInfo = DetectGestureKeyword(normalized, currentPhase);
        if (gestureInfo == null)
            return false;

        // Use fallback timing since we have complete text
        _gestureTriggeredThisResponse = true;
        float adjustedFallback = Mathf.Max(0f, gestureInfo.FallbackDelay - Mathf.Max(0f, gestureInfo.AdvanceSec));
        _activeGestureCoroutine = StartCoroutine(TriggerGestureAfterDelay(gestureInfo, adjustedFallback));

        return true;
    }

    /// <summary>
    /// Calculate gesture delay based on keyword position in text
    /// </summary>
    private float CalculateGestureDelay(string normalizedText, int keywordStartIndex, float fallbackDelay, float timeScale, float advanceSec)
    {
        if (keywordStartIndex < 0 || keywordStartIndex >= normalizedText.Length)
        {
            if (showTimingCalculations)
                Debug.LogWarning($"[GestureSynchronizer] ⚠️ Invalid keyword position {keywordStartIndex}, using fallback delay {fallbackDelay:F2}s");
            return Mathf.Max(0f, fallbackDelay - Mathf.Max(0f, advanceSec));
        }

        // Calculate characters before keyword
        int charsBeforeKeyword = keywordStartIndex;
        
        // Estimate time to speak those characters
        float estimatedTime = charsBeforeKeyword / Mathf.Max(1f, charactersPerSecond);
        float scaledEstimatedTime = estimatedTime * Mathf.Clamp(timeScale, 0.2f, 2.0f);
        
        // Add safety margin
        float totalDelay = scaledEstimatedTime + timingSafetyMarginSec - Mathf.Max(0f, advanceSec);

        // If audio already started, adjust delay based on elapsed time
        if (_audioStartTime > 0)
        {
            float elapsedAudioTime = Time.realtimeSinceStartup - _audioStartTime;
            totalDelay = Mathf.Max(0, totalDelay - elapsedAudioTime);
        }

        // Clamp to reasonable range
        totalDelay = Mathf.Clamp(totalDelay, 0f, maxGestureDelaySec);

        if (showTimingCalculations)
        {
            Debug.Log($"[GestureSynchronizer] 📊 Timing calculation:" +
                     $"\n  • Chars before keyword: {charsBeforeKeyword}" +
                     $"\n  • Estimated speak time: {estimatedTime:F2}s" +
                     $"\n  • Phase time scale: {timeScale:F2}" +
                     $"\n  • Scaled speak time: {scaledEstimatedTime:F2}s" +
                     $"\n  • Phase advance: {Mathf.Max(0f, advanceSec):F2}s" +
                     $"\n  • Safety margin: {timingSafetyMarginSec:F2}s" +
                     $"\n  • Audio elapsed: {(_audioStartTime > 0 ? (Time.realtimeSinceStartup - _audioStartTime) : 0):F2}s" +
                     $"\n  • Final delay: {totalDelay:F2}s");
        }

        return totalDelay;
    }

    /// <summary>
    /// Detect gesture keyword and return gesture information
    /// </summary>
    private GestureInfo DetectGestureKeyword(string normalizedText, GameManager.GameState phase)
    {
        switch (phase)
        {
            case GameManager.GameState.Interphase:
            case GameManager.GameState.InterphasePart2:
                return DetectEatGesture(normalizedText);

            case GameManager.GameState.Prophase:
                return DetectCondenseGesture(normalizedText);

            case GameManager.GameState.Metaphase:
                return DetectLineUpGesture(normalizedText);

            case GameManager.GameState.Anaphase:
                return DetectSplitOutwardGesture(normalizedText);

            default:
                return null;
        }
    }

    private GestureInfo DetectEatGesture(string normalizedText)
    {
        // Keywords in priority order (most specific first)
        string[] keywords = {
            "eat food",
            "eating food",
            "eat the food",
            "eats food",
            "food for energy",
            "get energy",
            "needs food",
            "need to eat",
            "must eat"
        };

        foreach (string keyword in keywords)
        {
            int index = normalizedText.IndexOf(keyword);
            if (index >= 0)
            {
                if (verboseDebug)
                    Debug.Log($"[GestureSynchronizer] 🍽️ Interphase: Detected '{keyword}' at position {index}");

                return new GestureInfo
                {
                    Phase = GameManager.GameState.Interphase,
                    GestureName = "EAT (DZ13)",
                    TriggerAction = () => ttsDriver?.TriggerDZ13(),
                    KeywordPosition = index,
                    Keyword = keyword,
                    FallbackDelay = fallbackInterphaseDelay,
                    TimeScale = interphaseTimeScale,
                    AdvanceSec = interphaseAdvanceSec
                };
            }
        }

        return null;
    }

    private GestureInfo DetectCondenseGesture(string normalizedText)
    {
        string[] keywords = {
            "x shape condense",
            "condense into x",
            "x shaped chromosome",
            "condenses into x",
            "x shaped",
            "x shape",
            "condense",
            "condenses"
        };

        foreach (string keyword in keywords)
        {
            int index = normalizedText.IndexOf(keyword);
            if (index >= 0)
            {
                if (verboseDebug)
                    Debug.Log($"[GestureSynchronizer] 🎯 Prophase: Detected '{keyword}' at position {index}");

                return new GestureInfo
                {
                    Phase = GameManager.GameState.Prophase,
                    GestureName = "CONDENSE (DZ18)",
                    TriggerAction = () => ttsDriver?.TriggerDZ18(),
                    KeywordPosition = index,
                    Keyword = keyword,
                    FallbackDelay = fallbackProphaseDelay,
                    TimeScale = prophaseTimeScale,
                    AdvanceSec = prophaseAdvanceSec
                };
            }
        }

        return null;
    }

    private GestureInfo DetectLineUpGesture(string normalizedText)
    {
        string[] keywords = {
            "line up at the center",
            "line up at center",
            "line up in the middle",
            "line up",
            "lined up",
            "line them up",
            "all line up",
            "in a row",
            "align at the center"
        };

        foreach (string keyword in keywords)
        {
            int index = normalizedText.IndexOf(keyword);
            if (index >= 0)
            {
                if (verboseDebug)
                    Debug.Log($"[GestureSynchronizer] 📏 Metaphase: Detected '{keyword}' at position {index}");

                return new GestureInfo
                {
                    Phase = GameManager.GameState.Metaphase,
                    GestureName = "LINE UP (DZ12)",
                    TriggerAction = () => ttsDriver?.TriggerDZ12(),
                    KeywordPosition = index,
                    Keyword = keyword,
                    FallbackDelay = fallbackMetaphaseDelay,
                    TimeScale = metaphaseTimeScale,
                    AdvanceSec = metaphaseAdvanceSec + Mathf.Max(0f, metaphaseLineUpExtraAdvanceSec)
                };
            }
        }

        return null;
    }

    private GestureInfo DetectSplitOutwardGesture(string normalizedText)
    {
        string[] keywords = {
            "split the chromosome",
            "split chromosome",
            "split into two halves",
            "split into two",
            "split in two",
            "pull each half",
            "separate chromatids",
            "move to opposite ends",
            "move them to opposite ends",
            "pull to opposite ends",
            "to opposite ends",
            "to opposite sides",
            "to opposite poles",
            "pull apart",
            "split apart",
            "move apart",
            "separate them"
        };

        foreach (string keyword in keywords)
        {
            int index = normalizedText.IndexOf(keyword);
            if (index >= 0)
            {
                if (verboseDebug)
                    Debug.Log($"[GestureSynchronizer] ↔️ Anaphase: Detected '{keyword}' at position {index}");

                return new GestureInfo
                {
                    Phase = GameManager.GameState.Anaphase,
                    GestureName = "SPLIT OUTWARD (DZ22)",
                    TriggerAction = () => ttsDriver?.TriggerDZ22(),
                    KeywordPosition = index,
                    Keyword = keyword,
                    FallbackDelay = fallbackAnaphaseDelay,
                    TimeScale = anaphaseTimeScale,
                    AdvanceSec = anaphaseAdvanceSec
                };
            }
        }

        return null;
    }

    private IEnumerator TriggerGestureAfterDelay(GestureInfo gestureInfo, float delaySec)
    {
        if (verboseDebug)
            Debug.Log($"[GestureSynchronizer] ⏱️ Scheduling {gestureInfo.GestureName} in {delaySec:F2}s (keyword: '{gestureInfo.Keyword}')");

        // Wait for the calculated delay
        float waitStart = Time.realtimeSinceStartup;
        yield return new WaitForSeconds(delaySec);
        float actualWait = Time.realtimeSinceStartup - waitStart;

        // Trigger the gesture
        if (ttsDriver == null)
        {
            Debug.LogWarning($"[GestureSynchronizer] ❌ Cannot trigger {gestureInfo.GestureName} - TTSAnimatorDriver is null");
            yield break;
        }

        gestureInfo.TriggerAction?.Invoke();

        if (verboseDebug)
            Debug.Log($"[GestureSynchronizer] ✅ Triggered {gestureInfo.GestureName} after {actualWait:F2}s actual wait");
    }

    private void CancelActiveGesture()
    {
        if (_activeGestureCoroutine != null)
        {
            StopCoroutine(_activeGestureCoroutine);
            _activeGestureCoroutine = null;
            
            if (verboseDebug)
                Debug.Log("[GestureSynchronizer] 🚫 Cancelled active gesture");
        }
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Convert to lowercase and remove punctuation
        text = text.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " ");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    // Data structure for gesture information
    private class GestureInfo
    {
        public GameManager.GameState Phase;
        public string GestureName;
        public Action TriggerAction;
        public int KeywordPosition;
        public string Keyword;
        public float FallbackDelay;
        public float TimeScale;
        public float AdvanceSec;
    }

    // Public API for external callers
    public void SetCharactersPerSecond(float cps)
    {
        charactersPerSecond = Mathf.Clamp(cps, 5f, 30f);
        if (verboseDebug)
            Debug.Log($"[GestureSynchronizer] ⚙️ Characters per second set to {charactersPerSecond:F1}");
    }

    public bool IsGestureTriggeredThisResponse => _gestureTriggeredThisResponse;
}
