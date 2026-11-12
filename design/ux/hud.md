# HUD Design

> **Status**: Approved (`/ux-review` APPROVED 2026-11-12; visual layer in `production/ui/combat-hud/visual-design.md`)
> **Author**: ux-designer (decisions by user, 2026-11-12)
> **Last Updated**: 2026-11-12
> **Game**: PURGATOR
> **Platform Targets**: PC Win64. Keyboard/mouse primary, gamepad partial. 1920x1080 reference; must hold 1280x720 to 4K, 16:10 and 21:9. HUD is display-only except the death card button.
> **Related GDDs**: none read for this draft (no GDD or game brief supplied). Data comes from `production/ui/combat-hud/inventory.md`. Systems: PlayerHealth, PlayerMovement, PlayerParryController, WeaponController/WeaponBase, PlayerAmmoInventory, WaveSpawnDirector, RunEvents/RunState, ArafLabyrinthDirector.
> **Accessibility Tier**: Standard (COMMITTED, `design/accessibility-requirements.md`)
> **Style Reference**: Main menu "Kan ve Kemik" (`design/ux/main-menu.md`): bone #E8DCC0, soot #0C0A0A, crimson #DB143D (graphics/fills only, 3.9:1, never text), focus #FFF4DC, danger #FF4D5E, dim #A09684. No art bible HUD section exists. Phase 2 visual design (tokens, plate opacity, contrast, assets): `production/ui/combat-hud/visual-design.md`.
> **Template**: HUD Design

> **Scope decision (user, 2026-11-12)**: Health, weapon+ammo, dash/parry/hook readiness, wave/encounter, Araf/run status migrate to UGUI (TextMeshPro, one HUD prefab). `AmmoHUD` and `RunHUD` are replaced. Crosshair, hitmarkers, damage numbers, damage vignette/chevrons STAY `OnGUI`, unchanged, out of scope. `PlayerDebugHUD` becomes default-OFF (F2); the RunHUD F3 block (seed / floor / run timer) moves into it. All HUD state is read-only: the HUD owns no game state, allocates nothing per frame, and is event-driven where events exist.

---

## 1. HUD Philosophy

PURGATOR is fast; the player's eyes live on the centre. The HUD is a set of four quiet corner plates the player reads in peripheral vision: bone text on soot, one accent per meaning, shape and text always paired with colour. It tells the player three things only: can I survive (health), can I act (ammo, dash, parry, hook), what is the room asking of me (wave, Araf). Everything else is world feedback.

**Visibility principle**: HIDE by default; CONTEXTUAL for encounter/Araf blocks; ALWAYS visible only for health, weapon+ammo, dash, parry, Araf status.

**Rule of Necessity**: "A HUD element earns its place when the player must make a split-second decision from it (fight, dash, swap, retreat) and the world cannot show it as clearly."

---

## 2. Information Architecture

### 2.1 Full Information Inventory

Source: inventory.md (b). Status: SHOWN (this HUD), EXISTING (stays OnGUI, unchanged), DEBUG (PlayerDebugHUD F2 only), NONE.

| Item | Status |
|---|---|
| Health current/max/percent, overheal | SHOWN |
| Low-health state | SHOWN (plate) + EXISTING (heartbeat vignette, must read same threshold) |
| Damage taken flash, damage direction chevrons, heal flash | EXISTING |
| Dash charges / recharge | SHOWN |
| Parry readiness / cooldown / active | SHOWN |
| Meat hook readiness | SHOWN (shotgun only) |
| Active weapon, ammo cur/max, empty | SHOWN |
| Primary/alt fire cooldown, blade combo step, full-auto flag | NONE (weapon feel carries it) |
| Crosshair, hitmarker, kill marker, recoil bloom | EXISTING |
| Damage numbers (dealt, crit, reflected, heal) | EXISTING |
| Wave n/total, enemies remaining in wave | SHOWN |
| Encounter banners: ARENA SEALED, WAVE n, WAVE n CLEARED, ARENA CLEARED | SHOWN (moved from debug) |
| Parry success feedback (was `PARRY!` banner) | SHOWN (moved, as parry-slot pulse, not centre text) |
| Wave name, wave timer, upcoming wave preview, encounter totals, pre-wave guards, alive/queued counts | DEBUG |
| Araf charge ready/used | SHOWN |
| Araf rooms revealed/total, exit revealed, Araf warning | SHOWN |
| Run phase | drives states (not displayed) |
| Seed, floor index, run timer (former RunHUD F3 block) | DEBUG (moved into PlayerDebugHUD; run timer still needs a source, see 4.3) |
| True-death card + new-run button | SHOWN (moved from debug) |
| Araf-transition death (OnDied with charge remaining) | No card; existing Araf fade/transition handles it |
| Speed, eye height, movement state, FPS | DEBUG |
| Enemy health bars, minimap, kill feed, stamina, reload | NONE (do not exist / not wanted) |
| Soul gems (`ruh cevheri`) | Deferred, no HUD this pass |
| Araf curse/blessing modifiers | Deferred, no HUD this pass |
| Boss health/name bar | Out of scope this pass |

### 2.2 Categorization

