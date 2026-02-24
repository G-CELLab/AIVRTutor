using UnityEngine;
using UnityEngine.UI;

public class Condense : MonoBehaviour
{
    [Header("Hand Managers")]
    public LeftHandManager leftHand;
    public RightHandManager rightHand;

    [Header("Required Objects")]
    [Tooltip("Drag the loose 'Chromatin' DNA here.")]
    public GameObject prophaseDNA;

    [Tooltip("Drag the 'Sparkling Fairy Effect' object here.")]
    public GameObject targetAreaObject;

    [Header("Progress Settings")]
    public float timer = 0f;
    public float targetTime = 3f; // Set to 3 seconds
    public float areaRadius = 0.5f;
    public Image sliderImg;
    public Animator dnaAnimator;

    private bool isFinished = false;

    void Update()
    {
        // 1. Safety Lock: Stop if finished or if the target area (fairy effect) isn't active yet
        if (isFinished || GameManager.eGameStatus != GameManager.GameState.Prophase) return;

        if (targetAreaObject == null || !targetAreaObject.activeInHierarchy)
        {
            // If the fairy effect isn't visible/active yet, reset everything and wait
            ResetProgress();
            return;
        }

        // 2. Check Input: Is either hand pinching?
        bool isPinching = (leftHand != null && leftHand.isGrabbed_left) ||
                          (rightHand != null && rightHand.isGrabbed_right);

        // 3. Check Location: Distance to the fairy effect
        float distance = Vector3.Distance(transform.position, targetAreaObject.transform.position);
        bool isInArea = distance < areaRadius;

        if (isPinching && isInArea)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / targetTime);

            if (sliderImg) sliderImg.fillAmount = progress;
            if (dnaAnimator) dnaAnimator.SetFloat("CondenseProgress", progress);

            if (timer >= targetTime)
            {
                FinishImmediately();
            }
        }
        else
        {
            ResetProgress();
        }
    }

    void ResetProgress()
    {
        timer = 0;
        if (sliderImg) sliderImg.fillAmount = 0;
        if (dnaAnimator) dnaAnimator.SetFloat("CondenseProgress", 0);
    }

    void FinishImmediately()
    {
        isFinished = true;
        LogEventHelper.LogDNACondensed();

        // INSTANT DISAPPEARANCE: Old DNA goes away first
        if (prophaseDNA != null)
        {
            prophaseDNA.SetActive(false);
        }

        // TRIGGER THE NEXT STEP: Metaphase
        GameManager gm = Object.FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            gm.Metaphase();
        }
    }
}