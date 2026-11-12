# Engine Notes + File Plan: Main Menu (PURGATOR)

> **Status**: Draft, planning only (Phase 3). No code has been written.
> **Author**: ui-programmer (also standing in for unity-ui-specialist; team.size: individual)
> **Date**: 2026-11-12
> **Inputs**: `design/ux/main-menu.md` (UX spec), `design/ux/interaction-patterns.md` (P1-P8), `production/ui/main-menu/visual-design.md` (**Direction A "Kan ve Kemik" only**), `docs/engine-reference/unity/current-best-practices.md` (6.4 -> 6.5), `docs/engine-reference/unity/deprecated-apis.md` (6.3 LTS -> 6.5), `docs/engine-reference/unity/breaking-changes.md` (6.4 -> 6.5).
> **Engine**: Unity 6000.5.5f1, URP 17.5.0, `com.unity.ugui` 2.5.0 (includes TextMeshPro), `com.unity.inputsystem` 1.19.0. No new packages.
> **Project rules (CLAUDE.md) that override everything else**: New Input System only; never `FindObjectOfType`; zero GC in Update/LateUpdate; `[SerializeField] private` with read-only getters only; extend through interfaces; everything as prefabs; data-driven values; POCO logic split from MonoBehaviour views and EditMode-testable; XML docs on public API; `GetInstanceID` is a compile error (use `EntityId`).

---

## 1. Engine Notes

### 1.1 UGUI vs UI Toolkit: **UGUI + TextMeshPro**

Decision: UGUI (Canvas) with TMP.

Reasons:
1. **Particles in the art layer.** Visual spec 0.3/4.5 puts ParticleSystem embers and smoke *between* art images and the scrim. UGUI's Screen Space - Camera canvas plus `overrideSorting` sub-canvases can interleave UI images with `ParticleSystemRenderer` sorting orders. UI Toolkit panels draw as one layer and cannot interleave world particles without a RenderTexture.
2. **Gamepad/keyboard navigation with explicit wrap.** UGUI `Selectable` explicit navigation gives P1 wrap-around and per-context focus traps directly. `InputSystemUIInputModule` is the documented bridge to the project's `InputSystem_Actions` UI map.
3. **Pixel-art ornaments.** Point-filtered 9-slice sprites, integer-scale ornaments and TMP SDF text are the visual spec's stated assumption (header: "UGUI + TextMeshPro").
4. **Project stack.** The engine reference calls UGUI "this project's HUD/menu stack" (6.4 -> 6.5 practices). UI Toolkit in 6.5 also moves from UI Document to the new Panel Renderer component, which adds churn we gain nothing from here.
5. **Builder pattern.** The existing editor builders (`ArafSceneBuilder`) create GameObjects and prefabs from code. UGUI hierarchies fit that pattern. UI Toolkit would add UXML/USS assets that the builder would have to generate as text.

Trade-off: UI Toolkit has better batching for large or data-heavy UIs. For a 3-item menu plus a 9-row panel this does not matter.

### 1.2 Canvas setup

| Canvas | Render mode | Sorting | Raycaster | Contents |
|---|---|---|---|---|
| `ArtCanvas` (in `MainMenuArt.prefab`) | **Screen Space - Camera**, camera = `MenuCamera`, plane distance 10 | sortingOrder 0 | **none** (art is never interactive) | ArtSky, ArtTowerFar, ArtFog, ArtTowerNear, ArtTorchGlow |
| `Embers` ParticleSystem (child of ArtCanvas) | renderer sortingOrder 1 | | | embers, maxParticles 64, prewarm |
| `Smoke` ParticleSystem (child of ArtCanvas) | renderer sortingOrder 2 | | | smoke, maxParticles 8, prewarm |
| `ScrimLayer` (nested Canvas, overrideSorting) | inherits Camera | sortingOrder 3 | none | LeftScrim |
| `MenuCanvas` (in `MainMenuCanvas.prefab`) | **Screen Space - Overlay** | sortingOrder 10 | GraphicRaycaster | Safe area, Title, MenuList, SettingsPanel, VersionTag, LoadError |
| `DialogLayer` (nested, overrideSorting) | Overlay | 20 | GraphicRaycaster | DimOverlay + QuitConfirm + RevertPrompt |
| `FadeLayer` (nested, overrideSorting) | Overlay | 100 | GraphicRaycaster | FadeOverlay |

