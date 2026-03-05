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
    public float gestureTimingOffset = 0.05f;

    [Tooltip("Ignore duplicate scheduling calls for the same clip within this window")]
    public float duplicateClipWindowSeconds = 0.35f;

    [Tooltip("Merge near-identical gesture schedules inside one utterance (seconds)")]
    public float scheduleMergeWindowSeconds = 0.75f;

    [Tooltip("Suppress repeated firing of the same gesture within this cooldown (seconds)")]
    public float sameGestureCooldownSeconds = 0.9f;
    
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
    private Coroutine processingCoroutine;
    private int scheduleRequestId;
    private string lastScheduledClipKey;
    private float lastScheduledClipTime = -999f;
    private readonly Dictionary<string, float> lastGestureFiredAt = new Dictionary<string, float>();
    
    // Gesture definitions by phase
    private Dictionary<GameManager.GameState, List<GestureKeyword>> phaseGestures;
    
    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Whisper is too slow on Quest 3 mobile hardware (~2 minutes to transcribe 10s audio)
        // Gestures fire long after speech ends. Disable and let GPTConnector handle gestures via text.
        Debug.LogWarning("[WhisperGesture] ⚠️ Whisper disabled on Android - transcription too slow for real-time gestures.");
        Debug.LogWarning("[WhisperGesture] GPT text-based gestures will be used instead (GPTConnector handles this).");
        enabled = false;
        return;
#endif

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
        Debug.Log("[WhisperGesture] 🚀 ===== WHISPER INITIALIZATION START =====");
        if (verboseLogging) Debug.Log($"[WhisperGesture] Platform: {Application.platform}, Streaming Assets: {Application.streamingAssetsPath}");

        whisperManager = GetComponent<WhisperManager>();
        if (!whisperManager)
        {
            whisperManager = FindAnyObjectByType<WhisperManager>();
        }

        if (!whisperManager)
        {
            Debug.Log("[WhisperGesture] ⚙️ No WhisperManager found. Creating new one...");
            whisperManager = gameObject.AddComponent<WhisperManager>();
            if (verboseLogging) Debug.Log("[WhisperGesture] ✓ Auto-added WhisperManager component");
            yield return null;
        }
        else
        {
            Debug.Log("[WhisperGesture] ✓ WhisperManager found");
        }

        if (whisperManager.IsLoading)
        {
            Debug.Log("[WhisperGesture] ⏳ WhisperManager is already loading. Waiting for completion...");
            float timeout = 0f;
            float maxWait = 30f;
            while (whisperManager.IsLoading && timeout < maxWait)
            {
                timeout += Time.deltaTime;
                yield return null;
            }
            if (timeout >= maxWait)
            {
                Debug.LogError("[WhisperGesture] ❌ Whisper loading timeout after 30 seconds!");
                initializationStarted = false;
                yield break;
            }
        }

        if (whisperManager.IsLoaded)
        {
            isInitialized = true;
            pendingInitWarningLogged = false;
            Debug.Log("[WhisperGesture] ✅✅✅ WHISPER ALREADY LOADED AND READY ✅✅✅");
            yield break;
        }

        var resolved = ResolveModelPathForWhisper(whisperModelPath);
        Debug.Log($"[WhisperGesture] 📂 Model Path Resolution:");
        Debug.Log($"  Config: '{whisperModelPath}'");
        Debug.Log($"  Resolved: '{resolved.resolvedPath}'");
        Debug.Log($"  In StreamingAssets: {resolved.isInStreamingAssets}");
        Debug.Log($"  StreamingAssets Path: '{Application.streamingAssetsPath}'");
        
        // Check if file exists (diagnostic for APK issues)
        if (resolved.isInStreamingAssets)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, resolved.resolvedPath);
            Debug.Log($"[WhisperGesture] 🔍 Checking model file exists at: {fullPath}");
#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, StreamingAssets are in a compressed archive, check differently
            StartCoroutine(CheckAndroidFileExists(resolved.resolvedPath));
#else
            if (File.Exists(fullPath))
            {
                Debug.Log($"[WhisperGesture] ✅ Model file EXISTS ({new FileInfo(fullPath).Length / 1024 / 1024:F1} MB)");
            }
            else
            {
                Debug.LogError($"[WhisperGesture] ❌❌❌ MODEL FILE DOES NOT EXIST! Check if it's included in build!");
            }
