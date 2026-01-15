using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class DubugDisplay : MonoBehaviour
{
    Dictionary<string, string> debugLogs = new Dictionary<string, string>();

    [SerializeField] GameObject player;

    public Text display;

    //private double newX = 0f;


    private void Start()
    {
        //newX = player.transform.position.x - 70.098;
    }
    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }


    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Log)
        {
            display.text = logString;
            //display.text += Application.persistentDataPath;
            //display.text += player.transform.position.x;
        }
    }

}
