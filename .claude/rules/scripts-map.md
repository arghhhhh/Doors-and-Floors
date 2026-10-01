# Scripts Quick Reference

Read the script itself for method signatures and field details. Keep this table current when adding, removing or renaming scripts.

## Core Gameplay (Assets/Scripts/)
| Script | Role |
|--------|------|
| `GameManager.cs` | Singleton state machine (Playing/Won), timer, spawn positions, restart flow |
| `PlayerController.cs` | Movement, jumping, portal teleport animation, per-reason freezes, `SetHorizontalVelocity`/`TryJump` input API, `OnReset` event, animator driving |
| `CpuPlayerInput.cs` | CPU opponent: picks doors by teleports-to-win, tracks the belt, jumps; difficulty fields + presets |
| `GameSession.cs` | Static lobby → game settings (`VsCpu`) |
| `PortalDoor.cs` | Teleport triggers, swivel animation, spiral shader params, cooldown system |
| `ConveyorBelt.cs` | Horizontal door movement, ghost clone wrapping, boundary detection |
| `DoorPairGenerator.cs` | Procedural door generation with critical path guarantee, color pairing |
| `FinishLine.cs` | Ribbon slicing effect with physics-driven halves |
| `WinTrigger.cs` | Win detection when player lands on top floor |
| `UIManager.cs` | App UI elements: timer, win panel, high score list, blinking via schedule |
| `HighScoreManager.cs` | Top 10 persistence as JSON with base64 PNG photos |
| `SFXManager.cs` | Sound effects and voice line playback |
| `ViewModeToggle.cs` | Editor utility: toggle OpenPose skeleton visibility |
| `Editor/CreateVideoRenderTexture.cs` | Menu item `ZedGames/Fix VideoQuad Color Banding`: builds the ARGBHalf RenderTexture for the StartScreen video quad |

## ZED Integration (Assets/Scripts/ZED/)
| Script | Role |
|--------|------|
| `ZEDTrackingProvider.cs` | Persistent singleton: body tracking hub, player assignments, re-identification |
| `StartScreenManager.cs` | Lobby 5-state machine: P1 Detection → P1 Confirm → P2 Waiting → P2 Confirm → Launching |
| `GameScreenManager.cs` | Scene orchestrator: activates tracked players, CPU opponent, or keyboard fallback |
| `BodyTrackingInput.cs` | Per-player input adapter: pelvis X mapping, jump gesture, tracking loss |
| `GestureDetector.cs` | Static utility: field goal gesture recognition (both wrists above nose) |
| `BodyTrackingRecorder.cs` | Records body tracking data for replay/testing |
| `TrackingLostOverlay.cs` | Visual feedback when player tracking is lost |
| `FaceCaptureHelper.cs` | Captures player face photos for high score display |
| `ZEDRigGuard.cs` | Runs first on ZED_Rig_Mono; deactivates the duplicate rig when StartScreen reloads |
| `ZEDPreviewDisabler.cs` | Hides the ZED camera preview (Camera_Left + Frame) once the ZED is ready; goes on ZED_Rig_Mono |

## Key Relationships
- `GameManager` owns game state; `GameScreenManager` orchestrates scene setup
- `ZEDTrackingProvider` persists across scenes (DontDestroyOnLoad), feeds `BodyTrackingInput`
- `PlayerController` reads input from `BodyTrackingInput` (ZED), `CpuPlayerInput` (CPU) or keyboard (fallback)
- `DoorPairGenerator` creates `ConveyorBelt` + `PortalDoor` objects under `GameManager`
- `UIManager` queries App UI elements via `Q<T>("name")` pattern
