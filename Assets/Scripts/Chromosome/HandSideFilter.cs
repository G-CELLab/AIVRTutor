using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class HandSideFilter : MonoBehaviour
{
    public enum HandSide { Left, Right }
    public HandSide allowedSide;

    private XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Check the interactor's name and its parents' names
        bool isLeftHand = IsObjectInSide(args.interactorObject.transform, "left");
        bool isRightHand = IsObjectInSide(args.interactorObject.transform, "right");

        bool isValid = (allowedSide == HandSide.Left && isLeftHand) || (allowedSide == HandSide.Right && isRightHand);

        if (!isValid)
        {
            Debug.Log($"Wrong Hand! {gameObject.name} can only be grabbed by the {allowedSide} hand. Detected: L:{isLeftHand} R:{isRightHand}");
            // Force release immediately
            args.manager.SelectExit(args.interactorObject, grabInteractable);
        }
    }

    private bool IsObjectInSide(Transform t, string side)
    {
        while (t != null)
        {
            if (t.name.ToLower().Contains(side)) return true;
            t = t.parent;
        }
        return false;
    }
}
