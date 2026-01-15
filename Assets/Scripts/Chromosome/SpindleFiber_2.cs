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
        if (GameManager.eGameStatus == GameManager.GameState.Metaphase || GameManager.eGameStatus == GameManager.GameState.Anaphase || GameManager.eGameStatus == GameManager.GameState.Telophase)
        {
            lineRenderer.material.color = Color.yellow;
            lineRenderer.SetPosition(0, this.transform.position);
            lineRenderer.SetPosition(1, target.transform.position);
        }
    }
}
