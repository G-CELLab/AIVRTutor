using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class RightHandManager : MonoBehaviour
{
    public bool isGrabbed_right = false;
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
                    Debug.Log("Right XR Hand Subsystem found and running.");
                    break;
                }
            }
            return;
        }

        var hand = m_HandSubsystem.rightHand;

        if (hand.isTracked)
        {
            var thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
            var indexTip = hand.GetJoint(XRHandJointID.IndexTip);

            if (thumbTip.TryGetPose(out Pose thumbPose) && indexTip.TryGetPose(out Pose indexPose))
            {
                float distance = Vector3.Distance(thumbPose.position, indexPose.position);
                isGrabbed_right = distance < 0.03f;
            }
        }
        else
        {
            isGrabbed_right = false;
        }
    }
}