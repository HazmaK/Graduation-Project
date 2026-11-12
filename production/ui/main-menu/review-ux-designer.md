# Phase 4 Review: PURGATOR Main Menu (ux-designer, three streams)

Reviewer: ux-designer, covering (A) UX vs spec, (B) art-director stand-in (Direction A), (C) accessibility-specialist stand-in (Standard tier).
Date: 2026-11-12. Mode: read-only review of the implementation; nothing under `Assets/` was edited.

## 0. Evidence and its limits

| Evidence | Status |
|---|---|
| `production/qa/evidence/main-menu-01-root.png` (root state, 1280x720 capture) | Seen. Covers the root state only. |
| Settings, RevertPrompt, QuitConfirm, Loading, Error, first-load, reduce-motion states | No screenshot. Reviewed from code only. All visual claims for these states are UNVERIFIED. |
| EditMode tests for `MainMenuStateMachine`, `RevertCountdown`, `StringTable`, `SettingsService`, `MenuTween` | Test files exist. No run log was provided, so I count them as supporting evidence for PASS-BY-CODE, not as PASS. |
| Contrast | I recomputed three of the spec's ratios (text on bg 14.51, text-dim on panel 6.50, accent on slider track about 3.15) and they match. No in-engine pixel sampling exists. |

Status legend: PASS (verified by screenshot or a run test), PASS-BY-CODE, FAIL, UNVERIFIED.

---

## Stream A: UX (implementation vs spec)

### A1. Findings (most severe first)

| ID | Severity | Where | Finding | Recommendation (for ui-programmer) |
|---|---|---|---|---|
| A-1 | High, fix before sign-off | `MenuSlider.cs` (no `OnPointerDown` override); `MainMenuBuilder.cs:172-189` (slider row), `:178` (track 235 wide), `:579-584` (`Row()` root Image has `raycastTarget = true`) | UGUI `Slider.OnPointerDown` maps the click x onto the handle container (the 235-unit Track) and clamps it. The whole 700x56 row is the raycast target. A mouse click on the label "Ana Ses" sets the slider to 0% and a click on the "%70" value text sets it to 100%. The change applies live and persists, so a mouse user who clicks the label to "select the row" mutes the game. This is a Fitts-friendly target with a destructive result, and it breaks P3 ("click track"). | Override `OnPointerDown`/`OnDrag` in `MenuSlider` to ignore presses outside the track rect (padded about 12 units), or move the raycast target to the Track and leave the row Image non-raycast. Keep hover-to-focus on the row. |
| A-2 | High for §14 sign-off (2 FAIL criteria) | `MainMenuBuilder.cs:599-601` (`Label()`: autosize min 24, `overflowMode = Overflow`, `NoWrap`); label widths `:177` and `:195` (250 units, ending at x=298) | The spec §13 says Settings labels shrink-to-fit and then truncate with an ellipsis. The code can only shrink to the 24 px floor and then spills over the neighbouring control. At the spec's own 26-character budget (about 310-330 units at 24 px in Liberation Sans) the label reaches the slider track (x=325) and the cycle `<` arrow (x=304). Today's Turkish strings fit, so players do not see it yet. A +40% pseudo-localisation fails. | Set `overflowMode = Ellipsis` on Settings labels (the 24 px floor stays), or widen the label column. Keep the 24 px floor. |
| A-3 | Process blocker | `production/qa/evidence/` has one file | `.claude/docs/coding-standards.md` makes a retained screenshot of every UI screen touched a BLOCKING gate. Only the root exists. Settings, both dialogs, loading, error and reduce-motion are missing. The note says simulated input does not advance in a background editor. | Capture with a foreground editor, or add an editor-only debug hook that forces a `MainMenuState` for capture. Required before the story closes. |
| A-4 | Medium | `InputSystem_Actions.inputactions:841` (`*/{Submit}`); `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Devices/Keyboard.cs:54-55` | The spec (§7.2, P1) says Enter / Space / A / left click confirm. In the Input System, only `enter` carries the `Submit` usage; `space` has none. Space therefore does nothing in this menu. Enter, A and click work. | Add `<Keyboard>/space` to UI/Submit. This touches a shared asset, so confirm that the gameplay maps do not conflict. Or amend the spec to Enter-only. |
| A-5 | Medium (silent failure) | `SettingsPanelView.cs:132-136`; `MainMenuStateMachine.cs:98` | `OpenRevertPrompt` returns false if `InputLocked` (an 80 ms press-lock is pending) or the state is not Settings. `Preview()` then silently calls `RevertDisplay()`. The player's resolution or window change is discarded with no prompt and no feedback. Rare, but it is a lost input. | Queue the display request until the lock clears, or reject the change visibly (revert the row text and play the Error cue). |
| A-6 | Medium (design risk; the spec chose it, so your call) | `SettingsPanelView.cs:120-135` | The spec says every resolution or window change triggers the 10 s prompt. Cycling through N resolutions means N real display switches and N prompts, with the default button being "Geri al". To go from 1280x720 to 3840x2160 the player confirms every intermediate mode. | See decision D-1 below. |
| A-7 | Low | `HoldAccelerator.cs:14-28`; `MenuSlider.cs:40-43` | The 0.5 s reset gap means quick separate taps also build up. Seven fast taps about 0.15 s apart reach x2 or more, so a tap can jump 10-20%. Motor-impaired players who tap rather than hold may overshoot. | Only accelerate when the interval is at most `moveRepeatRate` (0.1 s), which is true OS or stick repeat, not discrete taps. |
| A-8 | Low | `MenuFocusVisual.cs:123` | `SetSizeWithCurrentAnchors` on the underline runs every frame while focused. It is allocation-free, but it may dirty the layout and re-batch the root canvas each frame. | Update only when `label.preferredWidth` changes. Profile before acting. |
| A-9 | Low | `MenuFocusVisual.cs:81`; `MainMenuBuilder.cs:658-659` | The "pressed shadow" uses `UnityEngine.UI.Shadow` on a TMP label. TMP does not honour `IMeshModifier` effects, so the crimson 3 px shadow from the visual spec probably never renders. The 3 px text offset, sigil flip and underline swap still work. | Use a TMP underlay or outline on the material for the 80 ms press. This is an art-director call. |
| A-10 | Low | `StringTable.cs:67-68,79-80` | The labels differ from the §5.1 wireframe: "Ses Efektleri" vs "Efektler"; "Arayüz Sesi" vs "Arayüz Sesleri". Cycle values are "AÇIK/KAPALI" (upper case) but "Tam Ekran/Çerçevesiz/Pencere" are title case. The dialog buttons mix "EVET/HAYIR" with "Onayla/Geri al". The Turkish is correct, but the case convention is inconsistent. | Pick one rule for values and dialog buttons, then update the spec wireframe. |
| A-11 | Info | `MenuFocusVisual.cs:45-49`; `MainMenuController.cs:127-151` | The Focus tick plays on every programmatic focus (on every state change, and together with the DialogOpen cue). Harmless, but it is two cues at once. | Suppress the Focus cue on programmatic selection. |

