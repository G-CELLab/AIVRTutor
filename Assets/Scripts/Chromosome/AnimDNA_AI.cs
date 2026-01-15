using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimDNA_AI : MonoBehaviour
{

    Animator anim;
    public GameManager gameManager;
    float timer = 0f;
    bool isDone = false;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (GameManager.eGameStatus == GameManager.GameState.Prophase && isDone == false)
        {
            timer += Time.deltaTime;
            if (timer > 3.5f)
            {
                isDone = true;
                anim.SetBool("isOpened", true);
                anim.SetBool("isIdle", false);
            }
        }
    }


    /* 여긴 손 대면 응축하는걸로 했던 코드인데 위와 같이 Prophase 가면 자동으로 애니메이션 실행되도록 하여 더이상 필요없음. => 이건 플레이어 DNA에만 필요
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Right"))
        {
            anim.SetBool("isOpened", true);
            anim.SetBool("isIdle", false);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Right"))
        {
            anim.SetBool("isOpened", false);
            anim.SetBool("isIdle", true);
        }
    }
    */
}