#endif
        }

        whisperManager.ModelPath = resolved.resolvedPath;
        whisperManager.IsModelPathInStreamingAssets = resolved.isInStreamingAssets;
        whisperManager.language = string.IsNullOrEmpty(language) ? "en" : language;
        whisperManager.enableTokens = enableWordTimestamps;

        Debug.Log("[WhisperGesture] 🔄 Starting model initialization...");
        var initTask = whisperManager.InitModel();
        
        float elapsed = 0f;
        float maxInitTime = 60f; // 60 second max init time
        while (!initTask.IsCompleted && elapsed < maxInitTime)
        {
            elapsed += Time.deltaTime;
            if (elapsed % 5 < Time.deltaTime)
            {
                Debug.Log($"[WhisperGesture] ⏳ Initializing... ({elapsed:F1}s / {maxInitTime}s)");
            }
            yield return null;
        }
        
        if (initTask.IsFaulted)
        {
            Debug.LogError($"[WhisperGesture] ❌❌❌ INITIALIZATION FAILED ❌❌❌");
            Debug.LogError($"[WhisperGesture] Exception: {initTask.Exception}");
            if (initTask.Exception.InnerException != null)
            {
                Debug.LogError($"[WhisperGesture] Inner Exception: {initTask.Exception.InnerException}");
            }
            initializationStarted = false;
            yield break;
        }

        if (!whisperManager.IsLoaded)
        {
            Debug.LogError("[WhisperGesture] ❌ Init completed but IsLoaded is FALSE. Checking if this is a timeout or load issue...");
            if (elapsed >= maxInitTime)
            {
                Debug.LogError($"[WhisperGesture] ❌ TIMEOUT: Took longer than {maxInitTime}s to load model. Model file may be too large or missing.");
            }
            initializationStarted = false;
            yield break;
        }
        
        isInitialized = true;
        pendingInitWarningLogged = false;
        Debug.Log("[WhisperGesture] ✅✅✅ WHISPER INITIALIZATION COMPLETE ✅✅✅");

        if (hasPendingClip && pendingClip != null)
        {
            var clipToProcess = pendingClip;
            pendingClip = null;
            hasPendingClip = false;

            Debug.Log($"[WhisperGesture] 🎬 Processing queued first clip after init: {clipToProcess.name}");
            TranscribeAndScheduleGestures(clipToProcess);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    IEnumerator CheckAndroidFileExists(string relativePath)
    {
        string url = Path.Combine(Application.streamingAssetsPath, relativePath);
        Debug.Log($"[WhisperGesture] 🔍 Checking Android file via UnityWebRequest: {url}");
        
        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Head(url))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string contentLength = www.GetResponseHeader("Content-Length");
                if (!string.IsNullOrEmpty(contentLength) && long.TryParse(contentLength, out long bytes))
                {
                    Debug.Log($"[WhisperGesture] ✅ Model file EXISTS in APK ({bytes / 1024 / 1024:F1} MB)");
                }
                else
                {
                    Debug.Log($"[WhisperGesture] ✅ Model file EXISTS in APK (size unknown)");
                }
            }
            else
            {
                Debug.LogError($"[WhisperGesture] ❌❌❌ MODEL FILE NOT FOUND IN APK! Error: {www.error}");
                Debug.LogError("[WhisperGesture] ❌ The Whisper model was NOT included in the APK build!");
                Debug.LogError("[WhisperGesture] ❌ Make sure Assets/StreamingAssets/Whisper/ggml-tiny.bin exists and is included in build.");
            }
        }
    }
