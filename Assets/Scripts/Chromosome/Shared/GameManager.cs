using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Intro,
        Interphase,
        InterphasePart2,
        Prophase,
        Metaphase,
        Anaphase,
        Telophase,
        ResetGame,
        GameOver
    }
    public static GameState eGameStatus;

    //public delegate void AsteroidHandler();
    //public static event AsteroidHandler AsteroidDestroyed;

    public UnityEvent onIntro;
    public UnityEvent onInterphase;
    public UnityEvent onInterphasePart2;
    public UnityEvent onProphase;
    public UnityEvent onMetaphase;
    public UnityEvent onAnaphase;
    public UnityEvent onTelophase;
    public UnityEvent onGameReset;
    public UnityEvent onGameOver;

    public bool proPhase = true;

    [Header("The Slider Components")]
    public Image ATPsliderImg;
    public Image HPsliderImg;

    public static int playerScore = 0;

    public void FoodCollision()
    {
        Debug.Log("ATP_Charged");
        ATPsliderImg.fillAmount += 0.3f;
    }

    public void CellDivided()
    {
        Debug.Log("HP_Charged");
        HPsliderImg.fillAmount += 0.3f;
    }

    private void Start()
    {
        if(ScoreManager.HPtracking < 0.4f)
        {
            eGameStatus = GameState.Intro;
            onIntro.Invoke();
            Debug.Log("Game_Started");
        }
        else if (ScoreManager.HPtracking > 0.4f && ScoreManager.HPtracking < 0.7f)
        {
            eGameStatus = GameState.Interphase;
            onInterphase.Invoke();
            Debug.Log("Game_Started_2");
        }
        else if (ScoreManager.HPtracking > 0.7f)
        {
            eGameStatus = GameState.Interphase;
            onInterphase.Invoke();
            Debug.Log("Game_Started_3");
        }

    }

    public void Intro()
    {
        eGameStatus = GameState.Intro;
        onIntro.Invoke();
        Debug.Log("Intro");
    }

    public void Interphase()
    {
        eGameStatus = GameState.Interphase;
        onInterphase.Invoke();
        Debug.Log("Interphase");
    }

    public void InterphasePart2()
    {
        eGameStatus = GameState.InterphasePart2;
        onInterphasePart2.Invoke();
        Debug.Log("Interphase Part 2");
    }

    public void Prophase()
    {
        eGameStatus = GameState.Prophase;
        onProphase.Invoke();
        proPhase = true;
        Debug.Log("Prophase");
    }

    public void Metaphase()
    {
        eGameStatus = GameState.Metaphase;
        onMetaphase.Invoke();
        Debug.Log("Metaphase");
    }

    public void Anaphase()
    {
        eGameStatus = GameState.Anaphase;
        onAnaphase.Invoke();
        Debug.Log("Anaphase");
    }

    public void Telophase()
    {
        eGameStatus = GameState.Telophase;
        onTelophase.Invoke();
        Debug.Log("Telophase");

        // Rigorous cleanup of previous phase objects
        CleanupPreviousPhases();
    }

    private void CleanupPreviousPhases()
    {
        // Disable individual objects by tag if they are in the scene
        string[] tagsToDisable = { "Centrosome1", "Centrosome2", "Meta", "Pro", "Finish" };
        foreach (string tag in tagsToDisable)
        {
            GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);
            foreach (var obj in objs)
            {
                obj.SetActive(false);
            }
        }

        // We use Resources.FindObjectsOfTypeAll to find objects even if they are already partially disabled
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in allObjects)
        {
            // Disable the DNA and any remaining chromatids
            if (obj.name == "Player_DNA" || obj.name == "P_Chromotid_L " || obj.name == "P_Chromotid_R")
            {
                // Only disable objects that are actually in a scene (not prefabs in the project)
                if (obj.scene.name != null) 
                {
                    obj.SetActive(false);
                }
            }
        }
    }

    public void GameEnd()
    {
        eGameStatus = GameState.GameOver;
        onGameOver.Invoke();
        Debug.Log("Game_End");
    }
}
