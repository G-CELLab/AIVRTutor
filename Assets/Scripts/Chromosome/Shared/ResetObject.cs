using UnityEngine;

public class ResetObject : MonoBehaviour
{
    [Header("Boundary")]
    [Tooltip("Assign the cube collider (or any collider) that defines the allowed simulation area.")]
    [SerializeField] private Collider boundaryCollider;

    [Header("Reset")]
    [Tooltip("If true, object returns to its start rotation when reset.")]
    [SerializeField] private bool resetRotation = true;

    [Tooltip("If true, object returns to its start scale when reset.")]
    [SerializeField] private bool resetScale = false;

    [Tooltip("Optional pause after a reset to prevent rapid repeated resets.")]
    [SerializeField] private float resetCooldownSeconds = 0.25f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;
    private Rigidbody rb;
    private float nextAllowedResetTime;

    private void Awake()
    {
        CaptureStartTransform();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (boundaryCollider == null)
            return;

        if (Time.time < nextAllowedResetTime)
            return;

        if (IsOutsideBoundary())
            ResetToStart();
    }

    [ContextMenu("Capture Current Transform As Start")]
    public void CaptureStartTransform()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;
    }

    [ContextMenu("Reset To Start Now")]
    public void ResetToStart()
    {
        transform.position = startPosition;

        if (resetRotation)
            transform.rotation = startRotation;

        if (resetScale)
            transform.localScale = startScale;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        nextAllowedResetTime = Time.time + Mathf.Max(0f, resetCooldownSeconds);
    }

    private bool IsOutsideBoundary()
    {
        Bounds bounds = boundaryCollider.bounds;
        return !bounds.Contains(transform.position);
    }
}