**Particle sorting decision (visual spec 4.5 flag): accepted.** Art and particles go on a Screen Space - Camera canvas. All text and interactive UI go on an Overlay canvas, which always draws after every camera, so it can never be covered by a particle or by art.
- `MenuCamera`: URP Base camera, **orthographic**, Clear = Solid Color `bg #0C0A0A`, culling mask = a dedicated `MenuArt` layer only (ArtCanvas and particles on that layer), no post-processing, **carries the scene's `AudioListener`**.
- Particle systems: `scalingMode = Hierarchy`, so sizes and speeds are in canvas units (px at 1080p) and scale with the CanvasScaler. Simulation space Local. Material: URP `Particles/Unlit` (Additive for embers, Alpha for smoke). Emitter shapes sit in the right 55% (`RectTransform` anchored right), so nothing spawns in the 0-48% band (visual 0.2). They are created by the builder, never instantiated at runtime.
- Both canvases: `CanvasScaler` = Scale With Screen Size, 1920x1080, **Match = 1 (height)** (visual 0.1). Safe area = a child RectTransform anchored 0.05-0.95 on both axes.
- Hit targets: menu rows use a full-row transparent Graphic as the raycast target, at least 56 px tall. **6.5 offers a "Raycast Receiver" component** (engine-ref 6.4 -> 6.5, UI) that does this more cheaply. The reference does not give its exact class name or namespace, so it is **UNVERIFIED**. The builder uses `Image` with alpha 0 now; swapping to Raycast Receiver is a follow-up once the type is confirmed in the 6.5 manual.

### 1.3 EventSystem + InputSystemUIInputModule, and how P1/P6 map onto them

`MenuEventSystem.prefab`: `EventSystem` + `InputSystemUIInputModule`:
- `actionsAsset` = `Assets/InputSystem_Actions.inputactions` (the project-wide asset). Its `UI` map already has Navigate (arrows, WASD, d-pad, left stick), Submit, Cancel (Esc, gamepad B/buttonEast), Point, Click, RightClick and ScrollWheel. **This shared asset is not modified.**
- `deselectOnBackgroundClick = false`. **Gotcha:** the default `true` clears the selection when the player clicks empty space, which leaves no focused item. That breaks P1 "focus always visible".
- `moveRepeatDelay` 0.4 s and `moveRepeatRate` 0.1 s give hold-repeat on Navigate. `HoldAccelerator` builds the P3 acceleration on top of that repeat.
- Never add `StandaloneInputModule`. It throws when Active Input Handling is "Input System Package".

| Requirement | UGUI mapping |
|---|---|
| P1 Up/Down/W/S/d-pad/stick, wrap | Every Selectable uses `Navigation.Mode.Explicit`. The builder wires selectOnUp/Down with wrap (last -> first) per context. Left/Right are unset on buttons (no-op) and handled by Slider/CycleRow on rows. |
| P1 Tab / Shift+Tab | The UI map has no Tab action. `MenuInputRouter` polls `Keyboard.current.tabKey.wasPressedThisFrame` (same style as `PlayerLook`/`RunHUD`, no allocation) and moves to the selected item's `selectOnDown` / `selectOnUp`. Tab order then equals the Up/Down order by construction. |
| P1 focus trap (Settings, dialogs) | Explicit nav links never leave the context. Inactive contexts get `CanvasGroup.interactable = false` and `blocksRaycasts = false`. `Selectable.IsInteractable()` honours the CanvasGroup, so the mouse cannot reach them either. The DimOverlay is a raycast target (P5). |
| P6 Esc / B | `MenuInputRouter` subscribes to `UI/Cancel` `performed` (InputActionReference) -> `MainMenuController.Back()`. A central router, not `ICancelHandler` on rows, so Back works whatever is selected and never fires twice. |
| P6 right-click | `UI/RightClick` `performed` -> the same `Back()`. The state machine makes root Back a no-op. |
| Input lock (first-load, loading, press lock) | The state machine reports `InputLocked`. The controller sets `EventSystem.sendNavigationEvents = false` and the FadeOverlay `blocksRaycasts = true`; the router ignores Back/Tab. **Gotcha:** a raycast blocker alone does not stop keyboard/gamepad Submit, so `sendNavigationEvents` must also be off. |
| Default focus | Each context has a default Selectable. `MenuInputRouter.Update` runs a focus guard: if `EventSystem.currentSelectedGameObject` is null or outside the active context, it re-selects the context default. This is a reference comparison and allocates nothing. |
| Scroll wheel on sliders | `MenuSlider` does not implement `IScrollHandler`, and the panel has no ScrollRect, so the wheel is ignored. |
| Gamepad disconnect | No code. The module keeps working with keyboard and mouse. Verify in playtest (UX 11 open item). |

### 1.4 Focus follows hover, single focus

- UGUI's built-in hover is a separate *highlighted* state, which breaks "hover = focus" (P2). Every Selectable gets **`transition = None`**, and all visuals are driven by `MenuFocusVisual` from `ISelectHandler`/`IDeselectHandler` only.
- `MenuFocusVisual` implements `IPointerEnterHandler` -> `EventSystem.current.SetSelectedGameObject(gameObject)`, guarded by "not input-locked and interactable". Hover therefore *moves the one selection*, and keyboard focus and hover can never show two different items (UX 14).
- Pointer exit does nothing: focus stays on the last item, so it is always visible.
- First load: BAŞLA is selected when the fade completes, with no click needed (UX 5.3).

