using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

/// <summary>
/// Aligns the XR rig so the player's headset position/orientation matches the
/// "Initial Location" anchor in the active scene. Runs on scene load, and again
/// when the user performs the Meta system gesture (palm menu), since the default
/// OpenXR recenter only recenters at the player's current position rather than
/// returning them to the spawn point.
///
/// Each scene that wants alignment must contain a plain (non-UI) GameObject
/// named "Initial Location" placed at the desired spawn position/orientation.
/// Scenes without one are skipped.
/// </summary>
[DefaultExecutionOrder(1000)]
public class XRStartupSpawnAligner : MonoBehaviour
{
    private const string XrRigName = "XR Origin Hands (XR Rig)";
    private const string AnchorName = "Initial Location";

    [Tooltip("Frames to wait after scene load before aligning (lets XR tracking initialize).")]
    [SerializeField] private int startupDelayFrames = 2;

    [Tooltip("If true, the Meta system gesture (palm menu) re-aligns the rig to the anchor.")]
    [SerializeField] private bool realignOnMetaGesture = true;

    [SerializeField] private float metaGestureRealignDelaySeconds = 0.1f;

    private static XRStartupSpawnAligner _instance;
    private MetaSystemGestureDetector _gestureDetector;
    private bool _gestureActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (_instance != null) return;

        var go = new GameObject("[XRStartupSpawnAligner]");
        _instance = go.AddComponent<XRStartupSpawnAligner>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
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
        UnbindGesture();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(AlignAfterFrames());
        BindGesture();
    }

    private IEnumerator AlignAfterFrames()
    {
        for (int i = 0; i < startupDelayFrames; i++)
        {
            yield return null;
        }

        AlignToAnchor();
    }

    /// <summary>
    /// Repositions/rotates the XR rig so the headset's current position and forward
    /// direction match the "Initial Location" anchor's position and forward direction.
    /// Does nothing if the rig or anchor can't be found.
    /// </summary>
    public void AlignToAnchor()
    {
        GameObject rigObj = GameObject.Find(XrRigName);
        if (rigObj == null)
        {
            return;
        }

        GameObject anchorObj = GameObject.Find(AnchorName);
        if (anchorObj == null)
        {
            return;
        }

        Transform rig = rigObj.transform;
        Transform anchor = anchorObj.transform;
        Transform camera = Camera.main != null ? Camera.main.transform : null;
        if (camera == null)
        {
            return;
        }

        // Rotate the rig so the camera's forward direction matches the anchor's forward direction.
        Vector3 camForward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
        Vector3 anchorForward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);

        if (camForward.sqrMagnitude > 0.0001f && anchorForward.sqrMagnitude > 0.0001f)
        {
            float yawDelta = Vector3.SignedAngle(camForward, anchorForward, Vector3.up);
            rig.RotateAround(camera.position, Vector3.up, yawDelta);
        }

        // Move the rig so the camera's position matches the anchor's position (keeping rig's own Y).
        Vector3 rigToCamera = rig.position - camera.position;
        Vector3 targetRigPos = anchor.position + rigToCamera;
        targetRigPos.y = rig.position.y;
        rig.position = targetRigPos;
    }

    // ===================== Meta Gesture Reset =====================

    private void BindGesture()
    {
        UnbindGesture();

        if (!realignOnMetaGesture)
        {
            return;
        }

        _gestureDetector = FindAnyObjectByType<MetaSystemGestureDetector>();
        if (_gestureDetector != null)
        {
            _gestureDetector.systemGestureStarted.AddListener(OnGestureStarted);
            _gestureDetector.systemGestureEnded.AddListener(OnGestureEnded);
        }
    }

    private void UnbindGesture()
    {
        if (_gestureDetector != null)
        {
            _gestureDetector.systemGestureStarted.RemoveListener(OnGestureStarted);
            _gestureDetector.systemGestureEnded.RemoveListener(OnGestureEnded);
            _gestureDetector = null;
        }

        _gestureActive = false;
    }

    private void OnGestureStarted() => _gestureActive = true;

    private void OnGestureEnded()
    {
        if (!_gestureActive) return;
        _gestureActive = false;
        StartCoroutine(RealignAfterDelay());
    }

    private IEnumerator RealignAfterDelay()
    {
        yield return new WaitForSeconds(metaGestureRealignDelaySeconds);
        AlignToAnchor();
    }
}