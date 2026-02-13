using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpindleFiber_2 : MonoBehaviour
{
    public GameManager gameManager;

    [SerializeField] GameObject target;

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = this.GetComponent<LineRenderer>();
    }

    void Update()
    {
        // Only draw if the stage is Anaphase or Telophase, or if we are in Metaphase but it's already triggered.
        // The LineRenderer component itself should be disabled by default or in Prophase.
        if (GameManager.eGameStatus == GameManager.GameState.Anaphase || GameManager.eGameStatus == GameManager.GameState.Telophase || 
           (GameManager.eGameStatus == GameManager.GameState.Metaphase && lineRenderer.enabled))
        {
            lineRenderer.enabled = true; // Ensure it stays on
            
            // Yellowish-green color and thin width as requested
            lineRenderer.material.color = new Color(0.7f, 1f, 0f, 1f);
            lineRenderer.startWidth = 0.015f;
            lineRenderer.endWidth = 0.015f;

            lineRenderer.SetPosition(0, this.transform.position);
            lineRenderer.SetPosition(1, target.transform.position);
        }
    }
}
