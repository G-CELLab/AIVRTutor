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
        ContentQuestions,
        VisualQuestions,
        ManipulationQuestions,
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
    public GameObject content_Info;
    public GameObject visual_Info;
    public GameObject manipulation_Info;
    public GameObject finish_Info;

    [Header("Question Tutorial Objects")]
    [Tooltip("Enable question tutorial stages")]
    public bool enableQuestionTutorial = false;
    public GameObject chromatid_Object;
    public GameObject centriole_Object;
    public GameObject chromosome_Object;

    public GameObject img_Touch;
    public GameObject img_Grab;
    public GameObject img_Duplicate;
    public GameObject img_Content;
    public GameObject img_Visual;
    public GameObject img_Manipulation;

    public Sprite touch_comp;
    public Sprite grab_comp;
    public Sprite duplicate_comp;
    public Sprite content_comp;
    public Sprite visual_comp;
    public Sprite manipulation_comp;

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
            if (enableQuestionTutorial)
            {
                AdvanceStage();
            }
            else
            {
                // Skip question stages, go directly to Finish
                tutorialStage = TutorialStage.Finish;
                ApplyStageState();
            }
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
        Debug.Log($"[Tutorial] Stage changed to: {tutorialStage}");

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
                SetActiveSafe(chromatid_Object, false);
                SetActiveSafe(centriole_Object, false);
                SetActiveSafe(chromosome_Object, false);
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
                SetActiveSafe(chromatid_Object, false);
                SetActiveSafe(centriole_Object, false);
                SetActiveSafe(chromosome_Object, false);
                break;
            case TutorialStage.ContentQuestions:
                SetActiveSafe(copy_Info, false);
                SetActiveSafe(copy_obj1, false);
                SetActiveSafe(copy_obj2, false);
                SetActiveSafe(copy_obj3, false);
                SetActiveSafe(copy_obj4, false);
                SetActiveSafe(grab_particle1, false);
                SetActiveSafe(grab_particle2, false);
                SetActiveSafe(img_Touch, false);
                SetActiveSafe(img_Grab, false);
                SetActiveSafe(img_Duplicate, false);
                SetActiveSafe(img_Content, true);
                SetActiveSafe(content_Info, true);
                SetActiveSafe(visual_Info, false);
                SetActiveSafe(manipulation_Info, false);
                SetActiveSafe(chromatid_Object, true);
                SetActiveSafe(centriole_Object, false);
                SetActiveSafe(chromosome_Object, false);
                SetActiveSafe(change_Scene, false);
                break;
            case TutorialStage.VisualQuestions:
                SetActiveSafe(content_Info, false);
                SetActiveSafe(visual_Info, true);
                SetActiveSafe(manipulation_Info, false);
                SetActiveSafe(chromatid_Object, false);
                SetActiveSafe(centriole_Object, true);
                SetActiveSafe(chromosome_Object, true);
                SetActiveSafe(change_Scene, false);
                SetImageSpriteSafe(img_Content, content_comp);
                SetActiveSafe(img_Visual, true);
                break;
            case TutorialStage.ManipulationQuestions:
                SetActiveSafe(content_Info, false);
                SetActiveSafe(visual_Info, false);
                SetActiveSafe(manipulation_Info, true);
                SetActiveSafe(chromatid_Object, false);
                SetActiveSafe(centriole_Object, true);
                SetActiveSafe(chromosome_Object, false);
                SetActiveSafe(change_Scene, false);
                SetImageSpriteSafe(img_Visual, visual_comp);
                SetActiveSafe(img_Manipulation, true);
                break;
            case TutorialStage.Finish:
                SetActiveSafe(copy_Info, false);
                SetActiveSafe(copy_obj1, false);
                SetActiveSafe(copy_obj2, false);
                SetActiveSafe(copy_obj3, false);
                SetActiveSafe(copy_obj4, false);
                SetActiveSafe(grab_particle1, false);
                SetActiveSafe(grab_particle2, false);
                SetActiveSafe(content_Info, false);
                SetActiveSafe(visual_Info, false);
                SetActiveSafe(manipulation_Info, false);
                SetActiveSafe(finish_Info, true);
                SetActiveSafe(chromatid_Object, false);
                SetActiveSafe(centriole_Object, false);
                SetActiveSafe(chromosome_Object, false);
                SetImageSpriteSafe(img_Manipulation, manipulation_comp);
                SetActiveSafe(change_Scene, true);
                break;
        }
    }

    public void RegisterContentQuestionComplete()
    {
        if (tutorialStage != TutorialStage.ContentQuestions)
        {
            return;
        }

        AdvanceStage();
    }

    public void RegisterVisualQuestionComplete()
    {
        if (tutorialStage != TutorialStage.VisualQuestions)
        {
            return;
        }

        AdvanceStage();
    }

    public void RegisterManipulationQuestionComplete()
    {
        if (tutorialStage != TutorialStage.ManipulationQuestions)
        {
            return;
        }

        AdvanceStage();
    }

    private void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null)
        {
            // First, ensure all parents are active
            if (active)
            {
                EnsureParentChainActive(target.transform);
            }
            
            target.SetActive(active);
            
            if (active)
            {
                // Verify it actually became active
                if (!target.activeInHierarchy)
                {
                    Debug.LogError($"[Tutorial] {target.name} is still inactive! Check for inactive parents.");
                }
                else
                {
                    Debug.Log($"[Tutorial] Successfully activated {target.name}");
                    Debug.Log($"  Position: {target.transform.position}");
                    Debug.Log($"  Scale: {target.transform.localScale}");
                }
            }
            else
            {
                Debug.Log($"[Tutorial] Deactivated {target.name}");
            }
        }
        else if (active)
        {
            Debug.LogWarning($"[Tutorial] Attempted to activate a NULL GameObject. Check Inspector assignments!");
        }
    }

    private void EnsureParentChainActive(Transform transform)
    {
        if (transform.parent != null)
        {
            // Recursively ensure all parents are active
            EnsureParentChainActive(transform.parent);
            
            if (!transform.parent.gameObject.activeSelf)
            {
                Debug.Log($"[Tutorial] Activating parent: {transform.parent.name}");
                transform.parent.gameObject.SetActive(true);
            }
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
