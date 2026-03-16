# Scripts API Reference

## PlayerController.cs

Attached to: Player1, Player2

### Inspector Fields
| Field              | Type       | Default | Description |
|--------------------|------------|---------|-------------|
| moveSpeed          | float      | 6       | Horizontal movement speed |
| jumpForce          | float      | 8       | Impulse force applied on jump |
| groundCheckDistance| float      | 0.15    | Raycast length for ground detection |
| groundLayer        | LayerMask  | -       | Layer mask for ground check (set to Ground) |
| leftKey            | KeyCode    | A       | Move left |
| rightKey           | KeyCode    | D       | Move right |
| jumpKey            | KeyCode    | W       | Jump |
| playerNumber       | int        | 1       | Player identity (1 or 2) |
| profilePhoto       | Texture2D  | null    | Square profile photo displayed above capsule |

### Public Properties
| Property    | Type | Description |
|-------------|------|-------------|
| IsGrounded  | bool | True when raycast hits Ground layer |
| CanTeleport | bool | True when: hasn't teleported this jump AND has landed since last reset |

### Public Methods
| Method | Signature | Description |
|--------|-----------|-------------|
| TeleportTo | `(Vector3 destination)` | Instantly moves player to destination, sets kinematic briefly to avoid velocity warning |
| StartDelayedTeleport | `(Vector3 destination, float delay)` | Starts coroutine: freeze -> hide -> wait -> move -> show -> unfreeze |
| FreezeForWin | `()` | Stops all physics, makes visible, called when any player wins |
| ResetPlayer | `(Vector3 spawnPosition)` | Full state reset: kills coroutines, increments generation counter, resets all flags and position |

### Important Internal State
- `resetGeneration` (int): Incremented on each reset. Coroutines capture this value and abort if it changes during their wait.
- `hasLandedSinceReset` (bool): Prevents teleports until the player has touched ground after a reset/game start.
- `frozen` (bool): When true, Update() returns immediately (no movement input processed).

---

## PortalDoor.cs

Attached to: Runtime-generated door cubes

### Inspector Fields
| Field            | Type       | Default | Description |
|------------------|------------|---------|-------------|
| pairedDoor       | PortalDoor | null    | The destination door (set by DoorPairGenerator) |
| doorColor        | Color      | white   | Visual color (set by DoorPairGenerator) |
| cooldownTime     | float      | 0.5     | Seconds before this door can teleport again |
| teleportYOffset  | float      | -0.5    | Y offset from paired door's position for destination |

### Behavior
- `OnTriggerStay`: Checks game state, cooldown, player airborne, CanTeleport
- Delegates teleport execution to `PlayerController.StartDelayedTeleport()` or `TeleportTo()`
- Color applied via `MaterialPropertyBlock` on `_BaseColor`

---

## ConveyorBelt.cs

Attached to: Runtime-generated belt parent objects

### Inspector Fields
| Field     | Type  | Default | Description |
|-----------|-------|---------|-------------|
| speed     | float | 1.5     | Movement speed in units/sec |
| leftBound | float | -6      | Left wrap boundary (local X) |
| rightBound| float | 6       | Right wrap boundary (local X) |
| moveRight | bool  | true    | Direction of movement |

### Behavior
- Moves all child transforms (doors) horizontally each frame
- Wraps children that exceed boundaries
- Pauses when game state is not Playing

---

## DoorPairGenerator.cs

Attached to: GameManager

### Inspector Fields
| Field           | Type        | Default | Description |
|-----------------|-------------|---------|-------------|
| floors          | Transform[] | auto    | Floor transforms, bottom to top. Auto-found by name if empty. |
| beltLeftBound   | float       | -6      | Left boundary for door placement |
| beltRightBound  | float       | 6       | Right boundary for door placement |
| minDoorsPerBelt | int         | 3       | Minimum doors per conveyor belt |
| maxDoorsPerBelt | int         | 5       | Maximum doors per conveyor belt |
| doorSize        | Vector3     | (1, 1.4, 0.4) | Scale of door cubes |
| doorHangDown    | float       | 0.9     | How far below the ceiling doors hang |
| conveyorSpeed   | float       | 1.5     | Speed passed to ConveyorBelt |
| doorColors      | Color[]     | 10 colors | Palette for door pair coloring |

### Public Methods
| Method   | Signature | Description |
|----------|-----------|-------------|
| Generate | `()`      | Destroys existing doors, creates new belts + doors, pairs them |

### Pairing Algorithm
1. Auto-find Floor_0 through Floor_N
2. Create belts: one belt per floor gap (belt_i hangs beneath floor_i+1)
3. Critical path pairs: belt_0->belt_2, belt_2->belt_4, belt_4->last_belt
4. Random pairs: shuffle remaining, pair within max distance of `total_belts - 3`
5. Destroy any unpaired doors

---

## GameManager.cs

Attached to: GameManager (singleton)

### Inspector Fields
| Field          | Type              | Default | Description |
|----------------|-------------------|---------|-------------|
| doorGenerator  | DoorPairGenerator | auto    | Auto-found on same GameObject |
| uiManager      | UIManager         | auto    | Auto-found via FindObjectOfType |
| teleportDelay  | float             | 0.3     | Seconds player is hidden during teleport |

### Public Properties
| Property     | Type      | Description |
|--------------|-----------|-------------|
| Instance     | static    | Singleton accessor |
| CurrentState | GameState | Playing or Won |

### Public Methods
| Method    | Signature           | Description |
|-----------|---------------------|-------------|
| PlayerWon | `(int playerNumber)` | Freezes all players, shows win panel, records high score |

### Restart Sequence
1. Reset all players (kills coroutines, resets position to Floor_0)
2. Regenerate doors
3. Hide win panel
4. Wait one FixedUpdate (clears physics triggers)
5. Re-verify player positions
6. Set state to Playing

---

## WinTrigger.cs

Attached to: WinZone

### Behavior
- `OnTriggerStay`: Fires only when game is Playing and player `IsGrounded`
- Calls `GameManager.PlayerWon(playerNumber)`

---

## HighScoreManager.cs

Attached to: GameManager (DontDestroyOnLoad singleton)

### Public Methods
| Method           | Signature | Description |
|------------------|-----------|-------------|
| IsHighScore      | `(float time)` | True if time qualifies for top 10 |
| AddScore         | `(string name, float time, Texture2D photo = null)` | Adds score with optional profile photo |
| GetScores        | `()` | Returns sorted List<ScoreEntry> |
| LoadProfilePhoto | `(ScoreEntry entry)` | Decodes base64 PNG back to Texture2D |

### ScoreEntry Fields
- `playerName` (string)
- `time` (float)
- `profilePhotoBase64` (string) - PNG encoded as base64

---

## UIManager.cs

Attached to: Canvas

### Auto-Discovery
All UI elements are found by name under Canvas/WinPanel. Missing elements (WinnerPhoto, HighScoreList) are auto-created at runtime.

### Public Methods
| Method       | Signature | Description |
|--------------|-----------|-------------|
| UpdateTimer  | `(float time)` | Updates timer text as MM:SS:MS |
| ShowWinPanel | `(int player, float time, bool isHighScore, Texture2D photo = null)` | Shows win screen with scores |
| HideWinPanel | `()` | Hides panel, destroys score entries |
