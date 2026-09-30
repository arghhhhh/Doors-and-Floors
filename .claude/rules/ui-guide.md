---
paths:
  - "Assets/UI/**"
  - "Assets/Scripts/UIManager.cs"
  - "Assets/Scripts/ZED/StartScreenManager.cs"
---
# UI System Guide

This rule loads when working on UI files or UI-related scripts.

## Stack
- App UI (com.unity.dt.app-ui 2.2.4) built on UI Toolkit
- NES pixel theme using PressStart2P-Regular.ttf (Google Fonts, OFL license)
- PPU color palette with zero border-radius for authentic NES look

## File Layout
```
Assets/UI/
  Fonts/PressStart2P-Regular.ttf
  Settings/GamePanelSettings.asset    (1920x1080 reference, scale with screen)
  Styles/
    nes-theme.uss                     (global NES palette, font, borders)
    start-screen.uss                  (StartScreen layout)
    game-screen.uss                   (timer, win panel, score list)
  UXML/
    StartScreen.uxml                  (title, status bars, countdown, prompt)
    GameScreen.uxml                   (timer overlay, win panel with photos/scores)
    HighScoreEntry.uxml               (template cloned per score row)
```

## Patterns
- Elements queried via `Q<T>("name")` in C# scripts
- Blinking effects use `schedule.Execute().Every(500)` toggling opacity
- Root panel uses `picking-mode: ignore` for gameplay click passthrough
- PanelSettings shared between scenes at 1920x1080 reference resolution

## Key Scripts
- `UIManager.cs` — GameScreen UI: timer display, win panel, high score list rendering
- `StartScreenManager.cs` — StartScreen UI: status messages, countdown, player prompts

## App UI Skills
The App UI skills are installed globally (claude-skills). Invoke them (app-ui, app-ui-mvvm, app-ui-theming, app-ui-navigation, app-ui-redux) before writing UI code.
