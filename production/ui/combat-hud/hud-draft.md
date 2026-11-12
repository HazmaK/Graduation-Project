# HUD Design

> **Status**: Draft
> **Author**: ux-designer (decisions by user, 2026-11-12)
> **Last Updated**: 2026-11-12
> **Game**: PURGATOR
> **Platform Targets**: PC Win64. Keyboard/mouse primary, gamepad partial. 1920x1080 reference; must hold 1280x720 to 4K, 16:10 and 21:9. HUD is display-only except the death card button.
> **Related GDDs**: none read for this draft (no GDD or game brief supplied). Data comes from `production/ui/combat-hud/inventory.md`. Systems: PlayerHealth, PlayerMovement, PlayerParryController, WeaponController/WeaponBase, PlayerAmmoInventory, WaveSpawnDirector, RunEvents/RunState, ArafLabyrinthDirector.
> **Accessibility Tier**: Standard (COMMITTED, `design/accessibility-requirements.md`)
> **Style Reference**: Main menu "Kan ve Kemik" (`design/ux/main-menu.md`): bone #E8DCC0, soot #0C0A0A, crimson #DB143D (graphics/fills only, 3.9:1, never text), focus #FFF4DC, danger #FF4D5E, dim #A09684. No art bible HUD section exists.
> **Template**: HUD Design
> **Promotion**: draft at `production/ui/combat-hud/hud-draft.md`; promote to `design/ux/hud.md` after user approval.

> **Scope decision (user, 2026-11-12)**: Health, weapon+ammo, dash/parry/hook readiness, wave/encounter, Araf/run status migrate to UGUI (TextMeshPro, one HUD prefab). `AmmoHUD` and `RunHUD` are replaced. Crosshair, hitmarkers, damage numbers, damage vignette/chevrons STAY `OnGUI`, unchanged, out of scope. `PlayerDebugHUD` becomes default-OFF (F2). All HUD state is read-only: the HUD owns no game state, allocates nothing per frame, and is event-driven where events exist.

---

## 1. HUD Philosophy

PURGATOR is fast; the player's eyes live on the centre. The HUD is a set of four quiet corner plates the player reads in peripheral vision: bone text on soot, one accent per meaning, shape and text always paired with colour. It tells the player three things only: can I survive (health), can I act (ammo, dash, parry, hook), what is the room asking of me (wave, Araf). Everything else is world feedback.

**Visibility principle**: HIDE by default; CONTEXTUAL for encounter/Araf blocks; ALWAYS visible only for health, weapon+ammo, dash, parry.

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
| Primary/alt fire cooldown, blade combo step, full-auto flag | NONE (not needed; weapon feel carries it) |
| Crosshair, hitmarker, kill marker, recoil bloom | EXISTING |
| Damage numbers (dealt, crit, reflected, heal) | EXISTING |
| Wave n/total, enemies remaining in wave | SHOWN |
| Encounter banners: ARENA SEALED, WAVE n, WAVE n CLEARED, ARENA CLEARED | SHOWN (moved from debug) |
| Parry success feedback (was `PARRY!` banner) | SHOWN (moved, as parry-slot pulse, not centre text) |
| Wave name, wave timer, upcoming wave preview, encounter totals, pre-wave guards, alive/queued counts | DEBUG |
| Araf charge ready/used | SHOWN |
| Araf rooms revealed/total, exit revealed, Araf warning | SHOWN |
| Run phase | drives states (not displayed) |
| Seed, floor index, run timer (RunHUD F3 block) | DEBUG (not migrated) |
| Death card + restart; true death | SHOWN (moved from debug) |
| Speed, eye height, movement state, FPS | DEBUG |
| Enemy health bars, minimap, kill feed, stamina, reload | NONE (do not exist / not wanted) |

### 2.2 Categorization