### A2. Flow and input checks

| Check | Result | Evidence |
|---|---|---|
| Nav wrap, root (Up/Down, W/S, D-pad, stick) | PASS-BY-CODE | `MainMenuBuilder.cs:664-676` `Wrap()` builds explicit modular Up/Down. Left/Right are unset on buttons, so they do nothing. The default UI/Navigate asset has WASD, arrows, D-pad and stick. |
| Nav wrap, Settings (9 rows) and dialogs | PASS-BY-CODE | `Wrap(order)` over 9 items in spec order (`:290-291`). Dialogs use a horizontal wrap so Left/Right and Up/Down both work (`:216`). |
| Tab / Shift+Tab, same order, wraps | PASS-BY-CODE | `MenuInputRouter.cs:43-48` reads `Keyboard.tabKey` and walks `selectOnDown/Up`. It is gated by `InputLocked`, so Tab is dead during fades and presses. |
| Esc / B / right-click is Back | PASS-BY-CODE | `Cancel` is bound to `*/{Cancel}` (Escape and gamepad B carry that usage). Right-click is PassThrough, and the router filters the release with `ReadValueAsButton()` (`MenuInputRouter.cs:28-32`). |
| Root Back is a silent no-op | PASS-BY-CODE | `MainMenuStateMachine.Back()` returns false at root, so there is no audio and no state change. `RootBackIsSilentNoOp` test exists. |
| Dialog Back = HAYIR; revert-prompt Back = revert | PASS-BY-CODE | `Pending.Back` branch in `Tick`. The Settings, QuitConfirm and RevertPrompt cases are handled separately. |
| Focus trap in dialogs | PASS-BY-CODE | `FindActive` matches only the two dialog buttons. Any other selection is repaired to `DefaultFocus` each frame (`MenuInputRouter.cs:37-42`). The dim overlay blocks raycasts (`MainMenuController.cs:160`). |
| Focus restore to opener | PASS-BY-CODE | Settings returns to AYARLAR, QuitConfirm to ÇIKIŞ, and revert to the opening row (`FocusTarget` in `Tick`). |
| Revert prompt on every res/window change | PASS-BY-CODE, with A-5 | `ModeChanged` and `ResolutionChanged` both call `Preview()`. `SettingsServiceTests.EveryDisplayRequestBecomesPendingAndRevertNotifies` exists. |
| 10 s default revert | PASS-BY-CODE | `RevertCountdown` (10 s, expiry means revert). The deadline wins over a pending Keep (`DeadlineWinsOverUnfinishedKeepPress`). Default focus is "Geri al". |
| Input lock at first load | PASS-BY-CODE | `InputLocked` includes the `FirstLoad` state and `fader.IsFading`. `sendNavigationEvents` is false, and the fader overlay blocks pointers. |
| Input lock during loading | PASS-BY-CODE | The `Loading` state is locked, including Back (`OnBack` is gated). The overlay blocks pointers. A re-trigger is rejected by `Queue()`. |
| Load error path | PASS-BY-CODE | `MenuLifecycle.Fail` leads to `LoadFailed`, then Root with focus on BAŞLA, a fade revert and the message "Seviye yüklenemedi". |
| Mouse hover = focus (single focus) | PASS-BY-CODE | `OnPointerEnter` sets `eventData.selectedObject`. The EventSystem has one selection, and `deselectOnBackgroundClick = false`. |
| Gamepad disconnect | PASS-BY-CODE | No code references `Gamepad.current` or shows a modal on disconnect. |
| Right-click on sliders and cycle rows | PASS-BY-CODE | Only the left button drags or cycles. |
| Scroll wheel on sliders | PASS-BY-CODE | `Slider` has no scroll handler, and there is no ScrollRect. |
| Cycle-row Enter / click / arrows | PASS-BY-CODE | Enter and row click go forward only. The `<` arrow and Left go back (`MenuCycleRow.cs`). The arrows are 56 units, which is at least 44 px at 1080p. |

