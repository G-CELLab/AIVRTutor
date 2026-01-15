using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NameChange : MonoBehaviour
{
    public GameManager gameManager;
    public GameObject text1;
    public GameObject text2;

    float timer = 0f;
    bool onetime = false;
       
    void Update()
    {
        if (GameManager.eGameStatus == GameManager.GameState.Prophase)
        {
            timer += Time.deltaTime;
            if (timer >= 4.5f && onetime == false)
            {
                ChangeName();
            }
        }
    }
    void ChangeName()
    {
        text1.SetActive(false);
        text2.SetActive(true);
        onetime = true;
    }
}