| Information Type | Always Show | Contextual | On Demand | Hidden | Reasoning |
|---|---|---|---|---|---|
| Health + overheal | X | | | | Survival; constant |
| Weapon + ammo | X | | | | Action capability; constant |
| Dash charges | X | | | | Core movement/defence resource |
| Parry state | X | | | | Timed defence, must be glanceable |
| Meat hook | | X (shotgun equipped) | | | Only usable with shotgun alt |
| Wave n/total + remaining | | X (encounter active) | | | Meaningless outside encounters |
| Araf charge status | X | | | | Second-chance is the run's core stake |
| Araf rooms / exit | | X (in Araf) | | | Only relevant in labyrinth |
| Encounter banners | | X (events, 1.8 s) | | | Momentary state changes |
| Death card | | X (death) | | | Replaces gameplay HUD |
| Run seed/floor/timer, wave timer, guards | | | X (F2/F3 debug) | | Not decision-critical |

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
| Bottom-left | Corner, up to 420x150 | Yes, 5% inset | Health, Dash, Parry, Hook | 4 | Survival + defence |
| Bottom-right | Corner, up to 360x140 | Yes, 5% inset | Weapon+ammo | 1 (3 sub-parts) | Replaces AmmoHUD footprint |
| Top-right | Corner, up to 400x190 | Yes, 5% inset | Wave, Enemies, Araf status, Araf rooms | 4 | Replaces RunHUD footprint |
| Top-centre | Centred, band y 12-20% | Yes | Announcement banner, 1 at a time | 1 | Outside the central 40% zone (centre band is 30-70%) |
| Full-screen overlay | Whole screen | n/a | Death card | 1 | Death only; hides all else |

| Platform | Top | Bottom | Left | Right | Notes |
|---|---|---|---|---|---|
| Win64 desktop (all aspect ratios) | 5% | 5% | 5% | 5% | Intersect with `Screen.safeArea`; 21:9 keeps corners, never pillar-boxes content to centre |

---

## 4. HUD Element Specifications

Design colours: bone #E8DCC0 text, plate soot #0C0A0A at 60% alpha, dim #A09684, danger #FF4D5E, focus #FFF4DC, shells amber (1,0.7,0.15), bolts cyan (0,0.9,1). All text TextMeshPro, bold, with plate behind for contrast. Numbers rendered from a cached string table (0..999 pre-built at load); no per-frame concatenation.

### 4.1 Element Overview Table

| Element | Zone | Always Visible | Visibility Trigger | Data Source | Update Frequency | Max Size (% W) | Min Readable | Overlap Priority | Accessibility Alt |
|---|---|---|---|---|---|---|---|---|---|
| Health | BL | Yes | Hidden on death card | `PlayerHealth.CurrentHealth/MaxHealth/HealthPercent`; events `OnDamageTaken`, `OnHealed`, `OnRevived`; NEW overheal cap getter | Event (read getter in handler) | 12% | 20px text | 1 | Number + bar + `!` glyph at low |
| Dash pips | BL | Yes | n/a | `PlayerMovement.CurrentDashCharges`; NEW max/recharge/event | Event if added, else dirty-checked poll | 8% | 24px pips | 2 | Filled vs hollow shape + count text |
| Parry | BL | Yes | n/a | `PlayerParryController.IsParrying/IsOnCooldown/CooldownNormalized`; `OnParrySuccess` | Poll (dirty-checked); event for success pulse | 6% | 20px label | 2 | Text state HAZIR/BEKLE + radial fill |
| Meat hook | BL | No | Shotgun active (`WeaponController.OnWeaponSwitched`) | `GothicShotgunWeapon.CanFireAlt`; NEW cooldown normalized | Poll (dirty-checked) | 6% | 20px label | 2 | Text state + fill |
| Weapon + ammo | BR | Yes | n/a | `WeaponController.ActiveWeapon`, `OnWeaponSwitched`; `WeaponBase.WeaponDisplayName/RequiredAmmoType`; `PlayerAmmoInventory.OnAmmoChanged/GetCurrentAmmo/GetMaxAmmo` | Event | 14% | 20px label, 48px count | 1 | Name + icon shape + `BOŞ` text + count |
| Wave counter | TR | No | Encounter active (`WaveSpawnDirector.OnEncounterStarted`, state) | `OnWaveStarted(int,int,string)`, `CurrentWaveIndex`, `TotalWaves` | Event | 12% | 28px | 3 | Text `DALGA n/t` |
| Enemies remaining | TR | No | Encounter active | `WaveSpawnDirector.OnEnemiesRemainingChanged(int,int)` | Event | 12% | 24px | 3 | Number + bar |
| Araf status | TR | Yes (while a run is active) | `RunEvents.OnPhaseChanged` | `RunEvents.CurrentRun.ArafChargeUsed`, `Phase`; NEW charge-changed event | Event (phase) + one read on change | 12% | 20px | 3 | Text HAZIR/KULLANILDI + sigil shape |
| Araf rooms | TR | No | Phase = Araf | `RunEvents.OnArafRoomsChanged(int,int,bool)` | Event | 12% | 24px | 3 | `ODALAR r/t` + `ÇIKIŞ BULUNDU` text |
| Announcement | TC | No | Encounter events | `OnEncounterStarted`, `OnWaveStarted`, `OnWaveCompleted`, `OnEncounterCompleted` | Event | 30% | 40px | 4 | Text only |
| Death card | Full | No | `PlayerHealth.OnDied`, `RunEvents.OnTrueDeath` | same + `OnRevived` to dismiss | Event | 100% | 40px title | 0 (top) | Text + focused button |

