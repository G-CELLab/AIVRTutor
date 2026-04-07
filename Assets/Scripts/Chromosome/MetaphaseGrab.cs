using UnityEngine;

public class MetaphaseGrab : MonoBehaviour
{
    public LeftHandManager leftHand;
    public RightHandManager rightHand;

    private bool isGrabbed = false;
    private Transform grabbingHand;

    void Update()
    {
        // Only allow interaction if the game is actually in Metaphase
        if (GameManager.eGameStatus != GameManager.GameState.Metaphase) return;

        // Check for Left Hand Grab
        if (leftHand != null && leftHand.isGrabbed_left)
        {
            if (!isGrabbed) StartGrab(leftHand.transform);
        }
        // Check for Right Hand Grab
        else if (rightHand != null && rightHand.isGrabbed_right)
        {
            if (!isGrabbed) StartGrab(rightHand.transform);
        }
        else
        {
            StopGrab();
        }

        // Move the Chromatid with the hand (Snapping)
        if (isGrabbed && grabbingHand != null)
        {
            // // transform.position = // Handled by XRI grabbingHand.position; // Handled by XRGrabInteractable
        }
    }

    void StartGrab(Transform hand)
    {
        // Only grab if the hand is close enough (e.g., within 0.2 meters)
        if (Vector3.Distance(transform.position, hand.position) < 0.4f)
        {
            isGrabbed = true;
            grabbingHand = hand;
        }
    }

    void StopGrab()
    {
        isGrabbed = false;
        grabbingHand = null;
    }
}