using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if XR_HANDS_1_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine.XR.Hands;
#endif

public class LeftHandManager : MonoBehaviour
{
    public bool isGrabbed_left = false;

    [Header("Interactor Source")]
    [SerializeField]
    private NearFarInteractor handInteractor;

    [Header("Pinch Settings")]
    [SerializeField]
    private float pinchPressDistance = 0.025f;

    [SerializeField]
    private float pinchReleaseDistance = 0.04f;

    private bool pinchActiveByDistance;

#if XR_HANDS_1_2_OR_NEWER
    private List<XRHandSubsystem> handSubsystems;
    private XRHandSubsystem handSubsystem;
#endif

    void Start()
    {
        EnsureInteractorReference();
        EnsureValidPinchThresholds();
#if XR_HANDS_1_2_OR_NEWER
        handSubsystems = new List<XRHandSubsystem>();
        TryFindHandSubsystem();
#endif
    }

    private void OnDisable()
    {
#if XR_HANDS_1_2_OR_NEWER
        if (handSubsystems != null)
        {
            handSubsystems.Clear();
            handSubsystems = null;
        }
        handSubsystem = null;
#endif
    }

    void Update()
    {
        EnsureInteractorReference();

        if (handInteractor != null)
        {
            transform.position = handInteractor.transform.position;
            transform.rotation = handInteractor.transform.rotation;
        }

        bool selectionPinch = handInteractor != null && handInteractor.hasSelection;
        bool distancePinch = TryGetDistancePinch();

        isGrabbed_left = selectionPinch || distancePinch;
    }

    private void EnsureInteractorReference()
    {
        if (handInteractor != null)
            return;

        handInteractor = GetComponentInChildren<NearFarInteractor>();
    }

    private void EnsureValidPinchThresholds()
    {
        pinchPressDistance = Mathf.Max(0.001f, pinchPressDistance);
        pinchReleaseDistance = Mathf.Max(pinchPressDistance + 0.001f, pinchReleaseDistance);
    }

    private bool TryGetDistancePinch()
    {
#if XR_HANDS_1_2_OR_NEWER
        if (handSubsystem == null)
            TryFindHandSubsystem();

        if (handSubsystem == null)
            return false;

        XRHand hand = handSubsystem.leftHand;
        if (!hand.isTracked)
        {
            pinchActiveByDistance = false;
            return false;
        }

        var thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
        var indexTip = hand.GetJoint(XRHandJointID.IndexTip);

        if (!thumbTip.TryGetPose(out var thumbPose) || !indexTip.TryGetPose(out var indexPose))
        {
            pinchActiveByDistance = false;
            return false;
        }

        float tipDistance = Vector3.Distance(thumbPose.position, indexPose.position);
        if (pinchActiveByDistance)
            pinchActiveByDistance = tipDistance <= pinchReleaseDistance;
        else
            pinchActiveByDistance = tipDistance <= pinchPressDistance;

        return pinchActiveByDistance;
#else
        return false;
#endif
    }

#if XR_HANDS_1_2_OR_NEWER
    private void TryFindHandSubsystem()
    {
        if (handSubsystem != null)
            return;

        SubsystemManager.GetSubsystems(handSubsystems);
        for (int i = 0; i < handSubsystems.Count; i++)
        {
            if (handSubsystems[i] != null && handSubsystems[i].running)
            {
                handSubsystem = handSubsystems[i];
                return;
            }
        }

        handSubsystem = handSubsystems.Count > 0 ? handSubsystems[0] : null;
    }
#endif
}