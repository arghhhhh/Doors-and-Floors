# App UI Migration Plan — NES Theme

## Context

The game currently uses legacy Canvas + UnityEngine.UI for all UI (Text, RawImage, etc.). We're porting to App UI (com.unity.dt.app-ui v2.2.0-pre.6) which uses UXML layouts + USS styling — essentially HTML/CSS for Unity. This makes UI agent-driven development practical (declarative layouts, CSS-like theming) and enables a classic NES visual theme via USS custom properties.

Unity 2022.3 does NOT support source-generated data binding (`[ObservableProperty]`), so we'll use the direct element query pattern: `Q<T>("name")` to find elements and set `.text` directly — same flow as current code, just different element types.

---

## File Structure

```
Assets/UI/
  Fonts/
    PressStart2P-Regular.ttf         # NES pixel font (Google Fonts, OFL license)
  Settings/
    GamePanelSettings.asset          # Shared PanelSettings (1920x1080, scale with screen)
  Styles/
    nes-theme.uss                    # NES color palette, pixel font, zero border-radius, chunky borders
    start-screen.uss                 # StartScreen layout positioning
    game-screen.uss                  # GameScreen layout (timer, win panel, score list)
  UXML/
    StartScreen.uxml                 # StartScreen layout (title, status bars, countdown)
    GameScreen.uxml                  # GameScreen layout (timer + win panel overlay)
    HighScoreEntry.uxml              # Template cloned per score row
```

---

## NES Theme (nes-theme.uss)

Overrides App UI CSS variables at `:root`:

- **Colors**: NES PPU palette (`--nes-black: #0C0C0C`, `--nes-blue: #0058F8`, `--nes-red: #F83800`, `--nes-gold: #F8B800`, `--nes-white: #FCFCFC`, etc.)
- **Backgrounds**: Map `--appui-backgrounds-*` → NES black/dark blue
- **Foregrounds**: Map `--appui-foregrounds-*` → NES white/gray
- **Accent/Destructive/Positive**: NES blue/red/green
- **Font**: Override `--appui-font-weights-*` → PressStart2P
- **Borders**: All `--appui-border-radius-*` → 0px (pixel-perfect). Border width → 3-4px (chunky)
- **Utility classes**: `.nes-panel` (white border box), `.nes-text-gold/red/green/blue`, `.nes-photo` (pixel border on images), `.nes-text-center`
- **Blink**: Done in C# via `schedule.Execute().Every(500)` toggling opacity (USS has no @keyframes in 2022.3)

---

## UXML Layouts

### StartScreen.uxml
```
Panel (dark theme)
├── title-text          Heading XXL, "DOORS & FLOORS", gold
├── prompt-text         Text L, ">> PRESS START <<", blinking
├── countdown-text      Text XL, red, hidden by default
└── status-bar          Row at bottom
    ├── p1-status       Text M, green, left
    └── p2-status       Text M, blue, right
```

### GameScreen.uxml
```
Panel (dark theme, transparent background, picking-mode: ignore)
├── timer-text          Heading L, top-center, white
└── win-panel           NES panel, centered, hidden by default
    ├── winner-text     Heading XL, gold
    ├── winner-photo    VisualElement 128x128, gold border, background-image set via code
    ├── final-time-text Heading M
    ├── high-score-label Text L, red, blinking
    ├── high-score-list  Container (children cloned from HighScoreEntry.uxml)
    └── instructions-text Text S, gray
```

### HighScoreEntry.uxml
```
score-entry (row)
├── score-photo         VisualElement 32x32, background-image set via code
└── score-text          Text S, white
```

---

## Script Changes

### StartScreenManager.cs
- Replace `using UnityEngine.UI` → `using UnityEngine.UIElements` + `using Unity.AppUI.UI`
- Remove `public Text titleText, p1StatusText, p2StatusText, countdownText` fields
- In `Start()`: get `UIDocument` component, query elements via `root.Q<Heading>("title-text")`, etc.
- `UpdateUI()`: same logic, just setting `.text` on App UI elements instead of legacy Text
- Countdown visibility: `countdownEl.style.display = show ? DisplayStyle.Flex : DisplayStyle.None`
- Add blink helper: `schedule.Execute(() => { visible = !visible; el.style.opacity = visible ? 1 : 0; }).Every(500)`
- Remove auto-find-by-name fallback code (UXML names are stable)

