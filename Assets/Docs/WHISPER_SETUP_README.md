# Whisper Gesture Control - Quick Setup

## What This Does

Automatically synchronizes your AI agent's gestures with speech using:
- ✅ **Word-level timestamps** from Whisper transcription
- ✅ **Phase-aware gesture mapping** (Interphase, Prophase, etc.)
- ✅ **Automatic TTS integration** (works with your existing system)
- ✅ **Zero code changes required** for basic usage

## Installation (5 Minutes)

### Step 1: Get Whisper Model

1. Download `whisper-small.bin` from: https://huggingface.co/ggerganov/whisper.cpp/tree/main
2. Create folder: `Assets/StreamingAssets/Whisper/`
3. Place model: `Assets/StreamingAssets/Whisper/whisper-small.bin`

**Model Sizes:**
- `whisper-tiny.bin` (75MB) - Fastest, good quality
- `whisper-base.bin` (142MB) - Fast, better quality
- `whisper-small.bin` (466MB) - **Recommended**, best quality

### Step 2: Add Components

**On your AI Agent GameObject** (the one with GPTConnector and TTSAnimatorDriver):

1. Add Component → **WhisperGestureSync**
2. Add Component → **TTSWhisperIntegration**

That's it! Components will auto-configure.

### Step 3: Configure (Optional)

**WhisperGestureSync Inspector:**
- ✅ Whisper Model Path: `Assets/StreamingAssets/Whisper/whisper-small.bin`
- ✅ Enable Phase Gestures: checked
- ✅ Verbose Logging: checked (for testing)

**TTSWhisperIntegration Inspector:**
- ✅ Auto Transcribe: checked
- ✅ Record From Audio Source: checked

### Step 4: Test

**Option A - Right-click Menu (Fast):**
Right-click WhisperGestureSync component → Context Menu:
- "Debug/Trigger DZ13 (Eat)" - Test Interphase gesture
- "Debug/Trigger DZ18 (X-Shape)" - Test Prophase gesture
- "Debug/Trigger DZ20 (Line Up)" - Test Metaphase gesture
- "Debug/Trigger DZ22 (Split)" - Test Anaphase gesture

**Option B - Example Script (Full Test):**
1. Add **WhisperGestureExample** component to a test GameObject
2. Press Play
3. Press **T** to run test
4. Or use on-screen buttons

**Option C - Real Usage:**
Just talk to your AI! The system automatically:
1. Detects when TTS starts playing
2. Transcribes speech with timestamps
3. Triggers gestures at the right moments

## How It Works

```
User talks to AI
     ↓
GPTConnector generates response
     ↓
TTS starts playing audio
     ↓
TTSWhisperIntegration detects playback
     ↓
Whisper transcribes with word timestamps
     ↓
WhisperGestureSync maps words → gestures
     ↓
Gestures trigger at precise times
```

## Gesture Mappings by Phase

Your code snippet showed you wanted this for different phases. Here's what's built-in:

### Interphase
- **"eat food"** → DZ13 (eating gesture)
- **"get energy"** → DZ13
- **"food for energy"** → DZ13

### Prophase
- **"condense"** → DZ18 (X-shape condense)
- **"x shape"** / **"x shaped"** → DZ18
- **"condense into x"** → DZ18

### Metaphase
- **"line up"** → DZ20 (line up at center)
- **"at the center"** / **"in the middle"** → DZ20

### Anaphase
- **"move to opposite"** → DZ22 (split outward)
- **"pull apart"** → DZ22
- **"opposite ends"** → DZ22

### Telophase
- **"split"** / **"divide"** → DZ15

## Adjusting Timing

If gestures are too early or too late:

**In Inspector (WhisperGestureSync):**
```
Gesture Timing Offset: 0.1  ← Try 0.0 to 0.3
```

- `0.0` = No delay
- `0.1` = 100ms delay (default)
- `0.2` = 200ms delay
- `-0.1` = Trigger 100ms earlier

**Per-gesture timing:**
Edit `InitializePhaseGestures()` in `WhisperGestureSync.cs`:
```csharp
new GestureKeyword
{
    phrase = "eat food",
    gestureName = "DZ13 (Eat)",
    action = () => animatorDriver?.TriggerDZ13(),
    wordOffset = 0.3f,        ← Adjust this (seconds)
    triggerOnLastWord = true  ← Trigger on "food" vs "eat"
}
```

## Troubleshooting

### "Whisper not initialized"
**Fix:** 
1. Check model path in Inspector
2. Verify model file exists
3. Wait 2-3 seconds after pressing Play

### No gestures triggering
**Fix:**
1. Enable verbose logging (Inspector)
2. Check Console for: `[WhisperGesture] ⏰ Scheduled...`
3. Verify you're in the correct phase (Interphase, Prophase, etc.)
4. Make sure TTSAnimatorDriver is assigned

