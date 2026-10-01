# Doors & Floors (ZedGames Unity Project)

## What This Is
Built for the Filmgate Interactive 2026 festival. GitHub repo: `arghhhhh/Doors-and-Floors` (the local folder and Unity product name are still `ZedGames`). A 2-player racing game (or 1 player vs a CPU when nobody else joins) where players compete to climb from the bottom floor to the top floor of a building by jumping into color-coded portal doors on conveyor belts. Uses ZED 2i stereo camera body tracking for player control, with keyboard fallback for testing.

## Tech Stack
- Unity 2022.3 (URP)
- ZED SDK 5.5 via upstream UPM package `stereolabs/zed-unity#v5.5.0`. The package tag must match the installed ZED SDK version, or `sl_unitywrapper.dll` fails to load and the editor crashes on open. Bump the tag in `Packages/manifest.json` whenever the SDK is upgraded.
- App UI 2.2.4 (UI Toolkit, from the Unity registry) with NES pixel theme (PressStart2P font)
- VFX Graph 14.0.12 (`Assets/VFX/`): effects must use Opaque outputs on a pixel-pass layer, or they don't render (see ARCHITECTURE "Visual Effects")
- C# with Animator-driven character animations (Generic rig Hazmat Man)

## Game Flow
StartScreen (lobby with gesture-based player registration) → GameScreen (7-floor race) → Win → Restart (in-place reset, no scene reload)

## Key Architecture
- **7 floors** (Floor_0 to Floor_6), each with a ConveyorBelt carrying PortalDoors
- **Portal pairs**: color-matched doors that teleport players up floors. Procedurally generated with guaranteed critical path (belt_0 → belt_2 → belt_4 → top)
- **Conveyor wrapping**: ghost clone system for seamless visual looping at belt edges
- **Movement**: pelvis X position mapped from physical space (±1.5m) to game space (±6.5), velocity-based to respect Rigidbody physics
- **Jump**: physical jump by default (`BodyTrackingInput.jumpMethod`); field goal gesture (both wrists above neck) is the alternative
- **CPU opponent**: `CpuPlayerInput` on Player2 when only P1 joins; tune difficulty on its Inspector
- **Teleportation**: shrink→pause→teleport→grow sequence tracking moving conveyor doors
- **Finish line**: ribbon slicing effect with physics-driven halves

## Scenes
- `StartScreen.unity` — Lobby with ZED camera, video tutorial quad, 5-state registration flow
- `GameScreen.unity` — Main gameplay: 7 floors, 2 walls, 2 players, FinishLine, WinZone

## Unity Automation
For any Unity work, use the global `unity` agent. It handles scene inspection, GameObject editing, C# code, prefabs, assets, input system, testing, and UI automation.

## Documentation
Detailed docs live in `Docs/` — update them when making significant changes:
- `Docs/ARCHITECTURE.md` — Full technical architecture
- `Docs/SCENE-SETUP.md` — Scene hierarchies and configuration

## Build & Test
- Open in Unity 2022.3
- Keyboard fallback: Player1 (A/D move, W jump), Player2 (Left/Right move, Up jump)
- Lobby without the ZED (Editor/dev builds): K = keyboard 2P, V = keyboard vs CPU
- ZED camera must be connected for body tracking mode
