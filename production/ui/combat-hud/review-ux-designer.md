# Phase 4 Review: PURGATOR Combat HUD (ux-designer, three streams)

> Reviewer: ux-designer, covering (A) UX, (B) art-director stand-in, (C) accessibility-specialist stand-in.
> Date: 2026-11-12. Scope: `design/ux/hud.md` §3, §4, §5, §8, §10, §12; `production/ui/combat-hud/visual-design.md`; `design/accessibility-requirements.md` (Standard).
> Evidence read: `production/qa/evidence/combat-hud-01..06`, `main-menu-01-root.png` (for consistency), `Scripts/UI/HUD/Views/*.cs`, `Scripts/Editor/CombatHudBuilder.cs`, `HudBannerQueue.cs`, `HealthDisplayModel.cs`, `PlayerLook/PlayerHealth/WeaponController` (cursor), `PlayerDamageFeedback` (vignette), `PlayerDebugHUD` (overlap).
> Stance: written to find failures. Anything not seen in a capture or proven by a test is marked as such.

## 0. Verdict

NOT READY TO SIGN OFF. One blocking visual defect on retained evidence, one blocking evidence gap against the Standard-tier contrast requirement. The rest is major or minor and can be scheduled.

---

## 1. Blocking findings

### B1. Health number is blank in `combat-hud-02-encounter.png` (UX / AC "player still sees health")
- Screenshot 02: BL plate shows the cross and a full bar, but the `100 / 100` number is missing. Screenshots 01, 03, 05 and 06 show it. The only difference in 02 is that an encounter has started.
- Health is the MUST KEEP element (§6). A blank number during combat is the worst place for it to disappear. The bar, border and chip carry some of it, but the spec says "number always present" (§4.2).
- Root cause is not confirmed from the code. Two candidates to check:
  1. `HealthHudView.Render` writes with `HudText.Template` -> `TMP_Text.SetCharArray` (HealthHudView.cs:34). `HudText.Prewarm` restores `label.text = original` (HudBindings.cs:213-217). If `.text` does not reflect text set through `SetCharArray`, any later Prewarm or `ForceMeshUpdate` round trip over this label blanks it, and `Render` will not rewrite it because `model.NumbersChanged` is false until the value changes. This also explains why it is intermittent across captures.
  2. `HudBindings.OnEnable` re-runs `Snapshot()` (HudBindings.cs:50), and `Start` toggles every node active/inactive for the warm-up pass (HudBindings.cs:67-78). A re-enable during the encounter path would re-enter these without re-writing the numbers.
- Required: reproduce (start encounter from a fresh Play Mode, capture 10 frames), add a Play Mode or EditMode check that `currentText.text`/`GetParsedText()` is non-empty after Initialize -> Snapshot -> encounter start, and re-capture 02. The 240/240 EditMode result does not cover this, because it exercises models, not TMP state.
- The same `SetCharArray` pattern is used for ammo (WeaponAmmoHudView.cs:68), dash count, wave, remaining and rooms. Capture each of them after an encounter start as part of the fix verification.

### B2. Contrast and greyscale AC have no measurement evidence, and two shape/contrast cues look weak in captures (Accessibility)
- §12 requires contrast >= 4.5:1 "over the lightest and darkest scenes" and a greyscale test. The visual-design numbers are computed, not measured (visual-design.md §2.3 says "re-measure in-engine"). No colour-picker sample or greyscale capture is in the evidence folder.
- Looking at the captures, the opaque danger chips (soot text on `#FF4D5E`, computed 6.09:1) do not read like 6:1:
  - `combat-hud-06-araf.png`: the Araf warning text on the salmon chip is muted, and the `!` shape cue at the chip's left edge is a hairline mark, close to invisible (Exclamation label is 12 px wide, CombatHudBuilder.cs:321).
  - `combat-hud-03-low-health.png`: the `!` chip next to `20` is a small dark mark on red, with the red wash over it.
- The `!` is the non-colour backup for low health, low ammo and the Araf warning (§10.1). If it does not read, the colour-only audit row regresses.
- Required: sample foreground and background pixels from 02, 03, 06 (and a captured low/empty ammo state, see §6) and record ratios; add a greyscale conversion of 03 and 06. If the chip text is rendering below 4.5:1, fix the colour or weight (probable cause: thin Bold at 20 px with anti-aliasing against a saturated fill; enlarge `!` to the 28 px used on the health chip).

---

## 2. Stream A: UX (implementation vs `design/ux/hud.md`)

