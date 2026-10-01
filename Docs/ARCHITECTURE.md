# Doors and Floors - Architecture

## Overview

A 2-player racing game where players race from the bottom floor to the top floor of a building by jumping into color-coded portal doors hanging from conveyor belts on each floor's ceiling. Built in Unity 2022.3 as a 3D project with perspective camera (side-scrolling 2.5D). Players are controlled via ZED 2i stereo camera body tracking, with keyboard fallback for testing.

## Project Structure

```
Assets/
  Animation/
    HazmatManController.controller  # Animator controller for character animations
  Materials/
    FinishLineCheckered.mat         # Checkered pattern material for finish line ribbon
    VideoQuadMat.mat                # URP Unlit material for StartScreen video quad
  Media/
    hands_up_gesture.mp4            # Gesture tutorial video (looped on StartScreen)
  Models/
    FinishLineRibbon.fbx            # Finish line ribbon mesh
    Meshy_AI_hazmat_man_1_biped_separate/  # Hazmat Man character (mesh, animations, textures)
  Scripts/                  # Gameplay scripts; see .claude/rules/scripts-map.md for the per-script index
    Editor/                 # Editor-only menu utilities
    ZED/                    # Body tracking, lobby flow, per-player input
  Shaders/
    PortalSpiral.shader       # Animated spiral effect for portal doors
    FinishLineCheckered.shader  # Checkered pattern with wind animation and clip-side slicing
  UI/
    Fonts/
      PressStart2P-Regular.ttf  # NES pixel font (Google Fonts, OFL license)
    Settings/
      GamePanelSettings.asset   # Shared PanelSettings (1920x1080, scale with screen)
    Styles/
      nes-theme.uss             # NES color palette, pixel font, zero border-radius
      start-screen.uss          # StartScreen layout positioning
      game-screen.uss           # GameScreen layout (timer, win panel, score list)
    UXML/
      StartScreen.uxml          # StartScreen layout (title, status bars, countdown)
      GameScreen.uxml           # GameScreen layout (timer + win panel overlay)
      HighScoreEntry.uxml       # Template cloned per score row
  Scenes/
    StartScreen.unity       # Lobby scene: player registration + ZED camera preview
    GameScreen.unity        # Main gameplay scene
    Character Testing.unity # Test scene (not in build settings)
Docs/
  ARCHITECTURE.md           # This file
  SCENE-SETUP.md            # Scene hierarchy and object configuration
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
    WaitingForP2Confirm   → field goal held 1s → P2 assigned (or timeout → P1 vs CPU)
    Launching             → load GameScreen (sets GameSession.VsCpu when only P1 joined)
  Dev builds/Editor: K = keyboard 2P, V = keyboard vs CPU (skips the ZED)
                                |
GameScreen
  GameScreenManager reads assignments from ZEDTrackingProvider
  Activates assigned players, attaches BodyTrackingInput
  GameSession.VsCpu → Player2 driven by CpuPlayerInput
  Falls back to keyboard if no ZED assignments (keyboardVsCpu → Player2 is the CPU)
                                |
  DoorPairGenerator.Generate()
                                |
  First load only: 10s tutorial modal (two looping WebM clips
    showing run + jump controls, rendered via VideoPlayer → RenderTexture
    → UI Toolkit backgroundImage). Players frozen until modal closes.
    Skipped on replay (static flag).
                                |
                          GameState.Playing
                                |
                          Timer runs, players move via body tracking
                                |
               Player hits FinishLine trigger → ribbon slice effect
                    Player lands on top floor (grounded in WinZone)
                                |
                          GameState.Won
                      All players frozen
                      Win panel + high scores
                      10s countdown to auto-return to menu
                                |
              Hands-up gesture (1s hold) → Restart()
              Enter key → Restart()  |  Escape key → StartScreen
              Countdown expires → StartScreen
                                |
                      Reset players to Floor_0
                      Reset finish line ribbon
                      Regenerate doors
                      Wait one FixedUpdate
                      Double-verify player positions on Floor_0
                      GameState.Playing
```

## ZED Body Tracking Architecture

```
ZED_Rig_Mono (DontDestroyOnLoad via ZEDManager.dontDestroyOnLoad, persists across scenes)
├── ZEDManager            # Camera connection, body detection, fires OnBodyTracking events
├── ZEDTrackingProvider   # Singleton. Player-body assignments, re-identification, events
├── ZEDRigGuard           # Runs first; deactivates the scene copy loaded when StartScreen reopens
└── Camera_Left           # Small viewport preview (top-left corner, toggleable)
      └── Frame           # Rendering plane for camera feed
```

### Data Flow

1. **ZEDManager** detects bodies and fires `OnBodyTracking(BodyTrackingFrame)`
2. **ZEDTrackingProvider** receives frame, updates `currentBodies` dictionary, checks assignments
3. For each assigned player: fires `OnPlayerBodyUpdated(playerNumber, body)` or `OnPlayerBodyLost(playerNumber)`
4. **BodyTrackingInput** (on each player) receives events, translates to movement:
   - Pelvis X → mapped from physical space (-1.5m to +1.5m) to game space (-6.5 to +6.5) via velocity
   - Jump (`jumpMethod`, default `PhysicalJump`): pelvis rising above a baseline, or field goal gesture rising edge → `PlayerController.TryJump()`

