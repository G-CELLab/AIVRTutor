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
    
    // Track unique colliders to handle multiple parts of the hand (e.g., fingers/wrist) entering/exiting
    private HashSet<Collider> activeColliders = new HashSet<Collider>();

    bool completed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
        {
            if (!activeColliders.Contains(other))
            {
                activeColliders.Add(other);
            }
            LogEventHelper.LogTriggerEnterWound();
            Debug.Log("Trigger_Enter_Wound");
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
        {
            if (activeColliders.Contains(other))
            {
                activeColliders.Remove(other);
            }

            // Reset only if NO hand colliders are left in the trigger
            if (activeColliders.Count == 0 && !completed)
            {
                timer = 0f;
                if (lodingImg != null) lodingImg.fillAmount = 0f;
                LogEventHelper.LogTriggerExitWound();
                Debug.Log("Trigger_Exit_Wound");
            }
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

        // If at least one collider is inside, progress the timer
        if (activeColliders.Count > 0 && !completed)
        {
            Timer();
        }
    }

    void Timer()
    {
        timer += Time.deltaTime;
        if (lodingImg != null) lodingImg.fillAmount = Mathf.Clamp01(timer / touchingTime);

        if (timer >= touchingTime)
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