### 1.5 Sigil / focus visual (Direction A "Kemik Mühür")

Per item, not one shared moving sigil: each `MenuButton` / row prefab carries its own `Sigil` Image (27x33 @1080p, 16 px left of the text) and `Underline` Image (9-slice, 6 px tall, text width via `ContentSizeFitter` on the label). `MenuFocusVisual.OnSelect` turns them on and `OnDeselect` turns them off, so the sigil "snaps" with no layout math or tween.
- Text: `text #E8DCC0` Medium -> `focus #FFF4DC` Bold. TMP `fontStyle` toggle (no material swap needed with the placeholder font).
- Scale 1.0 -> 1.06 over 80 ms (`MenuTween`, unscaled), **skipped when reduce-motion is on**. Settings rows also turn on a row band `#1E1718`.
- Pressed (80 ms): label moves 3 px down, sigil colour -> `focus`, underline full `accent`. The press lock then holds (P2).
- Slider fill swaps `text-dim` -> `accent` while the row is focused. The value always shows as % text.
- Optional 0.5 Hz sigil glow: **not implemented in this pass** (the spec marks it optional; one fewer moving element).
- Greyscale check: sigil plus underline carry focus without colour (visual 1.4, UX 14).

### 1.6 Settings application

**Settings service lives in `RetroFPS.Core`** (POCO `SettingsService` behind the static access point `GameSettings`). Gameplay code (`PlayerLook`, `CombatAudio`) only references Core, so it can read settings without depending on the menu. It also loads at `BeforeSceneLoad`, so entering Play Mode directly in `TestLevel_Gauntlet` still applies persisted settings. The UI calls `ISettingsService` request methods (`SetVolume`, `SetSensitivity`, `RequestDisplay`, `SetReduceMotion`); it never writes PlayerPrefs (UX 14).

> **Deviation from UX Sec. 9 (flagged):** the spec names events `SettingChangeRequested` / `DisplaySettingChangeRequested`. They are implemented as **request methods on an injected `ISettingsService` interface**, plus one `Changed` event on the service that listeners subscribe to. The decoupling is the same (UI -> service; service persists and notifies), it is type-safe, and the POCO is testable with fakes. `DisplaySettingConfirmed` / `DisplaySettingReverted` become `ConfirmDisplay()` / `RevertDisplay()`.

**Display**
- `WindowModeSetting { Fullscreen, Borderless, Windowed }` -> `FullScreenMode.ExclusiveFullScreen`, `FullScreenWindow`, `Windowed` (Win64 supports exclusive).
- Apply: `Screen.SetResolution(w, h, mode)`. **Gotcha:** this takes effect at the end of the frame, and `Screen.width`/`height` are stale until then. The revert target is therefore taken from the service's committed values, never read back from `Screen`.
- Resolution list: `Screen.resolutions` (allocates; read only when Settings opens) -> `ResolutionOptions` POCO: deduplicate refresh rates by WxH, drop anything below 1280x720, sort ascending. Saved value not in list -> nearest; nothing saved -> `Screen.currentResolution` (desktop), per UX 8.
- **P4 revert:** `RequestDisplay(mode, w, h)` applies live and marks the change *pending*. `RevertCountdown` (10 s, unscaled) starts. Keep -> `ConfirmDisplay()` persists. Expiry, Back or "Geri al" -> `RevertDisplay()` re-applies the committed values. A crash during the prompt leaves the old values persisted, which is safe. Every mode and resolution change triggers it (no filtering).
- **`GameResolutionManager` conflict, and the fix:** it currently forces `1920x1080 FullScreenWindow` at `BeforeSceneLoad` on every boot, which would overwrite the persisted choice. **Proposed:** it becomes the single boot entry point: `GameSettings.EnsureInitialized()` (load from PlayerPrefs) -> apply persisted display, or the first-boot default -> keep `Application.targetFrameRate = 144`. Having one `BeforeSceneLoad` method avoids undefined ordering between two such methods. First-boot default per UX 8: **Fullscreen at desktop resolution**. **This changes current boot behaviour** (today it is borderless 1920x1080; see CONCERNS). Unity also persists its own `Screenmanager*` keys; our explicit apply at BeforeSceneLoad wins over them.
- In the Editor, `Screen.SetResolution` is effectively ignored. Display rows still cycle and the revert flow is testable; the real check is in a Win64 build.