| Information Type | Always Show | Contextual | On Demand | Hidden | Reasoning |
|---|---|---|---|---|---|
| Health + overheal | X | | | | Survival; constant |
| Weapon + ammo | X | | | | Action capability; constant |
| Dash charges | X | | | | Core movement/defence resource |
| Parry state | X | | | | Timed defence, glanceable |
| Meat hook | | X (shotgun equipped) | | | Only usable with shotgun alt |
| Wave n/total + remaining | | X (encounter active) | | | Meaningless outside encounters |
| Araf charge status | X | | | | Second chance is the run's core stake |
| Araf rooms / exit | | X (in Araf) | | | Only relevant in labyrinth |
| Encounter banners | | X (events, ~1.8 s) | | | Momentary state changes |
| True-death card | | X (true death) | | | Replaces gameplay HUD |
| Seed/floor/run timer, wave timer, guards | | | X (F2 debug) | | Not decision-critical |

---

## 3. Layout Zones

### 3.1 Zone Diagram

```
 ┌────────────────────────────────────────────────────────────┐
 │  5% safe inset on all sides (inside Screen.safeArea)       │
 │  ┌──────────────────────────────────────────────────────┐  │
 │  │                 [TOP-CENTRE: ANNOUNCE]   [TOP-RIGHT]  │  │
 │  │                  banner, ~12-20% down    WAVE / ENEMY │  │
 │  │                                          ARAF status  │  │
 │  │                                                      │  │
 │  │          CENTRE 40% x 40%: CLEAR                     │  │
 │  │          (crosshair + OnGUI feedback only)           │  │
 │  │                                                      │  │
 │  │ [BOTTOM-LEFT]                          [BOTTOM-RIGHT] │  │
 │  │  HEALTH bar                              WEAPON name  │  │
 │  │  DASH pips  PARRY  HOOK                  AMMO count   │  │
 │  └──────────────────────────────────────────────────────┘  │
 └────────────────────────────────────────────────────────────┘
```

Anchoring: each zone is anchored to its own corner of the safe-area container (pivot on that corner). The container sits under a `HudRoot` whose local scale is a single float (1.0 now) so the deferred 75-150% scaling is a one-line addition with no re-layout.

### 3.2 Zone Specification Table

Sizes at 1080p reference.

| Zone | Position | Safe Zone | Primary Elements | Max Simultaneous | Notes |
|---|---|---|---|---|---|
| Bottom-left | Corner plate 400x150 | Yes, 5% inset | Health, Dash, Parry, Hook | 4 | Survival + defence |
| Bottom-right | Corner plate 320x120 | Yes, 5% inset | Weapon+ammo | 1 (3 sub-parts) | Replaces AmmoHUD footprint |
| Top-right | Corner zone bound 400x190; plate width 340 (height 40 exploration, 120 combat, 150 Araf) | Yes, 5% inset | Araf status (always); Wave + Enemies (encounter, never in Araf); Araf rooms (Araf only) | 4 | Replaces RunHUD footprint. Wave/Enemies and Araf rooms are mutually exclusive: Araf rooms (`ArafRoom`) never use `WaveSpawnDirector`/`EncounterArea`, so no wave encounter exists in Araf |
| Top-centre | Centred, band y 12-20% | Yes | Announcement banner, 1 at a time | 1 | Outside the central 40% zone (centre band is 30-70%) |
| Full-screen overlay | Whole screen | n/a | True-death card | 1 | True death only; hides all else |

| Platform | Top | Bottom | Left | Right | Notes |
|---|---|---|---|---|---|
| Win64 desktop (all aspect ratios) | 5% | 5% | 5% | 5% | Intersect with `Screen.safeArea`; 21:9 keeps corners, never pushes content to the centre |

---

## 4. HUD Element Specifications

Design colours: bone #E8DCC0 text, plate soot #0C0A0A at 60% alpha, dim #A09684, danger #FF4D5E, focus #FFF4DC, shells amber (1,0.7,0.15), bolts cyan (0,0.9,1). All text TextMeshPro, bold, with plate behind for contrast. Numbers rendered from a cached string table (0..999 pre-built at load); no per-frame concatenation.

### 4.1 Element Overview Table