### A3. §14 acceptance criteria (33 items)

| # | Criterion (short) | Status | Note |
|---|---|---|---|
| 1 | First frame within 200 ms | UNVERIFIED | Needs a timing run. |
| 2 | Interactive within 500 ms plus fade | UNVERIFIED | By code the menu unlocks when the 0.6 s fade ends. |
| 3 | Framerate holds with ambient motion | UNVERIFIED | Needs a profiler. |
| 4 | Zero GC per frame | PASS-BY-CODE | Countdown, percent and version strings are pre-built. No concat in `Update`. See A-8; the profiler is unverified. |
| 5 | Correct at 1280x720 and 3840x2160 | UNVERIFIED | Root at 720p looks right in the screenshot. Settings and dialogs at 720p, and all states at 4K, are not seen. Settings panel arithmetic fits at 720p (top margin about 94 px). |
| 6 | 16:9, 16:10 and 21:9; no seams at 21:9 | UNVERIFIED | Anchoring looks sound by code (Match = 1). A fog seam is already visible; see B-1. |
| 7 | 4:3 best-effort | UNVERIFIED | |
| 8 | No Turkish overflow within max-char bounds | FAIL | A-2. Settings labels at 26 characters overflow. |
| 9 | No overflow with +40% pseudo-loc | FAIL | A-2. Same cause. |
| 10 | All §6 states render correctly | UNVERIFIED | Only the root has a screenshot (A-3). |
| 11 | Keyboard-only reachable | PASS-BY-CODE | Space is a spec deviation (A-4); Enter works. |
| 12 | Gamepad-only reachable | PASS-BY-CODE | |
| 13 | Mouse-only reachable | PASS-BY-CODE | See A-1 for the label-click hazard. |
| 14 | BAŞLA focused on load, no click | PASS-BY-CODE | The screenshot shows BAŞLA focused. "No click" is not provable from a still. |
| 15 | Esc/B/right-click on root does nothing | PASS-BY-CODE | |
| 16 | ÇIKIŞ dialog flow | PASS-BY-CODE | HAYIR default, Esc/B closes, EVET quits (the editor stops play mode), and Tab is trapped. |
| 17 | Focus visible and distinguishable in greyscale | PASS-BY-CODE | The screenshot shows a bone-outlined sigil plus an underline plus Bold. No greyscale capture and no Settings or dialog capture. |
| 18 | Cursor visible and unlocked; `timeScale == 1` | PASS-BY-CODE | `MainMenuController.Awake:62-64`. |
| 19 | Loading locks input; no double-trigger | PASS-BY-CODE | `StartFiresOnceAndLoadingRejectsAllActions`. |
| 20 | Single focus | PASS-BY-CODE | |
| 21 | Start fires once with `TestLevel_Gauntlet`, loads after the 0.6 s fade | PASS-BY-CODE | `MenuLifecycle` holds `allowSceneActivation` until `FadeOutComplete`. |
| 22 | Settings apply immediately and persist | PASS-BY-CODE | `SettingsServiceTests`. |
| 23 | Every res/window change prompts; 10 s revert; Keep persists | PASS-BY-CODE | See A-5. |
| 24 | No direct Quit / AudioSource / PlayerPrefs in UI | PASS-BY-CODE | `Application.Quit` lives in `MenuLifecycle.cs:66` and `PlayOneShot` in `MenuAudioPlayer.cs:33`. Both are event consumers, but they sit in `Views/`. A folder move would make the rule easy to audit. |
| 25 | Reopened Settings shows persisted values | PASS-BY-CODE | `SettingsPanelView.Open()` calls `Refresh`. |
| 26 | Contrast 4.5:1 / 3:1 | PASS-BY-CODE | Token math is in section C. There is no in-engine sampling, "animated and static" is not done, and only the root has been looked at. |
| 27 | Text at least 24 px at 1080p, proportional | PASS-BY-CODE | See section C. |
| 28 | No info by colour alone | PASS-BY-CODE | |
| 29 | No info by sound alone | PASS-BY-CODE | |
| 30 | Reduce-motion coverage | PASS-BY-CODE | See section C. |
| 31 | No flashing | PASS-BY-CODE | Torch alpha 0.85-1.0 at 1.5 Hz or less; one fade. |
| 32 | All text from string keys | PASS-BY-CODE | The only literals are the `<` and `>` glyphs (`MainMenuBuilder.cs:196-197`), which are not language text. |
| 33 | Turkish glyphs render; İ/ı casing | UNVERIFIED | Ş and Ç are visible in the screenshot (BAŞLA, ÇIKIŞ). İ (GERİ), Ö, Ü, Ğ in Settings were not seen. Casing is correct by code: strings are pre-cased. |

