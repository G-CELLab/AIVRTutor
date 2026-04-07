using UnityEngine;

public class AnaphaseGrab : MonoBehaviour
{
    public LeftHandManager leftHand;
    public RightHandManager rightHand;

    public enum HandSide { Left, Right, Both }
    public HandSide allowedHand = HandSide.Both;

    private bool isBeingHeld = false;
    private Transform activeHand;
    private Vector3 grabPosOffset;
    private Quaternion grabRotOffset;

    void Update()
    {
        // 1. Only allow pulling if the GameManager says it's Anaphase
        if (GameManager.eGameStatus != GameManager.GameState.Anaphase) return;

        // 2. Check for the grab
        CheckForGrab();

        // 3. Move the chromatid if held
        if (isBeingHeld && activeHand != null)
        {
            // Manual movement disabled to let XRGrabInteractable handle the transform.
            
            // Check if we've pulled it far enough to finish the stage
            CheckCompletion();
        }
    }

    void CheckForGrab()
    {
        bool leftPossible = (allowedHand == HandSide.Left || allowedHand == HandSide.Both);
        bool rightPossible = (allowedHand == HandSide.Right || allowedHand == HandSide.Both);

        // If already being held, check if we should release
        if (isBeingHeld && activeHand != null)
        {
            bool stillGrabbed = false;
            if (activeHand == leftHand.transform) stillGrabbed = leftHand.isGrabbed_left;
            else if (activeHand == rightHand.transform) stillGrabbed = rightHand.isGrabbed_right;

            if (!stillGrabbed)
            {
                isBeingHeld = false;
                activeHand = null;
            }
            return;
        }

        // Not being held, check for new grab
        // Check Left Hand
        if (leftPossible && leftHand.isGrabbed_left && IsHandNear(leftHand.transform))
        {
            isBeingHeld = true;
            activeHand = leftHand.transform;
            grabPosOffset = activeHand.InverseTransformPoint(transform.position);
            grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
        }
        // Check Right Hand
        else if (rightPossible && rightHand.isGrabbed_right && IsHandNear(rightHand.transform))
        {
            isBeingHeld = true;
            activeHand = rightHand.transform;
            grabPosOffset = activeHand.InverseTransformPoint(transform.position);
            grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
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