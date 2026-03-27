using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    [Tooltip("If true, perform one alignment on scene load.")]
    [SerializeField] private bool enableInitialSceneLoadAlign = true;
    [Tooltip("If true, run additional startup realign attempts after the initial alignment.")]
    [SerializeField] private bool enableStartupFollowupRealigns = false;
    [SerializeField] private float focusRealignWindowSeconds = 8f;
    [Tooltip("If true, align again when app focus returns shortly after load.")]
    [SerializeField] private bool enableFocusReturnRealign = false;
    [SerializeField] private int maxFocusRealignAttempts = 2;
    [SerializeField] private float additionalYawOffsetDegrees = 0f;
    [SerializeField] private int startupRealignAttempts = 4;
    [SerializeField] private float startupRealignIntervalSeconds = 0.2f;
    [Tooltip("Additional editor-only realign window to handle late Quest Link tracking updates.")]
    [SerializeField] private bool enableEditorLateRealign = false;
    [SerializeField] private float editorLateRealignWindowSeconds = 6f;
    [SerializeField] private float editorLateRealignIntervalSeconds = 0.5f;
    [Tooltip("Only run editor late realign when planar camera-anchor drift exceeds this threshold.")]
    [SerializeField] private float editorLatePlanarDriftThresholdMeters = 0.06f;
    [Tooltip("Only run editor late realign when yaw drift exceeds this threshold.")]
    [SerializeField] private float editorLateYawDriftThresholdDegrees = 4f;
    [Tooltip("Stop editor late realign after this many consecutive stable checks.")]
    [SerializeField] private int editorLateStableChecksToStop = 2;
    [Tooltip("Optional explicit world-space anchor. If assigned, this is used instead of name search.")]
    [SerializeField] private Transform spawnAnchorOverride;
    [Tooltip("If true, alignment sets rig Y from anchor. If false, keeps current rig Y from scene/runtime.")]
    [SerializeField] private bool alignVerticalToAnchor = false;
    [Tooltip("Extra vertical offset applied only when Align Vertical To Anchor is true.")]
    [SerializeField] private float anchorVerticalOffsetMeters = 0f;

    private float tutorialSceneLoadedAt = -999f;
    private int focusRealignAttempts;
    private Coroutine startupRealignRoutine;
    private Scene _tutorialScene;
    private bool _loggedAnchorCandidates;
    private bool _loggedInvalidRigChildAnchor;

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
            // Never destroy the host GameObject here. This script may be placed on scene anchors
            // (for convenience in the Inspector), and deleting the GameObject can remove "Initial Location".
            Destroy(this);
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
        if (!enableFocusReturnRealign)
        {
            return;
        }

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

        _tutorialScene = scene;
        tutorialSceneLoadedAt = Time.realtimeSinceStartup;
        focusRealignAttempts = 0;
        if (startupRealignRoutine != null)
        {
            StopCoroutine(startupRealignRoutine);
        }

        startupRealignRoutine = StartCoroutine(AlignBurstOnStartup());
    }

    /// <summary>
    /// Public method to force realignment to the starting position.
    /// Call this when the user presses the meta button or needs to reset their position.
    /// </summary>
    public void ResetToStartingPosition()
    {
        TryAlign("manual-reset", true);
    }

    private IEnumerator AlignAfterFrames(string reason)
    {
        if (!enableInitialSceneLoadAlign)
        {
            yield break;
        }

        for (int i = 0; i < startupDelayFrames; i++)
        {
            yield return null;
        }

        TryAlign(reason, true);
    }

    private IEnumerator AlignBurstOnStartup()
    {
        yield return AlignAfterFrames("scene-load");

        if (!enableStartupFollowupRealigns)
        {
            startupRealignRoutine = null;
            yield break;
        }

        int attempts = Mathf.Max(1, startupRealignAttempts);
        float interval = Mathf.Max(0.02f, startupRealignIntervalSeconds);

        for (int i = 1; i < attempts; i++)
        {
            yield return new WaitForSecondsRealtime(interval);
            TryAlign($"scene-load-realign-{i}", true);
        }

        if (Application.isEditor && enableEditorLateRealign)
        {
            float lateWindow = Mathf.Max(0f, editorLateRealignWindowSeconds);
            float lateInterval = Mathf.Max(0.1f, editorLateRealignIntervalSeconds);
            float elapsed = 0f;
            int lateIndex = 0;
            int stableChecks = 0;

            while (elapsed < lateWindow)
            {
                yield return new WaitForSecondsRealtime(lateInterval);
                elapsed += lateInterval;
                lateIndex++;

                bool didAlign = TryAlign($"editor-late-realign-{lateIndex}", false);
                if (didAlign)
                {
                    stableChecks = 0;
                }
                else
                {
                    stableChecks++;
                    if (stableChecks >= Mathf.Max(1, editorLateStableChecksToStop))
                    {
                        break;
                    }
                }
            }
        }

        startupRealignRoutine = null;
    }

    private bool TryAlign(string reason, bool force)
    {
        GameObject rigObject = GameObject.Find(XrRigName);
        if (rigObject == null)
        {
            Debug.LogWarning("[XRStartupSpawnAligner] XR rig not found, skipping alignment.");
            return false;
        }

        Transform rigTransform = rigObject.transform;
        Transform spawnAnchor = ResolveSpawnAnchor(rigTransform);
        if (spawnAnchor == null)
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            Debug.LogWarning($"[XRStartupSpawnAligner] Initial Location not found, skipping alignment. ActiveScene={activeSceneName}, LoadedScenes={GetLoadedSceneNames()}");
            LogAnchorCandidatesOnce();
            return false;
        }

        Transform cameraTransform = Camera.main != null ? Camera.main.transform : null;
        if (cameraTransform == null)
        {
            Debug.LogWarning("[XRStartupSpawnAligner] Main camera not found, skipping alignment.");
            return false;
        }

        if (!force && !NeedsLateRealign(cameraTransform, spawnAnchor))
        {
            return false;
        }

        Vector3 cameraForwardFlat = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        Vector3 anchorForwardFlat = Vector3.ProjectOnPlane(spawnAnchor.forward, Vector3.up);

        if (cameraForwardFlat.sqrMagnitude > 0.0001f && anchorForwardFlat.sqrMagnitude > 0.0001f)
        {
            float yawDelta = Vector3.SignedAngle(cameraForwardFlat, anchorForwardFlat, Vector3.up);
            yawDelta += additionalYawOffsetDegrees;
            rigTransform.RotateAround(cameraTransform.position, Vector3.up, yawDelta);
        }

        Vector3 rigToCameraOffset = rigTransform.position - cameraTransform.position;
        Vector3 targetRigPosition = spawnAnchor.position + rigToCameraOffset;

        if (alignVerticalToAnchor)
        {
            targetRigPosition.y += anchorVerticalOffsetMeters;
        }
        else
        {
            // Keep rig Y from scene/runtime so changing XR Origin Y in the Inspector has effect.
            targetRigPosition.y = rigTransform.position.y;
        }

        rigTransform.position = targetRigPosition;

        Debug.Log($"[XRStartupSpawnAligner] Aligned tutorial rig on {reason}. rigY={rigTransform.position.y:0.###}, camY={cameraTransform.position.y:0.###}, anchorY={spawnAnchor.position.y:0.###}");
        return true;
    }

    private bool NeedsLateRealign(Transform cameraTransform, Transform spawnAnchor)
    {
        Vector3 cameraFlat = Vector3.ProjectOnPlane(cameraTransform.position, Vector3.up);
        Vector3 anchorFlat = Vector3.ProjectOnPlane(spawnAnchor.position, Vector3.up);
        float planarDrift = Vector3.Distance(cameraFlat, anchorFlat);

        Vector3 cameraForwardFlat = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        Vector3 anchorForwardFlat = Vector3.ProjectOnPlane(spawnAnchor.forward, Vector3.up);
        float yawDrift = 0f;
        if (cameraForwardFlat.sqrMagnitude > 0.0001f && anchorForwardFlat.sqrMagnitude > 0.0001f)
        {
            yawDrift = Mathf.Abs(Vector3.SignedAngle(cameraForwardFlat, anchorForwardFlat, Vector3.up) + additionalYawOffsetDegrees);
        }

        return planarDrift > Mathf.Max(0.005f, editorLatePlanarDriftThresholdMeters) ||
               yawDrift > Mathf.Max(0.5f, editorLateYawDriftThresholdDegrees);
    }

    private Transform FindAnchorInActiveScene(string targetName)
    {
        Transform anchor = FindAnchorInScene(_tutorialScene, targetName);
        if (anchor != null)
        {
            return anchor;
        }

        Scene active = SceneManager.GetActiveScene();
        anchor = FindAnchorInScene(active, targetName);
        if (anchor != null)
        {
            return anchor;
        }

        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            anchor = FindAnchorInScene(scene, targetName);
            if (anchor != null)
            {
                return anchor;
            }
        }

        // Last-resort global lookup for inactive/nested objects that can be missed in edge cases.
        anchor = FindAnchorInAllLoadedObjects(targetName);
        if (anchor != null)
        {
            return anchor;
        }

        return null;
    }

    private Transform ResolveSpawnAnchor(Transform rigTransform)
    {
        Transform candidate = spawnAnchorOverride != null ? spawnAnchorOverride : FindAnchorInActiveScene(SpawnAnchorName);
        if (candidate == null)
        {
            return null;
        }

        if (candidate == rigTransform || candidate.IsChildOf(rigTransform))
        {
            if (!_loggedInvalidRigChildAnchor)
            {
                _loggedInvalidRigChildAnchor = true;
                Debug.LogWarning($"[XRStartupSpawnAligner] Anchor '{candidate.name}' is parented under XR rig. Use a world-space anchor outside '{XrRigName}' and assign it to spawnAnchorOverride.");
            }

            return null;
        }

        return candidate;
    }

    private static Transform FindAnchorInScene(Scene scene, string targetName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
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
            if (IsAnchorNameMatch(current.name, targetName))
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

    private static bool IsAnchorNameMatch(string name, string targetName)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        string normalized = name.Trim();
        if (normalized.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow small naming variations in scene objects.
        return
            normalized.Equals("InitialLocation", System.StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Initial Spawn", System.StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Start Location", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLoadedSceneNames()
    {
        int count = SceneManager.sceneCount;
        StringBuilder sb = new StringBuilder(count * 16);
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            Scene s = SceneManager.GetSceneAt(i);
            sb.Append(s.name);
        }

        return sb.ToString();
    }

    private static Transform FindAnchorInAllLoadedObjects(string targetName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
            {
                continue;
            }

            Scene scene = t.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            if (IsAnchorNameMatch(t.name, targetName))
            {
                return t;
            }
        }

        return null;
    }

    private void LogAnchorCandidatesOnce()
    {
        if (_loggedAnchorCandidates)
        {
            return;
        }

        _loggedAnchorCandidates = true;

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        StringBuilder sb = new StringBuilder(512);
        int hitCount = 0;
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
            {
                continue;
            }

            string n = t.name ?? string.Empty;
            if (n.IndexOf("initial", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                n.IndexOf("start", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Scene scene = t.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            if (hitCount < 12)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" | ");
                }

                sb.Append(n);
                sb.Append("@");
                sb.Append(scene.name);
                sb.Append(" active=");
                sb.Append(t.gameObject.activeInHierarchy ? "1" : "0");
            }

            hitCount++;
        }

        Debug.Log($"[XRStartupSpawnAligner] Anchor candidates in loaded scenes: {hitCount}. Samples: {sb}");
    }
}