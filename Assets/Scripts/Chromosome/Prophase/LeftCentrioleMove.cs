using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeftCentrioleMove : MonoBehaviour
{
    public Transform endLocation;
    public bool lineConnectingL = false;
    public bool touchedL = false;

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

                if (lineConnectingL == true)
                {
                    timer = 0f;
                    return;
                }                    
            }
        }
        if (lineConnectingL == true && touchedL == true && hand.L_handTouched == false)
        {
            lineRenderer.material.color = Color.yellow;
            lineRenderer.SetPosition(0, this.transform.position);
            lineRenderer.SetPosition(1, target.transform.position);
            //Debug.Log("Shoot2");

        }

        if (hand.L_handTouched == true)
        {
            //Debug.Log(timer);
            lineConnectingL = false;
            touchedL = false;
            lineRenderer.material.color = Color.yellow;
            lineRenderer.SetPosition(0, this.transform.position);
            lineRenderer.SetPosition(1, finalTarget.transform.position);
            //Debug.Log("Shoot3");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Finish")
        {
            this.gameObject.GetComponent<BoxCollider>().enabled = true;
            this.gameObject.GetComponentInChildren<CapsuleCollider>().enabled = true;
            lineConnectingL = true;
            Debug.Log("Centriole_Moved_To_Edges");
        }
        else if (other.gameObject.tag == "Left" && lineConnectingL == true)
        {
            touchedL = true;
            //Debug.Log(touchedL);

        }
    }

}