**Audio: no AudioMixer exists. Minimal approach:**
- **Master** -> `AudioListener.volume` (global static, survives scene loads, no asset needed). Works for every existing sound immediately.
- **SFX** -> a two-line change in `CombatAudio` (`Play2D`, `PlayClipAt`): `volume * masterCombatVolume * GameSettings.Current.SfxVolume`. That covers the pooled combat voices. `EncounterArea`, `WaveSpawnDirector` and `OccultSpawnTelegraph` own AudioSources that are **not** covered (CONCERN).
- **UI** -> `MenuAudioPlayer`'s AudioSource volume = `UiVolume` (master still applied by the listener).
- **Music** -> persisted and exposed. Nothing plays music today (MenuAmbience is optional/none), so it has no audible effect yet.
- An `IAudioVolumeApplier` interface sits in the service. If the user later hand-authors an AudioMixer (Master/Music/SFX/UI groups with exposed params; it cannot be created reliably from an editor script), a `MixerVolumeApplier` replaces `ListenerVolumeApplier` with no UI change. Volume -> dB conversion would then be `20*log10(max(v, 0.0001))`.

**Mouse sensitivity -> PlayerLook (minimal targeted change)**
- The slider stores a 0-1 value (default 0.5, shown as 50%).
- `SensitivityMapping.ToScale(p, minScale, maxScale)` is exponential, so **50% = scale 1.0 = today's exact feel** (`mouseSensitivity 0.10` stays as the base). Bounds are serialized on PlayerLook (`sensitivityMinScale = 0.25`, `sensitivityMaxScale = 4`), which keeps them data-driven and tunable in the Inspector.
- PlayerLook: `OnEnable` subscribes to `GameSettings.Changed` and reads the current value into a cached `_sensitivityScale`; `OnDisable` unsubscribes; `HandleLook` multiplies by the cached float. No allocation, no lookup in Update. Read on load and on change, as UX 8 requires.

**Persistence**: `PlayerPrefsSettingsStore : ISettingsStore`. Keys are exactly the UX keys (`masterVolume`, `musicVolume`, `sfxVolume`, `uiVolume`, `mouseSensitivity`, `reduceMotion`, plus `windowMode`, `resolutionWidth`, `resolutionHeight`) with a `settings.` prefix to avoid collisions. Values are clamped on load (a corrupt pref falls back to the default). `PlayerPrefs.Save()` runs on Settings close, on display Keep, and on Quit (UX 4: "flushed before quit").

**Defaults**: 100/70/100/80 (master/music/sfx/ui), sensitivity 50%, Fullscreen, desktop resolution, reduce-motion off. These live in one `GameSettingsData.Defaults` definition in Core. The menu's `MenuTheme` cannot hold them because they must exist at `BeforeSceneLoad` without scene references (UX open item "default values" is game-designer-owned; flagged).

### 1.7 Reduce motion

- `GameSettings.Current.ReduceMotion`, read at scene start, so the first-load fade is already short (UX 6).
- Effects: fades become 100 ms (`MenuTheme.reducedFadeSeconds`) and panel/dialog changes instant; no focus scale; embers `Simulate(prewarm)` then `Pause()` (a frozen, populated frame); smoke paused; fog uvRect scroll stops; torch glow fixed at 0.92. Sigil and brightness stay.
- An OS "reduce animations" preference is **not** read: no documented Unity API for the Windows setting in the engine reference (unverified), so the default is off.

### 1.8 Turkish text and localization

- **Strings are stored pre-cased** in the table (BAŞLA, AYARLAR, ÇIKIŞ, GERİ). Code never calls `ToUpper()` / `ToUpperInvariant()` on player text (invariant casing turns "i" into "I", not "İ"). If casing is ever needed: `ToUpper(CultureInfo.GetCultureInfo("tr-TR"))`. This is not used now.
- **Turkish percent format is "%50" (sign first).** `settings.percent_format` = `%{0}`. The 21 possible strings (0..100 step 5) are built once when the panel binds, so updating a value allocates nothing.
- Countdown: 11 strings (10..0) are pre-built from `display.revert_countdown_format`. The text changes once per second, and at <= 3 s it switches to `danger` colour (the number carries the information).
- `StringTable` (ScriptableObject): `[Serializable]` key/value list plus a Dictionary built in `OnEnable`. A missing key returns the key itself and logs one warning, so text is never blank (UX 8).
- Version tag: `menu.version_format` + `Application.version`, formatted once at scene start.
- TMP auto-size (shrink-to-fit) with `fontSizeMin` held at **24 px** for every role, so the 24 px floor is guaranteed (UX 12). The title uses a higher minimum.

### 1.9 TextMeshPro: Essential Resources

