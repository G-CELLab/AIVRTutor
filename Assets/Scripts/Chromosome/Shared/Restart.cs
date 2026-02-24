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
            // Check if game is already over (user touching healed wound to close app)
            if (GameManager.eGameStatus == GameManager.GameState.GameOver)
            {
                Debug.Log("User touched healed wound. Closing application.");
                Application.Quit();
                return;
            }
            
            // Increment the healing cycle counter FIRST
            GameManager.IncrementHealingCycle();
            
            // Update HP tracking to reflect the completed cycle
            ScoreManager.HPtracking = 0.3f + (GameManager.GetHealingCycleCount() * 0.3f);
            
            // Log the current cycle (logging system handles cycle tracking automatically)
            Debug.Log($"Cycle {GameManager.GetHealingCycleCount()}/{GameManager.MAX_HEALING_CYCLES} completed. HPtracking: {ScoreManager.HPtracking}");
            
            // Check if the wound is now fully healed (3 cycles completed)
            if (GameManager.IsWoundHealed())
            {
                Debug.Log("Wound fully healed! Showing ending screen.");
                gameManager.GameEnd();
            }
            else
            {
                Debug.Log("Wound not yet healed. Continuing cell division process...");
                triggerDetected = false;
                RestartButton();
            } 
        }
    }
    void RestartButton()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
