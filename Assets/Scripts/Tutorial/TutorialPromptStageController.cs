using UnityEngine;

public class TutorialPromptStageController : MonoBehaviour
{
    public Manager_Tutorial tutorialManager;
    public GPTConnector gptConnector;
    [TextArea(3, 10)] public string basePrompt = AI.Prompts.TutorialPrompts.Tutorial;
    [TextArea(3, 10)] public string contentPrompt = AI.Prompts.TutorialPrompts.TutorialContentQuestions;
    [TextArea(3, 10)] public string visualPrompt = AI.Prompts.TutorialPrompts.TutorialVisualQuestions;
    [TextArea(3, 10)] public string manipulationPrompt = AI.Prompts.TutorialPrompts.TutorialManipulationQuestions;
    [Header("Prompt Source")]
    [Tooltip("Use prompts from TutorialPrompts.cs at runtime so scene-cached text cannot go stale")]
    public bool useRuntimePromptConstants = true;
    public bool clearHistoryOnStageChange = true;
    
    [Header("AI Greeting")]
    [Tooltip("Automatically trigger AI greeting when entering question stages")]
    public bool autoGreetOnStageEnter = true;
    [Tooltip("Delay in seconds before AI greeting is triggered")]
    public float greetingDelay = 0.5f;

    private Manager_Tutorial.TutorialStage lastStage = (Manager_Tutorial.TutorialStage)(-1);

    private void Awake()
    {
        if (!useRuntimePromptConstants)
        {
            return;
        }

        // Keep runtime prompt source in sync with TutorialPrompts.cs, even if scene serialized values are outdated.
        basePrompt = AI.Prompts.TutorialPrompts.Tutorial;
        contentPrompt = AI.Prompts.TutorialPrompts.TutorialContentQuestions;
        visualPrompt = AI.Prompts.TutorialPrompts.TutorialVisualQuestions;
        manipulationPrompt = AI.Prompts.TutorialPrompts.TutorialManipulationQuestions;
    }

    private void Update()
    {
        if (tutorialManager == null || gptConnector == null)
        {
            return;
        }

        Manager_Tutorial.TutorialStage stage = tutorialManager.CurrentStage;
        if (stage == lastStage)
        {
            return;
        }

        lastStage = stage;
        string prompt = GetPromptForStage(stage);
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            gptConnector.SetPhasePromptOverride(prompt, true);
            if (clearHistoryOnStageChange)
            {
                gptConnector.ClearHistory();
            }
            
            // Trigger AI greeting for question stages
            if (autoGreetOnStageEnter && IsQuestionStage(stage))
            {
                StartCoroutine(TriggerAIGreeting(stage));
            }
        }
    }

    private bool IsQuestionStage(Manager_Tutorial.TutorialStage stage)
    {
        return stage == Manager_Tutorial.TutorialStage.ContentQuestions ||
               stage == Manager_Tutorial.TutorialStage.VisualQuestions ||
               stage == Manager_Tutorial.TutorialStage.ManipulationQuestions;
    }
    
    private System.Collections.IEnumerator TriggerAIGreeting(Manager_Tutorial.TutorialStage stage)
    {
        yield return new WaitForSeconds(greetingDelay);
        
        string greetingPrompt = GetGreetingForStage(stage);
        Debug.Log($"[TutorialPrompt] Triggering AI greeting for {stage}: {greetingPrompt}");
        
        gptConnector.SendToGPT(greetingPrompt, null);
    }
    
    private string GetGreetingForStage(Manager_Tutorial.TutorialStage stage)
    {
        switch (stage)
        {
            case Manager_Tutorial.TutorialStage.ContentQuestions:
                return "Start speaking now and follow this wording closely in order: 'Hello. I'm here to answer your questions. During this VR activity, you can ask me questions whenever you need help. I can answer three types of your questions: content questions, visual reference questions, and manipulation questions. Let's practice. For this task, you will need to move the blue chromosome. First, you might want to understand what a chromosome is. This is called a content question. For example, you could ask: What is a chromosome? Or What does a chromosome do? Please ask me a content question about chromosomes.'";
            case Manager_Tutorial.TutorialStage.VisualQuestions:
                return "Start speaking now and follow this wording closely: 'Next, you may want to know which object in this space is the blue chromosome. This is a visual reference question. For example, you could ask: Which object is the blue chromosome? Please ask me a visual reference question.'";
            case Manager_Tutorial.TutorialStage.ManipulationQuestions:
                return "Start speaking now and follow this wording closely: 'Finally, you may want to know how to move the blue chromosome. This is a manipulation question. For example, you could ask: How do I move the blue chromosome? Please ask me a manipulation question.'";
            default:
                return "Hello! How can I help you?";
        }
    }

    private string GetPromptForStage(Manager_Tutorial.TutorialStage stage)
    {
        switch (stage)
        {
            case Manager_Tutorial.TutorialStage.ContentQuestions:
                return contentPrompt;
            case Manager_Tutorial.TutorialStage.VisualQuestions:
                return visualPrompt;
            case Manager_Tutorial.TutorialStage.ManipulationQuestions:
                return manipulationPrompt;
            default:
                return basePrompt;
        }
    }
}
