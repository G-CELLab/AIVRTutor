# NewAI System Architecture Summary

## Project Structure

```
AIVRTutor/
├── AiPrototype/                    ← Existing Python RAG system
│   ├── rag_cli.py                  (TF-IDF + OpenAI CLI)
│   ├── knowledge_base/             (Markdown documents)
│   ├── requirements.txt
│   └── tests/
│
├── NewAI/                          ← NEW: Unified AI architecture
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── AITutor.cs          Main NPC controller
│   │   │   ├── AIResponseGenerator.cs
│   │   │   └── TutorSceneState.cs
│   │   ├── RAG/
│   │   │   └── RAGRetriever.cs     Pluggable retrieval backends
│   │   ├── Examples/
│   │   │   └── AITutorExample.cs
│   │   └── RAGBridge.cs            Unity ↔ Backend bridge
│   │
│   ├── knowledge_base/             Shared with AiPrototype
│   ├── rag_integration_server.py   Optional HTTP bridge
│   ├── README.md
│   ├── INTEGRATION_CHECKLIST.md
│   └── .gitignore
│
└── Assets/                         Your existing Unity assets
```

## How It Works

### 1. User speaks query in VR
```
Player: "What is mitosis?"
```

### 2. AITutor processes query
```
AITutor.ProcessUserQuery("What is mitosis?")
  ↓
  Retrieves current scene state (phase, visible objects)
  ↓
  RAGRetriever.RetrieveAsync()
```

### 3. Retrieval layer (pluggable)
```
Option A: LocalRAGService (subprocess)
  └─→ Launches Python rag_cli.py
  └─→ Uses TF-IDF indexing
  
Option B: HttpRAGService (HTTP)
  └─→ Calls http://localhost:5000/retrieve
  └─→ Communicates with rag_integration_server.py
```

### 4. Response generation
```
AIResponseGenerator receives:
  - User query
  - Retrieved chunks
  - Scene context (phase, objects)
  ↓
  Builds OpenAI prompt with context
  ↓
  Streams tokens for low-latency speech synthesis
  ↓
  Returns full response
```

### 5. Response playback
```
AITutor.OnResponseCompleted.Invoke(response)
  ↓
  Scene listens to event
  ↓
  Plays audio / displays text
```

## Implementation Levels

### Level 1: Mock/Testing (Current ✅)
- ✅ Architecture in place
- ✅ Example scripts ready
- ✅ Mock retrieval and responses
- ❌ No external dependencies needed
- ❌ Good for testing scene integration

**Use when**: Prototyping scene flow, testing UI/audio integration

### Level 2: Local Python Backend
- [ ] Implement `LocalRAGService.RetrieveAsync()`
- [ ] Use subprocess to communicate with Python
- [ ] Real TF-IDF retrieval from markdown files
- [ ] Real (but not cached) scene state
- [ ] Mock OpenAI responses still

**Use when**: Testing retrieval quality, optimizing prompt design

### Level 3: Full Integration
- [ ] Add OpenAI API integration
- [ ] Streaming token responses
- [ ] Real latency metrics
- [ ] Audio playback
- [ ] Conversation history

**Use when**: Production deployment

### Level 4: Advanced (Future)
- [ ] Custom LLM (Llama, etc.)
- [ ] Offline mode
- [ ] Multi-language support
- [ ] Advanced caching
- [ ] Analytics logging

## Quick Start: Add to Your Scene

### 1. Create empty GameObject
```
Inspector:
  Name: "TutorNPC"
  Components:
    ✓ Transform
    ✓ AudioSource ← Required
```

### 2. Add NewAI components
```csharp
aiTutorObject.AddComponent<AITutor>();
aiTutorObject.AddComponent<RAGRetriever>();
aiTutorObject.AddComponent<AIResponseGenerator>();
aiTutorObject.AddComponent<RAGBridge>();
```

