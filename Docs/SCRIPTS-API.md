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
| leftKey            | KeyCode    | A       | Move left (keyboard mode only) |
| rightKey           | KeyCode    | D       | Move right (keyboard mode only) |
| jumpKey            | KeyCode    | W       | Jump (keyboard mode only) |
| playerNumber       | int        | 1       | Player identity (1 or 2) |
| profilePhoto       | Texture2D  | null    | Square profile photo displayed above character |

### Hidden Fields (set at runtime)
| Field            | Type | Default | Description |
|------------------|------|---------|-------------|
| rb               | Rigidbody | auto | Rigidbody reference, public for BodyTrackingInput |
| useBodyTracking  | bool | false   | When true, keyboard input is disabled (BodyTrackingInput drives movement) |

### Public Properties
| Property    | Type | Description |
|-------------|------|-------------|
| IsGrounded  | bool | True when raycast hits Ground layer |
| IsFrozen    | bool | True during teleportation or win state |
| CanTeleport | bool | True when: hasn't teleported this jump AND has landed since last reset |

### Public Methods
| Method | Signature | Description |
|--------|-----------|-------------|
| TeleportTo | `(Vector3 destination)` | Instantly moves player to destination |
| StartDelayedTeleport | `(Vector3 destination, float delay)` | Coroutine: freeze → hide → wait → move → show → unfreeze |
| FreezeForWin | `()` | Stops all physics, makes visible |
| ResetPlayer | `(Vector3 spawnPosition)` | Full state reset: kills coroutines, increments generation counter, resets all flags and position |

### Animation
- Animator found via `GetComponentInChildren<Animator>()` on the HazmatManModel child
- Drives parameters: `Speed` (float, absolute horizontal input), `IsGrounded` (bool), `VelocityY` (float, vertical Rigidbody velocity)
- States: Idle (default), Walk (Speed > 0.1), Run (Speed > 0.6), JumpUp (VelocityY > 0.5, airborne), JumpDown (VelocityY < -0.5 or after JumpUp apex)
- Child model rotated to face movement direction: Y=180 (forward/idle), Y=90 (right), Y=270 (left)

### Important Internal State
- `resetGeneration` (int): Incremented on each reset. Coroutines capture this value and abort if it changes during their wait.
- `hasLandedSinceReset` (bool): Prevents teleports until the player has touched ground after a reset/game start.
- `frozen` (bool): When true, Update() returns immediately (no movement input processed).
- `modelTransform` (Transform): Reference to child model transform for facing rotation.

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
| Method          | Signature              | Description |
|-----------------|------------------------|-------------|
| PlayerWon       | `(int playerNumber)`   | Freezes all players, shows win panel, records high score |
| GetSpawnPosition| `(int playerNumber)`   | Returns spawn position on Floor_0 |
| GetElapsedTime  | `()`                   | Returns current timer value |

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
| GetScores        | `()` | Returns sorted List&lt;ScoreEntry&gt; |
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

---

## ZEDTrackingProvider.cs

Attached to: ZED_Rig_Mono (DontDestroyOnLoad singleton)

### Inspector Fields
| Field                  | Type       | Default | Description |
|------------------------|------------|---------|-------------|
| zedManager             | ZEDManager | auto    | Auto-found via GetComponent/FindObjectOfType |
| reidentificationRadius | float      | 1.0     | Max distance (meters) for auto-reassigning a lost body |

### Public Properties/Fields
| Property    | Type | Description |
|-------------|------|-------------|
| Instance    | static | Singleton accessor |
| IsZEDReady  | bool | True after ZEDManager initializes and body tracking starts |

### Events
| Event               | Signature | Description |
|---------------------|-----------|-------------|
| OnPlayerBodyUpdated | `Action<int, DetectedBody>` | Fires each frame for each assigned player with tracking data |
| OnPlayerBodyLost    | `Action<int>` | Fires when an assigned player's body is no longer tracked |

### Public Methods
| Method              | Signature | Description |
|---------------------|-----------|-------------|
| AssignBodyToPlayer  | `(int playerNumber, int bodyId)` | Binds a body to a player slot |
| UnassignPlayer      | `(int playerNumber)` | Removes player's body assignment |
| ClearAllAssignments | `()` | Removes all assignments |
| GetBodyForPlayer    | `(int playerNumber)` → DetectedBody | Returns tracked body or null |
| IsPlayerAssigned    | `(int playerNumber)` → bool | Check if player has a body |
| GetAssignedPlayerCount | `()` → int | Number of assigned players |
| GetUnassignedBodies | `()` → List&lt;DetectedBody&gt; | Bodies not assigned to any player |
| GetAllCurrentBodies | `()` → Dictionary&lt;int, DetectedBody&gt; | All currently tracked bodies |
| GetKeypointWorld    | `(DetectedBody, int keypointIndex)` → Vector3 | World-space position of a keypoint |
| GetKeypointConfidence | `(DetectedBody, int keypointIndex)` → float | Confidence value for a keypoint |

