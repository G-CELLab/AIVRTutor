using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class RightHandManager : MonoBehaviour
{
    public InputDeviceCharacteristics controllerType;
    public InputDevice thisController;

    public bool isControllerDetected = false;
    public bool isGrabbed_right = false;

    //public GameObject test;


    public void Start()
    {
        Initialise();
    }

    public void Initialise()
    {
        List<InputDevice> controllerDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerType, controllerDevices);

        if (controllerDevices.Count.Equals(0))
        {
            Debug.Log("Controller list is empty");
        }
        else
        {
            thisController = controllerDevices[0];
            isControllerDetected = true;
        }
    }

    public void Update()
    {
        if (!isControllerDetected)
        {
            Initialise();
        }
        else
        {
            if (thisController.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue > 0.9f)
            {
                //GlobalPennel.transform.parent = LeftHandPosition.transform;
            }
            else
            {
                //GlobalPennel.transform.parent = GlobalPennel_original_location.transform;
            }
            if (thisController.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && gripValue > 0.9f)
            {
                //test.SetActive(true);
                Debug.Log(thisController.name + "Grab_Pressed");
                isGrabbed_right = true;
            }
            else
            {
                //test.SetActive(false);
                isGrabbed_right = false;
            }
        }
    }

}
