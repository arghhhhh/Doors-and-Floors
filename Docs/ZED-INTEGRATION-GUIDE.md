# ZED Body Tracking Integration Guide

## Goal

Replace keyboard input with ZED 2i stereo camera body tracking so that:
1. Two real people standing in front of the camera control the two in-game players
2. Player X position maps to tracked body X position
3. Jump is triggered by a body gesture (e.g., raising arms, crouching then standing)
4. Profile photos are captured from the ZED camera feed during player activation in StartScreen

## Current Player Input Architecture

### How Players Move Now

In `PlayerController.cs`, movement happens in `Update()`:

```csharp
float moveX = 0f;
if (Input.GetKey(leftKey)) moveX -= 1f;
if (Input.GetKey(rightKey)) moveX += 1f;
rb.velocity = new Vector3(moveX * moveSpeed, rb.velocity.y, 0f);

if (Input.GetKeyDown(jumpKey) && isGrounded)
    rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
```

### What Needs to Change

The body tracking system needs to:
1. **Set player X position** directly (not velocity) based on tracked body position
2. **Trigger jump** based on gesture detection
3. **NOT interfere** with teleportation (frozen state, visibility, Z-position clamping)

### Integration Points

```csharp
// PlayerController already has these guards:
if (frozen) return;  // Don't move during teleport
if (GameManager.Instance.CurrentState != GameState.Playing) return;  // Don't move after win

// The body tracking system should respect these same guards.
// Recommended approach: add a public method like:
public void SetBodyTrackingInput(float worldX, bool jumpGesture)
{
    if (frozen) return;
    if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        return;

    // Map tracked X to game world X (clamped to walls at -7 to +7)
    Vector3 pos = transform.position;
    pos.x = Mathf.Clamp(worldX, -6.5f, 6.5f);
    transform.position = pos;

    if (jumpGesture && isGrounded)
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
}
```

## Persistence Requirements

### What Must Survive Scene Transitions

The ZED tracking system must persist across:
- **StartScreen -> GameScreen**: Body tracking active throughout
- **GameScreen restart** (in-place reset, no scene reload): Body tracking unaffected
- **GameScreen -> StartScreen** (if implemented): Body tracking continues

**Implementation**: Use `DontDestroyOnLoad` on the ZED manager object, same pattern as `HighScoreManager`.

### What Must Survive Game Restarts

Game restarts are **in-place** (no scene reload) specifically to preserve external state like body tracking. The restart flow:
1. All players get `ResetPlayer()` called (kills coroutines, resets position)
2. Doors regenerated
3. Wait one physics frame
4. State set to Playing

Body tracking input will be ignored during steps 1-3 because players are `frozen`.

## Player-Body Pairing

### Current Setup
- `Player1` (playerNumber=1): left side of screen, starts at X=-3
- `Player2` (playerNumber=2): right side of screen, starts at X=+3

### Suggested Pairing Strategy
1. During **StartScreen activation phase**: detect two bodies
2. Assign the left body to Player 1, right body to Player 2 (by X position)
3. Store body tracking IDs so they persist even if people cross paths
4. If a tracked body is lost, freeze that player until re-acquired

## Profile Photo Capture

### Current System
- `PlayerController.profilePhoto` (Texture2D) is assigned in inspector
- A quad with Unlit/Texture material renders above the capsule
- On win, the photo is stored as base64 PNG in `HighScoreManager`

### ZED Integration
1. During StartScreen activation, capture a frame from ZED's left camera
2. Crop the face region of each detected body
3. Assign the cropped Texture2D to the corresponding `PlayerController.profilePhoto`
4. The rest (quad rendering, high score storage) is already handled

## Scene Hierarchy - Relevant Objects

```
Player1    Layer: Player(9), Capsule + Rigidbody + PlayerController
Player2    Layer: Player(9), Capsule + Rigidbody + PlayerController
GameManager  GameManager + DoorPairGenerator + HighScoreManager
```

## Important Constraints

1. **Z position**: Players are clamped to Z=-0.3 (in front of doors). Body tracking should only affect X.
2. **Y position**: Controlled by physics (gravity + jump). Body tracking should NOT set Y directly.
3. **Frozen state**: When `frozen == true`, all input must be ignored. This happens during teleportation and win state.
4. **Ground check**: Runs in `FixedUpdate`, uses raycast on Layer 8 (Ground). Jump gestures should check `isGrounded` before triggering.
5. **Wall bounds**: X range is approximately -6.5 to +6.5 (walls at -7.25 and +7.25, minus player radius).

## ZED SDK Components Already in Project

The project already has ZED SDK installed (visible in component list):
- ZEDManager
- ZEDBodyTrackingManager
- ZEDSkeletonAnimator
- ZEDRenderingPlane
- ZEDMirror
- ZEDSVOManager
- And others

These are available but not yet configured in the GameScreen scene.

## Recommended Architecture

```
ZEDPersistentManager (DontDestroyOnLoad)
  ZEDManager              # Camera feed + tracking
  ZEDBodyTrackingManager  # Body detection
  BodyToPlayerMapper       # Custom script:
                           #   - Maps tracked bodies to Player1/Player2
                           #   - Converts body X to world X
                           #   - Detects jump gestures
                           #   - Captures profile photos
                           #   - Handles body loss/recovery
```
