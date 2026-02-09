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

        if (GameManager.eGameStatus == GameManager.GameState.Metaphase || GameManager.eGameStatus == GameManager.GameState.Anaphase || GameManager.eGameStatus == GameManager.GameState.Telophase)
        {
            lineRenderer.material.color = Color.yellow;
            lineRenderer.SetPosition(0, this.transform.position + new Vector3(0, 0.13f, 0));
            lineRenderer.SetPosition(1, target.transform.position);
        }
    }
}
