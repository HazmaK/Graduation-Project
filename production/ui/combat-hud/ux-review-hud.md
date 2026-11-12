## UX Review: HUD Design (PURGATOR) — re-review after revision
**Date**: 2026-11-12
**Reviewer**: ux-review skill
**Document**: design/ux/hud.md
**Checklist**: 3B (HUD Design) — from its `> **Template**: HUD Design` line
**Platform Target**: PC Win64, keyboard/mouse primary, gamepad partial, no touch (matches `.claude/docs/technical-preferences.md` `## Input & Platform`; project.yaml has no `platform` block)
**Accessibility Tier**: Standard — COMMITTED in `design/accessibility-requirements.md` (spec header matches)
**Workflow tier**: minimal

### Re-check of the previous blocking issues
- **Visual Budget arithmetic (was BLOCKING): RESOLVED, verified independently at 1080p (2,073,600 px).**
  - Exploration: 400x130 (52,000) + 320x120 (38,400) + 340x40 (13,600) = 104,000 = 5.02% (limit 6%). Correct.
  - Combat steady state: 52,000 + 38,400 + 340x120 (40,800) = 131,200 = 6.33% (limit 10%). Correct.
  - Araf worst case: 340x150 = 51,000, total 141,400 = 6.82%. Correct.
  - Combat with banner: banner 600x60 = 36,000; 131,200 + 36,000 = 167,200 = 8.06% (limit 10%). Correct.
  - Element count: combat worst case = Health, Dash, Parry, Hook, Weapon, Wave, Enemies, Araf status, Announcement = 9 (limit 10); Araf = 8. Correct.
  - Zone sizes in 3.2, the 4.1 "Max Size (% W)" column and acceptance criterion 3 now agree with section 7.
- **Encounter inside Araf (was BLOCKING): RESOLVED.** Sections 3.2, 4.1, 5 and 13 now state that Wave/Enemies are hidden in Araf (Araf rooms never use `WaveSpawnDirector`/`EncounterArea`), so TR holds at most 4 elements (Araf status, warning, rooms, exit) in a 340x150 plate inside the 400x190 zone. The claim about the code is the spec's own and was not re-verified against the source here.
- **`lowPulseHz` (was advisory): RESOLVED.** Range is now 0.5-1.0, matching the <=1 Hz criterion and section 10.3.

### Completeness: 10/10 sections present
- [x] HUD Philosophy
- [x] Information Architecture
- [x] Layout Zones with safe-zone margins
- [x] Per-element specs (zone, trigger, data source, priority)
- [x] HUD States by Gameplay Context (exploration, combat, Araf, paused provisional, true death; dialogue/cutscene not listed)
- [x] Information Hierarchy
- [x] Visual Budget
- [x] Platform Adaptation
- [x] Tuning Knobs
- [x] Acceptance Criteria (15)

### Quality Issues: 9 found (0 BLOCKING, 9 ADVISORY) — GDD gaps are listed separately below

1. **No dialogue/cutscene context in HUD States** [ADVISORY]. Where: section 5. Fix: add a row, or state N/A (no dialogue or cutscenes).
2. **Paused state is provisional** [ADVISORY]. Where: section 5, open questions. Fix: resolve with the pause ux-spec.
3. **UI scaling 75-150% deferred; text-scale table all TBD** [ADVISORY]. The Standard tier commits to adjustable UI size; the deferral is a tracked user decision in the accessibility doc. Where: section 10.2. Fix: fill the 100/125/150% table and verify the 150% worst case (BL 600x195, TR 510x225) fits the 5% inset at 1280x720.
4. **Colourblind palette values deferred** [ADVISORY]. Non-colour backups are fully specified (10.1). Fix: define the palette swap before polish.
5. **Merged two-line banner does not fit the banner cap** [ADVISORY]. Two lines of 40px text need about 90 px, but the banner plate is capped at 600x60 (section 7, criterion 3); the 12-20% band is about 86 px. Where: sections 7, 8 (merge rule). Fix: raise the banner cap for the merged case (and recompute the 8.06%), or reduce the merged text size.
6. **All contrast figures are "To verify"** [ADVISORY]. Plates are 60% alpha over arbitrary scenes and OnGUI vignettes draw above UGUI. Fix: measure on lightest and darkest scenes.
7. **Unresolved "Before Approved" open questions** [ADVISORY]. No game brief or player journey; MaxHealth/30% threshold, pre-wave guard need, overheal cap, `CurrentWaveIndex` base. Weapon names not in the StringTable.
8. **Localisation limits absent for Turkish text** [ADVISORY]. Fix: add max character counts; confirm uppercase Turkish glyphs (İ, Ş, Ğ, Ç) render in the TMP font asset.
9. **True-death card is an interactive navigated screen inside the HUD doc** [ADVISORY]. Fix: move to a ux-spec before implementation, or keep it and record that focus/Back behaviour is complete.

### GDD Alignment: GAPS FOUND (20/26 vault UI requirements covered; 0 blocking)
Source: the Obsidian vault `documentation/retro-fps/` is this project's GDD (user clarification; there is no `design/gdd/`). Checked: CoreLoopAndPurgatory, ArafLabyrinthSystem, DualLoadoutAndWeapons, WeaponArsenalArchitecture, HealthOrbAndDropSystem, WaveAndEncounterArchitecture, PlayerMovementArchitecture, GameFeelArchitecture, HitstopAndAudioFeedback, MainMenuAndSettings.

