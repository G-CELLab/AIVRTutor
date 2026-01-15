using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class HandAnimationController : MonoBehaviour
{
    public InputDeviceCharacteristics controllerType;
    public InputDevice thisController;

    private bool isControllerDetected = false;
    private Animator animatorController;
    
    void Start()
    {
        Initialise();
        animatorController = GetComponent<Animator>();
    } 

    void Initialise()
    {
        List<InputDevice> controllerDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerType, controllerDevices);

        if (controllerDevices.Count.Equals(0))
        {
            //Debug.Log("List is empty");
        }
        else
        {
            thisController = controllerDevices[0];
            isControllerDetected = true;
            //Debug.Log(thisController.name);
        }
    }

    void Update()
    {        
        if(!isControllerDetected)
        {
            Initialise();
        }
        else
        {
            if (thisController.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue > 0.1f)
            {
                //Debug.Log(thisController.name + "Trigger_Pressed");
                animatorController.SetFloat("Trigger", triggerValue);                
            }
            else
            {
                animatorController.SetFloat("Trigger", 0);
            }
            if (thisController.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && gripValue > 0.3f)
            {
                //Debug.Log(thisController.name + "Grab_Pressed"); 
                animatorController.SetFloat("Grip", gripValue);
            }
            else
            {
                animatorController.SetFloat("Grip", 0);
            }
        }
    }
}

