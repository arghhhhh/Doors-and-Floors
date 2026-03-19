# Scene Setup

## StartScreen

### Hierarchy

```
Main Camera          Perspective, default settings
Directional Light    Default scene lighting
VideoQuad            Quad (8×4.5), VideoPlayer loops hands_up_gesture.mp4, URP Unlit mat
ZED_Rig_Mono         Prefab instance (DontDestroyOnLoad)
  ├── ZEDManager       Camera connection, body tracking, bodyFormat=BODY_38
  ├── ZEDTrackingProvider  Singleton, player-body assignments
  └── Camera_Left      Small viewport preview (top-left, 25% of screen, depth=10)
        └── Frame      Rendering plane for camera feed
StartScreenManager   StartScreenManager + UIDocument (StartScreen.uxml)
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
Main Camera          Perspective, FOV 60, position (0, 7.5, -10), solid color bg
Directional Light    Default scene lighting
Global Volume        Post-processing (URP)
Floor_0              Cube, Layer: Ground(8), scale (14, 0.3, 1), Y=-0.88, Bricks mat
Floor_1              Cube, Layer: Ground(8), scale (14, 0.3, 1), Y=1.62, Bricks mat
Floor_2              Cube, Layer: Ground(8), scale (14, 0.3, 1), Y=4.14, Bricks mat
Floor_3              Cube, Layer: Ground(8), scale (14, 0.3, 1), Y=6.62, Bricks mat
Floor_4              Cube, Layer: Ground(8), scale (14, 0.3, 1), Y=9.12, Bricks mat
Floor_5              Cube, Layer: Ground(8), scale (14, 0.3, 1), Y=11.62, Bricks mat
Floor_6              Cube, Layer: Ground(8), scale (14, 0.3, 1), Y=14.12, Bricks mat  <- Roof
Wall_Left            Cube, Layer: Ground(8), X=-7.25, scale (0.5, 16, 1), Bricks mat
Wall_Right           Cube, Layer: Ground(8), X=+7.25, scale (0.5, 16, 1), Bricks mat
Player1              Layer: Player(9), scale (0.34, 0.34, 0.34), Rigidbody + PlayerController
Player2              Layer: Player(9), scale (0.34, 0.34, 0.34), Rigidbody + PlayerController
GameManager          Empty, GameManager + DoorPairGenerator + HighScoreManager
GameScreenManager    Empty, GameScreenManager + UIManager + UIDocument (GameScreen.uxml)
FinishLine           BoxCollider(trigger, size 14x0.5x2), FinishLine script, Y=12.25
  └── [FinishLineRibbon]  Child mesh (FinishLineRibbon.fbx) with FinishLineCheckered material
WinZone              BoxCollider(trigger), WinTrigger, position (0, 12.5, 0), scale (14, 1, 1)
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
| 3     | OpenPose| ZED skeleton visualization (toggled by ViewModeToggle) |
| 8     | Ground  | Floor/wall colliders (used by ground raycast) |
| 9     | Player  | Player capsules (self-collision disabled)     |

## Runtime-Generated Objects

Created by `DoorPairGenerator.Generate()` at runtime under GameManager:

```
GameManager/
  ConveyorBelt_Floor0/     <- Hangs below Floor_1
    Door_F0_0              <- Cube with PortalDoor + BoxCollider(trigger)
    Door_F0_1
    Door_F0_0_ghost        <- Visual-only clone (no collider) during wrap-around
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

- Scale: (1.0, 1.4, 0.15)
- BoxCollider set to trigger, size (1.5, 1, 3)
- PortalDoor component with paired reference, color, spiral shader params, and swivel animation
- Material: `ZedGames/PortalSpiral` shader instance with per-door parameters
- Y-rotation oscillates continuously (swivel animation)
- Ghost clones appear during wrap-around (mesh + material only, no collider)

## Player Configuration

| Property        | Player 1                                                          | Player 2         |
| --------------- | ----------------------------------------------------------------- | ---------------- |
| playerNumber    | 1                                                                 | 2                |
| leftKey         | A                                                                 | LeftArrow        |
| rightKey        | D                                                                 | RightArrow       |
| jumpKey         | W                                                                 | UpArrow          |
| groundLayer     | Ground (Layer 8)                                                  | Ground (Layer 8) |
| useBodyTracking | false (set at runtime by GameScreenManager)                       | same             |
| Scale           | (0.34, 0.34, 0.34)                                               | same             |
| CapsuleCollider | center (0, 1.375, 0), height 2.75, radius 0.75                   | same             |
| Rigidbody       | Constraints: FreezeZ + FreezeRotation, Interpolate, Continuous CD | same             |
| Position        | (-3, -0.3, 0)                                                     | (3, -0.3, 0)    |

## Camera

- Perspective, FOV 60
- Position: (0, 7.5, -10)
- Covers all 7 floors (0 through 6) within the view
- Solid color background

## Materials

| Material | Shader | Texture | Notes |
| -------- | ------ | ------- | ----- |
| Bricks (`Assets/Materials/Bricks.mat`) | `ZedGames/WorldSpaceUnlit` | `Assets/Media/bricks.jpg` | World-space XY UVs so all floors/walls tile seamlessly as one surface. Tiling (2,2). |

The `WorldSpaceUnlit` shader (`Assets/Shaders/WorldSpaceUnlit.shader`) projects the texture using world-space XY coordinates instead of mesh UVs. This makes separate cubes appear as a single continuous surface. Parameters: `_Tiling` (Vector2), `_Offset` (Vector2).

Editor utility: `Tools > ZedGames > Setup Bricks Material` re-creates the material and bulk-applies it.

## UI System

Both scenes use App UI (UI Toolkit) instead of legacy Canvas:

- **PanelSettings**: `Assets/UI/Settings/GamePanelSettings.asset` (1920x1080 reference, scale with screen)
- **StartScreen**: UIDocument on StartScreenManager → `StartScreen.uxml`
- **GameScreen**: UIDocument on GameScreenManager → `GameScreen.uxml`
- **Theme**: `nes-theme.uss` (NES pixel font, color palette, zero border-radius)
- No EventSystem needed (UI Toolkit handles its own input)
