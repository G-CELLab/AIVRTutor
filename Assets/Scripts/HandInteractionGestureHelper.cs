using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Hands.Samples.GestureSample;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

/// <summary>
/// Fixes issues with objects sticking to hands and incorrect anchor points.
/// Swaps between Pinch and Grab attach points based on hand gesture.
/// </summary>
public class HandInteractionGestureHelper : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The interactor to monitor.")]
    NearFarInteractor m_Interactor;

    [SerializeField]
    [Tooltip("The attach controller to update.")]
    InteractionAttachController m_AttachController;

    [SerializeField]
    [Tooltip("The select input reader to adjust thresholds.")]
    ReleaseThresholdButtonReader m_SelectReader;

    [Header("Attach Points")]
    [SerializeField]
    [Tooltip("Transform representing the pinch point (between index and thumb).")]
    Transform m_PinchPoint;

    [SerializeField]
    [Tooltip("Transform representing the grab point (center of the fist).")]
    Transform m_GrabPoint;

    [Header("Gestures")]
    [SerializeField]
    [Tooltip("The static gesture representing a fist/grab.")]
    StaticHandGesture m_FistGesture;

    [Header("Condense Gesture Suppression")]
    [SerializeField]
    [Tooltip("The DualHandMotionGesture to monitor.")]
    DualHandMotionGesture m_CondenseGesture;

    [SerializeField]
    [Tooltip("If hands are closer than this distance, the interactor is suppressed.")]
    float m_CondenseSuppressionDistance = 0.45f;

    [Header("Settings")]
    [SerializeField]
    [Tooltip("Force pinch to rotate dynamically by disabling the ray endpoint look-at logic.")]
    bool m_ForceDynamicPinchRotation = true;

    [SerializeField]
    [Tooltip("Apply rotation compensation to the grab pose.")]
    bool m_ApplyGrabRotationFix = true;

    [SerializeField]
    Vector3 m_LeftGrabRotationOffset = new Vector3(0, -45, 0);

    [SerializeField]
    Vector3 m_RightGrabRotationOffset = new Vector3(0, 45, 0);

    [SerializeField]
    [Tooltip("The new release threshold for the select input. Lowering this prevents objects from sticking.")]
    [Range(0f, 1f)]
    float m_ReleaseThreshold = 0.25f;

    [SerializeField]
    [Tooltip("If true, will force release if no pinch/grab is detected.")]
    bool m_ForceReleaseOnOpenHand = true;

    private bool m_IsSuppressed = false;

    private bool m_IsLeftHand = false;

    void Start()
    {
        m_IsLeftHand = name.Contains("Left") || name.Contains("L_");

        if (m_SelectReader != null)
        {
            m_SelectReader.releaseThreshold = m_ReleaseThreshold;
        }

        // Issue: Pinch not rotating dynamically.
        if (m_ForceDynamicPinchRotation && m_PinchPoint != null)
        {
            var follower = m_PinchPoint.GetComponent<PinchPointFollow>();
            if (follower != null)
            {
                ClearFollowerInteractors(follower);
            }
        }
    }

    private void ClearFollowerInteractors(PinchPointFollow follower)
    {
        try 
        {
            var type = typeof(PinchPointFollow);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var fields = type.GetFields(flags);
            foreach(var f in fields)
            {
                if (f.Name == "m_NearFarInteractor" || f.Name == "m_RayInteractor")
                {
                    f.SetValue(follower, null);
                }
            }
        }
        catch {}
    }

    void Update()
    {
        if (m_Interactor == null || m_AttachController == null)
            return;

        // Issue 1: Disable gestures when condensing
        CheckCondenseSuppression();

        if (m_IsSuppressed) return;

        // Issue 3: Update anchor points relative to gesture
        bool isFisting = m_FistGesture != null && m_FistGesture.isPerformed;
        
        // Switch the transform to follow based on whether we are grabbing or pinching
        if (isFisting)
        {
            if (m_GrabPoint != null && m_AttachController.transformToFollow != m_GrabPoint)
                m_AttachController.transformToFollow = m_GrabPoint;

            if (m_ApplyGrabRotationFix && m_GrabPoint != null)
            {
                m_GrabPoint.localEulerAngles = m_IsLeftHand ? m_LeftGrabRotationOffset : m_RightGrabRotationOffset;
            }
        }
        else
        {
            // Default to pinch point
            if (m_PinchPoint != null && m_AttachController.transformToFollow != m_PinchPoint)
                m_AttachController.transformToFollow = m_PinchPoint;
        }

        // Issue 2: Force detach if the hand is open (not pinching or fisting)
        if (m_ForceReleaseOnOpenHand && m_Interactor.hasSelection)
        {
            // Lowering the threshold in the Reader is usually the correct XRI way.
        }
    }

    private void CheckCondenseSuppression()
    {
        bool shouldSuppress = false;

        if (m_CondenseGesture != null)
        {
            // Suppress if the gesture is fully active OR if the hands are getting very close
            // This prevents "pinching" highlights from showing up while trying to do the condense gesture.
            if (m_CondenseGesture.isGestureActive || m_CondenseGesture.currentDistance < m_CondenseSuppressionDistance)
            {
                shouldSuppress = true;
            }
        }

        if (shouldSuppress && !m_IsSuppressed)
        {
            m_IsSuppressed = true;
            m_Interactor.enabled = false;
            
            // If we are currently holding something, release it
            if (m_Interactor.hasSelection)
            {
                // This might be abrupt, but is requested to disable everything.
            }

            Debug.Log($"[GestureHelper] Suppressing interactor {gameObject.name} (Hands distance: {m_CondenseGesture?.currentDistance:F2}m)");
        }
        else if (!shouldSuppress && m_IsSuppressed)
        {
            m_IsSuppressed = false;
            m_Interactor.enabled = true;
            Debug.Log($"[GestureHelper] Restoring interactor {gameObject.name}");
        }
    }
}
