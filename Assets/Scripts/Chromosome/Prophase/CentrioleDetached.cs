using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CentrioleDetached : MonoBehaviour
{
    public GameManager gameManager;
    public GameObject obj;
    public GameObject feedback;    

    public float distanceBetweenObjects;
    float timeTracker = 0f;
    bool oneTimeOperator = false;
    float feedbackTime = 0f;
    bool feedbackOK = true;


    private void Update()
    {
        distanceBetweenObjects = Vector3.Distance(transform.position, obj.transform.position);
        //Debug.DrawLine(transform.position, obj.transform.position, Color.green);

        if (distanceBetweenObjects >= 0.15 && oneTimeOperator == false)
        {
            timeTracker += Time.deltaTime;
            if (timeTracker >= 3.0f)
            {
                //this.gameObject.GetComponent<BoxCollider>().enabled = false;
                oneTimeOperator = true;
                gameManager.Prophase();
            }
        }
        else
        {
            timeTracker = 0f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "Left" && feedbackOK == true)
        {
            feedbackTime += Time.deltaTime;
            if (feedbackTime >= 3f)
            {
                feedback.SetActive(true);
                Debug.Log("Inter_Feedback");
                feedbackOK = false;
            }
        }
        else if (other.gameObject.tag == "Right" && feedbackOK == true)
        {
            feedbackTime += Time.deltaTime;
            if (feedbackTime >= 3f)
            {
                feedback.SetActive(true);
                Debug.Log("Inter_Feedback");
                feedbackOK = false;
            }
        }
        else
            feedbackTime = 0f;
    }

    /*
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player" && feedbackOK == true)
        {
            Debug.Log("opop");
            feedback.SetActive(true);
            feedbackOK = false;
        }
    }
    */

    /*
    private void OnDrawGizmos()
    {
        GUI.color = Color.black;
        Handles.Label(transform.position - (transform.position -
        obj.transform.position) / 2, distanceBetweenObjects.ToString());
    }
    */
}
