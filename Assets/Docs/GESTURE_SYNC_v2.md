# Improved Gesture Synchronization System (v2)

## Overview
This document describes the **new audio-synchronized gesture timing system** that fixes gesture timing issues by calculating precise trigger times based on text position and audio playback.

## Problem Statement

### Previous System Issues
The previous streaming-based system (`GESTURE_TIMING_UPGRADE.md`) had timing problems:

1. **Streaming deltas arrive out of sync with audio playback**
   - Network latency causes transcript deltas to arrive at different times than audio
   - Delta arrival != word being spoken
   
2. **No audio-text alignment**
   - System couldn't know WHERE in the audio a keyword actually occurs
   - Fixed delays were guesswork
   
3. **Variable speech speeds**
   - AI voice speed changes between responses
   - Fixed delays worked sometimes, failed other times

4. **Quest 3 / Mobile Issues**
   - Quest Link introduces additional latency
   - Whisper doesn't work on mobile (no local transcription)
   - Audio buffering on standalone devices

## Solution: GestureSynchronizer

The new `GestureSynchronizer` component solves these issues by:

### 1. **Position-Based Timing**
- Calculates keyword position in text (character index)
- Estimates speaking time based on characters-per-second rate
- Triggers gesture at calculated time during audio playback

### 2. **Audio Playback Tracking**
- Knows exactly when audio playback starts
- Adjusts delays based on elapsed playback time
- Synchronizes with actual audio, not transcript arrival

### 3. **Fallback Mechanisms**
- Includes safety margins for reliability
- Falls back to tuned delays if calculation fails
- Works in both realtime and HTTP modes

## Architecture

```
┌─────────────────────────────────────────────────┐
│            GPTConnector                          │
│                                                  │
│  1. response.created                             │
│     └─> gestureSynchronizer.OnResponseStart()   │
│                                                  │
│  2. response.audio_transcript.delta              │
│     └─> gestureSynchronizer.ProcessStreamingDelta()│
│         - Detects keyword                        │
│         - Calculates character position          │
│         - Estimates speech timing                │
│                                                  │
│  3. Audio playback starts                        │
│     └─> gestureSynchronizer.OnAudioPlaybackStart()│
│         - Adjusts delays for elapsed time        │
│                                                  │
│  4. Wait calculated delay                        │
│     └─> Trigger gesture (DZ13/DZ18/DZ12/DZ22)   │
└─────────────────────────────────────────────────┘
```

## Usage

### Setup in Unity

1. **Automatic Setup** (Recommended)
   - GPTConnector automatically creates GestureSynchronizer
   - Set `useGestureSynchronizer = true` in Inspector

2. **Manual Setup**
   ```
   - Add GestureSynchronizer component to scene
   - Assign references in GPTConnector Inspector:
     - Enable "Use Gesture Synchronizer"
     - Drag GestureSynchronizer to field
   ```

### Configuration Parameters

#### GestureSynchronizer Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| `charactersPerSecond` | 15.0 | Average speaking rate (adjust for voice speed) |
| `timingSafetyMarginSec` | 0.1 | Safety delay added to calculations |
| `maxGestureDelaySec` | 10.0 | Maximum allowed delay (fallback after this) |
| `fallbackInterphaseDelay` | 2.0 | Fallback delay for Interphase (EAT gesture) |
| `fallbackProphaseDelay` | 2.5 | Fallback delay for Prophase (CONDENSE gesture) |
| `fallbackMetaphaseDelay` | 2.0 | Fallback delay for Metaphase (LINE UP gesture) |
| `fallbackAnaphaseDelay` | 3.0 | Fallback delay for Anaphase (SPLIT gesture) |

#### GPTConnector Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| `useGestureSynchronizer` | true | Enable new timing system (RECOMMENDED) |
| `useStreamingGestureTiming` | true | Use streaming deltas for detection |
| `reactToAssistantTranscript` | true | Enable gesture triggering |

### Tuning Speech Rate

The `charactersPerSecond` parameter is key to timing accuracy:

- **Standard English**: 15 cps (~180 words per minute)
- **Faster voice**: 18-20 cps
- **Slower/clearer voice**: 12-14 cps

#### How to Tune
1. Enable `showTimingCalculations = true`
2. Watch console logs for timing calculations
3. Compare "Estimated speak time" vs actual time to keyword
4. Adjust `charactersPerSecond` up/down

**Example Log:**
```
[GestureSynchronizer] 📊 Timing calculation:
  • Chars before keyword: 45
  • Estimated speak time: 3.00s
  • Safety margin: 0.10s
  • Audio elapsed: 0.15s
  • Final delay: 2.95s
```

If gesture triggers too early: Lower `charactersPerSecond`
If gesture triggers too late: Raise `charactersPerSecond`

## Keyword Detection

### Interphase (EAT - DZ13)
```csharp
Keywords (priority order):
- "eat food"                    // Primary trigger
- "eating food"
- "eat the food"
- "eats food"
- "food for energy"
- "get energy"
- "needs food"
- "need to eat"
- "must eat"
```

