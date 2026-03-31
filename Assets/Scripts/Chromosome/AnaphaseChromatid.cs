using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class AnaphaseChromatid : MonoBehaviour
{
    public enum HandRequirement { Left, Right }
    public HandRequirement requiredHand;
    
    private XRGrabInteractable grabInteractable;
    private Vector3 startPosition;
    private bool hasReachedPole = false;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        startPosition = transform.position;
    }

    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabEnter);
    }

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabEnter);
    }

    private void OnGrabEnter(SelectEnterEventArgs args)
    {
        // Identify the hand by the name of the interactor or its parent
        string interactorName = args.interactorObject.transform.name.ToLower();
        bool isLeft = interactorName.Contains("left");
        bool isRight = interactorName.Contains("right");

        if (requiredHand == HandRequirement.Left && !isLeft)
        {
            Debug.Log("Left Chromatid can only be grabbed by the Left Hand.");
            CancelGrab(args);
        }
        else if (requiredHand == HandRequirement.Right && !isRight)
        {
            Debug.Log("Right Chromatid can only be grabbed by the Right Hand.");
            CancelGrab(args);
        }
    }

    private void CancelGrab(SelectEnterEventArgs args)
    {
        // Force the interactor to drop the object
        args.manager.SelectExit(args.interactorObject, grabInteractable);
    }

    void Update()
    {
        if (GameManager.eGameStatus != GameManager.GameState.Anaphase) return;

        // Track progress: pulling to poles
        if (!hasReachedPole)
        {
            float pullDistance = Mathf.Abs(transform.position.x - startPosition.x);
            if (pullDistance > 2.0f)
            {
                hasReachedPole = true;
                Debug.Log(gameObject.name + " reached the pole!");
                CheckStageCompletion();
            }
        }
    }

    void CheckStageCompletion()
    {
        // Simple check: are both chromatids at poles?
        AnaphaseChromatid[] allChromatids = Object.FindObjectsByType<AnaphaseChromatid>(FindObjectsInactive.Exclude);
        int reachedCount = 0;
        foreach (var c in allChromatids)
        {
            if (c.hasReachedPole) reachedCount++;
        }

        if (reachedCount >= 2)
        {
            Debug.Log("Anaphase Complete! Moving to Telophase.");
            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            if (gm != null) gm.Telophase();
        }
    }
}