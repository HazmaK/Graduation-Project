# Combat HUD Inventory (fact-finding)

Scope: read-only inventory of on-screen HUD elements and the data available to show in a combat HUD.
Source root: `Graduation Project/Assets/_Project/Scripts/`. Paths below are relative to that root.
Positions are for the 1920x1080 reference unless noted. Nothing was run in Unity.

## (a) Existing on-screen HUD elements

| Element | Class | What it draws | Position / anchor | Method | Colours / font | Listens to |
|---|---|---|---|---|---|---|
| Ammo panel | `UI/AmmoHUD.cs` | Title (`BOLTS [1]`, `SHELLS [3]`, or `MELEE`), big `cur / max` count, `EMPTY!` tag, bottom fill bar | Bottom-right, 210x75, 35px margin | OnGUI, matrix scaled by `Screen/1920x1080` (non-uniform) | Bolts cyan (0,0.9,1), shells amber (1,0.7,0.15), empty red; count blinks (PingPong) when ratio < 0.25; font 28 bold count, 13 bold title, 11 bold warning | No events. Polls `WeaponController.ActiveWeapon`, `WeaponBase.RequiredAmmoType`, `PlayerAmmoInventory.GetCurrentAmmo/GetMaxAmmo` every frame |
| Crosshair | `UI/CrosshairHUD.cs` | Centre dot (white, gold on crit hit); slot-specific reticle (Arbalest 4-tick spread, Shotgun brackets + hook pin, Blade arc line); recoil bloom; hitmarker X (red, gold on crit, 0.16 s); kill marker X (red, pale gold on crit, 0.32 s) | Screen centre. Drawn only when cursor is locked | OnGUI, no matrix scaling (raw Screen px) | Hitmarker red (1,0.25,0.25), crit gold (1,0.88,0.15), kill red (1,0.08,0.06) | `CombatEvents.OnEnemyKilled` (subscribed in OnEnable); `WeaponController.OnWeaponHit(bool)`. Bloom is pushed by weapons via direct call `TriggerRecoilBloom(float)`, not an event |
| Run panel | `UI/RunHUD.cs` | `ARAF: HAZIR` / `ARAF: KULLANILDI` line; while in Araf: warning line, `ODALAR r/t` room count, `ÇIKIŞ BULUNDU` when exit revealed; F3 debug block (`SEED`, `KAT`, `SÜRE mm:ss`) | Top-right, 360 wide, y = 35 | OnGUI, matrix scaled to 1920x1080 | Ready crimson (0.86,0.08,0.24), used grey, Araf warning (1,0.3,0.3); font 13/12 bold | `RunEvents.OnTrueDeath`, `OnPhaseChanged`, `OnArafRoomsChanged` (subscribed). Polls `RunEvents.CurrentRun` (Seed, FloorIndex, ArafChargeUsed, Phase) each frame; reads `ArafLabyrinthDirector.Active` on enable. Run timer is computed locally from `Time.unscaledTime` |
| Developer Debug HUD (default ON) | `Player/PlayerDebugHUD.cs` | Top panel: FPS/frame ms/resolution. Player block: `HEALTH n / max HP` + bar (colour at >50 / >25 / else), `DEFENSE [Q]` parry state, `WEAPON` name, `SPEED` + bar, `EYE HEIGHT`, `DASH` pips 0-3, `STATE` (ground/dash/grapple/lunge/meteor/slide/crouch/air). Encounter block: STANDBY/guards, `WAVE n / total - name`, `THIS WAVE` remaining/total + bar, `ENCOUNTER TOTAL`, `WAVE TIMER`, upcoming wave preview. Custom metrics block. Also: centre banners (`PARRY!`, `ARENA SEALED`, `WAVE n`, `WAVE n CLEARED!`, `ARENA CLEARED!`), full-screen `YOU DIED` card with restart button, F2 watermark when hidden | Top-left at (20,20), 380 wide, uniform scale clamp(H/1080, 0.8, 2.5) | OnGUI using GUILayout inside GUILayout.BeginArea; rich-text labels | Health colours #55FF55 / #FFAA00 / #FF3333; state colours per label; banner title colours per event; font 10-32 | `PlayerHealth.OnDamageTaken` (flash only), `PlayerParryController.OnParrySuccess`, `WaveSpawnDirector.OnEncounterStarted / OnWaveStarted / OnWaveCompleted / OnEncounterCompleted`, static `WaveSpawnDirector.OnActiveDirectorChanged`. Polls health, weapons, parry, movement, director every frame. Hotkeys: F2 toggle, B start encounter |
| Damage numbers | `Combat/DamageNumberSystem.cs` | World-space popups that rise and fall: normal, `CRIT! n`, reflected/parry, `+n HP` heal. Pool of 32, lifetime 0.70 s (heal 0.77 s) | Projected from world via `Camera.main`, offset by random jitter | OnGUI, 4-way dark outline + main label, GUIContent pooled | Normal ivory (1,0.9,0.65) 17pt; crit gold (1,0.82,0.12) 24pt, scale 1.4 to 1; reflected cyan (0.2,0.95,1) 21pt; heal green (0.2,1,0.45) 20pt | No events. Called directly by: `RepeaterArbalestWeapon`, `GothicShotgunWeapon`, `ExecutionerBladeWeapon`, `PlayerParryController`, `HealthOrb`, `AmmoPickup` |
| Screen damage feedback | `Player/PlayerDamageFeedback.cs` | Full-screen dark ambient vignette (0.12 alpha), red pain vignette (up to 0.85 alpha on hit, decays 2.4/s), low-health heartbeat vignette (alpha 0.20 to 0.58 pulse at <= 30 HP), green heal vignette flash, up to 4 directional red chevrons orbiting the centre (radius 100 px, 0.7 s) | Full screen; chevrons around screen centre | OnGUI, raw Screen px | Pain (0.75,0.04,0.06); heal (0.10,0.95,0.42); chevron (1,0.15,0.15) | No events. Driven by direct calls: `PlayerHealth.TakeDamage` calls `OnPlayerDamaged(amount, hitPoint, normal, attacker)`; `PlayerHealth.Heal` calls `TriggerHealFlash` |