| Element | Zone | Always Visible | Visibility Trigger | Data Source | Update Frequency | Max Size (% W) | Min Readable Size | Overlap Priority | Accessibility Alt |
|---|---|---|---|---|---|---|---|---|---|
| Health | BL | Yes | Hidden on true-death card | `PlayerHealth.CurrentHealth/MaxHealth/HealthPercent`; events `OnDamageTaken`, `OnHealed`, `OnRevived`; NEW overheal cap getter | Event (read getter in handler) | 21% | 20px text | 1 | Number + bar + `!` glyph at low |
| Dash pips | BL | Yes | n/a | `PlayerMovement.CurrentDashCharges`; NEW max/recharge/event | Event if added, else dirty-checked poll | 8% | 24px pips | 2 | Filled vs hollow shape + count text |
| Parry | BL | Only when the parry buckler is enabled (`enableShield`, default off; DualLoadoutAndWeapons §4) | Buckler enabled | `PlayerParryController.IsParrying/IsOnCooldown/CooldownNormalized`; `OnParrySuccess` | Poll (dirty-checked); event for success pulse | 6% | 20px label | 2 | Text state HAZIR/BEKLE + radial fill |
| Meat hook | BL | No | Shotgun active (`WeaponController.OnWeaponSwitched`) | `GothicShotgunWeapon.CanFireAlt`; NEW cooldown normalized | Poll (dirty-checked) | 6% | 20px label | 2 | Text state + fill |
| Weapon + ammo | BR | Yes | n/a | `WeaponController.ActiveWeapon`, `OnWeaponSwitched`; `WeaponBase.RequiredAmmoType`; `PlayerAmmoInventory.OnAmmoChanged/GetCurrentAmmo/GetMaxAmmo/GetAmmoRatio` | Event | 17% | 20px label, 48px count | 1 | Name + glyph shape + `BOŞ` text + count |
| Wave counter | TR | No | Encounter active, never in Araf (`WaveSpawnDirector.OnEncounterStarted`); binds to `WaveSpawnDirector.ActiveDirector` / `OnActiveDirectorChanged` and must not jump focus when the player walks into another room | `OnWaveStarted(int,int,string)`, `CurrentWaveIndex`, `TotalWaves` | Event | 18% | 28px | 3 | Text `DALGA n/t` |
| Enemies remaining | TR | No | Encounter active, never in Araf | `WaveSpawnDirector.OnEnemiesRemainingChanged(int,int)` | Event | 18% | 24px | 3 | Number + bar |
| Araf status | TR | Yes (run active) | `RunEvents.OnPhaseChanged` | `RunEvents.CurrentRun.ArafChargeUsed`, `Phase`; NEW charge-changed event | Event (phase) + one read on change | 18% | 20px | 3 | Text HAZIR/KULLANILDI + sigil shape |
| Araf rooms | TR | No | Phase = Araf | `RunEvents.OnArafRoomsChanged(int,int,bool)` | Event | 18% | 24px | 3 | `ODALAR r/t` + `ÇIKIŞ BULUNDU` text |
| Announcement | TC | No | Encounter events | `OnEncounterStarted`, `OnWaveStarted`, `OnWaveCompleted`, `OnEncounterCompleted` | Event | 30% | 40px | 4 | Text only |
| True-death card | Full | No | `RunEvents.OnTrueDeath` only | same | Event | 100% | 40px title | 0 (top) | Text + focused button |

### 4.2 Element Detail Blocks

**Health**
- Visual: soot plate, left heart/cross sigil, horizontal bar (fills left to right) over 0-max, big bone number (44px) then dim `/max` (20px). Fill crimson #DB143D (graphic, 3:1 OK).
- Overheal (HP above max): the bar stays max-length; an overflow segment in bone #FFF4DC with diagonal hatch extends past the right end (up to the overheal cap); number gains a `+n` suffix (e.g. `100 /100 +35`). Numeric always present, so not colour-dependent. Requires NEW `PlayerHealth` overheal cap getter (4.3).
- Update: on `OnDamageTaken` fill snaps down and a bright 120 ms edge flash marks the lost chunk; on `OnHealed` snaps up. Reduce Motion: no flash, snap only.
- Urgency: Normal; LOW at `HealthPercent <= lowHealthPercent`: plate border thickens to 3px in danger #FF4D5E, an opaque danger chip with `!` appears left of the number (number stays bone for contrast, see visual-design.md), 1 Hz pulse of border alpha. Reduce Motion: static thick border, no pulse. At 0 on an Araf transition the plate shows `0` until `OnRevived`.
- **Single low-health threshold: 30% of MaxHealth (`HealthPercent <= 0.30`).** Unifies the 30 (PlayerDamageFeedback) and 50/25 (PlayerDebugHUD) values; a percentage survives future max-HP changes. **Must be data-driven:** one `HudConfig` ScriptableObject field (`lowHealthPercent`), also read by the `PlayerDamageFeedback` heartbeat vignette and PlayerDebugHUD so there is one source of truth. If MaxHealth is 100 this equals today's 30 HP.
- Interaction: display-only.

**Dash pips**
- Visual: row of N diamond pips (N = max charges, not hard-coded 3). Filled bone diamond = ready; hollow outline = spent; next-recharging pip shows a clockwise fill. `n/N` text 20px beside them.
- Update: charge change event (NEW) or dirty-checked poll of `CurrentDashCharges`. `OnDashStarted` brightens the consumed pip 100 ms (skipped under Reduce Motion).
- Urgency: 0 charges: all hollow, `0/N` in dim, recharge fill visible.
- Interaction: display-only.

**Parry**
- Visual: shield glyph with radial cooldown fill, label `SAVUŞTUR` and state text: `HAZIR` (bone, glyph solid), `AKTİF` (focus colour, glyph with thick outline), `BEKLE` (dim, glyph hollow, fill sweeping).
- Update: poll `IsParrying`, `IsOnCooldown`, `CooldownNormalized`, dirty-checked (fill quantized to `cooldownFillSteps`). `OnParrySuccess(bool)` triggers a 250 ms glyph flash and `SAVUŞTURULDU` state-text swap (replaces the old centre `PARRY!` banner). Reduce Motion: text swap only.
- Urgency: none. Active window remaining is not shown (`AKTİF` is binary).
- Interaction: display-only.

