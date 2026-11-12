# UX Spec: Main Menu

> Authoring guidance: .claude/docs/templates/guidance/ux-spec-guide.md (load per-section as you author — do not read entirely).

> **Status**: Draft
> **Author**: user + ux-designer
> **Last Updated**: 2026-11-12
> **Journey Phase(s)**: Boot -> Menu -> Run start (ASSUMED; `design/player-journey.md` absent)
> **Screen / Flow Name**: `MainMenu` (scene `MainMenu`, Build Settings index 0). Sub-panels: `MainMenu.Settings`, `MainMenu.QuitConfirm`
> **Platform Target**: PC Win64. Keyboard/mouse primary, gamepad partial, no touch. 1920x1080 reference; scales to 16:10 and 21:9 with safe layout.
> **Related GDDs**: None (no game brief / menu GDD exists). Run loop context: Araf run loop commits (`feat(run)`), scene `TestLevel_Gauntlet`.
> **Related ADRs**: None.
> **Related UX Specs**: `design/ux/interaction-patterns.md` (P1-P8); future `settings.md` (Settings panel is specified inline here until split out); future `pause-menu.md`.
> **Accessibility Tier**: Standard (COMMITTED, per `design/accessibility-requirements.md`)
> **Template**: UX Spec

> **Accessibility notice**: Designed against the COMMITTED Standard tier (`design/accessibility-requirements.md`): full keyboard + gamepad navigation, non-colour focus indicator, menu text >= 24 px at 1080p, contrast >= 4.5:1 body / >= 3:1 large text, reduce-motion toggle, no information by colour or sound alone. Known gaps vs Standard are listed in Section 12 and Open Questions.

---

## 1. Purpose & Player Need

**What player need does this screen serve?**

The player wants to get into the fight immediately, with the option to tune the game to their machine and hands first, and to leave without accident. The screen sets the dark-fantasy tone in the first seconds and then gets out of the way: three choices, no clutter, no friction.

**The player goal** (what the player wants to accomplish):

Start a run (BAŞLA), adjust audio/sensitivity/display (AYARLAR), or quit (ÇIKIŞ) within 2 inputs from first-load, with no accidental quit.

**The game goal** (what the game needs to communicate or capture):

Load `TestLevel_Gauntlet` on explicit player intent; capture and persist settings via PlayerPrefs; establish the PURGATOR tone and title.

---

## 2. Player Context on Arrival

| Question | Answer |
|----------|--------|
| What was the player just doing? | Launched the game (first-load), or returned from Settings / cancelled the quit dialog. (Return from a run is out of scope until a pause/death flow exists.) |
| What is their emotional state? | Anticipation; wants to play fast. Low patience for menus. |
| What cognitive load are they carrying? | Low. Nothing tracked. |
| What information do they already have? | Genre expectations only. No save state exists. |
| What are they most likely trying to do? | Press BAŞLA. Secondary: check volume/sensitivity/resolution before first play. |
| What are they likely afraid of? | Accidental quit; settings not applying or not persisting; unreadable text on ornate art; hitting Esc and exiting. |

**Emotional design target for this screen**:

Foreboding calm at the foot of the tower: heavy, confident, and instantly actionable.

---

## 3. Navigation Position

**Screen hierarchy**:

```
MainMenu (scene, root)
  ├── Settings panel (in-scene panel, replaces menu list focus)
  │     └── (P4 resolution/window revert prompt, 10 s, within Settings)
  └── QuitConfirm dialog (P5 modal)
```

**Modal behavior**: Root menu is the top-level screen (non-modal, nothing beneath it). Settings: in-scene panel, blocks menu list input while open; dismissed by Esc / gamepad B / right-click / "GERİ" button (P6). QuitConfirm: Modal (P5); dismissed by Esc / B / right-click = HAYIR (P6). Esc on the root menu does nothing (P6): no quit, no dialog.

**Reachability — all entry points**:

| Entry Point | Triggered By | Notes |
|-------------|-------------|-------|
| Game boot | Launch; `MainMenu` is Build index 0 | First-load state |
| Return from Settings | P6 back on Settings | Focus returns to AYARLAR |
| Return from QuitConfirm | HAYIR / P6 | Focus returns to ÇIKIŞ |
| Return from gameplay | Not defined yet | Deferred; Open Questions |

