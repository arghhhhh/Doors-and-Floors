# ZedGames Unity Project

## What This Is
A 2-player racing game where players compete to climb from the bottom floor to the top floor of a building by jumping into color-coded portal doors on conveyor belts. Uses ZED 2i stereo camera body tracking for player control, with keyboard fallback for testing.

## Tech Stack
- Unity 2022.3 (URP)
- ZED SDK via custom UPM fork (https://github.com/arghhhhh/zed-unity)
- App UI (UI Toolkit) with NES pixel theme (PressStart2P font)
- C# with Animator-driven character animations (Generic rig Hazmat Man)

## Game Flow
StartScreen (lobby with gesture-based player registration) → GameScreen (7-floor race) → Win → Restart (in-place reset, no scene reload)

## Key Architecture
- **7 floors** (Floor_0 to Floor_6), each with a ConveyorBelt carrying PortalDoors
- **Portal pairs**: color-matched doors that teleport players up floors. Procedurally generated with guaranteed critical path (belt_0 → belt_2 → belt_4 → top)
- **Conveyor wrapping**: ghost clone system for seamless visual looping at belt edges
- **Movement**: pelvis X position mapped from physical space (±1.5m) to game space (±6.5), velocity-based to respect Rigidbody physics
- **Jump**: field goal gesture (both wrists above nose)
- **Teleportation**: shrink→pause→teleport→grow sequence tracking moving conveyor doors
- **Finish line**: ribbon slicing effect with physics-driven halves

## Scenes
- `StartScreen.unity` — Lobby with ZED camera, video tutorial quad, 5-state registration flow
- `GameScreen.unity` — Main gameplay: 7 floors, 2 walls, 2 players, FinishLine, WinZone

## Unity Automation
For any Unity work, use the global `unity` agent. It handles scene inspection, GameObject editing, C# code, prefabs, assets, input system, testing, and UI automation.

## Documentation
Detailed docs live in `docs/` — update them when making significant changes:
- `docs/ARCHITECTURE.md` — Full technical architecture
- `docs/SCRIPTS-API.md` — Complete API reference for all scripts
- `docs/SCENE-SETUP.md` — Scene hierarchies and configuration
- `docs/APP-UI-MIGRATION-PLAN.md` — UI migration history (reference only)
- `docs/CUSTOM-PACKAGES-SETUP.md` — Package installation notes

## Build & Test
- Open in Unity 2022.3
- Keyboard fallback: Player1 (A/D move, W jump), Player2 (Left/Right move, Up jump)
- ZED camera must be connected for body tracking mode