**Meat hook**
- Visual: hook glyph + `KANCA` + `HAZIR` / `BEKLE`, radial fill during cooldown. Same slot style as parry. Hidden when shotgun not active; the slot keeps its place (no layout shift).
- Update: `OnWeaponSwitched` toggles visibility; readiness from `CanFireAlt` dirty-checked; fill needs NEW cooldown-normalized getter.
- Interaction: display-only.

**Weapon + ammo**
- Visual: bottom-right plate. Top line: weapon name (20px bold), slot index `[1]`/`[3]`, and a weapon glyph with a distinct silhouette per weapon (bolt, shell, blade). Large count `cur` (48px) + dim `/max` (20px). Bottom fill bar tinted by ammo type: bolts cyan, shells amber (identity colour only; name and glyph carry the same info). Melee (Executioner Blade): glyph + `YAKIN`, no count, no bar.
- Update: `OnWeaponSwitched` and `OnAmmoChanged` events set cached strings. `OnAmmoCollected` pulses the count 150 ms (Reduce Motion: none).
- Urgency: LOW at ratio < `lowAmmoRatio` (0.25): opaque danger chip with `!` appears; 1 Hz alpha pulse (static under Reduce Motion). EMPTY (0): count `0`, opaque danger chip with text tag `BOŞ` (20px), bar hollow outline. State is carried by `!` and `BOŞ`, never colour alone.
- Interaction: display-only.

**Wave counter and Enemies remaining**
- Visual: TR plate. Line 1 `DALGA n/t` (28px). Line 2 `KALAN 7` (24px) + thin remaining/total bar. Wave name not shown (debug only).
- Update: events only. HUD displays 1-based wave number (verify base of `CurrentWaveIndex`).
- Urgency: when remaining <= `lowEnemiesEmphasis` (3) the number is bold with underline (no colour needed). Hidden when no encounter is active; fades out 300 ms after `OnEncounterCompleted` (instant under Reduce Motion).
- Interaction: display-only.

**Araf status and Araf rooms**
- Visual: `ARAF: HAZIR` (crimson sigil + bone text) or `ARAF: KULLANILDI` (hollow grey sigil + dim text). In Araf phase: warning line (existing RunHUD copy, danger colour with `!` glyph), `ODALAR r/t` (24px), and `ÇIKIŞ BULUNDU` with an arrow glyph when the exit is revealed.
- Update: `RunEvents.OnPhaseChanged`, `OnArafRoomsChanged`; charge state read once on phase change (NEW event on `ConsumeArafCharge` preferred). Newly revealed room brightens 200 ms (Reduce Motion: none). An audio cue for exit reveal is owned by audio, not the HUD.
- Interaction: display-only.

**Announcement (top-centre)**
- Visual: single text band, 40px bold bone on a 60% soot plate; no icon; meaning carried by text only.
- Content and keys: section 8. One at a time, 1.8 s hold, 150 ms fade-in, 300 ms fade-out; unscaled time. Plate cap: 600x60 single line, 600x96 when a merged second line (28px) is shown (never more than two lines). Reduce Motion: instant on/off, no slide or scale.
- Interaction: display-only.

**True-death card**
- Shown ONLY on `RunEvents.OnTrueDeath` (the state after which a new run starts). `PlayerHealth.OnDied` while an Araf charge remains shows NO card; the existing Araf fade/transition handles it and the HUD stays as in the Araf context.
- Visual: full-screen soot overlay at 85% alpha (the 65% cap applies to HUD plates, not this state), centred title `GERÇEK ÖLÜM` 64px bone, one button `TEKRAR DENE` (P2 states, P1 focus sigil), 24px minimum.
- Input: cursor unlocks, button auto-focused; Enter/Space/gamepad A/left click confirm (P1/P2); no Back action. Confirm raises a restart-request event; the HUD does nothing else. Input lock of ~80 ms after confirm prevents double-trigger (P2).
- Update: fade-in 400 ms (instant under Reduce Motion). Dismissed on scene reload / new run start. All gameplay HUD hides while shown.
- Implementation dependency: restart logic currently lives in `PlayerDebugHUD`; it will be extracted into its own class that subscribes to the restart-request event. The HUD never starts a run itself.
- Interaction: interactive (1 button).

### 4.3 Required new read-only data (gaps from inventory)

The HUD must not own state. Implementation adds minimal getters/events; nothing else.

| Need | Requires |
|---|---|
| Overheal cap (to size overflow segment) | requires new read-only getter in `PlayerHealth` (e.g. `OverhealCap`); if the cap varies per source, show `+n` text only, no segment |
| Dash max charges | requires new read-only getter in `PlayerMovement` (`MaxDashCharges`) |
| Dash recharge progress 0-1 | requires new read-only getter in `PlayerMovement` (`DashRechargeNormalized`) |
| Dash charge changed | requires new event in `PlayerMovement` (`OnDashChargesChanged(int cur, int max)`); until then dirty-checked poll |
| Meat hook cooldown 0-1 | requires new read-only getter in `GothicShotgunWeapon` (`AltCooldownNormalized`) |
| Hook/parry/weapon "ready" events | optional; requires new event in `WeaponBase` / `PlayerParryController`; dirty-checked poll is acceptable (zero-GC) |
| Araf charge changed | requires new event in `RunEvents` raised by `RunState.ConsumeArafCharge` |
| Run elapsed time (for the debug F3 block) | requires new read-only getter in `RunState` (currently computed locally by RunHUD from `Time.unscaledTime`) |
| Low-health threshold | single `lowHealthPercent` in `HudConfig` SO; `PlayerDamageFeedback` and `PlayerDebugHUD` read it |
| Restart request | new restart-request event raised by the HUD; requires new restart-handler class extracted from `PlayerDebugHUD` |
| Parry window remaining | not needed (binary `AKTİF`) |

