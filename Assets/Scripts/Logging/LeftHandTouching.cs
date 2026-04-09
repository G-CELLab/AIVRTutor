using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeftHandTouching : MonoBehaviour
{
    [SerializeField] private float clearTouchGraceSeconds = 0.2f;

    private HashSet<Collider> activeColliders = new HashSet<Collider>();
    private Collider priorityCollider;
    private string currentObjectName = "";
    private float noTouchTimer = 0f;

    private void Update()
    {
        if (activeColliders.Count == 0)
        {
            if (priorityCollider != null)
            {
                noTouchTimer += Time.deltaTime;
                if (noTouchTimer >= clearTouchGraceSeconds)
                    ClearCurrentTouch();
            }
            return;
        }

        noTouchTimer = 0f;

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
        noTouchTimer = 0f;
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
            noTouchTimer = 0f;
            UpdatePriorityCollider();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        activeColliders.Remove(other);

        if (activeColliders.Count > 0)
        {
            noTouchTimer = 0f;
            UpdatePriorityCollider();
        }
        else
        {
            noTouchTimer = 0f;
        }
    }

    private bool IsColliderStale(Collider collider)
    {
        return collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy;
    }

    private void ResetTouchState()
    {
        activeColliders.Clear();
        noTouchTimer = 0f;
        ClearCurrentTouch();
    }

    private void ClearCurrentTouch()
    {
        priorityCollider = null;
        currentObjectName = "";
        TouchTracker.ClearLeftTouchObject();
    }
    
    private bool ShouldIgnore(Collider other)
    {
        if (other == null)
            return true;

        string lowerName = other.name.ToLowerInvariant();
        if (lowerName.Contains("boundry") || lowerName.Contains("boundary"))
            return true;

        // Never log left/right detector overlap as touched gameplay objects.
        if (other.GetComponentInParent<LeftHandTouching>() != null ||
            other.GetComponentInParent<RightHandTouching>() != null)
            return true;

        return other.name == "TopPlatform" || 
               other.CompareTag("Left") || 
               other.CompareTag("Right") || 
               other.transform.root.CompareTag("Left") ||
               other.transform.root.CompareTag("Right") ||
               other.name.Contains("Hand");
    }
    
    private void UpdatePriorityCollider()
    {
        // Clean up null references
        activeColliders.RemoveWhere(IsColliderStale);

        Collider bestCollider = SelectBestCollider();
        
        // Hold last touch during short transitions; clear is handled by grace timer.
        if (bestCollider == null)
            return;

        // Update if changed
        if (priorityCollider != bestCollider)
        {
            priorityCollider = bestCollider;

            currentObjectName = GetColliderLogName(bestCollider);
            TouchTracker.SetLeftTouchObject(currentObjectName);
        }
    }

    private Collider SelectBestCollider()
    {
        Collider bestCollider = null;
        int bestScore = int.MinValue;

        foreach (var collider in activeColliders)
        {
            if (collider == null || ShouldIgnore(collider))
                continue;

            int score = GetColliderPriority(collider);
            if (score > bestScore)
            {
                bestScore = score;
                bestCollider = collider;
            }
        }

        return bestCollider;
    }

    private int GetColliderPriority(Collider collider)
    {
        if (collider == null)
            return int.MinValue;

        // Nutrients should win when overlapping larger organelle colliders.
        if (collider.CompareTag("Food") || collider.CompareTag("Food2") || collider.CompareTag("Food3"))
            return 1000;

        string lower = collider.name.ToLowerInvariant();
        if (lower.Contains("vitamin") || lower.Contains("protein") || lower.Contains("magnesium"))
            return 900;

        if (lower.Contains("mitochond"))
            return 100;

        return 500;
    }

    private string GetColliderLogName(Collider collider)
    {
        if (collider == null)
            return "";

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag("Food"))
                return "Protein";
            if (current.CompareTag("Food2"))
                return "Magnesium";
            if (current.CompareTag("Food3"))
                return "VitaminC";

            string lower = current.name.ToLowerInvariant();
            if (lower.Contains("protein"))
                return "Protein";
            if (lower.Contains("magnesium"))
                return "Magnesium";
            if (lower.Contains("vitamin"))
                return "VitaminC";

            current = current.parent;
        }

        return collider.name;
    }
}
