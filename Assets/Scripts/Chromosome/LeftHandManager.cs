using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class LeftHandManager : MonoBehaviour
{
    public InputDeviceCharacteristics controllerType;
    public InputDevice thisController;
    public GameObject GlobalPennel;
    public GameObject LeftHandPosition;
    public GameObject GlobalPennel_original_location;

    public bool isControllerDetected = false;
    public bool isGrabbed_left = false;

    //public GameObject test;

    /* 다시 잡을수 있는 시간 딜레이 주려고 시도했는데 앙댐
     * 내 생각에는 isGrabbed_left = false인 시간을 계산해서 2초가 넘어가면 사라지도록??
    float regrabTime = 3.0f;
    float timer = 0.0f;
    bool youCanGrab = true;
    int onetime = 1;
    */

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
            //Debug.Log("List is empty");
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
            /*
            if (thisController.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue > 0.9f)
            {             
                if (GameManager.eGameStatus != GameManager.GameState.Intro)
                {
                    GlobalPennel.transform.parent = LeftHandPosition.transform;
                    Debug.Log("InfoPanel_Moving");
                }
            }
            else
            {
                GlobalPennel.transform.parent = GlobalPennel_original_location.transform;
            }
            */
            if (thisController.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && gripValue > 0.9f) //&& youCanGrab == true
            {
                Debug.Log(thisController.name + "Grab_Pressed");
                //test.SetActive(true);
                isGrabbed_left = true;
                //onetime++;
            }
            else
            {
                //test.SetActive(false);
                isGrabbed_left = false;               
                /*if (onetime == 1)
                {
                    youCanGrab = false;
                    Timer();
                }
                */
            }
        }
    }

    /*
    void Timer()
    {
        timer += Time.deltaTime;

        if (timer > regrabTime)
        {
            youCanGrab = true;
            timer = 0f;
            onetime--;
        }
    }
    */
}