### Prophase (CONDENSE - DZ18)
```csharp
Keywords:
- "x shape condense"            // Ideal trigger
- "condense into x"
- "x shaped chromosome"
- "condenses into x"
- "x shaped"
- "x shape"
- "condense"
- "condenses"
```

### Metaphase (LINE UP - DZ12)
```csharp
Keywords:
- "line up at the center"       // Primary
- "line up at center"
- "line up in the middle"
- "line up"
- "lined up"
- "line them up"
- "all line up"
- "in a row"
- "align at the center"
```

### Anaphase (SPLIT OUTWARD - DZ22)
```csharp
Keywords:
- "move to opposite ends"       // Primary
- "move them to opposite ends"
- "pull to opposite ends"
- "to opposite ends"
- "to opposite sides"
- "to opposite poles"
- "pull apart"
- "split apart"
- "move apart"
- "separate them"
```

## Debug Information

### Console Logs

**Response Start:**
```
[GestureSynchronizer] 🆕 New response started - reset state
```

**Audio Playback:**
```
[GestureSynchronizer] 🔊 Audio playback started at 123.45
```

**Keyword Detection:**
```
[GestureSynchronizer] 🍽️ Interphase: Detected 'eat food' at position 42
[GestureSynchronizer] ⏱️ Scheduling EAT (DZ13) in 2.95s (keyword: 'eat food')
```

**Timing Calculation:** (when `showTimingCalculations = true`)
```
[GestureSynchronizer] 📊 Timing calculation:
  • Chars before keyword: 42
  • Estimated speak time: 2.80s
  • Safety margin: 0.10s
  • Audio elapsed: 0.05s
  • Final delay: 2.85s
```

**Gesture Trigger:**
```
[GestureSynchronizer] ✅ Triggered EAT (DZ13) after 2.85s actual wait
```

### Troubleshooting

#### Gestures trigger too early
- **Increase** `charactersPerSecond` (voice is speaking slower than estimated)
- Or **increase** `timingSafetyMarginSec`

#### Gestures trigger too late
- **Decrease** `charactersPerSecond` (voice is speaking faster than estimated)
- Or check if keyword appears later in response than expected

#### Gestures don't trigger at all
1. Check `useGestureSynchronizer = true`
2. Check `reactToAssistantTranscript = true`
3. Check `useStreamingGestureTiming = true`
4. Enable `verboseDebug = true` and check logs for keyword detection
5. Verify AI responses contain expected keywords

#### Timing is inconsistent
- AI voice speed may vary between responses
- Add speech rate detection (future enhancement)
- Use fallback delays as safety net

## Comparison: Old vs New System

| Feature | Old System | New System |
|---------|-----------|------------|
| **Timing Method** | Fixed delays from speech start | Character-position calculated |
| **Audio Sync** | None (delay-based) | Tracks audio playback time |
| **Keyword Position** | Ignored | Used for timing calculation |
| **Speech Rate** | Assumed constant | Configurable |
| **Fallback** | Fixed delays | Tunable fallback delays |
| **Debug Info** | Limited | Comprehensive calculations |
| **Quest 3 Support** | Hit-or-miss | Improved reliability |

## Future Enhancements

1. **Audio Duration Analysis**
   - Use audio clip length for better timing
   - Cross-reference with text length

2. **Speech Rate Auto-Detection**
   - Calculate actual CPS from previous responses
   - Auto-adjust `charactersPerSecond`

3. **Phrase-Level Timing**
   - Break text into phrases/sentences
   - Estimate timing per phrase

4. **ML-Based Prediction**
   - Train model on actual gesture timing data
   - Predict optimal trigger time based on context

## Legacy Mode

To use the old streaming delta timing system:
1. Set `useGestureSynchronizer = false`
2. Adjust `streamingInterphaseEatDelaySec` and `streamingProphaseCondenseDelaySec`
3. Gestures will trigger instantly or with fixed delays on keyword detection

**Note:** Legacy mode is not recommended for Quest 3 or production use.

## Implementation Notes

### Integration Points

1. **GPTConnector.OnEnable()**
   - Initializes GestureSynchronizer

2. **HandleRealtimeEvent("response.created")**
   - Resets gesture state for new response

3. **HandleRealtimeEvent("response.audio_transcript.delta")**
   - Processes streaming deltas for keyword detection
   - Routes to GestureSynchronizer if enabled

4. **MarkSpeakingStart()**
   - Notifies GestureSynchronizer when audio begins

### Thread Safety
- All timing calculations run on Unity main thread
- Coroutines handle delays safely
- No async/threading issues

## Credits

- **Original streaming system**: GESTURE_TIMING_UPGRADE.md
- **Improved audio-sync system**: GESTURE_SYNC_v2.md (this document)
- **Target platform**: Meta Quest 3 standalone
