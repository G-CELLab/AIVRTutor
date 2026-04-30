using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;

public class OpenAISpeechRecognizer : MonoBehaviour
{
    [Header("OpenAI")]
    public string openAIKey = "";                 // ⚠️ Do NOT hardcode API key; set it in the Inspector
    public GPTConnector gptConnector;             // If assigned, hand audio to GPTConnector after capture/transcription
    [Tooltip("If enabled, send captured audio to GPTConnector after transcription")]
    public bool sendAudioToGptConnector = true;

    [Header("Transcription")]
    [Tooltip("Transcribe user audio before sending so User_Speech logs contain the actual utterance")]
    public bool transcribeBeforeSend = true;
    [Tooltip("OpenAI transcription model used for user speech logging")]
    public string transcriptionModel = "gpt-4o-mini-transcribe";

    [Header("Record Settings")]
    public int sampleRate = 16000;                // Quest 3 recommended 48000
    public int maxRecordTime = 30;                // Maximum single-record duration (seconds)
    public float minRecordTime = 0.6f;            // Minimum valid recording duration (seconds)
    public float preRollSeconds = 0.2f;           // Pre-roll buffer before start point (seconds)
    [Tooltip("Cooldown after sending an utterance before listening for the next one (seconds)")]
    public float recordCooldownSec = 0.8f;

    [Header("Voice Activity Detection")]
    [Tooltip("Volume threshold to consider 'start speaking' (0~1)")]
    public float startThreshold = 0.02f;
    [Tooltip("How long volume must stay above startThreshold to mark speech start")]
    public float startHoldTime = 0.35f; // Require 350ms above threshold to trigger VAD (shorter for snappier replies)
    [Tooltip("Volume threshold to consider 'stop speaking' (recommend slightly below start threshold)")]
    public float stopThreshold = 0.015f;
    [Tooltip("Silence duration after speech start to consider the utterance ended")]
    public float stopSilenceTime = 1.0f;

    [Header("TTS Interrupt")]
    [Tooltip("TTS player to interrupt (optional; OnUserSpeechLikely still broadcast if null)")]
    public TextToSpeechPlayer ttsToInterrupt;
    [Tooltip("Allow listening and barge-in while AI is speaking")]
    public bool allowBargeIn = true;
    [Tooltip("Even before VAD triggers, a short peak above threshold can hard-interrupt TTS (DISABLED for uninterruptible agent)")]
    public bool interruptEvenBeforeVAD = true; // ENABLED: allow fast hard-interrupts from user during TTS
    [Tooltip("Instant threshold for hard interrupt (0~1, adjust per device)")]
    public float interruptThreshold = 0.03f;
    [Tooltip("Minimum duration (ms) the hard-interrupt threshold must hold")]
    public float interruptGraceMs = 700f;
    [Tooltip("Stop all registered TTS on interrupt (recommended when multiple TTS exist)")]
    public bool killAllTTSOnInterrupt = true;
    [Tooltip("Protection window after TTS starts during which user audio won't interrupt (seconds)")]
    public float ttsProtectionDurationSec = 0.5f;
    [Tooltip("Minimum interval between interrupt triggers to avoid duplicate barge-in calls")]
    public float interruptCooldownSec = 0.5f;

    // Public events: invoked when user likely starts speaking (subscribed by TTS)
    public event Action OnUserSpeechLikely;
    // Public events: invoked after transcription is ready (subscribed by NewAI/AITutor)
    public event Action<string> OnTranscriptReady;

    [Header("Debug")]
    public bool showDebugOverlay = true;
    [Tooltip("Enable verbose debug logging")]
    public bool verboseDebug = true;
    [Tooltip("Throttle: minimum interval between logs (seconds)")]
    public float debugLogInterval = 0.25f;

    // 内部状态
    private string microphoneName;
    private AudioClip micClip;
    private bool isRunning;
    private float interruptAboveTimer = 0f;   // timer for how long peak remains above threshold (seconds)
    private float lastBatchMax = 0f;          // debug display: max of the most recent batch
    private GUIStyle _g;
    private float _ttsStartTime = -999f;      // Track when TTS started to prevent self-interrupt
    private float _lastInterruptTriggerAt = -999f;

