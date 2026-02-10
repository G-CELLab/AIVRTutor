using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class LeftHandManager : MonoBehaviour
{
    public bool isGrabbed_left = false;
    private XRHandSubsystem m_HandSubsystem;

    void Update()
    {
        if (m_HandSubsystem == null || !m_HandSubsystem.running)
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
            {
                if (s.running)
                {
                    m_HandSubsystem = s;
                    Debug.Log("Left XR Hand Subsystem found and running.");
                    break;
                }
            }
            return;
        }

        var hand = m_HandSubsystem.leftHand;

        if (hand.isTracked)
        {
            var thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
            var indexTip = hand.GetJoint(XRHandJointID.IndexTip);

            if (thumbTip.TryGetPose(out Pose thumbPose) && indexTip.TryGetPose(out Pose indexPose))
            {
                float distance = Vector3.Distance(thumbPose.position, indexPose.position);
                // Increased threshold slightly to 0.03f (3cm) for better reliability in VR
                isGrabbed_left = distance < 0.03f;
            }
        }
        else
        {
            isGrabbed_left = false;
        }
    }
}