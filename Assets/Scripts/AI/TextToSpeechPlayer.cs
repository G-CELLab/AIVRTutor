using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class TextToSpeechPlayer : MonoBehaviour
{
    private void OnEnable()
    {
        StartCoroutine(GlobalAudioSourceMonitor());
    }

    private IEnumerator GlobalAudioSourceMonitor()
    {
        while (true)
        {
            var all = GameObject.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var s in all)
            {
                if (s == null) continue;
                if (!s.isPlaying && s.clip == null && IsSpeaking)
                {
                    Debug.LogWarning($"[TTS][GlobalMonitor] AudioSource {s.name} on {s.gameObject.name} stopped and clip is null while IsSpeaking=true. This may indicate an external interruption. StackTrace: {System.Environment.StackTrace}");
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
    [Header("OpenAI")]
    
    public string openAIKey = "*****";
    public string voice = "nova";

    [Header("Audio")]
    public AudioSource audioSource;
    [Tooltip("当被打断时，除了本组件外，是否尝试停止项目中所有注册的 TTS 播放器")]
    public bool killAllTTSOnInterrupt = true;

    private static readonly HashSet<TextToSpeechPlayer> INSTANCES = new HashSet<TextToSpeechPlayer>();
    public bool IsSpeaking { get; private set; } = false;
    
    // Track current speech for logging
    private static string currentSpeechText = "";
    private static string lastSpeechText = ""; // Keep last speech for logging persistence
    public static string GetCurrentSpeech() => !string.IsNullOrEmpty(currentSpeechText) ? currentSpeechText : lastSpeechText;
    public static void SetCurrentSpeech(string text)
    {
        currentSpeechText = text;
        lastSpeechText = text;
    }

    public static void ClearCurrentSpeech()
    {
        currentSpeechText = "";
        lastSpeechText = "";
    }

    void Awake()
    {
        if (!INSTANCES.Contains(this)) INSTANCES.Add(this);
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    void OnDestroy() { INSTANCES.Remove(this); }

    public static void KillAllTTS()
    {
        foreach (var t in INSTANCES) { if (t != null) t.StopSpeaking(); }
    }

    public void StopSpeaking()
    {
        Debug.LogWarning($"[TTS] StopSpeaking() called! Stopping speech. StackTrace: {System.Environment.StackTrace}");
        IsSpeaking = false;
        // Don't clear currentSpeechText here - keep it for logging purposes
        // It will be replaced when new speech starts
        if (audioSource != null)
        {
            try
            {
                if (audioSource.isPlaying)
                {
                    Debug.LogWarning($"[TTS] audioSource.Stop() called on {audioSource.name} (GameObject: {audioSource.gameObject.name})");
                    audioSource.Stop();
                }
                if (audioSource.clip != null)
                {
                    audioSource.time = 0f;
                    Debug.LogWarning($"[TTS] audioSource.clip set to null on {audioSource.name} (GameObject: {audioSource.gameObject.name})");
                    audioSource.clip = null;
                }
                audioSource.mute = false;
            }
            catch (System.Exception ex) { Debug.LogError($"[TTS] Exception in StopSpeaking: {ex}"); }
        }
    }

    public void BindRecognizer(OpenAISpeechRecognizer rec)
    {
        if (rec == null) return;
        rec.OnUserSpeechLikely -= HandleUserSpeechLikely;
        rec.OnUserSpeechLikely += HandleUserSpeechLikely;
    }

    private void HandleUserSpeechLikely()
    {
        if (!IsSpeaking) return;
        if (killAllTTSOnInterrupt) KillAllTTS();
        else StopSpeaking();
    }

    // ====== 新增：播放模型直接返回的音频（base64） ======
    public void PlayModelAudioBase64(string base64Data, string format, Action onPlaybackComplete)
    {
        StartCoroutine(PlayModelAudioBase64_Co(base64Data, format, onPlaybackComplete));
    }

    // Overload that accepts speech text for logging
    public void PlayModelAudioBase64(string base64Data, string format, string speechText, Action onPlaybackComplete)
    {
        currentSpeechText = speechText;
        lastSpeechText = speechText; // Store for persistent logging
        StartCoroutine(PlayModelAudioBase64_Co(base64Data, format, onPlaybackComplete));
    }

    private IEnumerator PlayModelAudioBase64_Co(string base64Data, string format, Action onPlaybackComplete)
    {
        if (string.IsNullOrEmpty(base64Data))
        {
            onPlaybackComplete?.Invoke();
            yield break;
        }

        string fmt = string.IsNullOrEmpty(format) ? "wav" : format.ToLowerInvariant();
        string ext = (fmt == "wav") ? "wav" : (fmt == "mp3" ? "mp3" : "mp3"); // 默认mp3兜底
        string filePath = Path.Combine(Application.persistentDataPath, "gpt_audio_reply." + ext);

        try
        {
            byte[] bytes = Convert.FromBase64String(base64Data);
            File.WriteAllBytes(filePath, bytes);
        }
        catch (Exception e)
        {
            Debug.LogError("写入模型音频失败: " + e.Message);
            onPlaybackComplete?.Invoke();
            yield break;
        }

        AudioType at = AudioType.MPEG;
        if (ext == "wav") at = AudioType.WAV;

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, at))
        {
            yield return www.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool ok = (www.result == UnityWebRequest.Result.Success);
#else
            bool ok = (!www.isNetworkError && !www.isHttpError);
#endif
            if (!ok)
            {
                Debug.LogError("加载模型音频失败: " + www.error + " (HTTP " + www.responseCode + ")");
                onPlaybackComplete?.Invoke();
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            if (audioSource == null) audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.clip = clip;

            IsSpeaking = true;
            audioSource.Play();

            // Robust playback wait: handle brief hiccups (Quest Link lag, GC, etc.)
            float expectedDuration = clip != null ? clip.length : 0f;
            float playbackStart = Time.realtimeSinceStartup;
            float lastPlayingTime = playbackStart;
            float gracePeriod = 0.5f; // Allow 500ms of "not playing" before considering finished
            
            while (true)
            {
                if (audioSource == null) break;
                
                bool isCurrentlyPlaying = audioSource.isPlaying;
                float elapsed = Time.realtimeSinceStartup - playbackStart;
                
                if (isCurrentlyPlaying)
                {
                    lastPlayingTime = Time.realtimeSinceStartup;
                }
                else
                {
                    float timeSinceLastPlaying = Time.realtimeSinceStartup - lastPlayingTime;
                    // Only exit if: not playing for grace period AND (elapsed > expected duration OR grace period exceeded)
                    if (timeSinceLastPlaying >= gracePeriod && elapsed >= expectedDuration * 0.8f)
                    {
                        break;
                    }
                }
                yield return null;
            }
        }

        IsSpeaking = false;
        onPlaybackComplete?.Invoke();
    }

    // ====== 旧功能：文本 → 本地TTS（保留以便回退） ======
    public void Speak(string text, Action onPlaybackComplete)
    {
        currentSpeechText = text;
        lastSpeechText = text; // Store for persistent logging
        Debug.Log($"[TTS] Speech text set for logging: '{text.Substring(0, Mathf.Min(50, text.Length))}'...");
        StartCoroutine(SendTextToSpeech(text, onPlaybackComplete));
    }

    IEnumerator SendTextToSpeech(string text, Action onPlaybackComplete)
    {
        string url = "https://api.openai.com/v1/audio/speech";

        string json = JsonUtility.ToJson(new SpeechRequest()
        {
            model = "openai/gpt-oss-120b",
            input = text,
            voice = voice,
            response_format = "mp3"
        });

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + openAIKey);
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        bool ok;
#if UNITY_2020_2_OR_NEWER
        ok = (req.result == UnityWebRequest.Result.Success);
#else
        ok = (!req.isNetworkError && !req.isHttpError);
#endif
        if (!ok)
        {
            Debug.LogError("TTS 请求失败: " + req.error + " (HTTP " + req.responseCode + ")");
            IsSpeaking = false;
            onPlaybackComplete?.Invoke();
            yield break;
        }

        string path = Path.Combine(Application.persistentDataPath, "tts_reply.mp3");
        File.WriteAllBytes(path, req.downloadHandler.data);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            bool ok2 = (www.result == UnityWebRequest.Result.Success);
#else
            bool ok2 = (!www.isNetworkError && !www.isHttpError);
#endif
            if (ok2)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (audioSource == null) audioSource = gameObject.GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

                audioSource.loop = false;
                audioSource.playOnAwake = false;
                audioSource.clip = clip;

                IsSpeaking = true;
                audioSource.Play();

                // Robust playback wait: handle brief hiccups (Quest Link lag, GC, etc.)
                float expectedDuration = clip != null ? clip.length : 0f;
                float playbackStart = Time.realtimeSinceStartup;
                float lastPlayingTime = playbackStart;
                float gracePeriod = 0.5f; // Allow 500ms of "not playing" before considering finished
                
                while (true)
                {
                    if (audioSource == null) break;
                    
                    bool isCurrentlyPlaying = audioSource.isPlaying;
                    float elapsed = Time.realtimeSinceStartup - playbackStart;
                    
                    if (isCurrentlyPlaying)
                    {
                        lastPlayingTime = Time.realtimeSinceStartup;
                    }
                    else
                    {
                        float timeSinceLastPlaying = Time.realtimeSinceStartup - lastPlayingTime;
                        // Only exit if: not playing for grace period AND (elapsed > expected duration OR grace period exceeded)
                        if (timeSinceLastPlaying >= gracePeriod && elapsed >= expectedDuration * 0.8f)
                        {
                            break;
                        }
                    }
                    yield return null;
                }
            }
            else
            {
                Debug.LogError("加载 MP3 失败: " + www.error + " (HTTP " + www.responseCode + ")");
            }
        }

        IsSpeaking = false;
        onPlaybackComplete?.Invoke();
    }

    [Serializable]
    public class SpeechRequest
    {
        public string model;
        public string input;
        public string voice;
        public string response_format;
    }
}
