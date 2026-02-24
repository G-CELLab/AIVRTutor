using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class LeftHandManager : MonoBehaviour
{
    public bool isGrabbed_left = false;
    
    [SerializeField] 
    private NearFarInteractor handInteractor;

    void Start()
    {
        // Auto-find NearFarInteractor if not assigned
        if (handInteractor == null)
        {
            handInteractor = GetComponentInChildren<NearFarInteractor>();
            if (handInteractor == null)
            {
                Debug.LogWarning("[LeftHandManager] NearFarInteractor not found in children!");
            }
        }
    }

    void Update()
    {
        if (handInteractor != null)
        {
            // Sync position/rotation to the actual tracked hand interactor
            transform.position = handInteractor.transform.position;
            transform.rotation = handInteractor.transform.rotation;

            // XRI handles the pinch gesture; hasSelection is true when grabbing
            isGrabbed_left = handInteractor.hasSelection;
        }
    }
}