---

## 4. Entry & Exit Points

**Entry table**:

| Trigger | Source Screen / State | Transition Type | Data Passed In | Notes |
|---------|----------------------|-----------------|----------------|-------|
| Game launch | None (boot) | Fade-in from black (see Sec. 10) | None; reads saved settings from PlayerPrefs | Cursor free+visible, `Time.timeScale = 1` enforced on scene start |
| Settings closed | Settings panel | Panel cross-fade (P7) | None | Focus -> AYARLAR |
| Quit cancelled | QuitConfirm | Dialog fade (P7) | None | Focus -> ÇIKIŞ |

**Exit table**:

| Exit Action | Destination | Transition Type | Data Returned / Saved | Notes |
|-------------|------------|-----------------|----------------------|-------|
| BAŞLA | Scene `TestLevel_Gauntlet` | Fade to black 0.6 s, then load (P7); input locked | None | No confirm (P5 "when NOT to use") |
| AYARLAR | Settings panel | Panel cross-fade 0.15-0.25 s (P7) | Settings applied live + saved to PlayerPrefs per change | Panel back (P6) returns here |
| ÇIKIŞ -> EVET | Application quit | Immediate (editor: stop play mode) | PlayerPrefs flushed before quit | Via event, UI does not call `Application.Quit` directly (Sec. 9) |
| ÇIKIŞ -> HAYIR | Root menu | Dialog fade (P7) | None | Default focus is HAYIR |

---

## 5. Layout Specification

### 5.1 ASCII Wireframe

```
┌──────────────────────────────────────────────────────────────────────────┐
│ ┌ SAFE AREA (5% margin) ───────────────────────────────────────────────┐ │
│  PURGATOR                                 {  Tower illustration,       }  │
│  (title, top-left)                        {  layered 2D parallax-free, }  │
│                                           {  slow smoke / embers       }  │
│                                           {  (dark, right ~55%)        }  │
│                                           {                            }  │
│                                           {                            }  │
│  ▶ [ BAŞLA   ]  ●                         {                            }  │
│    [ AYARLAR ]                            {                            }  │
│    [ ÇIKIŞ   ]                            {                            }  │
│  (menu list, bottom-left)                 {                            }  │
│  v0.x build tag (small, bottom-left)                                      │
│ └──────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘

Settings panel (replaces menu list area, left ~45%, art stays visible right):
  AYARLAR
  SES                (non-focusable group label)
  Ana Ses            [=========----]  70%      (P3)
  Müzik              [=======------]  50%      (P3)
  Efektler           [==========---]  80%      (P3)
  Arayüz Sesleri     [==========---]  80%      (P3)
  KONTROL            (non-focusable group label)
  Fare Hassasiyeti   [=====--------]  40%      (P3)
  GÖRÜNTÜ            (non-focusable group label)
  Pencere Modu       <  Tam Ekran  >           (P4)
  Çözünürlük         <  1920x1080  >           (P4)
  Hareketi Azalt     <  Kapalı  >              (P4)
  [ GERİ ]

QuitConfirm (centered over dimmed scene):
  ╔══════════════════════════════╗
  ║  Oyundan çıkılsın mı?        ║
  ║   [ EVET ]    ● [ HAYIR ]    ║
  ╚══════════════════════════════╝
```

### 5.2 Layout Zones

| Zone Name | Description | Approximate Size | Scrollable? | Overflow Behavior |
|-----------|-------------|-----------------|-------------|-------------------|
| Safe area | Anchored container, 5% margin all sides; all UI inside | 90% x 90% | No | Anchors scale with aspect; no UI outside |
| Title | PURGATOR logotype text, top-left | ~35% x 15% | No | Shrink-to-fit to zone width; never wraps |
| Menu list | Vertical list, bottom-left, bottom-aligned | ~25% x 30% | No | Fixed 3 items; per-item min width, text shrink-to-fit |
| Settings panel | Replaces menu list; grows upward from bottom-left | ~42% x 60% | No (8 rows + 3 group labels + back must fit; verify at 1280x720) | Rows shrink-to-fit; at min resolution panel fits within safe area |
| Scene art | Right-side illustration, full-bleed behind UI | 100% bg, subject on right ~55% | No | Background scales to cover (crop-to-fill); at 21:9 extend dark gradient/side extension, subject stays inside 16:9 centre-right; at 16:10 crop top/bottom only |
| Dialog | Centered modal with dim layer | ~30% x 20% | No | Buttons stack vertically if text exceeds width |