- **Confirmed:** `com.unity.ugui` 2.5.0 ships TMP, but the project has **no TMP Essential Resources imported** (no `TMP Settings` asset or `LiberationSans SDF` under `Assets/`). The package file exists at `Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage`.
- Without them, TMP components have no default font, material or shader settings, and the Editor pops the TMP Importer window on first use.
- **Builder handling:** step 0 checks `AssetDatabase.FindAssets("t:TMP_Settings")`. If it is missing, the builder calls `AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false)` (non-interactive). It then **stops** with a log asking the user to run the menu item again: the import completes after an asset refresh/domain reload, so the build cannot continue in the same call. The second run finds `LiberationSans SDF` (its dynamic fallback covers Ş Ğ İ ı, per visual 4.1) and assigns it through `MenuTheme`.
- Why not avoid the import: generating a dynamic `TMP_FontAsset` from an OS font at edit time still needs TMP Settings for default shaders, and its atlas and material sub-assets would have to be saved manually. Importing the official package is one line and is the documented path. Importing is not adding a new package.
- Later, the OFL fonts (Grenze Gotisch, Alegreya Sans SC) are swapped in through `MenuTheme` after the glyph test string passes (visual 1.3/4.7).

### 1.10 Scene flow, quit, lifecycle

- `MainMenuController.Awake`: `Cursor.lockState = None`, `Cursor.visible = true`, `Time.timeScale = 1` (UX 4/14).
- BAŞLA -> state machine `Loading` -> `MenuEvents.StartRequested("TestLevel_Gauntlet")` fires once. `MenuLifecycle` (receiver) checks `Application.CanStreamedLevelBeLoaded`, starts `SceneManager.LoadSceneAsync` with `allowSceneActivation = false` (it preloads during the fade), and activates on `MenuEvents.FadeOutComplete`. If the scene is missing or invalid -> `MenuEvents.LoadFailed` -> controller shows `menu.load_error` in `danger`, fades back in, unlocks, focuses BAŞLA (UX 6 Error). On activation, `PlayerLook.Awake` locks the cursor as it does today.
- EVET -> `MenuEvents.QuitConfirmed` -> `MenuLifecycle`: `GameSettings.Save()` -> `#if UNITY_EDITOR EditorApplication.isPlaying = false #else Application.Quit()`. UI code never calls `Application.Quit` (UX 14).
- UI audio: `MenuEvents.UiAudio(UiAudioEventType)` -> `MenuAudioPlayer` (receiver) -> `AudioSource.PlayOneShot(clip)` with a 50 ms per-event throttle (P8). UI code never touches AudioSource.
- Static events reset at `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`, following the existing `CombatAudio.ResetInstance` pattern. This is safe with domain reload disabled. 6.5 recommends `OnEnteringPlayModeAttribute`, but the old attribute is not obsolete (deprecated-apis 6.5), so we stay consistent with the codebase.

### 1.11 Gotchas checklist

1. `deselectOnBackgroundClick` must be false (1.3).
2. `Selectable.transition = None` everywhere, or UGUI's highlight state creates a second "focus" look on hover (1.4).
3. A raycast blocker does not block keyboard/gamepad: also toggle `EventSystem.sendNavigationEvents` (1.3).
4. `Screen.SetResolution` is applied at end of frame; never read `Screen.width` back for revert (1.6).
5. A ParticleSystem inside a canvas needs `scalingMode = Hierarchy`, or particles are microscopic in canvas space (1.2).
6. `MenuCamera` must carry the only `AudioListener` in MainMenu, or UI SFX are silent and Unity warns.
7. `SliderEditor` is a `[CustomEditor(typeof(Slider), true)]`, so `MenuSlider`'s extra serialized fields are hidden in the Inspector. Keep them few, and the builder sets them through `SerializedObject`.
8. Use `Slider.wholeNumbers` with range 0..20 so one Navigate step = 5% exactly (no float drift; 20 -> 100%).
9. Never `ToUpperInvariant` Turkish text (1.8).
10. `EntityId` not `GetInstanceID` anywhere (compile error in 6.5). `GetComponent` is not allowed in Update; cache references in Awake/builder.
11. 6.5 removed `go.audio` / `.camera` shortcuts: use `GetComponent<T>()` in the builder.
12. `com.unity.ugui` stays an explicit manifest dependency (it already is, 2.5.0).
13. The new asmdef must reference `UnityEngine.UI` and `Unity.TextMeshPro` by name. The Gameplay asmdef does not have them, so menu code goes in its own asmdef (2.2) and Gameplay is untouched.

---

## 2. File Plan

Paths are relative to `Graduation Project/`. All new code is `namespace RetroFPS.*` with XML doc comments on the public API.