### Player-Body Assignment

- Managed by `ZEDTrackingProvider.playerBodyAssignments` (playerNumber → bodyId)
- Set during StartScreen lobby via `AssignBodyToPlayer()`
- Re-identification: when assigned body lost and new unassigned body appears near last known X, auto-reassign

### Tracking Loss Handling

- **BodyTrackingInput** manages 3 states: `Tracking → Searching → Lost`
- 1s grace period before showing lost indicator
- 10s timeout in Lost state → `GameScreenManager.RemovePlayer()`
- Recovery at any point cancels the timer
- If all human players removed → return to StartScreen after 2s delay (the CPU doesn't count)
- `PlayerController.ResetPlayer` raises `OnReset`; BodyTrackingInput clears its tracking-loss state on restart
- Freezes are tracked per reason (Portal / TrackingLoss / Win), so recovering tracking mid-teleport can't unfreeze the portal animation

## Key Design Decisions

### Physics

- 3D Rigidbody with constraints: Freeze Z position, freeze all rotation
- Players at Z = -0.3 (in front of doors for rendering), clamped every frame
- Player scale: (0.34, 0.34, 0.34)
- CapsuleCollider on players (center (0, 1.375, 0), height 2.75, radius 0.75), BoxCollider triggers on doors
- Ground detection: downward raycast in FixedUpdate from the Rigidbody pose (`rb.position`, not the interpolated transform), starting a capsule-radius above the capsule bottom so a fast landing that slightly penetrates the floor can't put the ray origin inside it (raycasts ignore colliders they start in). A false "airborne" step would re-arm `teleportedThisJump` while the player still stands inside the arrival door's trigger and send them straight back through it
- Ground check skipped while frozen to prevent false ground transitions during teleport animation
- Players on Layer 9 ("Player") with self-collision disabled

### Character Model

- Rigged Hazmat Man mesh (Meshy AI) as child GameObject (HazmatManModel) of each player
- Animator on child with Generic rig, applyRootMotion=false (Rigidbody drives movement)
- AnimatorController (Assets/Animation/HazmatManController.controller) with Idle/Walk/Run/JumpUp/JumpDown states
- PlayerController drives animator parameters (Speed, IsGrounded, VelocityY) and rotates child model to face movement direction
- CapsuleCollider remains on root player GameObject for physics

### Controls

- **Primary**: ZED 2i body tracking (pelvis for X movement, physical jump or field goal gesture for jump)
- **Fallback**: Keyboard (Player 1: A/D/W, Player 2: Arrows). Active when `useExternalInput = false`
- **CPU**: `CpuPlayerInput` on Player2 in vs-CPU mode (see CPU Opponent below)
- **Debug** (Editor/development builds only): Q teleports Player 1 to WinZone for instant win; T toggles the tutorial; K/V in the lobby launch keyboard 2P / vs CPU

### CPU Opponent

- `CpuPlayerInput` sits disabled on Player2; `GameScreenManager.SetupCpuPlayer` enables it and sets `PlayerController.isCpu`
- Each landing: compute teleports-to-win per floor from the door pairs (`PortalDoor.beltIndex`, `DoorPairGenerator.GetDoorsOnBelt`), pick the door on the belt above with the fewest hops left (travel time breaks ties), steer under it with belt-speed feed-forward, jump when lined up
- Difficulty fields: `speedMultiplier`, `reactionTimeRange`, `jumpHesitationRange`, `wrongDoorChance` (random door that never goes down), `missChance` (lines up just outside the door's overlap window), `aimJitter`. Context menu has Easy/Normal/Hard presets
- Stands still while a human is frozen for tracking loss (`pauseWhileOpponentTrackingLost`), and always once the race had humans but none is left (e.g. during the return-to-lobby delay). A CPU-only race never waits
- A CPU beating a human shows "CPU Wins!" and plays `SFXManager.voiceCpuWins` (falls back to `voiceP2Wins`); CPU wins are never added to high scores

### CPU vs CPU Demo

- `GameScreenManager.cpuVsCpuDemo`: both players are CPUs (Player1 gets a `CpuPlayerInput` with default Normal settings), the tutorial is skipped, and each round restarts `demoRestartDelay` seconds after the win. For capturing footage or an attract loop; don't save the scene with it on
- `demoTimeScale` sets `Time.timeScale` every frame, so speed can be changed live; reset to 1 when the scene unloads
- Win screen reads "Player N Wins!" with the P1/P2 voice lines and inspector portraits, like a real 2P round

### Movement Mapping

- Velocity-based X movement (not direct position) to respect Rigidbody physics and collisions
- Pelvis keypoint used for X position (most stable; head/wrists move during gestures)
- `xTrackingSpeed` multiplier controls responsiveness (default 10)
- `trackingXOffset` accumulates teleport deltas so body-tracked position doesn't snap back after portal

### Input Separation

- `BodyTrackingInput` and `CpuPlayerInput` are separate components from `PlayerController`; both drive it through `SetHorizontalVelocity()` / `TryJump()` with `useExternalInput = true`
- Keeps existing keyboard code clean; game remains playable without ZED
- Rising-edge gesture detection prevents repeat-jumping while arms are raised
- Hold-duration gesture for lobby confirmation prevents accidental activation

### Teleportation Sequence

1. Door's `OnTriggerStay` detects airborne player inside trigger
2. Guards: game is Playing, cooldown clear (both doors), player hasn't teleported this jump, player has landed since last reset
3. Portal animation coroutine on PlayerController:
   - **Shrink phase**: player scales to zero while lerping toward the entry door's live position (tracks moving conveyor door)
   - Brief pause at zero scale (0.05s)
   - **Move**: teleport to destination, accumulate `trackingXOffset`
   - **Grow phase**: player scales back up at destination, feet offset adjusts as scale grows
4. Generation counter (`resetGeneration`) invalidates pending coroutines if a reset occurs mid-animation
5. BodyTrackingInput checks `IsFrozen` → skips input while teleporting

### Portal Door Visuals

- Each door uses the `ZedGames/PortalSpiral` shader with per-door parameters (speed, arms, rings, bands, angle, brightness, edge color)
- Paired doors share identical spiral parameters and base color
- Static `sharedSpiralMaterial` cached at class level; each door gets its own material instance
- Doors swivel continuously via Y-rotation oscillation (randomized center, range, speed, phase offset per door)

### Door Pairing Algorithm

1. Auto-find Floor_0 through Floor_N, create belts beneath each floor (except Floor_0)
2. Critical path: belt_0 → belt_2 → belt_4 → last belt (guarantees winnability)
3. Remaining doors: shuffled and paired randomly with max distance constraint of `total_belts - 3`
4. Unpaired doors are destroyed
5. Each pair gets a unique color from a 10-color palette with randomized spiral shader parameters
6. Conveyor belt direction alternates: even-indexed belts move right, odd move left

### Conveyor Belt Wrapping

- Ghost clone system for seamless visual wrap-around at belt edges
- When a door partially crosses a boundary, a visual-only clone appears on the opposite side
- Ghost clones have mesh + material copied but no collider (purely visual)
- Accounts for door rotation when computing projected X half-width (doors swivel)
- Once the door fully crosses, it wraps to the clone's position and the clone is destroyed

### Finish Line

- Visual ribbon mesh (FinishLineRibbon.fbx) with checkered shader spanning the win floor area
- On first player contact (`OnTriggerEnter`): ribbon is sliced at the player's X position
- Two halves created with material clip parameters (`_ClipSide`, `_ClipX`), physics rigidbodies kick them outward
- Halves self-destruct after 5 seconds
- `ResetFinishLine()` called by `GameManager.Restart()` to restore the ribbon

### UI System

UI uses App UI (UI Toolkit) with NES pixel theme, replacing the original legacy Canvas system:

- **PanelSettings**: Shared `GamePanelSettings.asset` (1920x1080 reference, scale with screen)
- **Theme**: `nes-theme.uss` — NES PPU color palette, PressStart2P pixel font, zero border-radius, chunky borders
- **Layouts**: UXML files define declarative UI structure, queried via `Q<T>("name")` in scripts
- **Blinking**: Implemented via `schedule.Execute().Every(500)` toggling opacity (no CSS keyframes in 2022.3)
- **Input passthrough**: Root panel uses `picking-mode: ignore` so gameplay clicks pass through
- **Win panel**: Shows winner, profile photo, time, high scores, then a restart row with gesture icon (frame_last.png at 48px) + "Raise hands to play again" text, and a 10s countdown. UIManager detects field-goal gesture from any tracked body (1s hold) to restart, or auto-returns to menu on countdown expiry. Keyboard shortcuts (Enter/Escape) still work for testing.

### Restart Strategy

In-place reset (no scene reload) to preserve body tracking state:

1. `StopAllCoroutines` on all players + increment `resetGeneration`
2. Reset player state (frozen, velocity, position, visibility, trackingXOffset, scale)
3. Regenerate doors
4. Reset finish line ribbon
5. Hide win panel
6. Wait one `WaitForFixedUpdate` to clear physics triggers
7. Double-verify player positions on Floor_0
8. Set state to Playing

Because the scene never reloads, nothing unloads runtime assets between rounds, so anything created per round must be destroyed explicitly: PortalDoor destroys its spiral material instance in `OnDestroy`, FinishLine destroys each ribbon half's material with the half, and UIManager destroys decoded leaderboard photos whenever the score list is cleared.

### High Score Persistence

- JSON file at `Application.persistentDataPath/highscores.json`
- Stores player name, time, and profile photo as base64-encoded PNG
- `HighScoreManager` uses `DontDestroyOnLoad` to survive scene transitions
