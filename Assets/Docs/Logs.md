**Log Data Summary**

- **Purpose**: Brief reference describing the two Cycle 1 logs exported during the tutorial run. Use this to interpret gesture/touch events, phase transitions, and object interactions and to align them with hand-position data.

- **Files**
  - Hand_Position.csv — timestamped hand position samples.
  - MainLogcsv — high-level event log with one row per timestamped frame (phases, detected gestures, touch states, spoken text, and named objects).
  - Player_Positioncsv — timestamped player (HMD/camera/root) positions and orientations.

**MainLog.csv — columns**
- **Time(s)**: Seconds from session start (float). Use to align with position samples.
- **Left_Gesture / Right_Gesture**: Detected discrete gestures per hand (e.g., Right_Pinch, Left_Pinch).
- **Left_Touch / Right_Touch**: Named object being touched by that hand (empty if none). Often contains object labels such as Magnesium, Protein, VitaminC, Mitochondrion.
- **Phase**: Tutorial phase or sub-phase (examples in this run: Intro, Wound, Interphase, InterphasePart2). Phase boundaries indicate high-level task changes.
- **User_Speech / AI_Speech**: Transcribed user utterances and AI assistant prompts or feedback.
- **AI_Gesture**: Gesture the AI avatar performs (if present).
- **Other**: Misc tags (e.g., ATP_Charged) used to mark important in-world state changes.

**Hand_Position.csv**
- Timestamped 3D coordinates for hand position. Both left and right hands are here.

**Player_Position.csv**
- Timestamped 3D position and orientation of the player's root or headset (HMD) and/or player avatar. Useful for:
  - Also includes a raycast column that outputs what object the raycast is hitting during the timestamp.