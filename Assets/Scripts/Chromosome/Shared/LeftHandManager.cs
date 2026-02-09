using System.Collections;
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
            List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0) m_HandSubsystem = subsystems[0];
            return;
        }

        var hand = m_HandSubsystem.leftHand;

        if (hand.isTracked)
        {
            var pinch = hand.GetFingerPinch(HandFinger.Index);
            isGrabbed_left = pinch.isPinching || pinch.pinchAmount > 0.8f;
        }
        else
        {
            isGrabbed_left = false;
        }
    }
}