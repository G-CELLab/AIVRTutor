using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using NewAI.Scripts.Core;
using NewAI.Scripts.RAG;

namespace NewAI.Scripts.Core
{
    /// <summary>
    /// Generates AI responses using RAG context and OpenAI integration
    /// Communicates with OpenAI API (compatible with GPTConnector setup)
    /// </summary>
    public class AIResponseGenerator : MonoBehaviour
    {
        [Header("🔑 OpenAI Configuration")]
        [SerializeField] private string openaiApiKey = "";
        [Tooltip("Set to your OpenAI API key. Or leave empty to use environment variable OPENAI_API_KEY")]
        [SerializeField] private string openaiModel = "gpt-4o-mini";
        [SerializeField] private float temperature = 0.2f;
        [SerializeField] private int maxTokens = 150;
        [SerializeField] private int requestTimeoutSeconds = 30;

        [Header("Response Settings")]
        [SerializeField] private bool useStreamingApi = false;
        [Tooltip("Log all API requests and responses for debugging")]
        [SerializeField] private bool logApiCalls = false;
        [SerializeField] private bool logLatencyMetrics = true;

        private const string OPENAI_API_ENDPOINT = "https://api.openai.com/v1/chat/completions";
        private const string SYSTEM_PROMPT_TEMPLATE = 
            "You are a friendly biology tutor for a VR mitosis simulation. " +
            "The learner is currently in the {0} phase. " +
            "Use a warm, encouraging, and natural tone. Answer biology questions clearly and accurately. " +
            "Use the knowledge base (retrieved context) for detailed information about mitosis, cell biology, and what the learner should do next in the scene. " +
            "If the learner asks what to do, guide them based on the knowledge base instructions for their current phase. " +
            "Keep replies concise and conversational—aim for 1-2 sentences when possible. " +
            "Never invent instructions or scene mechanics not in the knowledge base.";

        /// <summary>
        /// Generate response asynchronously using RAG context
        /// Makes actual OpenAI API call
        /// </summary>
        public IEnumerator GenerateResponseAsync(
            string userQuery,
            List<RAGChunk> retrievedChunks,
            TutorSceneState sceneState,
            int maxTokens,
            System.Action<string> onTokenStreamed)
        {
            var startTime = System.DateTime.Now;
            // Quick invocation trace to help debug missing responses
            try
            {
                string shortQ = string.IsNullOrWhiteSpace(userQuery) ? "(empty)" : userQuery.Length <= 120 ? userQuery : userQuery.Substring(0, 120) + "...";
                Debug.Log($"[AIResponseGenerator] GenerateResponseAsync invoked. Query='{shortQ}'");
            }
            catch { }

            // Build context block from retrieved chunks
            var contextLines = new List<string>();
            for (int i = 0; i < retrievedChunks.Count; i++)
            {
                var chunk = retrievedChunks[i];
                contextLines.Add($"[{i + 1}] {chunk.Text}");
            }

            string contextBlock = contextLines.Count > 0 
                ? string.Join("\n\n", contextLines)
                : "(no relevant knowledge base entries found)";

            // Build user message with scene context
            string userMessage = 
                $"Simulation state: phase={sceneState.Phase}. Scene: {sceneState.SceneSummary}. " +
                $"Visible objects: {sceneState.VisibleObjects}.\n\n" +
                $"Knowledge base:\n{contextBlock}\n\n" +
                $"User: {userQuery}";

            // Build system prompt
            string systemPrompt = string.Format(SYSTEM_PROMPT_TEMPLATE, sceneState.Phase);

            if (logApiCalls)
            {
                Debug.Log($"[AIResponseGenerator] System: {systemPrompt}");
                Debug.Log($"[AIResponseGenerator] User: {userMessage.Substring(0, Mathf.Min(200, userMessage.Length))}...");
            }

            // Make OpenAI API call
            yield return SendOpenAIRequest(systemPrompt, userMessage, maxTokens, onTokenStreamed);

            if (logLatencyMetrics)
            {
                var elapsed = (System.DateTime.Now - startTime).TotalMilliseconds;
                Debug.Log($"[AIResponseGenerator] Response generation completed in {elapsed:F1}ms");
            }
        }

        /// <summary>
        /// Send request to OpenAI Chat Completions API
        /// </summary>
        private IEnumerator SendOpenAIRequest(
            string systemPrompt, 
            string userMessage,
            int maxTokens,
            System.Action<string> onTokenStreamed)
        {
            // Get API key from field or environment
            string apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogError("[AIResponseGenerator] OpenAI API key not set. Set it in Inspector or OPENAI_API_KEY environment variable.");
                onTokenStreamed?.Invoke("[Error: API key not configured]");
                yield break;
            }

            // Build JSON payload
            string jsonPayload = BuildOpenAIPayload(systemPrompt, userMessage, maxTokens);

