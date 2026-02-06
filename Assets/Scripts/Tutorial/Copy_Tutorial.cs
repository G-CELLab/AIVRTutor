using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Copy_Tutorial : MonoBehaviour
{
    public Manager_Tutorial tutorialManager;
    public GameObject obj;

    public float distanceBetweenObjects;
    float timeTracker = 0f;
    bool oneTimeOperator = false;
    bool handTouchingDetect = false;

    private void Update()
    {
        distanceBetweenObjects = Vector3.Distance(transform.position, obj.transform.position);
        //Debug.DrawLine(transform.position, obj.transform.position, Color.green);

        if (distanceBetweenObjects >= 0.15 && oneTimeOperator == false)
        {
            timeTracker += Time.deltaTime;
            if (timeTracker >= 3.0f && handTouchingDetect == false)
            {
                //this.gameObject.GetComponent<BoxCollider>().enabled = false;
                oneTimeOperator = true;
                tutorialManager.tutorial_stage++;
            }
        }
        else
        {
            timeTracker = 0f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "Left" || other.gameObject.tag == "Right")
        {
            handTouchingDetect = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Left" || other.gameObject.tag == "Right")
        {
            handTouchingDetect = false;
        }
    }


}