### UIManager.cs
- Replace `using UnityEngine.UI` → `using UnityEngine.UIElements` + `using Unity.AppUI.UI`
- Remove all `public Text`, `public RawImage`, `public GameObject`, `public Transform` fields
- Add `[SerializeField] VisualTreeAsset highScoreEntryTemplate` for cloning score rows
- In `Awake()`: query all elements via `Q<T>("name")`
- `UpdateTimer()`: same logic, set `timerEl.text`
- `ShowWinPanel()`: show via `style.display = Flex`, set winner photo via `style.backgroundImage = new StyleBackground(texture)`
- `BuildHighScoreList()`: clone `highScoreEntryTemplate` per entry, set photo via `backgroundImage`, set text. No more `GameObject.Instantiate` or `RawImage`
- `HideWinPanel()`: `style.display = None`, `highScoreListEl.Clear()`

### GameManager.cs
- No changes needed (UIManager public API is unchanged)

### PlayerController.cs
- No changes needed

---

## Scene Setup

### Both scenes
1. Create `Assets/UI/Settings/GamePanelSettings.asset` — PanelSettings with 1920x1080 reference, scale with screen size, theme set to App UI dark TSS

### StartScreen
1. Delete the `Canvas` GameObject and all children
2. Delete `EventSystem` if present (UI Toolkit doesn't need it)
3. Add `UIDocument` component to `StartScreenManager` GO
4. Set panelSettings → `GamePanelSettings.asset`, sourceAsset → `StartScreen.uxml`

### GameScreen
1. Delete the `Canvas` GameObject and all children
2. Delete `EventSystem` GO
3. Add `UIDocument` component alongside `UIManager` (or on a new GO)
4. Set panelSettings → `GamePanelSettings.asset`, sourceAsset → `GameScreen.uxml`
5. Assign `highScoreEntryTemplate` → `HighScoreEntry.uxml`
6. Ensure root has `picking-mode: ignore` so gameplay clicks pass through

---

## Implementation Order

1. **Font import** — Download PressStart2P, import to `Assets/UI/Fonts/`
2. **PanelSettings** — Create shared `GamePanelSettings.asset`
3. **nes-theme.uss** — Core theme (colors, font, borders). Foundation for everything.
4. **StartScreen.uxml + start-screen.uss** — Simplest screen first
5. **StartScreenManager.cs** — Migrate to UI Toolkit queries. Test lobby flow end-to-end.
6. **GameScreen.uxml + game-screen.uss + HighScoreEntry.uxml** — More complex layout
7. **UIManager.cs** — Migrate timer, win panel, high score list. Most complex change.
8. **Scene cleanup** — Delete old Canvas GOs, add UIDocuments, wire references
9. **Polish** — Blink animations, verify camera preview still works, verify input passthrough

---

## What Gets Deleted

- **StartScreen**: Canvas GO + children (TitleText, P1StatusText, P2StatusText, CountdownText)
- **GameScreen**: Canvas GO + children (TimerText, WinPanel tree), EventSystem GO
- **Code**: All `using UnityEngine.UI` in StartScreenManager.cs and UIManager.cs, all legacy Text/RawImage/Transform serialized fields, auto-find-by-name code, programmatic GameObject creation in BuildHighScoreList

## What Stays Unchanged

- ZED camera preview (Camera component, not UI)
- GameManager.cs, PlayerController.cs, all ZED/* scripts except StartScreenManager
- HighScoreManager.cs (data layer, no UI)
- Game scene hierarchy (floors, walls, players)

---

## Verification

1. StartScreen: title displays in pixel font, gold color, NES black background
2. Lobby flow: P1/P2 status text updates correctly, countdown appears/hides
3. Blinking text works on prompt and high score label
4. GameScreen: timer renders top-center during gameplay
5. Win panel: appears on win with winner text, photo, time, high scores
6. High score list: photo thumbnails + times render correctly
7. Input passthrough: clicking game area during play doesn't get eaten by UI
8. Camera preview: ZED overlay still renders in top-left corner
9. Keyboard fallback: UI works the same without ZED connected
