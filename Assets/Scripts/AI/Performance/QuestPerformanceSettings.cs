using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.XR;
using Debug = UnityEngine.Debug;

namespace AI.Performance
{
    /// <summary>
    /// Quest-specific performance settings to prevent the hourglass/loading indicator
    /// and minimize frame drops during AI interactions.
    /// 
    /// The Meta Quest shows the spinning hourglass when:
    /// 1. Frame time exceeds ~13.9ms (Quest 2/3 at 72Hz) or ~11.1ms at 90Hz
    /// 2. The app misses multiple consecutive frames
    /// 3. The XR compositor doesn't receive frames in time
    /// 
    /// This component addresses these issues through:
    /// - Aggressive GC management
    /// - Quality settings optimization
    /// - Proactive frame timing monitoring
    /// - Render pipeline adjustments
    /// </summary>
    [DefaultExecutionOrder(-1000)] // Run before other scripts
    public class QuestPerformanceSettings : MonoBehaviour
    {
        // Static timing helper for debugging
        private static Stopwatch _debugStopwatch;
        private static string _debugLabel;
        
        /// <summary>
        /// Start timing an operation for debug purposes
        /// </summary>
        public static void StartTiming(string label)
        {
            _debugLabel = label;
            _debugStopwatch = Stopwatch.StartNew();
        }
        
        /// <summary>
        /// End timing and log if it took longer than threshold
        /// </summary>
        public static void EndTiming(float thresholdMs = 10f)
        {
            if (_debugStopwatch == null) return;
            _debugStopwatch.Stop();
            float ms = _debugStopwatch.ElapsedMilliseconds;
            if (ms >= thresholdMs)
            {
                Debug.LogWarning($"[Timing] ⏱️ {_debugLabel}: {ms:F0}ms");
            }
            _debugStopwatch = null;
        }
        
        [Header("Quest Settings")]
        [Tooltip("Target frame rate (72, 90, or 120 for Quest 3)")]
        public int targetFrameRate = 72;
        
        [Tooltip("Enable aggressive GC management to prevent GC spikes")]
        public bool aggressiveGCManagement = true;
        
        [Tooltip("Interval in seconds to run incremental GC")]
        public float gcInterval = 0.5f;
        
        [Tooltip("Enable frame timing warnings in logs")]
        public bool logFrameTimingWarnings = true;
        
        [Tooltip("Frame time threshold in ms to trigger warning (25ms = noticeable stutter)")]
        public float frameTimeWarningThreshold = 25f;
        
        [Header("Quality Optimizations")]
        [Tooltip("Override quality settings for Quest")]
        public bool overrideQualitySettings = true;
        
        [Tooltip("Pixel light count (lower = better performance)")]
        public int pixelLightCount = 1;
        
        [Tooltip("Texture quality (0=Full, 1=Half, 2=Quarter)")]
        public int textureQuality = 0;
        
        [Tooltip("Shadow distance (0 to disable shadows)")]
        public float shadowDistance = 0f;
        
        [Tooltip("Soft particles (disable for better performance)")]
        public bool softParticles = false;
        
        [Tooltip("Anisotropic filtering (disable for better performance)")]
        public AnisotropicFiltering anisotropicFiltering = AnisotropicFiltering.Disable;
        
        [Header("Audio Optimizations")]
        [Tooltip("DSP buffer size (higher = more latency but less CPU)")]
        public AudioDSPBufferSize dspBufferSize = AudioDSPBufferSize.Best;
        
        [Header("XR Optimizations")]
        [Tooltip("Use single-pass instanced rendering")]
        public bool useSinglePassInstanced = true;
        
        [Tooltip("Foveated rendering level (0-4, higher = more aggressive)")]
        [Range(0, 4)]
        public int foveatedRenderingLevel = 2;
        
        [Header("Debug")]
        public bool verboseDebug = false;
        
        // Frame timing
        private float _lastFrameTime;
        private float _lastGCTime;
        private int _consecutiveSlowFrames;
        private float _lastSlowFrameWarningTime;
        private const int MAX_SLOW_FRAMES_BEFORE_EMERGENCY = 3;
        private const float SLOW_FRAME_WARNING_COOLDOWN = 2f; // Only warn every 2 seconds max
        
        // Stats
        private int _gcCallsThisSecond;
        private float _lastStatsReset;
        
        public enum AudioDSPBufferSize
        {
            Default = 0,
            Best = 256,
            Good = 512,
            Latency = 1024
        }
        
