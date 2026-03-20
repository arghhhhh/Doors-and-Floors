# Game Mechanics Reference

## Physics
- 3D Rigidbody with Freeze Z position and all rotation constraints
- CapsuleCollider: center (0, 1.375, 0), height 2.75, radius 0.75
- Ground detection: downward raycast accounting for collider offset and scale
- Players at Z = -0.3 (in front of doors for rendering)

## Player Movement
- Pelvis X mapped: physical (-1.5m to +1.5m) → game (-6.5 to +6.5)
- Velocity-based (not direct position) to respect collisions
- `trackingXOffset` accumulates teleport deltas to prevent snap-back after portal
- Model rotates to face movement direction

## Animation
- Hazmat Man (Meshy AI): Generic rig, applyRootMotion=false
- States: Idle → Walk (Speed > 0.1) → Run (Speed > 0.6), JumpUp (VelocityY > 0.5), JumpDown
- Controller: Assets/Animation/HazmatManController.controller

## Portal Teleportation Sequence
1. Shrink toward entry door's live position (tracks moving conveyor)
2. Pause at zero scale (0.05s)
3. Teleport + accumulate trackingXOffset
4. Grow back up at destination, adjust Y for feet alignment
5. Abort if resetGeneration changes mid-animation

## Conveyor Belt Wrapping
- Ghost clone: visual-only copy (mesh + material, no collider) at opposite edge
- Accounts for door rotation (swivel) when computing effective X width
- Door wraps when center fully crosses boundary

## Door Generation (DoorPairGenerator)
- Procedural with guaranteed winnability
- Critical path: belt_0 → belt_2 → belt_4 → last belt (top floor)
- Color-paired doors with randomized spiral shader parameters
- Each floor gets a ConveyorBelt with multiple PortalDoors

## Win Condition
- First player to land on top floor (Floor_6) triggers WinTrigger
- FinishLine ribbon slices with physics-driven halves
- GameManager transitions to Won state, timer stops
- High scores persisted as JSON with base64-encoded profile photos (top 10)

## Restart
- In-place reset without scene reload
- GameManager resets state, DoorPairGenerator regenerates, players respawn
