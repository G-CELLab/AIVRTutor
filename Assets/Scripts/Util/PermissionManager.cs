using System.Collections;
using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// Requests microphone permission as early as possible at app startup.
/// Does nothing else - no camera, spawn, or scene logic.
/// </summary>
public class PermissionManager : MonoBehaviour
{
    private static PermissionManager _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[PermissionManager]");
        _instance = go.AddComponent<PermissionManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        RequestMicrophonePermission();
    }

    private void RequestMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
#endif
    }

    /// <summary>
    /// True if the microphone permission is currently granted (always true in Editor / non-Android).
    /// </summary>
    public static bool HasMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
        return true;
#endif
    }
}