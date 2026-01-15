using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Manager_Tutorial : MonoBehaviour
{
    public int tutorial_stage = 0;
    public GameObject touch_Info;
    public GameObject grab_Info;
    public GameObject grab_Obj;
    public GameObject grab_particle1;
    public GameObject grab_particle2;
    public GameObject copy_Info;
    public GameObject copy_obj1;
    public GameObject copy_obj2;
    public GameObject copy_obj3;
    public GameObject copy_obj4;

    public GameObject img_Touch;
    public GameObject img_Grab;
    public GameObject img_Duplicate;

    public Sprite touch_comp;
    public Sprite grab_comp;
    public Sprite duplicate_comp;

    public GameObject change_Scene;
    public SceneTransitionManager sceneTransition;
    //public string sceneName;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (tutorial_stage == 3)
        {
            touch_Info.SetActive(false);
            grab_Info.SetActive(true);
            grab_Obj.SetActive(true);
            grab_particle1.SetActive(true);
            img_Touch.GetComponent<Image>().sprite = touch_comp;
            img_Grab.SetActive(true);
        }
        else if (tutorial_stage == 4)
        {
            grab_particle2.SetActive(true);
        }
        else if (tutorial_stage == 5)
        {
            grab_Info.SetActive(false);
            grab_Obj.SetActive(false);
            copy_Info.SetActive(true);
            copy_obj1.SetActive(true);
            copy_obj2.SetActive(true);
            img_Grab.GetComponent<Image>().sprite = grab_comp;
            img_Duplicate.SetActive(true);

        }
        else if (tutorial_stage == 6)
        {
            copy_obj1.SetActive(false);
            copy_obj2.SetActive(false);
            copy_obj3.SetActive(true);
            copy_obj4.SetActive(true);
        }
        else if (tutorial_stage == 7)
        {
            copy_obj3.SetActive(false);
            copy_obj4.SetActive(false);
            img_Duplicate.GetComponent<Image>().sprite = duplicate_comp;
            change_Scene.SetActive(true);
        }
        else if (tutorial_stage == 8)
        {
            sceneTransition.GoToScene(1);
        }
    }
}
