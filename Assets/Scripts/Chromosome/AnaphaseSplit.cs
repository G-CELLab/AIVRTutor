using UnityEngine;

public class AnaphaseSplit : MonoBehaviour
{
    public LeftHandManager leftHand;
    public RightHandManager rightHand;

    // Set this in the inspector: L for left chromatid, R for right
    public enum Side { Left, Right }
    public Side side;


    void Update()
    {
        // Only interact during Anaphase
        if (GameManager.eGameStatus != GameManager.GameState.Anaphase) return;

        // Determine which hand should grab this specific side
        bool canGrab = false;
        Transform grabbingHandTransform = null;

        if (side == Side.Left && leftHand != null && leftHand.isGrabbed_left)
        {
            canGrab = true;
            grabbingHandTransform = leftHand.transform;
        }
        else if (side == Side.Right && rightHand != null && rightHand.isGrabbed_right)
        {
            canGrab = true;
            grabbingHandTransform = rightHand.transform;
        }

        if (canGrab && IsNear(grabbingHandTransform))
        {
            // // transform.position = // Handled by XRI grabbingHandTransform.position; // Handled by XRGrabInteractable
        }
    }

    bool IsNear(Transform t)
    {
        return Vector3.Distance(transform.position, t.position) < 0.4f;
    }
}