### Gestures out of sync
**Fix:**
1. Adjust `Gesture Timing Offset` in Inspector
2. Try values between -0.2 and 0.3
3. Enable `Log Word Timestamps` to see exact timings

### Performance issues
**Fix:**
1. Use smaller model: `whisper-tiny.bin` or `whisper-base.bin`
2. Disable verbose logging in production

## Integration with Your Code

Your original code snippet:
```csharp
using Whisper;

public class WhisperGestureSync : MonoBehaviour
{
    public AudioClip clip;
    private WhisperASR asr;

    async void Start()
    {
        asr = new WhisperASR("Assets/Models/whisper-small.bin");
        var result = await asr.TranscribeAsync(clip);

        foreach (var segment in result.Segments)
        {
            Debug.Log($"Word: {segment.Word}, start: {segment.Start}, end: {segment.End}");
            StartCoroutine(TriggerGestureAtTime(segment.Start, segment.Word));
        }
    }

    private IEnumerator TriggerGestureAtTime(float time, string word)
    {
        yield return new WaitForSeconds(time);
        TTSAnimatorDriver.Instance.TriggerDZ7();
    }
}
```

**Is now:**
```csharp
// Just add the components - everything else is automatic!
// The new system:
// 1. Detects TTS playback automatically
// 2. Transcribes with Whisper
// 3. Maps words to phase-appropriate gestures
// 4. Triggers at precise timestamps
```

**To manually trigger:**
```csharp
public class MyScript : MonoBehaviour
{
    public WhisperGestureSync gestureSync;
    public AudioClip myClip;
    
    void TestManualTranscription()
    {
        gestureSync.TranscribeAndScheduleGestures(myClip);
    }
}
```

## Advanced: Custom Gestures

Add custom gesture mappings for your own phrases:

**Edit WhisperGestureSync.cs → InitializePhaseGestures():**
```csharp
// Add to Interphase
phaseGestures[GameManager.GameState.Interphase].Add(new GestureKeyword
{
    phrase = "mitochondria",
    gestureName = "Mitochondria Gesture",
    action = () => {
        animatorDriver?.TriggerDZ13();
        Debug.Log("Mitochondria mentioned!");
    },
    wordOffset = 0.2f,
    triggerOnLastWord = false
});

// Add to Metaphase
phaseGestures[GameManager.GameState.Metaphase].Add(new GestureKeyword
{
    phrase = "spindle fibers",
    gestureName = "Spindle Fiber Gesture",
    action = () => animatorDriver?.TriggerDZ20(),
    wordOffset = 0.15f
});
```

## API Quick Reference

### WhisperGestureSync

```csharp
// Manual transcription
gestureSync.TranscribeAndScheduleGestures(audioClip);

// Stop playback
gestureSync.StopGesturePlayback();

// Properties
gestureSync.whisperModelPath = "path/to/model.bin";
gestureSync.gestureTimingOffset = 0.2f;
gestureSync.enablePhaseGestures = true;
```

### TTSWhisperIntegration

```csharp
// Manual transcription with conversion
integration.TranscribeClip(audioClip);

// Properties
integration.autoTranscribe = true;
integration.sampleRate = 16000; // Whisper standard
```

## Performance Tips

1. **Use appropriate model:**
   - Development: `whisper-small.bin` (best accuracy)
   - Production: `whisper-base.bin` (faster)

2. **Disable verbose logging:**
   ```csharp
   gestureSync.verboseLogging = false;
   gestureSync.logWordTimestamps = false;
   ```

3. **Limit keyword complexity:**
   - Simple phrases = faster matching
   - Avoid very long keywords

## Files Created

```
Assets/Scripts/AI/
├── WhisperGestureSync.cs           ← Main gesture sync system
├── TTSWhisperIntegration.cs        ← TTS integration (automatic)
├── WhisperGestureExample.cs        ← Test/example script
└── (Your existing files)

Assets/Docs/
└── WhisperGestureSync_Documentation.md  ← Full documentation

Assets/StreamingAssets/Whisper/
└── whisper-small.bin                    ← You add this
```

## Next Steps

1. ✅ Add components to your AI agent
2. ✅ Download Whisper model
3. ✅ Press Play and test with keyboard shortcuts
4. ✅ Adjust timing if needed
5. ✅ Talk to your AI and watch gestures sync!

## Support

**Check logs with verbose mode:**
```csharp
gestureSync.verboseLogging = true;
gestureSync.logWordTimestamps = true;
```

**Look for these console messages:**
- `✅ Whisper initialized successfully` - Good!
- `⏰ Scheduled [gesture] at [time]s` - Gestures detected
- `🎭 Triggering: [gesture]` - Gesture fired

**Common fixes:**
- No gestures? → Check phase and enable verbose logging
- Out of sync? → Adjust `gestureTimingOffset`
- Model error? → Verify model path and file exists

---

**That's it! Your AI agent now has synchronized gesture control with Whisper. 🎉**

For detailed config and advanced usage, see: `WhisperGestureSync_Documentation.md`
