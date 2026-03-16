# Scene Setup - GameScreen

## Hierarchy

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
Floor_6              Cube, Layer: Ground(8), scale (14, 0.3, 1)  <- Roof (no gameplay, just ceiling for last belt)
Wall_Left            Cube, Layer: Ground(8), X=-7.25, scale (0.5, 16, 1)
Wall_Right           Cube, Layer: Ground(8), X=+7.25, scale (0.5, 16, 1)
Player1              Capsule, Layer: Player(9), Rigidbody + PlayerController
Player2              Capsule, Layer: Player(9), Rigidbody + PlayerController
GameManager          Empty, GameManager + DoorPairGenerator + HighScoreManager
WinZone              Empty, BoxCollider(trigger) + WinTrigger, spans Floor_5 area
Canvas               Screen Space Overlay, CanvasScaler (1920x1080 ref), UIManager
  TimerText          Text, top-center, "00:00:00"
  WinPanel           Panel (starts inactive), dark semi-transparent bg
    WinnerText       Text, "Player X Wins!"
    WinnerPhoto      RawImage (auto-created if missing)
    FinalTimeText    Text
    HighScoreLabel   Text
    HighScoreList    Container (auto-created if missing)
    InstructionsText Text
EventSystem          EventSystem + StandaloneInputModule
```

## Layers

| Index | Name    | Purpose                                    |
|-------|---------|--------------------------------------------|
| 0     | Default | Floors, walls, doors, general objects       |
| 8     | Ground  | Floor/wall colliders (used by ground raycast) |
| 9     | Player  | Player capsules (self-collision disabled)    |

## Runtime-Generated Objects

The following are created by `DoorPairGenerator.Generate()` at runtime under the GameManager:

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

| Property       | Player 1         | Player 2              |
|----------------|------------------|-----------------------|
| playerNumber   | 1                | 2                     |
| leftKey        | A                | LeftArrow (276)       |
| rightKey       | D                | RightArrow (275)      |
| jumpKey        | W                | UpArrow (273)         |
| groundLayer    | Ground (bit 256) | Ground (bit 256)      |
| profilePhoto   | profile-demo-player-1.jpg | profile-demo-player-2.jpg |
| Rigidbody      | Constraints: FreezeZ + FreezeRotation, Interpolate, Continuous CD |

## Camera

- Orthographic, size 8.5
- Position: (0, 7.5, -10)
- Covers all 7 floors (0 through 6) within the view
