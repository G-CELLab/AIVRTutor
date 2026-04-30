using NewAI.Scripts.Core;
using UnityEngine;

namespace NewAI.Scripts.Core
{
    /// <summary>
    /// Bridges OpenAISpeechRecognizer transcripts into AITutor and plays responses via TextToSpeechPlayer.
    /// Use this when you want voice input in VR with the NewAI tutoring stack.
    /// </summary>
    public class AITutorVoiceBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AITutor aiTutor;
        [SerializeField] private OpenAISpeechRecognizer speechRecognizer;
        [SerializeField] private TextToSpeechPlayer ttsPlayer;

        [Header("Behavior")]
        [SerializeField] private bool autoWireRecognizerToTutor = true;
        [SerializeField] private bool speakTutorResponses = true;
        [SerializeField] private bool logTranscriptRouting = true;

        private string pendingTranscript;
        private Coroutine pendingTranscriptFlushRoutine;

        private void Awake()
        {
            if (aiTutor == null)
            {
                aiTutor = GetComponent<AITutor>();
            }

            if (speechRecognizer == null)
            {
                speechRecognizer = FindFirstObjectByType<OpenAISpeechRecognizer>();
            }

            if (ttsPlayer == null)
            {
                ttsPlayer = FindFirstObjectByType<TextToSpeechPlayer>();
            }
        }

        private void OnEnable()
        {
            Debug.Log("[AITutorVoiceBridge] OnEnable called");
            if (speechRecognizer != null)
            {
                Debug.Log("[AITutorVoiceBridge] Subscribing to OnTranscriptReady event");
                speechRecognizer.OnTranscriptReady += HandleTranscriptReady;
            }

            if (aiTutor != null)
            {
                aiTutor.OnResponseCompleted.AddListener(HandleTutorResponseCompleted);
            }

            if (autoWireRecognizerToTutor && speechRecognizer != null)
            {
                speechRecognizer.sendAudioToGptConnector = false;
            }
        }

        private void OnDisable()
        {
            Debug.Log("[AITutorVoiceBridge] OnDisable called - unsubscribing from events");
            if (speechRecognizer != null)
            {
                Debug.Log("[AITutorVoiceBridge] Unsubscribing from OnTranscriptReady event");
                speechRecognizer.OnTranscriptReady -= HandleTranscriptReady;
            }

            if (aiTutor != null)
            {
                aiTutor.OnResponseCompleted.RemoveListener(HandleTutorResponseCompleted);
            }
        }

        private void HandleTranscriptReady(string transcript)
        {
            Debug.Log($"[AITutorVoiceBridge.HandleTranscriptReady] ENTRY - transcript: {transcript}");
            
            if (aiTutor == null)
            {
                Debug.LogWarning("[AITutorVoiceBridge] AITutor is not assigned.");
                return;
            }

            if (string.IsNullOrWhiteSpace(transcript))
            {
                Debug.Log("[AITutorVoiceBridge.HandleTranscriptReady] Transcript is null/empty, returning");
                return;
            }

            if (logTranscriptRouting)
            {
                Debug.Log($"[AITutorVoiceBridge] User transcript -> AITutor: {transcript}");
            }

            if (aiTutor.IsProcessing)
            {
                pendingTranscript = transcript;
                if (pendingTranscriptFlushRoutine == null)
                {
                    pendingTranscriptFlushRoutine = StartCoroutine(FlushPendingTranscriptWhenReady());
                }

                Debug.Log("[AITutorVoiceBridge.HandleTranscriptReady] Tutor is processing, queued transcript for the next available turn");
                return;
            }

            Debug.Log($"[AITutorVoiceBridge.HandleTranscriptReady] Calling aiTutor.ProcessUserQuery('{transcript}')");
            aiTutor.ProcessUserQuery(transcript);
            Debug.Log($"[AITutorVoiceBridge.HandleTranscriptReady] ProcessUserQuery returned");
        }

        private void HandleTutorResponseCompleted(string response)
        {
            Debug.Log($"[AITutorVoiceBridge.HandleTutorResponseCompleted] ENTRY - response length: {response?.Length ?? 0}, speakTutorResponses={speakTutorResponses}");
            
            if (!speakTutorResponses)
            {
                Debug.Log("[AITutorVoiceBridge.HandleTutorResponseCompleted] speakTutorResponses=false, returning");
                return;
            }

            if (ttsPlayer == null)
            {
                Debug.LogWarning("[AITutorVoiceBridge] TextToSpeechPlayer is not assigned.");
                return;
            }

            if (string.IsNullOrWhiteSpace(response))
            {
                Debug.Log("[AITutorVoiceBridge.HandleTutorResponseCompleted] Response is null/empty, returning");
                return;
            }

            Debug.Log("[AITutorVoiceBridge.HandleTutorResponseCompleted] Calling ttsPlayer.StopSpeaking()");
            ttsPlayer.StopSpeaking();
            Debug.Log($"[AITutorVoiceBridge.HandleTutorResponseCompleted] Calling ttsPlayer.Speak() with response length {response.Length}");
            ttsPlayer.Speak(response, null);
            Debug.Log("[AITutorVoiceBridge.HandleTutorResponseCompleted] Speak() returned");
        }

        private System.Collections.IEnumerator FlushPendingTranscriptWhenReady()
        {
            while (aiTutor != null && aiTutor.IsProcessing)
            {
                yield return null;
            }

            pendingTranscriptFlushRoutine = null;

            if (string.IsNullOrWhiteSpace(pendingTranscript) || aiTutor == null)
            {
                pendingTranscript = null;
                yield break;
            }

            string transcriptToProcess = pendingTranscript;
            pendingTranscript = null;

            Debug.Log($"[AITutorVoiceBridge.FlushPendingTranscriptWhenReady] Processing queued transcript: {transcriptToProcess}");
            aiTutor.ProcessUserQuery(transcriptToProcess);
        }
    }
}
