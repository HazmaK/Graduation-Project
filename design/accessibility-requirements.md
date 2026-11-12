# Accessibility Requirements

> **Status**: Committed
> **Author**: user + ux-designer
> **Last Updated**: 2026-11-12
> **Accessibility Tier Target**: Standard
> **Platform(s)**: PC (Windows 64-bit)
> **External Standards Targeted**:
> - WCAG 2.1 Level AA (text contrast, text sizing)
> - AbleGamers CVAA Guidelines — reference only (no voice chat/messaging, CVAA not legally applicable)
> - Xbox Accessibility Guidelines (XAG): No
> - PlayStation Accessibility (Sony Guidelines): No
> - Apple / Google Accessibility Guidelines: N/A
> **Accessibility Consultant**: None engaged
> **Linked Documents**: `design/ux/interaction-patterns.md`, `design/ux/main-menu.md` (no `design/gdd/systems-index.md` at `rigor: minimal`; systems are documented in `documentation/retro-fps`)
> **Template**: Accessibility Requirements

> If a feature conflicts with a commitment made here, this document wins —
> change the feature, not the commitment, unless a formal revision is approved.

---

## Accessibility Tier Definition

| Tier | Core Commitment |
|------|----------------|
| Basic | Critical text readable; no colour-only information; independent volume controls; completable without photosensitivity risk. |
| **Standard (this project)** | Basic + full input remapping, adjustable text/UI size, at least one colourblind mode, no un-extendable timed input. |
| Comprehensive | Standard + menu screen reader, mono audio, HUD repositioning, visual indicators for all critical audio. |
| Exemplary | Comprehensive + full subtitle customisation, high contrast mode, external audit. |

### This Project's Commitment

**Target Tier**: Standard

**Rationale**: PURGATOR is a fast, reflex-driven boomer shooter, so its heaviest
barriers are motor (speed, aiming, held inputs) and visual (dark gothic scenes,
muzzle flashes, gore, hitstop). It has no voiced dialogue and little text, so
reading/cognitive load is low. Standard covers the motor barriers through input
remapping and hold/toggle alternatives, and the visual ones through text/UI
scaling, colourblind modes and reduce motion. Comprehensive features (menu
screen reader, mono audio, HUD repositioning) exceed a small graduation-project
team's capacity; dropping to Basic would leave players who need remapping or
colourblind support unable to play a game whose core loop depends on both.

**Features explicitly in scope (beyond tier baseline)**:
- Full keyboard-only and gamepad-only menu navigation (P1, P6) — the fast-start goal of the main menu depends on it.
- Mouse sensitivity setting — aiming is the core verb.

**Features explicitly out of scope**:
- Screen reader for menus, mono audio, HUD repositioning, one-hand mode — see Known Intentional Limitations.

---

## Visual Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Minimum text size — menu UI | Standard | All menu screens | Not Started | ≥ 24px at 1080p; scale proportionally above. |
| Minimum text size — HUD | Standard | Critical HUD (health, ammo, Araf status) | Not Started | ≥ 20px at 1080p. Current HUDs are `OnGUI` — audit on HUD redesign. |
| Text contrast — UI text | Standard | All UI text | Not Started | ≥ 4.5:1 body, ≥ 3:1 large text. Dark art behind text must be backed/darkened. |
| Subtitles (all rows) | — | — | N/A | No voiced content. Revisit if dialogue is added. |
| Colourblind modes — Protan / Deutan / Tritan | Standard | Enemy, damage, pickup, Araf signals | Not Started | At least one mode required; target all three via palette swap. |
| Colour-as-only-indicator audit | Basic | All UI and gameplay | In Progress | See audit table below. |
| UI scaling | Standard | All UI | Not Started | 75%–150%, default 100%, HUD independent of menus. **Deferred from the main menu work by user decision 2026-11-12** — main menu Settings does not yet expose it. |
| High contrast mode | Comprehensive | — | Out of scope | See Known Intentional Limitations. |
| Brightness / gamma | Basic | Global | Not Started | Settings row + calibration image, −50% to +50%. Deferred (user decision 2026-11-12). |
| Flash / strobe warning | Basic | Muzzle flash, hitstop, gore VFX, Araf transition | Not Started | Pre-launch photosensitivity notice; audit VFX against ≤ 3 flashes/s; flash-reduction option. Deferred (user decision 2026-11-12). |
| Motion reduction mode | Standard | Menu transitions/ambient art, camera shake, view bob, hitstop flash | In Progress | Main menu: "Hareketi Azalt" row specified (`design/ux/main-menu.md`). Gameplay camera shake/bob must also read it. |

### Color-as-Only-Indicator Audit