### 5.3 Component Inventory

| Component Name | Type | Zone | Purpose | Required? | Reuses Existing Component? |
|----------------|------|------|---------|-----------|---------------------------|
| TitleText | TMP label | Title | Game title (string key, serialized placeholder) | Yes | No — new |
| MenuList | Container (UGUI vertical layout + Navigation) | Menu list | Hosts 3 items | Yes | Yes — P1 |
| MenuButton x3 (BAŞLA, AYARLAR, ÇIKIŞ) | Button | Menu list | Primary actions | Yes | Yes — P2 |
| FocusSigil | Image marker | Menu list / rows | Non-colour focus indicator next to focused item | Yes | Yes — P1/P2 |
| SceneArtLayers | Image layers + particle/UI scroll | Scene art | Tower illustration, smoke/ember motion | Yes | No — new |
| SettingsPanel | Panel | Settings panel | Hosts rows | Yes | No — new |
| SliderRow x5 | Settings row | Settings panel | Master/Music/SFX/UI (Arayüz Sesleri)/Mouse sensitivity | Yes | Yes — P3 |
| GroupLabel x3 (SES, KONTROL, GÖRÜNTÜ) | TMP label | Settings panel | Non-focusable group headers | No | No — new |
| CycleRow x2 (+1 reduce-motion) | Settings row | Settings panel | Window mode, resolution (+ reduce motion) | Yes (motion reduction is a Standard-tier feature) | Yes — P4 |
| BackButton | Button | Settings panel | Visible "Geri" (mouse) | Yes | Yes — P6 |
| RevertPrompt | Dialog + 10 s countdown | Dialog | Confirm/revert every resolution or window-mode change | Yes | Yes — P4 + P5 |
| QuitConfirmDialog | Modal | Dialog | Quit confirmation | Yes | Yes — P5 |
| FadeOverlay | Full-screen black Image (top canvas) | Overlay | First-load fade-in; load fade-out; input blocker | Yes | Yes — P7 |
| VersionTag | TMP label | Safe area bottom-left | Build version | No | No — new |

**Primary focus element on open**: BAŞLA (first item). On return from Settings -> AYARLAR; from QuitConfirm -> ÇIKIŞ. If the mouse is the last-used device on first load, BAŞLA still holds logical focus so keyboard/gamepad work without a click.

### 5.4 Information Hierarchy

1. Title: establishes identity; first read, largest type.
2. Menu list: the only actions; focus sigil makes the current item unmistakable.
3. Scene art: atmosphere; non-informational, never competes with text (darkened behind the left column).
4. Version tag: lowest priority, small, bottom edge.
Settings values and the quit prompt are hidden behind AYARLAR / ÇIKIŞ (progressive disclosure).

---

## 6. States & Variants

| State Name | Trigger | What Changes Visually | What Changes Behaviorally | Notes |
|------------|---------|----------------------|--------------------------|-------|
| First-load | Scene start | Fade from black ~0.6 s; menu list fades in; art motion starts | Input locked until fade ends; then BAŞLA focused; cursor free+visible; `timeScale = 1` | Unlock at fade end, or on first input if reduce-motion |
| Idle / item focus | Navigation or hover | Sigil beside item, brighter + slight scale on focused; others dim (P1/P2) | One focused item; hover moves focus (P1) | Focus is identical for hover and keyboard (P2) |
| Item pressed | Confirm input | ~80 ms pressed visual (P2) | Short input lock after action (P2) | |
| Settings open | AYARLAR confirmed | Menu list hidden/dimmed; SettingsPanel cross-fades in; first row focused | Menu list gets no input; changes apply live and persist | Esc/B/right-click/GERİ closes (P6) |
| Revert prompt | EVERY resolution change and EVERY window-mode change (no risk filtering) | Dialog with 10 s countdown (text number, not colour) | Default = Geri al (revert); focus on revert | Per P4 |
| Confirm-quit open | ÇIKIŞ confirmed | Scene dims; dialog centred | Focus trapped in dialog; default HAYIR; menu inert (P5) | Esc/B = HAYIR |
| Loading | BAŞLA confirmed | Fade to black 0.6 s; art motion continues until black | Input fully locked (incl. Esc, mouse); async scene load; no re-trigger | Cursor handling passes to gameplay scene |
| Reduce-motion variant | Reduce-motion on | Art static, no smoke/ember motion; fades become instant or <= 100 ms; no focus scale animation (sigil + brightness remain) | Same logic | Applies at first-load too |
| Disabled | n/a | No disabled items on this screen | | Continue is intentionally absent, not disabled |
| Error | Scene load fails | Message "Seviye yüklenemedi" shown on menu; fade reverts | Input unlocked; focus on BAŞLA | Also logs error |

