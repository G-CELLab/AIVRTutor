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
    float lodingTime = 5f; // Increased from 3f
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
            // Automatically enable spindle fibers when Metaphase starts
            EnableSpindleFibers(true);
        }
        else if (GameManager.eGameStatus != GameManager.GameState.Anaphase && GameManager.eGameStatus != GameManager.GameState.Telophase)
        {
            // Reset timers if not in Metaphase/Anaphase/Telophase
            if (timer != 0 || timer2 != 0 || delayTimer != 0 || metaSuccess)
            {
                timer = 0f;
                timer2 = 0f;
                delayTimer = 0f;
                metaSuccess = false;
                EnableSpindleFibers(false);
            }
        }
    }

    //3초 이상 Metaphase 구역에 잘 충돌하고 있으면 Anaphase로 이동
    private void OnTriggerStay(Collider other)
    {
        if (GameManager.eGameStatus == GameManager.GameState.Metaphase && 
            other.gameObject.CompareTag("Meta") && 
            metaSuccess == false)
        {
            if (sliderImg != null)
            {
                if (!sliderImg.enabled) sliderImg.enabled = true;
                sliderImg.sprite = loadingImg;
                sliderImg.color = new Color32(0, 255, 0, 155);
                
                timer += Time.deltaTime;
                sliderImg.fillAmount = timer / lodingTime;
            }

            // Once the timer hits 5 seconds (lodingTime), then we trigger Anaphase
            if (timer >= lodingTime)
            {
                metaSuccess = true;
                if (particleEffect != null) particleEffect.SetActive(false);
                if (sliderImg != null) sliderImg.enabled = false;

                Debug.Log("Metaphase Complete - Transitioning to Anaphase");
                if (gameManager != null) gameManager.Anaphase();
                timer = 0f;
            }
        }
        else
        {
            // Reset the slider timer if they leave the zone
            if (timer > 0 && !metaSuccess) 
            {
                timer = 0f;
            }
        }
    }

    private void EnableSpindleFibers(bool enable)
    {
        LineRenderer leftLR = GetComponent<LineRenderer>();
        if (leftLR != null) leftLR.enabled = enable;

        if (r_renderer != null)
        {
            // Ensure the right renderer object is active during Metaphase if we want to see it
            if (GameManager.eGameStatus == GameManager.GameState.Metaphase) 
                r_renderer.SetActive(true);
                
            LineRenderer rightLR = r_renderer.GetComponent<LineRenderer>();
            if (rightLR != null) rightLR.enabled = enable;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Meta") && metaSuccess == false)
        {
            timer = 0f;
        }
    }
}
