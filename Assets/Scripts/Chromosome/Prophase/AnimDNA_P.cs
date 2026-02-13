using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class AnimDNA_P : MonoBehaviour
{
    Animator anim;
    float timer = 0f;
    float delayTimer = 0f;
    bool isDone = false;
    public GameManager gameManager;
    
    // Selection tracking via XRI
    private XRGrabInteractable grabInteractable;

    public GameObject text1;
    public GameObject text2;

    [Header("Condense Settings")]
    public float targetCondenseTime = 8.0f; // Increased from 6.0f to ensure full animation
    public float prophaseStartDelay = 2.0f; 

    void Start()
    {
        anim = GetComponent<Animator>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        
        // Fix levitation: Set to Instantaneous for hand tracking
        if (grabInteractable != null)
        {
            grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grabInteractable.useDynamicAttach = true;
        }
    }

    void OnEnable()
    {
        // Reset progress when enabled
        ResetState();
    }

    void Update()
    {
        // Only handle condensation logic during Prophase
        if (GameManager.eGameStatus != GameManager.GameState.Prophase) 
        {
            // Reset logic: Only reset if we go back to Intro or Interphase
            if (GameManager.eGameStatus == GameManager.GameState.Intro || 
                GameManager.eGameStatus == GameManager.GameState.Interphase)
            {
                if (isDone || timer > 0 || delayTimer > 0) ResetState();
            }
            return;
        }

        if (isDone) return;

        delayTimer += Time.deltaTime;
        if (delayTimer < prophaseStartDelay) return;

        // Check if being held
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            timer += Time.deltaTime;
            
            if (anim != null)
            {
                anim.SetBool("isOpened", true);
                anim.SetBool("isIdle", false);
            }

            // Once the timer hits target, we are done.
            if (timer >= targetCondenseTime)
            {
                CompleteCondensation();
            }
        }
        else
        {
            // Reset animation if let go
            if (anim != null)
            {
                anim.SetBool("isOpened", false);
                anim.SetBool("isIdle", true);
            }
            
            if (timer > 0) timer -= Time.deltaTime;
        }
    }

    void ResetState()
    {
        isDone = false;
        timer = 0f;
        delayTimer = 0f;
        if (anim != null)
        {
            anim.SetBool("isOpened", false);
            anim.SetBool("isIdle", true);
        }
    }

    void CompleteCondensation()
    {
        if (isDone) return;
        isDone = true;
        
        Debug.Log("DNA_Condensed successfully - Transitioning to Metaphase");
        
        if (text1 != null) text1.SetActive(false);
        if (text2 != null) text2.SetActive(true);
        
        if (gameManager != null)
        {
            gameManager.Metaphase();
        }
    }
}
