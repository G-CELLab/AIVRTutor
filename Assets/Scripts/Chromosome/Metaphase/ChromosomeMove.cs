using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChromosomeMove : MonoBehaviour
{
    //public Transform endLocation_pro;
    public Transform endLocation_meta;
    //public Transform endLocation_Ana;
    //public Centrosome_Hand centro;
    //private float timer = 0f;
    private float timer2 = 0f;
    //private float timer3 = 0f;
    //bool isDone = false;
    bool isDone2 = false;
    //bool isDone3 = false;
    private float startDelay = 2.0f;
    public GameObject detachable_L;
    public GameObject detachable_R;
    public GameObject r_renderer;

    void Update()
    {
        if (GameManager.eGameStatus == GameManager.GameState.Metaphase && isDone2 == false)
        {
            this.GetComponent<LineRenderer>().enabled = true;
            r_renderer.SetActive(true);
            timer2 += Time.deltaTime;
            if (timer2 > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation_meta.position, 0.01f);

                if (timer2 > 7f)
                {
                    detachable_L.SetActive(true);
                    detachable_R.SetActive(true);
                    this.gameObject.SetActive(false);

                    timer2 = 0f;
                    isDone2 = true;
                }
            }
        }
        /* 아나페이스는 Chromosome을 두개로 나눠야 해서 다른 스크립트로 옮김
        else if (GameManager.eGameStatus == GameManager.GameState.Anaphase && isDone3 == false)
        {
            timer3 += Time.deltaTime;
            if (timer3 > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation_Ana.position, 0.01f);

                if (timer3 > 10f)
                {
                    timer3 = 0f;
                    isDone3 = true;
                }
            }

        }
        */


        /* 여긴 애니메이션 Chromosome으로 바꾸기 전 코드
        if (GameManager.eGameStatus == GameManager.GameState.Prophase && isDone == false)
        {
            timer += Time.deltaTime;
            if (timer > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation_pro.position, 0.01f);

                if (timer > 10f)
                {
                    timer = 0f;
                    isDone = true;
                }                    
            }
        }
        
        if (GameManager.eGameStatus == GameManager.GameState.Metaphase && centro.L_handTouched == true && centro.R_handTouched == true && isDone2 == false)
        {
            timer2 += Time.deltaTime;
            if (timer2 > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation_meta.position, 0.01f);

                if (timer2 > 10f)
                {
                    timer2 = 0f;
                    isDone2 = true;
                }
            }
        }
        else if (GameManager.eGameStatus == GameManager.GameState.Anaphase && isDone3 == false)
        {
            timer3 += Time.deltaTime;
            if (timer3 > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation_Ana.position, 0.01f);

                if (timer3 > 10f)
                {
                    timer3 = 0f;
                    isDone3 = true;
                }
            }

        }
        */
    }
}