### 3. Use in your interaction code
```csharp
using NewAI.Scripts;

public class InteractionHandler : MonoBehaviour
{
    void OnPlayerSpeech(string text)
    {
        // Send to AI tutor
        RAGBridge.Instance.ProcessQuery(text);
    }
    
    void OnMitosisPhaseChanged(string phase)
    {
        RAGBridge.Instance.SetMitosisPhase(phase);
    }
}
```

### 4. Listen for responses
```csharp
AITutor tutor = GetComponent<AITutor>();

tutor.OnResponseCompleted.AddListener((response) => 
{
    // Play audio
    PlaySpeech(response);
    // Show text
    DisplaySubtitle(response);
});
```

## File-by-File Reference

| File | Purpose | Status |
|------|---------|--------|
| AITutor.cs | Main controller, orchestrates retrieval + generation | ✅ Done |
| TutorSceneState.cs | Tracks phase, objects, objectives | ✅ Done |
| AIResponseGenerator.cs | Builds prompts, streams responses | ✅ Mock ready |
| RAGRetriever.cs | Abstract retrieval interface | ✅ Interface done |
| RAGBridge.cs | Scene interaction bridge | ✅ Done |
| AITutorExample.cs | Test script with keyboard controls | ✅ Done |
| rag_integration_server.py | Flask HTTP bridge (optional) | ✅ Done |
| README.md | Architecture overview | ✅ Done |
| INTEGRATION_CHECKLIST.md | Step-by-step setup guide | ✅ Done |

## Next Implementation Task

### Priority 1: Local Retrieval Backend
```csharp
// In RAGRetriever.cs -> LocalRAGService.RetrieveAsync()

// TODO: Implement Python subprocess communication
// 1. Spawn: rag_cli.py with IPC mode
// 2. Send query as JSON: {"query": "...", "top_k": 3}
// 3. Receive chunks: [{"source": "...", "text": "...", "score": 0.85}]
// 4. Parse and return as RAGChunk list
```

**Why**: Enables real TF-IDF retrieval, closest to production behavior

### Priority 2: OpenAI Integration
```csharp
// In AIResponseGenerator.cs -> GenerateResponseAsync()

// TODO: Replace mock response with real OpenAI call
// 1. Ensure openaiApiKey is set
// 2. Create OpenAI client (UnityOpenAI SDK or HttpClient)
// 3. Build messages array with system + user prompts
// 4. Stream completion tokens
// 5. Parse and emit OnTokenStreamed events
```

**Why**: Actual conversational responses with scene awareness

### Priority 3: Audio Playback
```csharp
// In AITutor.cs or TutorAudioManager

void PlayTutorResponse(string text)
{
    // Option A: Text-to-Speech
    TextToSpeechService.Speak(text, OnAudioComplete);
    
    // Option B: Pre-recorded audio
    AudioManager.PlayClip("tutor_response_" + hash);
}
```

**Why**: Users need to hear the responses

## Dependencies & Setup

### Python (Optional, for local backend)
```bash
cd AiPrototype
pip install -r requirements.txt
# Adds: openai, grpcio (if needed for other features)
```

### Unity (Built-in, no extra packages needed)
- Uses only standard Unity 2021.3+ APIs
- No external assets required initially
- Optional: UnityWebRequest for HTTP backend

### OpenAI (Optional, for real responses)
```bash
pip install openai  # In AiPrototype
export OPENAI_API_KEY="sk-..."
```

## Testing Workflow

