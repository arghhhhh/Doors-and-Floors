<img width="1338" height="162" alt="doors_and_floors_title2026-10-01 123041" src="https://github.com/user-attachments/assets/5494f18f-74c8-4eed-8aab-956218b24a3c" />

A full-body racing game for one or two players, built for the 2026 [FilmGate Interactive Media Festival](https://www.filmgate.miami/filmgate-interactive-media-festival). I challenged myself at the onset of this project to use AI services to build/generate every single part of this game from end-to-end. Aside from requiring some curation and a few manual image/video edits, the endeavor was successful.

Players race from the bottom floor of a building to the top by jumping through portal doors that ride on conveyor belts. There are no controllers: a ZED 2i stereo camera tracks each player's body, so you run left and right by actually moving and jump by actually jumping.

## Gameplay

https://github.com/user-attachments/assets/b142a8a5-af48-40ba-a1b3-932d8fc37937

## How to Play

1. **Step in front of the camera** and raise both arms above your head (a "field goal") for a second to join as Player 1.
2. **A second player** has 10 seconds to step in and do the same. If nobody joins, you race the **CPU** instead.
3. **Move left and right** by walking. Your position in front of the camera maps to your character's position on the floor.
4. **Jump** into a door to teleport. Doors come in color-matched pairs: jumping into one sends you out of its twin.
5. **Watch out:** doors work both ways, so some lead *down*. The first player to land on the top floor wins.
6. **Raise your arms** on the win screen to play again.

## Features

- **Body tracking:** ZED 2i skeleton tracking for movement, jumping and gesture-based menus, including recovery when a player briefly leaves the frame.
- **CPU opponent:** solo players race an AI that plans its route through the door pairs. Easy, Normal and Hard presets are tuned against real human run times (15–30s).
- **Retro look:** a custom URP pass renders the game at low resolution with posterized colors and outlines, using an NES palette and pixel font.
- **Pixel-style effects:** VFX Graph jump dust, portal entry and exit bursts tinted to each door's color, and win confetti.
- **Procedural levels:** every round generates a new door layout with a guaranteed path to the top.
- **High scores:** the top 10 times are saved with a face photo captured in the lobby.
- **Demo mode:** CPU vs CPU rounds that loop on their own, for an attract screen or capturing footage.

## Requirements

- Windows with an NVIDIA GPU (required by the ZED SDK)
- [ZED SDK 5.5](https://www.stereolabs.com/developers/release) and a ZED 2i camera
- Unity **2022.3.62f2** (URP)

The ZED Unity package is pinned to `stereolabs/zed-unity#v5.5.0` in `Packages/manifest.json`. **That tag must match your installed ZED SDK version**, or the plugin fails to load and the editor crashes on open. Bump the tag whenever you upgrade the SDK.

## Getting Started

1. Clone the repo and open the folder in Unity 2022.3.62f2.
2. Connect the ZED camera, open `Assets/Scenes/StartScreen.unity` and press **Play**.

Players need roughly 3 m of clear width in front of the camera: the play area maps ±1.5 m of physical movement onto the building's full width.

### Playing without a camera

In the Editor and development builds you can skip the camera entirely:

| Where | Key | Action |
|---|---|---|
| Lobby | `K` | Start a 2-player keyboard game |
| Lobby | `V` | Start a keyboard game against the CPU |
| Game | `A` / `D`, `W` | Player 1 move, jump |
| Game | `←` / `→`, `↑` | Player 2 move, jump |
| Win screen | `Enter` / `Esc` | Play again / back to menu |

You can also open `Assets/Scenes/GameScreen.unity` directly to play on the keyboard.

### Demo mode

1. Open `GameScreen.unity` and select **GameScreenManager**.
2. Tick **Cpu Vs Cpu Demo** and press **Play**.
3. Change **Demo Time Scale** (0.25–4×) while it runs to slow down or speed up the action.

Untick the box before saving the scene, or the real game will start in demo mode.

## Project Structure

```
Assets/
  Scenes/      StartScreen (lobby) and GameScreen (the race)
  Scripts/     Gameplay, CPU opponent, UI, VFX
    ZED/       Body tracking, lobby flow, per-player input
  VFX/         VFX Graph effects
  PixelPass/   Low-resolution pixel renderer feature
  UI/          App UI (UI Toolkit) layouts and NES theme
Docs/
  ARCHITECTURE.md   How the systems fit together
  SCENE-SETUP.md    Scene hierarchies and configuration
```

## Credits
- Built for the [FilmGate Interactive Media Festival](https://www.filmgate.miami/filmgate-interactive-media-festival)
- All code written by [Claude](https://claude.com/claude-code) (Anthropic)
- [unity-cli](https://github.com/akiojin/unity-cli) by akiojin, which the AI agents used to work inside the Unity Editor on this project
- Body tracking by the [Stereolabs ZED SDK](https://www.stereolabs.com/)
- Hazmat Man character model generated with [MidJourney](https://www.midjourney.com/) and [Meshy](https://www.meshy.ai/)
- Sound FX generated by [ElevenLabs](https://elevenlabs.io)
- Hazmat Man character video animation generated by [Grok Imagine](https://grok.com/imagine/)
- [Press Start 2P](https://fonts.google.com/specimen/Press+Start+2P) font by CodeMan38 (SIL Open Font License)
- UI built with Unity [App UI](https://docs.unity3d.com/Packages/com.unity.dt.app-ui@latest)
- Pixel art shader from [Madalaski/PixelatedAdvancedTutorial](https://github.com/Madalaski/PixelatedAdvancedTutorial)
