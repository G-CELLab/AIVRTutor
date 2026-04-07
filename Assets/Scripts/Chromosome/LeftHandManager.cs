using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class LeftHandManager : MonoBehaviour
{
    [Header("State")]
    public bool isGrabbed_left = false;

    [Header("Interactor Source")]
    [SerializeField]
    private NearFarInteractor handInteractor;

    [Header("Fist Settings")]
    [SerializeField]
    private float fistThreshold = 0.08f; 

    private XRHandSubsystem handSubsystem;
    private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
    private XROrigin xrOrigin;

    private Transform visualPalm;
    private Vector3 lastValidWorldPos;
    private Quaternion lastValidWorldRot;
    private bool hasEverBeenTracked = false;

    void Start()
    {
        xrOrigin = Object.FindAnyObjectByType<XROrigin>();
        EnsureInteractorReference();
        ConfigureInteractorInput();
        FindVisualPalm();
        
        lastValidWorldPos = transform.position;
        lastValidWorldRot = transform.rotation;
    }

    private void ConfigureInteractorInput()
    {
        if (handInteractor != null)
        {
            handInteractor.selectInput.inputSourceMode = (UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputButtonReader.InputSourceMode)4;
            handInteractor.attachTransform = null;
            
            foreach (Transform child in handInteractor.transform)
            {
                if (child.name.Contains("Select Input") || child.name.Contains("UI Press Input"))
                    child.gameObject.SetActive(false);
            }
        }
    }

    private void FindVisualPalm()
    {
        if (transform.parent != null)
        {
            var allChildren = transform.parent.GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                if (child.name == "L_Palm" && child.gameObject.activeInHierarchy)
                {
                    visualPalm = child;
                    break;
                }
            }
        }
    }

    void Update()
    {
        EnsureInteractorReference();

        if (TryGetPalmWorldPose(out Vector3 pPos, out Quaternion pRot))
        {
            lastValidWorldPos = pPos;
            lastValidWorldRot = pRot;
            hasEverBeenTracked = true;
        }
        else if (visualPalm != null && visualPalm.gameObject.activeInHierarchy)
        {
            lastValidWorldPos = visualPalm.position;
            lastValidWorldRot = visualPalm.rotation;
            hasEverBeenTracked = true;
        }

        if (hasEverBeenTracked)
        {
            transform.position = lastValidWorldPos;
            transform.rotation = lastValidWorldRot;
            
            if (handInteractor != null)
            {
                handInteractor.transform.position = transform.position;
                handInteractor.transform.rotation = transform.rotation;
            }
        }

        bool gestureActive = EvaluateClosedFist();
        UpdateXRISelection(gestureActive);

        isGrabbed_left = gestureActive;
    }

    private void UpdateXRISelection(bool active)
    {
        if (handInteractor == null || !handInteractor.enabled || !handInteractor.gameObject.activeInHierarchy)
            return;

        if (active && !handInteractor.hasSelection)
        {
            var targets = handInteractor.interactablesHovered;
            if (targets != null && targets.Count > 0)
            {
                var target = targets[0] as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
                if (target != null && target.IsSelectableBy(handInteractor))
                {
                    handInteractor.StartManualInteraction(target);
                }
            }
        }
        else if (!active && handInteractor.hasSelection)
        {
            handInteractor.EndManualInteraction();
        }
    }

    private void EnsureInteractorReference()
    {
        if (handInteractor != null && handInteractor.gameObject.activeInHierarchy) return;
        handInteractor = GetComponentInChildren<NearFarInteractor>(true);
        if (handInteractor == null && transform.parent != null)
            handInteractor = transform.parent.GetComponentInChildren<NearFarInteractor>(true);
    }

    private bool TryGetPalmWorldPose(out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        if (handSubsystem == null || !handSubsystem.running) TryFindHandSubsystem();
        if (handSubsystem != null && handSubsystem.running && handSubsystem.leftHand.isTracked)
        {
            var palm = handSubsystem.leftHand.GetJoint(XRHandJointID.Palm);
            if (palm.TryGetPose(out var pose))
            {
                if (xrOrigin != null)
                {
                    pos = xrOrigin.transform.TransformPoint(pose.position);
                    rot = xrOrigin.transform.rotation * pose.rotation;
                }
                else { pos = pose.position; rot = pose.rotation; }
                return true;
            }
        }
        return false;
    }

    private bool EvaluateClosedFist()
    {
        if (handSubsystem == null || !handSubsystem.running) TryFindHandSubsystem();
        if (handSubsystem == null || !handSubsystem.running) return false;

        XRHand hand = handSubsystem.leftHand;
        if (!hand.isTracked) return false;

        var palm = hand.GetJoint(XRHandJointID.Palm);
        if (!palm.TryGetPose(out var pPose)) return false;

        int curled = 0;
        XRHandJointID[] tips = { XRHandJointID.IndexTip, XRHandJointID.MiddleTip, XRHandJointID.RingTip, XRHandJointID.LittleTip };
        foreach (var id in tips)
        {
            if (hand.GetJoint(id).TryGetPose(out var tipPose))
            {
                if (Vector3.Distance(pPose.position, tipPose.position) <= fistThreshold)
                    curled++;
            }
        }

        // Deliberate Closed Fist: at least 3 fingers must be curled. 
        // Instant release if fewer than 3.
        return curled >= 3;
    }

    private void TryFindHandSubsystem()
    {
        SubsystemManager.GetSubsystems(s_Subsystems);
        foreach (var s in s_Subsystems) { if (s.running) { handSubsystem = s; break; } }
    }
}