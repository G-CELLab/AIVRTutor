using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleEffectActivate : MonoBehaviour
{
    float waitingTime = 8f;
    float timer = 0f;
    bool isDone = false;

    public GameManager gameManager;
    public GameObject particleEffect;

    void Start()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && GameManager.eGameStatus == GameManager.GameState.Metaphase)
        {
            Debug.Log("Meta_Chromosome_Entered");
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && GameManager.eGameStatus == GameManager.GameState.Metaphase)
        {
            Debug.Log("Meta_Chromosome_Exited");
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= waitingTime && isDone == false)
        {
            particleEffect.SetActive(true);
            timer = 0f;
            isDone = true;
        }
    }
}
