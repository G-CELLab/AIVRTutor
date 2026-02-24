using UnityEngine;

/// <summary>
/// Helper class for logging special events to the MainLogger.
/// Other scripts can call these methods when specific events occur.
/// </summary>
public class LogEventHelper : MonoBehaviour
{
    public static void LogTriggerEnterWound()
    {
        MainLogger.LogOtherEvent("Trigger_Enter_Wound");
        Debug.Log("[LogEventHelper] Wound entered");
    }

    public static void LogTriggerExitWound()
    {
        MainLogger.LogOtherEvent("Trigger_Exit_Wound");
        Debug.Log("[LogEventHelper] Wound exited");
    }

    public static void LogATPCharged()
    {
        MainLogger.LogOtherEvent("ATP_Charged");
        Debug.Log("[LogEventHelper] ATP charged");
    }

    public static void LogCentrioleMoved()
    {
        MainLogger.LogOtherEvent("Centriole_Moved_To_Edges");
        Debug.Log("[LogEventHelper] Centriole moved to edges");
    }

    public static void LogDNACondensed()
    {
        MainLogger.LogOtherEvent("DNA_Condensed");
        Debug.Log("[LogEventHelper] DNA condensed");
    }

    public static void LogCustomEvent(string eventName)
    {
        MainLogger.LogOtherEvent(eventName);
        Debug.Log("[LogEventHelper] Event logged: " + eventName);
    }
}
