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
    [Tooltip("Maximum seconds to wait for AI speech to finish before advancing")]
    public float maxWaitForSpeechEnd = 12.0f;

    private Coroutine pulseRoutine;
    private bool awaitingVisualAdvance;
    private bool awaitingContentAdvance;
    private bool awaitingManipulationAdvance;

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
            if (IsContentSuccessResponse(normalized) && !awaitingContentAdvance)
            {
                awaitingContentAdvance = true;
                StartCoroutine(AdvanceWhenReady(0f, () =>
                {
                    awaitingContentAdvance = false;
                    tutorialManager.RegisterContentQuestionComplete();
                }));
            }
            return;
        }

        if (stage == Manager_Tutorial.TutorialStage.VisualQuestions)
        {
            if (IsVisualSuccessResponse(normalized) && !awaitingVisualAdvance)
            {
                Transform target = ResolveVisualTarget(normalized);
                float delay = Mathf.Max(0.1f, visualAdvanceDelay);
                if (target != null)
                {
                    StartPulse(target);
                }
                awaitingVisualAdvance = true;
                StartCoroutine(AdvanceWhenReady(delay, () =>
                {
                    awaitingVisualAdvance = false;
                    tutorialManager.RegisterVisualQuestionComplete();
                }));
            }
            return;
        }

        if (stage == Manager_Tutorial.TutorialStage.ManipulationQuestions)
        {
            if (IsManipulationSuccessResponse(normalized) && !awaitingManipulationAdvance)
            {
                awaitingManipulationAdvance = true;
                StartCoroutine(AdvanceWhenReady(0f, () =>
                {
                    awaitingManipulationAdvance = false;
                    tutorialManager.RegisterManipulationQuestionComplete();
                }));
            }
        }
    }

    private IEnumerator AdvanceWhenReady(float delay, System.Action onAdvance)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        while (gptConnector != null && gptConnector.IsAgentBusy && elapsed < maxWaitForSpeechEnd)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        onAdvance?.Invoke();
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

    private static bool HasGoodValidationCue(string normalized)
    {
        // Accept 'good' as a standalone word anywhere in the response.
        return ContainsPhrase(normalized, "good");
    }

    private static bool IsContentSuccessResponse(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized)) return false;
        // Allow progression when the model validates with "Good" and answers a chromosome content question.
        return HasGoodValidationCue(normalized) &&
               ContainsPhrase(normalized, "chromosome") &&
               (ContainsPhrase(normalized, "genetic") || ContainsPhrase(normalized, "dna") || ContainsPhrase(normalized, "structure"));
    }

    private static bool IsVisualSuccessResponse(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized)) return false;
        // Accept common visual-reference confirmations for the blue chromosome target.
        return HasGoodValidationCue(normalized) &&
               (ContainsPhrase(normalized, "blue chromosome") ||
                ContainsPhrase(normalized, "blue x shaped") ||
                (ContainsPhrase(normalized, "chromosome") && ContainsPhrase(normalized, "middle")));
    }

    private static bool IsManipulationSuccessResponse(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized)) return false;
        // Accept common manipulation guidance phrasing for grab-and-move instructions.
        return HasGoodValidationCue(normalized) &&
               (ContainsPhrase(normalized, "grab") ||
                ContainsPhrase(normalized, "move") ||
                ContainsPhrase(normalized, "highlighted area"));
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