### 4.2 Element Detail Blocks

**Health**
- Visual: soot plate, left heart/cross sigil, horizontal bar (fill direction left to right) with 0-max range, big bone number (44px) then dim `/max` (20px). Fill crimson #DB143D (graphic, 3:1 OK). No lerp lag layer needed.
- Overheal (HP above max): the bar stays max-length; an overflow segment in bone #FFF4DC with diagonal hatch extends past the right end (up to the overheal cap length); number gains a `+n` suffix in bone (e.g. `100 /100 +35`). Numeric always present, so not colour-dependent. Requires NEW `PlayerHealth` overheal cap getter (see 4.3).
- Update: on `OnDamageTaken` fill snaps down and a bright 120 ms edge flash marks the lost chunk; on `OnHealed` snaps up. Reduce Motion: no flash, snap only.
- Urgency: Normal; LOW at `HealthPercent <= lowHealthThreshold` (single value, see below): plate border thickens to 3px in danger #FF4D5E, `!` glyph appears left of the number, number turns danger colour, 1 Hz pulse of border alpha. Reduce Motion: static thick border, no pulse. ZERO: bar empty, number `0`, card takes over.
- **Single low-health threshold: 30% of MaxHealth (`HealthPercent <= 0.30`).** Rationale: unifies the 30 (PlayerDamageFeedback) and 50/25 (PlayerDebugHUD) values; a percentage survives future max-HP changes. Must be data-driven: one `HudConfig` ScriptableObject field (`lowHealthPercent`), also read by `PlayerDamageFeedback` heartbeat vignette and PlayerDebugHUD so there is one source of truth. If actual MaxHealth is 100 this equals today's 30 HP.
- Interaction: display-only. Customization: none now (HUD opacity/scale deferred).

**Dash pips**
- Visual: row of N diamond pips (N = max charges, not hard-coded 3). Filled bone diamond = ready; hollow outline = spent; the next-recharging pip shows a clockwise fill. Number `n/N` text 20px beside them.
- Update: charge change event (NEW) or dirty-checked poll of `CurrentDashCharges`; no animation except pip fill progress. Dash start (`OnDashStarted`) causes a 100 ms brightening of the consumed pip (skipped under Reduce Motion).
- Urgency: 0 charges: all hollow and `0/N` in dim colour, recharge pip fill visible.
- Interaction: display-only.

**Parry**
- Visual: shield glyph with radial cooldown fill and label `SAVUŞTUR` + state text: `HAZIR` (bone, glyph solid), `AKTİF` (focus colour, glyph outlined with thick border), `BEKLE` (dim, glyph hollow, fill sweeping).
- Update: poll `IsParrying`, `IsOnCooldown`, `CooldownNormalized` with dirty check (fill updated only when quantized value changes, 20 steps). `OnParrySuccess(bool)` triggers a 250 ms glyph flash + `SAVUŞTURULDU` text swap in the state text (replaces the old centre `PARRY!` banner). Reduce Motion: state text swap only, no flash.
- Urgency: none. Active window remaining is NOT shown (gap 4); `AKTİF` is binary.
- Interaction: display-only.

