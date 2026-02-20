using System;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Samples.GestureSample;

public class DualHandMotionGesture : MonoBehaviour
{
    [Header("Hand Poses (Static parts)")]
    public StaticHandGesture leftHandPose;
    public StaticHandGesture rightHandPose;

    [Header("Motion Settings")]
    public float minDistance = 0.25f; // Increased for easier detection
    public float minVelocity = 0.5f; 
    
    [Header("Events")]
    public UnityEngine.Events.UnityEvent onGesturePerformed;

    [Header("Gesture Config")]
    public bool requirePoses = true; // Added to allow relaxing the gesture for users
    public float releaseDistanceOffset = 0.05f;

    private bool m_IsGestureActive = false;
    public bool isGestureActive => m_IsGestureActive;
    public float currentDistance { get; private set; }

    private float m_NextLogTime = 0f;

    void Update()
    {
        try 
        {
            // Safety Checks
            if (leftHandPose == null || rightHandPose == null) return;
            if (leftHandPose.handTrackingEvents == null || rightHandPose.handTrackingEvents == null) return;

            // If either hand is not tracked, reset and return
            if (!leftHandPose.handTrackingEvents.handIsTracked || !rightHandPose.handTrackingEvents.handIsTracked)
            {
                if (m_IsGestureActive) Debug.Log("[DualHandMotion] Tracking lost, resetting gesture.");
                m_IsGestureActive = false;
                currentDistance = 100f;
                return;
            }

            // Check if BOTH hands are in the correct static shape
            bool posesCorrect = !requirePoses || (leftHandPose.isPerformed && rightHandPose.isPerformed);

            // Distance between palms (wrists)
            Vector3 leftPalm = leftHandPose.handTrackingEvents.rootPose.position;
            Vector3 rightPalm = rightHandPose.handTrackingEvents.rootPose.position;
            float distance = Vector3.Distance(leftPalm, rightPalm);
            currentDistance = distance;

            // Logging for debug - throttled to every 1 second
            if (Time.time > m_NextLogTime)
            {
                string poseStatus = requirePoses ? $"(L:{leftHandPose.isPerformed} R:{rightHandPose.isPerformed})" : "(Poses ignored)";
                Debug.Log($"[DualHandMotion] Correct: {posesCorrect} {poseStatus}, Dist: {distance:F2}m (target < {minDistance}m)");
                m_NextLogTime = Time.time + 1.0f;
            }

            // Logic for the gesture trigger
            if (posesCorrect && distance < minDistance && !m_IsGestureActive)
            {
                m_IsGestureActive = true;
                if (onGesturePerformed != null)
                    onGesturePerformed.Invoke();
                
                Debug.Log("[DualHandMotion] Gesture Triggered!");
            }
            else if (distance > minDistance + releaseDistanceOffset)
            {
                m_IsGestureActive = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DualHandMotionGesture] Error in Update: {e.Message}\n{e.StackTrace}");
        }
    }
}