### 2.1 Major
- A1. Death card has no arming delay (TrueDeathView.cs:107-124). Confirm is armed as soon as the button is selected, and the poll reads `Space`, `Enter` and gamepad A directly. Space is jump in this game and the card is shown while the player is mid-fight, so a mashed jump or a held key can restart the run before the 400 ms fade has finished and before the player has read "GERÇEK ÖLÜM". Spec §4.2 only asks for an 80 ms lock after confirm, which does not cover this. Recommend: ignore submit until the fade has completed and all submit inputs have been released once (or a fixed ~500 ms arm time, 0 under Reduce Motion is still safe because it only gates input, not visuals).
- A2. Cursor ownership on non-true deaths. `PlayerHealth.Die` unlocks the cursor and disables `PlayerLook`, `WeaponController`, `PlayerParryController` on every death, including the Araf transition (PlayerHealth.cs:153-167). The HUD shows no card there (verified), but the cursor becomes visible for the fade. UNVERIFIED whether the revive path re-locks it; if not, look input is dead until the player clicks (PlayerLook.cs:194 is the only fallback). Outside HUD ownership; hand to the player/run owner and verify in Play Mode.
- A3. Banner timing deviates from the likely intent when events are > 500 ms apart. Sealed then Wave merge only inside 500 ms (HudBannerQueue.cs:156-157). Otherwise Wave is held as Pending behind the 2.25 s Sealed banner (equal priority, not near expiry), so "DALGA 1" can appear about 2 s after enemies have started spawning. Capture 02 shows a lone `DALGA 1` and no `ARENA MÜHÜRLENDİ`, so the in-engine merge was never observed. The queue logic is unit-tested; the timing against the real `WaveSpawnDirector` delay is not.
- A4. ARENA CLEARED has no persistent backup. The wave block fades 300 ms after `OnEncounterCompleted`, and the banner holds 2.2 s. After that the player has no on-screen statement that the arena is clear. Acceptable as design if audio/barrier visuals carry it; flag for the game-designer.

### 2.2 Minor
- A5. 96 px dead gap between the cross and the health number whenever health is not low (screenshots 01, 05, 06). It is the reserved cell for the low-health chip (no layout shift, a sound choice), but the number reads as detached from its sigil. Consider placing the number at the sigil and letting the chip push it, or moving the chip to the right of the number.
- A6. With the buckler disabled (default), the BL ability row has a 128 px hole between dash and hook (screenshot 05). Parry visuals (SAVUŞTUR, HAZIR/AKTİF/BEKLE, flash, `SAVUŞTURULDU`) are therefore never visible in any capture.
- A7. `KALAN 8` sits directly on top of its 6 px bar (screenshot 02: text baseline touches the bar). Move the bar 4 px down or the text 4 px up.
- A8. `KANCA` label ends about 7 px inside the plate edge (screenshot 05). It uses `Overflow` (CombatHudBuilder.cs:255), so any longer string, the deferred 150% scale or a font swap pushes it over the plate edge rather than ellipsizing.
- A9. Rich-text templates hardcode sizes (`<size=20>`, `<size=24>`; HealthHudView.cs:34, WeaponAmmoHudView.cs:68). §11 says nothing hardcoded in HUD scripts; also bypasses the theme/`minimumTextSize`. Move to `HudTheme`.
- A10. Overheal is `+n` text only; the hatched overflow segment of §4.2/§10.1 is not built (the `hatch` sprite is assigned in the theme but no Image uses it). §4.3 allows the text-only fallback when the cap varies, and `HealthDisplayModel` states no cap is assumed, so this is an accepted deviation that must be written into the spec and AC (see §12 row 8).
- A11. Evidence gaps (cannot be reviewed from screenshots): low ammo, empty ammo (`BOŞ`), melee (`YAKIN`), parry in any state, hook on cooldown, spent dash pips / recharge fill, overheal, `ÇIKIŞ BULUNDU`, banners ARENA SEALED / WAVE CLEARED / ARENA CLEARED, F2 debug on. These are PASS-BY-CODE at best.