---

## 7. Interaction Map

### 7.1 Navigation Inputs

| Input | Platform | Action | Visual Response | Audio Cue | Notes |
|-------|----------|--------|-----------------|-----------|-------|
| Up/Down, W/S | Keyboard | Move focus, wrap at ends (P1) | Sigil + brightness move | Focus tick (P8) | |
| Tab / Shift+Tab | Keyboard | Next/previous item in reading order, wraps | Same | Focus tick | Same order as Up/Down |
| D-pad / left stick | Gamepad | Move focus (P1) | Same | Focus tick | Stick deadzone via Input System UI module |
| Mouse hover | Mouse | Focus follows hover (P1) | Same | Focus tick | |
| Left/Right, A/D | Keyboard | In Settings: adjust slider / cycle value (P3/P4) | Value updates | Slider step / focus tick | Hold accelerates (P3) |
| D-pad L/R | Gamepad | Same | Same | Same | |
| Esc / B / right-click | All | Back (P6): Settings -> menu; dialog -> HAYIR; root = no-op | Panel cross-fade | Back sound (not on root no-op) | Root: nothing happens |

### 7.2 Action Inputs

| Input | Platform | Context (What must be focused) | Action | Response | Animation | Audio Cue | Notes |
|-------|----------|-------------------------------|--------|----------|-----------|-----------|-------|
| Enter / Space / A / left click | All | BAŞLA | Start run | Fire `MenuStartRequested`; loading state | Press 80 ms, fade 0.6 s (P7) | Confirm | No confirm dialog |
| Same | All | AYARLAR | Open Settings | Panel opens, first row focused | Cross-fade 0.15-0.25 s | Confirm | |
| Same | All | ÇIKIŞ | Open QuitConfirm | Dialog, focus HAYIR | Fade 0.15-0.25 s | Confirm | P5 |
| Same | All | EVET | Quit | Fire `MenuQuitConfirmed` | Instant | Confirm | |
| Same | All | HAYIR | Close dialog | Focus -> ÇIKIŞ | Fade | Back | |
| Left/Right, drag, click track | All | Slider row | Change value %5 step, apply immediately, persist | Value text updates | None | Slider step; volume sliders play sample (P3) | Boundary: silent stop |
| Left/Right, Enter, arrow click | All | Cycle row | Next/prev option; apply immediately | Value text updates | None | Confirm tick | Every window-mode/resolution change triggers the P4 revert prompt (10 s, default revert) |
| Enter / A / click | All | GERİ | Close Settings (P6) | Focus -> AYARLAR | Cross-fade | Back | |
| Right-click, other keys | Mouse/KB | Anywhere on root | No-op (right-click on root: no-op) | None | None | None | No undefined behaviour |

### 7.3 State-Specific Behaviors

| State | Input Restriction | Reason |
|-------|------------------|--------|
| First-load fade | All input ignored until fade ends | Prevents mis-press while content appears |
| Settings open | Menu list not navigable; root actions inert | Single focus context |
| Revert prompt / QuitConfirm | Focus trapped in dialog; background inert (P5) | Avoid accidental destructive input |
| Loading | All input locked incl. Esc | Prevent double-trigger / cancel mid-load (P7) |
| Scene transition / press lock | Brief lock after any action (P2) | No double activation |