#endif

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

        string clipKey = BuildClipKey(clip);
        float nowRealtime = Time.realtimeSinceStartup;
        if (clipKey == lastScheduledClipKey && (nowRealtime - lastScheduledClipTime) <= duplicateClipWindowSeconds)
        {
            Debug.Log($"[WhisperGesture] ⏭️ Skipping duplicate schedule for '{clip.name}' ({nowRealtime - lastScheduledClipTime:F2}s apart)");
            return;
        }

        lastScheduledClipKey = clipKey;
        lastScheduledClipTime = nowRealtime;

        if (!isInitialized)
        {
            Debug.Log("[WhisperGesture] ⏳ Whisper not ready yet, checking for existing WhisperManager...");
            TryAdoptLoadedWhisperManager();
        }

        if (!isInitialized)
        {
            Debug.LogWarning($"[WhisperGesture] ⚠️ Whisper still not initialized, queueing clip for later: {clip.name}");
            pendingClip = clip;
            hasPendingClip = true;
            EnsureInitializationStarted();

            if (!pendingInitWarningLogged)
            {
                Debug.LogWarning($"[WhisperGesture] ⚠️ Whisper initialization in progress. Gesture sync will happen after initialization completes.");
                pendingInitWarningLogged = true;
            }
            return;
        }
        
        if (!animatorDriver)
        {
            Debug.LogError("[WhisperGesture] ❌ No TTSAnimatorDriver assigned. Cannot schedule gestures!");
            return;
        }

        Debug.Log($"[WhisperGesture] 🎬 Starting Whisper transcription for: {clip.name} ({clip.length:F2}s)");

        scheduleRequestId++;

        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }

        processingCoroutine = StartCoroutine(ProcessAudioClip(clip, scheduleRequestId));
    }

    string BuildClipKey(AudioClip clip)
    {
        return $"{clip.GetInstanceID()}_{clip.samples}_{clip.frequency}_{clip.channels}_{clip.length:F3}";
    }

    IEnumerator ProcessAudioClip(AudioClip clip, int requestId)
    {
        Debug.Log($"[WhisperGesture] 📝 ProcessAudioClip START: {clip.name} ({clip.length:F2}s, {clip.frequency}Hz, {clip.channels}ch, {clip.samples} samples)");
        
        // Clear previous scheduled gestures
        scheduledGestures.Clear();
        
        // Transcribe with word timestamps
        Debug.Log($"[WhisperGesture] 🔄 Calling whisperManager.GetTextAsync()...");
        System.Threading.Tasks.Task<WhisperResult> transcriptionTask = null;
        
        try
        {
            transcriptionTask = whisperManager.GetTextAsync(clip);
            Debug.Log($"[WhisperGesture] ✓ GetTextAsync() called successfully, waiting for completion...");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WhisperGesture] ❌ Exception calling GetTextAsync: {e.Message}\n{e.StackTrace}");
            yield break;
        }
        
        // Wait for transcription with timeout and periodic logging
        float elapsed = 0f;
        float timeout = 30f; // 30 second timeout
        float lastLogTime = 0f;
        
        while (!transcriptionTask.IsCompleted && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            
            // Log every 2 seconds
            if (elapsed - lastLogTime >= 2f)
            {
                Debug.Log($"[WhisperGesture] ⏳ Transcribing... {elapsed:F1}s / {timeout}s (Status: {transcriptionTask.Status})");
                lastLogTime = elapsed;
            }
            
            yield return null;
        }
        
        if (!transcriptionTask.IsCompleted)
        {
            Debug.LogError($"[WhisperGesture] ❌ TRANSCRIPTION TIMEOUT after {elapsed:F1}s! Task status: {transcriptionTask.Status}");
            yield break;
        }
        
        Debug.Log($"[WhisperGesture] ✓ Transcription completed in {elapsed:F1}s");

        if (requestId != scheduleRequestId)
        {
            Debug.Log("[WhisperGesture] ⏭️ Ignoring stale transcription result (newer request exists)");
            yield break;
        }
        
        if (transcriptionTask.IsFaulted)
        {
            Debug.LogError($"[WhisperGesture] ❌ Transcription FAULTED!");
            if (transcriptionTask.Exception != null)
            {
                Debug.LogError($"[WhisperGesture] Exception: {transcriptionTask.Exception}");
                if (transcriptionTask.Exception.InnerException != null)
                {
                    Debug.LogError($"[WhisperGesture] Inner: {transcriptionTask.Exception.InnerException}");
                }
            }
            yield break;
        }
        
        WhisperResult result = transcriptionTask.Result;
        if (result == null)
        {
            Debug.LogError("[WhisperGesture] ❌ Transcription result is NULL!");
            yield break;
        }
        
        string fullText = result.Result;
        Debug.Log($"[WhisperGesture] 📄 Transcription text: '{fullText}'");
        
        // Get word-level segments (requires Whisper with timestamp support)
        var segments = result.Segments;
        if (segments != null && segments.Count > 0)
        {
            Debug.Log($"[WhisperGesture] ✓ Got {segments.Count} word segments, processing...");
            ProcessSegmentsAndScheduleGestures(segments, fullText);
        }
        else
        {
            Debug.LogWarning("[WhisperGesture] ⚠️ No word segments available. Falling back to text-only analysis.");
            AnalyzeTextAndScheduleGestures(fullText, clip.length);
        }

        scheduledGestures = DeduplicateScheduledGestures(scheduledGestures);

        // Start gesture playback coroutine
        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
        var snapshot = scheduledGestures.Select(g => g.Clone()).ToList();
        playbackCoroutine = StartCoroutine(PlaybackScheduledGestures(snapshot, requestId));
        processingCoroutine = null;
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
        
        var timedWords = BuildTimedWords(segments);
        
        // Track which gestures have been scheduled in this response to prevent duplicates
        var scheduledGestureNames = new HashSet<string>();

        // Analyze segments for keyword matches
        foreach (var keyword in keywords)
        {
            // Check if this gesture is already scheduled in this response
            if (scheduledGestureNames.Contains(keyword.gestureName))
            {
                if (verboseLogging)
                {
                    Debug.Log($"[WhisperGesture] Skipping duplicate gesture in response: {keyword.gestureName}");
                }
                continue;
            }
            
            // Check if keyword phrase exists in full text (loose check: all phrase words must be present)
            if (!ContainsAllWords(normalizedFull, NormalizeText(keyword.phrase)))
                continue;
            
            // Match phrase against estimated per-word timeline (finer than segment-only timing)
            var matchingWordTimes = FindPhraseWordTimes(timedWords, keyword.phrase);

            if (matchingWordTimes.Count > 0)
            {
                // Calculate timing based on phrase position
                float triggerTime = CalculateTriggerTime(matchingWordTimes, keyword);
                
                scheduledGestures.Add(new ScheduledGesture
                {
                    triggerTime = triggerTime + gestureTimingOffset,
                    gestureName = keyword.gestureName,
                    phrase = keyword.phrase,
                    triggerAction = keyword.action
                });
                
                scheduledGestureNames.Add(keyword.gestureName);
                
                if (verboseLogging)
                {
                    Debug.Log($"[WhisperGesture] ⏰ Scheduled {keyword.gestureName} at {triggerTime + gestureTimingOffset:F2}s for phrase: '{keyword.phrase}'");
                }
            }
        }
        
        // Sort gestures by trigger time
        scheduledGestures = scheduledGestures.OrderBy(g => g.triggerTime).ToList();
    }

    List<TimedWord> BuildTimedWords(List<WhisperSegment> segments)
    {
        var words = new List<TimedWord>();

        foreach (var seg in segments)
        {
            string normalizedSegment = NormalizeText(seg.Text);
            if (string.IsNullOrWhiteSpace(normalizedSegment))
                continue;

            var segmentWords = normalizedSegment.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (segmentWords.Length == 0)
                continue;

            float start = (float)seg.Start.TotalSeconds;
            float end = (float)seg.End.TotalSeconds;
            float duration = Mathf.Max(0.01f, end - start);

            for (int i = 0; i < segmentWords.Length; i++)
            {
                float t = start + duration * ((i + 0.5f) / segmentWords.Length);
                words.Add(new TimedWord { word = segmentWords[i], time = t });
            }
        }

        return words;
    }

    List<float> FindPhraseWordTimes(List<TimedWord> timedWords, string phrase)
    {
        var result = new List<float>();
        string normalizedPhrase = NormalizeText(phrase);
        string[] phraseWords = normalizedPhrase.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        if (phraseWords.Length == 0 || timedWords.Count == 0)
            return result;

        // First try exact consecutive match
        for (int i = 0; i <= timedWords.Count - phraseWords.Length; i++)
        {
            bool allMatch = true;
            for (int j = 0; j < phraseWords.Length; j++)
            {
                if (timedWords[i + j].word != phraseWords[j])
                {
                    allMatch = false;
                    break;
                }
            }

            if (!allMatch)
                continue;

            for (int j = 0; j < phraseWords.Length; j++)
            {
                result.Add(timedWords[i + j].time);
            }
            return result;
        }

        // Fallback: loose match (words in order, but non-consecutive; allows intervening words)
        int wordIdx = 0;
        var looseMatch = new List<float>();
        for (int i = 0; i < timedWords.Count && wordIdx < phraseWords.Length; i++)
        {
            if (timedWords[i].word == phraseWords[wordIdx])
            {
                looseMatch.Add(timedWords[i].time);
                wordIdx++;
            }
        }

        // Only return loose match if all phrase words were found
        if (wordIdx >= phraseWords.Length)
        {
            return looseMatch;
        }

        return result; // Empty result if no match
    }

    float CalculateTriggerTime(List<float> matchedWordTimes, GestureKeyword keyword)
    {
        if (matchedWordTimes.Count == 0) return 0f;

        // Use the start time of the phrase, optionally with keyword-specific offset
        float baseTime = matchedWordTimes[0];

        // For multi-word phrases, optionally trigger on the last word for better sync
        if (keyword.triggerOnLastWord && matchedWordTimes.Count > 1)
        {
            baseTime = matchedWordTimes[matchedWordTimes.Count - 1];
        }

        return baseTime + keyword.wordOffset;
    }

    bool ContainsAllWords(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
            return false;

        var phraseWords = phrase.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        var textWords = text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var phraseWord in phraseWords)
        {
            if (!textWords.Contains(phraseWord))
                return false;
        }

        return true;
    }
    
    void AnalyzeTextAndScheduleGestures(string text, float clipLength)
    {
        // Fallback: Analyze text without timestamps, distribute gestures evenly
        var currentPhase = GameManager.eGameStatus;
        if (!enablePhaseGestures || !phaseGestures.ContainsKey(currentPhase))
            return;
        
        var keywords = phaseGestures[currentPhase];
        string normalized = NormalizeText(text);
        
        // Track which gestures have been scheduled in this response to prevent duplicates
        var scheduledGestureNames = new HashSet<string>();
        
        foreach (var keyword in keywords)
        {
            // Check if this gesture is already scheduled in this response
            if (scheduledGestureNames.Contains(keyword.gestureName))
            {
                if (verboseLogging)
                {
                    Debug.Log($"[WhisperGesture] Skipping duplicate gesture in response: {keyword.gestureName}");
                }
                continue;
            }
            
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
                
                scheduledGestureNames.Add(keyword.gestureName);
                
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
    
    List<ScheduledGesture> DeduplicateScheduledGestures(List<ScheduledGesture> gestures)
    {
        if (gestures == null || gestures.Count <= 1)
            return gestures ?? new List<ScheduledGesture>();

        var ordered = gestures.OrderBy(g => g.triggerTime).ToList();
        var deduped = new List<ScheduledGesture>();

        foreach (var gesture in ordered)
        {
            bool isNearDuplicate = deduped.Any(existing =>
                existing.gestureName == gesture.gestureName &&
                Mathf.Abs(existing.triggerTime - gesture.triggerTime) <= scheduleMergeWindowSeconds);

            if (isNearDuplicate)
            {
                if (verboseLogging)
                {
                    Debug.Log($"[WhisperGesture] Skipping near-duplicate schedule: {gesture.gestureName} at {gesture.triggerTime:F2}s (phrase: '{gesture.phrase}')");
                }
                continue;
            }

            deduped.Add(gesture);
        }

        return deduped;
    }

    IEnumerator PlaybackScheduledGestures(List<ScheduledGesture> gestures, int requestId)
    {
        if (gestures.Count == 0)
        {
            if (verboseLogging) Debug.Log("[WhisperGesture] No gestures scheduled");
            yield break;
        }
        
        if (verboseLogging)
        {
            Debug.Log($"[WhisperGesture] 🎬 Starting gesture playback, {gestures.Count} gestures scheduled");
        }
        
        float startTime = Time.time;
        int gestureIndex = 0;
        
        while (gestureIndex < gestures.Count)
        {
            if (requestId != scheduleRequestId)
            {
                if (verboseLogging) Debug.Log("[WhisperGesture] Stopping stale playback (newer request exists)");
                yield break;
            }

            float elapsed = Time.time - startTime;
            var gesture = gestures[gestureIndex];
            
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
        string gestureKey = gesture.gestureName ?? string.Empty;
        float now = Time.time;
        if (lastGestureFiredAt.TryGetValue(gestureKey, out float previousTime) &&
            now - previousTime <= sameGestureCooldownSeconds)
        {
            if (verboseLogging)
            {
                Debug.Log($"[WhisperGesture] Skipping duplicate trigger for {gesture.gestureName} ({now - previousTime:F2}s apart)");
            }
            return;
        }

        lastGestureFiredAt[gestureKey] = now;

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
                wordOffset = 0.1f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "get energy",
                gestureName = "DZ13 (Eat)",
                action = () => animatorDriver?.TriggerDZ13(),
                wordOffset = 0.05f
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
                wordOffset = 0.1f
            },
            new GestureKeyword
            {
                phrase = "x shape",
                gestureName = "DZ18 (X-Shape Condense)",
                action = () => animatorDriver?.TriggerDZ18(),
                wordOffset = 0.05f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "condense into x",
                gestureName = "DZ18 (X-Shape Condense)",
                action = () => animatorDriver?.TriggerDZ18(),
                wordOffset = 0.15f,
                triggerOnLastWord = true
            }
        };
        
        // METAPHASE: Line up/alignment gestures
        phaseGestures[GameManager.GameState.Metaphase] = new List<GestureKeyword>
        {
            new GestureKeyword
            {
                phrase = "line up",
                gestureName = "DZ12 (Line Up)",
                action = () => animatorDriver?.TriggerDZ12(),
                wordOffset = 0.1f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "at the center",
                gestureName = "DZ12 (Line Up)",
                action = () => animatorDriver?.TriggerDZ12(),
                wordOffset = 0.0f
            },
            new GestureKeyword
            {
                phrase = "in the middle",
                gestureName = "DZ12 (Line Up)",
                action = () => animatorDriver?.TriggerDZ12(),
                wordOffset = 0.0f
            }
        };
        
        // ANAPHASE: Splitting/separation gestures
        phaseGestures[GameManager.GameState.Anaphase] = new List<GestureKeyword>
        {
            new GestureKeyword
            {
                phrase = "split",
                gestureName = "DZ22 (Split Outward)",
                action = () => animatorDriver?.TriggerDZ22(),
                wordOffset = 0.05f,
                triggerOnLastWord = false
            },
            new GestureKeyword
            {
                phrase = "move to opposite",
                gestureName = "DZ22 (Split Outward)",
                action = () => animatorDriver?.TriggerDZ22(),
                wordOffset = 0.15f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "pull apart",
                gestureName = "DZ22 (Split Outward)",
                action = () => animatorDriver?.TriggerDZ22(),
                wordOffset = 0.1f,
                triggerOnLastWord = true
            },
            new GestureKeyword
            {
                phrase = "opposite ends",
                gestureName = "DZ22 (Split Outward)",
                action = () => animatorDriver?.TriggerDZ22(),
                wordOffset = 0.05f
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
                wordOffset = 0.1f
            },
            new GestureKeyword
            {
                phrase = "divide",
                gestureName = "DZ15 (Split)",
                action = () => animatorDriver?.TriggerDZ15(),
                wordOffset = 0.1f
            }
        };
    }
    
    string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        text = text.ToLower();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s]", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        if (string.IsNullOrEmpty(text)) return "";

        var tokens = text
            .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(CanonicalizeToken)
            .ToArray();

        return string.Join(" ", tokens);
    }

    string CanonicalizeToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return "";

        switch (token)
        {
            // Eat variants
            case "eating": return "eat";
            case "ate": return "eat";
            
            // Get variants
            case "getting": return "get";
            case "got": return "get";
            case "gathered": return "gather";
            case "gathering": return "gather";
            
            // Condense variants
            case "condense": return "condense";
            case "condenses": return "condense";
            case "condensing": return "condense";
            case "condensed": return "condense";
            
            // Line variants
            case "line": return "line";
            case "lines": return "line";
            case "lining": return "line";
            case "lined": return "line";
            
            // Pull variants
            case "pulling": return "pull";
            case "pulled": return "pull";
            
            // Move variants
            case "moves": return "move";
            case "moved": return "move";
            case "moving": return "move";
            
            // Shape variants
            case "shape": return "shape";
            case "shaped": return "shape";
            case "shapes": return "shape";
            
            // Split variants
            case "splitting": return "split";
            case "divide": return "divide";
            case "dividing": return "divide";
            case "divided": return "divide";
            
            // Opposite variants
            case "opposites": return "opposite";
            
            // Spatial variants
            case "poles": return "ends";
            case "pole": return "ends";
            case "opposingends": return "opposite";
            
            default: return token;
        }
    }
    
    /// <summary>
    /// Stop all gesture playback
    /// </summary>
    public void StopGesturePlayback()
    {
        scheduleRequestId++;

        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

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

        public ScheduledGesture Clone()
        {
            return new ScheduledGesture
            {
                triggerTime = triggerTime,
                gestureName = gestureName,
                phrase = phrase,
                triggerAction = triggerAction
            };
        }
    }

    struct TimedWord
    {
        public string word;
        public float time;
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