### 2.1 POCO logic: Core settings (assembly `RetroFPS.Core`, namespace `RetroFPS.Settings`)

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/Core/Settings/GameSettingsData.cs` | `[Serializable]` struct of all setting values, `WindowModeSetting` enum, `Defaults`, `Clamp()`. |
| `Assets/_Project/Scripts/Core/Settings/SettingsContracts.cs` | Interfaces `ISettingsService`, `ISettingsStore`, `IDisplayApplier`, `IAudioVolumeApplier`. |
| `Assets/_Project/Scripts/Core/Settings/SettingsService.cs` | POCO: load/clamp, apply + persist per change, display pending/confirm/revert, `Changed` event. Dependencies injected. |
| `Assets/_Project/Scripts/Core/Settings/ResolutionOptions.cs` | POCO: dedupe/filter (>= 1280x720)/sort a resolution list, nearest-match and fallback index. |
| `Assets/_Project/Scripts/Core/Settings/SensitivityMapping.cs` | POCO: exponential 0-1 -> look-scale mapping (0.5 = 1.0). |
| `Assets/_Project/Scripts/Core/Settings/UnitySettingsAdapters.cs` | `PlayerPrefsSettingsStore`, `ScreenDisplayApplier` (`Screen.SetResolution`), `ListenerVolumeApplier` (`AudioListener.volume`). Thin engine adapters. |
| `Assets/_Project/Scripts/Core/Settings/GameSettings.cs` | Static access point: `EnsureInitialized()`, `Service`, `Current`, `Changed`, `Save()`, `SetServiceForTests()`, SubsystemRegistration reset. |

### 2.2 Menu assembly (new asmdef)

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/UI/Menu/RetroFPS.Menu.asmdef` | New asmdef `RetroFPS.Menu` (rootNamespace `RetroFPS.UI.Menu`); refs `RetroFPS.Core`, `Unity.InputSystem`, `UnityEngine.UI`, `Unity.TextMeshPro`. Isolates the UGUI/TMP dependency from Gameplay. |

**POCO logic (`RetroFPS.UI.Menu`, EditMode-testable)**

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/UI/Menu/Logic/MainMenuStateMachine.cs` | States (FirstLoad, Root, Settings, RevertPrompt, QuitConfirm, Loading) + error flag; transitions; Back rules (root no-op); focus target per transition; input lock + 80 ms press lock; single-fire Start. |
| `Assets/_Project/Scripts/UI/Menu/Logic/MenuTween.cs` | Unscaled 0-1 tween with ease-in/out/in-out; resolves duration under reduce-motion. |
| `Assets/_Project/Scripts/UI/Menu/Logic/RevertCountdown.cs` | 10 s countdown: `Tick(dt)`, whole seconds remaining, `Expired`, danger threshold. |
| `Assets/_Project/Scripts/UI/Menu/Logic/HoldAccelerator.cs` | P3 hold acceleration: step multiplier from consecutive repeat timing. |
| `Assets/_Project/Scripts/UI/Menu/Logic/UiAudioThrottle.cs` | P8 per-event 50 ms throttle (unscaled time). |
| `Assets/_Project/Scripts/UI/Menu/Logic/MenuEvents.cs` | Static events `StartRequested`, `QuitConfirmed`, `PanelChanged`, `UiAudio`, `FadeOutComplete`, `LoadFailed` + `UiAudioEventType` enum; SubsystemRegistration reset. |

**ScriptableObjects**

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/UI/Menu/Data/MenuTheme.cs` | Palette tokens (Direction A), TMP font refs, type sizes/min sizes, timings (600/200/80/100 ms, 10 s revert, focus scale 1.06), ember/smoke/torch params. |
| `Assets/_Project/Scripts/UI/Menu/Data/StringTable.cs` | Key/value table with `Get(key)` (missing -> key + warning) and lookup cache; package-compatible keys. |

