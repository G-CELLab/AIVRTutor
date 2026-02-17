using UnityEngine;

public class ChromatidGrabber : MonoBehaviour
{
    private LeftHandManager left;
    private RightHandManager right;
    private bool grabbed = false;

    void Start()
    {
        left = Object.FindFirstObjectByType<LeftHandManager>();
        right = Object.FindFirstObjectByType<RightHandManager>();
    }

    void Update()
    {
        // Only allow movement during Metaphase or Anaphase
        if (GameManager.eGameStatus != GameManager.GameState.Metaphase &&
            GameManager.eGameStatus != GameManager.GameState.Anaphase) return;

        bool isPinching = false;
        Transform activeHand = null;

        // Check Left Hand
        if (left != null && left.isGrabbed_left && IsNear(left.transform))
        {
            isPinching = true;
            activeHand = left.transform;
        }
        // Check Right Hand
        else if (right != null && right.isGrabbed_right && IsNear(right.transform))
        {
            isPinching = true;
            activeHand = right.transform;
        }

        if (isPinching && activeHand != null)
        {
            if (activeHand == left.transform)
            {
                transform.position = left.GetActiveGrabPosition();
                transform.rotation = left.GetActiveGrabRotation();
            }
            else if (activeHand == right.transform)
            {
                transform.position = right.GetActiveGrabPosition();
                transform.rotation = right.GetActiveGrabRotation();
            }
            grabbed = true;
        }
        else
        {
            grabbed = false;
        }
    }

    bool IsNear(Transform hand)
    {
        // If your hands pass through, try increasing this to 0.3f or 0.4f
        return Vector3.Distance(transform.position, hand.position) < 0.25f;
    }
}