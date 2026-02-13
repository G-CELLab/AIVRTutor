using System.Collections;
using System.Text;
using UnityEngine;

public class TutorialQuestionPhaseController : MonoBehaviour
{
    public Manager_Tutorial tutorialManager;
    public GPTConnector gptConnector;

    [Header("Scene Objects")]
    public Transform chromatidObject;
    public Transform centrioleObject;
    public Transform chromosomeObject;

    [Header("Visual Feedback")]
    public float pulseDuration = 1.0f;
    public float pulseScaleMultiplier = 1.15f;
    public float visualAdvanceDelay = 1.0f;

    private Coroutine pulseRoutine;
    private bool awaitingVisualAdvance;

    private void OnEnable()
    {
        if (gptConnector != null)
        {
            gptConnector.OnAssistantTranscript += HandleAssistantTranscript;
        }
    }

    private void OnDisable()
    {
        if (gptConnector != null)
        {
            gptConnector.OnAssistantTranscript -= HandleAssistantTranscript;
        }
    }

    private void HandleAssistantTranscript(string transcript)
    {
        if (tutorialManager == null || string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        string normalized = Normalize(transcript);
        Manager_Tutorial.TutorialStage stage = tutorialManager.CurrentStage;

        if (stage == Manager_Tutorial.TutorialStage.ContentQuestions)
        {
            if (ContainsPhrase(normalized, "that was a content question"))
            {
                tutorialManager.RegisterContentQuestionComplete();
            }
            return;
        }

        if (stage == Manager_Tutorial.TutorialStage.VisualQuestions)
        {
            if (ContainsPhrase(normalized, "that was a visual reference question") && !awaitingVisualAdvance)
            {
                Transform target = ResolveVisualTarget(normalized);
                float delay = Mathf.Max(0.1f, visualAdvanceDelay);
                if (target != null)
                {
                    StartPulse(target);
                }
                awaitingVisualAdvance = true;
                StartCoroutine(AdvanceAfterDelay(delay));
            }
            return;
        }

        if (stage == Manager_Tutorial.TutorialStage.ManipulationQuestions)
        {
            if (ContainsPhrase(normalized, "that was a manipulation question"))
            {
                tutorialManager.RegisterManipulationQuestionComplete();
            }
        }
    }

    private IEnumerator AdvanceAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        awaitingVisualAdvance = false;
        tutorialManager.RegisterVisualQuestionComplete();
    }

    private Transform ResolveVisualTarget(string normalized)
    {
        if (normalized.Contains("chromosome"))
        {
            return chromosomeObject;
        }

        if (normalized.Contains("centriole"))
        {
            return centrioleObject;
        }

        return null;
    }

    private void StartPulse(Transform target)
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
        }
        pulseRoutine = StartCoroutine(PulseScale(target, pulseDuration, pulseScaleMultiplier));
    }

    private IEnumerator PulseScale(Transform target, float duration, float scaleMultiplier)
    {
        if (target == null)
        {
            yield break;
        }

        Vector3 baseScale = target.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float wave = Mathf.Sin(normalized * Mathf.PI);
            float scale = Mathf.Lerp(1f, scaleMultiplier, wave);
            target.localScale = baseScale * scale;
            yield return null;
        }

        target.localScale = baseScale;
    }

    private static bool ContainsPhrase(string normalized, string phrase)
    {
        return normalized.Contains(Normalize(phrase));
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(' ');
            }
        }

        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
    }
}
