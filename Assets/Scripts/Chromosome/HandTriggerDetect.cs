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

    bool isFinished = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
        {
            triggerDetected = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
        {
            triggerDetected = false;
            if (!isFinished)
            {
                timer = 0f;
                lodingImg.fillAmount = 0f;
            }
        }
    }

    private void Update()
    {
        if (triggerDetected && !isFinished)
        {
            timer += Time.deltaTime;
            lodingImg.fillAmount = timer / touchingTime;

            if (timer > touchingTime)
            {
                isFinished = true;
                if (gameManager != null)
                {
                    gameManager.Interphase();
                    gameManager.proPhase = true; // Unlocks Condense logic
                }

                if (UI != null && UIlocation != null)
                {
                    UI.transform.position = UIlocation.transform.position;
                    UI.transform.rotation = UIlocation.transform.rotation;
                }
                this.enabled = false;
            }
        }
    }
}