Counts: PASS 0, PASS-BY-CODE 23, FAIL 2, UNVERIFIED 8 (total 33).

---

## Stream B: art-director stand-in (consistency with Direction A)

Scope: visual consistency only. I make no new style decisions.

| ID | Severity | Finding |
|---|---|---|
| B-1 | Medium | In the screenshot the right-hand fog band shows a hard-edged vertical seam, with a lighter rectangular block at about x=1200, y=440-690 (1280x720 capture). Cause (by code): `ArtFog` is a 128x32 Perlin texture (`MainMenuPlaceholderArt.cs:47-51`) that is not tileable. It is stretched about 15 times across the whole width as one tile, and `uvRect.x` scrolls with Repeat wrap (`MenuAmbientMotion.cs:35-37`). So the wrap seam travels across the screen. It also reads as blocky rectangles from the bilinear magnification. This puts the "no art seams" acceptance at risk, worst at 21:9. Fix options: a seamless noise, or mirrored tiles. |
| B-2 | Low (expected, per spec §4.7 step 5) | The typography is a placeholder. Title and UI use `LiberationSans SDF` (`MainMenuBuilder.cs:46-47,142-143`) rather than Grenze Gotisch and Alegreya Sans SC. The screenshot title reads as generic bold sans, so the "inscription carved in bone" tone is carried only by the crimson rule and sigil. Sizes (132/44/56/28/24/32/24), tracking and colours already match Direction A. Menu items use Regular, not the specified Medium. The spec's own open item (verify OFL fonts for Turkish) is still open. |
| B-3 | Low | The panel jewel reuses `Sigil_A_BoneSeal` (a 9x11 sprite) stretched into a 45x21 rectangle (`MainMenuBuilder.cs:256-257`). The pixel aspect is distorted. The spec asks for a separate 15x7 jewel. |
| B-4 | Low | Pressed-state accent shadow probably does not render on TMP (A-9). |
| B-5 | Info | The screenshot shows the embers as a flat dashed line at the torch base. In the screenshot-capture editor, time did not advance, so spread and rise cannot be judged. Ember/smoke placement was checked by code only. |

