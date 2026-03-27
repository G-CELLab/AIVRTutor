using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

/// <summary>
/// Listens to Meta system gestures (meta button/palm menu) and resets the player to starting position.
/// This counteracts the default OpenXR recenter behavior which resets at current position instead of returning to spawn point.
/// Compatible with MetaSystemGestureDetector from XRI samples.
/// </summary>
public class MetaGestureStartPositionResetter : MonoBehaviour
{
    [SerializeField] private bool enableMetaResetOnSystemGesture = true;
    [SerializeField] private float resetDelaySeconds = 0.1f;

    private XRStartupSpawnAligner _spawnAligner;
    private MetaSystemGestureDetector _gestureDetector;
    private bool _isSystemGestureActive = false;

    private void OnEnable()
    {
        _spawnAligner = FindAnyObjectByType<XRStartupSpawnAligner>();
        _gestureDetector = FindAnyObjectByType<MetaSystemGestureDetector>();

        if (_gestureDetector != null && enableMetaResetOnSystemGesture)
        {
            _gestureDetector.systemGestureStarted.AddListener(OnSystemGestureStarted);
            _gestureDetector.systemGestureEnded.AddListener(OnSystemGestureEnded);
        }
    }

    private void OnDisable()
    {
        if (_gestureDetector != null)
        {
            _gestureDetector.systemGestureStarted.RemoveListener(OnSystemGestureStarted);
            _gestureDetector.systemGestureEnded.RemoveListener(OnSystemGestureEnded);
        }
    }

    private void OnSystemGestureStarted()
    {
        _isSystemGestureActive = true;
    }

    private void OnSystemGestureEnded()
    {
        if (_isSystemGestureActive && _spawnAligner != null)
        {
            _isSystemGestureActive = false;
            StartCoroutine(ResetAfterDelay());
        }
    }

    private IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(resetDelaySeconds);
        if (_spawnAligner != null)
        {
            _spawnAligner.ResetToStartingPosition();
        }
    }
}
