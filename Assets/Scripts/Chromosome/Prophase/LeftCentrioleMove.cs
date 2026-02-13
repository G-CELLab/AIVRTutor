using UnityEngine;

public class LeftCentrioleMove : MonoBehaviour
{
    [Header("Movement")]
    public Transform endLocation;
    private float timer = 0f;
    private float startDelay = 3.0f;

    [Header("State")]
    public bool lineConnectingL = false;
    public bool touchedL = false;

    [Header("Connections")]
    private LineRenderer lineRenderer;
    [SerializeField] GameObject target;
    [SerializeField] GameObject finalTarget;
    public Centrosome_Hand hand;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        // Ensure Rigidbody exists for trigger detection
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void Update()
    {
        if (GameManager.eGameStatus == GameManager.GameState.Prophase)
        {
            timer += Time.deltaTime;
            if (timer > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation.position, 0.01f);
                if (timer > 10f) return;
            }
        }

        // Draw Spindle Fibers
        if (lineConnectingL && touchedL && hand.L_handTouched == false)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, target.transform.position);
        }
        else if (hand.L_handTouched)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, finalTarget.transform.position);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Reached the pole
        if (other.CompareTag("Finish"))
        {
            lineConnectingL = true;
            // Enable the collider to wait for the hand touch
            if (GetComponent<BoxCollider>()) GetComponent<BoxCollider>().enabled = true;
        }

        // 2. Either hand touches the centriole
        if ((other.CompareTag("Left") || other.CompareTag("Right")) && lineConnectingL)
        {
            if (hand.L_handTouched) return; // Already locked

            touchedL = true;
            if (hand != null) hand.L_handTouched = true;

            // DISABLE INTERACTION IMMEDIATELY
            if (GetComponent<BoxCollider>()) GetComponent<BoxCollider>().enabled = false;
            CapsuleCollider child = GetComponentInChildren<CapsuleCollider>();
            if (child) child.enabled = false;

            Debug.Log("Left Centriole Locked and Interaction Disabled.");
        }
    }
}