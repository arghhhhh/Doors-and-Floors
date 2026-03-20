---
paths:
  - "Assets/Scripts/ZED/**"
---
# ZED Body Tracking Context

This rule loads when working on ZED-related scripts.

## ZED SDK Setup
- ZED 2i stereo camera with body tracking
- Plugin: custom UPM fork at https://github.com/arghhhhh/zed-unity (master branch)
- Local fork clone: `C:\Users\Joss_loaner\Desktop\Projects\Mine\zed-unity`
- Plugin source: `ZEDCamera/Assets/` in the fork repo
- UPM URL in manifest: `https://github.com/arghhhhh/zed-unity.git?path=ZEDCamera/Assets`

## Architecture
- `ZEDTrackingProvider` is a persistent singleton (DontDestroyOnLoad) on the `ZED_Rig_Mono` GameObject
- It manages the ZED camera lifecycle and body tracking across both scenes
- Player-body assignment uses re-identification to maintain tracking across frames
- Events notify downstream systems of tracking state changes

## StartScreen Flow (5 States)
1. **P1 Detection** — waiting for first body
2. **P1 Confirm** — first player holds field goal gesture to confirm
3. **P2 Waiting** — waiting for second body
4. **P2 Confirm** — second player confirms with gesture
5. **Launching** — countdown then scene transition to GameScreen

## Body Tracking → Game Input Pipeline
```
ZED Camera → ZEDTrackingProvider (body detection + assignment)
  → BodyTrackingInput (per-player adapter)
    → pelvis X position mapped to horizontal movement
    → field goal gesture mapped to jump
  → PlayerController (applies velocity + physics)
```

## Common Gotchas
- `trackingXOffset` in PlayerController must be updated during teleportation to prevent snap-back
- Tracking loss triggers `TrackingLostOverlay` and pauses that player's input
- `ZEDTrackingProvider` must be on the ZED_Rig_Mono GameObject which has DontDestroyOnLoad
- Face capture happens during the confirmation gesture for high score photos
