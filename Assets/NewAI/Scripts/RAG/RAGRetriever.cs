using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NewAI.Scripts.RAG
{
    /// <summary>
    /// Represents a single chunk from the knowledge base
    /// </summary>
    [System.Serializable]
    public class RAGChunk
    {
        public string Source { get; set; }
        public string Text { get; set; }
        public float Score { get; set; }

        public RAGChunk(string source, string text, float score = 0f)
        {
            Source = source;
            Text = text;
            Score = score;
        }
    }

    /// <summary>
    /// Manages retrieval from the local RAG knowledge base
    /// Interfaces with Python RAG backend via HTTP or direct method calls
    /// </summary>
    public class RAGRetriever : MonoBehaviour
    {
        [Header("RAG Configuration")]
        [SerializeField] private string ragServiceUrl = "http://localhost:5000";
        [SerializeField] private int defaultTopK = 3;
        [SerializeField] private bool useLocalPythonProcess = true;
        [SerializeField] private string pythonScriptPath = "../AiPrototype/rag_cli.py";

        private RAGService _ragService;

        private void Awake()
        {
            if (useLocalPythonProcess)
            {
                _ragService = new LocalRAGService();
            }
            else
            {
                _ragService = new HttpRAGService(ragServiceUrl);
            }
        }

        /// <summary>
        /// Retrieve chunks from the knowledge base asynchronously
        /// </summary>
        public IEnumerator RetrieveAsync(string query, int topK, System.Action<List<RAGChunk>> onComplete)
        {
            if (_ragService == null)
            {
                Debug.LogError("RAG Service not initialized");
                onComplete?.Invoke(new List<RAGChunk>());
                yield break;
            }

            yield return _ragService.RetrieveAsync(query, topK, onComplete);
        }

        /// <summary>
        /// Retrieve with default topK setting
        /// </summary>
        public IEnumerator RetrieveAsync(string query, System.Action<List<RAGChunk>> onComplete)
        {
            yield return RetrieveAsync(query, defaultTopK, onComplete);
        }
    }

    /// <summary>
    /// Abstract base for RAG service implementations
    /// </summary>
    public abstract class RAGService
    {
        public abstract IEnumerator RetrieveAsync(string query, int topK, System.Action<List<RAGChunk>> onComplete);
    }

    /// <summary>
    /// HTTP-based RAG service for remote backend
    /// </summary>
    public class HttpRAGService : RAGService
    {
        private string _serviceUrl;

        public HttpRAGService(string serviceUrl)
        {
            _serviceUrl = serviceUrl;
        }

        public override IEnumerator RetrieveAsync(string query, int topK, System.Action<List<RAGChunk>> onComplete)
        {
            // TODO: Implement HTTP call to RAG backend
            Debug.Log($"[HttpRAGService] Retrieving for query: {query} (topK={topK})");
            
            // For now, return empty list
            onComplete?.Invoke(new List<RAGChunk>());
            yield return null;
        }
    }

    /// <summary>
    /// Local Python process RAG service
    /// Uses the Python rag_cli.py directly via IPC
    /// </summary>
    public class LocalRAGService : RAGService
    {
        private System.Diagnostics.Process _process;
        private bool _initialized = false;

        public override IEnumerator RetrieveAsync(string query, int topK, System.Action<List<RAGChunk>> onComplete)
        {
            Debug.Log($"[LocalRAGService] Retrieving for query: {query} (topK={topK})");
            
            // TODO: Implement local Python process IPC
            // This would communicate with the Python RAG system
            // For now, return mock data for demonstration
            
            var mockChunks = new List<RAGChunk>
            {
                new RAGChunk("mitosis_basics.md", "Mitosis is the process of cell division where a cell divides into two identical daughter cells.", 0.85f),
                new RAGChunk("interphase.md", "During interphase, the cell prepares for division by replicating its DNA and growing.", 0.72f),
                new RAGChunk("mitosis_phases.md", "Mitosis consists of prophase, metaphase, anaphase, and telophase stages.", 0.68f)
            };

            onComplete?.Invoke(mockChunks);
            yield return null;
        }
    }
}
