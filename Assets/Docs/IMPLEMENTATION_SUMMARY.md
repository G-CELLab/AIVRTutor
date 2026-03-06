# Gesture Timing Implementation Summary

**Date:** March 6, 2026
**Issue:** AI agent gesture timing is misaligned with speech on Meta Quest 3
**Solution:** Implemented audio-synchronized gesture timing system

---

## Problem Analysis

The previous system had several issues:

1. **Streaming delta timing mismatch**
   - Transcript deltas arrive via network, not in perfect sync with audio
   - Delta arrival time ≠ word speaking time
   - Network latency varies (especially Quest Link)

2. **No audio-text position correlation**
   - System couldn't determine WHERE in audio a keyword occurs
   - Fixed delays were guesswork
   - Different speech speeds broke timing

3. **Quest 3 specific issues**
   - Whisper doesn't work on mobile/standalone
   - Audio buffering on standalone devices
   - Quest Link introduces additional latency

4. **Previous "MCP" approach**
   - Master branch used simpler fixed delays
   - More consistent because expectations were lower
   - Current streaming approach theoretically better but didn't account for sync issues

---

## Solution Implemented

### New Component: `GestureSynchronizer.cs`

**Core Innovation:** Calculate gesture trigger time based on **text position** and **audio playback tracking**

#### Key Features

1. **Position-Based Timing**
   ```csharp
   // Find keyword position in text
   int keywordIndex = normalizedText.IndexOf("eat food");
   
   // Calculate speaking time to reach keyword
   float speakTime = keywordIndex / charactersPerSecond;
   
   // Add safety margin and adjust for elapsed playback
   float delay = speakTime + safetyMargin - audioElapsedTime;
   ```

2. **Audio Playback Tracking**
   - Records exact audio start time
   - Adjusts delays based on elapsed playback
   - Synchronizes with actual audio, not transcript arrival

3. **Robust Keyword Detection**
   - Priority-ordered keyword matching
   - Most specific phrases matched first
   - Phase-specific gesture mapping

4. **Fallback Mechanisms**
   - Safety margins for reliability
   - Tunable fallback delays if calculation fails
   - Works in both realtime and HTTP modes

### Integration Points

1. **GPTConnector Changes**
   ```csharp
   // Added fields
   public bool useGestureSynchronizer = true;
   public GestureSynchronizer gestureSynchronizer;
   
   // Auto-initialization
   private void InitializeGestureSynchronizer()
   
   // Notifications at key points
   - OnResponseStart() when response.created event
   - OnAudioPlaybackStart() when audio begins
   - ProcessStreamingDelta() for keyword detection
   ```

2. **Backward Compatibility**
   - Legacy streaming system still available
   - Set `useGestureSynchronizer = false` to revert
   - Old delays preserved as fallbacks

---

## Files Created/Modified

### New Files
- ✅ `Assets/Scripts/AI/GestureSynchronizer.cs` - New timing engine
- ✅ `Assets/Scripts/AI/GestureSynchronizer.cs.meta` - Unity metadata
- ✅ `GESTURE_SYNC_v2.md` - Comprehensive documentation
- ✅ `QUICK_SETUP_GESTURES.md` - Quick start guide
- ✅ `IMPLEMENTATION_SUMMARY.md` - This file

### Modified Files
- ✅ `Assets/Scripts/AI/GPTConnector.cs` - Integrated synchronizer
  - Added GestureSynchronizer reference and initialization
  - Updated response.created handler to notify synchronizer
  - Updated audio_transcript.delta to use synchronizer
  - Added OnAudioPlaybackStart notifications at all playback points
  - Maintained backward compatibility with legacy timing

### Existing Files (Reference Only)
- 📄 `Assets/Scripts/AI/TTSAnimatorDriver.cs` - Unchanged (gesture triggers)
- 📄 `Assets/Scripts/AI/TextToSpeechPlayer.cs` - Unchanged (audio playback)
- 📄 `GESTURE_TIMING_UPGRADE.md` - Previous streaming approach docs

---

## How It Works (Flow Diagram)

