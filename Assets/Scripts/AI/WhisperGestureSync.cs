using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Whisper;

/// <summary>
/// Synchronizes AI agent gestures with speech using Whisper word-level timestamps.
/// Transcribes audio in real-time and triggers phase-appropriate gestures based on semantic keywords.
/// </summary>
public class WhisperGestureSync : MonoBehaviour
{
    [Header("Whisper Configuration")]
    [Tooltip("Model path. Preferred: Whisper/ggml-tiny.bin (inside StreamingAssets). Also accepts Assets/StreamingAssets/... and absolute paths.")]
    public string whisperModelPath = "Whisper/ggml-tiny.bin";
    
    [Tooltip("Language for transcription (empty = auto-detect)")]
    public string language = "en";
    
    [Tooltip("Enable word-level timestamps")]
    public bool enableWordTimestamps = true;
    
    [Header("Gesture Control")]
    [Tooltip("Reference to the TTSAnimatorDriver that controls gestures")]
    public TTSAnimatorDriver animatorDriver;
    
    [Tooltip("Reference to GPTConnector for phase awareness")]
    public GPTConnector gptConnector;
    
    [Header("Audio Source")]
    [Tooltip("AudioSource playing the TTS audio")]
    public AudioSource ttsAudioSource;
    
    [Header("Timing Adjustments")]
    [Tooltip("Offset in seconds to sync gestures with speech (positive = delay, negative = earlier)")]
    public float gestureTimingOffset = 0.1f;
    
    [Tooltip("Minimum confidence threshold for word detection (0-1)")]
    [Range(0f, 1f)]
    public float confidenceThreshold = 0.5f;
    
    [Header("Phase-Specific Gestures")]
    public bool enablePhaseGestures = true;
    
    [Header("Debug")]
    public bool verboseLogging = true;
    public bool logWordTimestamps = true;
    
    // Whisper components
    private WhisperManager whisperManager;
    private bool isInitialized = false;
    private bool initializationStarted = false;
    private bool pendingInitWarningLogged = false;
    private AudioClip pendingClip;
    private bool hasPendingClip = false;
    
    // Gesture scheduling
    private List<ScheduledGesture> scheduledGestures = new List<ScheduledGesture>();
    private Coroutine playbackCoroutine;
    
    // Gesture definitions by phase
    private Dictionary<GameManager.GameState, List<GestureKeyword>> phaseGestures;
    
    void Awake()
    {
        InitializePhaseGestures();
        
        // Auto-find components if not assigned
        if (!animatorDriver) animatorDriver = FindAnyObjectByType<TTSAnimatorDriver>();
        if (!gptConnector) gptConnector = FindAnyObjectByType<GPTConnector>();
        if (!ttsAudioSource && gptConnector && gptConnector.ttsPlayer)
        {
            ttsAudioSource = gptConnector.ttsPlayer.audioSource;
        }

        EnsureInitializationStarted();
    }
    
    void Start()
    {
        EnsureInitializationStarted();
    }

    void EnsureInitializationStarted()
    {
        if (initializationStarted) return;
        initializationStarted = true;
        StartCoroutine(InitializeWhisper());
    }

    bool TryAdoptLoadedWhisperManager()
    {
        if (whisperManager != null && whisperManager.IsLoaded)
        {
            isInitialized = true;
            pendingInitWarningLogged = false;
            return true;
        }

        var found = FindAnyObjectByType<WhisperManager>();
        if (found != null && found.IsLoaded)
        {
            whisperManager = found;
            isInitialized = true;
            pendingInitWarningLogged = false;
            if (verboseLogging) Debug.Log("[WhisperGesture] Adopted already-loaded WhisperManager");
            return true;
        }

        return false;
    }
    