**MonoBehaviour views (`RetroFPS.UI.Menu`)**

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/UI/Menu/Views/MainMenuController.cs` | Hosts the state machine; drives CanvasGroups, selection, `sendNavigationEvents`, cursor/timeScale on start, version tag, error text; raises `MenuEvents`. |
| `Assets/_Project/Scripts/UI/Menu/Views/MenuInputRouter.cs` | `UI/Cancel` + `UI/RightClick` -> `Back()`; Tab/Shift+Tab -> explicit nav; focus guard re-selects the context default. |
| `Assets/_Project/Scripts/UI/Menu/Views/MenuFocusVisual.cs` | Select/deselect visuals (sigil, underline, colour, bold, row band, scale tween), hover -> select, 80 ms press visual, Focus audio event. |
| `Assets/_Project/Scripts/UI/Menu/Views/MenuSlider.cs` | `Slider` subclass: 0..20 integer steps (5%), hold acceleration, value-changed callback. |
| `Assets/_Project/Scripts/UI/Menu/Views/MenuCycleRow.cs` | `Selectable`: Left/Right/Submit cycles options, clickable `<` `>` arrows (non-navigable), value text. |
| `Assets/_Project/Scripts/UI/Menu/Views/SettingsPanelView.cs` | Binds the 8 rows to `ISettingsService` (read on open, request on change, refresh on `Changed`); cached % strings; opens the revert prompt. |
| `Assets/_Project/Scripts/UI/Menu/Views/ConfirmDialogView.cs` | Generic P5 dialog (prompt, two buttons, optional countdown text); used for QuitConfirm and RevertPrompt. |
| `Assets/_Project/Scripts/UI/Menu/Views/ScreenFader.cs` | FadeOverlay alpha via `MenuTween`, raycast blocking while fading, raises `FadeOutComplete`. |
| `Assets/_Project/Scripts/UI/Menu/Views/LocalizedText.cs` | Sets a TMP label from a `StringTable` key at enable. |
| `Assets/_Project/Scripts/UI/Menu/Views/MenuAmbientMotion.cs` | Torch glow noise, fog uvRect scroll, particle play/pause under reduce-motion (unscaled). |
| `Assets/_Project/Scripts/UI/Menu/Views/MenuAudioPlayer.cs` | Receiver: `MenuEvents.UiAudio` -> `PlayOneShot` with throttle; volume = `UiVolume`. Placeholder clips may be null (silent). |
| `Assets/_Project/Scripts/UI/Menu/Views/MenuLifecycle.cs` | Receiver: async preload `TestLevel_Gauntlet`, activate on fade complete, failure -> `LoadFailed`; quit = save prefs + quit/stop play mode. |

### 2.3 Editor builder (assembly `RetroFPS.Editor`, namespace `RetroFPS.Editor`)

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/Editor/MainMenuBuilder.cs` | Menu item **`RetroFPS/Build Main Menu`**. Same guards as `ArafSceneBuilder` (no Play Mode, no dirty scene, return codes). Steps: TMP Essentials check/import (stop + re-run message); create `MenuTheme_KanVeKemik` + `StringTable_MainMenu_tr` (all UX Sec. 13 keys, Turkish defaults); build prefabs; build `Assets/Scenes/MainMenu.unity`; insert MainMenu at **Build Settings index 0** (others kept in order). Idempotent (load-or-create). |
| `Assets/_Project/Scripts/Editor/MainMenuPlaceholderArt.cs` | Edit-time generation of placeholder PNGs + import settings (Point/no mips/RGBA32/Clamp; Bilinear for gradients/smoke): sigil 9x11 mask, underline, panel/dialog 9-slice, sky gradient, scrim gradient, fog noise, tower silhouettes, ember 2x2. |

**Generated by the builder (not hand-written):**
- Prefabs in `Assets/_Project/Prefabs/UI/MainMenu/`: `MenuButton`, `MenuSliderRow`, `MenuCycleRow`, `ConfirmDialog` (leaf); `MainMenuCanvas` (nests the leaves; menu list, settings panel, dialogs, fade), `MainMenuArt` (MenuCamera + ArtCanvas + particles), `MenuEventSystem`, `MainMenuSystems` (controller, router, lifecycle, audio player).
- Data in `Assets/_Project/Data/UI/`: `MenuTheme_KanVeKemik.asset`, `StringTable_MainMenu_tr.asset`.
- Placeholder art in `Assets/_Project/Art/UI/MainMenu/Placeholder/*.png`; particle materials in `Assets/_Project/Data/Materials/UI/`.
- Scene `Assets/Scenes/MainMenu.unity` (four prefab instances only).
- `Assets/TextMesh Pro/` (official TMP Essential Resources import).
- `ProjectSettings/EditorBuildSettings.asset` (MainMenu at index 0).

### 2.4 EditMode tests (assembly `RetroFPS.Tests.EditMode`, `Assets/_Project/Tests/EditMode/`)

| File | Covers |
|---|---|
| `SettingsServiceTests.cs` | Defaults on empty store; clamp of corrupt values; set -> applied + persisted; display change pending (not persisted) until Confirm; Revert restores committed; `Changed` raised. Fake store/appliers. |
| `ResolutionOptionsTests.cs` | Dedupe refresh rates, drop < 1280x720, sort, nearest match, desktop fallback. |
| `SensitivityMappingTests.cs` | 0.5 -> 1.0; 0 -> min; 1 -> max; monotonic. |
| `MainMenuStateMachineTests.cs` | Root Back no-op; Settings Back -> Root focus AYARLAR; QuitConfirm default HAYIR, Back -> Root focus ÇIKIŞ; Start fires once; Loading ignores all input; LoadFailed -> Root + error, focus BAŞLA; press lock. |
| `RevertCountdownTests.cs` | Expires at 10 s; seconds text index; danger at <= 3 s; Keep stops. |
| `MenuTweenTests.cs` | Easing endpoints; reduce-motion duration (<= 100 ms / instant). |
| `HoldAcceleratorTests.cs` | Multiplier grows on sustained repeat, resets on gap. |
| `UiAudioThrottleTests.cs` | Same event within 50 ms suppressed; different events independent. |
| `StringTableTests.cs` | Missing key returns key; generated `MainMenu` table contains every UX Sec. 13 key (`Assert.Ignore` if the builder has not run). |

