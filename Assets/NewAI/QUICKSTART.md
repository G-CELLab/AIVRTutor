# Quick Reference Guide

## File Locations Map

```
AIVRTutor/
├── NewAI/                       ← START HERE
│   ├── README.md               (Overview)
│   ├── ARCHITECTURE.md         (Detailed design)
│   ├── INTEGRATION_CHECKLIST.md (Step-by-step setup)
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── AITutor.cs           ← Main NPC component
│   │   │   ├── TutorSceneState.cs   ← Scene context
│   │   │   └── AIResponseGenerator.cs ← Response generation
│   │   ├── RAG/
│   │   │   └── RAGRetriever.cs      ← Retrieval abstraction
│   │   ├── Examples/
│   │   │   └── AITutorExample.cs    ← Test with keyboard
│   │   └── RAGBridge.cs             ← Scene controller
│   ├── knowledge_base/          (Empty, link to AiPrototype)
│   ├── rag_integration_server.py (Optional HTTP bridge)
│   └── requirements-python.txt
│
├── AiPrototype/                 ← Existing Python RAG
│   ├── rag_cli.py               (Main RAG system)
│   ├── knowledge_base/          (Markdown documents)
│   └── requirements.txt
│
└── Assets/
    └── Scenes/
        └── YourScene.unity      (Add AITutor here)
```

## Unity Scene Setup (5 minutes)

### Step 1: Create AI Tutor GameObject
```
Right-click in Hierarchy
  └─ Create Empty
     └─ Name: "AITutorNPC"
```

### Step 2: Add Components
```
Inspector → Add Component:
  ✓ Audio Source
  ✓ AITutor (NewAI.Scripts.Core)
  ✓ RAGRetriever (NewAI.Scripts.RAG)
  ✓ AIResponseGenerator (NewAI.Scripts.Core)
  ✓ RAGBridge (NewAI.Scripts)
```

### Step 3: Configure OpenAI API Key
```
Select AIResponseGenerator component:
  OpenAI Configuration section:
    ├─ Openai Api Key → Paste your key here
    │  (same key used in GPTConnector)
    └─ Openai Model → gpt-4o-mini (recommended)
```

### Step 4: Wire Up References
```
Select "AITutorNPC" in Hierarchy
Inspector:
  RAGBridge component:
    ├─ AI Tutor → Drag "AITutorNPC" here
    └─ Use Python Backend → false (start with mock)
```

### Step 5: Test with Example
```
Add to same GameObject:
  ✓ AITutorExample (NewAI.Scripts.Examples)

Play Scene:
  ├─ Press SPACE → Send test query
  ├─ Press P → Cycle through phases
  └─ Check Console for output
```

### Step 5: Connect Your Interaction Handler
```csharp
using NewAI.Scripts;

public class MitosisInteractionHandler : MonoBehaviour
{
    void OnPlayerAsksQuestion(string question)
    {
        RAGBridge.Instance.ProcessQuery(question);
    }
    
    void OnPhaseChanged(string newPhase)
    {
        RAGBridge.Instance.SetMitosisPhase(newPhase);
    }
    
    void Start()
    {
        var tutor = GetComponent<AITutor>();
        
        tutor.OnResponseCompleted.AddListener((response) =>
        {
            Debug.Log($"Tutor says: {response}");
            // Play audio, update UI, etc.
        });
    }
}
```

## Common Workflows

### Workflow A: Just Want Mock Responses (Now)
```
1. Add AITutor components to GameObject
2. Run AITutorExample
3. Press SPACE to test
4. No API key needed yet
✓ Ready in < 5 minutes
```

### Workflow B: Want Real AI Responses with RAG (10 min)
```
1. Complete Workflow A
2. Get your OpenAI API key (or use same key as GPTConnector)
3. Set API key in AIResponseGenerator Inspector
4. Copy knowledge_base from AiPrototype to NewAI/
5. Test with SPACE - now get real GPT-4o-mini responses with RAG context!
✓ Full integration working
```

