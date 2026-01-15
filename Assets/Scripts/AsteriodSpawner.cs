using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteriodSpawner : MonoBehaviour
{
    [Header("Size of the spawn area")]
    public Vector3 size;

    [Header("Rate of instantiation")]
    public float spawnRate = 1f;

    [Header("Model used to instantiate")]
    public GameObject asteriodModel;

    [Header("Asteriod Parent")]
    public Transform asteriodParent;

    private float nextSpawn = 0;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawCube(transform.position, size);
    }

    void Start()
    {
        
    }

    void Update()
    {
        //Timer to control spawning (time in seconds since the start of the game)
        if(Time.time > nextSpawn)
        {
            nextSpawn = Time.time + spawnRate;
            //call the function to create an asteriod
            SpawnAnAsteriod();
        }
    }

    private void SpawnAnAsteriod()
    {
        Vector3 spawnPoint = transform.position + new Vector3(UnityEngine.Random.Range(-size.x / 2, size.x / 2), 
                                                              UnityEngine.Random.Range(-size.y / 2, size.y / 2),
                                                              UnityEngine.Random.Range(-size.z / 2, size.z / 2));

        /*Quaternion asteriodRotation = Quaternion.Euler(UnityEngine.Random.Range(0, 360),
                                                       UnityEngine.Random.Range(0, 360),
                                                       UnityEngine.Random.Range(0, 360));*/

        GameObject asteriod = Instantiate(asteriodModel, spawnPoint, transform.rotation);

        asteriod.transform.SetParent(asteriodParent);
    }
}