---

## 8. Data Requirements

| Data Element | Source System | Update Frequency | Who Owns It | Format | Null / Missing Handling |
|--------------|--------------|-----------------|-------------|--------|------------------------|
| Master/Music/SFX/UI volume | Settings service (PlayerPrefs-backed) | On scene start; on each slider change | Settings/Audio system | float 0-1 shown as 0-100% | Default 100/70/100/80 (tunable) |
| Mouse sensitivity | Settings service | On scene start; on slider change | Settings/Player input system | float, shown as % | Default 50%. Gameplay dependency: the player camera/look code must read this value from the Settings service on load and on change (not via the menu UI) |
| Window mode | Settings service / `Screen` | On scene start; on cycle | Settings service | enum (Fullscreen, Borderless, Windowed) | Default Fullscreen |
| Resolution | Settings service / `Screen.resolutions` | On scene start; on cycle | Settings service | width x height list filtered from supported | Fall back to current desktop resolution |
| Reduce motion | Settings service | On scene start; on toggle | Settings service | bool | Default false (or OS preference if available) |
| Localised strings | String table | On scene start / locale change | Localization system | string by key | Show key + log warning, never blank |
| Build version | Application.version | Scene start | Build | string | Hide tag |
| Input device (last used) | Input System | On any input | Input System | enum | Assume keyboard/mouse |

> **Rule**: This screen never writes game state directly. Settings rows send change requests to the Settings service, which persists to PlayerPrefs and notifies listeners.

---

## 9. Events Fired

| Player Action | Event Fired | Payload | Receiver System | Notes |
|---------------|-------------|---------|-----------------|-------|
| Confirm BAŞLA | `MenuStartRequested` | `{sceneName:"TestLevel_Gauntlet"}` | Scene loader | Loader performs async load after 0.6 s fade |
| Change volume slider (keys: masterVolume, musicVolume, sfxVolume, uiVolume) | `SettingChangeRequested` | `{key, value}` | Settings service | Applies + persists (PlayerPrefs) immediately; also audio event system |
| Change sensitivity | `SettingChangeRequested` | `{key:"mouseSensitivity", value}` | Settings service | Gameplay reads on load |
| Change window mode / resolution | `DisplaySettingChangeRequested` | `{mode, width, height}` | Settings service | Every change starts the 10 s revert timer (P4); `DisplaySettingConfirmed` / `DisplaySettingReverted` follow |
| Toggle reduce motion | `SettingChangeRequested` | `{key:"reduceMotion", value}` | Settings service | |
| Confirm EVET | `MenuQuitConfirmed` | `{}` | Application lifecycle service | Flush PlayerPrefs, then quit |
| Open/close Settings, dialog | `MenuPanelChanged` | `{panel, open}` | Analytics only | Optional |
| UI sound events | `UiAudioEvent` | `{eventType}` | Audio event system (P8) | UI never plays AudioSource directly |

---

## 10. Transitions & Animations

| Transition | Trigger | Direction / Type | Duration (ms) | Easing | Interruptible? | Skipped by Reduced Motion? |
|------------|---------|-----------------|--------------|--------|----------------|---------------------------|
| First-load fade-in | Scene start | Black -> menu | 600 | Ease-out | No | Yes: <= 100 ms fade |
| Panel change (menu <-> Settings) | AYARLAR / P6 | Cross-fade | 150-250 | Ease-in-out | Yes (last wins) | Yes: instant |
| Dialog open/close | ÇIKIŞ / HAYIR | Fade + dim | 150-250 | Ease-in-out | Yes | Yes: instant |
| Focus change | Navigation | Sigil snap + brightness/scale | 0-100 (instant per P7) | Linear | Yes | Scale animation off; sigil + brightness stay |
| Button press | Confirm | Press visual | 80 | Linear | No | No (needed feedback), no scale |
| Menu -> game | BAŞLA | Fade to black | 600 | Ease-in | No | Yes: <= 100 ms |
| Ambient art motion | Always | Slow smoke/ember drift | Loop | Linear | n/a | Yes: fully static |

All timings use `Time.unscaledDeltaTime` (P7).

---

## 11. Input Method Completeness Checklist