**Covered (20)**
1. Araf charge HAZIR/KULLANILDI (CoreLoop, RunHUD) -> hud.md 4.2 Araf status
2. Araf-phase mission/warning text (CoreLoop) -> 4.2 Araf warning line
3. ODALAR x/N and ÇIKIŞ BULUNDU (Araf sec. 5) -> 4.1, 4.2
4. Araf text contrast too low (Araf sec. 8) -> 4.2/sec. 7 (crimson never used for text)
5. No minimap (Araf sec. 8) -> 2.1 (NONE, justified)
6. No instant Game Over on first death; Araf fade/transition (CoreLoop sec. 2) -> 4.2, sec. 5
7. True permadeath end state (CoreLoop sec. 3) -> 4.2 true-death card
8. Hitmarker, kill marker, damage numbers (DualLoadout, GameFeel sec. 6, Hitstop) -> 2.1 EXISTING
9. Overheal (Araf Shrine to 150, HealthOrb `allowOverheal`) -> 4.2 Health
10. Heal flash (HealthOrb) -> 2.1 EXISTING
11. Ammo count, bar, type, caps 120/32 (WeaponArsenal sec. 3) -> 4.2 Weapon + ammo
12. Dry fire / empty (WeaponArsenal) -> 4.2 BOŞ
13. Dash 3 charges, 1.2 s recharge (PlayerMovement sec. 3) -> 4.2 Dash pips
14. Parry 0.35 s window / 0.5 s cooldown (DualLoadout) -> 4.2 Parry
15. Meat hook, shotgun alt only (WeaponArsenal, PlayerMovement sec. 5) -> 4.2 Meat hook
16. Wave n/total and remaining enemies (WaveAndEncounter) -> 4.2 Wave/Enemies
17. Low-health threshold 0.30 (HealthOrb) -> 4.2 (30%, same value)
18. UI rules: no owned state, zero GC, StringTable keys, CanvasScaler 1920x1080 match height, UI scaling deferred, ReduceMotion (MainMenuAndSettings) -> sec. 1, 4.3, 9, 10
19. Hitstop/timeScale safety (Hitstop, GameFeel) -> sec. 8 rule 4 (unscaled time)
20. Seed/floor/run timer debug (CoreLoop) -> 2.1 DEBUG

**Missing or unspecified (6, all ADVISORY)**
- DualLoadoutAndWeapons sec. 4: the Spiked Buckler/parry is optional (`PlayerParryController.enableShield`, default off), but hud.md shows the Parry slot as always visible. Add a visibility rule (hide the slot when the shield is disabled).
- WaveAndEncounterArchitecture (`ActiveDirector`, HUD focus): hud.md does not say which director the wave block binds to, or that focus must not shift when the player enters another room.
- CoreLoopAndPurgatory sec. 2 (return from Araf): the encounter restarts at wave 1, but sec. 5 has no "return from Araf" state (wave block reappearing, banners re-running); "Loading / Araf transition" is marked unspecified.
- CoreLoopAndPurgatory sec. 3 (soul gems, `ruh cevheri`): no HUD pickup or counter, and hud.md does not categorise it as hidden or deferred (meta-progression, not yet built).
- CoreLoopAndPurgatory sec. 2 (double-edged Araf items, Phase 3 curse/blessing): no active-modifier display and no hidden/deferred statement; MaxHealth changes are handled because the bar reads MaxHealth dynamically.
- CoreLoopAndPurgatory sec. 1 (boss purification duels): no boss name/health display, and 2.1 only says enemy health bars are "not wanted"; state boss HUD as out of scope or specify it.

Notes: the vault's wave director uses a separate <35% low-health rule for spawn pacing (WaveAndEncounter), which is gameplay, not the HUD warning, so it does not conflict with the 30% HUD threshold. The vault also shows an overheal cap exists for the Shrine (150), which partly answers the open overheal-cap question.

### Accessibility: GAPS
- Target tier: Standard (committed).
- Met: critical text >= 20px at 1080p; 4.5:1 contrast target stated; every colour-coded element has a non-colour backup; Reduce Motion honoured for each motion element; pulses capped at 1 Hz; no subtitles needed.
- Gaps: UI scaling deferred, colourblind palette deferred, text-scale table TBD, contrast unmeasured. All but contrast are tracked as deferred in the accessibility doc.

### Pattern Library: N/A — no pattern library checklist for this document type

### Verdict: APPROVED
**Blocking issues**: 0
**Advisory issues**: 15 (9 from the earlier pass + 6 GDD Alignment gaps above)
**Dimensions not assessed**: 0 (Pattern Library is N/A and excluded from the count)

Remaining non-pass dimensions: Accessibility GAPS (all tracked deferrals except unmeasured contrast) and GDD Alignment GAPS FOUND (6 advisory items, none blocking). The spec is ready for handoff to `/team-ui` Phase 2 (Visual Design). Recommended before implementation: add the `enableShield` visibility rule for Parry and decide the Araf-return state. The review is advisory; no files other than this report were written.
