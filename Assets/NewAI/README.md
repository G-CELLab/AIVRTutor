# NewAI: Integrated RAG + Core Architecture

This folder combines the Convai Core architecture with your local RAG system for VR tutoring.

## Architecture Overview

```
NewAI/
├── Scripts/
│   ├── Core/
│   │   ├── AITutor.cs          ← Main NPC controller (replaces ConvaiNPC)
│   │   ├── AIResponseGenerator.cs ← Response streaming with RAG context
│   │   └── TutorSceneState.cs  ← Scene context management
│   ├── RAG/
│   │   └── RAGRetriever.cs     ← Local/HTTP RAG service interface
│   └── RAGBridge.cs            ← Unity ↔ Python bridge
├── knowledge_base/             ← Markdown knowledge files
└── README.md
```

## Key Differences from Core

| Feature | Original Core | NewAI |
|---------|---------------|-------|
| Backend | Convai gRPC API | Local TF-IDF + OpenAI (same key as your GPTConnector) |
| Knowledge | External API | Local markdown files |
| Retrieval | None (streaming only) | Local TF-IDF ranking |
| Latency | Network dependent | Fast local retrieval |
| Cost | Per-token API calls | Minimal (local indexing) |
| API Key | Convai API | OpenAI API (your existing setup) |

## 🔐 OpenAI API Key Setup (Same as GPTConnector)

You already have OpenAI configured in `Assets/Scripts/AI/GPTConnector.cs`. NewAI uses the same approach:

**Option A: Inspector (Recommended)**
1. Open your scene with **AITutor** GameObject
2. Select the GameObject with `AIResponseGenerator` component
3. In Inspector, find **OpenAI Configuration**
4. Paste your API key in the **Openai Api Key** field
5. Save the scene

**Option B: Environment Variable**
- Set `OPENAI_API_KEY` environment variable on your system
- NewAI will automatically use it if the field is empty
- This is secure for production builds

**Option C: Code**
```csharp
var generator = GetComponent<AIResponseGenerator>();
generator.openaiApiKey = "sk-proj-...";
```

## Quick Start

### 1. Setup Unity Scene

Add **AITutor** to your NPC GameObject:
```csharp
// In your scene setup script:
AITutor tutor = npcObject.AddComponent<AITutor>();
tutor.SetPhase("interphase");
tutor.SetVisibleObjects("Nutrient capsules, DNA strands, centrioles");

// Listen for responses:
tutor.OnResponseStarted.AddListener(query => Debug.Log($"Processing: {query}"));
tutor.OnResponseStreamed.AddListener(chunk => Debug.Log(chunk));
tutor.OnResponseCompleted.AddListener(response => PlayTutorAudio(response));
```

### 2. Connect to Python Backend

The Python RAG system is located in `../AiPrototype/rag_cli.py`.

**Option A: Direct Python Process (Recommended)**
- Set `AIResponseGenerator.useLocalPythonProcess = true`
- Will launch and manage the Python process automatically

**Option B: HTTP Service**
- Run: `python rag_cli.py --serve --port 5000`
- Set `AIResponseGenerator.ragServiceUrl = "http://localhost:5000"`
- Set `useLocalPythonProcess = false`

### 3. Add Knowledge Base

Create markdown files in `knowledge_base/`:
```markdown
# interphase.md
During interphase, the cell prepares for division...

# prophase.md
In prophase, the nuclear envelope breaks down...
```

Copy or link from `../AiPrototype/knowledge_base/`.

## Integration Points

### RAGRetriever
Implements two backends:

**LocalRAGService**
- Communicates with Python via subprocess or IPC
- Real TF-IDF retrieval
- Currently mocks response (TODO: implement IPC)

**HttpRAGService**
- Posts queries to HTTP endpoint
- Useful for distributed setups
- Currently mocks response (TODO: implement HTTP)

### AIResponseGenerator
- Builds OpenAI messages with scene context
- Streams tokens for low-latency speech synthesis
- Supports both streaming and buffered responses

### TutorSceneState
- Tracks current phase, visible objects, objectives
- Serializes to JSON for Python backend
- Matches `SceneState` from `rag_cli.py`

## Development Roadmap

**Phase 1 (Current)**
- ✅ Architecture setup
- ✅ Mock retrieval/generation
- ❌ Real IPC with Python backend
- ❌ OpenAI integration

**Phase 2**
- [ ] Implement Python subprocess communication
- [ ] Real TF-IDF retrieval
- [ ] OpenAI streaming integration
- [ ] Latency metrics

**Phase 3**
- [ ] Audio playback of responses
- [ ] Conversation history management
- [ ] Phase-aware response ranking
- [ ] Offline mode (no OpenAI)

## Testing

Run the Python prototype to verify RAG works:
```bash
cd ../AiPrototype
python rag_cli.py
```

Commands in the prototype:
- `/phase <name>` - Set current phase
- `/status` - Show current state
- `/reindex` - Reload knowledge base
- `/exit` - Quit

## Troubleshooting

**"RAG Service not initialized"**
- Ensure RAGRetriever component is attached to the same GameObject

**"Python process failed to start"**
- Check that Python 3.9+ is installed
- Verify `../AiPrototype/requirements.txt` is satisfied
- Check console logs for subprocess errors

**No retrieved chunks**
- Verify `knowledge_base/` has .md files
- Run `/reindex` command in Python prototype
- Check that queries match knowledge base content

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Unity (AIVRTutor)                         │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐         ┌──────────────────┐              │
│  │   AITutor    │◄────────┤  RAGBridge       │              │
│  │  (Main NPC)  │         │  (UI Controller) │              │
│  └──────┬───────┘         └──────────────────┘              │
│         │                                                    │
│  ┌──────▼─────────────────────────────────────────┐         │
│  │  AIResponseGenerator                           │         │
│  │  - Builds OpenAI messages                      │         │
│  │  - Streams tokens                              │         │
│  │  - Manages latency metrics                     │         │
│  └──────┬──────────────────────────┬──────────────┘         │
│         │                          │                        │
│    ┌────▼─────┐            ┌──────▼──────────┐             │
│    │RAGRetriever         │  OpenAI API      │             │
│    │(LocalService)       │  (For responses) │             │
│    └────┬─────┘            └──────────────────┘             │
│         │                                                    │
├─────────┼────────────────────────────────────────────────────┤
│         │  IPC / HTTP                                       │
├─────────┼────────────────────────────────────────────────────┤
│         │ Python Backend                                    │
│    ┌────▼─────────────────────────┐                        │
│    │  rag_cli.py                  │                        │
│    │  - LocalRAGIndex (TF-IDF)    │                        │
│    │  - Knowledge base loading    │                        │
│    │  - Query retrieval           │                        │
│    │  - Scene state management    │                        │
│    │  - OpenAI integration        │                        │
│    └──────────────────────────────┘                        │
│         │                                                   │
│    ┌────▼──────────────┐                                   │
│    │ knowledge_base/   │                                   │
│    │ *.md files        │                                   │
│    └───────────────────┘                                   │
└─────────────────────────────────────────────────────────────┘
```

## References

- **Original Core**: `/ConvaiDemo/Assets/Convai/Scripts/Runtime/Core/`
- **Python RAG**: `../AiPrototype/rag_cli.py`
- **Knowledge Base**: `knowledge_base/` (or symlink to `../AiPrototype/knowledge_base/`)