Zero-GC rules: no `GetComponent`/`Find` in Update; cache references on enable; subscribe in `OnEnable`, unsubscribe in `OnDisable`; polled values are dirty-checked so TMP text is touched only on change; strings come from the StringTable cache plus a pre-built int string table; no concatenation or `string.Format` per frame; no `SetText(float)` per frame.

---

## 5. HUD States by Gameplay Context

| Context | Elements Shown | Elements Hidden | Elements Modified | Transition Into This State |
|---|---|---|---|---|
| Exploration (tower, no encounter) | Health, Dash, Parry, Weapon+ammo, Araf status, Hook (if shotgun) | Wave, Enemies, Araf rooms, Announcement | none | Default on scene start; wave block fades out 300 ms after `OnEncounterCompleted` |
| Combat / encounter | Exploration set + Wave counter, Enemies remaining | Araf rooms | Announcements on events; low-health/low-ammo urgency active | `OnEncounterStarted`: wave block appears instantly with `ARENA MÜHÜRLENDİ` banner |
| Return from Araf (`RunEvents.OnEscapedAraf`) | Exploration set + Wave/Enemies when the encounter restarts (`OnEncounterRetried`), banner `DALGA 1` | Araf rooms, Araf warning | Araf status becomes `ARAF: KULLANILDI`; encounter restarts at wave 1 | `OnEscapedAraf`: Araf lines hide instantly, wave block binds to the active director |
| Araf (labyrinth) | Health, Dash, Parry, Weapon+ammo, Hook (if shotgun); TR zone: Araf status (KULLANILDI), warning, `ODALAR r/t`, `ÇIKIŞ BULUNDU` when revealed | Wave counter and Enemies remaining (code fact: Araf rooms never use `WaveSpawnDirector`/`EncounterArea`, so no wave encounter exists in Araf) | Araf warning line with `!`; TR plate grows to 340x150 inside the 400x190 zone, 4 TR elements max | `RunEvents.OnPhaseChanged(Araf)`: wave block hides instantly, rooms line appears |
| Death with Araf charge (OnDied, transition to Araf) | Gameplay HUD unchanged | none | Health plate shows 0 until `OnRevived` | No death card; existing Araf fade/transition runs |
| True death | True-death card + button | All gameplay HUD | Cursor unlocked, button focused | `RunEvents.OnTrueDeath`: 400 ms fade (instant under Reduce Motion) |
| Paused | No pause menu exists yet. Provisional: HUD stays as-is; banners run on unscaled time | none | none | Open Question; when a pause menu is added (ux-spec), HUD hides or dims behind it |
| Main menu | HUD absent: HUD prefab is instantiated only in gameplay scenes | all | n/a | Scene load; prefab must not exist in the MainMenu scene |
| Loading / Araf transition | Not specified here (screen transition P7) | all | n/a | n/a |

---

## 6. Information Hierarchy

| Element | Priority Tier | Reasoning | What Replaces It If Hidden |
|---|---|---|---|
| Health | MUST KEEP | Survival | Damage vignette/chevrons (OnGUI) |
| Weapon + ammo | MUST KEEP | Cannot act without it | Weapon model, sound |
| Dash pips | MUST KEEP | Core defensive resource | Camera/FOV feedback |
| Parry | SHOULD KEEP | Timed defence | Parry VFX/audio |
| Araf status | MUST KEEP | Run stakes | none |
| Wave counter | SHOULD KEEP | Pacing information | Banners |
| Enemies remaining | SHOULD KEEP | Tells when arena ends | Banner `DALGA n TEMİZLENDİ` |
| Araf rooms / exit | SHOULD KEEP | Navigation goal | none |
| Meat hook | CAN HIDE | Shotgun-only | Weapon feel |
| Announcement | CAN HIDE | Redundant with counters | Counters |
| True-death card | MUST KEEP | Player must know the run ended and how to start another | none |
| Seed/floor/run timer, wave timer, guards, FPS | ALWAYS HIDE (debug F2) | Not decision-critical | n/a |

---

## 7. Visual Budget

At 1920x1080 (2,073,600 px). Plates counted including padding.

