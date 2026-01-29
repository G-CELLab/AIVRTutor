using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AI.Performance
{
    /// <summary>
    /// ThreadPoolDispatcher provides safe async operations for Unity XR applications.
    /// Ensures expensive operations run on background threads while safely dispatching
    /// results back to Unity's main thread without blocking the render/XR thread.
    /// 
    /// Key features:
    /// - Non-blocking dispatch to main thread via Update() polling
    /// - Task-based async operations on ThreadPool
    /// - Cancellation support for cleanup
    /// - GC-friendly action queuing
    /// </summary>
    public class ThreadPoolDispatcher : MonoBehaviour
    {
        private static ThreadPoolDispatcher _instance;
        private static readonly object _lock = new object();
        
        // Main thread action queue - processed in Update() to avoid blocking XR
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        
        // Track pending operations for cancellation
        private CancellationTokenSource _cts;
        
        // Main thread ID for validation
        private int _mainThreadId;
        
        // Max actions per frame to prevent frame spikes
        private const int MAX_ACTIONS_PER_FRAME = 4;
        
        public static ThreadPoolDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            var go = new GameObject("[ThreadPoolDispatcher]");
                            go.hideFlags = HideFlags.HideInHierarchy;
                            _instance = go.AddComponent<ThreadPoolDispatcher>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _cts = new CancellationTokenSource();
            DontDestroyOnLoad(gameObject);
        }
        
        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            
            // Clear pending actions
            while (_mainThreadQueue.TryDequeue(out _)) { }
            
            if (_instance == this)
                _instance = null;
        }
        
        private void Update()
        {
            // Process queued actions on main thread
            // Limit per frame to prevent stutters
            int processed = 0;
            while (processed < MAX_ACTIONS_PER_FRAME && _mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                processed++;
            }
        }
        
        /// <summary>
        /// Check if we're currently on the main thread
        /// </summary>
        public bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        
        /// <summary>
        /// Execute an action on the main thread. If already on main thread, executes immediately.
        /// Otherwise queues for next Update().
        /// </summary>
        public void RunOnMainThread(Action action)
        {
            if (action == null) return;
            
            if (IsMainThread)
            {
                action();
            }
            else
            {
                _mainThreadQueue.Enqueue(action);
            }
        }
        
        /// <summary>
        /// Run a function on a background thread and callback on main thread with result.
        /// This is the main pattern for offloading expensive work.
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="backgroundWork">Work to run on background thread</param>
        /// <param name="onComplete">Callback on main thread with result</param>
        /// <param name="onError">Optional error handler on main thread</param>
        public void RunAsync<T>(Func<T> backgroundWork, Action<T> onComplete, Action<Exception> onError = null)
        {
            if (backgroundWork == null) return;
            
            var token = _cts?.Token ?? CancellationToken.None;
            
            Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;
                    
                    T result = backgroundWork();
                    
                    if (token.IsCancellationRequested) return;
                    
                    if (onComplete != null)
                    {
                        RunOnMainThread(() => onComplete(result));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown, ignore
                }
                catch (Exception e)
                {
                    if (onError != null)
                    {
                        RunOnMainThread(() => onError(e));
                    }
                    else
                    {
                        RunOnMainThread(() => Debug.LogException(e));
                    }
                }
            }, token);
        }
        
        /// <summary>
        /// Run an action on a background thread with completion callback.
        /// </summary>
        public void RunAsync(Action backgroundWork, Action onComplete = null, Action<Exception> onError = null)
        {
            if (backgroundWork == null) return;
            
            var token = _cts?.Token ?? CancellationToken.None;
            
            Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;
                    
                    backgroundWork();
                    
                    if (token.IsCancellationRequested) return;
                    
                    if (onComplete != null)
                    {
                        RunOnMainThread(onComplete);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown, ignore
                }
                catch (Exception e)
                {
                    if (onError != null)
                    {
                        RunOnMainThread(() => onError(e));
                    }
                    else
                    {
                        RunOnMainThread(() => Debug.LogException(e));
                    }
                }
            }, token);
        }
        
        /// <summary>
        /// Encode bytes to base64 on background thread (common expensive operation)
        /// </summary>
        public void EncodeBase64Async(byte[] data, Action<string> onComplete)
        {
            if (data == null || data.Length == 0)
            {
                onComplete?.Invoke(string.Empty);
                return;
            }
            
            RunAsync(
                () => Convert.ToBase64String(data),
                onComplete
            );
        }
        
        /// <summary>
        /// Decode base64 to bytes on background thread
        /// </summary>
        public void DecodeBase64Async(string base64, Action<byte[]> onComplete)
        {
            if (string.IsNullOrEmpty(base64))
            {
                onComplete?.Invoke(null);
                return;
            }
            
            RunAsync(
                () => Convert.FromBase64String(base64),
                onComplete
            );
        }
        
        /// <summary>
        /// Read file bytes on background thread
        /// </summary>
        public void ReadFileBytesAsync(string path, Action<byte[]> onComplete, Action<Exception> onError = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                onComplete?.Invoke(null);
                return;
            }
            
            RunAsync(
                () => System.IO.File.ReadAllBytes(path),
                onComplete,
                onError
            );
        }
        
        /// <summary>
        /// Write file bytes on background thread
        /// </summary>
        public void WriteFileBytesAsync(string path, byte[] data, Action onComplete = null, Action<Exception> onError = null)
        {
            if (string.IsNullOrEmpty(path) || data == null)
            {
                onComplete?.Invoke();
                return;
            }
            
            RunAsync(
                () => System.IO.File.WriteAllBytes(path, data),
                onComplete,
                onError
            );
        }
        
        /// <summary>
        /// Get the cancellation token for external async operations
        /// </summary>
        public CancellationToken GetCancellationToken()
        {
            return _cts?.Token ?? CancellationToken.None;
        }
    }
}
