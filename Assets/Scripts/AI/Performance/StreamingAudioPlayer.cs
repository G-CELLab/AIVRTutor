using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace AI.Performance
{
    /// <summary>
    /// StreamingAudioPlayer provides non-blocking audio playback for VR applications.
    /// 
    /// Key features:
    /// - Ring buffer for streaming PCM data without blocking main thread
    /// - Background thread processes audio chunks
    /// - Lock-free queue for audio data
    /// - Minimizes GC by reusing AudioClip and buffers
    /// - Handles Quest Link cable latency gracefully
    /// 
    /// Architecture:
    /// 1. Caller pushes PCM chunks via AddPcmChunk() from any thread
    /// 2. Background processor converts chunks for Unity
    /// 3. Main thread Update() feeds data to AudioSource via OnAudioFilterRead
    /// </summary>
    public class StreamingAudioPlayer : MonoBehaviour
    {
        [Header("Audio Settings")]
        [Tooltip("Sample rate of incoming PCM16 audio")]
        public int sampleRate = 24000;
        
        [Tooltip("Number of audio channels")]
        public int channels = 1;
        
        [Tooltip("Buffer size in seconds - needs to be large for OpenAI burst audio")]
        public float bufferDurationSeconds = 10f;
        
        [Tooltip("Minimum buffered seconds before starting playback - higher prevents stuttering")]
        public float startThresholdSeconds = 4f;
        
        [Header("Playback")]
        public AudioSource audioSource;
        
        [Header("Debug")]
        public bool verboseDebug = false;
        
        // Ring buffer for float samples
        private float[] _ringBuffer;
        private volatile int _writePos;
        private volatile int _readPos;
        private volatile int _bufferedSamples;
        private readonly object _bufferLock = new object();
        
        // PCM16 chunk queue (from network/decoder)
        private struct AudioChunk
        {
            public byte[] Buffer;
            public int Length;
        }
        private readonly ConcurrentQueue<AudioChunk> _pcmQueue = new ConcurrentQueue<AudioChunk>();
        private volatile bool _isProcessing;
        private Thread _processorThread;
        private volatile bool _stopThread;
        
        // Playback state
        private volatile bool _hasStartedPlaying;
        private volatile bool _streamComplete;
        private AudioClip _streamClip;
        
        // GC-friendly buffer
        private byte[] _processingBuffer;
        
        // Events
        public event Action OnPlaybackStarted;
        public event Action OnPlaybackComplete;
        
        /// <summary>
        /// True if audio is currently playing or buffering
        /// </summary>
        public bool IsPlaying => _hasStartedPlaying && !_streamComplete;
        
        /// <summary>
        /// True if actively outputting audio (use this for lip sync)
        /// </summary>
        public bool IsSpeaking => _hasStartedPlaying && _bufferedSamples > 0;
        
        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            
            try
            {
                InitializeBuffers();
                Debug.Log($"[StreamingAudio] Initialized: buffer={bufferDurationSeconds}s ({_ringBuffer.Length} samples = {_ringBuffer.Length * 4 / 1024f:F1}KB)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamingAudio] Failed to initialize buffers: {e}");
                throw;
            }
        }
        
        private void InitializeBuffers()
        {
            int bufferSize = Mathf.CeilToInt(sampleRate * channels * bufferDurationSeconds);
            _ringBuffer = new float[bufferSize];
            _processingBuffer = new byte[65536]; // 64KB processing buffer
            _writePos = 0;
            _readPos = 0;
            _bufferedSamples = 0;
        }
        
        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                StartProcessorThread();
            }
        }
        
        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                StopProcessorThread();
                Stop();
            }
        }
        
        private void OnDestroy()
        {
            StopProcessorThread();
            
            if (_streamClip != null)
            {
                Destroy(_streamClip);
                _streamClip = null;
            }
        }
        
        private void StartProcessorThread()
        {
            if (_processorThread != null && _processorThread.IsAlive)
            {
                Debug.LogWarning("[StreamingAudio] Processor thread already running!");
                return;
            }
            
            Debug.Log("[StreamingAudio] Starting processor thread...");
            _stopThread = false;
            _processorThread = new Thread(ProcessorLoop)
            {
                Name = "StreamingAudioProcessor",
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.AboveNormal
            };
            _processorThread.Start();
            Debug.Log("[StreamingAudio] Processor thread started");
        }
        
        private void StopProcessorThread()
        {
            _stopThread = true;
            _processorThread?.Join(1000);
            _processorThread = null;
        }
        
        /// <summary>
        /// Background thread that processes incoming PCM chunks
        /// </summary>
        private void ProcessorLoop()
        {
            while (!_stopThread)
            {
                if (_pcmQueue.TryDequeue(out var chunk))
                {
                    _isProcessing = true;
                    ProcessPcmChunk(chunk.Buffer, chunk.Length);
                    
                    // Return chunk to pool
                    ByteArrayPool.Instance.Return(chunk.Buffer);
                }
                else
                {
                    _isProcessing = false;
                    Thread.Sleep(1); // Prevent busy-wait
                }
            }
        }
        
        private void ProcessPcmChunk(byte[] pcm16, int byteLength)
        {
            if (pcm16 == null || byteLength < 2) return;
            
            int sampleCount = byteLength / 2;
            
            lock (_bufferLock)
            {
                // Check if we have room in ring buffer
                int available = _ringBuffer.Length - _bufferedSamples;
                if (sampleCount > available)
                {
                    Debug.LogWarning($"[StreamingAudio] ⚠️ BUFFER OVERFLOW - dropping {sampleCount - available} samples ({(sampleCount - available) / (float)sampleRate:F2}s audio)! Buffer: {_bufferedSamples}/{_ringBuffer.Length} ({BufferedSeconds:F2}s/{bufferDurationSeconds:F1}s). Increase bufferDurationSeconds in Inspector!");
                    sampleCount = available;
                }
                
                // Convert PCM16 to float and write to ring buffer
                for (int i = 0; i < sampleCount; i++)
                {
                    int byteIndex = i * 2;
                    if (byteIndex + 1 >= byteLength) break;
                    
                    short sample = (short)(pcm16[byteIndex] | (pcm16[byteIndex + 1] << 8));
                    float normalized = sample / 32768f;
                    
                    _ringBuffer[_writePos] = normalized;
                    _writePos = (_writePos + 1) % _ringBuffer.Length;
                }
                
                _bufferedSamples += sampleCount;
            }
        }
        
        // Track buffer underrun for completion logic
        private float _bufferEmptyTime;
        private const float BUFFER_EMPTY_GRACE_PERIOD = 0.5f; // Wait 500ms after buffer empty before completing
        
        // Underrun detection
        private int _consecutiveUnderruns;
        private int _totalUnderruns;
        private int _audioCallbacksSinceLastWarning;
        
        private void Update()
        {
            // Check if we should start playback
            if (!_hasStartedPlaying)
            {
                int samplesNeeded = Mathf.CeilToInt(sampleRate * channels * startThresholdSeconds);
                
                // Only start if we have enough samples buffered OR being processed
                // (queue chunks are converted to samples by processor thread)
                if (_bufferedSamples >= samplesNeeded)
                {
                    StartPlayback();
                }
            }
            else if (_hasStartedPlaying && !_streamComplete)
            {
                // During active streaming, ensure AudioSource is playing if we have buffer
                if (_bufferedSamples > 0 && audioSource != null && !audioSource.isPlaying)
                {
                    if (verboseDebug)
                    {
                        Debug.Log($"[StreamingAudio] Resuming playback - buffer refilled to {_bufferedSamples} samples ({BufferedSeconds:F2}s)");
                    }
                    audioSource.Play();
                }
            }
            
            // Check if playback is complete - with grace period to handle network jitter
            if (_hasStartedPlaying && _streamComplete)
            {
                if (_bufferedSamples == 0 && !_isProcessing && _pcmQueue.IsEmpty)
                {
                    // Buffer is empty - start or continue grace period timer
                    if (_bufferEmptyTime == 0f)
                    {
                        _bufferEmptyTime = Time.unscaledTime;
                        if (verboseDebug)
                        {
                            Debug.Log($"[StreamingAudio] Buffer empty - starting {BUFFER_EMPTY_GRACE_PERIOD}s grace period");
                        }
                    }
                    else if (Time.unscaledTime - _bufferEmptyTime >= BUFFER_EMPTY_GRACE_PERIOD)
                    {
                        // Grace period expired, actually complete
                        if (verboseDebug)
                        {
                            Debug.Log($"[StreamingAudio] Grace period expired - completing playback");
                        }
                        CompletePlayback();
                    }
                }
                else
                {
                    // Buffer has data again, reset grace period
                    if (_bufferEmptyTime != 0f && verboseDebug)
                    {
                        Debug.Log($"[StreamingAudio] Buffer refilled - resetting grace period (samples: {_bufferedSamples}, queue: {_pcmQueue.Count})");
                    }
                    _bufferEmptyTime = 0f;
                }
            }
        }
        
        private void StartPlayback()
        {
            if (_hasStartedPlaying) return;
            
            // Create a streaming AudioClip that reads from our buffer
            // Use a reasonable buffer size for VR (20ms frames)
            int clipSamples = Mathf.CeilToInt(sampleRate * 0.1f); // 100ms clip
            
            if (_streamClip == null || _streamClip.samples != clipSamples)
            {
                if (_streamClip != null) Destroy(_streamClip);
                _streamClip = AudioClip.Create("StreamClip", clipSamples, channels, sampleRate, true, OnAudioRead);
            }
            
            audioSource.clip = _streamClip;
            audioSource.loop = true;
            audioSource.Play();
            
            _hasStartedPlaying = true;
            
            OnPlaybackStarted?.Invoke();
            
            Debug.Log($"[StreamingAudio] ▶️ Started playback - buffered: {_bufferedSamples} samples ({BufferedSeconds:F2}s), queue: {_pcmQueue.Count} chunks");
        }
        
        /// <summary>
        /// Called by Unity to fill the AudioClip buffer
        /// </summary>
        private void OnAudioRead(float[] data)
        {
            lock (_bufferLock)
            {
                int samplesToRead = Mathf.Min(data.Length, _bufferedSamples);
                
                // Detect underrun (buffer can't keep up with playback)
                if (_hasStartedPlaying && samplesToRead < data.Length)
                {
                    int missingSamples = data.Length - samplesToRead;
                    _consecutiveUnderruns++;
                    _totalUnderruns++;
                    
                    // Warn about underruns but throttle to avoid spam (every 100 callbacks ~= 2 seconds at 48 samples/callback)
                    _audioCallbacksSinceLastWarning++;
                    if (_audioCallbacksSinceLastWarning > 100)
                    {
                        float missingSeconds = missingSamples / (float)sampleRate;
                        Debug.LogWarning($"[StreamingAudio] ⚠️ BUFFER UNDERRUN #{_totalUnderruns} - need {data.Length} samples but only have {samplesToRead}! Missing {missingSeconds:F3}s audio. Buffer: {_bufferedSamples}/{_ringBuffer.Length} ({BufferedSeconds:F2}s). Queue: {_pcmQueue.Count} chunks");
                        _audioCallbacksSinceLastWarning = 0;
                    }
                }
                else if (_consecutiveUnderruns > 0)
                {
                    _consecutiveUnderruns = 0;
                    _audioCallbacksSinceLastWarning = 0;
                }
                
                for (int i = 0; i < samplesToRead; i++)
                {
                    data[i] = _ringBuffer[_readPos];
                    _readPos = (_readPos + 1) % _ringBuffer.Length;
                }
                
                // Fill remaining with silence (this causes the "pause")
                for (int i = samplesToRead; i < data.Length; i++)
                {
                    data[i] = 0f;
                }
                
                _bufferedSamples -= samplesToRead;
            }
        }
        
        private void CompletePlayback()
        {
            if (!_hasStartedPlaying) return;
            
            audioSource.Stop();
            _hasStartedPlaying = false;
            _streamComplete = false;
            _bufferEmptyTime = 0f; // Reset grace period timer
            
            OnPlaybackComplete?.Invoke();
            
            if (verboseDebug)
            {
                Debug.Log("[StreamingAudio] Playback complete");
            }
        }
        
        /// <summary>
        /// Add a PCM16 audio chunk to the playback queue.
        /// Can be called from any thread.
        /// </summary>
        /// <param name="pcm16">PCM16 little-endian audio data</param>
        public void AddPcmChunk(byte[] pcm16)
        {
            if (pcm16 == null || pcm16.Length == 0) return;
            
            int actualLength = pcm16.Length;
            
            // Copy to pooled buffer to avoid holding references
            var pooledCopy = ByteArrayPool.Instance.Rent(actualLength);
            Buffer.BlockCopy(pcm16, 0, pooledCopy, 0, actualLength);
            
            _pcmQueue.Enqueue(new AudioChunk { Buffer = pooledCopy, Length = actualLength });
            
            if (verboseDebug)
            {
                Debug.Log($"[StreamingAudio] Added chunk: {actualLength} bytes, queue size={_pcmQueue.Count}");
            }
        }
        
        /// <summary>
        /// Add a PCM16 chunk with specified length (for pooled buffers)
        /// </summary>
        public void AddPcmChunk(byte[] pcm16, int offset, int length)
        {
            if (pcm16 == null || length <= 0) return;
            
            var chunk = ByteArrayPool.Instance.Rent(length);
            Buffer.BlockCopy(pcm16, offset, chunk, 0, length);
            _pcmQueue.Enqueue(new AudioChunk { Buffer = chunk, Length = length });
        }
        
        /// <summary>
        /// Signal that no more audio chunks will be added
        /// </summary>
        public void MarkStreamComplete()
        {
            _streamComplete = true;            Debug.Log($"[StreamingAudio] Stream marked complete - buffered: {_bufferedSamples} samples ({BufferedSeconds:F2}s), queue: {_pcmQueue.Count} chunks");            
            if (verboseDebug)
            {
                Debug.Log($"[StreamingAudio] MarkStreamComplete called - buffered samples: {_bufferedSamples}, queue size: {_pcmQueue.Count}, isProcessing: {_isProcessing}");
            }
        }
        
        /// <summary>
        /// Stop playback and clear buffers
        /// </summary>
        public void Stop()
        {
            audioSource?.Stop();
            
            // Clear queued chunks
            while (_pcmQueue.TryDequeue(out var chunk))
            {
                ByteArrayPool.Instance.Return(chunk.Buffer);
            }
            
            lock (_bufferLock)
            {
                _writePos = 0;
                _readPos = 0;
                _bufferedSamples = 0;
            }
            
            _hasStartedPlaying = false;
            _streamComplete = false;
            _bufferEmptyTime = 0f; // Reset grace period timer
            _consecutiveUnderruns = 0;
            _totalUnderruns = 0;
        }
        
        /// <summary>
        /// Prepare for a new stream
        /// </summary>
        public void BeginStream(int newSampleRate = 0, int newChannels = 0)
        {
            Stop();
            
            if (newSampleRate > 0 && newSampleRate != sampleRate)
            {
                sampleRate = newSampleRate;
                InitializeBuffers();
            }
            
            if (newChannels > 0)
            {
                channels = newChannels;
            }
            
            _streamComplete = false;
        }
        
        /// <summary>
        /// Current buffer fill level (0-1)
        /// </summary>
        public float BufferLevel => _ringBuffer.Length > 0 ? (float)_bufferedSamples / _ringBuffer.Length : 0f;
        
        /// <summary>
        /// Buffered audio duration in seconds
        /// </summary>
        public float BufferedSeconds => sampleRate > 0 ? (float)_bufferedSamples / (sampleRate * channels) : 0f;
    }
}
