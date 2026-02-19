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

    [Header("Physical Alignment (Inspector)")]
    [Tooltip("Manual offset for the palm contact point.")]
    public Vector3 palmOffset = Vector3.zero;
    [Tooltip("Manual offset for the pinch contact point.")]
    public Vector3 pinchOffset = Vector3.zero;
    [Tooltip("If true, objects will stay upright (world identity rotation) while held.")]
    public bool keepUpright = true;

    private XRHandSubsystem m_HandSubsystem;
    private XROrigin m_XROrigin;
    private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
    
    private GameObject m_SkeletalAnchor;
    private IXRSelectInteractable m_ActiveInteractable;
    private Vector3 m_ColliderLocalCenter;

    private const float k_SelectionRange = 0.12f;
    private bool m_IsPinching = false;
    private bool m_IsFisting = false;

    void Awake()
    {
        if (handInteractor != null)
        {
            // The anchor serves as the attachTransform for the interactor.
            m_SkeletalAnchor = new GameObject("LeftHand_Anchor");
            m_SkeletalAnchor.transform.SetParent(transform);
            handInteractor.attachTransform = m_SkeletalAnchor.transform;
            
            if (handInteractor.selectInput != null)
            {
                handInteractor.selectInput.manualPerformed = true;
                handInteractor.selectInput.manualValue = 0f;
            }
        }
    }

    void Update()
    {
        if (handInteractor == null) return;

        if (m_HandSubsystem == null || !m_HandSubsystem.running)
        {
            SubsystemManager.GetSubsystems(s_Subsystems);
            foreach (var s in s_Subsystems) { if (s.running) { m_HandSubsystem = s; break; } }
        }
        
        if (m_XROrigin == null) m_XROrigin = GetComponentInParent<XROrigin>();

        UpdateInteractionState();

        if (m_IsPinching || m_IsFisting)
        {
            if (!handInteractor.hasSelection)
            {
                var target = GetValidTarget(m_SkeletalAnchor.transform.position);
                if (target != null && target is XRGrabInteractable grab)
                {
                    // CENTER ON COLLIDER: Calculate local center of the object's collider.
                    var col = grab.GetComponentInChildren<Collider>();
                    if (col != null)
                        m_ColliderLocalCenter = grab.transform.InverseTransformPoint(col.bounds.center);
                    else
                        m_ColliderLocalCenter = Vector3.zero;

                    // Disable default XRI attach logic to allow our manual override.
                    grab.attachTransform = null;
                    grab.useDynamicAttach = false;
                    grab.snapToColliderVolume = false;
                    
                    handInteractor.StartManualInteraction(target);
                    m_ActiveInteractable = target;
                }
            }
            
            if (m_ActiveInteractable != null)
            {
                // UPRIGHT POSITIONING:
                // We want the object's Collider Center to be at HandContactPoint.
                // We want the object's Rotation to be fixed (Upright).
                Vector3 h = GetHandContactPoint();
                Quaternion r = keepUpright ? Quaternion.identity : m_SkeletalAnchor.transform.rotation;
                
                // Set the anchor so that XRI moves the object pivot to: H - (R * L)
                m_SkeletalAnchor.transform.rotation = r;
                m_SkeletalAnchor.transform.position = h - (r * m_ColliderLocalCenter);
            }
        }
        else if (m_ActiveInteractable != null)
        {
            handInteractor.EndManualInteraction();
            m_ActiveInteractable = null;
        }

        isGrabbed_left = handInteractor.hasSelection;
    }

    private void UpdateInteractionState()
    {
        if (m_HandSubsystem != null && m_HandSubsystem.leftHand.isTracked)
        {
            var hand = m_HandSubsystem.leftHand;
            
            // Gestures
            var indexJoint = hand.GetJoint(XRHandJointID.IndexTip);
            var thumbJoint = hand.GetJoint(XRHandJointID.ThumbTip);
            var palmJoint = hand.GetJoint(XRHandJointID.Palm);

            if (indexJoint.TryGetPose(out Pose indexPose) && thumbJoint.TryGetPose(out Pose thumbPose))
            {
                float d = Vector3.Distance(indexPose.position, thumbPose.position);
                if (!m_IsPinching && d < 0.015f) m_IsPinching = true;
                else if (m_IsPinching && d > 0.045f) m_IsPinching = false;
            }

            if (palmJoint.TryGetPose(out Pose palmPose))
            {
                int curled = 0;
                XRHandJointID[] tips = { XRHandJointID.MiddleTip, XRHandJointID.RingTip, XRHandJointID.LittleTip };
                foreach (var id in tips)
                {
                    if (hand.GetJoint(id).TryGetPose(out Pose tipPose) && Vector3.Distance(tipPose.position, palmPose.position) < 0.085f) curled++;
                }
                if (!m_IsFisting && curled >= 2) m_IsFisting = true;
                else if (m_IsFisting && curled < 1) m_IsFisting = false;
            }
        }
        else { m_IsPinching = m_IsFisting = false; }
    }

    private Vector3 GetHandContactPoint()
    {
        if (m_HandSubsystem == null || !m_HandSubsystem.leftHand.isTracked || m_XROrigin == null) return transform.position;
        var hand = m_HandSubsystem.leftHand;

        if (m_IsPinching)
        {
            var indexJoint = hand.GetJoint(XRHandJointID.IndexTip);
            var thumbJoint = hand.GetJoint(XRHandJointID.ThumbTip);
            if (indexJoint.TryGetPose(out Pose indexPose) && thumbJoint.TryGetPose(out Pose thumbPose))
            {
                Vector3 localMid = (indexPose.position + thumbPose.position) * 0.5f;
                return m_XROrigin.Origin.transform.TransformPoint(localMid + (m_SkeletalAnchor.transform.parent.rotation * pinchOffset));
            }
        }

        var palmJoint = hand.GetJoint(XRHandJointID.Palm);
        if (palmJoint.TryGetPose(out Pose palmPose))
        {
            return m_XROrigin.Origin.transform.TransformPoint(palmPose.position + (m_SkeletalAnchor.transform.parent.rotation * palmOffset));
        }

        return transform.position;
    }

    private IXRSelectInteractable GetValidTarget(Vector3 worldPos)
    {
        IXRSelectInteractable best = null;
        float minDist = k_SelectionRange;
        foreach (var h in handInteractor.interactablesHovered)
        {
            if (h is XRGrabInteractable grab)
            {
                var filter = grab.GetComponent<HandSideFilter>();
                if (filter != null && filter.allowedSide != HandSideFilter.HandSide.Left) continue;
                float d = Vector3.Distance(worldPos, grab.transform.position);
                if (d < minDist) { minDist = d; best = grab; }
            }
        }
        return best;
    }

    public Vector3 GetActiveGrabPosition() => m_SkeletalAnchor.transform.position;
    public Quaternion GetActiveGrabRotation() => m_SkeletalAnchor.transform.rotation;
}