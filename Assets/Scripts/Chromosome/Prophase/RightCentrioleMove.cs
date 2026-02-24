using UnityEngine;

public class RightCentrioleMove : MonoBehaviour
{
    [Header("Movement")]
    public Transform endLocation;
    private float timer = 0f;
    private float startDelay = 3.0f;

    [Header("State")]
    public bool lineConnectingR = false;
    public bool touchedR = false;

    [Header("Connections")]
    private LineRenderer lineRenderer;
    [SerializeField] GameObject target;
    [SerializeField] GameObject finalTarget;
    public Centrosome_Hand hand;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

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
        if (lineConnectingR && touchedR && hand.R_handTouched == false)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, target.transform.position);
        }
        else if (hand.R_handTouched)
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
            lineConnectingR = true;
            if (GetComponent<BoxCollider>()) GetComponent<BoxCollider>().enabled = true;
        }

        // 2. Either hand touches the centriole
        if ((other.CompareTag("Left") || other.CompareTag("Right")) && lineConnectingR)
        {
            if (hand.R_handTouched) return; // Already locked

            touchedR = true;
            if (hand != null) hand.R_handTouched = true;
            LogEventHelper.LogCentrioleMoved();

            // DISABLE INTERACTION IMMEDIATELY
            if (GetComponent<BoxCollider>()) GetComponent<BoxCollider>().enabled = false;
            CapsuleCollider child = GetComponentInChildren<CapsuleCollider>();
            if (child) child.enabled = false;

            Debug.Log("Right Centriole Locked and Interaction Disabled.");
        }
    }
}