        private void Awake()
        {
            // Set frame rate
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0; // VSync is handled by XR
            
            // TEMPORARILY DISABLED for debugging - these may cause issues
            // ConfigureXRSettings();
            
            // Configure quality
            if (overrideQualitySettings)
            {
                ConfigureQualitySettings();
            }
            
            // TEMPORARILY DISABLED - AudioSettings.Reset can cause issues
            // ConfigureAudioSettings();
            
            // TEMPORARILY DISABLED - GC settings may cause issues
            // ConfigureGCSettings();
            
            if (verboseDebug)
            {
                Debug.Log($"[QuestPerf] Initialized: targetFPS={targetFrameRate}, GC interval={gcInterval}s (some features disabled for debug)");
            }
        }
        
        private void ConfigureXRSettings()
        {
            // Try to set Quest-specific XR settings
            try
            {
                // Note: Modern Unity XR uses XRInputSubsystem for tracking origin
                // The tracking space is typically set via the XR Plug-in Management settings
                
                // Enable single-pass instanced if available
                if (useSinglePassInstanced)
                {
                    // This is typically set in project settings, but we can try to ensure it
                    // XRSettings.stereoRenderingMode = XRSettings.StereoRenderingMode.SinglePassInstanced;
                }
                
                if (verboseDebug)
                {
                    Debug.Log($"[QuestPerf] XR configured: device={XRSettings.loadedDeviceName}");
                }
            }
            catch (Exception e)
            {
                if (verboseDebug)
                {
                    Debug.LogWarning($"[QuestPerf] XR configuration warning: {e.Message}");
                }
            }
            
            // Try to set foveated rendering via OVRManager if available
            TrySetFoveatedRendering();
        }
        
