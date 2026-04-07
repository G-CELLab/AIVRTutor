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
    
    // Selection tracking via XRI and HandManager
    private XRGrabInteractable grabInteractable;
    private LeftHandManager leftManager;
    private RightHandManager rightManager;
    private Transform activeHand;
    private Vector3 grabPosOffset;
    private Quaternion grabRotOffset;

    public GameObject text1;
    public GameObject text2;

    [Header("Condense Settings")]
    public float targetCondenseTime = 8.0f; // Increased from 6.0f to ensure full animation
    public float prophaseStartDelay = 2.0f; 

    void Start()
    {
        anim = GetComponent<Animator>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        leftManager = Object.FindAnyObjectByType<LeftHandManager>();
        rightManager = Object.FindAnyObjectByType<RightHandManager>();
        
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

        // Check if being held via XRI or custom manager
        bool isHeldByXRI = grabInteractable != null && grabInteractable.isSelected;
        
        // Manage manual grab tracking
        HandleManualGrab();
        bool isHeldByManager = activeHand != null;

        if (isHeldByXRI || isHeldByManager)
        {
            timer += Time.deltaTime;
            
            // Apply manual movement if not selected by XRI
            if (isHeldByManager && !isHeldByXRI)
            {
                // transform.position = // Handled by XRI activeHand.TransformPoint(grabPosOffset);
                // transform.rotation = // Handled by XRI activeHand.rotation * grabRotOffset;
            }

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

    void HandleManualGrab()
    {
        // Check if we should release
        if (activeHand != null)
        {
            bool stillGrabbed = false;
            if (activeHand == leftManager?.transform) stillGrabbed = leftManager.isGrabbed_left;
            else if (activeHand == rightManager?.transform) stillGrabbed = rightManager.isGrabbed_right;

            if (!stillGrabbed)
            {
                activeHand = null;
            }
        }

        // Check for new grab
        if (activeHand == null)
        {
            if (leftManager != null && leftManager.isGrabbed_left && IsNear(leftManager.transform))
            {
                activeHand = leftManager.transform;
                grabPosOffset = activeHand.InverseTransformPoint(transform.position);
                grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
            }
            else if (rightManager != null && rightManager.isGrabbed_right && IsNear(rightManager.transform))
            {
                activeHand = rightManager.transform;
                grabPosOffset = activeHand.InverseTransformPoint(transform.position);
                grabRotOffset = Quaternion.Inverse(activeHand.rotation) * transform.rotation;
            }
        }
    }

    bool IsNear(Transform hand)
    {
        return Vector3.Distance(transform.position, hand.position) < 0.25f;
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
