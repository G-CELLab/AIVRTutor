using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeftHandTouching : MonoBehaviour
{
    private HashSet<Collider> activeColliders = new HashSet<Collider>();
    private Collider priorityCollider;
    private string currentObjectName = "";

    private void Update()
    {
        if (activeColliders.Count == 0)
            return;

        bool removedStale = activeColliders.RemoveWhere(IsColliderStale) > 0;
        bool priorityIsStale = IsColliderStale(priorityCollider);

        if (removedStale || priorityIsStale)
            UpdatePriorityCollider();
    }

    private void OnDisable()
    {
        ResetTouchState();
    }

    private void OnDestroy()
    {
        ResetTouchState();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore platform and hand-to-hand collisions
        if (ShouldIgnore(other))
            return;
            
        activeColliders.Add(other);
        UpdatePriorityCollider();
    }

    private void OnTriggerStay(Collider other)
    {
        // Ignore platform and hand-to-hand collisions  
        if (ShouldIgnore(other))
            return;

        // Ensure it's in the active set
        if (!activeColliders.Contains(other))
        {
            activeColliders.Add(other);
            UpdatePriorityCollider();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        activeColliders.Remove(other);
        UpdatePriorityCollider();
    }

    private bool IsColliderStale(Collider collider)
    {
        return collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy;
    }

    private void ResetTouchState()
    {
        activeColliders.Clear();
        priorityCollider = null;
        currentObjectName = "";
        TouchTracker.ClearLeftTouchObject();
    }
    
    private bool ShouldIgnore(Collider other)
    {
        return other.name == "TopPlatform" || 
               other.CompareTag("Left") || 
               other.CompareTag("Right") || 
               other.name.Contains("Hand");
    }
    
    private void UpdatePriorityCollider()
    {
        // Clean up null references  
        activeColliders.RemoveWhere(IsColliderStale);
        
        // Find highest priority collider (non-platform, non-hand objects)
        Collider bestCollider = null;
        foreach (var collider in activeColliders)
        {
            if (collider != null && !ShouldIgnore(collider))
            {
                bestCollider = collider;
                break;
            }
        }
        
        // Update if changed
        if (priorityCollider != bestCollider)
        {
            priorityCollider = bestCollider;
            
            if (bestCollider != null)
            {
                currentObjectName = bestCollider.name;
                TouchTracker.SetLeftTouchObject(bestCollider.name);
            }
            else
            {
                currentObjectName = "";
                TouchTracker.ClearLeftTouchObject();
            }
        }
    }
}