| Budget Constraint | Limit | Measurement Method | Current Estimate | Status |
|---|---|---|---|---|
| Max simultaneous active HUD elements | 10 | Count visible elements in one frame | 9 worst case: combat = Health, Dash, Parry, Hook, Weapon, Wave, Enemies, Araf status, Announcement (Araf = 8; Wave/Enemies never coexist with Araf rooms) | To verify |
| Max % screen, exploration (steady state) | 6% | Plate pixel area / 2,073,600 px | BL 400x150 = 60,000 + BR 320x120 = 38,400 + TR 340x40 = 13,600 = 112,000 = 5.40% | To verify |
| Max % screen, combat (steady state, no banner) | 10% | Same | 60,000 + 38,400 + TR 340x120 = 40,800 = 139,200 = 6.71%. Araf worst case: TR 340x150 = 51,000, total 149,400 = 7.20% | To verify |
| Max % screen, combat incl. announcement banner (transient) | 10% (banner own cap: 600x96 merged two-line = 57,600 = 2.78%) | Same, banner counted | 139,200 + 57,600 = 196,800 = 9.49% | To verify |
| Max % of centre 40% zone | 0% | Only crosshair and OnGUI feedback allowed | 0% (announcement sits at 12-20% height) | To verify |
| Min contrast, text on plate | 4.5:1 (large >= 3:1) | Measure over lightest and darkest scenes with plate at final alpha | bone on soot ~14:1; dim #A09684 ~7:1; danger #FF4D5E ~6:1 | To verify |
| Backing plate opacity | 90% (min 88%; death overlay 88%) | Image alpha. Raised from 65%: a 65% plate over bright fire #FF8C2A gives only 4.00:1 for bone text; 90% gives 5.05:1 even over a white-hot flash (visual-design.md section 2) | 90% | To verify |
| Min text size (critical HUD) | 20px at 1080p (~13px at 720p by proportional scale) | Measure at 1080p and 1280x720 | 20px | To verify |
| Min icon/pip size | 24px at 1080p | Measure | 24px | To verify |

---

## 8. Feedback & Notification Systems

Only the banner system is a queue; low-health and low-ammo are state changes on their own elements, not notifications.

| Notification Type | Trigger System | Screen Position | Duration (ms) | Animation In / Out | Max Simultaneous | Priority (1 = highest) | Queue Behavior | Dismissible? |
|---|---|---|---|---|---|---|---|---|
| ARENA CLEARED | `OnEncounterCompleted` | Top-centre | 2200 | 150 fade-in / 300 fade-out | 1 | 1 | Never preempted | No |
| ARENA SEALED | `OnEncounterStarted` | Top-centre | 1800 | same | 1 | 2 | Preempts lower | No |
| WAVE n | `OnWaveStarted` | Top-centre | 1800 | same | 1 | 2 | Preempts lower; within 500 ms of ARENA SEALED it merges as a second line | No |
| WAVE n CLEARED | `OnWaveCompleted` | Top-centre | 1800 | same | 1 | 3 | Replaced by a newer WAVE n | No |
| Parry success | `OnParrySuccess` | Parry slot (BL), not a banner | 250 | glyph flash | 1 | n/a | Coalesces | No |
| True-death card | `RunEvents.OnTrueDeath` | Overlay | until action | 400 fade | 1 | 0 (critical) | Bypasses queue; clears banners | Button only |

Rules:
1. Single banner slot, lower priority number = more important. A more important banner replaces the current one; an equal or less important one replaces it only when the current one has less than 500 ms left, otherwise the new one waits (max 1 pending; an older pending one is dropped).
2. Same-type banners within 500 ms merge (e.g. back-to-back WAVE n CLEARED / WAVE n+1 show as one band, two lines).
3. True-death card is critical: bypasses queue, clears the current banner. Scene change clears all banners.
4. All timing uses unscaled time so hitstop/timeScale does not freeze or stretch banners.

### String Table keys (Turkish, pre-uppercased, existing ScriptableObject StringTable)

Final wording TBD by user; `{0}`/`{1}` formatting happens once per event, never per frame.

| Key | Value |
|---|---|
| `hud.health.label` | `CAN` |
| `hud.dash.label` | `ATILMA` |
| `hud.parry.label` | `SAVUŞTUR` |
| `hud.parry.ready` / `hud.parry.active` / `hud.parry.cooldown` / `hud.parry.success` | `HAZIR` / `AKTİF` / `BEKLE` / `SAVUŞTURULDU` |
| `hud.hook.label` | `KANCA` |
| `hud.hook.ready` / `hud.hook.cooldown` | `HAZIR` / `BEKLE` |
| `hud.ammo.empty` | `BOŞ` |
| `hud.weapon.melee` | `YAKIN` |
| `hud.ammo.bolts` / `hud.ammo.shells` | `OK` / `SAÇMA` |
| `hud.wave.counter` | `DALGA {0}/{1}` |
| `hud.wave.remaining` | `KALAN {0}` |
| `hud.araf.ready` / `hud.araf.used` | `ARAF: HAZIR` / `ARAF: KULLANILDI` |
| `hud.araf.warning` | existing RunHUD copy (move into table) |
| `hud.araf.rooms` | `ODALAR {0}/{1}` |
| `hud.araf.exitFound` | `ÇIKIŞ BULUNDU` |
| `hud.banner.arenaSealed` | `ARENA MÜHÜRLENDİ` |
| `hud.banner.wave` | `DALGA {0}` |
| `hud.banner.waveCleared` | `DALGA {0} TEMİZLENDİ` |
| `hud.banner.arenaCleared` | `ARENA TEMİZLENDİ` |
| `hud.truedeath.title` | `GERÇEK ÖLÜM` |
| `hud.truedeath.restart` | `TEKRAR DENE` |

Weapon display names currently come from `WeaponBase.WeaponDisplayName` (not the StringTable); the HUD should look names up by weapon slot (`hud.weapon.<slot>`) or the names must be moved into the table.

