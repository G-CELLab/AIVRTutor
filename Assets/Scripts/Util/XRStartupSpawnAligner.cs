using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Tutorial spawn deterministic by aligning the XR rig camera to
/// "Initial Location" on scene load and shortly after focus returns.
/// </summary>
[DefaultExecutionOrder(1000)]
public class XRStartupSpawnAligner : MonoBehaviour
{
    private const string TutorialSceneName = "Tutorial";
    private const string XrRigName = "XR Origin Hands (XR Rig)";
    private const string SpawnAnchorName = "Initial Location";

    private static XRStartupSpawnAligner _instance;

    [SerializeField] private int startupDelayFrames = 2;
    [SerializeField] private float focusRealignWindowSeconds = 8f;
    [SerializeField] private int maxFocusRealignAttempts = 2;

    private float tutorialSceneLoadedAt = -999f;
    private int focusRealignAttempts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[XRStartupSpawnAligner]");
        _instance = go.AddComponent<XRStartupSpawnAligner>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != TutorialSceneName)
        {
            return;
        }

        float timeSinceLoad = Time.realtimeSinceStartup - tutorialSceneLoadedAt;
        if (timeSinceLoad > focusRealignWindowSeconds)
        {
            return;
        }

        if (focusRealignAttempts >= maxFocusRealignAttempts)
        {
            return;
        }

        focusRealignAttempts++;
        StartCoroutine(AlignAfterFrames("focus-return"));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TutorialSceneName)
        {
            return;
        }

        tutorialSceneLoadedAt = Time.realtimeSinceStartup;
        focusRealignAttempts = 0;
        StartCoroutine(AlignAfterFrames("scene-load"));
    }

    private IEnumerator AlignAfterFrames(string reason)
    {
        for (int i = 0; i < startupDelayFrames; i++)
        {
            yield return null;
        }

        TryAlign(reason);
    }

    private void TryAlign(string reason)
    {
        GameObject rigObject = GameObject.Find(XrRigName);
        if (rigObject == null)
        {
            Debug.LogWarning("[XRStartupSpawnAligner] XR rig not found, skipping alignment.");
            return;
        }

        Transform spawnAnchor = FindAnchorInActiveScene(SpawnAnchorName);
        if (spawnAnchor == null)
        {
            Debug.LogWarning("[XRStartupSpawnAligner] Initial Location not found, skipping alignment.");
            return;
        }

        Transform cameraTransform = Camera.main != null ? Camera.main.transform : null;
        if (cameraTransform == null)
        {
            Debug.LogWarning("[XRStartupSpawnAligner] Main camera not found, skipping alignment.");
            return;
        }

        Transform rigTransform = rigObject.transform;

        Vector3 cameraForwardFlat = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        Vector3 anchorForwardFlat = Vector3.ProjectOnPlane(spawnAnchor.forward, Vector3.up);

        if (cameraForwardFlat.sqrMagnitude > 0.0001f && anchorForwardFlat.sqrMagnitude > 0.0001f)
        {
            float yawDelta = Vector3.SignedAngle(cameraForwardFlat, anchorForwardFlat, Vector3.up);
            rigTransform.RotateAround(cameraTransform.position, Vector3.up, yawDelta);
        }

        Vector3 rigToCameraOffset = rigTransform.position - cameraTransform.position;
        rigTransform.position = spawnAnchor.position + rigToCameraOffset;

        Debug.Log($"[XRStartupSpawnAligner] Aligned tutorial rig on {reason}.");
    }

    private static Transform FindAnchorInActiveScene(string targetName)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return null;
        }

        Queue<Transform> queue = new Queue<Transform>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            queue.Enqueue(roots[i].transform);
        }

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (current.name == targetName)
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                queue.Enqueue(current.GetChild(i));
            }
        }

        return null;
    }
}