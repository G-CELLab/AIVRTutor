using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Condense : MonoBehaviour
{
    public GameManager gameManager;
    public GameObject LeftHandManager;
    public GameObject RightHandManager;

    bool leftHandDetected = false;
    bool rightHandDetected = false;

    public Image sliderImg;
    float lodingTime = 30f;
    float timer = 0f;
    bool proSuccess = false;
    //public Sprite lockingImg;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left"))
        {
            leftHandDetected = true;
        }
        else if (other.CompareTag("Right"))
        {
            rightHandDetected = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left"))
        {
            leftHandDetected = false;
            timer = 0f;
        }
        else if (other.CompareTag("Right"))
        {
            rightHandDetected = false;
            timer = 0f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Left") && leftHandDetected == true && LeftHandManager.GetComponent<LeftHandManager>().isGrabbed_left == true && proSuccess == false && gameManager.proPhase == true)
        {
            timer += Time.deltaTime;
            sliderImg.fillAmount = timer / lodingTime;

            if (timer >= lodingTime)
            {
                proSuccess = true;
                gameManager.proPhase = false;

                //sliderImg.sprite = lockingImg;
                //sliderImg.color = new Color32(255, 255, 0, 155);

                //go to metaphase
                //게임 메니저 상에는 여기서 메타페이스로 가지만 UI는 나중에 Spindle fiber까지 다 붙이고 난 다음에 Meta info UI가 켜지도록 스크립트 작성함 (Centrosome_Hand.cs 참고)
                gameManager.Metaphase();

                //지금은 이렇게 삭제하지만 나중에는 손에 잡힌상태에서 물체 사라지는 것에 대한 해결책을 찾아야만 함!!
                //Destroy(this.gameObject, 3f);

                timer = 0f;
            }
        }
        else if (other.CompareTag("Right") && rightHandDetected == true && RightHandManager.GetComponent<RightHandManager>().isGrabbed_right == true && proSuccess == false && gameManager.proPhase == true)
        {
            timer += Time.deltaTime;
            sliderImg.fillAmount = timer / lodingTime;

            if (timer >= lodingTime)
            {
                proSuccess = true;
                gameManager.proPhase = false;

                //sliderImg.sprite = lockingImg;
                //sliderImg.color = new Color32(255, 255, 0, 155);

                //go to metaphase
                gameManager.Metaphase();
                //Destroy(this.gameObject, 3f);

                timer = 0f;
            }
        }
    }


}
