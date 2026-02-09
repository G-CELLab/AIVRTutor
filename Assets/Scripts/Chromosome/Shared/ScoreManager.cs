using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class ScoreManager : MonoBehaviour
{
    public static float HPtracking = 0.3f;
    public Image HPbar;
    public GameObject hpText;
    public GameManager gameManager;
    //bool onetime = false;
    //bool endgame = false;

    //public int currentScore = 0;

    /*
    private void Awake()
    {
        GameObject[] scoreObj = GameObject.FindGameObjectsWithTag("Score");
        if (scoreObj.Length > 1)
        {
            Destroy(this.gameObject);
            //this.GetComponentInChildren<Image>().fillAmount = 0.6f;
        }
        DontDestroyOnLoad(this.gameObject);
    }
    */

    private void Start()
    {
        HPbar.fillAmount = HPtracking;
    }

    private void Update()
    {        
        if (GameManager.eGameStatus == GameManager.GameState.Intro)
        {
            this.GetComponent<Canvas>().enabled = false;
            HPbar.GetComponent<Image>().enabled = false;
            hpText.SetActive(false);
            //onetime = false;
            
        }

        if (GameManager.eGameStatus == GameManager.GameState.Interphase)
        {
            this.GetComponent<Canvas>().enabled = true;
            HPbar.GetComponent<Image>().enabled = true;
            hpText.SetActive(true);
            /*
            if (HPbar.fillAmount > 0.8f)
            {
                endgame = true;
            }
            else if (HPbar.fillAmount <= 0.8f)
            {
                endgame = false;
            }
            */
        }
        /*
        if (GameManager.eGameStatus == GameManager.GameState.Telophase && onetime == false && HPbar.fillAmount < 0.8)
        {
            Healed();
        }
        */

        /* 왜 안되는지 모르겠음
        if (HPbar.fillAmount > 0.8f)
        {
            gameManager.GameEnd();
            Debug.Log("MMMMM");
            //HPbar.fillAmount = 0.4f;
        }
        */

    }

    /*
    void Healed()
    {
         HPbar.fillAmount += 0.3f;
         onetime = true;        
    }
    */

    /*
    private void Start()
    {
        currentScore += 1;
        if (currentScore == 1)
        {
            HPbar.fillAmount += 0.1f;
        }
        else if (currentScore == 2)
        {
            HPbar.fillAmount += 0.3f;
        }
        else if (currentScore == 3)
        {
            HPbar.fillAmount += 0.3f;
        }
        else if (currentScore == 4)
        {
            HPbar.fillAmount += 0.3f;
        }
        else if (currentScore == 5)
        {
            return;
        }
    }
    */


}
