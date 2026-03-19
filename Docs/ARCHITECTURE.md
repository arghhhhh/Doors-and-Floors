# Doors and Floors - Architecture

## Overview

A 2-player racing game where players race from the bottom floor to the top floor of a building by jumping into color-coded portal doors hanging from conveyor belts on each floor's ceiling. Built in Unity 2022.3 as a 3D project with orthographic camera (simulating 2D). Players are controlled via ZED 2i stereo camera body tracking, with keyboard fallback for testing.

## Project Structure

```
Assets/
  Scripts/
    GameManager.cs          # Singleton. State machine, timer, win/restart flow
    PlayerController.cs     # Movement, jump, teleport, profile photo quad
    PortalDoor.cs           # Trigger-based teleport between paired doors
    ConveyorBelt.cs         # Moves child doors horizontally with wraparound
    DoorPairGenerator.cs    # Procedural door/belt generation with winnability guarantee
    WinTrigger.cs           # Detects player landing on top floor
    UIManager.cs            # Timer HUD, win panel, high score display
    HighScoreManager.cs     # Top 10 scores persisted as JSON with profile photos
    ZED/
      ZEDTrackingProvider.cs  # Persistent singleton. Body tracking data hub + player assignments
      GestureDetector.cs      # Static utility for gesture recognition (field goal)
      StartScreenManager.cs   # Lobby state machine for player registration via gesture
      BodyTrackingInput.cs    # Per-player input adapter (body tracking → PlayerController)
      GameScreenManager.cs    # Game scene orchestrator. Activates tracked players, handles loss
  Scenes/
    StartScreen.unity       # Lobby scene: player registration + ZED camera preview
    GameScreen.unity        # Main gameplay scene
Docs/
  ARCHITECTURE.md           # This file
  SCENE-SETUP.md            # Scene hierarchy and object configuration
  SCRIPTS-API.md            # Public API reference for all scripts
```

## Game Flow

```
StartScreen
  ZED_Rig_Mono initializes (DontDestroyOnLoad)
  ZEDTrackingProvider starts body tracking
  StartScreenManager state machine:
    WaitingForP1Detection → body detected
    WaitingForP1Confirm   → field goal held 1s → P1 assigned
    WaitingForP2           → 10s countdown begins
    WaitingForP2Confirm   → field goal held 1s → P2 assigned (or timeout → P1 only)
    Launching             → load GameScreen
                                |
GameScreen
  GameScreenManager reads assignments from ZEDTrackingProvider
  Activates assigned players, attaches BodyTrackingInput
  Falls back to keyboard if no ZED assignments
                                |
  DoorPairGenerator.Generate() → GameState.Playing
                                |
                          Timer runs, players move via body tracking
                                |
                    Player lands on top floor (grounded)
                                |
                          GameState.Won
                      All players frozen
                      Win panel + high scores
                                |
                  Enter → Restart()  |  Escape → StartScreen
                      Reset players to Floor_0
                      Regenerate doors
                      Wait one FixedUpdate
                      GameState.Playing
```

## ZED Body Tracking Architecture

```
ZED_Rig_Mono (DontDestroyOnLoad, persists across scenes)
├── ZEDManager            # Camera connection, body detection, fires OnBodyTracking events
├── ZEDTrackingProvider   # Singleton. Player-body assignments, re-identification, events
└── Camera_Left           # Small viewport preview (top-left corner, toggleable)
      └── Frame           # Rendering plane for camera feed
```

### Data Flow

1. **ZEDManager** detects bodies and fires `OnBodyTracking(BodyTrackingFrame)`
2. **ZEDTrackingProvider** receives frame, updates `currentBodies` dictionary, checks assignments
3. For each assigned player: fires `OnPlayerBodyUpdated(playerNumber, body)` or `OnPlayerBodyLost(playerNumber)`
4. **BodyTrackingInput** (on each player) receives events, translates to movement:
   - Pelvis X → mapped from physical space (-1.5m to +1.5m) to game space (-6.5 to +6.5) via velocity
   - Field goal gesture rising edge + grounded → jump impulse