---

## 9. Platform Adaptation

| Platform | Safe Zone | Resolution Range | Input Method | HUD-Specific Notes |
|---|---|---|---|---|
| Win64 | 5% inset + `Screen.safeArea` | 1280x720 to 3840x2160; 16:9, 16:10, 21:9 | KB/M primary, gamepad partial | Canvas Scaler: Scale With Screen Size, reference 1920x1080, match height (replaces the non-uniform OnGUI matrix scaling of AmmoHUD/RunHUD). Corner anchoring so ultrawide never pushes HUD toward centre. No input prompts in HUD (display-only except the death button) |

**HUD repositionability**: out of scope (Known Intentional Limitations: HUD repositioning, Comprehensive tier). Mitigation: critical HUD stays in consistent corners.

---

## 10. Accessibility

### 10.1 Colorblind Modes

| Element | Colour-Only Risk | Fix |
|---|---|---|
| Health low | Danger red | `!` glyph + thick border + number |
| Overheal | Bone vs crimson segment | Hatched segment + `+n` number |
| Ammo type | Cyan vs amber | Weapon name + glyph silhouette |
| Ammo low/empty | Red count | `!` glyph, `BOŞ` text |
| Parry/hook ready vs cooldown | Bone vs dim | `HAZIR/BEKLE` text, solid vs hollow glyph, fill |
| Dash pips | Fill colour | Filled vs hollow diamond + `n/N` |
| Araf ready/used | Crimson vs grey | Text `HAZIR/KULLANILDI` + solid vs hollow sigil |

Palette-swap colourblind modes (Standard tier) apply to HUD accent colours via the shared theme asset; no meaning depends on them.

### 10.2 Text Scaling

UI scaling 75-150% is DEFERRED (carried gap from `design/accessibility-requirements.md`). Design provisions: single `HudRoot` scale, corner-anchored zones, content-sized plates (layout groups), no fixed-width text boxes. At 150% worst case BL (400x150 -> 600x225) and TR (340x150 -> 510x225) must still fit inside the 5% inset at 1280x720 with plates growing inward: to verify.

| Element | 100% | 125% | 150% | Overflow behavior |
|---|---|---|---|---|
| Health | TBD | TBD | TBD | Plate grows inward |
| Weapon + ammo | TBD | TBD | TBD | Plate grows inward |
| TR block | TBD | TBD | TBD | Plate grows inward/down; Araf rows may stack |
| Announcement | TBD | TBD | TBD | Wraps to two lines, never clips |

### 10.3 Motion Sensitivity

Setting: `GameSettings.Current.ReduceMotion`, read on enable and on settings change.

| Motion Element | Severity | Disabled by Reduce Motion? | Replacement |
|---|---|---|---|
| Low-health border pulse | Moderate | Yes | Static thick border |
| Low-ammo count pulse | Moderate | Yes | Static `!` + danger colour |
| Damage edge flash on bar | Mild | Yes | Snap |
| Dash pip brighten / ammo pickup pulse / room reveal brighten | Mild | Yes | None |
| Parry glyph flash | Mild | Yes | State text swap only |
| Banner fade | Mild | Yes | Instant on/off |
| Death card fade | Mild | Yes | Instant |
| Wave block fade-out | Mild | Yes | Instant |

Nothing flashes faster than 1 Hz; no shake in this HUD (camera shake is outside scope).

### 10.4 Subtitles Specification

N/A: no voiced content (`design/accessibility-requirements.md`). Banners are not subtitles. Revisit if dialogue is added.

### 10.5 HUD Opacity and Visibility Controls

| Setting | Range | Default | Status |
|---|---|---|---|
| Reduce Motion | On/Off | Off | Exists (`GameSettings`); HUD must honour |
| HUD text scale | 75-150% | 100% | Deferred; anchors prepared |
| HUD opacity / notification verbosity / damage-number toggle / high contrast | n/a | n/a | Not committed at Standard tier (high contrast out of scope); not specified |

---

## 11. Tuning Knobs

All in one `HudConfig` ScriptableObject (nothing hardcoded in HUD scripts).

| Parameter | Current Value | Range | Effect of Increase | Effect of Decrease | Player Adjustable? | Notes |
|---|---|---|---|---|---|---|
| `lowHealthPercent` | 0.30 | 0.1-0.5 | Warns earlier | Warns later | No | Single source also for vignette heartbeat and debug HUD |
| `lowAmmoRatio` | 0.25 | 0.1-0.5 | Warns earlier | Warns later | No | Matches old AmmoHUD |
| `lowEnemiesEmphasis` | 3 | 1-10 | Underline earlier | Later | No | Remaining-count emphasis |
| `bannerHoldMs` / `arenaClearedHoldMs` | 1800 / 2200 | 800-4000 | Longer text | Shorter | No | Unscaled time |
| `bannerFadeInMs` / `bannerFadeOutMs` | 150 / 300 | 0-600 | Softer | Snappier | No | 0 when Reduce Motion |
| `bannerMergeWindowMs` | 500 | 100-1000 | More merging | Less | No | |
| `plateAlpha` | 0.90 | 0.88-0.95 | More contrast | More world visible (below 0.88 worst-case contrast fails) | No | See visual-design.md section 2 |
| `safeInsetPercent` | 5 | 3-10 | Further from edge | Closer | No | |
| `hudScale` | 1.0 | 0.75-1.5 | Larger | Smaller | Deferred | One root float |
| `lowPulseHz` | 1.0 | 0.5-1.0 | Faster pulse | Slower | No | Hard cap 1 Hz (matches acceptance criterion) |
| `deathFadeMs` | 400 | 0-800 | Slower | Faster | No | |
| `cooldownFillSteps` | 20 | 8-60 | Smoother | Fewer UI updates | No | Dirty-check quantization |