---

## GestureDetector.cs

Static utility (no MonoBehaviour)

### Static Methods
| Method             | Signature | Description |
|--------------------|-----------|-------------|
| IsFieldGoalGesture | `(Vector3[] keypoints, float[] confidences, float minConfidence = 50f, float minHeightAboveNose = 0.15f)` → bool | Both wrists above nose by minHeightAboveNose. Uses BODY_38 indices: nose=5, left_wrist=16, right_wrist=17 |

---

## StartScreenManager.cs

Attached to: StartScreenManager (lobby scene controller)

### Inspector Fields
| Field               | Type   | Default    | Description |
|---------------------|--------|------------|-------------|
| titleText           | Text   | auto-found | Title display |
| p1StatusText        | Text   | auto-found | Player 1 status |
| p2StatusText        | Text   | auto-found | Player 2 status |
| countdownText       | Text   | auto-found | P2 countdown |
| confirmHoldDuration | float  | 1.0        | Seconds to hold gesture for confirmation |
| p2CountdownDuration | float  | 10         | Seconds to wait for P2 before launching P1 only |
| showCameraPreview   | bool   | true       | Toggle ZED camera overlay in top-left corner |
| gameSceneName       | string | "GameScreen" | Scene to load on launch |

### Public Properties
| Property     | Type       | Description |
|--------------|------------|-------------|
| CurrentState | LobbyState | Current lobby state machine state |

### LobbyState Enum
`WaitingForP1Detection` → `WaitingForP1Confirm` → `WaitingForP2` → `WaitingForP2Confirm` → `Launching`

---

## BodyTrackingInput.cs

Attached to: Player GameObjects at runtime (added by GameScreenManager)

### Inspector Fields
| Field                   | Type       | Default | Description |
|-------------------------|------------|---------|-------------|
| playerNumber            | int        | 1       | Which player this tracks |
| physicalXMin / Max      | float      | -1.5 / 1.5 | Physical space bounds (meters) |
| gameXMin / Max          | float      | -6.5 / 6.5 | Game space bounds |
| xTrackingSpeed          | float      | 10      | Velocity multiplier for X tracking |
| jumpCooldown            | float      | 0.5     | Seconds between jumps |
| trackingLossGracePeriod | float      | 1.0     | Seconds before showing lost indicator |
| trackingLossTimeout     | float      | 10      | Seconds in Lost state before removal |
| trackingLostIndicator   | GameObject | null    | Red X overlay (created by GameScreenManager) |
| trackingLostTimerText   | Text       | null    | Countdown text (created by GameScreenManager) |

### Behavior
- Maps pelvis X from physical space to game space via `InverseLerp`/`Lerp`, applied as velocity
- Jump on rising edge of field goal gesture + grounded + cooldown clear
- Tracking states: `Tracking` → `Searching` (1s grace) → `Lost` (10s timeout → RemovePlayer)

---

## GameScreenManager.cs

Attached to: GameScreenManager (game scene orchestrator)

### Inspector Fields
| Field               | Type       | Default       | Description |
|---------------------|------------|---------------|-------------|
| player1Object       | GameObject | auto-found    | Player1 GameObject |
| player2Object       | GameObject | auto-found    | Player2 GameObject |
| trackingLostPrefab  | GameObject | null          | Optional prefab for lost indicator |
| startScreenSceneName| string     | "StartScreen" | Scene on all-players-lost |
| returnToStartDelay  | float      | 2             | Seconds before returning to StartScreen |

### Public Methods
| Method       | Signature            | Description |
|--------------|----------------------|-------------|
| RemovePlayer | `(int playerNumber)` | Deactivates player, unassigns body. If 0 players remain, returns to StartScreen |

### Behavior
- On Start: reads assignments from ZEDTrackingProvider, activates corresponding players
- Attaches BodyTrackingInput + creates tracking lost UI on each tracked player
- Sets initial spawn X from current physical body position
- Falls back to keyboard mode if no ZED assignments found