### Player-Body Assignment

- Managed by `ZEDTrackingProvider.playerBodyAssignments` (playerNumber → bodyId)
- Set during StartScreen lobby via `AssignBodyToPlayer()`
- Re-identification: when assigned body lost and new unassigned body appears near last known X, auto-reassign

### Tracking Loss Handling

- **BodyTrackingInput** manages 3 states: `Tracking → Searching → Lost`
- 1s grace period before showing lost indicator
- 10s timeout in Lost state → `GameScreenManager.RemovePlayer()`
- Recovery at any point cancels the timer
- If all players removed → return to StartScreen after 2s delay

## Key Design Decisions

### Physics

- 3D Rigidbody with constraints: Freeze Z position, freeze all rotation
- Players at Z = -0.3 (in front of doors for rendering)
- CapsuleCollider on players (center offset to fit character model), BoxCollider triggers on doors
- Ground detection: downward raycast in FixedUpdate, accounts for both capsule center offset and transform scale
- Players on Layer 9 ("Player") with self-collision disabled

### Character Model

- Rigged Hazmat Man mesh (Meshy AI) as child GameObject (HazmatManModel) of each player
- Animator on child with Generic rig, applyRootMotion=false (Rigidbody drives movement)
- AnimatorController (Assets/Animation/HazmatManController.controller) with Idle/Walk/Run/JumpUp/JumpDown states
- PlayerController drives animator parameters (Speed, IsGrounded, IsJumping) and rotates child model to face movement direction
- CapsuleCollider remains on root player GameObject for physics

### Controls

- **Primary**: ZED 2i body tracking (pelvis for X movement, field goal gesture for jump)
- **Fallback**: Keyboard (Player 1: A/D/W, Player 2: Arrows). Active when `useBodyTracking = false`

### Movement Mapping

- Velocity-based X movement (not direct position) to respect Rigidbody physics and collisions
- Pelvis keypoint used for X position (most stable; head/wrists move during gestures)
- `xTrackingSpeed` multiplier controls responsiveness (default 10)

### Input Separation

- `BodyTrackingInput` is a separate component from `PlayerController`
- Keeps existing keyboard code clean; game remains playable without ZED
- Rising-edge gesture detection prevents repeat-jumping while arms are raised
- Hold-duration gesture for lobby confirmation prevents accidental activation

### Teleportation Sequence

1. Door's `OnTriggerStay` detects airborne player inside trigger
2. Guards: game is Playing, cooldown clear, player hasn't teleported this jump, player has landed since last reset
3. Coroutine on PlayerController: freeze → hide → wait `teleportDelay` → move to destination → show → unfreeze
4. Generation counter (`resetGeneration`) invalidates pending coroutines if a reset occurs mid-teleport
5. BodyTrackingInput checks `IsFrozen` → skips input while teleporting

### Door Pairing Algorithm

1. Auto-find Floor_0 through Floor_N, create belts beneath each floor (except Floor_0)
2. Critical path: belt_0 → belt_2 → belt_4 → last belt (guarantees winnability)
3. Remaining doors: shuffled and paired randomly with max distance constraint of `total_belts - 3`
4. Unpaired doors are destroyed
5. Each pair gets a unique color from a 10-color palette

### Restart Strategy

In-place reset (no scene reload) to preserve body tracking state:

1. `StopAllCoroutines` on all players + increment `resetGeneration`
2. Reset player state (frozen, velocity, position, visibility)
3. Regenerate doors
4. Wait one `WaitForFixedUpdate` to clear physics triggers
5. Re-verify player positions on Floor_0
6. Set state to Playing

### High Score Persistence

- JSON file at `Application.persistentDataPath/highscores.json`
- Stores player name, time, and profile photo as base64-encoded PNG
- `HighScoreManager` uses `DontDestroyOnLoad` to survive scene transitions
