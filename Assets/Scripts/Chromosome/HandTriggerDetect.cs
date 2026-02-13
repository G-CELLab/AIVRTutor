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
    
    // Track unique colliders to handle multiple parts of the hand (e.g., fingers/wrist) entering/exiting
    private HashSet<Collider> activeColliders = new HashSet<Collider>();
    
    public GameObject UI;
    public GameObject UIlocation;

    bool isFinished = false;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Wound triggered by: {other.name} with tag: {other.tag}");
        if (other.CompareTag("Left") || other.CompareTag("Right"))
        {
            if (!activeColliders.Contains(other))
            {
                activeColliders.Add(other);
                Debug.Log($"Hand collider added: {other.name}. Total: {activeColliders.Count}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
        {
            if (activeColliders.Contains(other))
            {
                activeColliders.Remove(other);
                Debug.Log($"Hand collider removed: {other.name}. Remaining: {activeColliders.Count}");
            }

            // Reset only if NO hand colliders are left in the trigger
            if (activeColliders.Count == 0 && !isFinished)
            {
                timer = 0f;
                if (lodingImg != null) lodingImg.fillAmount = 0f;
                Debug.Log("Timer reset because no hand is touching the wound.");
            }
        }
    }

    private void Update()
    {
        // If at least one collider is inside, progress the timer
        if (activeColliders.Count > 0 && !isFinished)
        {
            timer += Time.deltaTime;
            if (lodingImg != null) lodingImg.fillAmount = Mathf.Clamp01(timer / touchingTime);

            if (timer >= touchingTime)
            {
                FinishInteraction();
            }
        }
    }

    private void FinishInteraction()
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
        
        Debug.Log("Wound interaction complete - Starting Interphase");
        this.enabled = false;
    }
}