    // 统计/节流
    private float _nextLogTime = 0f;
    private float _globalMax = 0f;
    private int _overStartCount = 0;
    private int _overStopCount = 0;
    private int _overInterruptCount = 0;

    // ========= Lifecycle =========
    void Start()
    {
        D("[Init] Start() entered");
        StartCoroutine(InitMicrophoneAndStartLoop());
    }

    private IEnumerator InitMicrophoneAndStartLoop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Permission should have been requested at app startup via PermissionManager
        // Wait a frame to ensure permission state is updated
        yield return null;
        
        if (!PermissionManager.HasMicrophonePermission())
        {
            Debug.LogError("[SpeechRecognizer] ❌ Microphone permission NOT granted. Waiting next frame to retry...");
            // Wait a bit and try again once
            yield return new WaitForSeconds(1f);
            if (!PermissionManager.HasMicrophonePermission())
            {
                Debug.LogError("[SpeechRecognizer] ❌ Microphone permission STILL not granted. User must enable in Quest settings.");
                enabled = false;
                yield break;
            }
        }
        Debug.Log("[SpeechRecognizer] ✅ Microphone permission verified - proceeding with initialization");
#endif

        // 打印设备列表
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("❌ No microphone detected");
            yield break;
        }
        else
        {
            for (int i = 0; i < Microphone.devices.Length; i++)
                D($"[Mic] Device[{i}]: {Microphone.devices[i]}");
        }

        microphoneName = Microphone.devices[0];
        D($"[Mic] Using device: {microphoneName}");

        StartCoroutine(RecordingLoop()); // non-blocking main loop: continuously listen and process
    }

    void OnGUI()
    {
        if (!showDebugOverlay) return;
        if (_g == null)
        {
            _g = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft };
            _g.normal.textColor = Color.white;
        }
        var msg =
            $"Mic max: {lastBatchMax:F4}\n" +
            $"Speaking: {(ttsToInterrupt != null && ttsToInterrupt.IsSpeaking)}\n" +
            $"Intr>thr(ms): {(int)(interruptAboveTimer * 1000f)} / {(int)interruptGraceMs}";
        GUI.Box(new Rect(10, 10, 260, 70), msg, _g);
    }

    // ========= Main loop: record one utterance -> process asynchronously -> continue listening =========
    private IEnumerator RecordingLoop()
    {
        D("[Loop] Entering RecordingLoop()");
        isRunning = true;
        float lastUtteranceTime = -999f;
            float cooldownSec = recordCooldownSec; // configurable cooldown after each utterance
        while (isRunning)
        {
            // Block recording while agent is busy (unless barge-in is enabled)
            if (!allowBargeIn && gptConnector != null && gptConnector.IsAgentBusy)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }
            string wavPath = null;
            _globalMax = 0f; _overStartCount = 0; _overStopCount = 0; _overInterruptCount = 0;

            // Record a segment (start+end) and save to wavPath
            yield return StartCoroutine(RecordUtteranceAndSave(path => { wavPath = path; Debug.Log($"[RecCallback] wavPath assigned: {path}"); }));

            if (!string.IsNullOrEmpty(wavPath))
            {
                D($"[Loop] Segment saved: {wavPath}");
                // 立刻异步处理（直接把音频送给 GPTConnector），不阻塞录音主循环
                StartCoroutine(ProcessUtterance(wavPath));
                lastUtteranceTime = Time.realtimeSinceStartup;
            }
            else
            {
                D("[Loop] Segment save failed / no valid speech, continuing");
            }

            // Cooldown after sending utterance
            while (Time.realtimeSinceStartup - lastUtteranceTime < cooldownSec)
            {
                yield return null;
            }
        }
    }

    // ========= Process a single utterance: (optionally transcribe) then send to GPT =========
    private IEnumerator ProcessUtterance(string wavPath)
    {
        if (sendAudioToGptConnector && gptConnector == null)
        {
            Debug.LogWarning("[SpeechRecognizer] sendAudioToGptConnector=true but GPTConnector is not assigned.");
        }
        // Check minimum audio duration before sending
        if (!string.IsNullOrEmpty(wavPath))
        {
            try {
                var wavBytes = File.ReadAllBytes(wavPath);
                // For 16kHz mono 16-bit PCM, 120ms = 1920 samples = 3840 bytes + 44 byte header
                if (wavBytes.Length < 3900)
                {
                    Debug.LogWarning($"[SpeechRecognizer] Skipping too-short utterance: {wavBytes.Length} bytes");
                    yield break;
                }
            } catch (Exception e) {
                Debug.LogWarning($"[SpeechRecognizer] Failed to check audio file: {e.Message}");
            }
        }
        D($"[Send] Processing utterance audio: {wavPath}");

        string transcriptContext = null;
        if (transcribeBeforeSend)
        {
            D("[ProcessUtterance] Starting nested SendAudioToOpenAI coroutine...");
            yield return StartCoroutine(SendAudioToOpenAI(wavPath, text => {
                    D($"[ProcessUtterance] SendAudioToOpenAI callback received: {(string.IsNullOrWhiteSpace(text) ? "(null)" : text)}");
                transcriptContext = text;
            }));
            D("[ProcessUtterance] Nested SendAudioToOpenAI coroutine completed");
            D($"[ProcessUtterance] transcriptContext value: {(string.IsNullOrWhiteSpace(transcriptContext) ? "(null)" : transcriptContext)}");
            
            if (!string.IsNullOrWhiteSpace(transcriptContext))
            {
                D($"[STT][user] {transcriptContext}");
                Debug.Log($"[SpeechRecognizer] OnTranscriptReady about to invoke with transcript: {transcriptContext}");
                OnTranscriptReady?.Invoke(transcriptContext);
                Debug.Log("[SpeechRecognizer] OnTranscriptReady invocation completed");
            }
            else
            {
                D("[ProcessUtterance] WARNING: transcriptContext is null after SendAudioToOpenAI");
            }
        }

        // Check audio length before sending (must be at least 100ms for 16kHz = 1600 samples, for 24kHz = 2400 samples)
        D("[ProcessUtterance] Checking audio file size...");
        try {
            var wavBytes = File.ReadAllBytes(wavPath);
            D($"[ProcessUtterance] Audio file size: {wavBytes.Length} bytes");
            // crude check: look for at least 3200 bytes (16-bit mono, 1600 samples)
            if (wavBytes.Length < 4000) {
                D($"[ProcessUtterance] WARNING: Audio buffer too small ({wavBytes.Length} bytes), not sending to GPTConnector");
                yield break;
            }
        } catch (Exception e) {
            D($"[ProcessUtterance] ERROR checking audio file size: {e.GetType().Name} - {e.Message}");
        }
        
        D("[ProcessUtterance] About to send audio to GPTConnector...");
        if (sendAudioToGptConnector && gptConnector != null)
        {
            D($"[ProcessUtterance] Calling gptConnector.SendAudioFileToGPT with transcript: {(string.IsNullOrWhiteSpace(transcriptContext) ? "(null)" : transcriptContext)}");
            gptConnector.SendAudioFileToGPT(wavPath, transcriptContext, null);
        }
        else
        {
            D($"[ProcessUtterance] GPTConnector call skipped (sendAudioToGptConnector={sendAudioToGptConnector}, gptConnector={(gptConnector != null)})");
        }
        
        D("[ProcessUtterance] Coroutine completing normally");
        yield return null;
    }

    // ========= Actual recording + VAD (includes hard-interrupt handling for TTS) =========
    private IEnumerator RecordUtteranceAndSave(Action<string> onSaved)
    {
        D($"[Rec] Microphone.Start(name={microphoneName}, rate={sampleRate})");
        micClip = Microphone.Start(microphoneName, true, maxRecordTime, sampleRate);

        // Wait for the device to start providing output
        yield return new WaitUntil(() =>
        {
            int pos = Microphone.GetPosition(microphoneName);
            return pos > 0;
        });

        D($"[Rec] Recording started successfully, clip={micClip}, freq={micClip.frequency}, channels={micClip.channels}");

        int channels = micClip.channels;
        int lastSample = 0;
        bool started = false;
        float aboveTimer = 0f;
        float silenceTimer = 0f;
        float recordedTime = 0f;
        float totalTime = 0f;

        int preRollMax = Mathf.CeilToInt(preRollSeconds * sampleRate);
        Queue<float> preRoll = new Queue<float>(preRollMax);
        List<float> capture = new List<float>(sampleRate * 10);

        // Throttled state logger (prints once every debugLogInterval seconds)
        Action throttledStateLog = () =>
        {
            if (!verboseDebug) return;
            if (Time.realtimeSinceStartup < _nextLogTime) return;
            _nextLogTime = Time.realtimeSinceStartup + Mathf.Max(0.05f, debugLogInterval);
            Debug.Log($"[VAD] started={started} batchMax={lastBatchMax:F4} globalMax={_globalMax:F4} " +
                      $"aboveTimer={aboveTimer:F3}s silenceTimer={silenceTimer:F3}s " +
                      $"recTime={recordedTime:F2}s total={totalTime:F2}s " +
                      $"over(start/stop/intr)={_overStartCount}/{_overStopCount}/{_overInterruptCount} " +
                      $"preRoll={preRoll.Count} cap={capture.Count}");
        };

        while (true)
        {
            int cur = Microphone.GetPosition(microphoneName);
            if (cur < 0)
            {
                Debug.LogWarning("[Rec] Microphone.GetPosition returned -1 (some platforms may do this), continuing to wait");
                yield return null;
                continue;
            }

            int delta = cur - lastSample;
            if (delta < 0) delta += micClip.samples; // circular buffer wrap
            if (delta == 0) { throttledStateLog(); yield return null; continue; }

            int toEnd = micClip.samples - lastSample;
            var chunks = new List<float[]>(2);
            if (delta <= toEnd)
            {
                float[] a = new float[delta * channels];
                micClip.GetData(a, lastSample);
                chunks.Add(a);
            }
            else
            {
                float[] a = new float[toEnd * channels];
                float[] b = new float[(delta - toEnd) * channels];
                micClip.GetData(a, lastSample);
                micClip.GetData(b, 0);
                chunks.Add(a);
                chunks.Add(b);
            }

            float batchMax = 0f;
            int batchFrames = 0;

            foreach (var src in chunks)
            {
                if (src == null) continue;
                int frames = src.Length / channels;
                batchFrames += frames;

                for (int f = 0; f < frames; f++)
                {
                    float mono;
                    if (channels == 1) mono = src[f];
                    else
                    {
                        float sum = 0f;
                        for (int c = 0; c < channels; c++) sum += src[f * channels + c];
                        mono = sum / channels;
                    }

                    float abs = Mathf.Abs(mono);
                    if (abs > batchMax) batchMax = abs;

                    if (!started)
                    {
                        if (preRoll.Count >= preRollMax) preRoll.Dequeue();
                        preRoll.Enqueue(mono);
                    }
                    else
                    {
                        capture.Add(mono);
                        recordedTime += 1f / sampleRate;
                    }
                }
            }

            float batchDur = batchFrames / (float)sampleRate;
            totalTime += batchDur;
            lastBatchMax = batchMax;
            if (batchMax > _globalMax) _globalMax = batchMax;

            // —— 1) Hard interrupt (peak > interruptThreshold sustained for interruptGraceMs) ——
            if (interruptEvenBeforeVAD && (ttsToInterrupt != null ? ttsToInterrupt.IsSpeaking : true))
            {
                if (batchMax > interruptThreshold)
                {
                    interruptAboveTimer += batchDur;
                    _overInterruptCount++;
                    DT($"[INT]", $"Hard interrupt timer: {interruptAboveTimer:F3}s / {(interruptGraceMs / 1000f):F3}s (batchMax={batchMax:F4})");

                    if (interruptAboveTimer >= (interruptGraceMs / 1000f))
                    {
                        HandleUserInterrupt();
                        interruptAboveTimer = 0f;
                    }
                }
                else
                {
                    if (interruptAboveTimer > 0f)
                        DT("[INT]", $"Hard interrupt timer reset (batchMax={batchMax:F4} < {interruptThreshold})");
                    interruptAboveTimer = 0f;
                }
            }

            // —— 2) Official 'start speaking' detection (VAD) ——
            if (!started)
            {
                if (batchMax > startThreshold)
                {
                    // Check if TTS is in protection period (prevent self-interrupt)
                    bool inProtectionPeriod = (ttsToInterrupt != null && ttsToInterrupt.IsSpeaking && 
                                              (Time.realtimeSinceStartup - _ttsStartTime) < ttsProtectionDurationSec);
                    
                    if (inProtectionPeriod)
                    {
                        DT("[VAD]", $"⏸️  TTS protection period active (started {(Time.realtimeSinceStartup - _ttsStartTime):F2}s ago), skipping VAD detection");
                        aboveTimer = 0f;
                    }
                    else
                    {
                        aboveTimer += batchDur;
                        _overStartCount++;
                        DT("[VAD]", $">startThreshold: aboveTimer={aboveTimer:F3}/{startHoldTime:F3} (batchMax={batchMax:F4})");

                        if (aboveTimer >= startHoldTime)
                        {
                            started = true;
                            DT("[VAD]", $"✅ STARTED! preRoll={preRoll.Count} samples merged into capture");

                            HandleUserInterrupt();

                            while (preRoll.Count > 0)
                            {
                                capture.Add(preRoll.Dequeue());
                                recordedTime += 1f / sampleRate;
                            }
                            silenceTimer = 0f;
                        }
                    }
                }
                else
                {
                    if (aboveTimer > 0f)
                        DT("[VAD]", $"start timer reset (batchMax={batchMax:F4} < {startThreshold})");
                    aboveTimer = 0f;
                }
            }
            else
            {
                // —— 3) 停止判定（尾部静音）——
                if (batchMax < stopThreshold)
                {
                    silenceTimer += batchDur;
                    _overStopCount++;
                    DT("[VAD]", $"<stopThreshold: silenceTimer={silenceTimer:F3}/{stopSilenceTime:F3} (batchMax={batchMax:F4})");
                }
                else
                {
                    if (silenceTimer > 0f)
                        DT("[VAD]", $"stop timer reset (batchMax={batchMax:F4} >= {stopThreshold})");
                    silenceTimer = 0f;
                }

                if (recordedTime >= minRecordTime && silenceTimer >= stopSilenceTime)
                {
                    DT("[VAD]", $"🛑 END: recordedTime={recordedTime:F2}s, silenceTimer={silenceTimer:F2}s");
                    break;
                }
            }

            if (totalTime >= maxRecordTime)
            {
                DT("[VAD]", $"⏱️ Reached max record time {maxRecordTime}s, forcing end of segment");
                break;
            }

            throttledStateLog();
            lastSample = cur;
            yield return null;
        }

        Microphone.End(microphoneName);
        D($"[Rec] Microphone.End(). globalMax={_globalMax:F4}, captureSamples={capture.Count}");

        // If VAD never flipped to 'started' we may still have useful audio in the pre-roll buffer
        // (short utterances or noisy input can prevent aboveTimer from reaching startHoldTime).
        // Salvage pre-roll into capture when there's evidence of audio activity.
        if (capture.Count == 0 && preRoll != null && preRoll.Count > 0 && _globalMax > startThreshold)
        {
            D($"[VAD] Salvaging pre-roll as utterance (globalMax={_globalMax:F4} > startThreshold={startThreshold:F4})");
            while (preRoll.Count > 0)
            {
                capture.Add(preRoll.Dequeue());
            }
        }

        if (capture.Count == 0)
        {
            Debug.LogWarning("⚠️ No valid speech captured (threshold may be too high or environment too quiet)" +
                             $" | globalMax={_globalMax:F4} startThr={startThreshold} stopThr={stopThreshold} " +
                             $" | over(start/stop/intr)={_overStartCount}/{_overStopCount}/{_overInterruptCount}");
            onSaved?.Invoke(null);
            yield break;
        }

        // 写入 WAV
        var outClip = AudioClip.Create("speech_trimmed", capture.Count, 1, sampleRate, false);
        outClip.SetData(capture.ToArray(), 0);

        string filePath = TrialLogPath.GetFilePath("temp_speech.wav");
            try
            {
                byte[] wav = WavUtility.FromAudioClip(outClip); // depends on your project's WavUtility
                File.WriteAllBytes(filePath, wav);
                Debug.Log($"[Save] WAV write completed: {filePath} ({wav.Length} bytes)");
                onSaved?.Invoke(filePath);
                Debug.Log($"[Save] onSaved invoked with path: {filePath}");
            }
        catch (Exception e)
        {
            Debug.LogError("Failed to save WAV: " + e.Message);
            onSaved?.Invoke(null);
        }
    }

    private void HandleUserInterrupt()
    {
        float now = Time.realtimeSinceStartup;
        if (now - _lastInterruptTriggerAt < Mathf.Max(0.05f, interruptCooldownSec))
        {
            return;
        }
        _lastInterruptTriggerAt = now;

        OnUserSpeechLikely?.Invoke();
        if (gptConnector != null && gptConnector.IsAgentBusy)
        {
            gptConnector.InterruptForBargeIn();
        }
    }

    // =========（未使用）转写 =========
    private IEnumerator SendAudioToOpenAI(string filePath, Action<string> onComplete)
    {
        D($"[SendAudioToOpenAI] ENTRY - filePath: {filePath}");
        
        if (string.IsNullOrEmpty(filePath))
        {
            D("[SendAudioToOpenAI] ERROR: filePath is null or empty");
            onComplete?.Invoke(null);
            yield break;
        }

        if (!File.Exists(filePath))
        {
            D($"[SendAudioToOpenAI] ERROR: File does not exist at path: {filePath}");
            onComplete?.Invoke(null);
            yield break;
        }

        D($"[SendAudioToOpenAI] File exists, size: {new System.IO.FileInfo(filePath).Length} bytes");

        string key = ResolveApiKey();
        if (string.IsNullOrEmpty(key))
        {
            D("[SendAudioToOpenAI] ERROR: API key is null or empty");
            onComplete?.Invoke(null);
            yield break;
        }

        D("[SendAudioToOpenAI] API key resolved successfully");

        byte[] audioData;
        try
        {
            D("[SendAudioToOpenAI] About to read audio file...");
            audioData = File.ReadAllBytes(filePath);
            D($"[SendAudioToOpenAI] Audio file read successfully, {audioData.Length} bytes");
        }
        catch (Exception ex)
        {
            D($"[SendAudioToOpenAI] ERROR reading file: {ex.GetType().Name} - {ex.Message}");
            onComplete?.Invoke(null);
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "speech.wav", "audio/wav");
        form.AddField("model", string.IsNullOrWhiteSpace(transcriptionModel) ? "gpt-4o-mini-transcribe" : transcriptionModel);

        D("[SendAudioToOpenAI] Creating WebRequest to OpenAI API...");
        var req = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form);
        req.SetRequestHeader("Authorization", "Bearer " + key);
        req.timeout = 20;
        
        D("[SendAudioToOpenAI] Sending WebRequest (timeout: 20s)...");
        yield return req.SendWebRequest();
        D("[SendAudioToOpenAI] WebRequest completed");

        bool ok;