### 2.3 Confirmed good (not just assumed)
- Corners and insets from captures: BL, BR and TR plates sit at 96 px left/right and 53-56 px top/bottom, i.e. 5% inset at 1080p.
- Centre 40x40 zone is clear in all HUD captures; banner band is at 12-17% height.
- Measured plate areas from captures (rect sizes include 3 px border): exploration about 5.3%, combat about 6.7% (8.4% with the banner), Araf about 7.2%. All inside §7 budgets.
- True death card (04): title, one button, focus seal + underline + bone border, gameplay HUD and crosshair hidden, cursor unlock in code (TrueDeathView.cs:66). No card on the Araf transition (given as verified).
- Hook slot appears only with the shotgun and keeps its cell (05, `alpha` toggle, AbilityHudView.cs:123).

---

## 3. Stream B: art consistency (stand-in for art-director)

### Consistent with Kan ve Kemik and the main menu
- Palette: bone text, soot plate at 0.90 (theme asset and builder both use `config.PlateAlpha`), crimson bar fill, `#7A6E5E`-style 3 px plate border. Matches visual-design §2.1.
- Death button reuses the menu seal sprite and the red underline as in `main-menu-01-root.png`; title and button typography match the menu placeholder font (both on the LiberationSans placeholder, which is a carried placeholder, not a new gap).
- Type colour is identity only: cyan and amber appear on the glyph and bar, never on text. Ammo count stays bone.
- Ornament level is low, plates are small, nothing in the centre.

### Findings
- B-1 (major). The Araf warning chip is the loudest object on the screen (06): a large solid salmon block in the TR corner, held for the whole Araf phase. In this HUD the opaque danger chip otherwise means "critical resource state" (low health, low or empty ammo). Using the same signal for a standing informational objective dilutes it and fights the "lighter relative of the menu" rule. Recommend a bone/`focus` plate treatment for the Araf objective and keep `danger` chips for transient critical states. Art-director decision.
- B-2 (major). In low health (03) the `PlayerDamageFeedback` full-screen red wash is drawn over the UGUI canvas (OnGUI renders above Screen Space Overlay). Result: plates, text and type colours are all tinted red at the moment the HUD matters most, the cyan ammo bar goes teal-grey and the amber shell bar would go brown. The wash is not a flat screen-edge vignette in the capture; it reads as a full-screen tint. Needs art direction (reduce interior alpha, or render the wash under the HUD, i.e. a canvas image at a lower sorting order).
- B-3 (minor). `PlayerDamageFeedback` heartbeat wash (Time.time * 6.5 rad/s, about 1.03 Hz) is a second red pulse in addition to the border pulse; together with `lowHealthPulseSpeed` being a separate field, the two can drift out of phase. Slave both to `config.LowPulseHz`.
- B-4 (minor). Text weight: 20 px labels render with cap height about 12 px at 1080p (ATILMA, ARAF: HAZIR in 01), so the nominal 20 px is visually small beside the 44/48 px numbers. Legible at 1080p, marginal at the 1280x720 floor (about 13 px nominal). Advisory.
- B-5 (out of scope, noted). `main-menu-01-root.png` shows a missing-glyph box in `v0.1.0` (the `0`). Not HUD.

---

## 4. Stream C: accessibility vs committed Standard tier

Carried (not new): UI scaling 75-150% (`HudScale` exists in config and `HudLayout`, no setting exposed); colourblind palette values; remapping screen; brightness/gamma; flash/strobe warning and VFX audit. Also still open and already listed: pause menu.

