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

    public bool proPhase = false;

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
    }

    public void GameEnd()
    {
        eGameStatus = GameState.GameOver;
        onGameOver.Invoke();
        Debug.Log("Game_End");
    }
}
