using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Centrosome_Hand : MonoBehaviour
{    
    public bool L_handTouched = false;
    public bool R_handTouched = false;
    public LeftCentrioleMove LeftCentriole;
    public RightCentrioleMove RightCentriole;
    public Centrosome centro;

   // public GameManager gameManager;
    public GameObject proInfo;
    public GameObject metaInfo;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left") && LeftCentriole.touchedL == true)
        {
            L_handTouched = true;
            //Debug.Log("Shoot" + L_handTouched);
        }
        else if (other.CompareTag("Right") && RightCentriole.touchedR == true)
        {
            R_handTouched = true;
            //Debug.Log("Shoot" + R_handTouched);
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (L_handTouched == true && R_handTouched == true && centro.metaSuccess == false)
        {
            proInfo.SetActive(false);
            metaInfo.SetActive(true);
        }
    }
}
