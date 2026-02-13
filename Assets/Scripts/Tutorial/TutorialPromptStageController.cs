using UnityEngine;

public class TutorialPromptStageController : MonoBehaviour
{
    public Manager_Tutorial tutorialManager;
    public GPTConnector gptConnector;
    [TextArea(3, 10)] public string basePrompt = AI.Prompts.TutorialPrompts.Tutorial;
    [TextArea(3, 10)] public string contentPrompt = AI.Prompts.TutorialPrompts.TutorialContentQuestions;
    [TextArea(3, 10)] public string visualPrompt = AI.Prompts.TutorialPrompts.TutorialVisualQuestions;
    [TextArea(3, 10)] public string manipulationPrompt = AI.Prompts.TutorialPrompts.TutorialManipulationQuestions;
    public bool clearHistoryOnStageChange = true;
    
    [Header("AI Greeting")]
    [Tooltip("Automatically trigger AI greeting when entering question stages")]
    public bool autoGreetOnStageEnter = true;
    [Tooltip("Delay in seconds before AI greeting is triggered")]
    public float greetingDelay = 0.5f;

    private Manager_Tutorial.TutorialStage lastStage = (Manager_Tutorial.TutorialStage)(-1);

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
                return "Start speaking now. Explain that this phase teaches how to ask content knowledge questions. Give a brief example like: 'What does chromatid mean?' and ask the learner to try one.";
            case Manager_Tutorial.TutorialStage.VisualQuestions:
                return "Start speaking now. Say: 'The next kind of question to learn is visual reference questions.' Briefly explain these connect biology terms to objects they can see. Give an example like: 'Which object here is the chromosome?' and ask them to try one.";
            case Manager_Tutorial.TutorialStage.ManipulationQuestions:
                return "Start speaking now. Say: 'The next kind of question to learn is manipulation and instruction questions.' Briefly explain these are for asking what to do next. Give an example like: 'What should I do next?' and ask them to try one.";
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
