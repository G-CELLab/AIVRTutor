using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;
using System.Collections.Generic;

public class LeftHandManager : MonoBehaviour
{
    public bool isGrabbed_left = false;
    
    [SerializeField] 
    private NearFarInteractor handInteractor;

    private XRHandSubsystem m_HandSubsystem;
    private XROrigin m_XROrigin;
    private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
    
    private Transform m_OriginalAttachTransform;
    private GameObject m_GrabAnchor;
    private IXRSelectInteractable m_ManualSelectedInteractable;

    // Relative offset to find the center of a closed fist from the palm joint.
    // OpenXR Palm: Y is normal (out of palm), Z is forward (fingers).
    private readonly Vector3 k_FistCenterOffset = new Vector3(0f, 0.02f, 0.03f); 

    void Awake()
    {
        if (handInteractor != null)
        {
            m_OriginalAttachTransform = handInteractor.attachTransform;
            
            // Create a dedicated anchor for the Grab gesture
            m_GrabAnchor = new GameObject("LeftHandGrabAnchor");
            m_GrabAnchor.transform.SetParent(transform);
        }
    }

    void Update()
    {
        if (handInteractor == null) return;

        // 1. Sync Tracking References
        if (m_HandSubsystem == null || !m_HandSubsystem.running)
        {
            SubsystemManager.GetSubsystems(s_Subsystems);
            foreach (var s in s_Subsystems)
            {
                if (s.running) { m_HandSubsystem = s; break; }
            }
        }

        if (m_XROrigin == null)
        {
            m_XROrigin = GetComponentInParent<XROrigin>();
            if (m_XROrigin == null) m_XROrigin = Object.FindFirstObjectByType<XROrigin>();
        }

        bool isGrabbing = false;
        Pose worldPalmPose = default;
        bool hasHandData = false;

        // 2. Gesture Detection & "Sticky" Pinch Prevention
        if (m_HandSubsystem != null && m_HandSubsystem.leftHand.isTracked)
        {
            var hand = m_HandSubsystem.leftHand;
            if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose localPalmPose))
            {
                hasHandData = true;
                if (m_XROrigin != null && m_XROrigin.Origin != null)
                {
                    worldPalmPose.position = m_XROrigin.Origin.transform.TransformPoint(localPalmPose.position);
                    worldPalmPose.rotation = m_XROrigin.Origin.transform.rotation * localPalmPose.rotation;
                }
                else
                {
                    worldPalmPose = localPalmPose;
                }

                // Detect Fist Curl (Grab)
                XRHandJointID[] tips = { XRHandJointID.MiddleTip, XRHandJointID.RingTip, XRHandJointID.LittleTip };
                int curledCount = 0;
                foreach (var tipID in tips)
                {
                    if (hand.GetJoint(tipID).TryGetPose(out Pose tipPose))
                    {
                        if (Vector3.Distance(tipPose.position, localPalmPose.position) < 0.1f)
                            curledCount++;
                    }
                }
                isGrabbing = curledCount >= 2;

                // Release logic for Pinch
                if (hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose localIndexPose) && 
                    hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose localThumbPose))
                {
                    float pinchDist = Vector3.Distance(localIndexPose.position, localThumbPose.position);
                    if (pinchDist > 0.04f && handInteractor.hasSelection && m_ManualSelectedInteractable == null)
                    {
                        var interactionManager = handInteractor.interactionManager;
                        var selected = new List<IXRSelectInteractable>(handInteractor.interactablesSelected);
                        foreach (var interactable in selected)
                        {
                            interactionManager.SelectExit(handInteractor, interactable);
                        }
                    }
                }
            }
        }

        // 3. Forced Selection Logic (Direct Grab)
        if (isGrabbing)
        {
            if (!handInteractor.hasSelection && handInteractor.interactablesHovered.Count > 0)
            {
                var interactable = handInteractor.interactablesHovered[0] as XRGrabInteractable;
                if (interactable != null)
                {
                    // Center the anchor in the fist
                    m_GrabAnchor.transform.position = worldPalmPose.position + (worldPalmPose.rotation * k_FistCenterOffset);
                    // Use parent Hand rotation to avoid the 45-degree ray tilt
                    m_GrabAnchor.transform.rotation = transform.rotation;
                    
                    handInteractor.attachTransform = m_GrabAnchor.transform;

                    // DEEP INVESTIGATION FIX: Disable snapping to collider surface for the Grab gesture.
                    // This ensures the object sits centered in the hand rather than on the edge.
                    interactable.useDynamicAttach = false;
                    interactable.snapToColliderVolume = false;

                    handInteractor.StartManualInteraction(interactable);
                    m_ManualSelectedInteractable = interactable;
                }
            }
        }
        else if (m_ManualSelectedInteractable != null)
        {
            handInteractor.EndManualInteraction();
            m_ManualSelectedInteractable = null;
        }

        isGrabbed_left = handInteractor.hasSelection || isGrabbing;

        // 4. Transform Sync
        if (hasHandData)
        {
            if (isGrabbing)
            {
                m_GrabAnchor.transform.position = worldPalmPose.position + (worldPalmPose.rotation * k_FistCenterOffset);
                m_GrabAnchor.transform.rotation = transform.rotation;
                handInteractor.attachTransform = m_GrabAnchor.transform;
            }
            else
            {
                handInteractor.attachTransform = m_OriginalAttachTransform;
            }
        }
        else
        {
            handInteractor.attachTransform = m_OriginalAttachTransform;
        }
    }

    public Vector3 GetActiveGrabPosition()
    {
        return handInteractor != null && handInteractor.attachTransform != null 
            ? handInteractor.attachTransform.position 
            : transform.position;
    }

    public Quaternion GetActiveGrabRotation()
    {
        return handInteractor != null && handInteractor.attachTransform != null 
            ? handInteractor.attachTransform.rotation 
            : transform.rotation;
    }
}