Verified consistent with Direction A:
- All 13 colour tokens in `MenuTheme_KanVeKemik.asset` equal the §1.1 hex values (text, text-dim, focus, accent, danger, panel, bg, W-bg, row band `#1E1718`, track `#2A2020`).
- Sigil: bone outline, accent fill, soot slit, 27x33 at 1080p. It is visible in the screenshot, 16 units left of the text.
- Focus underline: bone ends and an accent core.
- Left scrim: alpha 0.92 to 50%, fading to 0 by 62% (`MainMenuPlaceholderArt.cs:41-46`).
- Layer order: scrim canvas is sorted above embers and smoke.
- Ember emitter: stays at about 69-81% of canvas width, outside the 0-48% region. At 21:9 it is at about 69%.
- Particle caps: embers at most 64, smoke at most 8.
- Dialog: heavy frame with an accent hairline, dim at 0.65.
- Panel fill: alpha 0.94.
- Ornaments: Point filter, no mips, uncompressed, and scaling is a whole number at 720p, 1080p, 1440p and 2160p.

Not verifiable (no screenshot): Settings panel, band, slider, dialogs, and the greyscale focus test in the visual-design §5 additions.

---

## Stream C: accessibility-specialist stand-in (Standard tier)

### C1. Contrast (WCAG 2.x, token pairs)

| Pair | Ratio | Needs | Result |
|---|---|---|---|
| Title / menu text `#E8DCC0` on bg `#0C0A0A` | 14.51 (recomputed) | 4.5 | Pass |
| Text on W-bg `#201818` | 12.80 | 4.5 | Pass |
| Focus `#FFF4DC` on bg | 18.08 | 4.5 | Pass |
| text-dim `#A09684` on panel `#140F10` | 6.50 (recomputed) | 4.5 | Pass |
| text-dim on W-bg (version tag, group labels) | 5.97 | 4.5 | Pass |
| danger `#FF4D5E` on panel / W-bg | 5.86 / 5.37 | 4.5 | Pass (countdown at 3 s or less, error text) |
| accent `#DB143D` on bg (sigil, underline, title rule) | 3.93 | 3.0 non-text | Pass. The code never uses accent as text (builder `:237`, `:654`, `:659`). |
| Slider fill text-dim on track `#2A2020` | about 5.3 (computed) | 3.0 | Pass |
| Slider fill accent (focused) on track | about 3.15 (computed) | 3.0 | Pass, but only just |

Margins: the focused slider fill at about 3.15 leaves little headroom. A change of the accent or track token could fail it. Only the root has any screenshot, and no in-engine pixel sampling was done.

### C2. Text size at 1080p (floor: 24 px)

| Element | Size (1080p) | Enforced floor | Result |
|---|---|---|---|
| Menu items | 44 | autosize min 24 (`Label()` `:599`) | Pass |
| Settings labels, values, Back | 28 | 24 | Pass |
| Group labels | 24 | 24 | Pass (at the floor) |
| Dialog prompt, buttons | 32 | 24 | Pass |
| Revert countdown | 40 | 24 | Pass |
| Version tag | 24 | 24 | Pass |
| Title | 132 | 56 (title min) | Pass |
| Scaling | Canvas Scaler 1920x1080, Match = 1 (`MainMenuBuilder.cs:564`) | proportional | Pass |

Note: at the 1280x720 minimum (scale 0.667) the 24 px floor renders as 16 physical px. That follows the spec ("24 px at 1080p, scale proportionally"). It does mean a 720p player gets small text, and UI scaling (deferred) is the intended remedy. See carried gap 1.

### C3. Focus and colour independence

