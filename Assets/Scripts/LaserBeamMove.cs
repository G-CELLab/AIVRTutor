using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserBeamMove : MonoBehaviour
{
    private Rigidbody rb;
    public float thrust = 30.0f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        rb.linearVelocity = transform.forward * thrust;
        Destroy(this.gameObject, 5f);
    }
}