**Meat hook**
- Visual: hook glyph + label `KANCA` + `HAZIR` / `BEKLE`, radial fill during cooldown. Same slot style as parry. Hidden when shotgun is not active; the slot keeps its place (no layout shift) and is simply not drawn.
- Update: `OnWeaponSwitched` toggles visibility; readiness from `CanFireAlt` dirty-checked; fill needs NEW cooldown-normalized getter.
- Interaction: display-only.

**Weapon + ammo**
- Visual: bottom-right plate. Top line: weapon name (20px bold) with slot index `[1]`/`[3]` and a weapon glyph whose silhouette differs per weapon (bolt, shell, blade). Large count `cur` (48px) + dim `/max` (20px). Bottom fill bar tinted by ammo type: bolts cyan, shells amber (identity colour only; the name and glyph carry the same info). Melee (Executioner Blade): glyph + `YAKIN`, no count, no bar.
- Update: `OnWeaponSwitched` and `OnAmmoChanged` events set cached strings. Pickup (`OnAmmoCollected`) pulses the count 150 ms scale (Reduce Motion: none).
- Urgency: LOW at ratio < 0.25 (`GetAmmoRatio`): count turns danger #FF4D5E and `!` glyph appears; blink in old AmmoHUD is replaced by a 1 Hz alpha pulse (static under Reduce Motion). EMPTY (0): count `0`, text tag `BOŞ` (20px, danger), bar hollow outline. Cannot rely on colour: `!` and `BOŞ` carry state. Low-ammo threshold data-driven (`lowAmmoRatio`).
- Interaction: display-only.

**Wave counter and Enemies remaining**
- Visual: TR plate. Line 1: `DALGA n/t` (28px). Line 2: `KALAN 7` (24px) + thin bar of remaining/total. Wave name not shown (debug only).
- Update: events only. Verify whether `CurrentWaveIndex` is zero-based; HUD must display 1-based.
- Urgency: when remaining <= 3 the number is bold bone with underline (no colour change needed). Hidden when encounter state is not active; fades out in 300 ms (instant under Reduce Motion) after `OnEncounterCompleted`.
- Interaction: display-only.

**Araf status and Araf rooms**
- Visual: line `ARAF: HAZIR` (crimson sigil + bone text) or `ARAF: KULLANILDI` (hollow grey sigil + dim text). In Araf phase: warning line (existing RunHUD copy, danger colour with `!` glyph), `ODALAR r/t` (24px), `ÇIKIŞ BULUNDU` line with an arrow glyph when `exitRevealed`.
- Update: `RunEvents.OnPhaseChanged`, `OnArafRoomsChanged`; charge state read once on phase change (NEW event on `ConsumeArafCharge` preferred). Newly revealed room: number brightens 200 ms (Reduce Motion: none). `ÇIKIŞ BULUNDU` appearance is accompanied by a one-shot audio cue (owned by audio, not HUD).
- Interaction: display-only.

**Announcement (top-centre)**
- Visual: single text band, 40px bold bone with thin soot plate at 60%; no icon. Title colours are not used for meaning (text carries it).
- Content and keys: see section 8. One at a time, 1.8 s hold, 150 ms fade-in, 300 ms fade-out; unscaled time. Reduce Motion: instant on/off, no slide or scale.
- Interaction: display-only.

**Death card**
- Visual: full-screen soot overlay at 85% alpha (the 65% panel cap applies to HUD plates, not this state), centred title 64px bone, subtitle 24px, one button (P2 states, P1 focus sigil).
- Variants (provisional, see Open Questions): (A) `OnDied` while Araf is available: title `ÖLDÜN`, subtitle `ARAF SENİ BEKLİYOR`, no button, advances when RunDirector triggers Araf transition. (B) `RunEvents.OnTrueDeath`: title `GERÇEK ÖLÜM`, button `TEKRAR DENE` (restart). Cursor unlocks and button is auto-focused so keyboard/gamepad confirm works without a mouse; input method per P1/P2/P6 (no Back action on this card).
- Update: shown on event, hidden on `OnRevived` / scene reload. Fade-in 400 ms (instant under Reduce Motion). Restart logic currently lives in `PlayerDebugHUD`; it must be extracted to a non-HUD component and the button only invokes it (requires extraction in the run/debug code).
- Interaction: interactive (1 button).