```
┌─────────────────────────────────────────────────────────────┐
│  OpenAI Realtime API                                         │
│                                                               │
│  1. User speaks → AI generates response                      │
│  2. response.created event                                   │
│     └─> GPTConnector.HandleRealtimeEvent()                   │
│         └─> gestureSynchronizer.OnResponseStart()            │
│             - Reset state for new response                   │
│             - Clear previous gesture triggers                │
│                                                               │
│  3. response.audio_transcript.delta (streaming)              │
│     - "The cell"                                             │
│     - "needs to"                                             │
│     - "eat food" ← KEYWORD DETECTED!                         │
│     └─> GPTConnector.HandleRealtimeEvent()                   │
│         └─> gestureSynchronizer.ProcessStreamingDelta()      │
│             - Accumulated: "The cell needs to eat food"      │
│             - Keyword "eat food" at position 20 chars        │
│             - Calculate: 20 / 15 cps = 1.33s                 │
│             - Add safety: 1.33s + 0.1s = 1.43s               │
│             - Schedule gesture trigger                       │
│                                                               │
│  4. Audio playback begins (realtime or HTTP)                 │
│     └─> GPTConnector.MarkSpeakingStart()                     │
│         └─> gestureSynchronizer.OnAudioPlaybackStart()       │
│             - Record audio start time                        │
│             - Adjust pending delays for elapsed time         │
│                                                               │
│  5. Coroutine waits calculated delay                         │
│     └─> After 1.43s from audio start...                      │
│         └─> ttsDriver.TriggerDZ13() ← GESTURE FIRES!         │
│             └─> Animator plays DZ13 (EAT gesture)            │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Configuration & Tuning

### Default Settings

| Setting | Default | Purpose |
|---------|---------|---------|
| `charactersPerSecond` | 15.0 | English speaking rate (~180 WPM) |
| `timingSafetyMarginSec` | 0.1 | Safety buffer for network latency |
| `maxGestureDelaySec` | 10.0 | Maximum delay before fallback |
| `fallbackInterphaseDelay` | 2.0 | Fallback for Interphase (EAT) |
| `fallbackProphaseDelay` | 2.5 | Fallback for Prophase (CONDENSE) |
| `fallbackMetaphaseDelay` | 2.0 | Fallback for Metaphase (LINE UP) |
| `fallbackAnaphaseDelay` | 3.0 | Fallback for Anaphase (SPLIT) |

### Tuning Guide

#### Speech Rate Calibration
1. Enable `showTimingCalculations = true` in GestureSynchronizer
2. Watch console for timing calculations
3. Compare estimated time vs actual gesture timing
4. Adjust `charactersPerSecond`:
   - **Gestures too early**: Increase (voice slower than estimated)
   - **Gestures too late**: Decrease (voice faster than estimated)

#### Example Tuning Session
```
Test 1: Default (15 cps)
- Keyword at 30 chars → Estimated 2.0s
- Gesture triggered at 2.1s ✅ Good!

Test 2: Default (15 cps)
- Keyword at 45 chars → Estimated 3.0s  
- Gesture triggered at 3.5s ❌ Too late!
- Conclusion: Voice is faster than 15 cps

Test 3: Adjusted (18 cps)
- Keyword at 45 chars → Estimated 2.5s
- Gesture triggered at 2.5s ✅ Perfect!

