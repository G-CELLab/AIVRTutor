using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using AI;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class GPTConnector : MonoBehaviour
{
    // Modular request queue for GPT requests
    private GPTRequestQueue _requestQueue = new GPTRequestQueue();
    [Header("OpenAI")]
    public string apiKey = ""; // ⚠️不要硬编码，Inspector 填
    public string chatModel = AI.Prompts.Customizations.DefaultChatModel;
    public int requestTimeoutSeconds = AI.Prompts.Customizations.DefaultRequestTimeoutSeconds;

    [Header("Realtime")]
    public bool useRealtime = AI.Prompts.Customizations.DefaultUseRealtime;
    public string realtimeModel = AI.Prompts.Customizations.DefaultRealtimeModel;
    public int realtimeSampleRate = AI.Prompts.Customizations.DefaultRealtimeSampleRate;
    public bool realtimeDumpEvents = AI.Prompts.Customizations.DefaultRealtimeDumpEvents;

    [Header("UI/Audio")]
    public TMP_Text outputText;
    public TextToSpeechPlayer ttsPlayer;

    [Header("Animator Driver")]
    public TTSAnimatorDriver ttsDriver;
    public float expectSpeechTimeout = 10f;

    [Header("Conversation Memory")]
    [TextArea(2, 6)]
    public string systemPrompt = AI.Prompts.PromptLibrary.SystemPrompt;
    public int maxHistoryTurnsToSend = AI.Prompts.Customizations.DefaultMaxHistoryTurnsToSend;
    public int maxCharsBudget = AI.Prompts.Customizations.DefaultMaxCharsBudget;

    [Header("Audio I/O Settings")]
    [Tooltip("启用：优先播放模型直接返回的语音；否则回退到本地TTS。")]
    public bool preferModelAudio = AI.Prompts.Customizations.DefaultPreferModelAudio;
    [Tooltip("让模型合成的语音音色")]
    public string gptVoice = AI.Prompts.Customizations.DefaultGptVoice;
    [Tooltip("模型语音格式（pcm16/g711_ulaw/g711_alaw）")]
    public string gptAudioFormat = AI.Prompts.Customizations.DefaultGptAudioFormat;

    [Header("Language")]
    public bool alwaysEnglish = AI.Prompts.Customizations.DefaultAlwaysEnglish; // ✅ 强制英文
    [TextArea(1, 3)]
    public string englishDirective = AI.Prompts.PromptLibrary.EnglishDirective;

    [Header("Audio Coordinator")]
    public OpenAISpeechRecognizer speechRecognizer;

    [Header("Prompt + Audio")]
    [Tooltip("将要与下一段语音一并发送到 Realtime 的文本提示（发出后自动清空）")]
    public string pendingUserPrompt = "";
    public void SetPendingUserPrompt(string s) { pendingUserPrompt = s ?? ""; }

    [Header("Debug")]
    public bool verboseDebug = AI.Prompts.Customizations.DefaultVerboseDebug;
    public bool dumpResponsesToFile = AI.Prompts.Customizations.DefaultDumpResponsesToFile;
    public float debugLogInterval = AI.Prompts.Customizations.DefaultDebugLogInterval;
    public int maxLogChars = AI.Prompts.Customizations.DefaultMaxLogChars;

    // ===== 延时触发：基于 isSpeaking 的延时调度 =====
    [Header("Speaking & Delayed Triggers")]
    [Tooltip("前两动作（Interphase: DZ13, Prophase: DZ18）在 isSpeaking 触发后延时秒数")]
    public float delayFirstGroupSec = 10f;
    [Tooltip("后两动作（Metaphase: DZ20, Telophase: DZ22）在 isSpeaking 触发后延时秒数")]
    public float delaySecondGroupSec = 3f;
    [Tooltip("后两动作（Metaphase: DZ20, Telophase: DZ22）在 isSpeaking 触发后延时秒数")]
    public float delayThirdGroupSec = 3f;
    [Tooltip("后两动作（Metaphase: DZ20, Telophase: DZ22）在 isSpeaking 触发后延时秒数")]
    public float delay4GroupSec = 3f;
    [Tooltip("若当前还未开始说话，最多等待多少秒以等到 isSpeaking=true；超时也会照常延时触发")]
    public float speakingWaitGraceSec = 5f;

    private volatile bool _isSpeaking = false;

    // ★ instruction debug
    [Header("Instruction Debug")]
    public bool logPrompts = true;
    public TMP_Text promptEchoText; // 可选：场景里拖一个 TMP_Text 看最后一次 instructions
    public int promptLogMaxChars = 2000;
    [TextArea(2, 8)] public string lastPrompt = ""; // 公开存储最近一次“最终发送”的 instructions

    // ================= 相位指令预缓存（文本+音频） =================
    [Header("Phase Cache (Precompute)")]
    public bool precomputePhaseAssetsOnStart = AI.Prompts.Customizations.DefaultPrecomputePhaseAssetsOnStart; // 启动时预缓存
    public bool savePhaseTextFiles = AI.Prompts.Customizations.DefaultSavePhaseTextFiles; // 保存 .txt
    public bool generatePhaseTtsAudio = AI.Prompts.Customizations.DefaultGeneratePhaseTtsAudio; // 生成 TTS
    public string ttsModel = AI.Prompts.Customizations.DefaultTtsModel; // TTS 模型
    public string ttsVoice = AI.Prompts.Customizations.DefaultTtsVoice; // TTS 音色
    public string phaseAudioFormat = AI.Prompts.Customizations.DefaultPhaseAudioFormat; // TTS 音频格式（建议 wav）
    public bool prependPhaseTextAsDeveloperItem = AI.Prompts.Customizations.DefaultPrependPhaseTextAsDeveloperItem; // 每次对话前把相位指令作为 developer 文本钉入
    public bool prependPhaseInstructionAudio = AI.Prompts.Customizations.DefaultPrependPhaseInstructionAudio; // 把“相位指令的音频”拼在用户语音前（需采样率一致，默认关)

    // ===== 触发来源：仅用“助理字幕/文本” =====
    [Header("Reactions")]
    public bool reactToAssistantTranscript = true; // 用助理回复字幕触发动作

    private readonly Dictionary<GameManager.GameState, string> _phaseTextCache = new Dictionary<GameManager.GameState, string>(); // 文本缓存
    private readonly Dictionary<GameManager.GameState, string> _phaseAudioPathCache = new Dictionary<GameManager.GameState, string>(); // 音频缓存（本地路径）

    // 内部
    private readonly List<Message> history = new List<Message>();
    private Action onReplyComplete;

    // Realtime：WS 状态
    private ClientWebSocket _ws;
    private CancellationTokenSource _wsCts;
    private readonly ConcurrentQueue<string> _eventQueue = new ConcurrentQueue<string>();
    private MemoryStream _audioAccum; // 累计 audio.delta (PCM16)
    private int _audioChunkCount;
    private StringBuilder _textAccum; // 累计 text.delta
    private float _nextLog = 0f;

    // === Realtime ack gate ===
    private volatile bool _sessionReady = false;
    private int _sessionUpdatedTick = 0;
    private volatile bool _responseInProgress = false; // Prevent simultaneous response.create calls

    // === 助理字幕累积 ===
    private readonly StringBuilder _assistantTranscriptAccum = new StringBuilder(256);

    // -------- 生命周期：启动即预缓存 --------
    // private void Start()
    // {
    //     if (precomputePhaseAssetsOnStart)
    //     {
    //         StartCoroutine(PrecacheAllPhaseAssets());
    //     }
    // }

    // ===== 语言策略（强制英文）=====
    private string ApplyLang(string instr)
    {
        if (string.IsNullOrWhiteSpace(instr)) instr = "";
        if (alwaysEnglish) return (instr.Length > 0 ? instr + "\n\n" : "") + englishDirective;
        return instr; // 如需跟随用户语言，可在此扩展
    }

    // ===== 合并“系统+相位+可选附加”为最终 instructions =====
    private string BuildFinalInstructions(string extra = null)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(systemPrompt)) parts.Add(systemPrompt);
        string phase = BuildSystemPromptForCurrentPhase();
        if (!string.IsNullOrEmpty(phase)) parts.Add(phase);
        if (!string.IsNullOrEmpty(extra)) parts.Add(extra);
        string joined = string.Join("\n\n", parts);
        return ApplyLang(joined);
    }

    // ===== 为“指定相位”构造最终 instructions（用于预缓存） =====
    private string BuildInstructionsForPhase(GameManager.GameState gs, string extra = null)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(systemPrompt)) parts.Add(systemPrompt);
        string phaseText = PhaseText(gs);
        if (!string.IsNullOrEmpty(phaseText)) parts.Add(phaseText);
        if (!string.IsNullOrEmpty(extra)) parts.Add(extra);
        return ApplyLang(string.Join("\n\n", parts));
    }

    // ========== 外部 API ==========
    public void SendToGPT(string userInput, Action onComplete)
    {
        if (_responseInProgress)
        {
            // Queue the request if a response is in progress
            _requestQueue.Enqueue(new GPTRequestQueue.QueuedRequest {
                Type = GPTRequestQueue.RequestType.Text,
                Text = userInput,
                OnComplete = onComplete
            });
            D("[Queue] Text request queued (response in progress)");
            return;
        }
        onReplyComplete = onComplete;
        if (ttsDriver) ttsDriver.NotifyExpectSpeech(expectSpeechTimeout);
        if (useRealtime)
        {
            StartCoroutine(SendTextViaRealtime(userInput));
        }
        else
        {
            D($"[SendText] HTTP model={chatModel}, history={history.Count}, len={userInput?.Length ?? 0}");
            StartCoroutine(SendTextRequest(userInput));
        }
    }

    // === Process next queued request ===
    private void ProcessNextQueuedRequest()
    {
        if (_responseInProgress) return;
        if (_requestQueue.Count == 0) return;
        var req = _requestQueue.Dequeue();
        if (req == null) return;
        switch (req.Type)
        {
            case GPTRequestQueue.RequestType.Text:
                SendToGPT(req.Text, req.OnComplete);
                break;
            case GPTRequestQueue.RequestType.AudioFile:
                SendAudioFileToGPT(req.AudioFilePath, req.OnComplete);
                break;
            case GPTRequestQueue.RequestType.AudioBytes:
                SendAudioBytesToGPT(req.AudioBytes, req.AudioFormat, req.OnComplete);
                break;
        }
    }

    public void SendAudioFileToGPT(string audioFilePath, Action onComplete)
    {
        if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
        {
            Debug.LogWarning("[GPTConnector] Audio path invalid.");
            return;
        }
        if (_responseInProgress)
        {
            // Queue the audio file request
            _requestQueue.Enqueue(new GPTRequestQueue.QueuedRequest {
                Type = GPTRequestQueue.RequestType.AudioFile,
                AudioFilePath = audioFilePath,
                OnComplete = onComplete
            });
            D("[Queue] Audio file request queued (response in progress)");
            return;
        }
        onReplyComplete = onComplete;
        if (ttsDriver) ttsDriver.NotifyExpectSpeech(expectSpeechTimeout);

        byte[] wav = File.ReadAllBytes(audioFilePath);
        D($"[SendAudioFile] {audioFilePath} bytes={wav.Length}, useRealtime={useRealtime}");

        if (useRealtime)
        {
            byte[] pcm = ExtractPcm16FromWavRobust(wav);
            if (pcm == null || pcm.Length == 0)
            {
                Warn($"[Realtime] 解析 WAV 失败，未能提取 PCM16。 wavBytes={wav.Length}, pcmBytes={(pcm?.Length ?? 0)}");
                return;
            }
            D($"[SendAudioFile] ✓ WAV extraction succeeded: {wav.Length} bytes → {pcm.Length} bytes PCM16");
            string extra = string.IsNullOrWhiteSpace(pendingUserPrompt) ? null : pendingUserPrompt;
            pendingUserPrompt = "";
            StartCoroutine(SendPcmViaRealtime(pcm, extra));
        }
        else
        {
            string b64 = Convert.ToBase64String(wav);
            StartCoroutine(SendAudioRequest(b64, "wav"));
        }
    }

    public void SendAudioBytesToGPT(byte[] audioBytes, string format, Action onComplete)
    {
        if (_responseInProgress)
        {
            // If a response is in progress, clear the queue and only process the latest request
            _requestQueue.Clear();
        }
        // Check audio buffer length: must be at least 100ms
        int minSamples = (int)(realtimeSampleRate * 0.1f); // 100ms
        if (audioBytes == null || audioBytes.Length < minSamples * 2) // 2 bytes per sample (pcm16)
        {
            Warn("[Realtime][Audio] Buffer too small. Skipping send. Need at least 100ms of audio.");
            onComplete?.Invoke();
            return;
        }
        onReplyComplete = onComplete;
        if (ttsDriver) ttsDriver.NotifyExpectSpeech(expectSpeechTimeout);

        if (useRealtime)
        {
            string extra = string.IsNullOrWhiteSpace(pendingUserPrompt) ? null : pendingUserPrompt;
            pendingUserPrompt = "";
            StartCoroutine(SendPcmViaRealtime(audioBytes, extra));
        }
        else
        {
            string fmt = string.IsNullOrEmpty(format) ? "wav" : format;
            string b64 = Convert.ToBase64String(audioBytes);
            D($"[SendAudioBytes] HTTP model={chatModel}, format={fmt}, base64Len={b64.Length}");
            StartCoroutine(SendAudioRequest(b64, fmt));
        }
    }

    public void ClearHistory() => history.Clear();
    public void SetSystemPrompt(string prompt) { systemPrompt = prompt ?? ""; }

    // ========== HTTP ==========
    private IEnumerator SendTextRequest(string userInput)
    {
        const string endpoint = "https://api.openai.com/v1/chat/completions";
        string messagesJson = BuildMessagesJson(userInput);
        string json = "{\"model\":\"" + chatModel + "\",\"messages\":" + messagesJson + "}";

        var req = new UnityWebRequest(endpoint, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        req.timeout = Mathf.Max(5, requestTimeoutSeconds);

        D($"[HTTP→] {endpoint}\nheaders: Authorization=Bearer {MaskKey(apiKey)}\npayloadBytes={bodyRaw.Length}");
        yield return req.SendWebRequest();

        bool ok;
#if UNITY_2020_2_OR_NEWER
        ok = (req.result == UnityWebRequest.Result.Success);
#else
        ok = (!req.isNetworkError && !req.isHttpError);
#endif
        D($"[HTTP←] code={req.responseCode}, ok={ok}, err={req.error}, respBytes={(req.downloadHandler?.data?.Length ?? -1)}");

        if (dumpResponsesToFile) SafeWriteFile("gpt_last_response.json", req.downloadHandler?.text);
        HandleCompletionResponse(ok, req.downloadHandler?.text);
    }

    private IEnumerator SendAudioRequest(string base64Audio, string format)
    {
        const string endpoint = "https://api.openai.com/v1/chat/completions";
        var sb = new StringBuilder(4096);
        sb.Append("{");
        sb.Append("\"model\":\"").Append(chatModel).Append("\",");
        sb.Append("\"modalities\":[\"text\",\"audio\"],");
                if (preferModelAudio)
                {
                        // Only use 'wav' for HTTP, never for Realtime
                        string httpAudioFormat = string.IsNullOrEmpty(gptAudioFormat) ? "wav" : gptAudioFormat;
                        if (httpAudioFormat != "pcm16" && httpAudioFormat != "g711_ulaw" && httpAudioFormat != "g711_alaw") httpAudioFormat = "wav";
                        sb.Append("\"audio\":{")
                            .Append("\"voice\":\"").Append(string.IsNullOrEmpty(gptVoice) ? "alloy" : gptVoice).Append("\",")
                            .Append("\"format\":\"").Append(httpAudioFormat).Append("\"")
                            .Append("},");
                }
        sb.Append("\"messages\":[");
        bool first = true;
        string finalInstr = BuildFinalInstructions();
        EchoPrompt("HTTP/Audio.Instructions(final)", finalInstr, true);
        AppendMsg(sb, "system", finalInstr, ref first);
        int start = Mathf.Max(0, history.Count - 2 * Mathf.Max(1, maxHistoryTurnsToSend));
        for (int i = start; i < history.Count; i++) AppendMsg(sb, history[i].role, history[i].content, ref first);
        if (!first) sb.Append(",");
        sb.Append("{\"role\":\"user\",\"content\":[");
        sb.Append("{\"type\":\"input_audio\",\"input_audio\":{\"data\":\"").Append(base64Audio)
          .Append("\",\"format\":\"").Append(format).Append("\"}}]}]}");
        string json = sb.ToString();

        var req = new UnityWebRequest(endpoint, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        req.timeout = Mathf.Max(5, requestTimeoutSeconds);

        D($"[HTTP→] {endpoint}\npayloadBytes={bodyRaw.Length}, base64Len={base64Audio.Length}");
        yield return req.SendWebRequest();

        bool ok;
#if UNITY_2020_2_OR_NEWER
        ok = (req.result == UnityWebRequest.Result.Success);
#else
        ok = (!req.isNetworkError && !req.isHttpError);
#endif
        D($"[HTTP←] code={req.responseCode}, ok={ok}, err={req.error}, respBytes={(req.downloadHandler?.data?.Length ?? -1)}");
        if (dumpResponsesToFile) SafeWriteFile("gpt_last_response.json", req.downloadHandler?.text);
        HandleCompletionResponse(ok, req.downloadHandler?.text);
    }

    // ========== Realtime(WebSocket) ==========
    private IEnumerator SendTextViaRealtime(string text)
    {
        if (_responseInProgress)
        {
            Warn("[Realtime] Response already in progress, queueing skipped (text input)");
            yield break;
        }
        _responseInProgress = true;
        try
        {
            yield return EnsureRealtimeConnected();
            yield return WaitForSessionReady(3f); // 等待会话指令就绪

        // 将“当前相位指令”作为 developer 文本钉入对话
        if (prependPhaseTextAsDeveloperItem)
        {
            string dev = BuildInstructionsForPhase(GameManager.eGameStatus);
            EchoPrompt("RT/DevItem(phase).Instructions(final)", dev, true);
            string devMsg = "{\"type\":\"conversation.item.create\",\"item\":{\"type\":\"message\",\"role\":\"developer\",\"content\":[{\"type\":\"input_text\",\"text\":\"" + Escape(dev) + "\"}]}}";
            yield return SendWsText(devMsg);
        }

        // 用户文本（如有）
        string userText = string.IsNullOrEmpty(text) ? "(empty)" : text;
        yield return SendWsText("{\"type\":\"conversation.item.create\",\"item\":{\"type\":\"message\",\"role\":\"user\",\"content\":[{\"type\":\"input_text\",\"text\":\"" + Escape(userText) + "\"}]}}");

        // 触发响应
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"response.create\",\"response\":{");

        string instr = BuildFinalInstructions();
        EchoPrompt("RT/Text.Instructions(final)", instr, true);

        sb.Append("\"modalities\":[\"text\",\"audio\"],");
        sb.Append("\"temperature\":0.8,");
        sb.Append("\"max_output_tokens\":180");
        sb.Append(",\"instructions\":\"").Append(Escape(instr)).Append("\"");
        sb.Append("}}");

        yield return SendWsText(sb.ToString());
        }
        finally
        {
            _responseInProgress = false;
        }
    }

    private IEnumerator SendPcmViaRealtime(byte[] pcm16User, string extraInstruction)
    {
        if (_responseInProgress)
        {
            Warn($"[Realtime] Response already in progress, queueing skipped (audio input, pcm={pcm16User?.Length ?? 0} bytes)");
            yield break;
        }
        _responseInProgress = true;
        try
        {
            yield return EnsureRealtimeConnected();
            yield return WaitForSessionReady(3f); // 等待会话指令就绪

        // 1) 先钉开发者文本（当前相位）
        if (prependPhaseTextAsDeveloperItem)
        {
            string dev = BuildInstructionsForPhase(GameManager.eGameStatus);
            EchoPrompt("RT/DevItem(phase).Instructions(final)", dev, true);
            string devMsg = "{\"type\":\"conversation.item.create\",\"item\":{\"type\":\"message\",\"role\":\"system\",\"content\":[{\"type\":\"input_text\",\"text\":\"" + Escape(dev) + "\"}]}}";
            yield return SendWsText(devMsg);
        }

        // 2) 送音频（必须至少 100ms）
        if (pcm16User == null || pcm16User.Length < 4800) // 24kHz * 0.1s * 2 bytes = 4800
        {
            Warn("[Realtime] Audio buffer too small (< 100ms). Skipping request.");
            _responseInProgress = false;
            // Immediately process next queued request
            ProcessNextQueuedRequest();
            yield break;
        }
        string b64 = Convert.ToBase64String(pcm16User);
        string append = "{\"type\":\"input_audio_buffer.append\",\"audio\":\"" + b64 + "\"}";
        yield return SendWsText(append);
        yield return SendWsText("{\"type\":\"input_audio_buffer.commit\"}");

        // 3) 触发响应（仍带 instructions）
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"response.create\",\"response\":{");

        string finalInstr = BuildFinalInstructions(string.IsNullOrWhiteSpace(extraInstruction) ? null : extraInstruction);
        EchoPrompt("RT/Audio.Instructions(final)", finalInstr, true);

        sb.Append("\"modalities\":[\"text\",\"audio\"],");
        sb.Append("\"temperature\":0.8,");
        sb.Append("\"max_output_tokens\":180");
        sb.Append(",\"instructions\":\"").Append(Escape(finalInstr)).Append("\"");
        sb.Append("}}");

        yield return SendWsText(sb.ToString());
        }
        finally
        {
            _responseInProgress = false;
        }
    }

    private IEnumerator EnsureRealtimeConnected()
    {
        if (_ws != null && _ws.State == WebSocketState.Open) yield break;

        if (_wsCts != null)
        {
            _wsCts.Cancel();
            _wsCts.Dispose();
        }
        _wsCts = new CancellationTokenSource();
        _audioAccum = new MemoryStream();
        _textAccum = new StringBuilder(512);
        _audioChunkCount = 0;
        _sessionReady = false;

        string url = "wss://api.openai.com/v1/realtime?model=" + Uri.EscapeDataString(string.IsNullOrEmpty(realtimeModel) ? "gpt-realtime-2025-08-28" : realtimeModel);
        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("Authorization", "Bearer " + apiKey);
        _ws.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        Task t = _ws.ConnectAsync(new Uri(url), _wsCts.Token);
        while (!t.IsCompleted) yield return null;

        if (_ws.State != WebSocketState.Open)
        {
            Warn("[Realtime] WebSocket 连接失败：" + _ws.State);
            yield break;
        }

        Debug.Log("[Realtime] ✅ WebSocket connected: " + url);
        _ = Task.Run(ReceiveLoop);

        // === 发送 session.update ===
        var cfg = new StringBuilder();
        cfg.Append("{\"type\":\"session.update\",\"session\":{");
        cfg.Append("\"modalities\":[\"text\",\"audio\"],");
        cfg.Append("\"voice\":\"").Append(string.IsNullOrEmpty(gptVoice) ? "alloy" : gptVoice).Append("\",");

        // turn_detection
        cfg.Append("\"turn_detection\":{")
          .Append("\"type\":\"server_vad\",")
          .Append("\"silence_duration_ms\":").Append(AI.Prompts.Customizations.SilenceDurationMs).Append(",")
          .Append("\"prefix_padding_ms\":").Append(AI.Prompts.Customizations.PrefixPaddingMs)
          .Append("},");
        // 输出音频格式（string，不是object）
        // Always use pcm16 for Realtime API regardless of Inspector value
        cfg.Append("\"output_audio_format\":\"pcm16\",");

        // 输入音频格式（string，不是object）
        cfg.Append("\"input_audio_format\":\"pcm16\"");

        // 指令
        string sessionInstr = BuildFinalInstructions();
        EchoPrompt("RT/session.update.Instructions(final)", sessionInstr, true);
        cfg.Append(",\"instructions\":\"").Append(Escape(sessionInstr)).Append("\"");
        cfg.Append("}}");

        Debug.Log("[Realtime] → sending session.update ...");
        yield return SendWsText(cfg.ToString());
        Debug.Log("[Realtime] ✓ session.update sent");

        // ✅ 等待 ack
        yield return WaitForSessionReady(15f);  // 15s timeout for slow networks
        D("[Realtime] session.update applied & acked.");
    }

    private async Task ReceiveLoop()
    {
        var buffer = new ArraySegment<byte>(new byte[1 << 20]); // 1MB 临时
        var sb = new StringBuilder(1 << 20);
        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open && !_wsCts.IsCancellationRequested)
            {
                sb.Length = 0;
                WebSocketReceiveResult res;
                do
                {
                    res = await _ws.ReceiveAsync(buffer, _wsCts.Token).ConfigureAwait(false);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "ok", _wsCts.Token).ConfigureAwait(false);
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer.Array, 0, res.Count));
                } while (!res.EndOfMessage);
                string msg = sb.ToString();
                _eventQueue.Enqueue(msg);
                if (realtimeDumpEvents) SafeAppendFile("realtime_events.ndjson", msg + "\n");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Realtime] ReceiveLoop异常: " + e.Message);
        }
    }

    private void Update()
    {
        while (_eventQueue.TryDequeue(out var json))
        {
            HandleRealtimeEvent(json);
        }
    }

    private void HandleRealtimeEvent(string json)
    {
        if (!verboseDebug && Time.realtimeSinceStartup < _nextLog)
        {
        }
        else
        {
            _nextLog = Time.realtimeSinceStartup + Mathf.Max(0.05f, debugLogInterval);
            D("[RT←] " + TrimForLog(json));
        }

        string type = ExtractJsonString(json, "type");
        if (string.IsNullOrEmpty(type)) return;

        // === 文本增量（兼容新旧命名）
        if (type == "response.text.delta" || type.Contains("response.output_text.delta"))
        {
            string delta = ExtractJsonString(json, "delta");
            if (!string.IsNullOrEmpty(delta))
            {
                _textAccum?.Append(delta);
                if (outputText) outputText.text += delta;
            }
            return;
        }

        // === 音频增量
        if (type.Contains("audio.delta"))
        {
            string b64 = ExtractJsonString(json, "delta");
            if (string.IsNullOrEmpty(b64))
            {
                b64 = ExtractJsonString(json, "data");
                if (string.IsNullOrEmpty(b64)) b64 = ExtractNested(json, "audio", "data");
            }
            if (!string.IsNullOrEmpty(b64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(b64);
                    _audioAccum?.Write(bytes, 0, bytes.Length);
                    _audioChunkCount++;
                    DT("[RT-audio]", $"chunk#{_audioChunkCount} bytes={bytes.Length} total={_audioAccum?.Length}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Realtime] audio.delta 解码失败: " + e.Message);
                }
            }
            return;
        }

        // === 模型语音字幕（delta）
        if (type == "response.audio_transcript.delta")
        {
            string delta = ExtractJsonString(json, "delta") ?? ExtractJsonString(json, "text");
            if (!string.IsNullOrEmpty(delta))
            {
                _assistantTranscriptAccum.Append(delta);
                Debug.Log("[STT][assistant/delta] " + delta);
            }
            return;
        }

        // === 模型语音字幕（done）→ 触发动作（仅助理）
        if (type == "response.audio_transcript.done")
        {
            string full = ExtractJsonString(json, "transcript") ?? ExtractJsonString(json, "text");
            if (string.IsNullOrEmpty(full) && _assistantTranscriptAccum.Length > 0) full = _assistantTranscriptAccum.ToString();
            _assistantTranscriptAccum.Length = 0;

            if (!string.IsNullOrEmpty(full) && reactToAssistantTranscript)
            {
                Debug.Log("[STT][assistant] " + full);
                TryFireFromAssistantTranscript(full);
            }
            return;
        }

        // === 完成
        if (type.Contains("response.completed") || type.Contains("response.done"))
        {
            DT("[RT]", "response completed/done");

            string replyText = _textAccum != null && _textAccum.Length > 0 ? _textAccum.ToString() : null;
            if (!string.IsNullOrEmpty(replyText))
            {
                history.Add(new Message { role = "assistant", content = replyText });
                TrimHistory();
            }

            Action afterPlayback = () =>
            {
                // After playback, clear response flag and process next queued request if any
                _responseInProgress = false;
                ProcessNextQueuedRequest();
            };

            if (_audioAccum != null && _audioAccum.Length > 0 && ttsPlayer != null && preferModelAudio)
            {
                byte[] pcm = _audioAccum.ToArray();
                byte[] wav = BuildWavFromPcm16(pcm, realtimeSampleRate, 1);
                string b64wav = Convert.ToBase64String(wav);

                D($"[Playback] Realtime音频完成 chunks={_audioChunkCount}, wavBytes={wav.Length}");
                MarkSpeakingStart();
                ttsPlayer.PlayModelAudioBase64(b64wav, "wav", () =>
                {
                    try { ttsDriver?.CancelWait(); } catch { }
                    MarkSpeakingEnd();
                    onReplyComplete?.Invoke();
                    afterPlayback();
                });
            }
            else
            {
                if (ttsPlayer != null && !string.IsNullOrEmpty(replyText))
                {
                    D("[Playback] 回退到本地TTS（文本）");
                    MarkSpeakingStart();
                    ttsPlayer.Speak(replyText, () =>
                    {
                        try { ttsDriver?.CancelWait(); } catch { }
                        MarkSpeakingEnd();
                        onReplyComplete?.Invoke();
                        afterPlayback();
                    });
                }
                else
                {
                    try { ttsDriver?.CancelWait(); } catch { }
                    onReplyComplete?.Invoke();
                    afterPlayback();
                }
            }

            _audioAccum?.Dispose();
            _audioAccum = new MemoryStream();
            _audioChunkCount = 0;
            _textAccum?.Clear();
            return;
        }
    // ...existing code...

        // === 会话已创建/更新（ACK）
        if (type == "session.created" || type.Contains("session.created"))
        {
            D("[RT] session.created");
            return;
        }

        if (type == "session.updated" || type.Contains("session.updated"))
        {
            _sessionUpdatedTick++;
            _sessionReady = true; // 只有收到此 ACK 才认为会话指令生效
            D("[RT] session.updated ack");
            return;
        }

        // === 服务器错误
        if (type == "error" || json.Contains("\"type\":\"error\""))
        {
            string msg = ExtractNested(json, "error", "message");
            Warn("[Realtime][Server ERROR] " + (msg ?? json));
            return;
        }

        DT("[RT]", type);
    }

    private void HandleCompletionResponse(bool ok, string json)
    {
        if (!ok)
        {
            Warn("❌ 请求失败");
            ttsDriver?.CancelWait();
            onReplyComplete?.Invoke();
            return;
        }

        string replyText = null;
        string audioB64 = null;
        string audioFmt = null;

        try
        {
            GPTResponse resp = JsonUtility.FromJson<GPTResponse>(json);
            if (resp != null && resp.choices != null && resp.choices.Length > 0 && resp.choices[0].message != null)
            {
                var msg = resp.choices[0].message;
                replyText = !string.IsNullOrEmpty(msg.content) ? msg.content : (msg.audio != null && !string.IsNullOrEmpty(msg.audio.transcript) ? msg.audio.transcript : null);
                if (msg.audio != null && !string.IsNullOrEmpty(msg.audio.data))
                {
                    audioB64 = msg.audio.data;
                    audioFmt = string.IsNullOrEmpty(msg.audio.format) ? gptAudioFormat : msg.audio.format;
                }
                D($"[Parse] textLen={(replyText == null ? 0 : replyText.Length)}, audioB64Len={(audioB64 == null ? 0 : audioB64.Length)}, fmt={audioFmt}");
            }
            else
            {
                Warn("[Parse] choices/message 为空，无法提取内容");
            }
        }
        catch (Exception e)
        {
            Warn("JSON 解析失败: " + e.Message);
        }

        if (outputText) outputText.text = string.IsNullOrEmpty(replyText) ? "(empty)" : replyText;

        if (!string.IsNullOrEmpty(replyText))
        {
            // HTTP 模式：直接用助理文本匹配（此处只做匹配与调度，真正触发会在语音开始后延时）
            TryFireFromAssistantTranscript(replyText);
            history.Add(new Message { role = "assistant", content = replyText });
            TrimHistory();
        }

        if (speechRecognizer != null && ttsPlayer != null)
        {
            speechRecognizer.ttsToInterrupt = ttsPlayer;
            ttsPlayer.BindRecognizer(speechRecognizer);
            D("[Bind] 已将 TTS 绑定识别器（可被打断）");
        }
        else
        {
            Warn($"[Bind] 未绑定：speechRecognizer={(speechRecognizer == null ? "null" : "ok")}, ttsPlayer={(ttsPlayer == null ? "null" : "ok")}");
        }

        void AfterPlayback()
        {
            try { ttsDriver?.CancelWait(); } catch { }
            MarkSpeakingEnd();
            D("[Playback] 完成回放回调 onReplyComplete()");
            onReplyComplete?.Invoke();
        }

        if (preferModelAudio && ttsPlayer != null && !string.IsNullOrEmpty(audioB64))
        {
            D("[Playback] 使用模型返回的音频播放(HTTP)");
            MarkSpeakingStart();
            ttsPlayer.PlayModelAudioBase64(audioB64, string.IsNullOrEmpty(audioFmt) ? "wav" : audioFmt, AfterPlayback);
        }
        else
        {
            if (ttsPlayer != null && !string.IsNullOrEmpty(replyText))
            {
                D("[Playback] 回退到本地 TTS 播放文本(HTTP)");
                MarkSpeakingStart();
                ttsPlayer.Speak(replyText, AfterPlayback);
            }
            else
            {
                Warn("[Playback] 没有可播放的内容");
                AfterPlayback();
            }
        }
    }

    // ========= 仅基于“助理字幕/文本”的 4 个动作（延时触发实现） =========
    private void TryFireFromAssistantTranscript(string transcript)
    {
        if (ttsDriver == null || string.IsNullOrWhiteSpace(transcript)) return;
        string t = Normalize(transcript); // 去标点/小写/压空白

        switch (GameManager.eGameStatus)
        {
            case GameManager.GameState.Interphase:
                if (IsNutrient(t))
                {
                    Debug.Log("[React][assistant] Interphase → DZ13 (will delay after isSpeaking)");
                    ScheduleTriggerAfterSpeaking(() => ttsDriver.TriggerDZ13(), delayFirstGroupSec, "Interphase:DZ13");
                }
                break;

            case GameManager.GameState.Prophase:
                if (IsXShape(t))
                {
                    Debug.Log("[React][assistant] Prophase → DZ18 (will delay after isSpeaking)");
                    ScheduleTriggerAfterSpeaking(() => ttsDriver.TriggerDZ18(), delay4GroupSec, "Metaphase:DZ18");
                }
                break;

            case GameManager.GameState.Metaphase:
                if (IsRowArrange(t))
                {
                    Debug.Log("[React][assistant] Metaphase → DZ20 (will delay after isSpeaking)");
                    ScheduleTriggerAfterSpeaking(() => ttsDriver.TriggerDZ20(), delaySecondGroupSec, "Metaphase:DZ20");
                }
                break;

            case GameManager.GameState.Anaphase:
                if (IsApartSeparate(t))
                {
                    Debug.Log("[React][assistant] Telophase → DZ22 (will delay after isSpeaking)");
                    ScheduleTriggerAfterSpeaking(() => ttsDriver.TriggerDZ22(), delayThirdGroupSec, "Telophase:DZ22");
                }
                break;

            default:
                break; // 其它阶段不触发
        }
    }

    // ====== Speaking 标记与延时调度 ======
    private void MarkSpeakingStart()
    {
        _isSpeaking = true;
        D("[Speak] start");
        
        // Notify speech recognizer that TTS is starting (for self-interrupt protection)
        if (speechRecognizer != null)
        {
            try
            {
                var fieldInfo = speechRecognizer.GetType().GetField("_ttsStartTime", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(speechRecognizer, Time.realtimeSinceStartup);
                    D("[Speak] Notified speech recognizer of TTS start time");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Speak] Failed to notify recognizer: {e.Message}");
            }
        }
    }

    private void MarkSpeakingEnd()
    {
        _isSpeaking = false;
        D("[Speak] end");
    }

    private void ScheduleTriggerAfterSpeaking(Action action, float delaySec, string tag)
    {
        StartCoroutine(CoScheduleAfterSpeaking(action, delaySec, tag));
    }

    private IEnumerator CoScheduleAfterSpeaking(Action action, float delaySec, string tag)
    {
        float waited = 0f;
        while (!_isSpeaking && waited < speakingWaitGraceSec)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        if (_isSpeaking)
            D($"[Trigger] 侦测到 isSpeaking，{delaySec:0.0}s 后触发：{tag}");
        else
            D($"[Trigger] 未侦测到 isSpeaking（{speakingWaitGraceSec:0.0}s 超时），仍延时 {delaySec:0.0}s 触发：{tag}");

        yield return new WaitForSeconds(delaySec);

        try { action?.Invoke(); }
        catch (Exception e) { Debug.LogWarning($"[Trigger:{tag}] 调用失败: {e.Message}"); }
    }

    // ========= 4 个匹配函数（仅保留这些） =========
    private bool IsNutrient(string s)
    {
        // 营养/能量/喂/吃（英文）；已小写和去标点
        string[] keys = { "eat food", "eats", "eating", "food" };
        return ContainsAnyNormalized(s, keys);
    }

    private bool IsXShape(string s)
    {
        string[] keys = { "condense", "x shaped", "x-shaped", "x-shaped chromosome", "x shaped chromosome", "condenses into" };
        return ContainsAnyNormalized(s, keys);
    }

    private bool IsRowArrange(string s)
    {
        string[] keys = { "arrange them in a neat row", "arrange them in a row", "line them up", "line up", "single row", "in a single row", "align in a row", "align at the center", "line up at the center", "line up in the middle", "align in the middle" };
        return ContainsAnyNormalized(s, keys);
    }

    private bool IsApartSeparate(string s)
    {
        string[] keys = { "apart", "move apart", "pull apart", "separate", "separating", "separate them", "separate the chromatids", "split", "split them", "to opposite ends", "to opposite sides", "to opposite poles", "move to opposite", "pull to opposite" };
        return ContainsAnyNormalized(s, keys);
    }

    // ========= 文本规范化与匹配 =========
    private string Normalize(string x)
    {
        if (string.IsNullOrEmpty(x)) return "";
        var sb = new StringBuilder(x.Length);
        foreach (var c in x)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)) sb.Append(char.ToLowerInvariant(c));
            else sb.Append(' ');
        }
        // 收缩多空白
        return Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
    }

    private bool ContainsAnyNormalized(string hay, string[] keys)
    {
        if (string.IsNullOrEmpty(hay) || keys == null || keys.Length == 0) return false;
        for (int i = 0; i < keys.Length; i++)
        {
            var k = Normalize(keys[i] ?? "");
            if (string.IsNullOrEmpty(k)) continue;
            if (hay.Contains(k)) return true;
        }
        return false;
    }

    // ========= WAV/PCM 工具 =========
    private static byte[] ExtractPcm16FromWav(byte[] wav)
    {
        if (wav == null || wav.Length < 44) return null;
        int len = wav.Length - 44;
        var pcm = new byte[len];
        Buffer.BlockCopy(wav, 44, pcm, 0, len);
        return pcm;
    }

    // Robust WAV parsing that handles variable chunk sizes
    private static byte[] ExtractPcm16FromWavRobust(byte[] wav)
    {
        if (wav == null || wav.Length < 12)
        {
            Debug.LogWarning($"[WAV] Buffer too small: {wav?.Length ?? 0} bytes");
            return null;
        }
        
        try
        {
            // Check RIFF header
            if (wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F')
            {
                Debug.LogWarning("[WAV] Invalid RIFF header");
                return null;
            }
            if (wav[8] != 'W' || wav[9] != 'A' || wav[10] != 'V' || wav[11] != 'E')
            {
                Debug.LogWarning("[WAV] Invalid WAVE header");
                return null;
            }

            // Find "data" chunk by scanning through all chunks
            int dataPos = -1;
            int dataSize = 0;
            int i = 12;
            int chunkCount = 0;
            
            while (i + 8 <= wav.Length)
            {
                // Read chunk ID (4 bytes)
                char c0 = (char)wav[i];
                char c1 = (char)wav[i + 1];
                char c2 = (char)wav[i + 2];
                char c3 = (char)wav[i + 3];
                string chunkId = new string(new[] { c0, c1, c2, c3 });
                i += 4;

                // Read chunk size (4 bytes, little-endian)
                if (i + 4 > wav.Length) break;
                int chunkSize = wav[i] | (wav[i + 1] << 8) | (wav[i + 2] << 16) | (wav[i + 3] << 24);
                i += 4;
                
                chunkCount++;
                Debug.Log($"[WAV] Chunk {chunkCount}: '{chunkId}' size={chunkSize} at offset={i}");

                if (chunkId == "data")
                {
                    dataPos = i;
                    dataSize = chunkSize;
                    Debug.Log($"[WAV] Found 'data' chunk: pos={dataPos}, size={dataSize}");
                    break;
                }

                // Skip to next chunk (align to even boundary)
                i += chunkSize;
                if ((chunkSize & 1) == 1) i++; // Padding byte if chunk size is odd
            }

            if (dataPos >= 0 && dataSize > 0)
            {
                int pcmLen = Mathf.Min(dataSize, wav.Length - dataPos);
                if (pcmLen > 0)
                {
                    byte[] pcm = new byte[pcmLen];
                    Buffer.BlockCopy(wav, dataPos, pcm, 0, pcmLen);
                    Debug.Log($"[WAV] ✓ Extracted PCM: {pcmLen} bytes");
                    return pcm;
                }
            }

            // Fallback: if no data chunk found, try simple 44-byte header
            Debug.Log("[WAV] No data chunk found, trying 44-byte fallback");
            if (wav.Length > 44)
            {
                byte[] pcm = new byte[wav.Length - 44];
                Buffer.BlockCopy(wav, 44, pcm, 0, pcm.Length);
                Debug.Log($"[WAV] ✓ Fallback extracted: {pcm.Length} bytes");
                return pcm;
            }

            Debug.LogWarning("[WAV] Failed: WAV too short and no valid chunks found");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WAV] Robust extraction exception: {e.Message}");
            return null;
        }
    }

    private static byte[] BuildWavFromPcm16(byte[] pcm, int sampleRate, int channels)
    {
        if (pcm == null) pcm = Array.Empty<byte>();
        int byteRate = sampleRate * channels * 2;
        int blockAlign = channels * 2;
        int subchunk2Size = pcm.Length;
        int chunkSize = 36 + subchunk2Size;
        using (var ms = new MemoryStream(44 + subchunk2Size))
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(subchunk2Size);
            bw.Write(pcm);
            return ms.ToArray();
        }
    }

    // 解析 WAV 头，返回采样率/声道，并提取 PCM16（预留）
    private static bool TryExtractPcmAndCheckFormat(byte[] wav, out byte[] pcm, out int sr, out int ch)
    {
        pcm = null; sr = 0; ch = 0;
        if (wav == null || wav.Length < 44) return false;
        try
        {
            using (var ms = new MemoryStream(wav))
            using (var br = new BinaryReader(ms))
            {
                ms.Position = 22; // 声道
                ch = br.ReadInt16();
                ms.Position = 24; // 采样率
                sr = br.ReadInt32();
                // 假设 16-bit PCM
                pcm = ExtractPcm16FromWav(wav);
                return pcm != null;
            }
        }
        catch { return false; }
    }

    private static byte[] ConcatBytes(byte[] a, byte[] b)
    {
        if (a == null || a.Length == 0) return b ?? Array.Empty<byte>();
        if (b == null || b.Length == 0) return a;
        byte[] r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }

    private IEnumerator SendWsText(string json)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
        {
            Warn("[Realtime] WebSocket 未连接。");
            yield break;
        }
        byte[] data = Encoding.UTF8.GetBytes(json);
        D("[RT→] " + TrimForLog(json));
        Task t = null;
        try
        {
            t = _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _wsCts.Token);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Realtime] SendAsync 异常: " + ex.Message);
            yield break;
        }
        while (!t.IsCompleted) yield return null;
        if (t.IsFaulted) Debug.LogWarning("[Realtime] SendAsync Faulted: " + (t.Exception?.GetBaseException().Message ?? "unknown"));
        if (t.IsCanceled) Debug.LogWarning("[Realtime] SendAsync Canceled");
    }

    private string ExtractJsonString(string json, string key)
    {
        string pat = "\"" + key + "\":\"";
        int i = json.IndexOf(pat, StringComparison.Ordinal);
        if (i < 0) return null;
        int s = i + pat.Length;
        int e = json.IndexOf("\"", s, StringComparison.Ordinal);
        if (e < 0) return null;
        return json.Substring(s, e - s);
    }

    private string ExtractNested(string json, string objKey, string innerKey)
    {
        int i = json.IndexOf("\"" + objKey + "\":{", StringComparison.Ordinal);
        if (i < 0) return null;
        int brace = 0;
        int start = -1;
        int end = -1;
        for (int p = i; p < json.Length; p++)
        {
            if (json[p] == '{')
            {
                brace++;
                if (start < 0) start = p;
            }
            else if (json[p] == '}')
            {
                brace--;
                if (brace == 0)
                {
                    end = p; break;
                }
            }
        }
        if (start < 0 || end < 0) return null;
        string sub = json.Substring(start, end - start + 1);
        return ExtractJsonString(sub, innerKey);
    }

    private void OnDisable()
    {
        try { _wsCts?.Cancel(); } catch { }
        try { _ws?.Dispose(); } catch { }
        _ws = null;
        _wsCts = null;
    }

    // ================== 相位 → 说明文本 ==================
    private static string BuildSystemPromptForCurrentPhase()
    {
        switch (GameManager.eGameStatus)
        {
            case GameManager.GameState.Interphase: return PhaseText(GameManager.GameState.Interphase);
            case GameManager.GameState.Prophase: return PhaseText(GameManager.GameState.Prophase);
            case GameManager.GameState.Metaphase: return PhaseText(GameManager.GameState.Metaphase);
            case GameManager.GameState.Anaphase: return PhaseText(GameManager.GameState.Anaphase);
            case GameManager.GameState.Telophase: return PhaseText(GameManager.GameState.Telophase);
            default: return ""; // Intro/Reset/GameOver：仅用 systemPrompt
        }
    }

    // 拆成静态方法便于重用（预缓存也用它）
    private static string PhaseText(GameManager.GameState gs)
    {
        const string interphase = @"Interphase Cureent is interphase. The goal in interphase is to generate ATP and replicate the centrioles. Tell the student bring the three capsule-shaped nutrients to the mitochondria; as they are absorbed, a green ATP bar fills and must be completely full before moving on. After ATP is generated, instruct the student to replicate the centrioles by grabbing one centriole and placing it a short distance away, as practiced in the tutorial. When explaining energy, use: “The mitochondria absorb nutrients to produce energy—just like when we eat food to get energy to move,” When answering questions in this phase, start with empathy and give two or three sentences that only cover these tasks. Example template: “You’re in Interphase. Bring the three capsule nutrients to the mitochondria; the green ATP bar must fill completely. After ATP is generated, replicate the centrioles by grabbing one and placing it a short distance away. ";
        const string prophase = @"Prophase Cureent is Prophase. The key concept in prophase is that thread-like DNA condenses into chromosomes. Tell the student to find the red, thread-like DNA and hold it for three seconds so it condenses into an X-shaped chromosome. If they are confused, point out the blue chromosomes as examples of DNA that already condensed in Prophase and ask them to do the same with the red DNA. Keep responses to two or three sentences and do not discuss other stages. Example template: “You’re in Prophase. Find the red, thread-like DNA and hold it for 3 seconds so it condenses into an X-shaped chromosome. If needed, use the blue chromosomes as examples of already-condensed DNA.”";
        const string metaphase = @"Metaphase Current is Metaphase. The goal in metaphase is to align chromosomes at the center. Explain that spindle fibers from the centrioles pull chromosomes to the cell’s center so they line up in a single row; the red chromosome is misaligned and should be moved to align with the others. If the student is unsure, direct them to the glowing yellow particle effect and have them place the red chromosome slightly above that spot. Keep answers short, and do not explain Prophase or Anaphase details. Example template: “You’re in Metaphase. Spindle fibers pull chromosomes to the middle—line them up at the center in a single row. Move the red chromosome to the glowing yellow spot (slightly above it) to align.”";
        const string anaphase = @"Anaphase Current is Anaphase. The goal in Anaphase is to separate chromatids to opposite ends. Instruct the student to split chromosomes into chromatids and move them to opposite ends, using the blue chromatids as examples. If needed, tell them to place chromatids on the glowing yellow particle effects at each end. Keep to two or three sentences and do not revisit Metaphase or narrate Telophase outcomes. Example template: “You’re in Anaphase. Separate the chromatids and move them to opposite ends of the cell. Use the glowing yellow markers at each end as targets.”";
        const string telophase = @"Telophase Current is Telophase. The goal in Telophase is to complete division and trigger the ending. Because chromatids moved to both ends in Anaphase, each daughter cell will receive identical DNA. Instruct the student to touch the wound on the arm again for three seconds to repeat the healing process until it’s complete; the ending scene will start automatically. Keep the answer brief and on-task. Example template: “You’re in Telophase. Each side now has identical DNA, so you just need to finish the last step. Touch the arm wound for 3 seconds until healing completes; the ending scene will start.”";

        switch (gs)
        {
            case GameManager.GameState.Interphase: return interphase;
            case GameManager.GameState.Prophase: return prophase;
            case GameManager.GameState.Metaphase: return metaphase;
            case GameManager.GameState.Anaphase: return anaphase;
            case GameManager.GameState.Telophase: return telophase;
            default: return "";
        }
    }

    // ★ instruction debug
    private void EchoPrompt(string origin, string prompt, bool finalFlag)
    {
        if (!logPrompts) return;
        string tag = finalFlag ? "[Instruction(final)]" : "[Instruction(raw)]";
        string head = $"{tag} @{origin}: len={(prompt == null ? 0 : prompt.Length)}";
        D($"{head}\n{TrimPromptForDisplay(prompt)}");
        if (promptEchoText != null) { promptEchoText.text = $"{head}\n{TrimPromptForDisplay(prompt)}"; }
        SafeAppendFile("gpt_prompt_log.txt", $"\n\n{DateTime.Now:O} {head}\n{(prompt ?? "(null)")}");
        if (finalFlag) lastPrompt = prompt ?? "";
    }

    private string TrimPromptForDisplay(string s)
    {
        if (string.IsNullOrEmpty(s)) return "(null)";
        int n = Mathf.Max(200, promptLogMaxChars);
        return s.Length <= n ? s : s.Substring(0, n) + $" ...(+{s.Length - n} chars)";
    }

    // === Wait for session.updated ACK ===
    private IEnumerator WaitForSessionReady(float timeoutSec = 5f)
    {
        float t = 0f;
        while (!_sessionReady && t < timeoutSec)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (!_sessionReady)
        {
            Warn($"[Realtime] session.update ACK timeout ({timeoutSec:0.0}s): instructions may not be fully applied. Continuing anyway.");
        }
        else
        {
            D($"[Realtime] ✅ session.updated ACK received in {t:0.0}s");
        }
    }

    // === 可选：阶段切换时明确覆盖会话指令 ===
    public IEnumerator PushPhaseToSession()
    {
        yield return EnsureRealtimeConnected();
        string sessionInstr = BuildFinalInstructions(); // 全局+阶段
        var j = "{\"type\":\"session.update\",\"session\":{\"instructions\":\"" + Escape(sessionInstr) + "\"}}";
        _sessionReady = false;
        EchoPrompt("RT/PushPhase.Instructions(final)", sessionInstr, true);
        yield return SendWsText(j);
        yield return WaitForSessionReady(3f);
        D("[Realtime] ✓ 阶段指令已覆盖并确认。");
    }

    // ========================= 预缓存实现（文本+TTS） =========================
    private IEnumerator PrecacheAllPhaseAssets()
    {
        // 准备五个相位
        var phases = new[]
        {
            GameManager.GameState.Interphase,
            GameManager.GameState.Prophase,
            GameManager.GameState.Metaphase,
            GameManager.GameState.Anaphase,
            GameManager.GameState.Telophase
        };

        foreach (var gs in phases)
        {
            string instr = BuildInstructionsForPhase(gs);
            _phaseTextCache[gs] = instr;

            // 保存文本
            if (savePhaseTextFiles)
            {
                try { File.WriteAllText(GetPhaseTxtPath(gs), instr); }
                catch (Exception e) { Debug.LogWarning("[PhaseCache] 写入文本失败: " + e.Message); }
            }

            // 生成 TTS
            if (generatePhaseTtsAudio)
            {
                yield return GeneratePhaseTTS(gs, instr);
            }
        }

        D("[PhaseCache] ✓ 全部相位指令预缓存完成。");
    }

    private string GetPhaseBaseName(GameManager.GameState gs) { return $"phase_{gs.ToString().ToLower()}"; }
    private string GetPhaseTxtPath(GameManager.GameState gs) { return Path.Combine(Application.persistentDataPath, GetPhaseBaseName(gs) + ".txt"); }
    private string GetPhaseWavPath(GameManager.GameState gs) { return Path.Combine(Application.persistentDataPath, GetPhaseBaseName(gs) + ".wav"); }

    private IEnumerator GeneratePhaseTTS(GameManager.GameState gs, string text)
    {
        // 用 OpenAI TTS: POST /v1/audio/speech 返回原始音频二进制
        const string endpoint = "https://api.openai.com/v1/audio/speech";
        var sb = new StringBuilder(4096);
        sb.Append("{");
        sb.Append("\"model\":\"").Append(string.IsNullOrEmpty(ttsModel) ? "openai/gpt-oss-120b" : ttsModel).Append("\",");
        sb.Append("\"voice\":\"").Append(string.IsNullOrEmpty(ttsVoice) ? "alloy" : ttsVoice).Append("\",");
        sb.Append("\"format\":\"").Append(string.IsNullOrEmpty(phaseAudioFormat) ? "wav" : phaseAudioFormat).Append("\",");
        sb.Append("\"input\":\"").Append(Escape(text)).Append("\"}");
        string json = sb.ToString();

        var req = new UnityWebRequest(endpoint, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        req.timeout = Mathf.Max(5, requestTimeoutSeconds);

        D($"[TTS→] {endpoint} phase={gs}");
        yield return req.SendWebRequest();

        bool ok;
#if UNITY_2020_2_OR_NEWER
        ok = (req.result == UnityWebRequest.Result.Success);
#else
        ok = (!req.isNetworkError && !req.isHttpError);
#endif
        if (!ok || req.downloadHandler == null || req.downloadHandler.data == null || req.downloadHandler.data.Length == 0)
        {
            Warn($"[TTS←] 生成失败 phase={gs}, code={req.responseCode}, err={req.error}");
            yield break;
        }

        byte[] audio = req.downloadHandler.data;
        var outPath = GetPhaseWavPath(gs);
        try
        {
            File.WriteAllBytes(outPath, audio);
            _phaseAudioPathCache[gs] = outPath;
            D($"[TTS] ✓ 写入 {outPath}, bytes={audio.Length}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TTS] 写入失败: " + e.Message);
        }
    }

    // ====== Build messages (HTTP) ======
    private string BuildMessagesJson(string currentUserInput)
    {
        var sb = new StringBuilder(2048);
        sb.Append("[");
        bool first = true;
        string sysInstr = BuildFinalInstructions();
        EchoPrompt("HTTP/Messages.Instructions(final)", sysInstr, true);
        AppendMsg(sb, "system", sysInstr, ref first);
        int start = Mathf.Max(0, history.Count - 2 * Mathf.Max(1, maxHistoryTurnsToSend));
        for (int i = start; i < history.Count; i++) AppendMsg(sb, history[i].role, history[i].content, ref first);
        AppendMsg(sb, "user", currentUserInput, ref first);
        sb.Append("]");
        return sb.ToString();
    }

    private void AppendMsg(StringBuilder sb, string role, string content, ref bool first)
    {
        if (!first) sb.Append(",");
        first = false;
        sb.Append("{\"role\":\"").Append(role).Append("\",\"content\":\"")
          .Append(Escape(content)).Append("\"}");
    }

    private void TrimHistory()
    {
        int maxMsgs = 2 * Mathf.Max(1, maxHistoryTurnsToSend);
        if (history.Count > maxMsgs) history.RemoveRange(0, history.Count - maxMsgs);

        if (maxCharsBudget > 0)
        {
            int total = 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                total += history[i].content != null ? history[i].content.Length : 0;
                if (total > maxCharsBudget)
                {
                    if (i > 0) history.RemoveRange(0, i);
                    break;
                }
            }
        }
    }

    private string Escape(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    [Serializable]
    public class Message { public string role; public string content; }

    [Serializable]
    private class GPTResponse { public Choice[] choices; }

    [Serializable]
    private class Choice { public AssistantMessage message; }

    [Serializable]
    private class AssistantMessage
    {
        public string role;
        public string content;
        public AudioPayload audio;
    }

    [Serializable]
    private class AudioPayload
    {
        public string data;
        public string id;
        public string transcript;
        public long expires_at;
        public string format;
    }

    private void D(string msg)
    {
        if (!verboseDebug) return;
        if (Time.realtimeSinceStartup < _nextLog) return;
        _nextLog = Time.realtimeSinceStartup + Mathf.Max(0.05f, debugLogInterval);
        Debug.Log(msg);
    }

    private void DT(string tag, string msg)
    {
        if (!verboseDebug) return;
        if (Time.realtimeSinceStartup < _nextLog) return;
        _nextLog = Time.realtimeSinceStartup + Mathf.Max(0.05f, debugLogInterval);
        Debug.Log($"{tag} {msg}");
    }

    private void Warn(string msg)
    {
        if (outputText) outputText.text = msg;
        Debug.LogWarning(msg);
    }

    private string TrimForLog(string s)
    {
        if (string.IsNullOrEmpty(s)) return "(null)";
        return s.Length <= maxLogChars ? s : s.Substring(0, maxLogChars) + $" ...(+{s.Length - maxLogChars} chars)";
    }

    private string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "(empty)";
        if (key.Length <= 8) return "****";
        return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
    }

    private void SafeWriteFile(string name, string content)
    {
        if (!dumpResponsesToFile) return;
        try
        {
            string path = Path.Combine(Application.persistentDataPath, name);
            File.WriteAllText(path, content ?? "");
            D($"[File] {name} -> {path}, len={(content == null ? 0 : content.Length)}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[File] 写入失败: " + e.Message);
        }
    }

    private void SafeAppendFile(string name, string content)
    {
        if (!dumpResponsesToFile) return;
        try
        {
            string path = Path.Combine(Application.persistentDataPath, name);
            File.AppendAllText(path, content ?? "");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[File] 追加失败: " + e.Message);
        }
    }
}
