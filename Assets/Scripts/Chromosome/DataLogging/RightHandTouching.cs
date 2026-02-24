using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RightHandTouching : MonoBehaviour
{
    private Collider currentCollider;
    private string currentObjectName = "";

    private void OnTriggerEnter(Collider other)
    {
        // Ignore platform and hand-to-hand collisions
        if (other.name == "TopPlatform" || other.CompareTag("Left") || other.CompareTag("Right") || other.name.Contains("Hand"))
            return;
            
        currentCollider = other;
        currentObjectName = other.name;
        TouchTracker.SetRightTouchObject(other.name);
        Debug.Log(other.name + "_RightTouch");
    }

    private void OnTriggerStay(Collider other)
    {
        // Deprioritize platform and ignore hand-to-hand collisions
        if (other.name == "TopPlatform" || other.CompareTag("Left") || other.CompareTag("Right") || other.name.Contains("Hand"))
        {
            if (currentCollider == null)
            {
                currentCollider = other;
                currentObjectName = other.name;
            }
            return;
        }

        if (currentCollider != other)
        {
            currentCollider = other;
            currentObjectName = other.name;
            TouchTracker.SetRightTouchObject(other.name);
        }
        Debug.Log(other.name + "_RightTouch");
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentCollider == other)
        {
            TouchTracker.ClearRightTouchObject();
            currentCollider = null;
            currentObjectName = "";
        }
    }
}
