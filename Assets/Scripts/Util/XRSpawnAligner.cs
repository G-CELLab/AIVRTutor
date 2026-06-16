// using System.Collections;
// using UnityEngine;
// using UnityEngine.SceneManagement;

// /// <summary>
// /// Aligns the XR rig to the "Initial Location" object in the scene on load.
// /// No gesture handling - Meta system gestures are handled by the headset natively.
// /// </summary>
// public class XRSpawnAligner : MonoBehaviour
// {
//     private static XRSpawnAligner _instance;

//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//     private static void EnsureExists()
//     {
//         if (_instance != null) return;
//         var go = new GameObject("[XRSpawnAligner]");
//         _instance = go.AddComponent<XRSpawnAligner>();
//         DontDestroyOnLoad(go);
//     }

//     private void Awake()
//     {
//         if (_instance != null && _instance != this) { Destroy(this); return; }
//         _instance = this;
//         DontDestroyOnLoad(gameObject);
//     }

//     private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
//     private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

//     private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
//     {
//         StartCoroutine(AlignNextFrame());
//     }

//     private IEnumerator AlignNextFrame()
//     {
//         // Wait 2 frames for XR tracking to initialize.
//         yield return null;
//         yield return null;
//         Align();
//     }

//     public void Align()
//     {
//         var rig = GameObject.Find("XR Origin Hands (XR Rig)");
//         var anchor = GameObject.Find("Initial Location");
//         var cam = Camera.main;

//         if (rig == null || anchor == null || cam == null) return;

//         Transform r = rig.transform;
//         Transform a = anchor.transform;
//         Transform c = cam.transform;

//         // Rotate rig so camera forward matches anchor forward (yaw only).
//         Vector3 camFlat = Vector3.ProjectOnPlane(c.forward, Vector3.up);
//         Vector3 anchorFlat = Vector3.ProjectOnPlane(a.forward, Vector3.up);
//         if (camFlat.sqrMagnitude > 0.001f && anchorFlat.sqrMagnitude > 0.001f)
//         {
//             float yaw = Vector3.SignedAngle(camFlat, anchorFlat, Vector3.up);
//             r.RotateAround(c.position, Vector3.up, yaw);
//         }

//         // Slide rig so camera position matches anchor position (keep rig Y).
//         Vector3 offset = r.position - c.position;
//         r.position = new Vector3(
//             a.position.x + offset.x,
//             r.position.y,
//             a.position.z + offset.z
//         );
//     }
// }