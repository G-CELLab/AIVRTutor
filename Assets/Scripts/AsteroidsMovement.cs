using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidsMovement : MonoBehaviour
{
    /*
    [Header("Control the speed of the asteroid")]
    public float maxSpeed;
    public float minSpeed;

    [Header("Control the rotation of the asteroid")]
    public float rotationSpeedMin;
    public float rotationSpeedMax;

    private float rotationSpeed;
    private float xAngle, yAngle, zAngle;

    public Vector3 movementDirection;
    private float _asteroidSpeed;

    void Start()
    {
        _asteroidSpeed = Random.Range(minSpeed, maxSpeed);
        xAngle = Random.Range(0, 360);
        yAngle = Random.Range(0, 360);
        zAngle = Random.Range(0, 360);

        transform.GetChild(0).transform.Rotate(xAngle, yAngle, zAngle, Space.Self);
        rotationSpeed = Random.Range(rotationSpeedMin, rotationSpeedMax);
    }

    void Update()
    {
        transform.Translate(movementDirection * Time.deltaTime * _asteroidSpeed);
        transform.GetChild(0).transform.Rotate(Vector3.up * Time.deltaTime * rotationSpeed);
    }
    */
}
