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
| portalShrinkDuration | float    | 0.2     | Duration of shrink animation at entry door |
| portalGrowDuration | float      | 0.2     | Duration of grow animation at destination |
| playerNumber       | int        | 1       | Player identity (1 or 2) |
| profilePhoto       | Texture2D  | null    | Square profile photo displayed above character |

### Hidden Fields (set at runtime)
| Field            | Type | Default | Description |
|------------------|------|---------|-------------|
| rb               | Rigidbody | auto | Rigidbody reference, public for BodyTrackingInput |
| useBodyTracking  | bool | false   | When true, keyboard input is disabled (BodyTrackingInput drives movement) |
| trackingXOffset  | float | 0      | Cumulative X offset from teleports; used by BodyTrackingInput to prevent snap-back after portal |

### Public Properties
| Property    | Type | Description |
|-------------|------|-------------|
| IsGrounded  | bool | True when raycast hits Ground layer |
| IsFrozen    | bool | True during portal animation or win state |
| CanTeleport | bool | True when: hasn't teleported this jump AND has landed since last reset |

### Public Methods
| Method | Signature | Description |
|--------|-----------|-------------|
| TeleportTo | `(Transform entryDoor, Vector3 destination)` | Portal animation: shrink toward entry door → teleport → grow at destination. Tracks door's live position (moving conveyor) |
| TeleportTo | `(Vector3 destination)` | Legacy overload (keyboard mode / direct calls). Same animation without door tracking |
| StartDelayedTeleport | `(Vector3 destination, float delay)` | Delegates to portal animation coroutine (delay parameter unused, kept for compatibility) |
| FreezeForWin | `()` | Stops all physics, makes visible, sets kinematic |
| ResetPlayer | `(Vector3 spawnPosition)` | Full state reset: kills coroutines, increments generation counter, resets all flags, position, scale, and trackingXOffset |

### Portal Animation
1. Freeze player, set kinematic, zero velocity, mark teleportedThisJump
2. **Shrink**: Over `portalShrinkDuration`, scale from original to zero while lerping position toward `entryDoor.position` (tracks moving door each frame)
3. Move to destination, accumulate `trackingXOffset` with X delta
4. Brief pause at zero scale (0.05s)
5. **Grow**: Over `portalGrowDuration`, scale from zero to original at destination, adjusting Y offset so feet align
6. Restore non-kinematic, unfreeze
7. Aborts if `resetGeneration` changes mid-animation

### Animation
- Animator found via `GetComponentInChildren<Animator>()` on the HazmatManModel child
- Drives parameters: `Speed` (float, absolute horizontal input), `IsGrounded` (bool), `VelocityY` (float, vertical Rigidbody velocity)
- States: Idle (default), Walk (Speed > 0.1), Run (Speed > 0.6), JumpUp (VelocityY > 0.5, airborne), JumpDown (VelocityY < -0.5 or after JumpUp apex)
- Child model rotated to face movement direction: Y=180 (forward/idle), Y=90 (right), Y=270 (left)

### Profile Photo Quad
- Created in `Awake()` if `profilePhoto` is assigned
- Quad primitive child, 0.8 local scale, positioned above character (localY=1.2)
- Unlit/Texture material, collider removed
- Visibility managed separately from player renderers

### Important Internal State
- `resetGeneration` (int): Incremented on each reset. Coroutines capture this value and abort if it changes during their wait.
- `hasLandedSinceReset` (bool): Prevents teleports until the player has touched ground after a reset/game start.
- `frozen` (bool): When true, Update() returns immediately (no movement input processed). Ground check also skipped to prevent false transitions.
- `modelTransform` (Transform): Reference to child model transform for facing rotation.
- `originalScale` (Vector3): Captured in Awake(), used to restore scale after portal animation.

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

### Hidden Fields (set by DoorPairGenerator)
| Field            | Type  | Default | Description |
|------------------|-------|---------|-------------|
| spiralSpeed      | float | 1.0     | Spiral animation speed (negative = reverse) |
| spiralArms       | float | 6.0     | Number of spiral arms |
| spiralRings      | float | 5.0     | Number of rings |
| spiralBands      | float | 10.0    | Number of bands |
| spiralAngle      | float | 1.0472  | Spiral twist angle (radians) |
| spiralBrightness | float | 1.3     | Color brightness multiplier |
| edgeColor        | Color | dark brown | Edge/border color |
| swivelCenter     | float | 90      | Y-rotation midpoint (degrees) |
| swivelRange      | float | 25      | ± oscillation range (degrees) |
| swivelSpeed      | float | 1.0     | Oscillation speed |
| swivelOffset     | float | 0       | Phase offset (radians) |

