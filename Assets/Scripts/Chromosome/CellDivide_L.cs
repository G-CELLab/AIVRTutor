using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellDivide_L : MonoBehaviour
{
    public GameManager gameManager;
    public Transform endLocation;
    private float timer = 0f;
    bool isDone = false;
    private float startDelay = 3.0f;

    Vector3 scaleChange;

    private void Start()
    {
        scaleChange = new Vector3(-0.0001f, -0.0001f, -0.0001f);
    }

    void Update()
    {
        if (isDone == false) //GameManager.eGameStatus == GameManager.GameState.Telophase && 
        {
            timer += Time.deltaTime;
            if (timer > startDelay)
            {
                transform.position = Vector3.Lerp(this.transform.position, endLocation.position, 0.01f);
                this.transform.localScale += scaleChange;

                if (this.transform.localScale.y < 0.05f)
                {
                    scaleChange = new Vector3(0f, 0f, 0f);
                }

                if (timer > 10f)
                {
                    timer = 0f;
                    isDone = true;
                    gameManager.CellDivided();
                }
            }
        }
    }
}