### 4.3 Required new read-only data (gaps from inventory)

The HUD must not own state. Implementation adds minimal getters/events; nothing else.

| Need | Requires |
|---|---|
| Overheal cap (to size overflow segment) | requires new read-only getter in `PlayerHealth` (e.g. `OverhealCap`); if cap varies per source, fall back to showing `+n` text only, no segment |
| Dash max charges | requires new read-only getter in `PlayerMovement` (`MaxDashCharges`) |
| Dash recharge progress 0-1 | requires new read-only getter in `PlayerMovement` (`DashRechargeNormalized`) |
| Dash charge changed | requires new event in `PlayerMovement` (`OnDashChargesChanged(int cur, int max)`); until then dirty-checked poll |
| Meat hook cooldown 0-1 | requires new read-only getter in `GothicShotgunWeapon` (`AltCooldownNormalized`) |
| Hook/parry/weapon "ready" events | optional; requires new event in `WeaponBase` / `PlayerParryController`; dirty-checked poll acceptable (zero-GC) |
| Araf charge changed | requires new event in `RunState`/`RunEvents` raised by `ConsumeArafCharge` |
| Low-health threshold | single `lowHealthPercent` in `HudConfig` SO; requires `PlayerDamageFeedback` to read it (replaces its serialized 30) |
| Restart action | requires extraction of restart handler out of `PlayerDebugHUD` |
| Parry window remaining | not needed (binary `AKTİF`) |

Zero-GC rules: no `GetComponent`/`Find` in Update; cache references on enable; subscribe in `OnEnable`, unsubscribe in `OnDisable`; polled values are dirty-checked so TMP text is only touched on change; strings come from the StringTable cache plus a pre-built int string table; no string concat or `string.Format` per frame; no `SetText(float)` per frame.

---

## 5. HUD States by Gameplay Context

| Context | Elements Shown | Elements Hidden | Elements Modified | Transition Into This State |
|---|---|---|---|---|
| Exploration (tower, no encounter) | Health, Dash, Parry, Weapon+ammo, Araf status, Hook (if shotgun) | Wave, Enemies, Araf rooms, Announcement | none | Default on scene start; wave block fades out 300 ms after `OnEncounterCompleted` |
| Combat / encounter | Exploration set + Wave counter, Enemies remaining | Araf rooms | Announcement fires on events; low-health/low-ammo urgency active | `OnEncounterStarted`: wave block appears instantly with `ARENA MÜHÜRLENDİ` banner |
| Araf (labyrinth) | Health, Dash, Parry, Weapon+ammo, Araf status (KULLANILDI), Araf rooms, warning, Hook | Wave counter (unless an encounter runs inside Araf, then it shows in the slot below Araf lines) | Araf warning line in danger colour with `!` | `RunEvents.OnPhaseChanged(Araf)` / `OnEnteredAraf`: instant, rooms line appears |
| Death (OnDied, Araf pending) | Death card variant A | All gameplay HUD | Card overlays | `OnDied`: 400 ms fade (instant under Reduce Motion) |
| True death | Death card variant B + button | All gameplay HUD | Cursor unlocked, button focused | `RunEvents.OnTrueDeath` |
| Paused | No pause menu exists yet. Provisional: HUD stays as-is, update on unscaled time only for banners | none | none | Open Question; when pause menu is added (ux-spec), HUD hides or dims behind it |
| Main menu | HUD absent: HUD prefab is only instantiated in gameplay scenes | all | n/a | Scene load; HUD prefab must not exist in MainMenu scene |
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
| Death card | MUST KEEP | Player needs to know what happened and how to retry | none |
| Seed/floor/timer, wave timer, guards, FPS | ALWAYS HIDE (debug F2/F3) | Not decision-critical | n/a |

---

## 7. Visual Budget

At 1920x1080 (2,073,600 px). Plates counted including padding.

