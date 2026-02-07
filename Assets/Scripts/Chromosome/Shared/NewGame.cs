using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class NewGame : MonoBehaviour
{
    public Image lodingImg;
    public GameManager gameManager;
    public float touchingTime = 3.0f;
    float timer = 0.0f;
    bool triggerDetected = false;
    //public GameObject UI;
    //public GameObject UIlocation;
    public Image HPbar;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Left" || other.gameObject.tag == "Right")
        {
            triggerDetected = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Left" || other.gameObject.tag == "Right")
        {
            triggerDetected = false;
        }
    }
    /*
    private void Start()
    {
        HPbar.fillAmount = 0.1f;
    }
    */
    private void Update()
    {
        if (triggerDetected == true)
        {
            Timer();
            Debug.Log(timer);
        }
        else
        {
            timer = 0f;
        }
    }

    void Timer()
    {
        timer += Time.deltaTime;
        lodingImg.fillAmount = timer / touchingTime;

        if (timer > touchingTime)
        {
            Application.Quit();
        }
    }
}
