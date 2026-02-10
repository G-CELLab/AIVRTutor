using UnityEngine;

public class AnaphaseGrab : MonoBehaviour
{
    public LeftHandManager leftHand;
    public RightHandManager rightHand;

    private bool isBeingHeld = false;
    private Transform activeHand;

    void Update()
    {
        // 1. Only allow pulling if the GameManager says it's Anaphase
        if (GameManager.eGameStatus != GameManager.GameState.Anaphase) return;

        // 2. Check for the pinch/grab
        CheckForGrab();

        // 3. Move the chromatid if held
        if (isBeingHeld && activeHand != null)
        {
            transform.position = activeHand.position;

            // Check if we've pulled it far enough to finish the stage
            CheckCompletion();
        }
    }

    void CheckForGrab()
    {
        // Check Left Hand
        if (leftHand.isGrabbed_left && IsHandNear(leftHand.transform))
        {
            isBeingHeld = true;
            activeHand = leftHand.transform;
        }
        // Check Right Hand
        else if (rightHand.isGrabbed_right && IsHandNear(rightHand.transform))
        {
            isBeingHeld = true;
            activeHand = rightHand.transform;
        }
        else
        {
            isBeingHeld = false;
            activeHand = null;
        }
    }

    bool IsHandNear(Transform hand)
    {
        return Vector3.Distance(transform.position, hand.position) < 0.15f;
    }

    void CheckCompletion()
    {
        // If the chromatid is pulled far enough from the center (e.g., 2 units)
        if (Mathf.Abs(transform.position.x) > 2.0f)
        {
            Debug.Log("Chromatid pulled to pole!");
            // You could tell the GameManager to move to Telophase here
        }
    }
}