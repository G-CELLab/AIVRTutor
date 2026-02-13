using UnityEngine;

public class TutorialPromptManager : MonoBehaviour
{
    public GPTConnector gptConnector;
    [TextArea(3, 8)]
    public string tutorialPromptOverride = AI.Prompts.TutorialPrompts.Tutorial;
    public bool clearHistoryOnEnable = true;

    private void OnEnable()
    {
        if (gptConnector == null)
        {
            Debug.LogWarning("[TutorialPromptManager] GPTConnector not assigned.");
            return;
        }

        string prompt = string.IsNullOrWhiteSpace(tutorialPromptOverride)
            ? AI.Prompts.TutorialPrompts.Tutorial
            : tutorialPromptOverride;

        gptConnector.SetPhasePromptOverride(prompt, true);
        if (clearHistoryOnEnable)
        {
            gptConnector.ClearHistory();
        }
    }

    private void OnDisable()
    {
        if (gptConnector == null) return;
        gptConnector.ClearPhasePromptOverride();
    }
}
