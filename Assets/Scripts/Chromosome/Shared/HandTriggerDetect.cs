using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandTriggerDetect : MonoBehaviour
{
    public Image lodingImg;
    public GameManager gameManager;
    public float touchingTime = 3.0f;
    float timer = 0.0f;
    bool triggerDetected = false;
    public GameObject UI;
    public GameObject UIlocation;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Left" || other.gameObject.tag == "Right")
        {
            triggerDetected = true;
            Debug.Log("Trigger_Enter_Wound");
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Left" || other.gameObject.tag == "Right")
        {
            triggerDetected = false;
            Debug.Log("Trigger_Exit_Wound");
        }
    }

    private void Start()
    {
        //Debug.Log("PC");
    }

    private void Update()
    {
        if(triggerDetected == true)
        {
            Timer();            
        }
        else
        {
            timer = 0f;
        }
    }

    void Timer()
    {
        timer += Time.deltaTime;
        lodingImg.fillAmount = timer / touchingTime;

        if (timer > touchingTime)
        {
            gameManager.Interphase();            
            triggerDetected = false;
            UI.transform.position = UIlocation.transform.position;
            UI.transform.rotation = UIlocation.transform.rotation;
        }
    }
    
    
}
