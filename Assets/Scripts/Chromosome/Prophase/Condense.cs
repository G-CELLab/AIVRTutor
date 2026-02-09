using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Condense : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    public GameManager gameManager;
    public Image sliderImg;
    public float lodingTime = 30f;
    private float timer = 0f;
    private bool proSuccess = false;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    void Update()
    {
        // 1. Is it Prophase? 
        if (gameManager == null || !gameManager.proPhase || proSuccess) return;

        // 2. Is the DNA being selected/pinched?
        bool isBeingHeld = (grabInteractable != null && grabInteractable.isSelected);

        if (isBeingHeld)
        {
            timer += Time.deltaTime;
            if (sliderImg != null) sliderImg.fillAmount = timer / lodingTime;

            if (timer >= lodingTime)
            {
                proSuccess = true;
                gameManager.proPhase = false;
                gameManager.Metaphase();
                Debug.Log("DNA Condensed!");
                this.enabled = false;
            }
        }
        else
        {
            // Reset if they let go
            timer = 0f;
            if (sliderImg != null) sliderImg.fillAmount = 0f;
        }
    }
}