| Location | Color Signal | What It Communicates | Non-Color Backup | Status |
|----------|-------------|---------------------|-----------------|--------|
| Main menu focus | Brightness/colour on focused item | Current selection | Sigil marker + scale (P1/P2) | Resolved (spec) |
| Settings sliders | Fill colour | Value | Numeric % text (P3) | Resolved (spec) |
| RunHUD Araf status | Red vs grey | Araf ready / used | Text "ARAF: HAZIR / KULLANILDI" | Resolved (existing) |
| Damage / enemy / pickup feedback | Colour flashes | Hit, damage taken, pickup type | To audit | Open |

---

## Motor Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Full input remapping | Standard | All gameplay inputs, KB/M and gamepad separately | Not Started | Separate "KONTROLLER" screen under Settings with its own UX spec (user decision 2026-11-12). Input System rebinding; conflict warning; persist. |
| Input method switching | Standard | PC | Not Started | Switch KB/M ↔ gamepad anytime; prompts update dynamically. Menu already single-focus across devices. |
| One-hand mode | — | — | Out of scope | See Known Intentional Limitations. |
| Hold-to-press alternatives | Standard | All hold inputs (to audit: sprint/dash/charge if any) | Not Started | Toggle alternative for every hold input. |
| Rapid input alternatives | Standard | Any sustained > 3 presses/s | Not Started | Audit weapons for mash-fire; provide hold-to-autofire. |
| Input timing adjustments | Standard | Timed inputs (parry window) | Not Started | Timing window multiplier 0.5x–3.0x if parry ships. |
| Aim assist | Standard | Gamepad ranged combat | Not Started | Gamepad only; strength and radius sliders. |
| Movement assists | Standard | Movement | Not Started | Toggle-sprint if sprint is a held input. |
| Platforming assists | — | — | N/A | No platforming system. |
| HUD repositioning | Comprehensive | — | Out of scope | See Known Intentional Limitations. |

---

## Cognitive Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Difficulty options | Standard | Damage dealt/received, enemy aggression | Not Started | Granular sliders preferred over a single label. |
| Pause anywhere | Basic | All gameplay states | Not Started | **Risk:** no pause menu exists yet. Araf transition and run end must also be pausable. |
| Tutorial persistence | Standard | Help text | Not Started | No tutorial system yet; applies when added. |
| Objective clarity | Standard | Araf "find the exit" goal | Not Started | Current objective visible within 2 presses (RunHUD shows Araf warning). |
| Visual indicators for audio-only info | Standard | Gameplay-critical SFX | Not Started | See SFX audit below. |
| Reading time for UI | Standard | Auto-dismissing UI | In Progress | Display revert prompt is 10 s (≥ 5 s, OK). No other auto-dismissing info. |
| Cognitive load documentation | Comprehensive | — | Out of scope | |
| Navigation assists | Standard | Araf labyrinth | Not Started | Decide whether labyrinth exploration intentionally omits waypoints; document justification. |

---

## Auditory Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| Subtitles for spoken dialogue | Basic | — | N/A | No voiced content. |
| Captions for critical SFX | Comprehensive | — | Out of scope | Visual backups (below) cover the Standard requirement. |
| Mono audio | Comprehensive | — | Out of scope | See Known Intentional Limitations. |
| Independent volume controls | Basic | Master / Music / SFX / UI | Not Started | Four sliders + master (user decision 2026-11-12: separate UI slider). No voice bus (no voiced content). Main settings now; pause menu when it exists. |
| Directional audio visuals | Comprehensive | — | Out of scope | |
| Hearing aid compatibility | Standard | High-frequency cues | Not Started | Audit cues > 4 kHz for low-frequency/visual equivalents. |

### Gameplay-Critical SFX Audit

| Sound Effect | What It Communicates | Visual Backup | Caption Required | Status |
|-------------|---------------------|--------------|-----------------|--------|
| Enemy attack wind-up | Incoming damage | Enemy animation / telegraph | No | To verify |
| Off-screen enemy | Threat direction | None | Yes (or indicator) | Open |
| Low health warning | Danger | HUD health value | No | To verify |
| Araf transition | Phase change | Fade + RunHUD warning | No | Resolved (existing) |
| Escape portal opened | Exit available | Portal visual | No | To verify |

---

## Platform Accessibility API Integration

| Platform | API / Standard | Features Planned | Status | Notes |
|----------|---------------|-----------------|--------|-------|
| PC (Steam) | Steam Input | Controller remapping via Steam Input in addition to in-game remapping | Not Started | In-game KB/M remapping still required. |
| PC (Screen Reader) | NVDA / Windows Narrator | — | Out of scope | See Known Intentional Limitations. |
| Xbox / PlayStation / iOS / Android | — | — | N/A | Not target platforms. |