        private void TrySetFoveatedRendering()
        {
            // Use reflection to access OVRManager if available (Meta XR SDK)
            try
            {
                var ovrManagerType = Type.GetType("OVRManager, Oculus.VR");
                if (ovrManagerType != null)
                {
#if UNITY_2023_1_OR_NEWER
                    var instance = FindAnyObjectByType(ovrManagerType);
#else
                    var instance = FindObjectOfType(ovrManagerType);
#endif
                    if (instance != null)
                    {
                        // Set fixed foveated rendering level
                        var ffrProp = ovrManagerType.GetProperty("fixedFoveatedRenderingLevel");
                        if (ffrProp != null)
                        {
                            // OVRManager.FixedFoveatedRenderingLevel enum: Off=0, Low=1, Medium=2, High=3, HighTop=4
                            var enumType = ffrProp.PropertyType;
                            var enumValue = Enum.ToObject(enumType, foveatedRenderingLevel);
                            ffrProp.SetValue(instance, enumValue);
                            
                            if (verboseDebug)
                            {
                                Debug.Log($"[QuestPerf] Foveated rendering set to level {foveatedRenderingLevel}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (verboseDebug)
                {
                    Debug.LogWarning($"[QuestPerf] Could not set foveated rendering: {e.Message}");
                }
            }
        }
        
        private void ConfigureQualitySettings()
        {
            QualitySettings.pixelLightCount = pixelLightCount;
#if UNITY_2022_1_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = textureQuality;
#else
            QualitySettings.masterTextureLimit = textureQuality;
#endif
            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.softParticles = softParticles;
            QualitySettings.anisotropicFiltering = anisotropicFiltering;
            
            // Disable real-time shadows for performance
            if (shadowDistance <= 0f)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
            }
            
            // Reduce LOD bias for better performance
            QualitySettings.lodBias = 1.0f;
            
            // Reduce particle raycast budget
            QualitySettings.particleRaycastBudget = 64;
            
            if (verboseDebug)
            {
                Debug.Log($"[QuestPerf] Quality configured: lights={pixelLightCount}, shadows={shadowDistance}");
            }
        }
        
        private void ConfigureAudioSettings()
        {
            // Get current audio configuration
            var config = AudioSettings.GetConfiguration();
            
            // Set DSP buffer size for lower latency
            if (dspBufferSize != AudioDSPBufferSize.Default)
            {
                config.dspBufferSize = (int)dspBufferSize;
                
                // Note: AudioSettings.Reset() must be called for changes to take effect
                // but it can cause audio interruption, so we only do it at startup
                try
                {
                    AudioSettings.Reset(config);
                    if (verboseDebug)
                    {
                        Debug.Log($"[QuestPerf] Audio DSP buffer set to {config.dspBufferSize}");
                    }
                }
                catch (Exception e)
                {
                    if (verboseDebug)
                    {
                        Debug.LogWarning($"[QuestPerf] Could not reset audio settings: {e.Message}");
                    }
                }
            }
        }
        
        private void ConfigureGCSettings()
        {
            if (aggressiveGCManagement)
            {
                // Enable incremental GC if available
#if UNITY_2019_1_OR_NEWER
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
#endif
                
                // Set GC latency mode to low latency
#if !UNITY_WEBGL
                try
                {
                    System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
                    if (verboseDebug)
                    {
                        Debug.Log("[QuestPerf] GC set to SustainedLowLatency mode");
                    }
                }
                catch (Exception e)
                {
                    if (verboseDebug)
                    {
                        Debug.LogWarning($"[QuestPerf] Could not set GC latency mode: {e.Message}");
                    }
                }
#endif
            }
        }
        
        private void Update()
        {
            float frameTime = Time.unscaledDeltaTime * 1000f; // in ms
            float targetFrameTime = 1000f / targetFrameRate;
            
            // Track slow frames
            if (frameTime > frameTimeWarningThreshold)
            {
                _consecutiveSlowFrames++;
                
                // Rate-limited warning (every 2 seconds max to prevent log spam)
                if (logFrameTimingWarnings && _consecutiveSlowFrames == 1 && 
                    Time.unscaledTime - _lastSlowFrameWarningTime >= SLOW_FRAME_WARNING_COOLDOWN)
                {
                    _lastSlowFrameWarningTime = Time.unscaledTime;
                    // Show detailed frame info for major slow frames
                    if (frameTime > 500f)
                    {
                        Debug.LogWarning($"[QuestPerf] 🔴 MAJOR FREEZE: {frameTime:F1}ms - Frame #{Time.frameCount} at {Time.unscaledTime:F1}s");
                    }
                    else
                    {
                        Debug.LogWarning($"[QuestPerf] ⚠️ Slow frame: {frameTime:F1}ms (target: {targetFrameTime:F1}ms)");
                    }
                }
                
                // Emergency GC if we're about to trigger hourglass
                if (_consecutiveSlowFrames >= MAX_SLOW_FRAMES_BEFORE_EMERGENCY)
                {
                    if (verboseDebug)
                    {
                        Debug.LogWarning($"[QuestPerf] 🚨 {_consecutiveSlowFrames} consecutive slow frames, triggering emergency measures");
                    }
                    
                    // Force incremental GC to prevent full GC spike
                    TryIncrementalGC();
                    
                    _consecutiveSlowFrames = 0;
                }
            }
            else
            {
                _consecutiveSlowFrames = 0;
            }
            
            // TEMPORARILY DISABLED - regular incremental GC may be causing issues
            // if (aggressiveGCManagement && Time.unscaledTime - _lastGCTime >= gcInterval)
            // {
            //     TryIncrementalGC();
            //     _lastGCTime = Time.unscaledTime;
            // }
            
            // Stats tracking
            if (Time.unscaledTime - _lastStatsReset >= 1f)
            {
                if (verboseDebug && _gcCallsThisSecond > 0)
                {
                    Debug.Log($"[QuestPerf] Stats: GC calls/sec={_gcCallsThisSecond}");
                }
                _gcCallsThisSecond = 0;
                _lastStatsReset = Time.unscaledTime;
            }
            
            _lastFrameTime = frameTime;
        }
        
        private void TryIncrementalGC()
        {
#if UNITY_2019_1_OR_NEWER
            // Use incremental GC if available (spreads collection over multiple frames)
            if (GarbageCollector.isIncremental)
            {
                // Run a small slice of GC work (nanoseconds budget)
                GarbageCollector.CollectIncremental(1000000); // 1ms budget
                _gcCallsThisSecond++;
            }
#endif
        }
        
        /// <summary>
        /// Call this before starting expensive AI operations to prepare for potential frame drops
        /// </summary>
        public void PrepareForExpensiveOperation()
        {
            // Run incremental GC to clear pending garbage before the operation
            TryIncrementalGC();
            
            if (verboseDebug)
            {
                Debug.Log("[QuestPerf] Prepared for expensive operation");
            }
        }
        
        /// <summary>
        /// Call this after expensive AI operations to recover
        /// </summary>
        public void RecoverFromExpensiveOperation()
        {
            _consecutiveSlowFrames = 0;
            
            if (verboseDebug)
            {
                Debug.Log("[QuestPerf] Recovered from expensive operation");
            }
        }
        
        /// <summary>
        /// Force a full GC collection during a loading screen or transition
        /// Only call this when the hourglass would be acceptable (scene transitions, etc.)
        /// </summary>
        public void ForceFullGC()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            if (verboseDebug)
            {
                Debug.Log("[QuestPerf] Forced full GC collection");
            }
        }
        
        /// <summary>
        /// Get the current frame budget in milliseconds
        /// </summary>
        public float GetFrameBudgetMs()
        {
            return 1000f / targetFrameRate;
        }
        
        /// <summary>
        /// Get remaining frame budget based on current frame time
        /// </summary>
        public float GetRemainingFrameBudgetMs()
        {
            float elapsed = Time.unscaledDeltaTime * 1000f;
            return GetFrameBudgetMs() - elapsed;
        }
        
        /// <summary>
        /// Check if we have time to perform additional work this frame
        /// </summary>
        public bool HasFrameBudget(float requiredMs = 2f)
        {
            return GetRemainingFrameBudgetMs() >= requiredMs;
        }
    }
}
