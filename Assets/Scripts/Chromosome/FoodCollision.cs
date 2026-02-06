using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR;
using UnityEngine;

public class FoodCollision : MonoBehaviour
{
    public GameManager gameManager;
    bool food = true;
    bool food2 = true;
    bool food3 = true;

    public GameObject Protein;
    public GameObject Magnesium;
    public GameObject VitaminC;

    public GameObject Chromatin3;

    public GameObject Centriole1;
    public GameObject Centriole2;

    public GameObject feedback;

    public GameObject LeftHandManager;
    public GameObject RightHandManager;

    bool leftHandDetected = false;
    bool rightHandDetected = false;

    float timer = 0f;
    float timer2 = 0f;
    float timer3 = 0f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left"))
        {
            leftHandDetected = true;
        }
        else if (other.CompareTag("Right"))
        {
            rightHandDetected = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left"))
        {
            leftHandDetected = false;
        }
        else if (other.CompareTag("Right"))
        {
            rightHandDetected = false;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Food") && LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == false && RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == false 
            && leftHandDetected == false && rightHandDetected == false)
        {
            timer += Time.deltaTime;
            if (timer > 2.0f)
            {
                Debug.Log("Protein_Touched");
                gameManager.FoodCollision();
                Destroy(Protein);
                food = false;
            }
        }
        else if (LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == true || RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == true)
        {
            timer = 0f;
        }

        if (other.gameObject.CompareTag("Food2") && LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == false && RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == false 
            && leftHandDetected == false && rightHandDetected == false)
        {
            timer2 += Time.deltaTime;
            if (timer2 > 2.0f)
            {
                Debug.Log("Magnesium_Touched");
                gameManager.FoodCollision();
                Destroy(Magnesium);
                food2 = false;
            }
        }
        else if (LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == true || RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == true)
        {
            timer2 = 0f;
        }

        if (other.gameObject.CompareTag("Food3") && LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == false && RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == false 
            && leftHandDetected == false && rightHandDetected == false)
        {
            timer3 += Time.deltaTime;
            if (timer3 > 2.0f)
            {
                Debug.Log("VitaminC_Touched");
                gameManager.FoodCollision();
                Destroy(VitaminC);
                food3 = false;
            }
        }
        else if (LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == true || RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == true)
        {
            timer3 = 0f;
        }

        /*
        if (other.CompareTag("Food") && food == true && LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == false && leftHandDetected == true)
        {
            //other.gameObject.GetComponent<MeshRenderer>().enabled = false;
            //other.gameObject.GetComponentInChildren<Canvas>().enabled = false;
            timer += Time.deltaTime;
            food = false;
            gameManager.FoodCollision();
            if (other.gameObject.CompareTag("Food"))
            {
                Destroy(Protein, 2.0f);
            }
        }
        else if (other.CompareTag("Food") && food == true && RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == false && rightHandDetected == true)
        {
            //other.gameObject.GetComponent<MeshRenderer>().enabled = false;
            //other.gameObject.GetComponentInChildren<Canvas>().enabled = false;
            food = false;
            gameManager.FoodCollision();
            if (other.gameObject.CompareTag("Food"))
            {
                Destroy(Protein, 2.0f);
            }
        }

        if (other.CompareTag("Food2") && food2 == true && LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == false && leftHandDetected == true)
        {
            //other.gameObject.GetComponent<MeshRenderer>().enabled = false;
            //other.gameObject.GetComponentInChildren<Canvas>().enabled = false;
            food2 = false;
            gameManager.FoodCollision();
            if (other.gameObject.CompareTag("Food2"))
            {
                Destroy(Magnesium, 2.0f);
            }
        }
        else if (other.CompareTag("Food2") && food2 == true && RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == false && rightHandDetected == true)
        {
            //other.gameObject.GetComponent<MeshRenderer>().enabled = false;
            //other.gameObject.GetComponentInChildren<Canvas>().enabled = false;
            food2 = false;
            gameManager.FoodCollision();
            if (other.gameObject.CompareTag("Food2"))
            {
                Destroy(Magnesium, 2.0f);
            }
        }

        if (other.CompareTag("Food3") && food3 == true && LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == false && leftHandDetected == true)
        {
            //other.gameObject.GetComponent<MeshRenderer>().enabled = false;
            //other.gameObject.GetComponentInChildren<Canvas>().enabled = false;
            food3 = false;
            gameManager.FoodCollision();
            if (other.gameObject.CompareTag("Food3"))
            {
                Destroy(VitaminC, 2.0f);
            }
        }
        else if (other.CompareTag("Food3") && food3 == true && RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == false && rightHandDetected == true)
        {
            //other.gameObject.GetComponent<MeshRenderer>().enabled = false;
            //other.gameObject.GetComponentInChildren<Canvas>().enabled = false;
            food3 = false;
            gameManager.FoodCollision();
            if (other.gameObject.CompareTag("Food3"))
            {
                Destroy(VitaminC, 2.0f);
            }
        }

        */
    }

    bool interphasePart2Triggered = false;

    private void Update()
    {
        if (food == false && food2 == false && food3 == false && !interphasePart2Triggered)
        {
            interphasePart2Triggered = true;
            gameManager.InterphasePart2();
            Centriole1.GetComponent<CapsuleCollider>().enabled = true;
            Centriole2.GetComponent<CapsuleCollider>().enabled = true;
            Chromatin3.SetActive(true);
            feedback.SetActive(false);
        }
    }
}
