using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnaMove : MonoBehaviour
{
    public Transform endLocation_ana;
    private float timer2 = 0f;
    bool isDone2 = false;
    private float startDelay = 3.0f;




    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.eGameStatus == GameManager.GameState.Anaphase && isDone2 == false)
        {
            timer2 += Time.deltaTime;
            if (timer2 > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation_ana.position, 0.01f);

                if (timer2 > 10f)
                {
                    timer2 = 0f;
                    isDone2 = true;
                }
            }
        }

    }
}