    IEnumerator InitializeWhisper()
    {
        if (verboseLogging) Debug.Log("[WhisperGesture] Initializing Whisper...");

        whisperManager = GetComponent<WhisperManager>();
        if (!whisperManager)
        {
            whisperManager = FindAnyObjectByType<WhisperManager>();
        }

        if (!whisperManager)
        {
            whisperManager = gameObject.AddComponent<WhisperManager>();
            if (verboseLogging) Debug.Log("[WhisperGesture] No WhisperManager found. Auto-added one to this GameObject.");
            yield return null;
        }

        if (whisperManager.IsLoading)
        {
            if (verboseLogging) Debug.Log("[WhisperGesture] WhisperManager is already loading. Waiting...");
            yield return new WaitUntil(() => !whisperManager.IsLoading);
        }

        if (whisperManager.IsLoaded)
        {
            isInitialized = true;
            pendingInitWarningLogged = false;
            if (verboseLogging) Debug.Log("[WhisperGesture] ✅ Whisper already initialized");
            yield break;
        }

        var resolved = ResolveModelPathForWhisper(whisperModelPath);
        if (verboseLogging)
        {
            Debug.Log($"[WhisperGesture] Model config='{whisperModelPath}', resolved='{resolved.resolvedPath}', inStreamingAssets={resolved.isInStreamingAssets}");
        }

        whisperManager.ModelPath = resolved.resolvedPath;
        whisperManager.IsModelPathInStreamingAssets = resolved.isInStreamingAssets;
        whisperManager.language = string.IsNullOrEmpty(language) ? "en" : language;
        whisperManager.enableTokens = enableWordTimestamps;

        var initTask = whisperManager.InitModel();
        yield return new WaitUntil(() => initTask.IsCompleted);
        
        if (initTask.IsFaulted)
        {
            Debug.LogError($"[WhisperGesture] Initialization failed: {initTask.Exception}");
            initializationStarted = false;
            yield break;
        }

        if (!whisperManager.IsLoaded)
        {
            Debug.LogError("[WhisperGesture] Initialization completed but model is not loaded. Check model path and WhisperManager logs.");
            initializationStarted = false;
            yield break;
        }
        
        isInitialized = true;
        pendingInitWarningLogged = false;
        if (verboseLogging) Debug.Log("[WhisperGesture] ✅ Whisper initialized successfully");

        if (hasPendingClip && pendingClip != null)
        {
            var clipToProcess = pendingClip;
            pendingClip = null;
            hasPendingClip = false;

            if (verboseLogging)
            {
                Debug.Log($"[WhisperGesture] Processing queued first clip after init: {clipToProcess.name}");
            }

            StartCoroutine(ProcessAudioClip(clipToProcess));
        }
    }

