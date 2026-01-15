using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PositionData : MonoBehaviour
{
    [Header("CSV File Name")]
    [SerializeField] string csvName;

    [Header("OVRCameraRig")]
    [SerializeField] GameObject player;
    //[SerializeField] GameObject Raycasting;

    private string filePath;
    private string hitObj;
    private bool startWriting;
    private bool canRecord;

    public Transform Default_Position;
    public Vector3 cameraRelative;

    //string filePath = "";
    private int round;
    float timeStamp = 0.0f;
    private static float activationTime;
    public static bool onetimeOperator = true;

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
        startWriting = false;
        canRecord = true;
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
    }

    private void Update()
    {
        timeStamp = Time.time - activationTime;
        if (canRecord)
        {
            if (Physics.Raycast(player.transform.position, player.transform.TransformDirection (Vector3.forward), out RaycastHit hitinfo, 100f))
            {
                hitObj = hitinfo.collider.name;
                Debug.DrawRay(player.transform.position, player.transform.TransformDirection(Vector3.forward) * hitinfo.distance, Color.red);
            }
            else
            {
                Debug.DrawRay(player.transform.position, player.transform.TransformDirection(Vector3.forward) * 20f, Color.green);
            }
            Vector3 cameraRelative = Default_Position.InverseTransformPoint(player.transform.position);
            addRecord(timeStamp, cameraRelative.x, cameraRelative.z, player.GetComponent<Transform>().rotation.y, hitObj, filePath);
            StartCoroutine(delayRecord());
        }
    }

    private void addRecord(float time, float x, float z, float y, string hitObj, string filePath)
    {
        try
        {
            if (!startWriting)
            {
                using (StreamWriter file = new StreamWriter(@filePath, false))
                {
                    file.WriteLine("Time" + "," + "XPos" + "," + "ZPos" + "," + "YRot" + "," + "Raycast");
                }
                startWriting = true;
            }
            else
            {
                using (StreamWriter file = new StreamWriter(@filePath, true))
                {
                    file.WriteLine(time + "," + x + "," + z + "," + y + "," + hitObj);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log("Something went wrong! Error: " + ex.Message);
        }
    }

    
    private IEnumerator delayRecord()
    {
        canRecord = false;
        yield return new WaitForSeconds(0.05f);
        canRecord = true;
    }
    
    /*
    private string GetFilePath()
    {        
        return Application.persistentDataPath + "/" + csvName + ".csv";   
    }
    */
}