| Budget Constraint | Limit | Measurement Method | Current Estimate | Status |
|---|---|---|---|---|
| Max simultaneous active HUD elements | 10 | Count visible elements in one frame (Health, Dash, Parry, Hook, Weapon, Wave, Enemies, Araf status, Araf rooms, Announcement) | 10 worst case | To verify |
| Max % screen, exploration | 6% | HUD plate pixel area / screen | ~4.9% (BL 420x150, BR 360x140, TR 400x40) | To verify |
| Max % screen, combat | 10% | Same | ~7.5%; Araf with rooms up to ~8.5% | To verify |
| Max % of centre 40% zone | 0% | Only crosshair and OnGUI feedback allowed | 0% (announcement sits at 12-20% height) | To verify |
| Min contrast, text on plate | 4.5:1 (large >= 3:1) | Measure over lightest and darkest scenes with plate at final alpha | bone on soot ~14:1; dim #A09684 ~7:1; danger #FF4D5E ~6:1 | To verify |
| Max opacity of backing plates | 65% (death card exempt) | Material/Image alpha | 60% | To verify |
| Min text size | 20px critical at 1080p (24px for secondary body is not required) | Measure at 1280x720: scaled min ~13.3px, acceptable by proportional scale | 20px | To verify |
| Min icon/pip size | 24px at 1080p | Measure | 24px | To verify |

---

## 8. Feedback & Notification Systems

Only the banner system is a queue; low-health and low-ammo are state changes on their own elements, not notifications.

| Notification Type | Trigger | Position | Duration (ms) | Animation In / Out | Max Simultaneous | Priority | Queue Behavior | Dismissible? |
|---|---|---|---|---|---|---|---|---|
| ARENA SEALED | `WaveSpawnDirector.OnEncounterStarted` | Top-centre | 1800 | 150 fade-in / 300 fade-out | 1 | 2 | Preempts lower | No |
| WAVE n | `OnWaveStarted` | Top-centre | 1800 | same | 1 | 2 | Preempts lower; if fired within 500 ms of ARENA SEALED, merged as second line | No |
| WAVE n CLEARED | `OnWaveCompleted` | Top-centre | 1800 | same | 1 | 3 | Replaced by a newer WAVE n | No |
| ARENA CLEARED | `OnEncounterCompleted` | Top-centre | 2200 | same | 1 | 1 (highest) | Never preempted | No |
| Parry success | `PlayerParryController.OnParrySuccess` | Parry slot (BL), not banner | 250 | glyph flash | 1 | n/a | Coalesces | No |
| Death card | `OnDied`, `OnTrueDeath` | Overlay | until action | 400 fade | 1 | 0 (critical) | Bypasses queue; clears banners | Button only (variant B) |

Rules:
1. Single banner slot. Higher priority number value = lower importance; a more important banner replaces the current one; an equal or less important one waits at most one banner (drop older). No backlog beyond 1 pending.
2. Same-type banners within 500 ms merge (e.g. back-to-back WAVE CLEARED / WAVE n show as `DALGA n TEMİZLENDİ` then `DALGA n+1`, no overlap).
3. Death card is critical: bypasses queue, clears current banner. Scene change clears all.
4. All timing uses unscaled time so hitstop/timeScale does not freeze or stretch banners.

### String Table keys (Turkish, pre-uppercased, existing ScriptableObject StringTable)

Final wording TBD by user; `{0}`/`{1}` formatting done once per event, never per frame.

| Key | Value |
|---|---|
| `hud.health.label` | `CAN` |
| `hud.health.overheal` | `FAZLA CAN` (screen-reader/future use) |
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
| `hud.araf.warning` | existing RunHUD copy (to be moved into table) |
| `hud.araf.rooms` | `ODALAR {0}/{1}` |
| `hud.araf.exitFound` | `ÇIKIŞ BULUNDU` |
| `hud.banner.arenaSealed` | `ARENA MÜHÜRLENDİ` |
| `hud.banner.wave` | `DALGA {0}` |
| `hud.banner.waveCleared` | `DALGA {0} TEMİZLENDİ` |
| `hud.banner.arenaCleared` | `ARENA TEMİZLENDİ` |
| `hud.death.title` / `hud.death.subtitle` | `ÖLDÜN` / `ARAF SENİ BEKLİYOR` |
| `hud.truedeath.title` | `GERÇEK ÖLÜM` |
| `hud.truedeath.restart` | `TEKRAR DENE` |

