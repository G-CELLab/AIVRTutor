using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Added for XRI 3.0+

public class Condense : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;

    public GameManager gameManager;

    // You can keep these references in the inspector to avoid errors, 
    // but the script no longer uses them for logic.
    public GameObject LeftHandManager;
    public GameObject RightHandManager;

    bool leftHandDetected = false;
    bool rightHandDetected = false;

    public Image sliderImg;
    float lodingTime = 30f;
    float timer = 0f;
    bool proSuccess = false;

    void Awake()
    {
        // Automatically finds the Grab component we added to the DNA prefab
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left"))
        {
            leftHandDetected = true;
        }
        else if (other.CompareTag("Right"))
        {
            rightHandDetected = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left"))
        {
            leftHandDetected = false;
            timer = 0f;
        }
        else if (other.CompareTag("Right"))
        {
            rightHandDetected = false;
            timer = 0f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Check if the object is currently being grabbed/selected by XRI
        bool isBeingHeld = grabInteractable != null && grabInteractable.isSelected;

        // Logic for Left Hand
        if (other.CompareTag("Left") && leftHandDetected && isBeingHeld && proSuccess == false && gameManager.proPhase == true)
        {
            RunTimer();
        }
        // Logic for Right Hand
        else if (other.CompareTag("Right") && rightHandDetected && isBeingHeld && proSuccess == false && gameManager.proPhase == true)
        {
            RunTimer();
        }
    }

    void RunTimer()
    {
        timer += Time.deltaTime;
        sliderImg.fillAmount = timer / lodingTime;

        if (timer >= lodingTime)
        {
            proSuccess = true;
            gameManager.proPhase = false;

            // Trigger the next phase in your GameManager
            gameManager.Metaphase();

            timer = 0f;
            Debug.Log("Condense Process Complete!");
        }
    }
}