Final setting: charactersPerSecond = 18
```

---

## Testing Instructions

### Quick Test (Play Mode)

1. **Setup**
   ```
   - Open Unity project
   - Load main scene with GPTConnector
   - Check Inspector settings:
     ✅ Use Gesture Synchronizer = TRUE
     ✅ React To Assistant Transcript = TRUE
     ✅ Use Streaming Gesture Timing = TRUE
   - Set Verbose Debug = TRUE (for logs)
   ```

2. **Test Each Phase**
   
   **Interphase:**
   - Ask: "How does the cell get energy?"
   - Expected response: "The cell needs to eat food..."
   - Expected gesture: DZ13 (EAT) when saying "eat food"
   - Check console for timing logs

   **Prophase:**
   - Ask: "What happens to DNA in Prophase?"
   - Expected response: "DNA condenses into X-shaped chromosomes..."
   - Expected gesture: DZ18 (CONDENSE) when saying "x shape"

   **Metaphase:**
   - Ask: "Where do chromosomes move in Metaphase?"
   - Expected response: "Chromosomes line up at the center..."
   - Expected gesture: DZ12 (LINE UP) when saying "line up"

   **Anaphase:**
   - Ask: "What happens in Anaphase?"
   - Expected response: "Chromosomes move to opposite ends..."
   - Expected gesture: DZ22 (SPLIT) when saying "opposite ends"

3. **Verify Timing**
   - Watch console for:
     ```
     [GestureSynchronizer] 🆕 New response started
     [GestureSynchronizer] 🍽️ Interphase: Detected 'eat food' at position X
     [GestureSynchronizer] 📊 Timing calculation: ...
     [GestureSynchronizer] ⏱️ Scheduling EAT (DZ13) in X.XXs
     [GestureSynchronizer] ✅ Triggered EAT (DZ13) after X.XXs
     ```
   - Gesture should fire DURING speech of keyword phrase
   - Not before keyword, not too long after

### Quest 3 Standalone Test

1. **Build & Deploy**
   ```
   - File → Build Settings
   - Platform: Android
   - Target Device: Quest 3
   - Build and Run
   ```

2. **Test in VR**
   - Put on Quest 3
   - Launch application
   - Go through same test questions
   - Observe gesture timing

3. **Monitor Logs**
   ```terminal
   # Connect Quest via USB
   adb logcat | grep "GestureSynchronizer"
   ```

4. **Expected Behavior**
   - Gestures should sync better than previous system
   - Some variation is normal (network latency)
   - Fallback delays provide safety net

### Regression Test (Legacy Mode)

1. **Disable New System**
   ```
   GPTConnector Inspector:
   - Use Gesture Synchronizer = FALSE
   ```

2. **Test Same Questions**
   - Compare timing to new system
   - Should see more inconsistency
   - Fixed delays may work sometimes, fail others

3. **Re-enable New System**
   ```
   - Use Gesture Synchronizer = TRUE
   ```

---

## Success Criteria

### ✅ System Works If:

1. **Gestures trigger during keyword phrases**
   - Not immediately at response start
   - Not way after keyword is spoken
   - Within ~0.5s of keyword utterance

2. **Console shows proper calculations**
   - Keyword detection logs appear
   - Timing calculations make sense
   - Gesture triggers are logged

3. **Consistent across responses**
   - Similar timing for similar text lengths
   - No wild variation between tests
   - Fallbacks rarely needed

4. **Works on Quest 3 standalone**
   - No worse than previous system
   - Ideally better/more consistent
   - Latency is acceptable

### ⚠️ Issue Indicators

- **Gestures never trigger**: Check enabled flags, keyword detection
- **Always too early**: Increase charactersPerSecond
- **Always too late**: Decrease charactersPerSecond
- **Wildly inconsistent**: Check network latency, Quest Link stability

---

## Advantages Over Previous System

| Aspect | Previous System | New System |
|--------|----------------|------------|
| **Timing Method** | Fixed delay from speech start | Position-calculated, audio-synced |
| **Keyword Position** | Ignored | Core to timing calculation |
| **Audio Awareness** | None | Tracks playback time |
| **Speech Rate** | Hardcoded one size fits all | Configurable per voice |
| **Fallback** | Single fixed delay | Multiple tuned fallbacks |
| **Debug Info** | Basic logs | Comprehensive timing data |
| **Consistency** | Varies widely | More predictable |
| **Quest 3 Support** | Poor | Improved |
| **Tuning** | Trial and error | Data-driven adjustment |

---

## Future Improvements

### Short Term
1. **Voice Speed Auto-Detection**
   - Measure actual CPS from completed responses
   - Auto-adjust charactersPerSecond dynamically

2. **Audio Clip Length Integration**
   - Use AudioClip.length for verification
   - Cross-reference with text length

3. **Phrase-Level Timing**
   - Break text into sentences/phrases
   - More accurate timing per segment

### Long Term
1. **ML-Based Prediction**
   - Train model on actual timing data
   - Predict optimal delay from context

2. **Visual Feedback**
   - In-editor timing preview
   - Real-time gesture timing visualization

3. **Multi-Language Support**
   - Language-specific CPS rates
   - Different keyword patterns

---

## Rollback Procedure

If new system causes issues:

1. **In Unity Editor**
   ```
   GPTConnector Inspector:
   - Use Gesture Synchronizer = FALSE
   ```

2. **Adjust Legacy Delays**
   ```
   - streamingInterphaseEatDelaySec = 1.0
   - streamingProphaseCondenseDelaySec = 1.0
   - delayEatGestureSec = 1.5
   - delayCondenseGestureSec = 2.0
   - delayLineUpGestureSec = 2.0
   - delaySplitOutwardGestureSec = 2.5
   ```

3. **Revert Code (if needed)**
   ```bash
   git checkout <previous-commit> Assets/Scripts/AI/GPTConnector.cs
   # Delete GestureSynchronizer.cs
   ```

---

## Support & Debugging

### Enable Full Debug Output
```csharp
// GestureSynchronizer
verboseDebug = true
showTimingCalculations = true

// GPTConnector  
verboseDebug = true
logPrompts = true
```

### Console Log Keywords
- `[GestureSynchronizer]` - Timing system logs
- `[Gesture/Streaming]` - Streaming delta detection
- `[Gesture/Fallback]` - Legacy system (if synchronizer disabled)
- `[STT][assistant/delta]` - Streaming transcript deltas

### Common Debug Scenarios

**No detection logs:**
- AI response doesn't contain expected keywords
- Check prompt engineering
- Review keyword lists in GestureSynchronizer

**Detection but no trigger:**
- TTSAnimatorDriver reference missing
- Check component connections
- Verify animator parameters exist

**Trigger but poor timing:**
- Tune charactersPerSecond
- Check audio playback notification
- Review timing calculation logs

---

## Conclusion

This implementation provides a robust, tunable, and debuggable gesture timing system that addresses the core synchronization issues. The position-based timing approach combined with audio playback tracking should provide much more consistent results than pure streaming delta timing, especially on Quest 3 standalone.

The system maintains backward compatibility while providing a clear upgrade path. Users can easily tune the system for their specific AI voice characteristics and revert if needed.

**Next Steps:**
1. Test in Unity Editor play mode
2. Tune charactersPerSecond for your AI voice
3. Build and test on Quest 3 standalone
4. Monitor and adjust based on real-world usage
5. Consider future enhancements based on results

---

**Implementation Complete** ✅
