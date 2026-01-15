using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidCollision : MonoBehaviour
{
    /*
    [SerializeField] private GameObject asteroidExplostionGo;
    
    public void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Asteroid")
        {
            Destroy(collision.gameObject);
            Instantiate(asteroidExplostionGo, collision.transform.position, collision.transform.rotation);

            //Send notification to game manager that we hit an asteroid.
            GameManager.AsteroidHit();

            Destroy(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
    */
}
