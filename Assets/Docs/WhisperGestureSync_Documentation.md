# Whisper Gesture Synchronization System

## Overview

This system uses OpenAI's Whisper speech recognition to transcribe AI agent speech in real-time and automatically trigger synchronized gestures based on:
- **Word-level timestamps** from Whisper transcription
- **Phase-appropriate gesture mapping** (Interphase, Prophase, Metaphase, Anaphase, Telophase)
- **Semantic keyword detection** in spoken phrases

## Quick Start

### 1. Download Whisper Model

1. Download a Whisper model (recommended: `whisper-small.bin` or `whisper-base.bin`)
   - From: https://huggingface.co/ggerganov/whisper.cpp/tree/main
2. Place it in: `Assets/StreamingAssets/Whisper/whisper-small.bin`
3. Or update the path in the inspector

### 2. Setup Components

Add to your AI agent GameObject:

```csharp
// 1. Add WhisperGestureSync component
var gestureSync = agent.AddComponent<WhisperGestureSync>();

// 2. Add TTSWhisperIntegration for automatic integration
var integration = agent.AddComponent<TTSWhisperIntegration>();

// 3. Configure references (usually auto-found)
gestureSync.animatorDriver = GetComponent<TTSAnimatorDriver>();
gestureSync.gptConnector = GetComponent<GPTConnector>();
```

### 3. Configure in Inspector

**WhisperGestureSync:**
- Whisper Model Path: `Assets/StreamingAssets/Whisper/whisper-small.bin`
- Animator Driver: (auto-found)
- TTS Audio Source: (auto-found from GPTConnector)
- Enable Phase Gestures: ✓
- Verbose Logging: ✓ (for debugging)

**TTSWhisperIntegration:**
- Auto Transcribe: ✓
- Record From Audio Source: ✓
- Verbose Logging: ✓

## How It Works

### Workflow

1. **TTS Starts Playing**
   - TTSWhisperIntegration detects TTS audio playback
   - Captures the AudioClip

2. **Whisper Transcription**
   - Audio is transcribed with word-level timestamps
   - Each word gets a start/end time

3. **Gesture Mapping**
   - System analyzes transcript for phase-specific keywords
   - Matches phrases to gestures (e.g., "eat food" → DZ13)

4. **Synchronized Playback**
   - Gestures are scheduled at precise timestamps
   - Triggers happen in sync with speech

### Phase-Specific Gesture Mappings

#### Interphase (Energy/Growth)
- **"eat food"**, **"get energy"** → `DZ13` (Eating gesture)
- **"food for energy"** → `DZ13`

#### Prophase (Chromosome Condensation)
- **"condense"** → `DZ18` (X-shape condense)
- **"x shape"**, **"x shaped"** → `DZ18`
- **"condense into x"** → `DZ18`

#### Metaphase (Chromosome Alignment)
- **"line up"** → `DZ20` (Line up at center)
- **"at the center"**, **"in the middle"** → `DZ20`

#### Anaphase (Chromosome Separation)
- **"move to opposite"** → `DZ22` (Split outward)
- **"pull apart"**, **"opposite ends"** → `DZ22`

#### Telophase (Cell Division)
- **"split"**, **"divide"** → `DZ15` (Split gesture)

## Advanced Configuration

### Custom Gesture Mappings

Edit `InitializePhaseGestures()` in `WhisperGestureSync.cs`:

```csharp
phaseGestures[GameManager.GameState.Metaphase] = new List<GestureKeyword>
{
    new GestureKeyword
    {
        phrase = "custom phrase",           // Phrase to detect
        gestureName = "Custom Gesture",     // Display name
        action = () => animatorDriver?.TriggerDZ20(),  // Gesture trigger
        wordOffset = 0.2f,                  // Timing offset (seconds)
        triggerOnLastWord = true            // Trigger on last word of phrase
    }
};
```

### Timing Adjustments

**Global Timing Offset** (`gestureTimingOffset`):
- Positive values: Delay gesture trigger
- Negative values: Trigger earlier
- Default: `0.1f` (100ms delay)

**Per-Keyword Timing** (`wordOffset`):
- Fine-tune timing for specific gestures
- Added to global offset

**Trigger Position** (`triggerOnLastWord`):
- `true`: Trigger at end of phrase (e.g., "line them **up**")
- `false`: Trigger at start of phrase (e.g., "**eat** food")

## Troubleshooting

### No Gestures Triggering

**Check:**
1. Whisper model loaded successfully?
   - Look for: `[WhisperGesture] ✅ Whisper initialized successfully`
2. Current phase has gesture mappings?
   - Look for: `[WhisperGesture] ⏰ Scheduled [gesture] at [time]s`
3. TTSAnimatorDriver assigned?
   - Check inspector references

**Enable verbose logging:**
```csharp
gestureSync.verboseLogging = true;
gestureSync.logWordTimestamps = true;
```

### Gestures Out of Sync

**Adjust timing:**
```csharp
// Global adjustment
gestureSync.gestureTimingOffset = 0.2f; // Add 200ms delay

// Or edit specific keyword offsets in InitializePhaseGestures()
```

