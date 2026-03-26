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
    public string openAIKey = "";                 // ⚠️ 不要硬编码 API Key，改在 Inspector 里填
    public GPTConnector gptConnector;             // 识别后把音频直接交给 GPTConnector

    [Header("Transcription")]
    [Tooltip("Transcribe user audio before sending so User_Speech logs contain the actual utterance")]
    public bool transcribeBeforeSend = true;
    [Tooltip("OpenAI transcription model used for user speech logging")]
    public string transcriptionModel = "gpt-4o-mini-transcribe";

    [Header("Record Settings")]
    public int sampleRate = 16000;                // Quest 3 建议 48000
    public int maxRecordTime = 30;                // 单段最大录音时长（秒）
    public float minRecordTime = 0.6f;            // 最短有效录音时长（秒）
    public float preRollSeconds = 0.2f;           // 开始点前的预录缓存（秒）

    [Header("Voice Activity Detection")]
    [Tooltip("判定“开始说话”的音量阈值（0~1）")]
    public float startThreshold = 0.02f;
    [Tooltip("音量持续高于 startThreshold 这么久才算“开始说话”")]
    public float startHoldTime = 0.7f; // Require 700ms above threshold to trigger VAD
    [Tooltip("判定“停止”的音量阈值（建议略低于开始阈值）")]
    public float stopThreshold = 0.015f;
    [Tooltip("开始说话后，静音持续这么久即判定“结束”")]
    public float stopSilenceTime = 1.0f;

    [Header("TTS Interrupt")]
    [Tooltip("需要被打断的 TTS 播放器（可空；为空时仍会广播 OnUserSpeechLikely 事件）")]
    public TextToSpeechPlayer ttsToInterrupt;
    [Tooltip("允许在 AI 说话时继续监听并打断（barge-in）")]
    public bool allowBargeIn = true;
    [Tooltip("即使 VAD 还没判定开始，只要峰值短时间超过阈值也立刻打断 TTS (DISABLED for uninterruptible agent)")]
    public bool interruptEvenBeforeVAD = false; // DISABLED: Agent is uninterruptible
    [Tooltip("硬中断的瞬时阈值（0~1，可按设备调）")]
    public float interruptThreshold = 0.03f;
    [Tooltip("硬中断阈值需要持续的最短时间（毫秒）")]
    public float interruptGraceMs = 700f;
    [Tooltip("打断时是否全局停所有注册的 TTS（项目里有多个 TTS 时建议开启）")]
    public bool killAllTTSOnInterrupt = true;
    [Tooltip("TTS 开始播放后，在此时间内不会被用户声音打断（秒，防止自中断）")]
    public float ttsProtectionDurationSec = 0.5f;
    [Tooltip("Minimum interval between interrupt triggers to avoid duplicate barge-in calls")]
    public float interruptCooldownSec = 0.5f;

    // 识别器对外事件：一旦“疑似用户开口”，立即触发（供 TTS 订阅）
    public event Action OnUserSpeechLikely;

    [Header("Debug")]
    public bool showDebugOverlay = true;
    [Tooltip("是否打印详细调试日志")]
    public bool verboseDebug = true;
    [Tooltip("节流：两次日志之间的最小间隔（秒）")]
    public float debugLogInterval = 0.25f;

    // 内部状态
    private string microphoneName;
    private AudioClip micClip;
    private bool isRunning;
    private float interruptAboveTimer = 0f;   // 峰值持续计时（秒）
    private float lastBatchMax = 0f;          // 调试显示：最近一批最大值
    private GUIStyle _g;
    private float _ttsStartTime = -999f;      // Track when TTS started to prevent self-interrupt
    private float _lastInterruptTriggerAt = -999f;

    // 统计/节流
    private float _nextLogTime = 0f;
    private float _globalMax = 0f;
    private int _overStartCount = 0;
    private int _overStopCount = 0;
    private int _overInterruptCount = 0;

    // ========= 生命周期 =========
    void Start()
    {
        D("[Init] Start() 进入");
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
            Debug.LogError("❌ 未检测到麦克风");
            yield break;
        }
        else
        {
            for (int i = 0; i < Microphone.devices.Length; i++)
                D($"[Mic] Device[{i}]: {Microphone.devices[i]}");
        }

        microphoneName = Microphone.devices[0];
        D($"[Mic] Using device: {microphoneName}");

        StartCoroutine(RecordingLoop()); // 非阻塞主循环：持续边听边处理
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

    // ========= 主循环：录一段 -> 立刻异步处理 -> 立即进入下一段 =========
    private IEnumerator RecordingLoop()
    {
        D("[Loop] 进入 RecordingLoop()");
        isRunning = true;
        float lastUtteranceTime = -999f;
        float cooldownSec = 1.0f; // 1 second cooldown after each utterance
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

            // 录到“开始”+“结束”一段，保存到 wavPath
            yield return StartCoroutine(RecordUtteranceAndSave(path => wavPath = path));

            if (!string.IsNullOrEmpty(wavPath))
            {
                D($"[Loop] 本段保存成功: {wavPath}");
                // 立刻异步处理（直接把音频送给 GPTConnector），不阻塞录音主循环
                StartCoroutine(ProcessUtterance(wavPath));
                lastUtteranceTime = Time.realtimeSinceStartup;
            }
            else
            {
                D("[Loop] 本段保存失败/无有效语音，继续下一轮监听");
            }

            // Cooldown after sending utterance
            while (Time.realtimeSinceStartup - lastUtteranceTime < cooldownSec)
            {
                yield return null;
            }
        }
    }

    // ========= 处理单段话：不再转写；直接送 GPT =========
    private IEnumerator ProcessUtterance(string wavPath)
    {
        if (gptConnector == null)
        {
            Debug.LogWarning("GPTConnector 未设置，无法发送音频。");
            yield break;
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
        D($"[Send] 发送音频到 GPTConnector: {wavPath}");

        string transcriptContext = null;
        if (transcribeBeforeSend)
        {
            yield return StartCoroutine(SendAudioToOpenAI(wavPath, text => transcriptContext = text));
            if (!string.IsNullOrWhiteSpace(transcriptContext))
            {
                D($"[STT][user] {transcriptContext}");
            }
        }

        // Check audio length before sending (must be at least 100ms for 16kHz = 1600 samples, for 24kHz = 2400 samples)
        try {
            var wavBytes = File.ReadAllBytes(wavPath);
            // crude check: look for at least 3200 bytes (16-bit mono, 1600 samples)
            if (wavBytes.Length < 4000) {
                Debug.LogWarning("[OpenAISpeechRecognizer] Audio buffer too small, not sending to GPTConnector.");
                yield break;
            }
        } catch (Exception e) {
            Debug.LogWarning($"[OpenAISpeechRecognizer] Failed to check audio file size: {e.Message}");
        }
        gptConnector.SendAudioFileToGPT(wavPath, transcriptContext, null);
        yield return null;
    }

    // ========= 真正的录音 + VAD（含硬中断触发 TTS 停止） =========
    private IEnumerator RecordUtteranceAndSave(Action<string> onSaved)
    {
        D($"[Rec] Microphone.Start(name={microphoneName}, rate={sampleRate})");
        micClip = Microphone.Start(microphoneName, true, maxRecordTime, sampleRate);

        // 等待设备开始输出
        yield return new WaitUntil(() =>
        {
            int pos = Microphone.GetPosition(microphoneName);
            return pos > 0;
        });

        D($"[Rec] 录音启动成功，clip={micClip}, freq={micClip.frequency}, channels={micClip.channels}");

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

        // 节流打印函数（每 debugLogInterval 秒打印一次状态）
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
                Debug.LogWarning("[Rec] Microphone.GetPosition 返回 -1（某些平台可能会这样），继续等待");
                yield return null;
                continue;
            }

            int delta = cur - lastSample;
            if (delta < 0) delta += micClip.samples; // 环形缓冲
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

            // —— 1) 硬中断（峰值 > interruptThreshold 持续 interruptGraceMs）——
            if (interruptEvenBeforeVAD && (ttsToInterrupt != null ? ttsToInterrupt.IsSpeaking : true))
            {
                if (batchMax > interruptThreshold)
                {
                    interruptAboveTimer += batchDur;
                    _overInterruptCount++;
                    DT($"[INT]", $"硬中断计时: {interruptAboveTimer:F3}s / {(interruptGraceMs / 1000f):F3}s (batchMax={batchMax:F4})");

                    if (interruptAboveTimer >= (interruptGraceMs / 1000f))
                    {
                        HandleUserInterrupt();
                        interruptAboveTimer = 0f;
                    }
                }
                else
                {
                    if (interruptAboveTimer > 0f)
                        DT("[INT]", $"硬中断计时被清零（batchMax={batchMax:F4} < {interruptThreshold})");
                    interruptAboveTimer = 0f;
                }
            }

            // —— 2) 正式“开始说话”判定（VAD）——
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
                        DT("[VAD]", $">startThreshold：aboveTimer={aboveTimer:F3}/{startHoldTime:F3} (batchMax={batchMax:F4})");

                        if (aboveTimer >= startHoldTime)
                        {
                            started = true;
                            DT("[VAD]", $"✅ STARTED! preRoll={preRoll.Count} samples 将并入 capture");

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
                        DT("[VAD]", $"start计时清零（batchMax={batchMax:F4} < {startThreshold})");
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
                    DT("[VAD]", $"<stopThreshold：silenceTimer={silenceTimer:F3}/{stopSilenceTime:F3} (batchMax={batchMax:F4})");
                }
                else
                {
                    if (silenceTimer > 0f)
                        DT("[VAD]", $"stop计时清零（batchMax={batchMax:F4} >= {stopThreshold})");
                    silenceTimer = 0f;
                }

                if (recordedTime >= minRecordTime && silenceTimer >= stopSilenceTime)
                {
                    DT("[VAD]", $"🛑 结束：recordedTime={recordedTime:F2}s, silenceTimer={silenceTimer:F2}s");
                    break;
                }
            }

            if (totalTime >= maxRecordTime)
            {
                DT("[VAD]", $"⏱️ 达到最大录音时长 {maxRecordTime}s，强制结束本段");
                break;
            }

            throttledStateLog();
            lastSample = cur;
            yield return null;
        }

        Microphone.End(microphoneName);
        D($"[Rec] Microphone.End(). globalMax={_globalMax:F4}, captureSamples={capture.Count}");

        if (capture.Count == 0)
        {
            Debug.LogWarning("⚠️ 未捕获到有效语音（阈值可能过高或环境过静）" +
                             $" | globalMax={_globalMax:F4} startThr={startThreshold} stopThr={stopThreshold} " +
                             $" | over(start/stop/intr)={_overStartCount}/{_overStopCount}/{_overInterruptCount}");
            onSaved?.Invoke(null);
            yield break;
        }

        // 写入 WAV
        var outClip = AudioClip.Create("speech_trimmed", capture.Count, 1, sampleRate, false);
        outClip.SetData(capture.ToArray(), 0);

        string filePath = Path.Combine(Application.persistentDataPath, "temp_speech.wav");
        try
        {
            byte[] wav = WavUtility.FromAudioClip(outClip); // 依赖你项目里的 WavUtility
            File.WriteAllBytes(filePath, wav);
            D($"[Save] 写入 WAV 完成: {filePath} ({wav.Length} bytes)");
            onSaved?.Invoke(filePath);
        }
        catch (Exception e)
        {
            Debug.LogError("保存 WAV 失败: " + e.Message);
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
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            onComplete?.Invoke(null);
            yield break;
        }

        string key = ResolveApiKey();
        if (string.IsNullOrEmpty(key))
        {
            onComplete?.Invoke(null);
            yield break;
        }

        byte[] audioData;
        try
        {
            audioData = File.ReadAllBytes(filePath);
        }
        catch
        {
            onComplete?.Invoke(null);
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "speech.wav", "audio/wav");
        form.AddField("model", string.IsNullOrWhiteSpace(transcriptionModel) ? "gpt-4o-mini-transcribe" : transcriptionModel);

        var req = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form);
        req.SetRequestHeader("Authorization", "Bearer " + key);
        req.timeout = 20;
        yield return req.SendWebRequest();

        bool ok;
