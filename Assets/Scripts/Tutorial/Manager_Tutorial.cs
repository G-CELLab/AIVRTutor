using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Manager_Tutorial : MonoBehaviour
{
    public enum TutorialStage
    {
        TouchStage,
        GrabTutorial,
        CopyTutorial,
        Finish
    }

    [SerializeField] private TutorialStage tutorialStage = TutorialStage.TouchStage;
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

    private TutorialStage lastStage = (TutorialStage)(-1);
    [SerializeField] private int touchTargetsRequired = 3;
    private int touchTargetsCompleted = 0;
    [SerializeField] private int copyTargetsRequired = 2;
    private int copyTargetsCompleted = 0;

    public TutorialStage CurrentStage => tutorialStage;


    // Start is called before the first frame update
    void Start()
    {
        ApplyStageState();
    }

    // Update is called once per frame
    void Update()
    {
        if (tutorialStage != lastStage)
        {
            ApplyStageState();
        }
    }

    public void AdvanceStage()
    {
        if (tutorialStage == TutorialStage.Finish)
        {
            return;
        }

        tutorialStage = (TutorialStage)((int)tutorialStage + 1);
        ApplyStageState();
    }

    public void RegisterTouchComplete()
    {
        if (tutorialStage != TutorialStage.TouchStage)
        {
            return;
        }

        touchTargetsCompleted++;
        if (touchTargetsCompleted >= touchTargetsRequired)
        {
            AdvanceStage();
        }
    }

    public void RegisterCopyComplete()
    {
        if (tutorialStage != TutorialStage.CopyTutorial)
        {
            return;
        }

        copyTargetsCompleted++;
        if (copyTargetsCompleted == 1 && copyTargetsRequired > 1)
        {
            SetActiveSafe(copy_obj1, false);
            SetActiveSafe(copy_obj2, false);
            SetActiveSafe(copy_obj3, true);
            SetActiveSafe(copy_obj4, true);
            return;
        }

        if (copyTargetsCompleted >= copyTargetsRequired)
        {
            AdvanceStage();
        }
    }

    public void GoToNextScene(int sceneIndex)
    {
        if (tutorialStage != TutorialStage.Finish)
        {
            return;
        }

        if (sceneTransition != null)
        {
            sceneTransition.GoToScene(sceneIndex);
        }
    }

    private void ApplyStageState()
    {
        lastStage = tutorialStage;

        switch (tutorialStage)
        {
            case TutorialStage.TouchStage:
                touchTargetsCompleted = 0;
                SetActiveSafe(touch_Info, true);
                SetActiveSafe(grab_Info, false);
                SetActiveSafe(grab_Obj, false);
                SetActiveSafe(grab_particle1, false);
                SetActiveSafe(grab_particle2, false);
                SetActiveSafe(copy_Info, false);
                SetActiveSafe(copy_obj1, false);
                SetActiveSafe(copy_obj2, false);
                SetActiveSafe(copy_obj3, false);
                SetActiveSafe(copy_obj4, false);
                SetActiveSafe(img_Touch, true);
                SetActiveSafe(img_Grab, false);
                SetActiveSafe(img_Duplicate, false);
                SetActiveSafe(change_Scene, false);
                break;
            case TutorialStage.GrabTutorial:
                SetActiveSafe(touch_Info, false);
                SetActiveSafe(grab_Info, true);
                SetActiveSafe(grab_Obj, true);
                SetActiveSafe(grab_particle1, true);
                SetActiveSafe(grab_particle2, false);
                SetImageSpriteSafe(img_Touch, touch_comp);
                SetActiveSafe(img_Grab, true);
                break;
            case TutorialStage.CopyTutorial:
                copyTargetsCompleted = 0;
                SetActiveSafe(grab_Info, false);
                SetActiveSafe(grab_Obj, false);
                SetActiveSafe(grab_particle1, false);
                SetActiveSafe(grab_particle2, false);
                SetActiveSafe(copy_Info, true);
                SetActiveSafe(copy_obj1, true);
                SetActiveSafe(copy_obj2, true);
                SetActiveSafe(copy_obj3, false);
                SetActiveSafe(copy_obj4, false);
                SetImageSpriteSafe(img_Grab, grab_comp);
                SetActiveSafe(img_Duplicate, true);
                break;
            case TutorialStage.Finish:
                SetActiveSafe(copy_Info, false);
                SetActiveSafe(copy_obj1, false);
                SetActiveSafe(copy_obj2, false);
                SetActiveSafe(copy_obj3, false);
                SetActiveSafe(copy_obj4, false);
                SetActiveSafe(grab_particle1, false);
                SetActiveSafe(grab_particle2, false);
                SetImageSpriteSafe(img_Duplicate, duplicate_comp);
                SetActiveSafe(change_Scene, true);
                break;
        }
    }

    private void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private void SetImageSpriteSafe(GameObject target, Sprite sprite)
    {
        if (target == null || sprite == null)
        {
            return;
        }

        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
        }
    }
}
