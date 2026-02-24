using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RightHandTouching : MonoBehaviour
{
    private HashSet<Collider> activeColliders = new HashSet<Collider>();
    private Collider priorityCollider;
    private string currentObjectName = "";

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
        activeColliders.RemoveWhere(c => c == null);
        
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
                TouchTracker.SetRightTouchObject(bestCollider.name);
            }
            else
            {
                currentObjectName = "";
                TouchTracker.ClearRightTouchObject();
            }
        }
    }
}
