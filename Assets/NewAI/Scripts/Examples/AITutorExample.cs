using UnityEngine;
using UnityEngine.InputSystem;
using NewAI.Scripts.Core;
using NewAI.Scripts;

namespace NewAI.Examples
{
    /// <summary>
    /// Example script showing how to use the AITutor system
    /// Attach this to a GameObject to test basic functionality
    /// </summary>
    public class AITutorExample : MonoBehaviour
    {
        [SerializeField] private AITutor aiTutor;
        [SerializeField] private string[] testQueries = new[]
        {
            "What is happening right now?",
            "What should I do in this phase?",
            "Tell me about mitosis"
        };

        private int _currentQueryIndex = 0;

        private void Start()
        {
            if (aiTutor == null)
            {
                Debug.LogError("AITutor reference not set!");
                return;
            }

            // Subscribe to events
            aiTutor.OnResponseStarted.AddListener(OnQueryStarted);
            aiTutor.OnResponseStreamed.AddListener(OnTokenStreamed);
            aiTutor.OnResponseCompleted.AddListener(OnResponseCompleted);
            aiTutor.OnErrorOccurred.AddListener(OnError);

            Debug.Log("AITutor Example initialized. Press SPACE to send test queries.");
            Debug.Log("Press 'P' to change phase (prophase, metaphase, anaphase, telophase)");
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                SendTestQuery();
            }

            if (Keyboard.current.pKey.wasPressedThisFrame)
            {
                CyclePhase();
            }
        }

        private void SendTestQuery()
        {
            if (aiTutor.IsProcessing)
            {
                Debug.LogWarning("Already processing a query!");
                return;
            }

            string query = testQueries[_currentQueryIndex % testQueries.Length];
            Debug.Log($"[Example] Sending query: {query}");
            aiTutor.ProcessUserQuery(query);
            _currentQueryIndex++;
        }

        private void CyclePhase()
        {
            var phases = new[] { "interphase", "prophase", "metaphase", "anaphase", "telophase" };
            var currentPhase = aiTutor.GetSceneState().Phase;
            var currentIndex = System.Array.IndexOf(phases, currentPhase);
            var nextIndex = (currentIndex + 1) % phases.Length;

            aiTutor.SetPhase(phases[nextIndex]);
            Debug.Log($"[Example] Phase changed to: {phases[nextIndex]}");
        }

        private void OnQueryStarted(string query)
        {
            Debug.Log($"[Example] Query started: {query}");
        }

        private void OnTokenStreamed(string token)
        {
            // Simulate streaming output
            // In real implementation, could send to speech synthesis
        }

        private void OnResponseCompleted(string fullResponse)
        {
            Debug.Log($"[Example] Response completed:\n{fullResponse}");
        }

        private void OnError(string error)
        {
            Debug.LogError($"[Example] Error: {error}");
        }
    }
}
