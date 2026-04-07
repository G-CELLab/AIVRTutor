using UnityEngine;

public class ChromatidInteraction : MonoBehaviour
{
    public LeftHandManager leftHand;
    public RightHandManager rightHand;

    // Set to LEFT for P_Chromatid_L, RIGHT for P_Chromatid_R
    public enum HandSide { Left, Right }
    public HandSide preferredHand;

    void CheckGrab()
    {
        bool grabbed = false;
        Transform handTransform = null;

        // Allow either hand to grab for Metaphase, or restrict for Anaphase pulling
        if (leftHand != null && leftHand.isGrabbed_left)
        {
            grabbed = true;
            handTransform = leftHand.transform;
        }
        else if (rightHand != null && rightHand.isGrabbed_right)
        {
            grabbed = true;
            handTransform = rightHand.transform;
        }

        if (grabbed && Vector3.Distance(transform.position, handTransform.position) < 0.4f)
        {
            // Snapping
            // // transform.position = // Handled by XRI handTransform.position; // Handled by XRGrabInteractable
        }
    }
}