            // Create request
            var request = new UnityWebRequest(OPENAI_API_ENDPOINT, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.timeout = requestTimeoutSeconds;

            if (logApiCalls)
            {
                Debug.Log($"[AIResponseGenerator] POST {OPENAI_API_ENDPOINT}");
                Debug.Log($"[AIResponseGenerator] Model: {openaiModel}, MaxTokens: {maxTokens}");
            }

            yield return request.SendWebRequest();

            // Check for errors
            bool ok;
#if UNITY_2020_2_OR_NEWER
            ok = (request.result == UnityWebRequest.Result.Success);
#else
            ok = (!request.isNetworkError && !request.isHttpError);
#endif

            if (!ok)
            {
                Debug.LogError($"[AIResponseGenerator] API Error: {request.error}");
                Debug.LogError($"[AIResponseGenerator] Response: {request.downloadHandler.text}");
                onTokenStreamed?.Invoke($"[Error: {request.error}]");
                yield break;
            }

            // Parse response
            try
            {
                string responseText = request.downloadHandler.text;
                
                if (logApiCalls)
                {
                    Debug.Log($"[AIResponseGenerator] Response received: {responseText.Substring(0, Mathf.Min(200, responseText.Length))}...");
                }

                string content = ExtractContentFromResponse(responseText);
                
                if (!string.IsNullOrEmpty(content))
                {
                    onTokenStreamed?.Invoke(content);
                    if (logApiCalls)
                    {
                        Debug.Log($"[AIResponseGenerator] Content: {content}");
                    }
                }
                else
                {
                    Debug.LogWarning("[AIResponseGenerator] No content in response");
                    onTokenStreamed?.Invoke("[No response from API]");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIResponseGenerator] Failed to parse response: {e.Message}");
                onTokenStreamed?.Invoke($"[Error parsing response: {e.Message}]");
            }
            finally
            {
                request.Dispose();
            }
        }

        /// <summary>
        /// Build JSON payload for OpenAI Chat Completions API
        /// </summary>
        private string BuildOpenAIPayload(string systemPrompt, string userMessage, int maxTokens)
        {
            var sb = new StringBuilder(1024);
            sb.Append("{");
            sb.Append("\"model\":\"").Append(openaiModel).Append("\",");
            sb.Append("\"temperature\":").Append(temperature.ToString("F1")).Append(",");
            sb.Append("\"max_tokens\":").Append(maxTokens).Append(",");
            sb.Append("\"messages\":[");
            
            // System message
            sb.Append("{\"role\":\"system\",\"content\":\"").Append(EscapeJson(systemPrompt)).Append("\"},");
            
            // User message
            sb.Append("{\"role\":\"user\",\"content\":\"").Append(EscapeJson(userMessage)).Append("\"}");
            
            sb.Append("]");
            sb.Append("}");

            return sb.ToString();
        }

        /// <summary>
        /// Extract content from OpenAI API response JSON
        /// </summary>
        private string ExtractContentFromResponse(string json)
        {
            try
            {
                // Simple JSON parsing without full deserialization
                // Look for "content": "..."
                int contentIndex = json.IndexOf("\"content\"");
                if (contentIndex < 0)
                    return null;

                int colonIndex = json.IndexOf(":", contentIndex);
                if (colonIndex < 0)
                    return null;

                int firstQuote = json.IndexOf("\"", colonIndex);
                if (firstQuote < 0)
                    return null;

                int secondQuote = json.IndexOf("\"", firstQuote + 1);
                while (secondQuote > 0 && json[secondQuote - 1] == '\\')
                {
                    secondQuote = json.IndexOf("\"", secondQuote + 1);
                }

                if (secondQuote < 0)
                    return null;

                string content = json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                
                // Unescape JSON strings
                content = content.Replace("\\\"", "\"");
                content = content.Replace("\\n", "\n");
                content = content.Replace("\\\\", "\\");

                return content;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIResponseGenerator] JSON parsing error: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get API key from field or environment variable
        /// </summary>
        private string GetApiKey()
        {
            // Check field first
            if (!string.IsNullOrWhiteSpace(openaiApiKey))
            {
                Debug.Log("[AIResponseGenerator] Resolved API key source: Inspector field");
                return openaiApiKey;
            }

            // Fall back to environment variable
            string envKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                Debug.Log("[AIResponseGenerator] Resolved API key source: OPENAI_API_KEY environment variable");
                return envKey;
            }

            var connector = FindAnyObjectByType<GPTConnector>();
            if (connector != null && !string.IsNullOrWhiteSpace(connector.apiKey))
            {
                Debug.Log("[AIResponseGenerator] Resolved API key source: GPTConnector.apiKey (scene)");
                return connector.apiKey;
            }

            Debug.LogWarning("[AIResponseGenerator] OpenAI API key could not be resolved from Inspector, ENV, or GPTConnector");
            return null;
        }

        public string GetResolvedApiKey()
        {
            return GetApiKey();
        }

        /// <summary>
        /// Escape special characters for JSON
        /// </summary>
        private string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
