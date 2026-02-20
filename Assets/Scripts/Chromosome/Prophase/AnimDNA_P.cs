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
    public float proximityRadius = 1.2f; // Increased from 0.8f

    [Header("Input Source Override")]
    public DualHandMotionGesture gestureSource;

    [Header("Visual Feedback Settings")]
    public float maxScaleMultiplier = 1.0f;
    public float minScaleMultiplier = 0.4f;
    public float startShrinkDistance = 0.5f;

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

        if (isDone) return;
        if (!m_ScaleInitialized) CaptureBaseScale();

        delayTimer += Time.deltaTime;
        if (delayTimer < prophaseStartDelay) return;

        bool isGestureActive = gestureSource != null && gestureSource.isGestureActive;
        bool isNearDNA = false;
        float distToDNA = 100f;

        if (gestureSource != null && 
            gestureSource.leftHandPose != null && gestureSource.leftHandPose.handTrackingEvents != null &&
            gestureSource.rightHandPose != null && gestureSource.rightHandPose.handTrackingEvents != null)
        {
            if (gestureSource.leftHandPose.handTrackingEvents.handIsTracked && 
                gestureSource.rightHandPose.handTrackingEvents.handIsTracked)
            {
                Vector3 leftPos = gestureSource.leftHandPose.handTrackingEvents.rootPose.position;
                Vector3 rightPos = gestureSource.rightHandPose.handTrackingEvents.rootPose.position;
                Vector3 midPoint = (leftPos + rightPos) * 0.5f;
                
                distToDNA = Vector3.Distance(midPoint, transform.position);
                if (distToDNA < proximityRadius) isNearDNA = true;

                if (isNearDNA && m_ScaleInitialized)
                {
                    float handDist = gestureSource.currentDistance;
                    float t = Mathf.InverseLerp(startShrinkDistance, gestureSource.minDistance, handDist);
                    float mult = Mathf.Lerp(maxScaleMultiplier, minScaleMultiplier, t);
                    transform.localScale = m_BaseScale * mult;
                }
                else if (m_ScaleInitialized)
                {
                    transform.localScale = m_BaseScale;
                }
            }
            else if (m_ScaleInitialized)
            {
                transform.localScale = m_BaseScale;
            }
        }
        else if (m_ScaleInitialized)
        {
            transform.localScale = m_BaseScale;
        }

        if (isGestureActive && isNearDNA)
        {
            if (Time.time > m_LastLogTime + 1.0f)
            {
                Debug.Log($"[AnimDNA_P] Condense Gesture ACTIVE! Dist: {distToDNA:F2}m (<{proximityRadius}m), Timer: {timer:F1}/{targetCondenseTime}");
                m_LastLogTime = Time.time;
            }
            timer += Time.deltaTime;
            if (anim != null)
            {
                anim.SetBool("isOpened", true);
                anim.SetBool("isIdle", false);
            }

            if (timer >= targetCondenseTime)
            {
                CompleteCondensation();
            }
        }
        else
        {
            if (anim != null)
            {
                anim.SetBool("isOpened", false);
                anim.SetBool("isIdle", true);
            }
            
            // Only log if gesture is active but distance check failed
            if (isGestureActive && !isNearDNA && Time.time > m_LastLogTime + 1.0f)
            {
                Debug.Log($"[AnimDNA_P] Gesture Triggered BUT TOO FAR! Dist: {distToDNA:F2}m (target < {proximityRadius}m)");
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
        }
    }

    public void CompleteCondensation()
    {
        if (isDone) return;
        isDone = true;
        if (m_ScaleInitialized) transform.localScale = m_BaseScale;
        
        if (text1 != null) text1.SetActive(false);
        if (text2 != null) text2.SetActive(true);

        if (gameManager != null)
            gameManager.Metaphase();
    }
}
