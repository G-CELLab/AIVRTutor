# NewAI ↔ GPTConnector Integration Guide

## Overview

Your existing **GPTConnector** (in `Assets/Scripts/AI/`) is a mature, production-grade AI system. **NewAI** is a lightweight RAG-first alternative that reuses your OpenAI API setup.

| Component | Purpose | Status |
|-----------|---------|--------|
| **GPTConnector** | Full-featured: Realtime API, TTS, gestures, complex prompting | ✅ In Use |
| **NewAI** | Lightweight: Local RAG + GPT, simple scene control | ✅ New, Compatible |

## API Key Sharing

Both systems use the **same OpenAI API key** with the same Bearer authentication:

```csharp
// GPTConnector (existing)
public string apiKey = ""; // Set in Inspector
// Uses: "Bearer " + apiKey for HTTP requests

// AIResponseGenerator (NewAI)
public string openaiApiKey = ""; // Set in Inspector
// Uses: "Bearer " + openaiApiKey for HTTP requests
```

**Setup:** Just copy your API key between the two Inspector fields.

## Architecture Comparison

### GPTConnector Flow
```
User Speech → OpenAISpeechRecognizer
             ↓
         GPTConnector
             ↓
    ┌────────┴──────────┐
    ↓                   ↓
 HTTP API          Realtime WS
    ↓                   ↓
OpenAI (gpt-4o-mini)   (Real-time audio)
    ↓
History + Conversation
    ↓
TTS + Gestures + UI
```

### NewAI Flow
```
Player Question → AITutor (Scene)
                    ↓
            RAGRetriever (Mock)
                    ↓
        LocalRAGIndex (TF-IDF)
                    ↓
         AIResponseGenerator
                    ↓
            UnityWebRequest
                    ↓
        OpenAI (gpt-4o-mini)
                    ↓
        OnResponseCompleted Event
                    ↓
        Scene Listener (TTS/UI)
```

## When to Use Which

### Use GPTConnector When:
- ✅ Real-time audio streaming is needed
- ✅ Complex prompt history and context management
- ✅ Phase pre-computation and caching
- ✅ Advanced gesture/animation synchronization
- ✅ Multi-turn conversation memory

### Use NewAI When:
- ✅ Simple Q&A without conversation history
- ✅ Local knowledge base retrieval (RAG) is priority
- ✅ Want to test RAG quality before full integration
- ✅ Need minimal latency (no history overhead)
- ✅ Building a RAG prototype

## Integration Scenarios

### Scenario 1: Coexist (Recommended)
Both systems run independently:

```
AIVRTutor Project/
├── Assets/Scripts/AI/
│   ├── GPTConnector.cs  (for full-featured tutoring)
│   └── [existing code]
│
└── NewAI/              (for RAG prototyping)
    ├── Scripts/
    │   ├── AITutor.cs
    │   ├── AIResponseGenerator.cs
    │   └── ...
    └── knowledge_base/ (symlink or copy)
```

**Use Case:** Test NewAI's RAG in a separate scene while GPTConnector runs in the main scene.

### Scenario 2: Gradual Migration
Start with NewAI for RAG, upgrade to GPTConnector features later:

```
Phase 1: Use NewAI (2 weeks)
  ├─ Get RAG working
  ├─ Test knowledge base quality
  ├─ Simple Q&A in scene

Phase 2: Migrate to GPTConnector (2 weeks)
  ├─ Add history/conversation
  ├─ Add real-time audio
  ├─ Add gesture sync

Phase 3: Hybrid (ongoing)
  ├─ GPTConnector for main AI
  ├─ NewAI for debugging/testing
```

### Scenario 3: RAG Enhancement to GPTConnector
Feed NewAI's RAG output into GPTConnector:

```csharp
// In your scene
void OnPlayerQuestion(string question)
{
    // Step 1: Get RAG context from NewAI
    var ragChunks = newaiTutor.GetLastRetrievedChunks();
    string ragContext = FormatChunksAsPrompt(ragChunks);
    
    // Step 2: Send to GPTConnector with RAG context
    gptConnector.SetPendingUserPrompt(ragContext);
    gptConnector.SendToGPT(question, OnResponse);
}
```

## API Key Management

### Setting the Key

**In Editor:**
1. Both **GPTConnector** and **AIResponseGenerator** have `apiKey`/`openaiApiKey` fields
2. Set them to the same value in Inspector
3. Save scene

**In Code:**
```csharp
// GPTConnector
public string apiKey;

// AIResponseGenerator
public string openaiApiKey;

// Or use environment variable
System.Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-proj-...");
```

**Security:**
- ⚠️ **Never hardcode** in code
- ✅ Use Inspector for editor/testing
- ✅ Use environment variable for builds
- ✅ Use secure credential manager for production

### Validating Setup

```csharp
// Check if key is set (from either source)
public bool IsApiKeyConfigured(AIResponseGenerator generator)
{
    return !string.IsNullOrWhiteSpace(generator.openaiApiKey) ||
           !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
}
```

