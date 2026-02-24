using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Touch_Tutorial : MonoBehaviour
{
    public GameObject touchSphere;
    public Image lodingImg;
    public Manager_Tutorial tutorialManager;
    public float touchingTime = 3.0f;
    float timer = 0.0f;
    bool triggerDetected = false;
    bool completed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Left" || other.gameObject.tag == "Right")
        {
            triggerDetected = true;
            LogEventHelper.LogTriggerEnterWound();
            Debug.Log("Trigger_Enter_Wound");
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Left" || other.gameObject.tag == "Right")
        {
            triggerDetected = false;
            LogEventHelper.LogTriggerExitWound();
            Debug.Log("Trigger_Exit_Wound");
        }
    }

    private void Start()
    {
        //Debug.Log("PC");
    }

    private void Update()
    {
        if (tutorialManager != null && tutorialManager.CurrentStage != Manager_Tutorial.TutorialStage.TouchStage)
        {
            timer = 0f;
            return;
        }

        if (completed)
        {
            return;
        }

        if (triggerDetected == true)
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
            touchSphere.SetActive(false);
            //touchSphere.GetComponent<Renderer>().material.color = new Color(1, 255, 1);
            if (tutorialManager != null)
            {
                completed = true;
                tutorialManager.RegisterTouchComplete();
            }
            
        }
    }


}