#if UNITY_2020_2_OR_NEWER
        ok = (req.result == UnityWebRequest.Result.Success);
#else
        ok = (!req.isNetworkError && !req.isHttpError);
#endif

        if (!ok || req.downloadHandler == null)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        string response = req.downloadHandler.text;
        string transcript = null;

        try
        {
            var parsed = JsonUtility.FromJson<TranscriptResponse>(response);
            if (parsed != null)
                transcript = parsed.text;
        }
        catch
        {
            // Fall back to regex extraction below.
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            Match m = Regex.Match(response ?? "", "\"text\"\\s*:\\s*\"(?<t>(?:\\\\.|[^\"])*)\"");
            if (m.Success)
            {
                transcript = Regex.Unescape(m.Groups["t"].Value);
            }
        }

        onComplete?.Invoke(string.IsNullOrWhiteSpace(transcript) ? null : transcript.Trim());
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(openAIKey))
            return openAIKey;

        if (gptConnector != null && !string.IsNullOrWhiteSpace(gptConnector.apiKey))
            return gptConnector.apiKey;

        return null;
    }

    // ========= 全局硬停 & 临时静音（兼容 Unity 2019） =========
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
        D($"[INT] {reason}: 停止所有 AudioSource");
    }

    private IEnumerator HardMuteForFrames(int frames)
    {
        float prev = AudioListener.volume;
        AudioListener.volume = 0f;
        for (int i = 0; i < frames; i++) yield return null;
        AudioListener.volume = prev;
    }

    [Serializable] private class TranscriptResponse { public string text; }

    // ======== 调试辅助 ========
    private void D(string msg) { if (verboseDebug) Debug.Log(msg); }
    private void DT(string tag, string msg)
    {
        if (!verboseDebug) return;
        if (Time.realtimeSinceStartup < _nextLogTime) return;
        _nextLogTime = Time.realtimeSinceStartup + Mathf.Max(0.05f, debugLogInterval);
        Debug.Log($"{tag} {msg}");
    }
}
