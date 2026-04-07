using UnityEngine;

public class ChromatidGrab : MonoBehaviour
{
    private LeftHandManager left;
    private RightHandManager right;

    [Header("Grab Tuning")]
    public float grabRange = 0.6f; // Large range for testing

    void Start()
    {
        left = Object.FindAnyObjectByType<LeftHandManager>();
        right = Object.FindAnyObjectByType<RightHandManager>();

        // Ensure we have a Rigidbody or physics will fail
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void HandleGrab()
    {
        Transform activeHandTransform = null;

        // Check Left Hand
        if (left != null && left.isGrabbed_left)
        {
            if (Vector3.Distance(transform.position, left.transform.position) < grabRange)
                activeHandTransform = left.transform;
        }

        // Check Right Hand
        if (activeHandTransform == null && right != null && right.isGrabbed_right)
        {
            if (Vector3.Distance(transform.position, right.transform.position) < grabRange)
                activeHandTransform = right.transform;
        }

        if (activeHandTransform != null)
        {
            // BREAK FROM PARENT: Handled by XRI
            // if (transform.parent != null) transform.SetParent(null);

            // Snapping behavior handled by XRGrabInteractable
        }
    }
}