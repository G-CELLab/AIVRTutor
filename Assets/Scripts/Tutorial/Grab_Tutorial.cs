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
    bool manipulationPlacementReported = false;
    bool manipulationTriggerDetected = false;
    bool lastManipulationStageActive = false;

    private void OnTriggerEnter(Collider other)
    {
        if (tutorialManager != null && tutorialManager.CurrentStage == Manager_Tutorial.TutorialStage.ManipulationQuestions)
        {
            if (IsBlueChromosomeCollider(other))
            {
                manipulationTriggerDetected = true;
                Debug.Log("[Tutorial] Blue chromosome entered manipulation target.");
            }
            return;
        }

        if (other.gameObject.tag == "Wound")
        {
            triggerDetected = true;
            LogEventHelper.LogTriggerEnterWound();
            Debug.Log("Trigger_Enter_Wound");
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (tutorialManager != null && tutorialManager.CurrentStage == Manager_Tutorial.TutorialStage.ManipulationQuestions)
        {
            if (IsBlueChromosomeCollider(other))
            {
                manipulationTriggerDetected = false;
                if (!manipulationPlacementReported)
                {
                    timer = 0f;
                    if (lodingImg != null) lodingImg.fillAmount = 0f;
                }
                Debug.Log("[Tutorial] Blue chromosome exited manipulation target.");
            }
            return;
        }

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
        bool inManipulationStage = tutorialManager != null && tutorialManager.CurrentStage == Manager_Tutorial.TutorialStage.ManipulationQuestions;

        if (inManipulationStage && !lastManipulationStageActive)
        {
            // Entering manipulation stage: ensure target UI starts empty.
            manipulationPlacementReported = false;
            manipulationTriggerDetected = false;
            timer = 0f;
            if (lodingImg != null) lodingImg.fillAmount = 0f;
        }

        if (!inManipulationStage)
        {
            manipulationPlacementReported = false;
            manipulationTriggerDetected = false;
            if (lodingImg != null) lodingImg.fillAmount = 0f;
        }

        if (inManipulationStage)
        {
            UpdateManipulationPlacementTimer();
            lastManipulationStageActive = true;
            return;
        }
        lastManipulationStageActive = false;

        if (tutorialManager != null && tutorialManager.CurrentStage != Manager_Tutorial.TutorialStage.GrabTutorial)
        {
            timer = 0f;
            if (lodingImg != null) lodingImg.fillAmount = 0f;
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

    private void UpdateManipulationPlacementTimer()
    {
        if (manipulationPlacementReported)
        {
            if (lodingImg != null) lodingImg.fillAmount = 1f;
            return;
        }

        if (manipulationTriggerDetected)
        {
            timer += Time.deltaTime;
            if (lodingImg != null) lodingImg.fillAmount = Mathf.Clamp01(timer / touchingTime);

            if (timer >= touchingTime)
            {
                manipulationPlacementReported = true;
                if (lodingImg != null) lodingImg.fillAmount = 1f;
                tutorialManager?.RegisterManipulationPlacementComplete();
                Debug.Log("[Tutorial] Blue chromosome held in manipulation target long enough.");
            }
        }
        else
        {
            timer = 0f;
            if (lodingImg != null) lodingImg.fillAmount = 0f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (tutorialManager == null || tutorialManager.CurrentStage != Manager_Tutorial.TutorialStage.ManipulationQuestions)
        {
            return;
        }

        if (IsBlueChromosomeCollider(other))
        {
            manipulationTriggerDetected = true;
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

    private bool IsBlueChromosomeCollider(Collider other)
    {
        if (other == null || tutorialManager == null || tutorialManager.chromosome_Object == null)
        {
            return false;
        }

        Transform chromosomeRoot = tutorialManager.chromosome_Object.transform;
        Transform otherTransform = other.transform;
        return otherTransform == chromosomeRoot || otherTransform.IsChildOf(chromosomeRoot);
    }
}