### Workflow C: Want Local RAG Retrieval (Advanced, 30 min)
```
1. Complete Workflow B (basic real responses work)
2. Implement LocalRAGService.RetrieveAsync() with Python subprocess
3. Use real TF-IDF retrieval instead of mock
4. Test that knowledge base chunks are ranked properly
```

## Component Reference

### AITutor
Main orchestrator. Handles query flow and scene state.

**Public Methods:**
```csharp
ProcessUserQuery(string query)      // Send user query
SetPhase(string phaseName)          // Update cell phase
SetVisibleObjects(string desc)      // Update scene objects
GetSceneState()                     // Get current state
IsProcessing { get; }               // Check if busy
```

**Events:**
```csharp
OnResponseStarted(string query)     // User query received
OnResponseStreamed(string token)    // Token from LLM
OnResponseCompleted(string response) // Full response ready
OnErrorOccurred(string error)       // Error occurred
```

### RAGRetriever
Pluggable retrieval backend.

**Inspector Settings:**
- `ragServiceUrl` - HTTP endpoint (if using HttpRAGService)
- `useLocalPythonProcess` - True = subprocess, False = HTTP
- `defaultTopK` - Number of chunks to retrieve

**Backends:**
- `LocalRAGService` - Subprocess to Python
- `HttpRAGService` - HTTP POST to Flask server

### AIResponseGenerator
Builds prompts and streams responses.

**Inspector Settings:**
- `openaiApiKey` - Your API key
- `openaiModel` - Model to use (gpt-4o-mini, gpt-4)
- `temperature` - 0-1, lower = more focused
- `streamTokens` - Enable token streaming
- `logLatencyMetrics` - Print timing info

## Debugging Tips

### See what's being retrieved
```csharp
AITutor settings.showDebugContext = true;
// Console will show retrieved chunks
```

### Check response latency
```csharp
AIResponseGenerator logLatencyMetrics = true;
// Console shows: retrieval=12ms, first_token=456ms, total=2100ms
```

### Test retrieval independently
```bash
cd ../AiPrototype
python rag_cli.py
/status
you> what is prophase?
```

### Monitor Python backend
```bash
# If using HTTP bridge
python NewAI/rag_integration_server.py --port 5000

# Test endpoint
curl http://localhost:5000/health
curl -X POST http://localhost:5000/retrieve \
  -H "Content-Type: application/json" \
  -d '{"query":"prophase","top_k":3}'
```

## Performance Targets

| Metric | Current | Target | Notes |
|--------|---------|--------|-------|
| Retrieval | 10-50ms | <50ms | Local TF-IDF |
| First token | Mock | 300-500ms | OpenAI latency |
| Response time | 100ms | <2s total | Streaming helps |
| Memory | < 50MB | < 200MB | Full KB indexed |

## Next Steps After Setup

1. **Test mock responses** (5 min)
   - Use AITutorExample to verify architecture

2. **Implement local retrieval** (30 min)
   - Write LocalRAGService.RetrieveAsync()
   - Use real TF-IDF from Python

3. **Add OpenAI integration** (30 min)
   - Get API key
   - Implement HTTP request to OpenAI
   - Add streaming and token tracking

4. **Add audio** (1 hour)
   - Text-to-speech or pre-recorded
   - Play response as audio
   - Update UI subtitles

5. **Scene integration** (varies)
   - Wire to your existing interaction system
   - Update scene based on tutor responses
   - Add conversation history

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "AITutor component not found" | Attach all required components |
| "No chunks retrieved" | Check knowledge_base has .md files |
| "Response is always mock" | Set useLocalPythonProcess = true |
| "Python process won't start" | Run `python --version` to check install |
| "API key error" | Set OPENAI_API_KEY environment var |
| "Slow retrieval" | Profile in Python CLI with `/reindex` |

## Support

- **Architecture questions** → Read ARCHITECTURE.md
- **Setup issues** → Check INTEGRATION_CHECKLIST.md
- **How RAG works** → See ../AiPrototype/README.md
- **Component docs** → Hover over fields in Inspector

---

**You're ready!** Start with Workflow A and work up from there.