```
1. Open NewAI/README.md
2. Follow INTEGRATION_CHECKLIST.md steps
3. Create test scene with AITutorExample.cs
4. Press SPACE to send queries
5. Press P to cycle through phases
6. Check console for mock responses
7. Ready for Level 2 implementation
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│ UNITY VR SCENE                                                   │
├──────────────────────────────────────────────────────────────┐──┤
│ Player Interaction                                            │  │
│  ├─→ Player speaks query  ──────┐                            │  │
│  ├─→ Player advances phase  ────┤                            │  │
│  └─→ Puzzle solved  ────────────┤                            │  │
│                                  ↓                            │  │
│                           ┌─────────────┐                    │  │
│                           │ AITutorNPC  │                    │  │
│                           │ (GameObject)│                    │  │
│                           └─────────────┘                    │  │
│                                  │                            │  │
│         ┌────────────────────────┼────────────────────────┐  │  │
│         │                        │                        │  │  │
│    ┌────▼─────┐         ┌───────▼────┐         ┌────────▼─┐ │  │
│    │ AITutor  │────────▶│RAGRetriever│         │   AIResp │ │  │
│    │ (Manager)│         │ (Source)   │────┐    │Generator │ │  │
│    └──────────┘         └────────────┘    │    └─────────┬┘ │  │
│         │                                  │              │  │  │
│         │          TutorSceneState         │              │  │  │
│         ├──────────────────────────────────┤              │  │  │
│         │         (phase, objects)         │              │  │  │
│         └────────────────────────────────────┐            │  │  │
│                                              │            │  │  │
│    ┌─────────────────────────────────────────▼─────┐     │  │  │
│    │ Events: OnResponseStarted / Streamed /Done    │     │  │  │
│    └─────────────────────────────────────────┬─────┘     │  │  │
│         ↑                                     │           │  │  │
│         │                    ┌────────────────┘           │  │  │
│         │                    │                            │  │  │
│         └────────────────────┴────────────────────────────┘  │  │
│                                                               │  │
│ Listeners (Scene-specific)                                  │  │
│  ├─→ Play audio/text                                        │  │
│  ├─→ Update UI subtitles                                    │  │
│  └─→ Trigger scene reactions                               │  │
└──────────────────────────────────────────────────────────────┘──┘
        │
        │ Subprocess/HTTP
        ▼
┌─────────────────────────────────────────────────────────────────┐
│ PYTHON BACKEND (Optional)                                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────┐     ┌─────────────────────────┐  │
│  │ LocalRAGIndex (TF-IDF)   │     │ SceneState (JSON)       │  │
│  │  - Load .md files        │     │  - Current phase        │  │
│  │  - Chunk text            │     │  - Visible objects      │  │
│  │  - Rank by relevance     │     │  - Objectives           │  │
│  └──────────────────────────┘     └─────────────────────────┘  │
│           │                                │                    │
│           └────────┬─────────────────────────┘                  │
│                    │                                            │
│              Query + Context                                   │
│                    │                                            │
│                    ▼                                            │
│           ┌──────────────────┐                                 │
│           │  OpenAI API      │                                 │
│           │  (gpt-4o-mini)   │                                 │
│           └────────┬─────────┘                                 │
│                    │                                            │
│              Response Token Stream                             │
│                    │                                            │
│                    ▼                                            │
│           Full Response → Unity                               │
│                                                                │
│  Knowledge Base (markdown)                                     │
│  ├─ interphase.md                                              │
│  ├─ prophase.md                                                │
│  ├─ metaphase.md                                               │
│  └─ anaphase.md                                                │
│                                                                │
└─────────────────────────────────────────────────────────────────┘
```

## Summary

You now have:
- ✅ **NewAI/ folder** with architecture copied from Core
- ✅ **RAGRetriever.cs** that plugs into local or HTTP backends
- ✅ **AITutor.cs** that orchestrates retrieval + generation
- ✅ **Scene context management** (TutorSceneState)
- ✅ **Example scripts** showing integration
- ✅ **Python integration bridge** (rag_integration_server.py)
- ✅ **Step-by-step guides** (README, CHECKLIST)

**Next Steps:**
1. Follow INTEGRATION_CHECKLIST.md
2. Add components to your scene
3. Test with mock responses (keyboard controls)
4. Implement Level 2 (local backend)
5. Add OpenAI integration
6. Add audio playback

Good luck! 🎉