### Public Methods
| Method   | Signature | Description |
|----------|-----------|-------------|
| SetColor | `(Color color)` | Updates doorColor and material `_BaseColor` if available |

### Behavior
- **Swivel**: Each frame, Y rotation oscillates as `swivelCenter + sin(time * swivelSpeed + swivelOffset) * swivelRange`
- **Material**: Uses `ZedGames/PortalSpiral` shader. Static `sharedSpiralMaterial` cached at class level; each door gets its own material instance with per-door parameters
- **Teleport**: `OnTriggerStay` checks game state, both-door cooldown, player airborne, CanTeleport. Calls `PlayerController.TeleportTo(transform, destination)` passing self as entry door
- **Cooldown**: Both the entry door and paired door track `lastTeleportTime`; both must have cleared cooldown

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
- Moves all child transforms (doors) horizontally each frame, skipping ghost clones
- **Ghost clone wrapping**: When a door partially crosses a boundary, a visual-only clone (mesh + material, no collider) appears on the opposite side. Syncs rotation with the real door each frame.
- Accounts for door rotation when computing effective X half-width (doors swivel, so projected width changes via cos/sin of Y rotation)
- Once the door center fully crosses the boundary, it wraps to the clone's position and the clone is destroyed
- Pauses when game state is not Playing
- Ghost clones are cleaned up on destroy

---

## DoorPairGenerator.cs

Attached to: GameManager

### Inspector Fields
| Field           | Type        | Default | Description |
|-----------------|-------------|---------|-------------|
| floors          | Transform[] | auto    | Floor transforms, bottom to top. Auto-found by name (Floor_0 through Floor_19) if empty. |
| beltLeftBound   | float       | -7      | Left boundary for door placement |
| beltRightBound  | float       | 7       | Right boundary for door placement |
| minDoorsPerBelt | int         | 3       | Minimum doors per conveyor belt |
| maxDoorsPerBelt | int         | 5       | Maximum doors per conveyor belt |
| doorSize        | Vector3     | (1, 1.4, 0.15) | Scale of door cubes |
| doorHangDown    | float       | 0.9     | How far below the ceiling doors hang |
| conveyorSpeed   | float       | 1.5     | Speed passed to ConveyorBelt |
| doorColors      | Color[]     | 10 colors | Palette for door pair coloring |

### Public Methods
| Method   | Signature | Description |
|----------|-----------|-------------|
| Generate | `()`      | Destroys existing doors, auto-finds floors, creates new belts + doors, pairs them with spiral shader params |

### Door Creation Details
- Each door is a Cube primitive with BoxCollider set to trigger, size (1.5, 1, 3)
- PortalDoor component added with randomized swivel parameters (center 75-105°, range 15-30°, speed 0.8-2.0, random phase)
- Random dark edge color per door

### Pairing Details
- **Critical path**: belt_0 → belt_2 → belt_4 → last belt (requires ≥5 belts)
- **Random pairs**: shuffled, max distance constraint of `Max(1, beltCount - 3)` belt levels apart
- **Spiral params**: Each pair shares identical spiral parameters (speed, arms, rings, bands, angle, brightness) with optional spin direction flip
- Unpaired doors are destroyed
- Belt direction alternates: even = right, odd = left

---

## GameManager.cs

Attached to: GameManager (singleton)

### Inspector Fields
| Field          | Type              | Default | Description |
|----------------|-------------------|---------|-------------|
| doorGenerator  | DoorPairGenerator | auto    | Auto-found on same GameObject |
| uiManager      | UIManager         | auto    | Auto-found via FindObjectOfType |
| teleportDelay  | float             | 0.3     | Seconds player is hidden during teleport (legacy, unused by portal animation) |

### Public Properties
| Property     | Type      | Description |
|--------------|-----------|-------------|
| Instance     | static    | Singleton accessor |
| CurrentState | GameState | Playing or Won |