---

## 12. Acceptance Criteria

**Layout & Visibility**
- [ ] At 1280x720, 1920x1080, 3840x2160, 16:10 and 21:9 all HUD elements lie inside the 5% safe inset with no clipping or overlap.
- [ ] Centre 40% x 40% zone contains no UGUI HUD element in any context (screenshot overlay test).
- [ ] Measured at 1080p: HUD plate area <= 6% of screen in exploration (expected 5.40%) and <= 10% in combat including the announcement banner (expected 9.49%; Araf worst case 7.20%); banner plate <= 600x60, or <= 600x96 when two lines are merged.
- [ ] Hook slot appears only while the shotgun is active and causes no layout shift.

**Per-Context Correctness**
- [ ] With `PlayerDebugHUD` OFF, the player still sees health, dash, parry, wave counter, banners and the true-death card; with F2 ON no debug element overlaps the new HUD.
- [ ] ARENA SEALED, WAVE n, WAVE n CLEARED, ARENA CLEARED each appear once on their event in the top-centre band and follow the priority/merge rules.
- [ ] Parry success shows the slot flash and `SAVUŞTURULDU`; no centre text appears.
- [ ] Overheal: HP above max shows `+n` and the hatched overflow; damage and healing update within one frame of the event.
- [ ] Low-health state triggers at exactly `lowHealthPercent`, and the `PlayerDamageFeedback` heartbeat vignette triggers at the same value (change the SO value and both follow).
- [ ] `OnDied` with an Araf charge remaining shows no death card and the gameplay HUD stays; `OnTrueDeath` shows `GERÇEK ÖLÜM` + `TEKRAR DENE`, cursor visible, button focused, all gameplay HUD hidden.
- [ ] Enter, Space, gamepad A and mouse click on `TEKRAR DENE` each raise exactly one restart-request event (double-press does not raise two).
- [ ] Araf: `ARAF: HAZIR/KULLANILDI`, `ODALAR r/t` and `ÇIKIŞ BULUNDU` match `RunEvents` payloads.
- [ ] The F3 block (seed / floor / run timer) appears only in PlayerDebugHUD and never in the player HUD.

**Accessibility**
- [ ] All critical HUD text >= 20px at 1080p; contrast >= 4.5:1 over the lightest and darkest scenes.
- [ ] Greyscale screenshot test: low health, low/empty ammo, parry/hook/dash ready vs cooldown, and Araf ready/used remain distinguishable.
- [ ] With ReduceMotion ON no HUD element pulses, flashes, fades or slides; with it OFF pulses are <= 1 Hz.

**Performance & Technical**
- [ ] Profiler: HUD scripts allocate 0 bytes per frame in combat; events are unsubscribed on disable.
- [ ] HUD prefab is absent from the MainMenu scene; all strings come from the StringTable (no literals in the prefab).

---

## 13. Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| Death-card flow (Araf-pending vs true death) | user | n/a | RESOLVED 2026-11-12: card only on `OnTrueDeath`; Araf transition shows no card |
| Where does restart live after extraction from PlayerDebugHUD? | user | n/a | RESOLVED 2026-11-12: own class; HUD only raises a restart-request event (implementation dependency) |
| Run seed/floor/timer (RunHUD F3 block) migrate or drop? | user | n/a | RESOLVED 2026-11-12: moves into PlayerDebugHUD; player HUD does not show it |
| No game brief or player-journey docs exist | user / game-designer | Before Approved | Open |
| Does `PlayerHealth` have a single overheal cap? If it varies per source, drop the overflow segment | game-designer / programmer | Before implementation | Open |
| Actual MaxHealth value; does 30% equal the intended warning point? | game-designer | Before Approved | Open |
| Is `CurrentWaveIndex` 0- or 1-based? | programmer | Implementation | Open |
| Pre-wave guard counter and wave timer were debug only: any player-facing need? | game-designer | Before Approved | Open |
| Pause menu does not exist: HUD behaviour when paused | ux-designer | With pause UX spec | Open |
| Does an encounter run inside Araf? | code | n/a | RESOLVED 2026-11-12: no. Araf rooms do not use `WaveSpawnDirector`/`EncounterArea`; Wave/Enemies are hidden in Araf |
| The true-death card is an explicitly navigated screen; it may belong in a ux-spec rather than the HUD doc | user | Before implementation | Open |
| OnGUI vignette/crosshair draw above UGUI; verify the heartbeat vignette does not push plate contrast below 4.5:1 | ui-programmer | Implementation | Open |
| Weapon display names are not in the StringTable | ui-programmer | Implementation | Open |
| UI scaling 75-150%, flash/strobe audit, colourblind palette values | user | Deferred per accessibility doc | Deferred |
