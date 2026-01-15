using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.XR;
using UnityEngine;

public class ControllerPositionData : MonoBehaviour
{
    [SerializeField] string csvName;

    // Controllers
    public GameObject rightController;
    public GameObject leftController;

    // Data values
    private Vector3 leftHandPosition;
    private Vector3 rightHandPosition;
    private float timeStamp = 0.0f;
    private static float activationTime;
    public static bool onetimeOperator = true;

    // private int index;

    private bool startWriting;

    private int dropRate;
    private int dropIndex = 0;
    string filePath;

    private int round;

    private void OnEnable()
    {
        if (onetimeOperator == true)
        {
            activationTime = Time.time;
            onetimeOperator = false;
        }
    }

    private void Start()
    {
        round = WriteDebugToFile.round;
        leftHandPosition = Vector3.zero;
        rightHandPosition = Vector3.zero;
        //timeStamp = 0f;
        dropRate = 3;

        //filePath = GetFilePath();
        if (round == 1)
        {
            filePath = Application.persistentDataPath + "/" + csvName + "1.csv";
        }
        else if (round == 2)
        {
            filePath = Application.persistentDataPath + "/" + csvName + "2.csv";
        }
        else if (round == 3)
        {
            filePath = Application.persistentDataPath + "/" + csvName + "3.csv";
        }
        else if (round == 4)
        {
            filePath = Application.persistentDataPath + "/" + csvName + "4.csv";
        }

        startWriting = true;
    }

    void Update()
    {
        if (dropIndex == 0)
        {
            UpdateData();
            UpdateRun();
        }

        dropIndex++;
        dropIndex = dropIndex % dropRate;
    }

    void UpdateData()
    {

        if (rightController != null)
        {
            rightHandPosition = rightController.transform.localPosition;
        }

        if (leftController != null)
        {
            leftHandPosition = leftController.transform.localPosition;
        }

        //timeStamp = Time.time;
        timeStamp = Time.time - activationTime;
    }

    void UpdateRun()
    {
        string[] leftData = new string[5];
        leftData[0] = "Left";
        leftData[1] = leftHandPosition.x.ToString();
        leftData[2] = leftHandPosition.y.ToString();
        leftData[3] = leftHandPosition.z.ToString();
        leftData[4] = timeStamp.ToString();

        string[] rightData = new string[5];
        rightData[0] = "Right";
        rightData[1] = rightHandPosition.x.ToString();
        rightData[2] = rightHandPosition.y.ToString();
        rightData[3] = rightHandPosition.z.ToString();
        rightData[4] = timeStamp.ToString();

        WriteDataLine(leftData);
        WriteDataLine(rightData);
    }

    public void WriteDataLine(string[] line)
    {
        try
        {
            if (startWriting)
            {
                using (StreamWriter file = new StreamWriter(@filePath, false))
                {
                    file.WriteLine("Hand" + "," + "XPos" + "," + "YPos" +
                        "," + "ZPos" + "," + "Time");
                }
                startWriting = false;
            }
            else
            {
                using (StreamWriter file = new StreamWriter(@filePath, true))
                {
                    file.WriteLine(line[0] + "," + line[1] + "," + line[2] + "," + line[3]
                        + "," + line[4]);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log("Something went wrong! Error: " + ex.Message);
        }
    }

    /*
    string GetFilePath()
    {
        return Application.persistentDataPath + "/" + csvName + ".csv";
    }
    */
}