### 2.5 Modified existing files (minimal diffs)

| File | Change |
|---|---|
| `Assets/_Project/Scripts/Core/GameResolutionManager.cs` | Replace the hard-coded `SetResolution(1920,1080,FullScreenWindow)` with `GameSettings.EnsureInitialized()` + apply persisted/first-boot display; keep `targetFrameRate = 144`. ~10 lines. |
| `Assets/_Project/Scripts/Player/PlayerLook.cs` | Add `[SerializeField] private float sensitivityMinScale = 0.25f, sensitivityMaxScale = 4f;`, cached `_sensitivityScale`; `OnEnable`/`OnDisable` subscribe to `GameSettings.Changed`; `HandleLook` multiplies by the scale. ~15 lines; base `mouseSensitivity` untouched. |
| `Assets/_Project/Scripts/Combat/CombatAudio.cs` | Lines 412 and 424: multiply by `GameSettings.Current.SfxVolume`. 2 lines. |
| `Assets/_Project/Scripts/Editor/RetroFPS.Editor.asmdef` | Add references `RetroFPS.Menu`, `UnityEngine.UI`, `Unity.TextMeshPro`. |
| `Assets/_Project/Tests/EditMode/RetroFPS.Tests.EditMode.asmdef` | Add reference `RetroFPS.Menu`. |

Not modified: `RetroFPS.Gameplay.asmdef`, `RetroFPS.Core.asmdef`, `InputSystem_Actions.inputactions`, `manifest.json`.

Counts: 7 Core + 1 asmdef + 6 menu POCO + 2 SO + 12 views + 2 editor + 9 tests = **39 new files**, **5 modified**.

---

## 3. Placeholders vs User Art

| Asset | Now (placeholder, builder-generated or built-in) | Needs user/artist |
|---|---|---|
| Title + UI fonts | TMP `LiberationSans SDF` + its dynamic fallback | Grenze Gotisch, Alegreya Sans SC .ttf (OFL) -> TMP SDF assets + `OFL.txt`; glyph test must pass |
| Title logotype | TMP text `menu.title` | Optional logo image (art-director, deferred) |
| ArtSky | Generated vertical gradient PNG | Final 640x360 sky (+21:9 extension) |
| ArtTowerFar / ArtTowerNear | Stepped-rectangle silhouette PNGs | **3D tower render baked to 640x360 PNG** (user's modelling) |
| ArtFog | Generated value-noise PNG, uv scroll | Optional painted fog strip |
| ArtTorchGlow | Built-in `Default-Particle` tinted, additive | Optional 64x64 glow |
| LeftScrim, DimOverlay, FadeOverlay | Procedural (also final) | none |
| Sigil, underline, panel/dialog 9-slice, jewel, divider, slider parts, cycle arrows | Generated pixel-mask PNGs / solid Images / TMP `<` `>` | Final pixel-art ornaments (Kemik Mühür sigil, vertebra frames) |
| Embers / smoke | Generated 2x2 PNG + built-in particle texture, URP Particles/Unlit | none required (embers final as-is) |
| UI SFX (Focus, Confirm, Back, SliderStep, VolumePreview, DialogOpen, StartRun, Error) | **Silent** (null clips; events and throttle still run) | WAV assets (sfxr-style is fine) |
| Menu ambience (Music bus) | none | Optional loop |
| AudioMixer | none (`AudioListener.volume` + per-source multiply) | Optional: user hand-authors a mixer for true buses |

---

## 4. Open Items / Concerns

1. **Boot behaviour change**: the first boot becomes Fullscreen (exclusive) at desktop resolution (UX 8) instead of today's forced 1920x1080 borderless. The user should confirm, or choose "Borderless at desktop" as the default.
2. **SFX coverage is partial without an AudioMixer**: `EncounterArea`, `WaveSpawnDirector` and `OccultSpawnTelegraph` AudioSources ignore the SFX slider (Master still applies). The Music slider has no audible target yet.
3. **UI SFX are silent placeholders**, so the P3 volume-preview sample is inaudible until clips exist (UX open item, audio-director).
4. **TMP Essential Resources import** adds `Assets/TextMesh Pro/` and needs the builder run twice the first time.
5. **Raycast Receiver (6.5)** class name unverified; transparent Image is used meanwhile.
6. **Settings defaults** are code-side in Core (needed before any scene loads). The values are a UX open item owned by game-designer.
7. **Spec deviation**: settings "events" are implemented as `ISettingsService` request methods + a `Changed` event (1.6).
8. **Evidence**: UI story closure needs retained screenshots per UX Sec. 6 state in `production/qa/evidence/`, plus a Win64 build for display/revert checks (the Editor ignores `SetResolution`).