**Check audio sample rate:**
- Whisper expects 16kHz mono audio
- Automatic conversion happens but may introduce latency

### Whisper Fails to Initialize

**Check model path:**
```csharp
gestureSync.whisperModelPath = "Assets/StreamingAssets/Whisper/whisper-small.bin";
```

**Verify model file:**
- Must be `.bin` format from whisper.cpp
- Must match Whisper.unity package requirements

### No Word Timestamps

**Enable timestamps in Whisper:**
```csharp
gestureSync.enableWordTimestamps = true;
```

**Note:** Not all Whisper models support word-level timestamps. Use these models:
- `whisper-small.bin` ✓
- `whisper-base.bin` ✓
- `whisper-tiny.bin` ✓

## API Reference

### WhisperGestureSync

#### Methods

```csharp
// Manually transcribe and schedule gestures
void TranscribeAndScheduleGestures(AudioClip clip)

// Stop all scheduled gestures
void StopGesturePlayback()
```

#### Properties

```csharp
string whisperModelPath          // Path to Whisper model
TTSAnimatorDriver animatorDriver // Gesture controller
float gestureTimingOffset        // Global timing offset
bool enablePhaseGestures         // Enable phase-specific mapping
bool verboseLogging              // Debug logging
```

### TTSWhisperIntegration

#### Methods

```csharp
// Manually trigger transcription
void TranscribeClip(AudioClip clip)
```

#### Properties

```csharp
bool autoTranscribe              // Auto-detect TTS playback
bool recordFromAudioSource       // Record from AudioSource
int sampleRate                   // Audio sample rate (16000)
```

## Performance Notes

### Whisper Model Size vs Speed

| Model | Size | Speed | Quality |
|-------|------|-------|---------|
| tiny  | 75MB | Very Fast | Good |
| base  | 142MB | Fast | Better |
| small | 466MB | Medium | Best |

**Recommendation:** Start with `whisper-base.bin` for best balance.

### Optimization Tips

1. **Transcribe once per TTS clip** (already handled by integration)
2. **Limit keyword complexity** (simple phrases = faster matching)
3. **Use `confidenceThreshold`** to filter low-quality matches

## Integration with Existing Systems

### With GPTConnector

Already integrated! The system:
- Uses `GameManager.eGameStatus` for phase detection
- Respects phase-based gesture rules
- Works alongside GPTConnector's existing transcript-based triggers

### With TTSAnimatorDriver

Direct integration via trigger methods:
- `TriggerDZ7()`, `TriggerDZ8()` - Affirmations
- `TriggerDZ13()` - Eat/Energy (Interphase)
- `TriggerDZ18()` - X-shape Condense (Prophase)
- `TriggerDZ20()` - Line Up (Metaphase)
- `TriggerDZ22()` - Split Outward (Anaphase)
- `TriggerDZ13()`, `TriggerDZ15()` - Split/Divide (Telophase)

## Example Usage

### Basic Setup

```csharp
public class AgentSetup : MonoBehaviour
{
    void Start()
    {
        // Get components
        var gestureSync = GetComponent<WhisperGestureSync>();
        var animatorDriver = GetComponent<TTSAnimatorDriver>();
        
        // Configure
        gestureSync.whisperModelPath = "Assets/StreamingAssets/Whisper/whisper-small.bin";
        gestureSync.animatorDriver = animatorDriver;
        gestureSync.enablePhaseGestures = true;
        gestureSync.gestureTimingOffset = 0.15f; // 150ms delay
    }
}
```

### Manual Transcription

```csharp
public class ManualTest : MonoBehaviour
{
    public AudioClip testClip;
    public WhisperGestureSync gestureSync;
    
    public void TestTranscription()
    {
        gestureSync.TranscribeAndScheduleGestures(testClip);
    }
}
```

### Custom Phase Gestures

```csharp
// Add to WhisperGestureSync.InitializePhaseGestures()
phaseGestures[GameManager.GameState.Interphase].Add(new GestureKeyword
{
    phrase = "mitochondria",
    gestureName = "Custom Energy",
    action = () => {
        animatorDriver?.TriggerDZ13();
        Debug.Log("Mitochondria mentioned!");
    },
    wordOffset = 0.1f
});
```

## Future Enhancements

Planned features:
- [ ] Real-time streaming transcription
- [ ] Multi-language support
- [ ] Custom gesture timing curves
- [ ] Gesture combination sequences
- [ ] Confidence-based gesture selection

## Credits

- **Whisper.unity**: OpenAI Whisper integration for Unity
- **whisper.cpp**: High-performance Whisper implementation
- **OpenAI Whisper**: Original speech recognition model

## Support

For issues or questions:
1. Check verbose logging output
2. Verify Whisper model is loaded
3. Confirm phase-gesture mappings match your needs
4. Adjust timing offsets for synchronization

**Debug command:**
```csharp
gestureSync.verboseLogging = true;
gestureSync.logWordTimestamps = true;
```

This will output detailed logs showing:
- Whisper initialization
- Transcription results
- Word timestamps
- Scheduled gestures
- Trigger events
