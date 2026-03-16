# Scene Setup

## StartScreen

### Hierarchy

```
Main Camera          Orthographic, default settings
Directional Light    Default scene lighting
ZED_Rig_Mono         Prefab instance (DontDestroyOnLoad)
  ├── ZEDManager       Camera connection, body tracking, bodyFormat=BODY_38
  ├── ZEDTrackingProvider  Singleton, player-body assignments
  └── Camera_Left      Small viewport preview (top-left, 25% of screen, depth=10)
        └── Frame      Rendering plane for camera feed
StartScreenManager   StartScreenManager script, references UI texts
Canvas               Screen Space Overlay, CanvasScaler (1920x1080 ref)
  ├── TitleText        Text, "DOORS & FLOORS", font size 72, centered
  ├── P1StatusText     Text, bottom-left, player 1 status
  ├── P2StatusText     Text, bottom-right, player 2 status
  └── CountdownText    Text, centered, P2 countdown timer
```

### ZEDManager Configuration

Aligned with the BodyTrackingMulti sample scene. Key settings:

- `inputType`: USB
- `bodyFormat`: BODY_38
- `bodyTrackingModel`: HUMAN_BODY_FAST
- `bodyTrackingTracking`: true
- `bodyTrackingConfidenceThreshold`: 40
- `bodyTrackingMinimumKPThreshold`: 8
- `bodyTrackingMaxRange`: 10m
- `bodyTrackingSkeletonSmoothing`: 0
- `dontDestroyOnLoad`: true
- `trackingIsStatic`: true
- `enableSpatialMemory`: false
- `positionalTrackingMode`: GEN_1

---

## GameScreen

### Hierarchy

```
Main Camera          Orthographic, size 8.5, position (0, 7.5, -10), dark bg
Directional Light    Default scene lighting
Global Volume        Post-processing (URP)
Floor_0              Cube, Layer: Ground(8), scale (14, 0.3, 1)
Floor_1              Cube, Layer: Ground(8), scale (14, 0.3, 1)
Floor_2              Cube, Layer: Ground(8), scale (14, 0.3, 1)
Floor_3              Cube, Layer: Ground(8), scale (14, 0.3, 1)
Floor_4              Cube, Layer: Ground(8), scale (14, 0.3, 1)
Floor_5              Cube, Layer: Ground(8), scale (14, 0.3, 1)  <- Win floor
Floor_6              Cube, Layer: Ground(8), scale (14, 0.3, 1)  <- Roof (ceiling for last belt)
Wall_Left            Cube, Layer: Ground(8), X=-7.25, scale (0.5, 16, 1)
Wall_Right           Cube, Layer: Ground(8), X=+7.25, scale (0.5, 16, 1)
Player1              Capsule, Layer: Player(9), Rigidbody + PlayerController
Player2              Capsule, Layer: Player(9), Rigidbody + PlayerController
GameManager          Empty, GameManager + DoorPairGenerator + HighScoreManager
GameScreenManager    Empty, GameScreenManager (references Player1, Player2)
WinZone              Empty, BoxCollider(trigger) + WinTrigger, spans Floor_5 area
Canvas               Screen Space Overlay, CanvasScaler (1920x1080 ref), UIManager
  ├── TimerText      Text, top-center, "00:00:00"
  └── WinPanel       Panel (starts inactive), dark semi-transparent bg
        ├── WinnerText       Text, "Player X Wins!"
        ├── WinnerPhoto      RawImage (auto-created if missing)
        ├── FinalTimeText    Text
        ├── HighScoreLabel   Text
        ├── HighScoreList    Container (auto-created if missing)
        └── InstructionsText Text
EventSystem          EventSystem + StandaloneInputModule
```

Note: ZED_Rig_Mono is NOT in this scene — it persists from StartScreen via DontDestroyOnLoad.

### Runtime-Attached Components

When body tracking is active, `GameScreenManager` attaches at runtime:

- **BodyTrackingInput** on Player1 and/or Player2 (depending on assignments)
- **TrackingLostIndicator** child on each tracked player (world-space canvas with red "X TRACKING LOST" text + countdown)

---

## Layers

| Index | Name    | Purpose                                       |
| ----- | ------- | --------------------------------------------- |
| 0     | Default | Floors, walls, doors, general objects         |
| 8     | Ground  | Floor/wall colliders (used by ground raycast) |
| 9     | Player  | Player capsules (self-collision disabled)     |

## Runtime-Generated Objects

Created by `DoorPairGenerator.Generate()` at runtime under GameManager:

```
GameManager/
  ConveyorBelt_Floor0/     <- Hangs below Floor_1
    Door_F0_0              <- Cube with PortalDoor + BoxCollider(trigger)
    Door_F0_1
    ...
  ConveyorBelt_Floor1/     <- Hangs below Floor_2
    Door_F1_0
    ...
  ...
  ConveyorBelt_Floor5/     <- Hangs below Floor_6 (roof)
    Door_F5_0
    ...
```

Each door is a Cube primitive with:

- Scale: (1.0, 1.4, 0.4)
- BoxCollider set to trigger, size (1.5, 1.5, 3.0)
- PortalDoor component with paired reference and color

## Player Configuration

| Property        | Player 1                                                          | Player 2         |
| --------------- | ----------------------------------------------------------------- | ---------------- |
| playerNumber    | 1                                                                 | 2                |
| leftKey         | A                                                                 | LeftArrow        |
| rightKey        | D                                                                 | RightArrow       |
| jumpKey         | W                                                                 | UpArrow          |
| groundLayer     | Ground (Layer 8)                                                  | Ground (Layer 8) |
| useBodyTracking | false (set at runtime by GameScreenManager)                       | same             |
| Rigidbody       | Constraints: FreezeZ + FreezeRotation, Interpolate, Continuous CD |

## Camera

- Orthographic, size 8.5
- Position: (0, 7.5, -10)
- Covers all 7 floors (0 through 6) within the view
