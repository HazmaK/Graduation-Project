# Enemy AI baseline (BEFORE change) - TestLevel_Gauntlet, Play Mode

Date 2026-11-26, Unity 6000.5.5f1 Editor Play Mode (not a build). No project files edited, scene not saved.

## Method
- Player teleported (CharacterController toggled) to (0, 0.15, 36), Encounter_Room01_CryptPit `StartEncounter()` called by eval. Player stayed STATIONARY and never fired; yaw not controlled, so screenshots mostly show a wall (enemy-ai-baseline-01..04.png). Treat as a baseline vs a stationary, non-fighting target.
- Player kept alive by calling `PlayerHealth.Heal(9999)` every 100 ms when HP < 60% (no invulnerability toggle exists). Damage figures below come from `OnDamageTaken` and are NOT reduced by the heal, but include ALL enemy types (ghoul melee 10-18, Penitent slam 35, Eye 14 etc.).
- Sampling every 100 ms for 42 s (states aggregated per 0.5 s); ghoul speed = horizontal position delta / real dt. Attack = ghoul state transition into Windup.
- Note: first attempt teleported to y=3.5 (room centre height), player fell to Purgatory and enemies never engaged; discarded.

## Combat metrics (first ~34 s of fight; enemies disengage after ~34 s, 0 damage in 40-42 s)
| Metric | Value |
|---|---|
| Ghoul attacks started (Windup entries) total | 34 in 42 s |
| Attacks per 10 s window (0-10 / 10-20 / 20-30 / 30-40) | 10 / 6 / 14 / 4 |
| Player damage per 10 s window (all enemies, hits) | 234 (11) / 277 (12) / 346 (19) / 133 (8) |
| Total damage 0-34 s | 990 over 50 hit events |
| EnemyAttackTokens.InUse | cap 2, reached 2 (max), usually 1-2 while fighting, 0 once enemies disengaged |
| Ghoul state distribution in-fight (typical 0.5 s sample) | 1-3 Idle, 0-3 Patrol, 1-3 Chase, 0-2 Windup, 0-1 Attack (never Stagger/Dead) |
| Eye state distribution | 2 eyes: Combat / Windup / Reposition alternating, rarely Idle/Patrol after alert |
| Penitent | 3 present (Advance -> SlamWindup -> SlamStrike -> SlamRecover); 2 of 5 are far away unaware in other rooms |

## Ghoul chase locomotion (100 ms samples, ghouls in Chase)
| Bucket | Samples | Speed < 0.3 m/s | Share |
|---|---|---|---|
| Chase, 3-8 m from player | 232 | 98 | 42% |
| Chase, > 8 m | 106 | 15 | 14% |
| Chase, < 3 m | 124 | 22 | 18% |
| All Chase | 462 | 135 | 29% |

Mean horizontal speed: Chase 2.03 m/s, Windup 1.90 m/s, Attack 1.97 m/s (includes lunge/step movement; Windup/Attack means are inflated by knockback/lunge).
Windup distance histogram (m): 0-1: 77, 2-3: 19, 4-5: 8, 6-7: 12 (some windups begin 6-7 m away, i.e. ranged/lunge style start). Attack distance: mostly 1-2 m (87 of 161 samples), tail up to 6 m.
Reading: about 4 in 10 chasing ghouls at mid range (3-8 m) are standing still, consistent with token starvation (cap 2) / queuing behind teammates / re-pathing.

## Qualitative notes
- Ghoul (BasicEnemyAI): alerted by group alert, chases, many hold position at 3-8 m while the two tokens are taken, then close to ~1-2 m, windup (0.4-1 s), melee hit. Constant pressure of ~1-2 simultaneous attackers, bursts of 14 attacks per 10 s with 5+ ghouls.
- Flying Eye (FlyingEyeAI): hovers (altitude via raycast), cycles Combat -> Windup -> Reposition (tangent burst after projectile). Keeps range, fires projectiles; contributes many 10-18 dmg hits.
- Penitent (PenitentAI): slow Advance with NavMesh steering, stops at stoppingDistance, then slam telegraph (SlamWindup) -> SlamStrike (35 dmg area) -> SlamRecover. Roots itself during slam (velocity zero). Needs a token.

## GC allocations in AI Update (Editor, deep profile + GCAlloc memory record mode, ~240 frames ~8 s of combat, player healed)
Allocations were attributed by the callee chain under each AI `Update`.
| AI | AI-internal GC in Update (240 frames) | Per frame | Call site |
|---|---|---|---|
| Ghoul (BasicEnemyAI) | 80 B / 2 allocs (+ damage-path HUD strings, not AI) | ~0.3 B | `RaycastHit.transform` in `BasicEnemyAI.CanSeePlayer()` (called from ExecuteChase) lines ~922/935 (`proxHit.transform`, `hit.transform`) |
| Flying Eye | 160 B / 4 allocs | ~0.7 B | `RaycastHit.transform` in `FlyingEyeAI.ApplyHoverAltitude()` (line ~873: `_groundHits[i].transform`), called from Update line 482 |
| Penitent | 160 B / 4 allocs in CanSeePlayer + 40 B in BeginSlamTelegraph | ~0.8 B | `PenitentAI.CanSeePlayer()` line 294 `Transform t = _sightHits[i].transform;` |

Mechanism: every allocation is a 40 B managed Transform wrapper created by `RaycastHit.transform -> Component.get_transform_Injected()` for a collider whose transform has no cached managed wrapper. It is sporadic, not a steady per-frame cost, in this run. The reported ~91 B/frame for the Penitent was NOT reproduced as a steady value; likely occurs when many ray hits touch not-yet-wrapped transforms (e.g. right after spawn/alert). Fix idea (not applied): use `RaycastHit.collider` identity checks or cache transforms; or compare `hit.colliderInstanceID`-style cached collider sets.
Also seen under the Penitent slam: `PlayerHealth.TakeDamage -> HudBindings.HealthChanged -> HealthHudView.Render -> HudText.Template -> TMP_Text.SetCharArray` allocates ~100 B per damage event (HUD, not AI; ~340 B + 282 B in 6 events).

## Not done / caveats
- Player never moved or shot, so enemies were never killed or staggered; kill/stagger paths not covered.
- Editor Play Mode with deep profiling (inflates frame time); a Mono player build might differ.
- Screenshots do not show enemies (view faces a wall).
- Deep-profiling toggle caused a recompile that surfaced a transient compile error in `ArafRoomSlotTrigger.cs` (another in-progress edit); it resolved itself. Deep profiling was switched back off.
