using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RightCentrioleMove : MonoBehaviour
{
    public Transform endLocation;
    public bool lineConnectingR = false;
    public bool touchedR = false;

    //private float speed = 0.002f;
    private float timer = 0f;
    private float startDelay = 3.0f;
    //private float startTime;
    //private float journeyLength;
    private LineRenderer lineRenderer;
    [SerializeField] GameObject target;
    [SerializeField] GameObject finalTarget;
    public Centrosome_Hand hand;


    void Start()
    {
        // Keep a note of the time the movement started.
        //startTime = Time.time;

        // Calculate the journey length.
        //journeyLength = Vector3.Distance(this.transform.position, endLocation.position);
        lineRenderer = this.GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (GameManager.eGameStatus == GameManager.GameState.Prophase)
        {
            timer += Time.deltaTime;
            if (timer > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation.position, 0.01f);

                if (timer > 10f)
                    return;
            }
        }
        if (lineConnectingR == true && touchedR == true && hand.R_handTouched == false)
        {
            lineRenderer.material.color = Color.yellow;
            lineRenderer.SetPosition(0, this.transform.position);
            lineRenderer.SetPosition(1, target.transform.position);
        }
        else if (hand.R_handTouched == true)
        {
            //Debug.Log("NoWay!");
            timer = 0f;
            lineConnectingR = false;
            touchedR = false;
            lineRenderer.material.color = Color.yellow;
            lineRenderer.SetPosition(0, this.transform.position);
            lineRenderer.SetPosition(1, finalTarget.transform.position);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Finish")
        {
            this.gameObject.GetComponent<BoxCollider>().enabled = true;
            this.gameObject.GetComponentInChildren<CapsuleCollider>().enabled = true;
            lineConnectingR = true;
            //Debug.Log("Finish");
        }
        else if (other.gameObject.tag == "Right" && lineConnectingR == true)
        {
            touchedR = true;
            //Debug.Log("RightTouch");
        }
    }
}
