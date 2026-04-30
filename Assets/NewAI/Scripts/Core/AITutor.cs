using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NewAI.Scripts.RAG;

namespace NewAI.Scripts.Core
{
    /// <summary>
    /// AITutor is the main NPC controller that combines RAG retrieval with conversational AI.
    /// It manages interaction state, scene context, and delegates to the RAG system for knowledge retrieval.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("NewAI/AI Tutor")]
    public class AITutor : MonoBehaviour
    {
        [System.Serializable]
        public class TutorSettings
        {
            [Tooltip("Name of the AI tutor character")]
            public string characterName = "Biology Tutor";

            [Tooltip("Top K chunks to retrieve from RAG")]
            public int topK = 3;

            [Tooltip("Maximum tokens for model response")]
            public int maxOutputTokens = 80;

            [Tooltip("Show debug context in console")]
            public bool showDebugContext = false;
        }

        [Header("Configuration")]
        [SerializeField] private TutorSettings settings = new TutorSettings();

        [Header("Events")]
        public UnityEvent<string> OnResponseStarted = new UnityEvent<string>();
        public UnityEvent<string> OnResponseStreamed = new UnityEvent<string>();
        public UnityEvent<string> OnResponseCompleted = new UnityEvent<string>();
        public UnityEvent<string> OnErrorOccurred = new UnityEvent<string>();

        private AudioSource _audioSource;
        private RAGRetriever _ragRetriever;
        private TutorSceneState _sceneState;
        private AIResponseGenerator _responseGenerator;
        private bool _isProcessing = false;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _ragRetriever = GetComponent<RAGRetriever>();
            _responseGenerator = GetComponent<AIResponseGenerator>();

            if (_ragRetriever == null)
            {
                Debug.LogError("RAGRetriever component not found on AITutor!");
            }

            if (_responseGenerator == null)
            {
                Debug.LogError("AIResponseGenerator component not found on AITutor!");
            }
        }

        private void Start()
        {
            _sceneState = new TutorSceneState();
        }

        /// <summary>
        /// Process user query through RAG system and generate response
        /// </summary>
        public void ProcessUserQuery(string userQuery)
        {
            Debug.Log($"[AITutor.ProcessUserQuery] ENTRY - query: {userQuery}, _isProcessing: {_isProcessing}");
            if (_isProcessing)
            {
                Debug.LogWarning($"[AITutor.ProcessUserQuery] BLOCKED - Already processing a query. Please wait. (_isProcessing={_isProcessing})");
                return;
            }
            Debug.Log($"[AITutor.ProcessUserQuery] Gate check passed, proceeding to coroutine");

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                OnErrorOccurred.Invoke("Empty query provided");
                return;
            }

            StartCoroutine(ProcessQueryCoroutine(userQuery));
        }

        /// <summary>
        /// Update the current scene phase (e.g., prophase, metaphase, etc.)
        /// </summary>
        public void SetPhase(string phaseName)
        {
            _sceneState.Phase = phaseName.ToLower();
            _sceneState.SceneSummary = $"The learner is in the {phaseName} stage of the mitosis simulation.";
            Debug.Log($"[AITutor] Phase updated to: {_sceneState.Phase}");
        }

        /// <summary>
        /// Update visible objects in the scene
        /// </summary>
        public void SetVisibleObjects(string objectDescription)
        {
            _sceneState.VisibleObjects = objectDescription;
        }

        /// <summary>
        /// Get current scene state
        /// </summary>
        public TutorSceneState GetSceneState()
        {
            return _sceneState;
        }

        private IEnumerator ProcessQueryCoroutine(string userQuery)
        {
            Debug.Log($"[AITutor.ProcessQueryCoroutine] START - setting _isProcessing=true for query: {userQuery}");
            _isProcessing = true;
            try
            {
                Debug.Log($"[AITutor.ProcessQueryCoroutine] Invoking OnResponseStarted event");
                OnResponseStarted.Invoke(userQuery);

                // Phase 1: Retrieve relevant knowledge base chunks
                List<RAGChunk> retrievedChunks = new List<RAGChunk>();
                
                if (_ragRetriever != null)
                {
                    yield return _ragRetriever.RetrieveAsync(userQuery, settings.topK, (chunks) =>
                    {
                        retrievedChunks = chunks;
                    });
                }

                if (settings.showDebugContext && retrievedChunks.Count > 0)
                {
                    Debug.Log($"[RAG] Retrieved {retrievedChunks.Count} chunks:");
                    for (int i = 0; i < retrievedChunks.Count; i++)
                    {
                        Debug.Log($"  [{i + 1}] {retrievedChunks[i].Source} (score: {retrievedChunks[i].Score:F3})");
                    }
                }

                // Phase 2: Generate response using RAG context
                if (_responseGenerator != null)
                {
                    string fullResponse = "";
                    yield return _responseGenerator.GenerateResponseAsync(
                        userQuery,
                        retrievedChunks,
                        _sceneState,
                        settings.maxOutputTokens,
                        (chunk) =>
                        {
                            OnResponseStreamed.Invoke(chunk);
                            fullResponse += chunk;
                        }
                    );

                    Debug.Log($"[AITutor.ProcessQueryCoroutine] Clearing _isProcessing before OnResponseCompleted");
                    _isProcessing = false;
                    Debug.Log($"[AITutor.ProcessQueryCoroutine] Invoking OnResponseCompleted event");
                    OnResponseCompleted.Invoke(fullResponse);
                    Debug.Log($"[AITutor.ProcessQueryCoroutine] OnResponseCompleted event completed");
                }
            }
            finally
            {
                if (_isProcessing)
                {
                    Debug.Log($"[AITutor.ProcessQueryCoroutine] FINALLY - clearing stuck _isProcessing flag");
                    _isProcessing = false;
                }
                Debug.Log($"[AITutor.ProcessQueryCoroutine] Gate cleared, ready for next query");
            }
        }

        /// <summary>
        /// Check if tutor is currently processing
        /// </summary>
        public bool IsProcessing => _isProcessing;
    }
}