---

## Per-Feature Accessibility Matrix

| System | Visual Concerns | Motor Concerns | Cognitive Concerns | Auditory Concerns | Addressed | Notes |
|--------|----------------|---------------|-------------------|------------------|-----------|-------|
| Main menu | Contrast on dark art; motion | None (full KB/gamepad/mouse) | Low (3 choices) | UI sounds have visuals (P8) | Partial | UI scaling deferred |
| Player movement / camera | Camera shake, view bob | Held inputs, speed | Low | — | No | Reduce motion + toggles |
| Weapons / combat | Muzzle flash, gore, hitstop flash | Aiming, rapid fire, parry timing | Medium | Hit/kill confirm sounds | No | Flash audit, aim assist |
| Enemies | Colour-coded feedback | — | Attack telegraphs | Off-screen threats | No | |
| Araf labyrinth / run loop | Dark scenes | — | Exit search, permadeath stakes | Transition sounds | Partial | Objective in RunHUD |
| HUD (OnGUI) | Text size, contrast | — | — | — | No | Audit on HUD redesign |

---

## Accessibility Test Plan

| Feature | Test Method | Test Cases | Pass Criteria | Responsible | Status |
|---------|------------|------------|--------------|-------------|--------|
| Text contrast | Contrast analyser on screenshots | All text/background pairs, art animated and static | Body ≥ 4.5:1, large ≥ 3:1 | ux-designer | Not Started |
| Colourblind modes | Coblis on screenshots | Combat, Araf, menus in each mode | No essential info lost | ux-designer | Not Started |
| Input remapping | Manual | Rebind all inputs, play TestLevel_Gauntlet | All actions work; conflicts prevented; persists | qa-tester | Not Started |
| Hold input toggles | Manual | All hold inputs in toggle mode | No state requires sustained hold | qa-tester | Not Started |
| Reduced motion | Manual | All menus + one run | No looping menu animation; no camera shake above threshold | ux-designer | Not Started |
| Menu navigation | Manual | Main menu, Settings, dialogs with keyboard-only / gamepad-only / mouse-only | Every element reachable; focus always visible | ux-designer | Not Started |
| Volume controls | Manual | Each slider 0–100 | Each bus independent; persists | qa-tester | Not Started |

---

## Known Intentional Limitations

| Feature | Tier Required | Why Not Included | Risk / Impact | Mitigation |
|---------|--------------|-----------------|--------------|------------|
| Menu screen reader | Comprehensive | UGUI exposes no accessible names to NVDA/Narrator without extra work; team capacity | Blind players cannot navigate menus | Simple, consistent menu structure; revisit post-graduation |
| Mono audio | Comprehensive | Audio mixer work beyond scope | Single-sided deafness loses directional cues | Visual telegraphs for attacks |
| HUD repositioning | Comprehensive | HUD is OnGUI, no layout system | Reduced peripheral vision users | Keep critical HUD near edges/centre consistently |
| One-hand mode | Comprehensive | Shooter requires aim + move simultaneously | One-handed players excluded | Remapping (Standard) partially helps |
| High contrast mode | Exemplary/Comprehensive | Art style dependency | Low-vision players | Contrast minimums + brightness setting |

---

## Audit History

| Date | Auditor | Type | Scope | Findings Summary | Status |
|------|---------|------|-------|-----------------|--------|
| 2026-11-12 | ux-designer (self) | Design review | Main menu spec | Basic-level items present; Standard UI scaling deferred | Open |

---

## External Resources

| Resource | URL | Relevance |
|----------|-----|-----------|
| WCAG 2.1 | https://www.w3.org/TR/WCAG21/ | Contrast and text sizing |
| Game Accessibility Guidelines | https://gameaccessibilityguidelines.com | Game-specific checklist |
| Colour Blindness Simulator (Coblis) | https://www.color-blindness.com/coblis-color-blindness-simulator/ | Colourblind testing |
| Accessible Games Database | https://accessible.games | Examples |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| When will UI scaling (Standard) ship, given it was deferred from the main menu? | user | Before HUD redesign | Open |
| Pause menu does not exist — required by Basic "pause anywhere". | user / ui-programmer | Before first playable build | Open |
| KONTROLLER (remapping) screen needs its own UX spec. | ux-designer | Before release | Open |
| Brightness/gamma and flash warning deferred — schedule them. | user | Before first external playtest | Open |
| Does Araf intentionally omit navigation assists (waypoints)? Needs design justification. | game-designer | Before Araf polish | Open |
