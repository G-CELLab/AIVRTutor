using System.Collections;
using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// Centralized permission management to request permissions early at app startup,
/// rather than when individual features are first used.
/// Singleton that starts automatically and requests permissions in Awake.
/// </summary>
public class PermissionManager : MonoBehaviour
{
    private static PermissionManager _instance;
    private static bool _permissionsRequested = false;
    
    // Static initializer - ensures PermissionManager is created as soon as Unity loads the class
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Debug.Log("[PermissionManager] 🔧 RuntimeInitialize - ensuring PermissionManager exists...");
        // Access Instance to create it if needed
        var instance = Instance;
        Debug.Log("[PermissionManager] ✅ PermissionManager initialized at app startup");
    }
    
    public static PermissionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Create a new PermissionManager if one doesn't exist
                GameObject obj = new GameObject("[PermissionManager]");
                _instance = obj.AddComponent<PermissionManager>();
                DontDestroyOnLoad(obj);
                // Call Awake manually if needed
                _instance.InitializePermissions();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        // Ensure we're truly a singleton
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializePermissions();
    }

    private void InitializePermissions()
    {
        // Request permissions immediately at startup
        if (!_permissionsRequested)
        {
            _permissionsRequested = true;
            RequestMicrophonePermissionImmediate();
            StartCoroutine(WaitForMicrophonePermission());
        }
    }

    /// <summary>
    /// Request microphone permission immediately (Awake/early initialization).
    /// </summary>
    private void RequestMicrophonePermissionImmediate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("[PermissionManager] 🎤 Requesting microphone permission at app startup...");
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Debug.Log("[PermissionManager] Calling Permission.RequestUserPermission(Permission.Microphone)");
            Permission.RequestUserPermission(Permission.Microphone);
            Debug.Log("[PermissionManager] ⏳ Permission dialog should appear to user...");
        }
        else
        {
            Debug.Log("[PermissionManager] ✅ Microphone permission already granted");
        }
#else
        Debug.Log("[PermissionManager] Running in editor or non-Android - permission bypassed");
#endif
    }

    /// <summary>
    /// Wait for microphone permission response in background.
    /// </summary>
    private IEnumerator WaitForMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        float elapsed = 0f;
        float timeout = 15f; // Increased timeout to 15 seconds
        
        while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            if (elapsed % 3 < Time.deltaTime) // Log every ~3 seconds
            {
                Debug.Log($"[PermissionManager] ⏳ Waiting for permission grant... ({elapsed:F1}s)");
            }
            yield return null;
        }
        
        if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Debug.Log("[PermissionManager] ✅✅✅ MICROPHONE PERMISSION GRANTED! Ready for speech recognition. ✅✅✅");
        }
        else
        {
            Debug.LogError("[PermissionManager] ❌ Microphone permission DENIED or TIMEOUT. User must enable it in Quest settings > Permissions.");
        }
#else
        yield return null;
#endif
    }

    /// <summary>
    /// Check if microphone permission is currently authorized.
    /// </summary>
    public static bool HasMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bool hasPermission = Permission.HasUserAuthorizedPermission(Permission.Microphone);
        if (!hasPermission)
        {
            Debug.LogWarning("[PermissionManager] ⚠️ Microphone permission check returned FALSE");
        }
        return hasPermission;
#else
        return true; // Always true on editor or non-Android
#endif
    }

    /// <summary>
    /// Force re-request permission (use if user denies and wants to retry).
    /// </summary>
    public static void RetryMicrophonePermission()
    {
        Debug.Log("[PermissionManager] Retrying microphone permission request...");
        Instance.RequestMicrophonePermissionImmediate();
        Instance.StartCoroutine(Instance.WaitForMicrophonePermission());
    }
}