    private (string resolvedPath, bool isInStreamingAssets) ResolveModelPathForWhisper(string configuredPath)
    {
        string fallback = "Whisper/ggml-tiny.bin";
        if (string.IsNullOrWhiteSpace(configuredPath))
            return (fallback, true);

        string normalized = configuredPath.Replace('\\', '/').Trim();

        const string assetsStreamingPrefix = "Assets/StreamingAssets/";
        if (normalized.StartsWith(assetsStreamingPrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            string relativeToStreaming = normalized.Substring(assetsStreamingPrefix.Length);
            return (relativeToStreaming, true);
        }

        string streamingAssetsPath = Application.streamingAssetsPath.Replace('\\', '/');
        if (normalized.StartsWith(streamingAssetsPath + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            string relativeToStreaming = normalized.Substring(streamingAssetsPath.Length + 1);
            return (relativeToStreaming, true);
        }

        if (Path.IsPathRooted(normalized))
        {
            return (configuredPath, false);
        }

        if (normalized.StartsWith("Whisper/", System.StringComparison.OrdinalIgnoreCase))
        {
            return (normalized, true);
        }

        if (normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            return (normalized, false);
        }

        return (normalized, true);
    }
    
    /// <summary>
    /// Transcribe audio clip and schedule gestures based on word timestamps.
    /// Call this when TTS starts playing.
    /// </summary>
    public void TranscribeAndScheduleGestures(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[WhisperGesture] Cannot transcribe null clip");
            return;
        }

        if (!isInitialized)
        {
            TryAdoptLoadedWhisperManager();
        }

        if (!isInitialized)
        {
            pendingClip = clip;
            hasPendingClip = true;
            EnsureInitializationStarted();

            if (verboseLogging && !pendingInitWarningLogged)
            {
                Debug.LogWarning($"[WhisperGesture] Whisper not initialized yet. Queued clip: {clip.name}");
                pendingInitWarningLogged = true;
            }
            return;
        }
        
        if (!animatorDriver)
        {
            Debug.LogWarning("[WhisperGesture] No TTSAnimatorDriver assigned");
            return;
        }
        
        StartCoroutine(ProcessAudioClip(clip));
    }
    
    IEnumerator ProcessAudioClip(AudioClip clip)
    {
        if (verboseLogging) Debug.Log($"[WhisperGesture] Processing audio clip: {clip.name} (length: {clip.length:F2}s)");
        
        // Clear previous scheduled gestures
        scheduledGestures.Clear();
        
        // Transcribe with word timestamps
        var transcriptionTask = whisperManager.GetTextAsync(clip);
        yield return new WaitUntil(() => transcriptionTask.IsCompleted);
        
        if (transcriptionTask.IsFaulted)
        {
            Debug.LogError($"[WhisperGesture] Transcription failed: {transcriptionTask.Exception}");
            yield break;
        }
        
        WhisperResult result = transcriptionTask.Result;
        if (result == null)
        {
            Debug.LogError("[WhisperGesture] Transcription result is null");
            yield break;
        }
        
        string fullText = result.Result;
        if (verboseLogging) Debug.Log($"[WhisperGesture] Transcription: {fullText}");
        
        // Get word-level segments (requires Whisper with timestamp support)
        var segments = result.Segments;
        if (segments != null && segments.Count > 0)
        {
            ProcessSegmentsAndScheduleGestures(segments, fullText);
        }
        else
        {
            Debug.LogWarning("[WhisperGesture] No word segments available. Falling back to text-only analysis.");
            AnalyzeTextAndScheduleGestures(fullText, clip.length);
        }
        
        // Start gesture playback coroutine
        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
        playbackCoroutine = StartCoroutine(PlaybackScheduledGestures());
    }
    
    void ProcessSegmentsAndScheduleGestures(List<WhisperSegment> segments, string fullText)
    {
        var currentPhase = GameManager.eGameStatus;
        string normalizedFull = NormalizeText(fullText);
        
        if (logWordTimestamps)
        {
            Debug.Log($"[WhisperGesture] === Word Timestamps ===");
            foreach (var seg in segments)
            {
                Debug.Log($"  [{seg.Start.TotalSeconds:F2}s - {seg.End.TotalSeconds:F2}s] '{seg.Text}'");
            }
        }
        
        // Get gesture keywords for current phase
        if (!enablePhaseGestures || !phaseGestures.ContainsKey(currentPhase))
        {
            if (verboseLogging) Debug.Log($"[WhisperGesture] No gesture mappings for phase: {currentPhase}");
            return;
        }
        
        var keywords = phaseGestures[currentPhase];
        
        // Analyze segments for keyword matches
        foreach (var keyword in keywords)
        {
            // Check if keyword phrase exists in full text
            if (!normalizedFull.Contains(NormalizeText(keyword.phrase)))
                continue;
            
            // Find the segment(s) containing this phrase
            var matchingSegments = FindSegmentsForPhrase(segments, keyword.phrase);
            
            if (matchingSegments.Count > 0)
            {
                // Calculate timing based on phrase position
                float triggerTime = CalculateTriggerTime(matchingSegments, keyword);
                
                scheduledGestures.Add(new ScheduledGesture
                {
                    triggerTime = triggerTime + gestureTimingOffset,
                    gestureName = keyword.gestureName,
                    phrase = keyword.phrase,
                    triggerAction = keyword.action
                });
                
                if (verboseLogging)
                {
                    Debug.Log($"[WhisperGesture] ⏰ Scheduled {keyword.gestureName} at {triggerTime + gestureTimingOffset:F2}s for phrase: '{keyword.phrase}'");
                }
            }
        }
        
        // Sort gestures by trigger time
        scheduledGestures = scheduledGestures.OrderBy(g => g.triggerTime).ToList();
    }
    
    List<WhisperSegment> FindSegmentsForPhrase(List<WhisperSegment> segments, string phrase)
    {
        var result = new List<WhisperSegment>();
        string normalizedPhrase = NormalizeText(phrase);
        string[] phraseWords = normalizedPhrase.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        // Try to find consecutive segments that match the phrase words
        for (int i = 0; i < segments.Count; i++)
        {
            string segmentText = NormalizeText(segments[i].Text);
            
            // Check if this segment starts matching the phrase
            if (phraseWords.Length > 0 && segmentText.Contains(phraseWords[0]))
            {
                result.Clear();
                result.Add(segments[i]);
                
                // Try to match additional words if phrase has multiple words
                int wordIdx = 1;
                for (int j = i + 1; j < segments.Count && wordIdx < phraseWords.Length; j++)
                {
                    string nextSegText = NormalizeText(segments[j].Text);
                    if (nextSegText.Contains(phraseWords[wordIdx]))
                    {
                        result.Add(segments[j]);
                        wordIdx++;
                    }
                    else
                    {
                        break; // Phrase match broken
                    }
                }
                
                // If we matched all words, return
                if (wordIdx >= phraseWords.Length)
                {
                    return result;
                }
                
                result.Clear();
            }
        }
        
        return result;
    }
    
    float CalculateTriggerTime(List<WhisperSegment> segments, GestureKeyword keyword)
    {
        if (segments.Count == 0) return 0f;
        
        // Use the start time of the phrase, optionally with keyword-specific offset
        float baseTime = (float)segments[0].Start.TotalSeconds;
        
        // For multi-word phrases, optionally trigger on the last word for better sync
        if (keyword.triggerOnLastWord && segments.Count > 1)
        {
            baseTime = (float)segments[segments.Count - 1].Start.TotalSeconds;
        }
        
        return baseTime + keyword.wordOffset;
    }
    
    void AnalyzeTextAndScheduleGestures(string text, float clipLength)
    {
        // Fallback: Analyze text without timestamps, distribute gestures evenly
        var currentPhase = GameManager.eGameStatus;
        if (!enablePhaseGestures || !phaseGestures.ContainsKey(currentPhase))
            return;
        
        var keywords = phaseGestures[currentPhase];
        string normalized = NormalizeText(text);
        
        foreach (var keyword in keywords)
        {
            if (normalized.Contains(NormalizeText(keyword.phrase)))
            {
                // Estimate timing based on text position
                float estimatedTime = EstimateTimeFromTextPosition(text, keyword.phrase, clipLength);
                
                scheduledGestures.Add(new ScheduledGesture
                {
                    triggerTime = estimatedTime + gestureTimingOffset,
                    gestureName = keyword.gestureName,
                    phrase = keyword.phrase,
                    triggerAction = keyword.action
                });
                
                if (verboseLogging)
                {
                    Debug.Log($"[WhisperGesture] ⏰ Estimated {keyword.gestureName} at {estimatedTime:F2}s (no timestamps)");
                }
            }
        }
        
        scheduledGestures = scheduledGestures.OrderBy(g => g.triggerTime).ToList();
    }
    
    float EstimateTimeFromTextPosition(string fullText, string phrase, float totalDuration)
    {
        int phraseIndex = fullText.IndexOf(phrase, System.StringComparison.OrdinalIgnoreCase);
        if (phraseIndex < 0) return 0f;
        
        // Estimate time based on character position (rough approximation)
        float ratio = (float)phraseIndex / fullText.Length;
        return ratio * totalDuration;
    }
    
    IEnumerator PlaybackScheduledGestures()
    {
        if (scheduledGestures.Count == 0)
        {
            if (verboseLogging) Debug.Log("[WhisperGesture] No gestures scheduled");
            yield break;
        }
        
        if (verboseLogging)
        {
            Debug.Log($"[WhisperGesture] 🎬 Starting gesture playback, {scheduledGestures.Count} gestures scheduled");
        }
        
        float startTime = Time.time;
        int gestureIndex = 0;
        
        while (gestureIndex < scheduledGestures.Count)
        {
            float elapsed = Time.time - startTime;
            var gesture = scheduledGestures[gestureIndex];
            
            if (elapsed >= gesture.triggerTime)
            {
                // Trigger the gesture
                TriggerGesture(gesture);
                gestureIndex++;
            }
            
            yield return null;
        }
        
        if (verboseLogging) Debug.Log("[WhisperGesture] ✅ All gestures triggered");
    }
    
    void TriggerGesture(ScheduledGesture gesture)
    {
        if (verboseLogging)
        {
            Debug.Log($"[WhisperGesture] 🎭 Triggering: {gesture.gestureName} (phrase: '{gesture.phrase}')");
        }
        
        try
        {
            gesture.triggerAction?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WhisperGesture] Error triggering gesture {gesture.gestureName}: {e.Message}");
        }
    }
    