- Focus is never colour-only. It uses the sigil, the underline, the Bold weight, the brighter `focus` colour, the scale (when reduce-motion is off) and the settings row band. The screenshot confirms sigil, underline and Bold at root.
- Slider value: shown as text "%N". The cycle value is shown as text. The countdown is a number in text, and danger colour is only a second cue.
- Sound-only information: none. Every cue has a visual twin (Error sound plus the "Seviye yüklenemedi" message).

### C4. Reduce-motion coverage

| Item | Behaviour with reduce-motion on | Source |
|---|---|---|
| Embers and smoke | Prewarmed, then `Pause()` (a static populated still) | `MenuAmbientMotion.cs:24-30` |
| Fog scroll, torch flicker | Stopped; glow fixed at 0.92 | `MenuAmbientMotion.cs:29,33` |
| Fades (first-load, load, error) | At most 0.1 s | `MenuTween.ResolveDuration` isFade branch |
| Panel and dialog crossfade | Instant (0 s) | `MainMenuController.RenderState:141-143` |
| Focus scale | None (1.0), and any in-progress scale is snapped | `MenuFocusVisual.cs:94,112-116` |
| Cycle-arrow nudge | Off | `MenuCycleRow.cs:82` |
| Applies at first load | Yes (`GameSettings.Current.ReduceMotion` at `Awake`) | `MainMenuController.cs:66` |
| Applies live on toggle | Yes (`GameSettings.Changed`, and `panelTween` is completed at once) | `MenuAmbientMotion.cs:16`, `MainMenuController.cs:109` |

Residual: the 3 px text offset on press (80 ms) remains with reduce-motion on. I judge it acceptable (it is feedback, not travel).

### C5. Other accessibility observations

- The revert prompt is a 10 s timed input that cannot be extended. The tier definition (`design/accessibility-requirements.md:28`) says "no un-extendable timed input". The requirements table (line 108) accepts a 10 s prompt as OK because expiry reverts safely. I read that as a documented exception. A "+10 s" or pause-while-focused option would close it. See decision D-2.
- Space does not confirm (A-4). Enter, A and click do.
- Pointer-down activation on sliders, plus the label-click hazard (A-1), is a motor-accessibility risk (WCAG 2.5.2).
- Hit targets: menu buttons 68 units (about 45 px at 720p), rows 56 units (37 px at 720p). The spec's 44 px target is stated at 1080p, where all pass.

### C6. Carried gaps (user-approved, not new findings)

1. UI scaling (75-150%): deferred by the user on 2026-11-12. No Settings row yet.
2. Input remapping: lives in a future KONTROLLER screen with its own spec.
3. Brightness/gamma and the flash warning: deferred.

### C7. Verdict

Compliant with the Standard tier for everything this screen implements (contrast, text size, focus, colour independence, reduce-motion, keyboard and gamepad completeness), except for the three carried gaps. The new items above (A-1, A-2, A-4, C5) are defects or advisories, not tier failures.

Accessibility: GAPS (carried: UI scaling, input remapping in future KONTROLLER screen, brightness/gamma and flash warning)

---

## Decisions for the user

**D-1. Revert-prompt cadence for display changes (A-6).** The spec makes every resolution or window change open a 10 s prompt. That is safest, but it makes a multi-step change laborious and flickers the real display at each step.
- Keep per-change prompt (Recommended): matches the spec and P4. Safe, but multi-step changes cost N prompts.
- Stage then apply: cycling only changes the displayed value; one explicit apply control triggers a single prompt. Fewer prompts, but it needs a new row and a spec change.
- Prompt only for risky changes: fewer prompts, but the spec forbids risk filtering.

**D-2. Timing adjustability of the revert countdown (C5).**
- Accept as documented exception (Recommended): expiry reverts safely, and the accessibility doc already marks it OK.
- Add "+10 s" / pause-while-focused: removes the un-extendable timer, at the cost of a new control.

**D-3. Space as confirm (A-4).**
- Add Space to UI/Submit (Recommended): matches the spec and P1, but edits a shared input asset.
- Amend spec to Enter / A / click: no asset change; keyboard users lose a common key.

## Open items before sign-off
1. Fix A-1 (slider label-click) and A-2 (label overflow).
2. Capture the missing states (A-3), including greyscale focus and reduce-motion first-load.
3. Fix or replace the fog texture (B-1) and re-check at 21:9.
4. In-engine contrast sampling, plus a 4K and 21:9 pass.
5. Fonts (B-2): swap in the OFL fonts after the glyph test string passes.