**Keyboard**
- [x] All interactive elements are reachable using Tab and arrow keys alone
- [x] Tab order follows visual reading order (title not focusable; menu top-to-bottom)
- [x] Every action achievable by mouse is also achievable by keyboard
- [x] Focus is visible at all times (sigil + brightness, not colour only)
- [x] Focus does not escape the screen while it is open (focus trap for Settings/dialogs)
- [x] Esc closes or cancels (and does not quit the game from within a screen; root Esc is a no-op)

**Gamepad**
- [x] All interactive elements reachable with D-Pad and left stick
- [x] Face button mapping documented: A = confirm, B = back (Section 7.2, P6)
- [x] No action requires analog stick precision that cannot be replicated with D-Pad
- [x] Trigger and bumper shortcuts: none used
- [ ] Controller disconnection while screen is open: menu remains usable by keyboard/mouse, no modal on disconnect. Needs implementation verification.

**Mouse**
- [x] Hover states defined for all interactive elements (= focus, P2)
- [x] Hit targets >= 44 px tall at 1080p (menu items full row width); verify at min resolution
- [x] Right-click: back (P6) in Settings/dialogs; no-op on root
- [x] Scroll wheel: no scrollable zones; wheel on sliders is ignored

**Touch**: Not applicable (no touch target).

---

## 12. Accessibility

> Standard tier, COMMITTED (`design/accessibility-requirements.md`).
>
> Standard-tier rules applied here: menu text >= 24 px at 1080p (scales with resolution); contrast >= 4.5:1 for body text and >= 3:1 for large text; focus never by colour alone; reduce-motion toggle present.
>
> **Known gaps vs Standard (not blocking this spec; owner user)**: (1) UI scaling 75-150% is deferred by user decision 2026-11-12, so there is no Settings row yet; (2) input remapping lives in a future separate KONTROLLER screen with its own spec, not on this menu now; (3) brightness/gamma and the flash warning are deferred.

**Text contrast requirements for this screen**:

| Text Element | Background Context | Required Ratio | Current Ratio | Pass? |
|--------------|-------------------|---------------|---------------|-------|
| Title | Dark art / left-side darkening gradient | 4.5:1 | TBD | [ ] |
| Menu items (normal and focused) | Same | 4.5:1 | TBD | [ ] |
| Settings labels and values | Panel backing (opaque ~85% dark) | 4.5:1 | TBD | [ ] |
| Dialog text and buttons | Dim layer + opaque dialog | 4.5:1 | TBD | [ ] |
| Version tag | Dark art edge | 4.5:1 | TBD | [ ] |

Art behind text zones must be darkened/backed so ratio holds regardless of ember or smoke position.

**Colorblind-unsafe elements and mitigations**:

| Element | Colorblind Risk | Mitigation |
|---------|----------------|------------|
| Focused item | Colour-only highlight | Sigil marker + brightness/scale difference |
| Slider fill | Colour-only value | Numeric percentage text |
| Cycle value | Icon-only | Always text value (P4) |
| Revert countdown | Colour urgency | Numeric seconds text |

**Focus order** (Tab key sequence, numbered):

Root: 1. BAŞLA, 2. AYARLAR, 3. ÇIKIŞ, then wraps to 1. Title, art, version tag are not focusable.
Settings: 1. Ana Ses, 2. Müzik, 3. Efektler, 4. Arayüz Sesleri, 5. Fare Hassasiyeti, 6. Pencere Modu, 7. Çözünürlük, 8. Hareketi Azalt, 9. GERİ, wraps to 1. Group labels (SES, KONTROL, GÖRÜNTÜ) are not focusable. Root list is inert.
QuitConfirm: HAYIR (default), EVET, wraps. Nothing behind is reachable.

**Screen reader announcements for key state changes**:

Screen reader announcements: not required. Menu screen reader is a Comprehensive-tier feature and is listed under Known Intentional Limitations in `design/accessibility-requirements.md`.

| State Change | Announcement Text | Announcement Timing |
|--------------|------------------|---------------------|
| n/a (out of scope) | n/a | n/a |

**Cognitive load assessment**:

Root: 3 choices, 0 concurrent streams. Settings: 9 rows (5 sliders, 3 cycle rows, GERİ), all independent and visible at once in one column with no tabs. That is at the top of the 7±2 range, so the rows are grouped under three non-focusable headers (SES, KONTROL, GÖRÜNTÜ) to chunk them into 4+1+3. Dialog: 2 choices. Ambient motion is non-informational and can be turned off.

Additional: no flashing content; ember/smoke motion is slow and low-contrast; no information by sound alone (P8 visual equivalents).

---

## 13. Localization Considerations

**General rules for this screen**:
- Player-facing text is Turkish only (no other locale planned). All strings go through a simple ScriptableObject string table keyed by the keys below; none hardcoded. The Unity Localization package is NOT used now; the key scheme stays package-compatible so migration later is a data import.
- Budget >= 40% expansion; menu items and title shrink-to-fit.
- RTL not a target; CJK not a target (revisit if locale plan changes).
- No text in images. The title is rendered text (or a logo image with a text-key alt in the string table; art decision deferred to art-director).

**String keys** (table: `MainMenu`):
`menu.title`, `menu.start`, `menu.settings`, `menu.quit`, `menu.version_format`, `menu.load_error`,
`settings.title`, `settings.master_volume`, `settings.music_volume`, `settings.sfx_volume`, `settings.ui_volume`, `settings.group.audio`, `settings.group.controls`, `settings.group.display`, `settings.mouse_sensitivity`, `settings.window_mode`, `settings.window_mode.fullscreen`, `settings.window_mode.borderless`, `settings.window_mode.windowed`, `settings.resolution`, `settings.reduce_motion`, `settings.on`, `settings.off`, `settings.back`, `settings.percent_format`,
`display.revert_title`, `display.revert_countdown_format`, `display.keep`, `display.revert`,
`quit.prompt`, `quit.yes`, `quit.no`.

Turkish defaults: BAŞLA, AYARLAR, ÇIKIŞ, GERİ, EVET, HAYIR, "Oyundan çıkılsın mı?", "Seviye yüklenemedi". Note Turkish dotted/dotless I casing (İ/ı): use culture-aware upper-casing or store strings pre-cased; verify font glyph coverage for Ç Ş Ğ İ Ö Ü.

| Text Element | English Baseline Length | Max Characters | Expansion Budget | RTL Behavior | Overflow Behavior | Risk |
|--------------|------------------------|----------------|-----------------|--------------|-------------------|------|
| Title (`menu.title`) | 9 (placeholder) | 16 | 75% | n/a | Shrink-to-fit | Low |
| Menu items | 5-8 | 14 | 75% | n/a | Shrink-to-fit | Low |
| Settings labels | 10-18 | 26 | 40% | n/a | Shrink-to-fit, then truncate with ellipsis | Medium |
| Cycle values | 9-12 | 18 | 50% | n/a | Shrink-to-fit | Medium |
| Dialog prompt | 22 | 40 | 80% | n/a | Wrap to 2 lines | Low |
| Dialog buttons | 4-5 | 10 | 100% | n/a | Stack vertically | Low |
| Load error | 22 | 40 | 80% | n/a | Wrap | Low |

---

## 14. Acceptance Criteria

**Performance**
- [ ] First frame of the menu visible within 200 ms of scene start at 1920x1080 on the development machine (Win64) (excluding fade)
- [ ] Menu interactive within 500 ms of scene start at 1920x1080 on the development machine (Win64) (plus fade-in)
- [ ] Navigation and panel changes hold target framerate (±5 fps) with ambient motion on
- [ ] Menu steady state allocates zero GC per frame (no string concat in Update)

**Layout & Rendering**
- [ ] Correct (no overlap, cutoff, overflow) at 1280x720 (minimum) and 3840x2160 (maximum)
- [ ] Correct at 16:9, 16:10 and 21:9; no UI outside the safe area; at 21:9 no art seams or letterboxing
- [ ] 4:3 renders without overlap (best-effort)
- [ ] No text overflow in Turkish within max-character bounds
- [ ] No text overflow with a pseudo-localised +40% string set
- [ ] All states in Section 6 render correctly