Weapon display names currently come from `WeaponBase.WeaponDisplayName` (not the StringTable); HUD should look up by weapon slot key (`hud.weapon.<slot>`) instead, or the names must be moved to the table.

---

## 9. Platform Adaptation

| Platform | Safe Zone | Resolution Range | Input Method | HUD-Specific Notes |
|---|---|---|---|---|
| Win64 | 5% inset + `Screen.safeArea` | 1280x720 to 3840x2160, 16:9, 16:10, 21:9 | KB/M primary, gamepad partial | Canvas Scaler: Scale With Screen Size, reference 1920x1080, match height; replaces the non-uniform OnGUI matrix scaling of AmmoHUD/RunHUD. Corner anchoring so ultrawide does not stretch HUD to centre. No input prompts in HUD (display-only, except death button) |

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

UI scaling 75-150% is DEFERRED (carried gap from `design/accessibility-requirements.md`). Design provisions: single `HudRoot` scale, corner-anchored zones, plates sized by content (layout groups with content size fitter), no fixed-width text boxes. At 150% worst case BL (420x150 -> 630x225) and TR (400x190 -> 600x285) still inside the 5% inset on 1280x720 only if the corner can grow inward: to verify.

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
| Banner fade/slide | Mild | Yes | Instant on/off |
| Death card fade | Mild | Yes | Instant |
| Wave block fade-out | Mild | Yes | Instant |

Nothing flashes faster than 1 Hz; no shake anywhere in this HUD (camera shake is outside scope).

### 10.4 Subtitles Specification

N/A: no voiced content (`design/accessibility-requirements.md`). Banners are not subtitles. Revisit if dialogue is added.

### 10.5 HUD Opacity and Visibility Controls

| Setting | Range | Default | Status |
|---|---|---|---|
| Reduce Motion | On/Off | Off | Exists (`GameSettings`); HUD must honour |
| HUD text scale | 75-150% | 100% | Deferred; anchors prepared |
| HUD opacity / minimap / notification verbosity / damage-number toggle / high contrast | n/a | n/a | Not committed at Standard tier (high contrast out of scope); not specified |

---

## 11. Tuning Knobs

All in one `HudConfig` ScriptableObject (no hardcoded gameplay-facing values).

| Parameter | Current Value | Range | Effect of Increase | Effect of Decrease | Player Adjustable? | Notes |
|---|---|---|---|---|---|---|
| `lowHealthPercent` | 0.30 | 0.1-0.5 | Warns earlier | Warns later | No | Single source also for vignette heartbeat |
| `lowAmmoRatio` | 0.25 | 0.1-0.5 | Warns earlier | Warns later | No | Matches old AmmoHUD |
| `lowEnemiesEmphasis` | 3 | 1-10 | Underline earlier | Later | No | Remaining-count emphasis |
| `bannerHoldMs` / `arenaClearedHoldMs` | 1800 / 2200 | 800-4000 | Longer text | Shorter | No | Unscaled time |
| `bannerFadeInMs` / `bannerFadeOutMs` | 150 / 300 | 0-600 | Softer | Snappier | No | 0 when Reduce Motion |
| `bannerMergeWindowMs` | 500 | 100-1000 | More merging | Less | No | |
| `plateAlpha` | 0.60 | 0.3-0.65 | More contrast | More world visible | No | Cap 65% |
| `safeInsetPercent` | 5 | 3-10 | Further from edge | Closer | No | |
| `hudScale` | 1.0 | 0.75-1.5 | Larger | Smaller | Deferred | One root float |
| `lowPulseHz` | 1.0 | 0.5-2 | Faster pulse | Slower | No | Must stay <= 2 Hz |
| `deathFadeMs` | 400 | 0-800 | Slower | Faster | No | |
| `cooldownFillSteps` | 20 | 8-60 | Smoother | Fewer UI updates | No | Dirty-check quantization |

