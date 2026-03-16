using UnityEngine;

public class ChromosomeManager : MonoBehaviour
{
    [Header("Linked Chromatids")]
    public Transform chromosome1;
    public Transform chromosome2;

    [Header("Motion Thresholds")]
    public float positionEpsilon = 0.000001f;
    public float rotationEpsilonDegrees = 0.01f;

    private Vector3 lastPos1;
    private Vector3 lastPos2;
    private Quaternion lastRot1;
    private Quaternion lastRot2;
    private bool initialized;

    void OnEnable()
    {
        TryInitialize();
    }

    void Start()
    {
        TryInitialize();
    }

    void LateUpdate()
    {
        if (!initialized)
        {
            TryInitialize();
            if (!initialized)
                return;
        }

        Vector3 deltaPos1 = chromosome1.position - lastPos1;
        Vector3 deltaPos2 = chromosome2.position - lastPos2;
        float deltaRot1 = Quaternion.Angle(lastRot1, chromosome1.rotation);
        float deltaRot2 = Quaternion.Angle(lastRot2, chromosome2.rotation);

        bool moved1 = deltaPos1.sqrMagnitude > positionEpsilon || deltaRot1 > rotationEpsilonDegrees;
        bool moved2 = deltaPos2.sqrMagnitude > positionEpsilon || deltaRot2 > rotationEpsilonDegrees;

        if (moved1 || moved2)
        {
            if (moved1 && !moved2)
            {
                ApplyDriverDelta(chromosome1, chromosome2, lastPos1, lastRot1);
            }
            else if (moved2 && !moved1)
            {
                ApplyDriverDelta(chromosome2, chromosome1, lastPos2, lastRot2);
            }
            else
            {
                float score1 = deltaPos1.sqrMagnitude + deltaRot1 * deltaRot1 * 0.0001f;
                float score2 = deltaPos2.sqrMagnitude + deltaRot2 * deltaRot2 * 0.0001f;
                if (score1 >= score2)
                    ApplyDriverDelta(chromosome1, chromosome2, lastPos1, lastRot1);
                else
                    ApplyDriverDelta(chromosome2, chromosome1, lastPos2, lastRot2);
            }
        }

        lastPos1 = chromosome1.position;
        lastPos2 = chromosome2.position;
        lastRot1 = chromosome1.rotation;
        lastRot2 = chromosome2.rotation;
    }

    private void TryInitialize()
    {
        initialized = chromosome1 != null && chromosome2 != null;
        if (!initialized)
            return;

        lastPos1 = chromosome1.position;
        lastPos2 = chromosome2.position;
        lastRot1 = chromosome1.rotation;
        lastRot2 = chromosome2.rotation;
    }

    // Apply the driver's rigid-body-like delta to the follower in world space.
    private static void ApplyDriverDelta(Transform driver, Transform follower, Vector3 prevDriverPos, Quaternion prevDriverRot)
    {
        Quaternion deltaRot = driver.rotation * Quaternion.Inverse(prevDriverRot);
        Vector3 followerRelative = follower.position - prevDriverPos;
        follower.position = driver.position + (deltaRot * followerRelative);
        follower.rotation = deltaRot * follower.rotation;
    }
}