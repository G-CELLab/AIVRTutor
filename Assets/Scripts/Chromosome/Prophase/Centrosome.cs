using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Centrosome : MonoBehaviour
{
    //public HandAnimationController handAnim;
    //public GameObject testing;

    public GameManager gameManager;

    public Image sliderImg;
    float lodingTime = 3f;
    float timer = 0f;
    float timer2 = 0f;
    float delayTimer = 0f;
    //bool proSuccess = false;
    public bool metaSuccess = false;
    //Transform tempTrans;
    //public GameObject RightChromosome;
    //public GameObject Chromoatin3;
    //public GameObject ProphaseObjs;
    //public GameObject LeftHand;
    //public GameObject RightHand;
    public Sprite loadingImg;
    //public Sprite lockingImg;
    //Component[] chromotins;
        
    public GameObject particleEffect;
    //public Centrosome_Hand hand;

    public RightHandManager rightHandManager;
    public LeftHandManager leftHandManager;

    public GameObject chromotid_L;
    public GameObject chromotid_R;
    public GameObject r_renderer;

    /* 굳이 아들로 뺐다가 부모로 뺄 필요없어짐 -> 오브젝트 자체가 달라져야함 1개짜리에서 2개짜리로
    private void Start()
    {
        RightHand.SetActive(false);
        LeftHand.SetActive(false);

        //set the left chromosome as a child of the right chromosome
        Chromoatin3.GetComponent<Transform>().position = RightChromosome.GetComponent<Transform>().position;
        tempTrans = RightChromosome.transform.parent;
        Chromoatin3.transform.parent = RightChromosome.transform;

        chromotins = Chromoatin3.GetComponentsInChildren<MeshRenderer>();
        foreach (var x in chromotins)
            x.GetComponent<MeshRenderer>().enabled = false;
        
        this.gameObject.GetComponent<Transform>().position = RightChromosome.GetComponent<Transform>().position;
        tempTrans = RightChromosome.transform.parent;
        this.transform.parent = RightChromosome.transform;

        RightHand.SetActive(true);
        LeftHand.SetActive(true);
        //Chromoatin3.SetActive(false);

        //StartCoroutine(WaitAndDelete());
    }

    
    IEnumerator WaitAndDelete()
    {
        yield return new WaitForSeconds(0.5f);
        Chromoatin3.SetActive(false);
    }
    */


    private void Update()
    {
        if (GameManager.eGameStatus == GameManager.GameState.Metaphase)
        {
            delayTimer += Time.deltaTime;
            r_renderer.SetActive(true);
            this.GetComponent<LineRenderer>().enabled = true;
            this.GetComponent<BoxCollider>().size = new Vector3(1f, 1f, 1f);

        }
        // 아나페이스가 된지 3초 후에 이 오브젝트 끄기
        if (GameManager.eGameStatus == GameManager.GameState.Anaphase)
        {
            timer2 += Time.deltaTime;
            if (timer2 >= 3.0f && rightHandManager.isGrabbed_right == false && leftHandManager.isGrabbed_left == false)
            {
                this.gameObject.SetActive(false);
                chromotid_L.SetActive(true);
                chromotid_R.SetActive(true);
            }
            else if (rightHandManager.isGrabbed_right == true || leftHandManager.isGrabbed_left == true)
            {
                timer2 = 0f;
            }
        }
    }


    //3초 이상 Metaphase 구역에 잘 충돌하고 있으면 Anaphase로 이동
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Meta") && metaSuccess == false && delayTimer >= 6.0f)
        {
            sliderImg.sprite = loadingImg;
            sliderImg.color = new Color32(0, 255, 0, 155);

            timer += Time.deltaTime;
            sliderImg.fillAmount = timer / lodingTime;

            if (timer >= lodingTime)
            {
                metaSuccess = true;
                particleEffect.SetActive(false);
                
                sliderImg.enabled = false;

                gameManager.Anaphase();
                timer = 0f;
            }
        }

        /*
        if (other.gameObject.CompareTag("Meta") && metaSuccess == false && hand.L_handTouched == true && hand.R_handTouched == true)
        {
            sliderImg.sprite = loadingImg;
            sliderImg.color = new Color32(0, 255, 0, 155);

            timer += Time.deltaTime;
            sliderImg.fillAmount = timer / lodingTime;

            if (timer >= lodingTime)
            {
                metaSuccess = true;
                particleEffect.SetActive(false);

                tempTrans = ProphaseObjs.transform.parent;
                this.transform.parent = ProphaseObjs.transform;

                sliderImg.enabled = false;

                gameManager.Anaphase();
                timer = 0f;
            }
        }
        */
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Meta") && metaSuccess == false)
        {
            timer = 0f;
        }
    }
    

}
