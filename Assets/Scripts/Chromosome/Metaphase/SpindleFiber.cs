using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpindleFiber : MonoBehaviour
{
    public GameManager gameManager;

    [SerializeField] GameObject target;

    private LineRenderer lineRenderer;
    float timer = 0f;
    void Start()
    {
        lineRenderer = this.GetComponent<LineRenderer>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Only update positions if the stage is Anaphase or Telophase, or if we are in Metaphase and the LineRenderer was enabled by Centrosome
        if (GameManager.eGameStatus == GameManager.GameState.Anaphase || GameManager.eGameStatus == GameManager.GameState.Telophase || 
           (GameManager.eGameStatus == GameManager.GameState.Metaphase && lineRenderer.enabled))
        {
            lineRenderer.enabled = true;
            // Yellowish-green color and thin width as requested
            lineRenderer.material.color = new Color(0.7f, 1f, 0f, 1f); 
            lineRenderer.startWidth = 0.015f;
            lineRenderer.endWidth = 0.015f;
            
            lineRenderer.SetPosition(0, this.transform.position + new Vector3(0, 0.13f, 0));
            lineRenderer.SetPosition(1, target.transform.position);
        }
        else if (GameManager.eGameStatus != GameManager.GameState.Metaphase)
        {
            // Only disable if NOT in Metaphase (let Centrosome control Metaphase state)
            lineRenderer.enabled = false;
        }
    }
}