### New or still-open items in the HUD scope
| # | Standard-tier item | Finding | Status |
|---|---|---|---|
| C1 | Text contrast >= 4.5:1 (UI text) | Computed only, never measured; chip text/`!` legibility doubtful in 03 and 06 (see B2) | GAP (blocking evidence) |
| C2 | Colour-as-only-indicator | By code: low health `!` + border + number + tick; ammo name + glyph + `BOŞ`; hook/parry state words; dash `n/N` + hollow/filled; Araf state text + sigil fill. Depends on the `!` actually being visible (B2). No greyscale capture | PASS-BY-CODE, unverified in greyscale |
| C3 | Min HUD text 20 px | All HUD labels 20 px min by builder and `minimumTextSize` 20; ammo and health larger; `<size=20>` literals equal the floor but are hardcoded (A9) | PASS-BY-CODE |
| C4 | Motion reduction | HUD views honour `GameSettings.Changed`: border pulse, ammo pulse, damage edge, dash brighten, parry flash, banner fades, death fade, wave fade, room flash all gated. NOT honoured by `PlayerDamageFeedback`: no `ReduceMotion` reference anywhere in `Scripts/Player`, so the heartbeat vignette pulse and screen wash keep running with Reduce Motion on | GAP (new): HUD PASS, low-health feedback not covered. Accessibility doc row stays "In Progress" |
| C5 | Reading time for auto-dismissing UI | `design/accessibility-requirements.md` states "No other auto-dismissing info." That is no longer true: banners hold 1.8 s (ARENA CLEARED 2.2 s) and the parry word 250 ms. Mitigated by the persistent wave counter and audio, but ARENA CLEARED has no persistent equivalent (A4) | GAP (new): update the doc row and record the mitigation or lengthen the hold |
| C6 | Keyboard-only | Death card: Enter/Space. All other HUD is display-only | PASS-BY-CODE (not driven, per given constraints) |
| C7 | Gamepad-only | Death card: gamepad A via `Gamepad.buttonSouth` | PASS-BY-CODE |
| C8 | Remapping interaction | Death-card confirm reads `Keyboard.enterKey`, `spaceKey`, `buttonSouth` directly (TrueDeathView.cs:120-121) next to the UI Submit action. Harmless today (UI submit is not remappable), but when the remapping screen ships these literals must go through the UI `Submit` action or be excluded from rebinding | Note against the carried remapping gap |
| C9 | No flashing without warning | HUD pulses are 1 Hz; vignette about 1.03 Hz; no HUD element exceeds 3 flashes/s. Photosensitivity notice itself is the carried item | PASS (HUD), warning carried |
| C10 | Subtitles | N/A (no voiced content) | N/A |
| C11 | Scales at supported resolutions | CanvasScaler 1920x1080, match height; `HudLayout` intersects `Screen.safeArea` and a 5% inset. Only 1080p captured. Floor 1280x720 gives about 13 px text (B-4) | UNVERIFIED beyond 1080p |

### Checklist result (the seven mandatory items)
- Keyboard only: yes (by code). Gamepad only: yes (by code).
- Text readable at minimum size: yes at 1080p (nominal 20 px), unmeasured contrast (C1).
- Not colour alone: yes by code, `!` visibility to be proven (B2).
- No flashing without warning: HUD yes; photosensitivity notice carried.
- Subtitles: N/A. UI scales: 1080p only (UI scaling setting carried).

---

## 5. §12 Acceptance Criteria, row by row

Legend: PASS = screenshot or test evidence. PASS-BY-CODE = code reading, not observed. FAIL. UNVERIFIED.

### Layout and visibility
| # | Criterion | Result | Basis |
|---|---|---|---|
| 1 | All elements inside the 5% inset at 1280x720, 1080p, 4K, 16:10, 21:9 | UNVERIFIED | 1080p insets confirmed from captures (96 px side, 53-56 px top/bottom). `HudLayout` logic is sound, but no other resolution was captured |
| 2 | Centre 40x40 contains no UGUI HUD element in any context | PASS | Captures 01, 02, 03, 05, 06; banner at 12-17% height. The death card is a full-screen overlay (§3.2 exempts it) |
| 3 | Plate area <= 6% exploration, <= 10% combat with banner; banner <= 600x60 / 600x96 | PASS | Measured from captures: 5.3% exploration, 8.4% combat with banner, 7.2% Araf; banner sizes from the theme asset (600x60, 600x96) |
| 4 | Hook only while shotgun active, no layout shift | PASS | Capture 05 vs 01; `hookCell.alpha`, fixed cell position |

