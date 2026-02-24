using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Grab_Tutorial : MonoBehaviour
{
    public GameObject grabPosition;
    public GameObject nextTarget;
    public Image lodingImg;
    public Manager_Tutorial tutorialManager;
    public float touchingTime = 3.0f;
    float timer = 0.0f;
    bool triggerDetected = false;
    bool completed = false;
    public bool isFirstTarget = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Wound")
        {
            triggerDetected = true;
            LogEventHelper.LogTriggerEnterWound();
            Debug.Log("Trigger_Enter_Wound");
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Wound")
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
        if (tutorialManager != null && tutorialManager.CurrentStage != Manager_Tutorial.TutorialStage.GrabTutorial)
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
            completed = true;

            if (isFirstTarget)
            {
                if (nextTarget != null)
                {
                    GameObject targetToDisable = grabPosition != null ? grabPosition : gameObject;
                    if (nextTarget.transform.IsChildOf(targetToDisable.transform))
                    {
                        nextTarget.transform.SetParent(targetToDisable.transform.parent, true);
                    }

                    targetToDisable.SetActive(false);
                    nextTarget.SetActive(true);
                }
                else
                {
                    GameObject targetToDisable = grabPosition != null ? grabPosition : gameObject;
                    targetToDisable.SetActive(false);
                }
            }
            else if (tutorialManager != null)
            {
                GameObject targetToDisable = grabPosition != null ? grabPosition : gameObject;
                targetToDisable.SetActive(false);
                tutorialManager.AdvanceStage();
            }
            //SceneManager.LoadScene("ChromosoME");
        }
    }
}
