using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR; // Standard XR namespace

public class RightHandManager : MonoBehaviour
{
    public bool isGrabbed_right = false;
    private InputDevice rightHandDevice;

    void Update()
    {
        if (!rightHandDevice.isValid)
        {
            InitializeDevice();
            return;
        }

        // In Unity 6, hand-tracking pinch is often mapped to 'Trigger' or 'Pinch' 
        // usage on the hand device.
        if (rightHandDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            isGrabbed_right = triggerValue > 0.8f;
        }
    }

    void InitializeDevice()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.HandTracking, devices);
        if (devices.Count > 0) rightHandDevice = devices[0];
    }
}