using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AnaPlayerCentrosomeDetect_R : MonoBehaviour
{
    public GameManager gameManager;
    public GameObject particleEffect;

    float timer = 0f;
    float loadingTime = 3f;
    public Image sliderImg;
    public Sprite checkedImg;
    public bool anaSuccess_R = false;
    bool handTouchingDetect = false;

    public GameObject LeftHand;
    public GameObject RightHand;
    public AnaPlayerCentrosomeDetect ana;

    public Image HPbar;

    private void OnTriggerEnter(Collider other)
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
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Centrosome2") && anaSuccess_R == false)
        {
            timer += Time.deltaTime;
            sliderImg.fillAmount = timer / loadingTime;

            if (timer >= loadingTime && handTouchingDetect == false)
            {
                //success effect                     
                anaSuccess_R = true;
                sliderImg.sprite = checkedImg;

                //RightHand.SetActive(false);
                //LeftHand.SetActive(false);

                other.GetComponent<SphereCollider>().enabled = false;

                //RightHand.SetActive(true);
                //LeftHand.SetActive(true);

                particleEffect.SetActive(false);
                timer = 0f;
                //Debug.Log(ana.anaSuccess_L + "Centrosome3");

                if (ana.anaSuccess_L == true)
                {
                    //Debug.Log(ana.anaSuccess_L + "Centrosome2");
                    gameManager.Telophase();
                    ScoreManager.HPtracking += 0.3f;
                }
                /*
                else if (ana.anaSuccess_L == true && HPbar.fillAmount > 0.9f)
                {
                    gameManager.GameEnd();
                }
                */
            }
        }
    }

    /*
    private void Update()
    {
        if (anaSuccess_L == true && anaSuccess_R == true)
        {
            Debug.Log("Centrosome3");
            gameManager.Telophase();
            anaSuccess_L = false;
            anaSuccess_R = false;
        }
    }
    */
}