Not on screen today: enemy health bars, hostile count, minimap or objective marker, stamina (no such system exists), reload indicator (no reload in code), kill feed, damage-dealt numbers on the HUD itself.

## (b) Candidate HUD information items

Key: "event" = C# event exists and can be subscribed; "getter" = public property or method, zero-GC to read; "poll" = HUD must read it every frame because no event exists.

| Item | Exists in code? | Source class.member | Event or getter | Notes |
|---|---|---|---|---|
| Health current | Yes | `Player/PlayerHealth.CurrentHealth` | getter; events `OnDamageTaken(float)`, `OnHealed(float)` | Events carry amounts, not the new total; read the getter inside the handler |
| Health max | Yes | `PlayerHealth.MaxHealth` | getter | Overheal cap is a parameter of `Heal`, not a getter (overheal shown above max needs `overhealCap`) |
| Health percent | Yes | `PlayerHealth.HealthPercent` | getter | Clamped 0-1 |
| Dead / revive | Yes | `PlayerHealth.IsDead`; `OnDied`, `OnRevived` | getter + events | Death screen is already drawn by PlayerDebugHUD |
| Heal received | Yes | `PlayerHealth.OnHealed(float actualHealed)` | event | Good hook for heal numbers / pickup feedback |
| Damage taken amount | Yes | `PlayerHealth.OnDamageTaken(float)` | event | No source or direction in payload |
| Low-health state | Partly | `PlayerDamageFeedback.lowHealthThreshold` (serialized, 30); `PlayerDebugHUD` hard-codes 50 / 25 | none | No getter, no event. Two different thresholds in two HUDs |
| Damage direction | Computed, not exposed | `PlayerDamageFeedback.OnPlayerDamaged` local `signedAngle` | none | Only drawn internally; HUD cannot read it |
| Dash charges | Yes | `Player/PlayerMovement.CurrentDashCharges` | getter (poll) | No charge-change event. PlayerDebugHUD clamps to 0-3 with a fixed table |
| Dash max charges | Not exposed | `PlayerMovement` (private max field) | none | Needed to draw pips generically |
| Dash recharge progress | Not exposed | `PlayerMovement._dashRechargeTimer` (private) | none | Gap |
| Dash started | Yes | `PlayerMovement.OnDashStarted` | event | Dash i-frames: `IsInvincible` getter, `IsDashing` getter |
| Slide state | Yes | `PlayerMovement.IsSliding`; `OnSlideStarted`, `OnSlideEnded` | getter + events | |
| Jump / land | Yes | `PlayerMovement.OnJumped`; `OnLanded(float impact)` | events | |
| Grapple (meat hook) in flight | Partly | `PlayerMovement.IsGrappling`, `GrappleTargetPoint` | getter; only `OnGrappleEnded` event | `StartGrapple` raises no start event |
| Meat hook readiness | Bool only | `GothicShotgunWeapon.CanFireAlt` | getter (poll); `hookCooldown` serialized, `_altCooldownTimer` private | No remaining time, no ready event |
| Horizontal speed | Yes | `PlayerMovement.CurrentHorizontalSpeed`; `WalkSpeed` | getter (poll) | |
| Parry active | Yes | `PlayerParryController.IsParrying` | getter; `OnParryTriggered` event (no subscriber) | |
| Parry window remaining | Not exposed | `PlayerParryController` active-duration field (private) | none | Gap |
| Parry cooldown | Yes | `IsOnCooldown`, `CooldownRemaining`, `CooldownNormalized` | getter (poll) | No "ready" event |
| Parry success | Yes | `PlayerParryController.OnParrySuccess(bool isProjectile)` | event | Subscribed by PlayerDebugHUD |
| Current weapon slot | Yes | `Weapons/WeaponController.CurrentSlot`, `ActiveWeapon` | getter (poll); `OnWeaponSwitched(WeaponSlot)` event | Event exists, no HUD subscribes |
| Weapon display name | Yes | `WeaponBase.WeaponDisplayName` | getter | PlayerDebugHUD uses hard-coded label strings instead |
| Ammo current / max | Yes | `PlayerAmmoInventory.GetCurrentAmmo(type)`, `GetMaxAmmo(type)`, `GetAmmoRatio(type)`, `CurrentBolts`, `CurrentShells`, `MaxBolts`, `MaxShells` | getter; `OnAmmoChanged(type, cur, max)` event | AmmoHUD polls instead of using the event |
| Ammo collected | Yes | `PlayerAmmoInventory.OnAmmoCollected(type, added)` | event | Pickup feedback hook |
| Ammo cost per shot | Yes | `WeaponBase.PrimaryAmmoCost`, `AltAmmoCost`, `RequiredAmmoType` | getter (virtual) | |
| Primary / alt fire readiness | Bool only | `WeaponBase.CanFirePrimary`, `CanFireAlt` (all 3 weapons) | getter (poll) | No remaining time, no ready event |
| Weapon cooldown remaining | Not exposed | `_primaryCooldownTimer`, `_altCooldownTimer` in each weapon (private) | none | Gap for all 3 weapons |
| Full-auto flag | Yes | `WeaponBase.IsFullAuto` | getter | |
| Blade combo step | Yes | `ExecutionerBladeWeapon.CurrentComboStep` | getter | No event |
| Weapon hit (hitmarker) | Yes | `WeaponController.OnWeaponHit(bool isCrit)`; source `WeaponBase.OnHitFeedback` via `NotifyHit` | event | Subscribed by CrosshairHUD |
| Damage dealt value | Not exposed | Weapons call `DamageNumberSystem.SpawnDamageNumber(pos, amount, crit)` directly | none | No event carries dealt damage |
| Crit zone | Yes | `Combat/Hitbox.IsCriticalZone` | getter | Crit flag passed to `WeaponController` as bool only |
| Kill confirmed | Yes | `Core/CombatEvents.OnEnemyKilled(Vector3 pos, bool crit, bool gibbed)` | static event | Raised only by `BasicEnemyAI` (line 1054) and `FlyingEyeAI` (line 837) |
| Killed enemy type | Not exposed | none in payload | none | Gap for kill feed by type |
| Encounter state | Yes | `Combat/WaveSpawnDirector.State` (EncounterState) | getter; `OnEncounterStarted`, `OnEncounterCompleted`, `OnEncounterRetried` | |
| Wave index / total / name | Yes | `WaveSpawnDirector.CurrentWaveIndex`, `TotalWaves`, `CurrentWaveDefinition.WaveName` | getter; `OnWaveStarted(int, int, string)`, `OnWaveCompleted(int)` | |
| Enemies remaining in wave | Yes | `WaveSpawnDirector.EnemiesRemainingInWave`, `TotalEnemiesInCurrentWave` | getter; `OnEnemiesRemainingChanged(int, int)` | Event raised at lines 907, 1080, 1165, 1819. Best source for a wave counter |
| Enemies alive / queued | Yes | `WaveSpawnDirector.ActiveEnemiesCount`, `QueuedEnemiesCount` | getter (poll) | No event |
| Encounter totals | Yes | `TotalEnemiesRemainingInEncounter`, `TotalEnemiesKilledInEncounter`, `TotalEnemiesInEncounter`, `UpcomingWavesTotalEnemies` | getter (poll) | No event; `TotalEnemiesInEncounter` body (line 311) not read, check per-call cost before polling |
| Wave timer | Yes | `WaveSpawnDirector.TimeUntilNextWave`, `WaveElapsedTime`, `StateTimer` | getter (poll) | Per-frame countdown, no event needed |
| Wave threshold (kill %) | Yes | `waveDef.PercentageThreshold`, `RemainingThreshold`, `TransitionMode` | getter on WaveDefinition | |
| Next wave preview | Yes | `WaveSpawnDirector.NextWaveDefinition`, `GetWaveDefinition(i)`, `HasNextWave`, `WaveDefinition.TotalEnemies / DensityRating / CompositionSummary` | getter | Static data |
| Pre-wave guards | Yes | `EncounterArea.RemainingGuardCount`, `RequiredGuardKills`, `KilledGuardsCount`; `WaveSpawnDirector.PreWaveGuards*` | getter; `OnGuardCountChanged(int, int)`, `OnAllGuardsCleared` | PlayerDebugHUD polls, does not subscribe |
| Player entered encounter | Yes | `EncounterArea.OnPlayerEnteredArea` | event | |
| Health orb pickup | Partly | `Combat/HealthOrb.Collect()` (private); `HealthOrb.HealAmount`, `IsActive` | no own event; use `PlayerHealth.OnHealed` | `HealthOrbManager` has no spawn or pickup event |
| Health orb spawn | Partly | `HealthOrbManager.TryDropOrbs`, `SpawnOrb` | none | |
| Run phase | Yes | `Run/RunState.Phase` (RunPhase); `RunEvents.OnPhaseChanged(RunPhase)` | getter + event | |
| Araf entered / escaped | Yes | `RunEvents.OnEnteredAraf`, `OnEscapedAraf` | events | |
| Araf escape portal | Yes | `RunEvents.OnEscapePortalReached` | event | Raised by `ArafEscapePortal` |
| True death | Yes | `RunEvents.OnTrueDeath` | event | |
| Araf charge used | Yes | `RunState.ArafChargeUsed`; `CanEnterAraf` | getter (poll) | `ConsumeArafCharge` raises no event |
| Seed / floor | Yes | `RunState.Seed`, `FloorIndex` | getter (poll) | No event |
| Araf room reveal | Yes | `ArafLabyrinthDirector.RevealedRooms`, `TotalRooms`, `ExitRevealed`, static `Active` | getter; `RunEvents.OnArafRoomsChanged(int, int, bool)` | |
| Run elapsed time | Not in RunState | `RunHUD` computes from `Time.unscaledTime` | none on RunState | Would be needed by any other UI |
| FPS | Yes | `PlayerDebugHUD._currentFps` (private) | none | Debug only |
| Stamina | No | none | none | No stamina system in code |

