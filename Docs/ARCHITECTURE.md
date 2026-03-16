# Portal Racing Game - Architecture

## Overview

A 2-player racing game where players race from the bottom floor to the top floor of a building by jumping into color-coded portal doors hanging from conveyor belts on each floor's ceiling. Built in Unity 2022.3 as a 3D project with orthographic camera (simulating 2D), designed for future ZED 2i stereo camera body tracking integration.

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
  Scenes/
    GameScreen.unity        # Main gameplay scene
  profile-demo-player-1.jpg # Demo profile photo for Player 1
  profile-demo-player-2.jpg # Demo profile photo for Player 2
Docs/
  ARCHITECTURE.md           # This file
  SCENE-SETUP.md            # Scene hierarchy and object configuration
  SCRIPTS-API.md            # Public API reference for all scripts
  ZED-INTEGRATION-GUIDE.md  # Guide for body tracking agent
```

## Game Flow

```
Start() -> DoorPairGenerator.Generate() -> GameState.Playing
                                                |
                                          Timer runs, players move
                                                |
                                    Player lands on top floor (grounded)
                                                |
                                          GameState.Won
                                      All players frozen
                                      Win panel + high scores
                                                |
                                  Enter -> Restart()  |  Escape -> StartScreen
                                      Reset players to Floor_0
                                      Regenerate doors
                                      Wait one FixedUpdate
                                      GameState.Playing
```

## Key Design Decisions

### Physics
- 3D Rigidbody with constraints: Freeze Z position, freeze all rotation
- Players at Z = -0.3 (in front of doors for rendering)
- CapsuleCollider on players, BoxCollider triggers on doors
- Ground detection: downward raycast in FixedUpdate, accounts for transform scale
- Players on Layer 9 ("Player") with self-collision disabled

### Controls (Current - Keyboard)
- **Player 1**: A/D move, W jump
- **Player 2**: Left/Right arrows, Up arrow jump
- **Future**: X position driven by ZED body tracking, jump by gesture

### Teleportation Sequence
1. Door's `OnTriggerStay` detects airborne player inside trigger
2. Guards: game is Playing, cooldown clear, player hasn't teleported this jump, player has landed since last reset
3. Coroutine on PlayerController: freeze player -> hide -> wait `teleportDelay` -> move to destination -> show -> unfreeze
4. Generation counter (`resetGeneration`) invalidates pending coroutines if a reset occurs mid-teleport

### Door Pairing Algorithm
1. Auto-find Floor_0 through Floor_N, create belts beneath each floor (except Floor_0)
2. Critical path: belt_0 -> belt_2 -> belt_4 -> last belt (guarantees winnability)
3. Remaining doors: shuffled and paired randomly with max distance constraint of `total_belts - 3`
4. Unpaired doors are destroyed
5. Each pair gets a unique color from a 10-color palette

### Restart Strategy
In-place reset (no scene reload) to preserve future body tracking state:
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