**Input**
- [ ] Every interactive element reachable by keyboard only (Tab/arrows/Enter/Esc)
- [ ] Every interactive element reachable by gamepad only (D-pad/stick, A, B)
- [ ] Every interactive element reachable by mouse only (hover, click, drag, GERİ button, right-click)
- [ ] On first load BAŞLA is focused with no click needed
- [ ] Esc / B / right-click on the root menu does nothing (no quit, no dialog)
- [ ] ÇIKIŞ opens the dialog with HAYIR focused; Esc/B closes it; EVET quits; Tab cannot reach the menu behind it
- [ ] Focus is visible at all times and is distinguishable in greyscale
- [ ] Cursor is visible and unlocked (`Cursor.lockState == None`) and `Time.timeScale == 1` on scene start
- [ ] During loading, no input has any effect; BAŞLA cannot be double-triggered
- [ ] Keyboard focus and hover cannot both show different focused items (single focus)

**Events & Data**
- [ ] BAŞLA fires `MenuStartRequested` once with `sceneName` = `TestLevel_Gauntlet` and loads that scene after 0.6 s fade
- [ ] Each Settings change applies immediately and persists across game restart (PlayerPrefs)
- [ ] EVERY resolution change and EVERY window-mode change shows the revert prompt; no input for 10 s reverts to the previous value; "Keep" commits and persists
- [ ] UI code contains no direct `Application.Quit`, `AudioSource.Play` or PlayerPrefs writes (events only)
- [ ] Closing and reopening Settings shows the current persisted values

**Accessibility**
- [ ] Body text >= 4.5:1 and large text >= 3:1 against its background (measure with art animated and static)
- [ ] All menu text (items, labels, values, dialog) renders >= 24 px at 1920x1080 and scales proportionally at other resolutions
- [ ] No information by colour alone (focus, slider value, countdown)
- [ ] No information by sound alone (every P8 cue has a visual)
- [ ] Reduce-motion on: art static, fades instant or <= 100 ms, no scale animation
- [ ] No flashing content

**Localization**
- [ ] All displayed text comes from string-table keys in Section 13 (no hardcoded text)
- [ ] Turkish glyphs (Ç Ş Ğ İ ı Ö Ü) render in the chosen font; upper-casing of İ/ı correct

---

## 15. Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| No game brief/concept document: tone and pillars assumed from the brief. | creative-director | Before Approved | Open |
| `design/player-journey.md` absent: menu arrival context (Section 2) is assumed. | ux-designer | Before Approved | Open |
| Accessibility tier not committed: which tier? | accessibility-specialist | Before Approved | Resolved 2026-11-12: Standard committed (`design/accessibility-requirements.md`). |
| Known gaps vs Standard: UI scaling 75-150% (deferred by user decision 2026-11-12, no Settings row yet); input remapping (future separate KONTROLLER screen with its own spec); brightness/gamma and flash warning (deferred). Not blocking this spec. | user | Before Approved for Standard conformance | Open |
| Is a reduce-motion toggle in the Settings panel acceptable (not in the user's listed settings rows) or should it follow the OS setting only? | user | Before Approved | Resolved 2026-11-12: keep the "Hareketi Azalt" row. |
| Return path from gameplay (pause menu / death / run end) to MainMenu is undefined; Entry Table has no such row. | game-designer | Before pause-menu spec | Open |
| Window mode / resolution revert prompt (10 s, P4): which changes trigger it? | user | Before Approved | Resolved 2026-11-12: every resolution change and every window-mode change; default is revert. |
| Default values for volume/sensitivity and supported minimum resolution (1280x720 assumed). | game-designer | Before implementation | Open |
| Title is a text placeholder (serialized later): final logotype and tower illustration art direction. | art-director | Before implementation | Open |
| Menu audio: P8 event system has no UI counterpart yet; sound assets for focus/confirm/back undefined. | audio-director / ui-programmer | Before implementation | Open |
| Localization plan: which locales and which mechanism? | user | Before Approved | Resolved 2026-11-12: Turkish only; ScriptableObject string table keyed by Section 13 keys; no Unity Localization package now (migratable later). |
| Screen reader announcements for UGUI menus at Standard? | accessibility-specialist | Before Approved | Resolved 2026-11-12: not required — Comprehensive feature, out of scope per `design/accessibility-requirements.md` Known Intentional Limitations. |
