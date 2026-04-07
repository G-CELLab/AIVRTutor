using UnityEngine;

public class ChromatidAnaphaseGrab : MonoBehaviour
{
    private LeftHandManager leftHand;
    private RightHandManager rightHand;

    private Vector3 grabPosOffset;
    private Quaternion grabRotOffset;
    private Transform activeHand;

    void Start()
    {
        // Automatically find the hand managers in the scene
        leftHand = Object.FindAnyObjectByType<LeftHandManager>();
        rightHand = Object.FindAnyObjectByType<RightHandManager>();
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
        // Check if we should release
        if (activeHand != null)
        {
            bool stillGrabbed = false;
            if (activeHand == leftHand?.transform) stillGrabbed = leftHand.isGrabbed_left;
            else if (activeHand == rightHand?.transform) stillGrabbed = rightHand.isGrabbed_right;

            if (!stillGrabbed)
            {
                activeHand = null;
            }
        }

        // Not being held, check for new grab
        if (activeHand == null)
        {
            if (leftHand != null && leftHand.isGrabbed_left && IsNear(leftHand.transform))
            {
                activeHand = leftHand.transform;
                grabPosOffset = activeHand.InverseTransformPoint(transform.position);
                grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
            }
            else if (rightHand != null && rightHand.isGrabbed_right && IsNear(rightHand.transform))
            {
                activeHand = rightHand.transform;
                grabPosOffset = activeHand.InverseTransformPoint(transform.position);
                grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
            }
        }

        // Follow the hand if grabbed
        if (activeHand != null)
        {
            // transform.position = // Handled by XRI activeHand.TransformPoint(grabPosOffset);
            // transform.rotation = // Handled by XRI activeHand.rotation * grabRotOffset;
        }
    }

    bool IsNear(Transform handPos)
    {
        // Adjust 0.2f if the "grab area" feels too small or too large
        return Vector3.Distance(transform.position, handPos.position) < 0.2f;
    }
}