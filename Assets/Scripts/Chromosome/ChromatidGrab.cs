using UnityEngine;

public class ChromatidGrab : MonoBehaviour
{
    private LeftHandManager left;
    private RightHandManager right;

    [Header("Grab Tuning")]
    public float grabRange = 0.6f; // Large range for testing

    void Start()
    {
        left = Object.FindFirstObjectByType<LeftHandManager>();
        right = Object.FindFirstObjectByType<RightHandManager>();

        // Ensure we have a Rigidbody or physics will fail
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void Update()
    {
        // Only allow interaction during Anaphase
        if (GameManager.eGameStatus != GameManager.GameState.Anaphase) return;

        HandleGrab();
    }

    void HandleGrab()
    {
        Transform activeHand = null;

        // Check Left Hand
        if (left != null && left.isGrabbed_left)
        {
            if (Vector3.Distance(transform.position, left.transform.position) < grabRange)
                activeHand = left.transform;
        }

        // Check Right Hand
        if (activeHand == null && right != null && right.isGrabbed_right)
        {
            if (Vector3.Distance(transform.position, right.transform.position) < grabRange)
                activeHand = right.transform;
        }

        if (activeHand != null)
        {
            // BREAK FROM PARENT: If this is part of a group, pull it out
            if (transform.parent != null) transform.SetParent(null);

            // FORCE POSITION: Directly overwrite the transform
            transform.position = activeHand.position;

            // Keep the rotation neutral so it's easier to see
            transform.rotation = activeHand.rotation;
        }
    }
}