using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimDNA_P : MonoBehaviour
{
    Animator anim;
    float timer = 0f;
    float delayTimer = 0f;
    //float timer2 = 0f;
    bool isDone = false;
    //bool animPlay = false;
    //bool turnOn = false;
    public GameManager gameManager;
    public RightHandManager rightHandManager;
    public LeftHandManager leftHandManager;
    string newName = "Chromosome";

    public GameObject text1;
    public GameObject text2;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Right") && rightHandManager.isGrabbed_right == true && isDone == false && GameManager.eGameStatus == GameManager.GameState.Prophase && delayTimer >= 6.0f)
        {
            timer += Time.deltaTime;
            anim.SetBool("isOpened", true);
            anim.SetBool("isIdle", false);
            //Debug.Log(isDone);
            //Debug.Log(timer);
            if (timer > 6.5f)
            {
                isDone = true;
                Debug.Log("DNA_Condensed");
                text1.SetActive(false);
                text2.SetActive(true);
                this.gameObject.name = newName;
                gameManager.Metaphase();
                //this.gameObject.name = new string(Chromosome);
            }
        }
        else if (other.CompareTag("Left") && leftHandManager.isGrabbed_left == true && isDone == false && GameManager.eGameStatus == GameManager.GameState.Prophase && delayTimer >= 6.0f)
        {
            timer += Time.deltaTime;
            anim.SetBool("isOpened", true);
            anim.SetBool("isIdle", false);
            //Debug.Log(isDone);
            //Debug.Log(timer);
            if (timer > 6.5f)
            {
                isDone = true;
                Debug.Log("DNA_Condensed");
                text1.SetActive(false);
                text2.SetActive(true);
                this.gameObject.name = newName;
                gameManager.Metaphase();
                //this.gameObject.name = new string(Chromosome);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Right") && isDone == false && GameManager.eGameStatus == GameManager.GameState.Prophase)
        {
            timer = 0f;
            anim.SetBool("isOpened", false);
            anim.SetBool("isIdle", true);
        }
        else if (other.CompareTag("Left") && isDone == false && GameManager.eGameStatus == GameManager.GameState.Prophase)
        {
            timer = 0f;
            anim.SetBool("isOpened", false);
            anim.SetBool("isIdle", true);
        }
    }
    

    void Update()
    {
        if (GameManager.eGameStatus == GameManager.GameState.Prophase)
        {
            delayTimer += Time.deltaTime;
        }
    }
    /*
    void BoxTurnOn()
    {
        this.GetComponent<BoxCollider>().enabled = true;
        turnOn = true;
    }
    */
}