---

## 12. Acceptance Criteria

**Layout & Visibility**
- [ ] At 1280x720, 1920x1080, 3840x2160, 16:10 and 21:9 all HUD elements lie inside the 5% safe inset with no clipping or overlap.
- [ ] Centre 40% x 40% zone contains no UGUI HUD element in any context (screenshot overlay test).
- [ ] HUD area <= 6% of screen in exploration and <= 10% in combat (pixel measurement at 1080p).
- [ ] Hook slot shows only while the shotgun is active and its appearance causes no layout shift.

**Correctness**
- [ ] With `PlayerDebugHUD` OFF, player still sees health, parry, dash, wave counter, banners and death card; with F2 ON no duplicated element overlaps the new HUD.
- [ ] ARENA SEALED, WAVE n, WAVE n CLEARED, ARENA CLEARED each appear on their event, in the top-centre band, once, and match the priority/merge rules.
- [ ] Parry success shows the slot flash and `SAVUŞTURULDU`; no centre text appears.
- [ ] Overheal: health above max shows `+n` and the hatched overflow; healing and damage update within one frame of the event.
- [ ] Low-health state triggers at exactly `lowHealthPercent` and the `PlayerDamageFeedback` heartbeat vignette triggers at the same value (change the SO value and both follow).
- [ ] Death variant B: cursor visible, `TEKRAR DENE` focused, Enter/Space/gamepad A and mouse click restart; all gameplay HUD hidden; `OnRevived`/scene reload clears the card.
- [ ] Araf: `ARAF: HAZIR/KULLANILDI`, `ODALAR r/t` and `ÇIKIŞ BULUNDU` match `RunEvents` payloads.

**Accessibility**
- [ ] All critical HUD text >= 20px at 1080p; contrast >= 4.5:1 over the lightest and darkest scenes.
- [ ] Greyscale screenshot test: health low, ammo low/empty, parry/hook/dash ready vs cooldown, Araf ready/used remain distinguishable.
- [ ] With ReduceMotion ON, no HUD element pulses, flashes, fades or slides; with OFF, pulses are <= 1 Hz.

**Performance & Technical**
- [ ] Profiler: HUD scripts allocate 0 bytes per frame in combat (no GC.Alloc samples in HUD Update/handlers); events unsubscribed on disable.
- [ ] HUD prefab absent from MainMenu scene; all strings come from the StringTable (no literals in the prefab).

---

## 13. Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| No game brief or player-journey docs: flow of first death -> Araf -> true death is assumed (death card variants A/B). Confirm. | user / game-designer | Before approval | Open |
| Does `PlayerHealth` have a single overheal cap? If it varies per source, drop the overflow segment. | game-designer / programmer | Before implementation | Open |
| Actual MaxHealth value; does 30% equal the intended warning point? | game-designer | Before approval | Open |
| Is `CurrentWaveIndex` 0- or 1-based? | programmer | Implementation | Open |
| Where does the restart action live after extraction from `PlayerDebugHUD`? | ui-programmer | Implementation | Open |
| Run seed/floor/timer (RunHUD F3 block) is not migrated: keep in PlayerDebugHUD, or drop? | user | Before approval | Open |
| Pre-wave guard counter and wave timer were debug only: any player-facing need? | game-designer | Before approval | Open |
| Pause menu does not exist: HUD behaviour when paused. | ux-designer | With pause UX spec | Open |
| Death card, as an explicitly navigated screen, may belong in a ux-spec rather than the HUD. | user | Before promotion | Open |
| Does an encounter run inside Araf, and where does the wave block go then? | game-designer | Before approval | Open |
| UGUI vs OnGUI draw order: OnGUI vignette/crosshair render above UGUI Screen Space Overlay (acceptable) but verify the heartbeat vignette does not reduce plate contrast below 4.5:1. | ui-programmer | Implementation | Open |
| UI scaling 75-150% deferred (carried gap), Flash/strobe audit, colourblind palette swap values. | user | Post-graduation / per accessibility doc | Deferred |
| Weapon display names are not in the StringTable. | ui-programmer | Implementation | Open |
