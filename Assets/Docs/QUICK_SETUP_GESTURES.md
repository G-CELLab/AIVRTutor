# Quick Setup Guide: Improved Gesture Timing

## What Changed

✅ **New audio-synchronized gesture timing system**
- Calculates WHERE in speech keywords occur
- Triggers gestures in sync with actual audio playback
- Works better on Quest 3 standalone

## Setup (Auto - Recommended)

1. **Just enable it in Inspector:**
   ```
   GPTConnector Component
   └─ Gesture Synchronization
      └─ ✅ Use Gesture Synchronizer = TRUE
   ```

2. **That's it!** GPTConnector auto-creates the synchronizer.

## Setup (Manual)

1. Add `GestureSynchronizer` component to scene
2. In`GPTConnector` Inspector:
   - Enable "Use Gesture Synchronizer"
   - Drag GestureSynchronizer to the field

## Configuration

### Basic Settings (GestureSynchronizer)
- **Characters Per Second**: 15 (adjust if gestures are early/late)
- **Timing Safety Margin Sec**: 0.1
- **Verbose Debug**: Enable to see timing calculations

### GPTConnector Settings
- **Use Gesture Synchronizer**: ✅ TRUE (RECOMMENDED)
- **Use Streaming Gesture Timing**: ✅ TRUE
- **React To Assistant Transcript**: ✅ TRUE

## Tuning

### If gestures trigger TOO EARLY:
- **Increase** `Characters Per Second` (15 → 18-20)
- Voice is speaking slower than estimated

### If gestures trigger TOO LATE:
- **Decrease** `Characters Per Second` (15 → 12-13)  
- Voice is speaking faster than estimated

### If gestures DON'T trigger:
1. Check all checkboxes are enabled
2. Enable `Verbose Debug = true`
3. Check console for keyword detection logs
4. Verify AI responses contain keywords like "eat food", "line up", etc.

## How It Works

````
1. AI Response starts
   └─> GestureSynchronizer resets state

2. Streaming transcript arrives: "The cell needs to eat food..."
   └─> Detects "eat food" at position 24 characters

3. Calculate timing:
   - 24 chars / 15 chars-per-sec = 1.6 seconds
   - Add safety margin: +0.1s = 1.7s
   - Audio already playing 0.2s
   - Final delay: 1.5s

4. Wait 1.5s from AUDIO START
   └─> Trigger EAT gesture (DZ13) ✅
````

## Keywords Reference

| Phase | Gesture | Primary Keyword |
|-------|---------|-----------------|
| Interphase | EAT (DZ13) | "eat food" |
| Prophase | CONDENSE (DZ18) | "x shape condense" |
| Metaphase | LINE UP (DZ12) | "line up" |
| Anaphase | SPLIT (DZ22) | "move to opposite ends" |

See `GESTURE_SYNC_v2.md` for full keyword lists.

## Debug Logs

Enable `Verbose Debug` to see:
```
[GestureSynchronizer] 🆕 New response started
[GestureSynchronizer] 🍽️ Interphase: Detected 'eat food' at position 24
[GestureSynchronizer] 📊 Timing calculation:
  • Chars before keyword: 24
  • Estimated speak time: 1.60s
  • Safety margin: 0.10s
  • Audio elapsed: 0.20s
  • Final delay: 1.50s
[GestureSynchronizer] ⏱️ Scheduling EAT (DZ13) in 1.50s
[GestureSynchronizer] ✅ Triggered EAT (DZ13) after 1.50s
```

## Comparison

### Old System (Fixed Delays)
- ❌ Fixed delay from speech start
- ❌ No keyword position awareness
- ❌ Timing drifts with different speech speeds
- ⚠️ Hit-or-miss on Quest 3

### New System (Audio-Synchronized)
- ✅ Calculates delay from keyword position
- ✅ Tracks actual audio playback time
- ✅ Adjusts for speech rate
- ✅ More reliable on Quest 3

## Files Changed

- `Assets/Scripts/AI/GestureSynchronizer.cs` - **NEW** component
- `Assets/Scripts/AI/GPTConnector.cs` - Updated to integrate synchronizer
- `GESTURE_SYNC_v2.md` - Full documentation
- `QUICK_SETUP_GESTURES.md` - This file

## Rollback to Old System

If you need to revert:
1. Set `useGestureSynchronizer = FALSE`
2. Old streaming delta timing will be used
3. Adjust legacy delays: `streamingInterphaseEatDelaySec`, etc.

## Need Help?

Check full documentation: `GESTURE_SYNC_v2.md`

Common issues:
- **No gestures**: Enable all checkboxes, check keywords in AI responses
- **Early gestures**: Increase Characters Per Second
- **Late gestures**: Decrease Characters Per Second
- **Inconsistent**: Check Quest Link latency, try standalone build
