using UnityEngine;

public class ChromatidAnaphaseGrab : MonoBehaviour
{
    private LeftHandManager leftHand;
    private RightHandManager rightHand;
    private bool isGrabbed = false;

    void Start()
    {
        // Automatically find the hand managers in the scene
        leftHand = Object.FindFirstObjectByType<LeftHandManager>();
        rightHand = Object.FindFirstObjectByType<RightHandManager>();
    }

    void Update()
    {
        // 1. Only allow interaction during Metaphase or Anaphase
        if (GameManager.eGameStatus != GameManager.GameState.Metaphase &&
            GameManager.eGameStatus != GameManager.GameState.Anaphase) return;

        HandleInteraction();
    }

    void HandleInteraction()
    {
        Transform activeHand = null;

        // 2. Check if hands are pinching AND near this specific object
        if (leftHand != null && leftHand.isGrabbed_left && IsNear(leftHand.transform))
            activeHand = leftHand.transform;
        else if (rightHand != null && rightHand.isGrabbed_right && IsNear(rightHand.transform))
            activeHand = rightHand.transform;

        // 3. Follow the hand if grabbed
        if (activeHand != null)
        {
            transform.position = activeHand.position;
            isGrabbed = true;
        }
        else
        {
            isGrabbed = false;
        }
    }

    bool IsNear(Transform handPos)
    {
        // Adjust 0.2f if the "grab area" feels too small or too large
        return Vector3.Distance(transform.position, handPos.position) < 0.2f;
    }
}