    void InitializePhaseGestures()
    {
        phaseGestures = new Dictionary<GameManager.GameState, List<GestureKeyword>>();
        
        // INTERPHASE: Eating/Energy gestures
        phaseGestures[GameManager.GameState.Interphase] = new List<GestureKeyword>
        {
            new GestureKeyword
            {
                phrase = "eat food",
                gestureName = "DZ13 (Eat)",
                action = () => animatorDriver?.TriggerDZ13(),
                wordOffset = 0.2f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "get energy",
                gestureName = "DZ13 (Eat)",
                action = () => animatorDriver?.TriggerDZ13(),
                wordOffset = 0.1f
            },
            new GestureKeyword
            {
                phrase = "food for energy",
                gestureName = "DZ13 (Eat)",
                action = () => animatorDriver?.TriggerDZ13(),
                wordOffset = 0.0f
            }
        };
        
        // PROPHASE: Condensing/X-shape gestures
        phaseGestures[GameManager.GameState.Prophase] = new List<GestureKeyword>
        {
            new GestureKeyword
            {
                phrase = "condense",
                gestureName = "DZ18 (X-Shape Condense)",
                action = () => animatorDriver?.TriggerDZ18(),
                wordOffset = 0.2f
            },
            new GestureKeyword
            {
                phrase = "x shape",
                gestureName = "DZ18 (X-Shape Condense)",
                action = () => animatorDriver?.TriggerDZ18(),
                wordOffset = 0.1f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "condense into x",
                gestureName = "DZ18 (X-Shape Condense)",
                action = () => animatorDriver?.TriggerDZ18(),
                wordOffset = 0.3f,
                triggerOnLastWord = true
            }
        };
        
        // METAPHASE: Line up/alignment gestures
        phaseGestures[GameManager.GameState.Metaphase] = new List<GestureKeyword>
        {
            new GestureKeyword
            {
                phrase = "line up",
                gestureName = "DZ20 (Line Up)",
                action = () => animatorDriver?.TriggerDZ20(),
                wordOffset = 0.2f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "at the center",
                gestureName = "DZ20 (Line Up)",
                action = () => animatorDriver?.TriggerDZ20(),
                wordOffset = 0.0f
            },
            new GestureKeyword
            {
                phrase = "in the middle",
                gestureName = "DZ20 (Line Up)",
                action = () => animatorDriver?.TriggerDZ20(),
                wordOffset = 0.0f
            }
        };
        
        // ANAPHASE: Splitting/separation gestures
        phaseGestures[GameManager.GameState.Anaphase] = new List<GestureKeyword>
        {
            new GestureKeyword
            {
                phrase = "move to opposite",
                gestureName = "DZ22 (Split Outward)",
                action = () => animatorDriver?.TriggerDZ22(),
                wordOffset = 0.3f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "pull apart",
                gestureName = "DZ22 (Split Outward)",
                action = () => animatorDriver?.TriggerDZ22(),
                wordOffset = 0.2f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "opposite ends",
                gestureName = "DZ22 (Split Outward)",
                action = () => animatorDriver?.TriggerDZ22(),
                wordOffset = 0.1f
            }
        };
        
        // TELOPHASE: Cell division gestures
        phaseGestures[GameManager.GameState.Telophase] = new List<GestureKeyword>
        {
            new GestureKeyword
            {
                phrase = "split",
                gestureName = "DZ15 (Split)",
                action = () => animatorDriver?.TriggerDZ15(),
                wordOffset = 0.2f
            },
            new GestureKeyword
            {
                phrase = "divide",
                gestureName = "DZ15 (Split)",
                action = () => animatorDriver?.TriggerDZ15(),
                wordOffset = 0.2f
            }
        };
    }
    
    string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        
        // Convert to lowercase and remove punctuation
        text = text.ToLower();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s]", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }
    
    /// <summary>
    /// Stop all gesture playback
    /// </summary>
    public void StopGesturePlayback()
    {
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }
        scheduledGestures.Clear();
        if (verboseLogging) Debug.Log("[WhisperGesture] Gesture playback stopped");
    }
    
    void OnDisable()
    {
        StopGesturePlayback();
    }
    
    // Helper classes
    [System.Serializable]
    class ScheduledGesture
    {
        public float triggerTime;
        public string gestureName;
        public string phrase;
        public System.Action triggerAction;
    }
    
    class GestureKeyword
    {
        public string phrase;
        public string gestureName;
        public System.Action action;
        public float wordOffset = 0f;
        public bool triggerOnLastWord = false;
    }
}
