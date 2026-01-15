using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;

public class WriteDebugToFile : MonoBehaviour
{
    string filename = "";
    float timer = 0.0f;
    public Image HPsliderImg;
    
    public static int round = 1;

    void OnEnable()
    {
        Application.logMessageReceived += Log;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= Log;
    }

    void Start()
    {
        if (round == 1)
        {
            filename = Application.persistentDataPath + "/Logfile1.csv";
        }
        else if (round == 2)
        {
            filename = Application.persistentDataPath + "/Logfile2.csv";
        }
        else if (round == 3)
        {
            filename = Application.persistentDataPath + "/Logfile3.csv";
        }
        else if (round == 4)
        {
            filename = Application.persistentDataPath + "/Logfile4.csv";
        }
        //Debug.Log(filename);
        WriteHeader();
    }

    void Update()
    {         
        Timer();
    }

    public void WriteHeader()
    {
        TextWriter tw = new StreamWriter(filename, false);
        tw.WriteLine("Time, Right_Pressed, Left_Pressed, Right_Grabbed, Left_Grabbed, Right_Touched, Left_Touched, InfoPanel, Phase, Others");
        tw.Close();
    }


    public void Log(string logString, string stackTrace, LogType type)
    {
            if (type == LogType.Log && logString.Contains("RightGrab"))
            {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftGrab"))
            {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("RightHand"))
            {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftHand"))
            {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("RightTouch") && !logString.Contains("rotation") && !logString.Contains("bone") && !logString.Contains("Prefab"))
        {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftTouch") && !logString.Contains("rotation") && !logString.Contains("bone") && !logString.Contains("Prefab"))
            {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("InfoPanel"))
            {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("phase"))
            {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && !logString.Contains("phase") && !logString.Contains("L_Grb_") && !logString.Contains("R_Grb_") && !logString.Contains("Left") && !logString.Contains("Right") && !logString.Contains("InfoPanel") && !logString.Contains("Touch"))
            {
                TextWriter tw = new StreamWriter(filename, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
        /*
        else if(HPsliderImg.fillAmount >= 0.4 && HPsliderImg.fillAmount <= 0.8)
        {
            Debug.Log("2nd");
            if (type == LogType.Log && logString.Contains("RightGrab"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftGrab"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("RightHand"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftHand"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("RightTouch") && !logString.Contains("rotation"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftTouch") && !logString.Contains("rotation"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("InfoPanel"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("phase"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && !logString.Contains("phase") && !logString.Contains("L_Grb_") && !logString.Contains("R_Grb_") && !logString.Contains("Left") && !logString.Contains("Right") && !logString.Contains("InfoPanel") && !logString.Contains("Touch"))
            {
                TextWriter tw = new StreamWriter(filename2, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
        }
        else if (HPsliderImg.fillAmount > 0.8)
        {
            if (type == LogType.Log && logString.Contains("RightGrab"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftGrab"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("RightHand"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftHand"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("RightTouch") && !logString.Contains("rotation"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("LeftTouch") && !logString.Contains("rotation"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("InfoPanel"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && logString.Contains("phase"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
            if (type == LogType.Log && !logString.Contains("phase") && !logString.Contains("L_Grb_") && !logString.Contains("R_Grb_") && !logString.Contains("Left") && !logString.Contains("Right") && !logString.Contains("InfoPanel") && !logString.Contains("Touch"))
            {
                TextWriter tw = new StreamWriter(filename3, true);
                tw.WriteLine(timer + "," + "," + "," + "," + "," + "," + "," + "," + "," + logString);
                tw.Close();
            }
        }
        */
    }

    void Timer()
    {
        timer += Time.deltaTime;
    }

}