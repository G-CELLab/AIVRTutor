using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RightHandManager : MonoBehaviour
{
    public bool isGrabbed_right = false;
    
    [SerializeField] 
    private NearFarInteractor handInteractor;

    void Update()
    {
        if (handInteractor != null)
        {
            // Sync position/rotation to the actual tracked hand interactor
            transform.position = handInteractor.transform.position;
            transform.rotation = handInteractor.transform.rotation;

            // XRI handles the pinch gesture; hasSelection is true when grabbing
            isGrabbed_right = handInteractor.hasSelection;
        }
    }
}