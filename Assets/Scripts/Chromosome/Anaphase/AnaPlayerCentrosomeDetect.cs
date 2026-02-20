using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AnaPlayerCentrosomeDetect : MonoBehaviour
{
    public GameManager gameManager;
    public GameObject particleEffect;

    float timer = 0f;
    float loadingTime = 3f;
    public Image sliderImg;
    public Sprite checkedImg;
    public bool anaSuccess_L = false;

    public GameObject LeftHand;
    public GameObject RightHand;
    public AnaPlayerCentrosomeDetect_R ana;

    public Image HPbar;

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Centrosome1") && anaSuccess_L == false)
        {
            // Only start the timer if the object has been moved away from the spawn point 
            // (Center is ~70.17, targets are ~0.39 away). We use 0.2 as a safe threshold.
            float distanceFromCenter = Mathf.Abs(other.transform.position.x - 70.17f);
            
            if (distanceFromCenter > 0.2f)
            {
                timer += Time.deltaTime;
                if (sliderImg != null) sliderImg.fillAmount = timer / loadingTime;

                if (timer >= loadingTime)
                {
                    // Success!
                    anaSuccess_L = true;
                    if (sliderImg != null)
                    {
                        sliderImg.sprite = checkedImg;
                        sliderImg.fillAmount = 1f;
                    }

                    // Lock it in place and disable interaction
                    var grab = other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    if (grab != null) grab.enabled = false;
                    
                    Collider col = other.GetComponent<Collider>();
                    if (col != null) col.enabled = false;

                    if (particleEffect != null) particleEffect.SetActive(false);
                    timer = 0f;

                    if (ana != null && ana.anaSuccess_R == true)
                    {
                        if (gameManager != null) gameManager.Telophase();
                        ScoreManager.HPtracking += 0.3f;
                    }
                }
            }
        }
    }

    /*
    private void Update()
    {
        if (anaSuccess_L == true && anaSuccess_R == true)
        {
            Debug.Log("Centrosome3");
            gameManager.Telophase();
            anaSuccess_L = false;
            anaSuccess_R = false;
        }
    }
    */
}
