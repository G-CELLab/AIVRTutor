using UnityEngine;

/// <summary>
/// Tracks what objects the left and right hands are currently touching.
/// Hooks into LeftHandTouching and RightHandTouching colliders.
/// </summary>
public class TouchTracker : MonoBehaviour
{
    private static string currentLeftTouchObject = "";
    private static string currentRightTouchObject = "";

    public static string GetLeftTouchObject()
    {
        return currentLeftTouchObject;
    }

    public static string GetRightTouchObject()
    {
        return currentRightTouchObject;
    }

    public static void SetLeftTouchObject(string objectName)
    {
        currentLeftTouchObject = objectName;
    }

    public static void SetRightTouchObject(string objectName)
    {
        currentRightTouchObject = objectName;
    }

    public static void ClearLeftTouchObject()
    {
        currentLeftTouchObject = "";
    }

    public static void ClearRightTouchObject()
    {
        currentRightTouchObject = "";
    }
}