## (c) Gaps (data with no getter or event)

1. Weapon cooldown remaining, primary and alt, for Arbalest, Blade and Shotgun. Timers are private; only `CanFire*` bools are public.
2. Ready events for weapons, parry and dash. A HUD must poll the bool to know when something becomes ready.
3. Dash recharge progress (`PlayerMovement._dashRechargeTimer`) and max dash charges (HUD hard-codes 3).
4. Parry active window remaining; only `IsParrying` is public.
5. Grapple start event (only `OnGrappleEnded` exists).
6. Damage dealt value. No event carries the amount a weapon dealt.
7. Incoming damage source and direction. `OnDamageTaken(float)` has no payload beyond the amount; direction is computed privately in `PlayerDamageFeedback`.
8. Low-health threshold as a getter or a `LowHealth` event. Thresholds differ: 30 (PlayerDamageFeedback, serialized) vs 50/25 (PlayerDebugHUD, hard-coded).
9. Kill payload has no enemy type or ID. Only `BasicEnemyAI` and `FlyingEyeAI` raise `CombatEvents.OnEnemyKilled`.
10. Encounter-level kill and remaining totals, alive count and queued count have no event. Only the in-wave remaining count has one.
11. `ArafChargeUsed` has no change event.
12. Run elapsed time is not stored on `RunState`.
13. Health orb pickup and spawn events on `HealthOrb` / `HealthOrbManager`.
14. No stamina, reload or ammo-reserve-regen system exists (nothing to show).

