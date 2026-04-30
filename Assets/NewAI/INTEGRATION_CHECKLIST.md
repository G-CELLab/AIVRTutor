# Integration Checklist

## Setup Steps

### 1. Create Example Scene

- [ ] Create new Unity scene
- [ ] Create empty GameObject called "AITutor"
- [ ] Add AudioSource component
- [ ] Add AITutor.cs component
- [ ] Add RAGRetriever.cs component
- [ ] Add AIResponseGenerator.cs component
- [ ] Add RAGBridge.cs component
- [ ] Assign AITutor reference to RAGBridge

### 2. Configure Components

**AITutor**
- [ ] Set Character Name (e.g., "Biology Tutor")
- [ ] Set TopK = 3
- [ ] Set Max Output Tokens = 80
- [ ] Enable Show Debug Context initially

**RAGRetriever**
- [ ] Set RAG Service URL (if using HTTP)
- [ ] Choose Local Process or HTTP service
- [ ] Point to knowledge_base directory

**AIResponseGenerator (★ Important)**
- [ ] Set **Openai Api Key** to your API key
  - Copy from your GPTConnector if you have one
  - Or get from: https://platform.openai.com/api-keys
- [ ] Set Model = gpt-4o-mini (recommended)
- [ ] Set Request Timeout Seconds = 30
- [ ] Enable Log Api Calls = true (for debugging)
- [ ] Enable Log Latency Metrics = true

### 3. Knowledge Base

- [ ] Copy markdown files to `knowledge_base/`
- [ ] Or symlink to `../AiPrototype/knowledge_base/`
- [ ] Files should have .md extension
- [ ] Include phase-specific guidance

Example knowledge base structure:
```
knowledge_base/
├── interphase.md
├── prophase.md
├── metaphase.md
├── anaphase.md
├── telophase.md
└── general_mitosis.md
```

### 4. Test in Play Mode

- [ ] Enter Play mode
- [ ] Press SPACE to send test query (using AITutorExample)
- [ ] Check console for API call logs
- [ ] Verify response is generated (real GPT response, not mock!)
- [ ] Check latency metrics
- [ ] Try different queries and phases

### 5. Integration into Scene

Add to your existing scene:
```csharp
// Get or create the AITutor
var aiTutor = GetComponent<AITutor>();

// Set scene context based on current mitosis phase
aiTutor.SetPhase("prophase");
aiTutor.SetVisibleObjects("Spindle fibers forming, chromosomes condensing");

// Process player query
aiTutor.ProcessUserQuery("What should I do now?");

// Handle response via events
aiTutor.OnResponseCompleted.AddListener(response => {
    // Play audio, display text, etc.
});
```

## Python Backend Integration

### Option A: Local Process (Automatic)

1. Ensure Python 3.9+ installed
2. Run once to verify: `cd ../AiPrototype && python rag_cli.py`
3. Set `AIResponseGenerator.useLocalPythonProcess = true`
4. Unity will spawn the process automatically

### Option B: HTTP Service (Manual)

1. Start backend: `python ../AiPrototype/rag_cli.py --serve --port 5000`
2. Set `AIResponseGenerator.ragServiceUrl = "http://localhost:5000"`
3. Set `AIResponseGenerator.useLocalPythonProcess = false`

### Option C: Direct Integration (Advanced)

For maximum performance, implement direct IPC:
1. Use named pipes or sockets
2. Serialize queries as JSON
3. Deserialize responses
4. See `LocalRAGService` class for integration point

## TODO: Implementation Tasks

### High Priority (OpenAI already done! ✅)
- [x] Implement `AIResponseGenerator` with real OpenAI HTTP calls
- [x] Add Bearer token authentication
- [x] Build proper JSON payload for Chat Completions API
- [x] Parse OpenAI responses
- [ ] Implement `LocalRAGService.RetrieveAsync()` with subprocess IPC
- [ ] Use real TF-IDF retrieval from Python backend
- [ ] Test with actual knowledge base files

### Medium Priority
- [ ] Conversation history tracking (optional for simple tutoring)
- [ ] Phase-aware response ranking
- [ ] Error handling and retry logic
- [ ] Add real audio playback support

### Low Priority
- [ ] Offline mode support (local LLM)
- [ ] Custom LLM integration
- [ ] Multi-language support
- [ ] Advanced caching

## Performance Targets

| Metric | Target | Notes |
|--------|--------|-------|
| Retrieval latency | < 50ms | TF-IDF indexing |
| First token latency | < 500ms | OpenAI API |
| Total response time | < 2s | Including streaming |
| Memory usage | < 200MB | Full knowledge base |

## Debugging

### Enable verbose logging:
```csharp
// In AITutor
settings.showDebugContext = true;

// In AIResponseGenerator
logLatencyMetrics = true;
```

### Common issues and fixes:

**"No chunks retrieved"**
- Check knowledge_base/ has .md files
- Verify TF-IDF indexing in Python backend
- Try `/reindex` in Python RAG CLI

**"API key not found"**
- Ensure OpenAI API key set as environment variable
- Check AIResponseGenerator openaiApiKey field

**"Python process not starting"**
- Verify Python installation: `python --version`
- Check subprocess error logs
- Try manual HTTP service instead

**"Responses are slow"**
- Check latency metrics breakdown
- Profile Python retrieval performance
- Consider increasing chunk overlap

## Architecture Evolution

Current:
```
Unity ─┬─→ Mock RAGRetriever
       ├─→ Mock AIResponseGenerator
       └─→ Mock responses
```

Target:
```
Unity ─┬─→ Real RAGRetriever ─→ Python rag_cli.py (TF-IDF)
       ├─→ Real AIResponseGenerator ─→ OpenAI API
       └─→ Streaming responses
```

## Next Steps

1. Choose Python integration method (subprocess vs HTTP)
2. Implement RAGRetriever IPC protocol
3. Get OpenAI API key and integrate
4. Test with real knowledge base
5. Add audio playback
6. Integrate into main VR scene