## API Endpoint Compatibility

Both systems call the same endpoint with the same authorization:

```csharp
// Both use this endpoint:
const string OPENAI_API_ENDPOINT = "https://api.openai.com/v1/chat/completions";

// Both use this header:
request.SetRequestHeader("Authorization", "Bearer " + apiKey);

// Both use these models:
- gpt-4o-mini (recommended, fast, cheap)
- gpt-4o (more accurate)
- gpt-4-turbo
```

## Message Format

### GPTConnector
```json
{
  "model": "gpt-4o-mini",
  "temperature": 0.2,
  "messages": [
    {"role": "system", "content": "You are..."},
    {"role": "user", "content": "..."}
  ]
}
```

### NewAI
```json
{
  "model": "gpt-4o-mini",
  "temperature": 0.2,
  "max_tokens": 150,
  "messages": [
    {"role": "system", "content": "You are... in {phase}"},
    {"role": "user", "content": "phase={phase} | scene=... | kb=[...] | User: ..."}
  ]
}
```

**Key Difference:** NewAI includes RAG context in the user message.

## Response Parsing

Both use similar JSON extraction patterns:

```csharp
// Both extract from:
// response.choices[0].message.content

private string ExtractContentFromResponse(string json)
{
    int contentIndex = json.IndexOf("\"content\"");
    // ... find quoted value ...
    return content;
}
```

## Performance Comparison

| Metric | GPTConnector | NewAI |
|--------|--------------|-------|
| API Latency | 300-800ms | 300-800ms (same API) |
| Retrieval | N/A | 10-50ms (local TF-IDF) |
| History Overhead | 200-500ms (per turn) | Minimal |
| Gesture Sync | Yes (complex) | No (simple) |
| TTS Integration | Built-in | Via events |
| Total Response | 1-3s | 500ms-1.5s |

## Troubleshooting

### "Unauthorized" Error
```
Problem: 401 error from OpenAI
Solution: 
  1. Check API key is correct
  2. Ensure "Bearer " prefix in request
  3. Verify key has API access enabled
```

### "API key not set"
```
Problem: Component says key is missing
Solution:
  1. Check Inspector field is populated
  2. Or set environment variable: OPENAI_API_KEY=sk-...
  3. Environment takes precedence if field is empty
```

### "Model not found"
```
Problem: gpt-4o-mini not available
Solution:
  1. Use gpt-4o instead
  2. Check API subscription includes model
  3. Use `curl https://api.openai.com/v1/models -H "Authorization: Bearer $OPENAI_API_KEY"`
```

### Different Responses
```
Problem: Same prompt gives different outputs
Solution:
  1. Temperature is set to 0.2 (slightly creative)
  2. Each API call is independent
  3. Use temperature: 0.0 for deterministic
  4. Check RAG context is identical
```

## Sharing RAG Results

If you want GPTConnector to use NewAI's RAG retrieval:

```csharp
public class RagContextBridge
{
    public static string GetRagContextForGpt(string userQuery, string currentPhase)
    {
        var newaiRetriever = FindObjectOfType<RAGRetriever>();
        
        // Synchronously get chunks (implement blocking version)
        List<RAGChunk> chunks = newaiRetriever.RetrieveSync(userQuery, topK: 3);
        
        // Format for GPTConnector
        var lines = new List<string>();
        foreach (var chunk in chunks)
        {
            lines.Add($"- {chunk.Text}");
        }
        
        return "KNOWLEDGE BASE:\n" + string.Join("\n", lines);
    }
}

// In your scene:
void OnUserSpeech(string text)
{
    string ragContext = RagContextBridge.GetRagContextForGpt(text, currentPhase);
    gptConnector.SetPhasePromptOverride(ragContext, enabled: true);
    gptConnector.SendToGPT(text, OnResponse);
}
```

## Next Steps

1. **Test NewAI in a separate scene** ← Start here
2. **Verify API key works** with both systems
3. **Copy knowledge base** to NewAI/knowledge_base/
4. **Compare responses** from both systems
5. **Decide integration strategy** (coexist/migrate/hybrid)
6. **Document findings** for team

## Useful Commands

### Test OpenAI API directly
```bash
curl https://api.openai.com/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-proj-..." \
  -d '{
    "model": "gpt-4o-mini",
    "messages": [{"role": "user", "content": "test"}]
  }'
```

### Check available models
```bash
curl https://api.openai.com/v1/models \
  -H "Authorization: Bearer sk-proj-..."
```

### Monitor API usage
```
https://platform.openai.com/usage/
```

## References

- **GPTConnector**: [Assets/Scripts/AI/GPTConnector.cs](../Assets/Scripts/AI/GPTConnector.cs)
- **NewAI AIResponseGenerator**: [NewAI/Scripts/Core/AIResponseGenerator.cs](Scripts/Core/AIResponseGenerator.cs)
- **OpenAI Docs**: https://platform.openai.com/docs
- **API Models**: https://platform.openai.com/docs/models