## Observations relevant to a HUD rewrite

- **Label precedence bug (confirmed by reading):** `DamageNumberSystem.SetLabel` uses `isCrit ? 1 : isReflected ? 2 : 0`, so the crit style wins. Parry melee (`PlayerParryController.cs:396`, amount 0, crit true, reflected true) and reflected projectiles (`PlayerParryController.cs:307`) therefore render as gold `CRIT! n`, and the cyan reflected style is never used. The `PARRY` label kind is unreachable from the public API.
- **Ammo popup mislabelled:** `AmmoPickup.cs:165-169` calls `SpawnDamageNumber(pos, added, true)`, so ammo pickups show `CRIT! n` in gold. The comment there says `+X SHELLS`.
- **Only health bar is in the debug HUD.** The default-on PlayerDebugHUD (top-left) is the sole on-screen HP bar, parry readout and dash readout. It is labelled as debug, is ON by default, and its `OnGUI` uses interpolated strings and `GUILayout` text, which allocates each frame (`PlayerDebugHUD.cs` around lines 1054, 1070, 1080).
- **Two scaling rules:** AmmoHUD and RunHUD scale non-uniformly to 1920x1080; PlayerDebugHUD scales uniformly by height, clamped. CrosshairHUD and DamageNumberSystem use raw Screen px.
- **Existing events not used by any HUD:** `WeaponController.OnWeaponSwitched`, `PlayerAmmoInventory.OnAmmoChanged`, `PlayerAmmoInventory.OnAmmoCollected`, `PlayerParryController.OnParryTriggered`, `EncounterArea.OnGuardCountChanged`, `WaveSpawnDirector.OnEnemiesRemainingChanged` (PlayerDebugHUD polls the same numbers instead).

## Not investigated

`Combat/EnemyAlertHub.cs`, `Combat/ParryVFX.cs`, `Combat/CombatVFXManager.cs`, `Combat/HitstopManager.cs`, `Player/Feel/PlayerCameraFeel.cs` internals, `Run/ArafEscapePortal.cs` internals, and the body of `WaveSpawnDirector.TotalEnemiesInEncounter`. Mentioned only where they appear in listed call sites.
