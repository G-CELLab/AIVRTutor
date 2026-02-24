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
    
    private XRGrabInteractable grabInteractable;

    public GameObject text1;
    public GameObject text2;

    [Header("Condense Settings")]
    public float targetCondenseTime = 3.0f; 
    public float prophaseStartDelay = 1.0f; 
    public float proximityRadius = 1.2f; // Increased from 0.6 to allow scaling to start earlier

    [Header("Input Source Override")]
    public DualHandMotionGesture gestureSource;

    [Header("Visual Feedback Settings")]
    public float maxScaleMultiplier = 1.0f;
    public float minScaleMultiplier = 0.4f;
    public float startShrinkDistance = 0.8f; 
    public float fullShrinkDistance = 0.25f; // Added to define the point of maximum squeeze
    public float scaleUpDuration = 1.5f;

    private Vector3 m_BaseScale = new Vector3(0.05f, 0.05f, 0.05f);
    private bool m_ScaleInitialized = false;
    private float m_LastLogTime = 0f;

    void Awake()
    {
        anim = GetComponent<Animator>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        CaptureBaseScale();
    }

    void OnEnable()
    {
        ResetState();
    }

    private void CaptureBaseScale()
    {
        if (m_ScaleInitialized) return;
        
        if (transform.localScale.sqrMagnitude > 0.00001f)
        {
            m_BaseScale = transform.localScale;
            m_ScaleInitialized = true;
            Debug.Log($"[AnimDNA_P] Captured base scale: {m_BaseScale}");
        }
    }

    void Update()
    {
        if (isDone) return;
        
        // Debug check for gesture source
        if (gestureSource == null && GameManager.eGameStatus == GameManager.GameState.Prophase)
        {
            if (Time.time > m_LastLogTime + 5f)
            {
                Debug.LogWarning("[AnimDNA_P] Gesture Source is missing! Gesture detection will not work.");
                m_LastLogTime = Time.time;
            }
        }

        if (GameManager.eGameStatus != GameManager.GameState.Prophase) 
        {
            if (GameManager.eGameStatus == GameManager.GameState.Intro || 
                GameManager.eGameStatus == GameManager.GameState.Interphase)
            {
                if (isDone || timer > 0 || delayTimer > 0) ResetState();
            }
            return;
        }

        if (!m_ScaleInitialized) CaptureBaseScale();

        delayTimer += Time.deltaTime;
        if (delayTimer < prophaseStartDelay) return;

        bool isGrabbed = grabInteractable != null && grabInteractable.isSelected;
        bool isGestureActive = gestureSource != null && gestureSource.isGestureActive;
        bool isNearDNA = false;
        float distToDNA = 100f;
        Vector3 midPoint = Vector3.zero;

        if (gestureSource != null && 
            gestureSource.leftHandPose != null && gestureSource.leftHandPose.handTrackingEvents != null &&
            gestureSource.rightHandPose != null && gestureSource.rightHandPose.handTrackingEvents != null)
        {
            if (gestureSource.leftHandPose.handTrackingEvents.handIsTracked && 
                gestureSource.rightHandPose.handTrackingEvents.handIsTracked)
            {
                // Convert hands positions from local XR Origin space to world space
                Transform originTransform = gestureSource.transform;
                Vector3 leftWorldPos = originTransform.TransformPoint(gestureSource.leftHandPose.handTrackingEvents.rootPose.position);
                Vector3 rightWorldPos = originTransform.TransformPoint(gestureSource.rightHandPose.handTrackingEvents.rootPose.position);
                midPoint = (leftWorldPos + rightWorldPos) * 0.5f;
                
                distToDNA = Vector3.Distance(midPoint, transform.position);
                if (distToDNA < proximityRadius) isNearDNA = true;
            }
        }

        // Requirement: Gesture ACTIVE AND NEAR DNA (and not being held)
        if (!isGrabbed && isGestureActive && isNearDNA)
        {
            // Calculate squeeze progress based on actual hand distance
            float handDist = gestureSource.currentDistance;
            float squeezeProgress = Mathf.InverseLerp(startShrinkDistance, fullShrinkDistance, handDist);

            if (Time.time > m_LastLogTime + 1.0f)
            {
                Debug.Log($"[AnimDNA_P] Squeezing: {squeezeProgress:P0} (Dist: {handDist:F2}m), Timer: {timer:F1}/{targetCondenseTime}");
                m_LastLogTime = Time.time;
            }
            
            // 1. Dynamic Scale mapped to hand distance
            if (m_ScaleInitialized)
            {
                float mult = Mathf.Lerp(maxScaleMultiplier, minScaleMultiplier, squeezeProgress);
                transform.localScale = m_BaseScale * mult;
            }

            // 2. Drive Animation parameters
            if (anim != null)
            {
                anim.SetBool("isOpened", squeezeProgress > 0.01f);
                anim.SetBool("isIdle", squeezeProgress <= 0.01f);
                // Drive the detailed progress parameter for fluid animation
                anim.SetFloat("CondenseProgress", squeezeProgress);
            }

            // 3. Completion logic: only progress timer if hands have squeezed significantly
            if (squeezeProgress > 0.85f)
            {
                timer += Time.deltaTime;
                if (timer >= targetCondenseTime)
                {
                    CompleteCondensation();
                }
            }
            else
            {
                if (timer > 0) timer -= Time.deltaTime;
            }
        }
        else
        {
            // Reset animator states if conditions aren't met
            if (anim != null)
            {
                anim.SetBool("isOpened", false);
                anim.SetBool("isIdle", true);
                anim.SetFloat("CondenseProgress", 0f);
            }

            // Gradually revert scale if gesture is lost
            if (m_ScaleInitialized && !isDone)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, m_BaseScale, Time.deltaTime * 3f);
            }
            
            // Diagnostic logging
            if (isGestureActive && Time.time > m_LastLogTime + 1.0f)
            {
                if (isGrabbed)
                    Debug.Log("[AnimDNA_P] Gesture Triggered BUT DNA IS STILL HELD!");
                else if (!isNearDNA)
                    Debug.Log($"[AnimDNA_P] Gesture Triggered BUT TOO FAR! DNA: {transform.position:F2}, MidPoint: {midPoint:F2}, Dist: {distToDNA:F2}m (target < {proximityRadius}m)");
                
                m_LastLogTime = Time.time;
            }

            if (timer > 0) timer -= Time.deltaTime;
        }
    }

    void ResetState()
    {
        isDone = false;
        timer = 0f;
        delayTimer = 0f;
        if (m_ScaleInitialized) transform.localScale = m_BaseScale;
        if (anim != null)
        {
            anim.SetBool("isOpened", false);
            anim.SetBool("isIdle", true);
            anim.SetBool("IsCondensed", false);
        }
    }

    public void CompleteCondensation()
    {
        if (isDone) return;
        isDone = true;
        
        if (text1 != null) text1.SetActive(false);
        if (text2 != null) text2.SetActive(true);

        // Start gradual scale up transition to Metaphase
        StartCoroutine(TransitionToMetaphase());
    }

    IEnumerator TransitionToMetaphase()
    {
        Vector3 shrunkenScale = transform.localScale;
        float elapsed = 0f;

        // Trigger Metaphase logic (activates markers, etc.)
        if (gameManager != null)
            gameManager.Metaphase();

        // Update Animator to the "Condensed" (X-shape) state
        if (anim != null)
        {
            anim.SetBool("isOpened", false);
            anim.SetBool("isIdle", false);
            anim.SetBool("IsCondensed", true);
        }

        // Gradually scale back up while the animation transitions
        while (elapsed < scaleUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleUpDuration;
            // Use smooth step for a nicer feel
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(shrunkenScale, m_BaseScale, smoothT);
            yield return null;
        }

        transform.localScale = m_BaseScale;
    }
}
