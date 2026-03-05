using UnityEngine;
using System.Collections;

/// <summary>
/// Automatically integrates WhisperGestureSync with TextToSpeechPlayer.
/// Transcribes TTS audio and schedules gestures in real-time.
/// </summary>
[RequireComponent(typeof(WhisperGestureSync))]
public class TTSWhisperIntegration : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("TextToSpeechPlayer to monitor (auto-finds if not set)")]
    public TextToSpeechPlayer ttsPlayer;
    
    [Tooltip("WhisperGestureSync component")]
    public WhisperGestureSync gestureSync;
    
    [Header("Integration Settings")]
    [Tooltip("Automatically transcribe when TTS starts playing")]
    public bool autoTranscribe = true;
    
    [Tooltip("Record audio from AudioSource for transcription")]
    public bool recordFromAudioSource = true;
    
    [Tooltip("Fallback: Use pre-generated text if audio recording fails")]
    public bool useFallbackText = true;
    
    [Header("Audio Recording")]
    [Tooltip("Sample rate for audio recording (must match Whisper model)")]
    public int sampleRate = 16000;
    
    [Tooltip("Audio channels (1=mono, 2=stereo)")]
    public int channels = 1;
    
    [Header("Debug")]
    public bool verboseLogging = true;

    private Coroutine recordingCoroutine;
    
    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Whisper disabled on Android - too slow for real-time gestures
        // Disabling TTSWhisperIntegration to prevent timeout errors
        Debug.LogWarning("[TTSWhisper] Disabled on Android - using GPTConnector text-based gestures instead");
        enabled = false;
        return;
