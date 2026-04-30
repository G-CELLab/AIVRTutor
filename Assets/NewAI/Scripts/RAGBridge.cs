using System;
using System.Collections.Generic;
using UnityEngine;
using NewAI.Scripts.Core;
using NewAI.Scripts.RAG;

namespace NewAI.Scripts
{
    /// <summary>
    /// Bridge between Unity AITutor and Python RAG backend
    /// Handles serialization/deserialization and inter-process communication
    /// </summary>
    public class RAGBridge : MonoBehaviour
    {
        [Header("Communication")]
        [SerializeField] private bool usePythonBackend = false;
        [SerializeField] private string pythonBackendUrl = "http://localhost:5000/retrieve";
        [SerializeField] private string pythonScriptPath = "../AiPrototype/rag_cli.py";

        [Header("References")]
        [SerializeField] private AITutor aiTutor;

        private static RAGBridge _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            
            if (aiTutor == null)
            {
                aiTutor = GetComponent<AITutor>();
            }
        }

        /// <summary>
        /// Call this to process a query through the RAG system
        /// </summary>
        public void ProcessQuery(string query)
        {
            if (aiTutor != null)
            {
                aiTutor.ProcessUserQuery(query);
            }
        }

        /// <summary>
        /// Update the current mitosis phase
        /// </summary>
        public void SetMitosisPhase(string phase)
        {
            if (aiTutor != null)
            {
                aiTutor.SetPhase(phase);
            }
        }

        /// <summary>
        /// Manually set visible objects for better context
        /// </summary>
        public void SetSceneContext(string objectDescription)
        {
            if (aiTutor != null)
            {
                aiTutor.SetVisibleObjects(objectDescription);
            }
        }

        public static RAGBridge Instance => _instance;
    }
}
