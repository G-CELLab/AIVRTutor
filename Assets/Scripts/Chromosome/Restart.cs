 using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Restart : MonoBehaviour
{
    public Image lodingImg;
    public float touchingTime = 3.0f;
    float timer = 0.0f;
    bool triggerDetected = false;
    public Image HPbar;
    public GameManager gameManager;

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

    private void Update()
    {
        if (triggerDetected == true)
        {
            Timer();
            //Debug.Log(timer);
        }
        else
        {
            timer = 0f;
        }
    }


    
    public void Start()
    {
        //ScoreManager.score
        //HPbar.fillAmount += 0.3f;
        //ScoreManager.HPtracking += 0.3f;
        HPbar.fillAmount = ScoreManager.HPtracking;
    }
    

    void Timer()
    {
        timer += Time.deltaTime;
        lodingImg.fillAmount = timer / touchingTime;

        if (timer > touchingTime)
        {
            
            if (ScoreManager.HPtracking >= 0.95)
            {
                gameManager.GameEnd();
            }
            else
            {
                triggerDetected = false;
                WriteDebugToFile.round += 1;
                RestartButton();
                //HPbar.fillAmount += 0.3f;
                
            } 
        }
    }
    void RestartButton()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
