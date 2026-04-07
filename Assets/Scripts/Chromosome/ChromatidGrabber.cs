using UnityEngine;

public class ChromatidGrabber : MonoBehaviour
{
    private LeftHandManager left;
    private RightHandManager right;

    void Start()
    {
        left = Object.FindAnyObjectByType<LeftHandManager>();
        right = Object.FindAnyObjectByType<RightHandManager>();
    }

    private Transform activeHand;
    private Vector3 grabPosOffset;
    private Quaternion grabRotOffset;

    void Update()
    {
        // Only allow movement during Metaphase or Anaphase
        if (GameManager.eGameStatus != GameManager.GameState.Metaphase &&
            GameManager.eGameStatus != GameManager.GameState.Anaphase) return;

        // Check if we should release
        if (activeHand != null)
        {
            bool stillGrabbed = false;
            if (activeHand == left?.transform) stillGrabbed = left.isGrabbed_left;
            else if (activeHand == right?.transform) stillGrabbed = right.isGrabbed_right;

            if (!stillGrabbed)
            {
                activeHand = null;
            }
        }

        // Not being held, check for new grab
        if (activeHand == null)
        {
            if (left != null && left.isGrabbed_left && IsNear(left.transform))
            {
                activeHand = left.transform;
                grabPosOffset = activeHand.InverseTransformPoint(transform.position);
                grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
            }
            else if (right != null && right.isGrabbed_right && IsNear(right.transform))
            {
                activeHand = right.transform;
                grabPosOffset = activeHand.InverseTransformPoint(transform.position);
                grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
            }
        }

        if (activeHand != null)
        {
            // transform.position = // Handled by XRI activeHand.TransformPoint(grabPosOffset);
            // transform.rotation = // Handled by XRI activeHand.rotation * grabRotOffset;
        }
    }

    bool IsNear(Transform hand)
    {
        // If your hands pass through, try increasing this to 0.3f or 0.4f
        return Vector3.Distance(transform.position, hand.position) < 0.25f;
    }
}