#endif

        if (!gestureSync) gestureSync = GetComponent<WhisperGestureSync>();
        if (!ttsPlayer) ttsPlayer = FindAnyObjectByType<TextToSpeechPlayer>();
        
        if (!gestureSync)
        {
            Debug.LogError("[TTSWhisper] WhisperGestureSync component not found!");
            enabled = false;
            return;
        }
    }
    
    void Start()
    {
        Debug.Log("[TTSWhisper] 🚀 Starting TTSWhisperIntegration...");
        
        // Hook into TTS player events if available
        if (ttsPlayer && autoTranscribe)
        {
            Debug.Log("[TTSWhisper] ✓ TTS Player found, monitoring playback");
            StartCoroutine(MonitorTTSPlayback());
        }
        else
        {
            Debug.LogWarning("[TTSWhisper] ⚠️ TTS Player not found, auto-transcribe disabled");
        }
    }
    
    IEnumerator MonitorTTSPlayback()
    {
        AudioSource audioSource = ttsPlayer.audioSource;
        if (!audioSource)
        {
            Debug.LogError("[TTSWhisper] ❌ TTS AudioSource not found!");
            yield break;
        }
        
        Debug.Log("[TTSWhisper] ✓ AudioSource found, starting playback monitor");
        bool wasPlaying = false;
        
        while (true)
        {
            bool isPlaying = audioSource.isPlaying;
            
            // Detect TTS start
            if (isPlaying && !wasPlaying)
            {
                Debug.Log("[TTSWhisper] 🎬 TTS PLAYBACK STARTED");
                OnTTSStarted(audioSource);
            }
            // Detect TTS stop
            else if (!isPlaying && wasPlaying)
            {
                Debug.Log("[TTSWhisper] ⏹️ TTS PLAYBACK STOPPED");
                OnTTSStopped();
            }
            
            wasPlaying = isPlaying;
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    void OnTTSStarted(AudioSource audioSource)
    {
        Debug.Log("[TTSWhisper] 🎙️ ==> OnTTSStarted called");
        
        // Get the audio clip
        AudioClip clip = audioSource.clip;
        
        if (clip == null)
        {
            Debug.LogError("[TTSWhisper] ❌ AudioSource has NO clip assigned!");
            return;
        }
        
        Debug.Log($"[TTSWhisper] ✓ Clip: {clip.name} ({clip.length:F2}s)");
        
        if (recordFromAudioSource)
        {
            Debug.Log("[TTSWhisper] 🔄 Recording from audio source for transcription...");
            // Record from audio source and transcribe
            if (recordingCoroutine != null) StopCoroutine(recordingCoroutine);
            recordingCoroutine = StartCoroutine(RecordAndTranscribe(audioSource, clip));
        }
        else if (useFallbackText && ttsPlayer != null)
        {
            Debug.LogWarning("[TTSWhisper] ⚠️ Audio recording disabled, using fallback text analysis");
            // Note: You may need to add a method to get the current speech text from TTS player
        }
    }
    
    void OnTTSStopped()
    {
        Debug.Log("[TTSWhisper] Cleanup on TTS stop");
        
        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }
    }
    
    IEnumerator RecordAndTranscribe(AudioSource audioSource, AudioClip originalClip)
    {
        Debug.Log("[TTSWhisper] ⏳ Waiting for audio playback to stabilize...");
        
        // Wait a tiny bit for audio to start playing
        yield return new WaitForSeconds(0.05f);
        
        // For pre-recorded TTS clips, we can directly transcribe the clip
        if (originalClip != null && !originalClip.name.Contains("Microphone"))
        {
            Debug.Log($"[TTSWhisper] 📝 Transcribing TTS clip: {originalClip.name} ({originalClip.length:F2}s, {originalClip.frequency}Hz, {originalClip.channels}ch)");
            
            // Convert to Whisper-compatible format if needed
            AudioClip processedClip = ConvertAudioClip(originalClip);
            Debug.Log($"[TTSWhisper] ✓ Clip prepared, sending to WhisperGestureSync...");
            gestureSync.TranscribeAndScheduleGestures(processedClip);
        }
        else
        {
            Debug.LogError("[TTSWhisper] ❌ Real-time recording from AudioSource not yet supported. Using direct clip transcription.");
        }
    }
    
    AudioClip ConvertAudioClip(AudioClip original)
    {
        // If clip already matches required format, return as-is
        if (original.frequency == sampleRate && original.channels == channels)
        {
            Debug.Log($"[TTSWhisper] ✓ Clip already in correct format ({sampleRate}Hz/{channels}ch), no conversion needed");
            return original;
        }
        
        // Otherwise, resample (basic implementation)
        Debug.Log($"[TTSWhisper] 🔄 Converting audio: {original.frequency}Hz/{original.channels}ch -> {sampleRate}Hz/{channels}ch");
        
        float[] originalData = new float[original.samples * original.channels];
        original.GetData(originalData, 0);
        
        // Simple resampling (for production, use a proper resampling algorithm)
        int newSamples = Mathf.RoundToInt((float)original.samples * sampleRate / original.frequency);
        float[] resampledData = ResampleAudio(originalData, original.samples, newSamples, original.channels, channels);
        
        AudioClip converted = AudioClip.Create(
            original.name + "_converted",
            newSamples,
            channels,
            sampleRate,
            false
        );
        converted.SetData(resampledData, 0);
        
        return converted;
    }
    
    float[] ResampleAudio(float[] input, int inputSamples, int outputSamples, int inputChannels, int outputChannels)
    {
        float[] output = new float[outputSamples * outputChannels];
        float ratio = (float)inputSamples / outputSamples;
        
        for (int i = 0; i < outputSamples; i++)
        {
            int srcIndex = Mathf.FloorToInt(i * ratio);
            srcIndex = Mathf.Clamp(srcIndex, 0, inputSamples - 1);
            
            // Handle channel conversion
            if (inputChannels == outputChannels)
            {
                // Same channel count - direct copy
                for (int ch = 0; ch < outputChannels; ch++)
                {
                    output[i * outputChannels + ch] = input[srcIndex * inputChannels + ch];
                }
            }
            else if (inputChannels == 2 && outputChannels == 1)
            {
                // Stereo to mono - average both channels
                output[i] = (input[srcIndex * 2] + input[srcIndex * 2 + 1]) * 0.5f;
            }
            else if (inputChannels == 1 && outputChannels == 2)
            {
                // Mono to stereo - duplicate channel
                output[i * 2] = input[srcIndex];
                output[i * 2 + 1] = input[srcIndex];
            }
        }
        
        return output;
    }
    
    /// <summary>
    /// Manually trigger transcription for a specific audio clip
    /// </summary>
    public void TranscribeClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[TTSWhisper] Cannot transcribe null clip");
            return;
        }
        
        AudioClip processedClip = ConvertAudioClip(clip);
        gestureSync.TranscribeAndScheduleGestures(processedClip);
    }
    
    void OnDisable()
    {
        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }
    }
}