### Per-context correctness
| # | Criterion | Result | Basis |
|---|---|---|---|
| 5 | With `PlayerDebugHUD` OFF the player still sees health, dash, parry, wave, banners, death card; F2 ON does not overlap | PASS-BY-CODE | `showHUD` set false in the builder; debug panel is x 20-410, y 20 to 0.7*H-40 (PlayerDebugHUD.cs:562), clear of BL/BR/TR at 16:9. Parry never visible in captures (A6). F2 ON not captured |
| 6 | ARENA SEALED / WAVE n / WAVE n CLEARED / ARENA CLEARED once, top-centre, priority and merge rules | PASS-BY-CODE | `HudBannerQueueTests` (98 matches); in-engine only `DALGA 1` seen (A3, A11) |
| 7 | Parry success: slot flash and `SAVUŞTURULDU`, no centre text | PASS-BY-CODE | AbilityHudView.cs:76-84; buckler off by default, not captured |
| 8 | Overheal: `+n` and hatched overflow; one-frame update | FAIL | No hatch is built (A10). `+n` text path and event-driven update exist. Non-blocking: §4.3 allows text-only, so amend the AC or build the segment |
| 9 | Low health triggers at exactly `lowHealthPercent`; vignette follows the same value | PASS | Capture 03 (20/100 shows `!` chip, thick border); `HudLowStateRules` tests; `PlayerDamageFeedback` and `PlayerDebugHUD` read the same `HudConfig` |
| 10 | `OnDied` with charge shows no card; `OnTrueDeath` shows title + button, cursor visible, button focused, gameplay HUD hidden | PASS | Capture 04 (card, focus seal, hidden HUD); capture 06 and the given verification (no card on Araf). Cursor by code |
| 11 | Enter, Space, gamepad A, click each raise exactly one restart request | PASS-BY-CODE | `TrueDeathGate` + tests; TEKRAR DENE triggers exactly one reload (given). Real input not driven. See A1 for the accidental-confirm hazard |
| 12 | Araf: HAZIR/KULLANILDI, `ODALAR r/t`, `ÇIKIŞ BULUNDU` match `RunEvents` payloads | PASS-BY-CODE | Capture 06 shows `ARAF: KULLANILDI`, `ODALAR 0/7`; exit line not captured |
| 13 | F3 block (seed/floor/timer) only in `PlayerDebugHUD` | PASS-BY-CODE | No seed/floor/timer in any HUD view |

### Accessibility
| # | Criterion | Result | Basis |
|---|---|---|---|
| 14 | Critical text >= 20 px at 1080p and contrast >= 4.5:1 over lightest and darkest scenes | UNVERIFIED | Size PASS-BY-CODE; contrast never measured (B2) |
| 15 | Greyscale test: low health, low/empty ammo, parry/hook/dash states, Araf used/ready distinguishable | UNVERIFIED | No greyscale capture; the low ammo, empty ammo, parry and hook-cooldown states were never captured |
| 16 | ReduceMotion ON: no HUD element pulses/flashes/fades/slides; OFF: pulses <= 1 Hz | PASS-BY-CODE | All view classes gate on `reduceMotion`; pulses use `LowPulseHz` (cap 1 Hz). Caveat C4: the OnGUI vignette heartbeat is outside the HUD views and ignores the setting |

### Performance and technical
| # | Criterion | Result | Basis |
|---|---|---|---|
| 17 | HUD scripts allocate 0 B/frame in combat; events unsubscribed on disable | PASS-BY-CODE | Idle 0 B over 120 frames (given); fixed char buffers, cached strings, dirty checks; `OnDisable` unsubscribes everything. Combat-state profile not provided |
| 18 | HUD prefab absent from MainMenu scene; all strings from StringTable | PASS-BY-CODE | HUD is nested in `Player.prefab`; no `CombatHud` in the menu scene search; copy keys via `LocalizedText`/`StringTable`. Template literals (`[`, `/`, `+`) and `<size>` tags remain in views (A9) |

Counts: PASS 5 (rows 2, 3, 4, 9, 10), PASS-BY-CODE 9 (5, 6, 7, 11, 12, 13, 16, 17, 18), FAIL 1 (row 8), UNVERIFIED 3 (1, 14, 15). Total 18.

---

## 6. Required actions before sign-off

1. Fix and re-capture B1; add a TMP-content check that survives the Snapshot -> encounter path.
2. Measure contrast on 02, 03, 06 and on low-ammo/empty-ammo captures; add greyscale conversions (B2, rows 14 and 15). Enlarge the `!` glyph if it is not visibly distinct.
3. Capture the missing states listed in A11 (at minimum: low ammo, empty ammo, melee, parry enabled, hook cooldown, 0 dash charges, overheal, exit found, SEALED/CLEARED banners, 1280x720 and 21:9).
4. Decide A1 (death card arming), B-1 (Araf chip semantics), B-2 (wash over HUD), C4 (vignette and Reduce Motion), C5 (auto-dismiss doc row), A10 (overheal hatch vs spec amendment). These are the user's and art-director's decisions; options and a recommendation are above.
5. Verify the Araf revive path restores cursor lock and input (A2).

## 7. Accessibility verdict vs Standard

GAPS. HUD-owned requirements are met by code (20 px floor, no colour-only meaning, keyboard and gamepad death card, Reduce Motion wired in every HUD view), but contrast and greyscale are unmeasured, Reduce Motion is not honoured by the low-health vignette, and the auto-dismiss statement in `design/accessibility-requirements.md` is now out of date. Carried and not counted as new: UI scaling, colourblind palette values, remapping screen, brightness and flash/strobe warning.