### Public Methods
| Method          | Signature              | Description |
|-----------------|------------------------|-------------|
| PlayerWon       | `(int playerNumber)`   | Freezes all players, shows win panel with profile photo, records high score |
| GetSpawnPosition| `(int playerNumber)`   | Returns spawn position on Floor_0 (dynamically reads Floor_0 Y position) |
| GetElapsedTime  | `()`                   | Returns current timer value |

### Restart Flow
1. Reset all players to spawn positions (kills coroutines, increments resetGeneration)
2. Regenerate doors
3. Reset finish line ribbon (`FindObjectOfType<FinishLine>().ResetFinishLine()`)
4. Hide win panel
5. Reset timer
6. Wait one FixedUpdate, then double-verify player positions
7. Set state to Playing

---

## FinishLine.cs

Attached to: FinishLine (positioned just below WinZone)

### Inspector Fields
| Field            | Type  | Default | Description |
|------------------|-------|---------|-------------|
| sliceFallSpeed   | float | 3       | Speed at which cut halves fall/fly away |
| sliceRotateSpeed | float | 90      | Angular velocity of cut halves (degrees/sec) |

### Public Methods
| Method          | Signature | Description |
|-----------------|-----------|-------------|
| ResetFinishLine | `()`      | Destroys halves, re-enables original ribbon, resets sliced flag |

### Behavior
- `OnTriggerEnter`: Fires once per game (guarded by `sliced` flag). Detects `PlayerController` on collider, calls `Slice(playerX)`
- `Slice`: Hides original ribbon renderer, creates two GameObjects (`FinishLine_Left`, `FinishLine_Right`) each with:
  - Copy of ribbon mesh and material with `_ClipSide` (1=left, 2=right) and `_ClipX` set to player's world X
  - Rigidbody with outward velocity + angular velocity
  - Self-destruct after 5 seconds
- Requires child GameObject with MeshFilter + MeshRenderer using `ZedGames/FinishLineCheckered` shader (provides `_ClipSide` and `_ClipX` properties)

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

Attached to: GameScreenManager (alongside UIDocument)

### Inspector Fields
| Field                  | Type            | Default | Description |
|------------------------|-----------------|---------|-------------|
| highScoreEntryTemplate | VisualTreeAsset | null    | UXML template for cloning score rows (falls back to manual creation) |

### UI Toolkit Elements (queried via `Q<T>("name")` in Awake)
| Element             | Type                  | UXML Name          |
|---------------------|-----------------------|--------------------|
| timerEl             | Heading               | timer-text         |
| winPanelEl          | VisualElement         | win-panel          |
| winnerTextEl        | Heading               | winner-text        |
| winnerPhotoEl       | VisualElement         | winner-photo       |
| finalTimeEl         | Heading               | final-time-text    |
| highScoreLabelEl    | Unity.AppUI.UI.Text   | high-score-label   |
| highScoreListEl     | VisualElement         | high-score-list    |
| instructionsEl      | Unity.AppUI.UI.Text   | instructions-text  |

### Public Methods
| Method       | Signature | Description |
|--------------|-----------|-------------|
| UpdateTimer  | `(float time)` | Updates timer text as MM:SS:MS |
| ShowWinPanel | `(int player, float time, bool isHighScore, Texture2D photo = null)` | Shows win panel; sets winner photo via `style.backgroundImage`; blinks high score label via `schedule.Execute().Every(500)` |
| HideWinPanel | `()` | Hides panel via `style.display = None`, clears score list |

---

## ViewModeToggle.cs

Editor utility (ExecuteInEditMode)

### Inspector Fields
| Field        | Type | Default | Description |
|--------------|------|---------|-------------|
| showOpenPose | bool | false   | Toggle OpenPose skeleton layer visibility on all cameras |

### Behavior
- Toggles Layer 3 (OpenPose) culling mask on all cameras
- Runs in edit mode for in-editor preview control

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

Attached to: StartScreenManager (lobby scene controller, with UIDocument)

### Inspector Fields
| Field               | Type   | Default    | Description |
|---------------------|--------|------------|-------------|
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

### UI
- Uses UIDocument with `StartScreen.uxml` layout
- Elements queried via `Q<T>("name")`: title-text, prompt-text, countdown-text, p1-status, p2-status

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

Attached to: GameScreenManager (game scene orchestrator, alongside UIManager and UIDocument)

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