#if UNITY_2020_2_OR_NEWER
        ok = (req.result == UnityWebRequest.Result.Success);
#else
        ok = (!req.isNetworkError && !req.isHttpError);
#endif

        if (!ok)
        {
            D($"[SendAudioToOpenAI] ERROR: WebRequest failed - result: {req.result}");
            if (req.downloadHandler != null)
                D($"[SendAudioToOpenAI] Response: {req.downloadHandler.text}");
            onComplete?.Invoke(null);
            yield break;
        }

        if (req.downloadHandler == null)
        {
            D("[SendAudioToOpenAI] ERROR: downloadHandler is null");
            onComplete?.Invoke(null);
            yield break;
        }

        D("[SendAudioToOpenAI] WebRequest successful, parsing response...");

        string response = req.downloadHandler.text;
        D($"[SendAudioToOpenAI] Response text: {response}");
        
        string transcript = null;

        try
        {
            D("[SendAudioToOpenAI] Attempting JSON parse...");
            var parsed = JsonUtility.FromJson<TranscriptResponse>(response);
            if (parsed != null)
                transcript = parsed.text;
            D($"[SendAudioToOpenAI] JSON parse successful, transcript: {transcript}");
        }
        catch (Exception ex)
        {
            D($"[SendAudioToOpenAI] JSON parse failed ({ex.GetType().Name}), attempting regex fallback");
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            D("[SendAudioToOpenAI] Attempting regex extraction...");
            Match m = Regex.Match(response ?? "", "\"text\"\\s*:\\s*\"(?<t>(?:\\\\.|[^\"])*)\"");
            if (m.Success)
            {
                transcript = Regex.Unescape(m.Groups["t"].Value);
                D($"[SendAudioToOpenAI] Regex extraction successful: {transcript}");
            }
            else
            {
                D("[SendAudioToOpenAI] Regex extraction failed");
            }
        }

        D($"[SendAudioToOpenAI] Final transcript: {(string.IsNullOrWhiteSpace(transcript) ? "(null)" : transcript.Trim())}");
        onComplete?.Invoke(string.IsNullOrWhiteSpace(transcript) ? null : transcript.Trim());
        D("[SendAudioToOpenAI] EXIT - callback invoked");
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(openAIKey))
            return openAIKey;

        if (gptConnector != null && !string.IsNullOrWhiteSpace(gptConnector.apiKey))
            return gptConnector.apiKey;

        return null;
    }

    // ========= Global hard-stop & temporary mute (compatible with Unity 2019) =========
    private void LogAndStopAllAudio(string reason)
    {
        AudioSource[] all;
#if UNITY_2020_1_OR_NEWER
        all = GameObject.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
#else
        var active = UnityEngine.Object.FindObjectsOfType<AudioSource>();
        var maybeAll = Resources.FindObjectsOfTypeAll<AudioSource>();
        var list = new List<AudioSource>(active);
        foreach (var a in maybeAll)
        {
            if (a == null) continue;
            var go = a.gameObject;
            if (go != null && go.scene.IsValid() && !list.Contains(a))
                list.Add(a);
        }
        all = list.ToArray();
#endif
        Debug.LogWarning($"[INT] LogAndStopAllAudio called! Reason: {reason}. StackTrace: {System.Environment.StackTrace}");
        foreach (var s in all)
        {
            if (s == null) continue;
            if (s.isPlaying)
            {
                Debug.LogWarning($"[INT] Stopping AudioSource {s.name} on GameObject {s.gameObject.name}.");
                s.Stop();
                s.time = 0f;
                Debug.LogWarning($"[INT] Clearing AudioSource.clip for {s.name} on GameObject {s.gameObject.name}.");
                s.clip = null;
            }
        }
        D($"[INT] {reason}: stopped all AudioSources");
    }

    private IEnumerator HardMuteForFrames(int frames)
    {
        float prev = AudioListener.volume;
        AudioListener.volume = 0f;
        for (int i = 0; i < frames; i++) yield return null;
        AudioListener.volume = prev;
    }

    [Serializable] private class TranscriptResponse { public string text; }

    // ======== Debug helpers ========
    private void D(string msg) { if (verboseDebug) Debug.Log(msg); }
    private void DT(string tag, string msg)
    {
        if (!verboseDebug) return;
        if (Time.realtimeSinceStartup < _nextLogTime) return;
        _nextLogTime = Time.realtimeSinceStartup + Mathf.Max(0.05f, debugLogInterval);
        Debug.Log($"{tag} {msg}");
    }
}
