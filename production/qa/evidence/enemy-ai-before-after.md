# Enemy AI redesign: before / after (2026-11-26, Unity 6000.5.5f1 Editor Play Mode)

Baseline: `enemy-ai-baseline.md`. No scene saved, no commits. Tests (EditMode, relevant fixtures only): 219 passed / 0 failed
(AttackTokenChannel 8, MeleeSurroundSlots 10, FlyingEyeAltitude 8, EnemyAggressionProfile 4, GhoulAttackLoop 16, PenitentAttackLoop 43,
PenitentGuard 19, PenitentPrefab 54, FlyingEyeAttackPlanner 6, FlyingEyePrefab 23, EnemyNavigator 26, RuntimeNavMeshBaker 2).

## Tower (TestLevel_Gauntlet, CryptPit room) - before vs after
Protocol deviations (needed, the new AI is far more lethal to a passive player):
- The scene starts a fight within ~2 s of Play (the player took damage and fell into Purgatory before any script could run). Harness pauses
  Play right after entry, teleports the player to (0,0.15,36), heals, unpauses, calls `StartEncounter()`.
- Heal rule tightened to HP < 99% every 100 ms (baseline: < 60%); with the baseline rule the player died within ~18 s in 3 of 4 runs.
- Player is re-anchored to (0,36) every 100 ms when displaced > 1 m or y > 0.8 (ghoul/penitent knockback and standing on enemy heads
  otherwise throws the player into the void / onto enemies, which makes ghouls "Ambush"-idle). 114-159 re-anchors per run.
- Sampling every 100 ms, 42 s. Numbers are 2 runs with anchoring (A / B); earlier un-anchored runs ranged 37-74% slow-chase and are not comparable.

| Metric | Before | After (run A / run B) |
|---|---|---|
| Ghoul attacks started (Windup entries), 42 s | 34 | 83 / 83 |
| Attacks per 10 s (0-10/10-20/20-30/30-40) | 10 / 6 / 14 / 4 | 12 / 22 / 20 / 23 (A); 14 / 19 / 23 / 23 (B) |
| Damage per 10 s (all enemies) | 234 / 277 / 346 / 133 | 230 / 501 / 547 / 547 (A); 330 / 266 / 369 / 464 (B) |
| Total damage | 990 in 34 s (50 hits) | 1940 (108 hits) / 1526 (92 hits) |
| Chase samples at 3-8 m with speed < 0.3 m/s | 42% (98/232) | 2.4% (4/168) / 18.8% (39/208) |
| Idle-like ghouls at 3-8 m (Idle/Patrol/slow chase, not waiting at a slot) | not measured (>= 42%) | 2.3% / 14.4% |
| Idle-state ghouls at 3-8 m (samples) | n/a | 2 / 4 |
| Token InUse mean (Melee / Slam / RangedHeavy) | cap 2, 1-2 | 1.58 / 0.74 / 0.25 (A); 1.59 / 0.77 / 0.00 (B); max 2 / 1 / 1-2 |
| Ghouls waiting at a surround slot at 3-8 m (samples) | n/a | 64 / 70 |

Feel changes beyond the intended idle fix:
- Roughly 2.4x as many ghoul attacks and 1.5-2x damage against a stationary target: the melee token is now freed at recover start, so
  pressure is continuous. Expect to retune tower damage/cap if the old pacing is wanted.
- Most 3-8 m stalls disappear only when the player is held still and not pushed. In the un-anchored runs 37-60% of 3-8 m chasing ghouls
  still stood still in `Pursue` intent with `EnemyNavigator.Status=Complete`, `Destination` equal to the ghoul's own position, 1 corner
  (stale destination; target unchanged). Not caused by the ledge guard (`HasGroundAhead` was true) nor the token wait (`_waitingForToken`
  false). Suspected hand-off from `ExecuteTokenWait` slot moves back to chase (the `_nav` keeps the slot destination; only one exit path calls
  `_nav.Invalidate()`). Open problem, behavioural, not touched.
- Player is now killed in about 2 s by a passive standing target in the tower (was survivable for 34 s).

Screenshot: `enemy-ai-after-tower-01.png` (ghoul filling the view at point blank, HP bar low). Shots 02/03 were discarded: the player had
already been moved to Purgatory by then and showed no enemies.

## Araf arena simulation (Araf.unity opened via EditorSceneManager, never saved)
Setup: Araf has no player of its own, so the Player prefab was instantiated at a navmesh point of the first Ossuary room, HP raised to 1,000,000
at runtime (test instance only). `ArafLevel.SetContentActive(true)` baked the NavMesh (2196 verts). 4 Ghouls (6-9 m ring), 1 Penitent and 1 Eye
placed on a navmesh point 0.7-1.3 m from a navmesh edge (11 m from the player), 1 more Eye. `OnSpawnFromDirector` then
`BeginHunt(player, AggressionProfile_Araf_U2)`; all 7 reported `IsHunting=true`. 30 s sampling.

| Check | Result |
|---|---|
| Engage immediately | first ghoul Windup at 0.8-1.7 s; 47-64 ghoul attacks in 30 s |
| Distinct surround slots used | 4-5 of 8 over 30 s (slots seen [2,0,7,1], [4,6,5,3,2]); 2-3 held at the same time |
| Ghoul idle at 3-8 m | 0 Idle/Patrol samples; slow chase without waiting 5-10 of 64-113 samples (~8%); 1-5 samples waiting at a slot (U2 allows 4 melee tokens, so ghouls usually attack straight away instead of waiting) |
| Penitent at edge | never fell (minY = startY over 30 s), alive |
| Eyes | mean height above ground 3.23 m (profile hover 3.0), never entered Idle/Patrol, minY never below spawn height, no dive; 44 of 188 samples had no ground below (over a gap) with height unchanged |
| Player | fell off the platform at ~18 s in one run (knockback); not an AI issue, but there is no player-side guard |

Screenshots: `enemy-ai-araf-arena-01..03.png` (01 and 03 are sky/void frames from the first aimed pass; 02 shows the engaged ghouls and Penitent).

## GC in AI Update (Profiler, 14 s of combat, un-deep profile, per `[Invoke]` Update sample)
| AI | Tower | Araf hunt | AI-internal call site |
|---|---|---|---|
| BasicEnemyAI.Update | 212 B / 1 event (HUD-shaped 80/70/36/26 B strings under the damage path) | 9,516 B / 156 allocs = 49 x (96/86/36/26 B) one set per player hit | none found; the 4-string sets (96/86/36/26 B) match the HUD `HealthHudView.Render` path from the player's `TakeDamage` (baseline already documented), not AI code |
| FlyingEyeAI.Update | 0 | 3 x 40 B in ~2500 eye-updates (~0.03 B/frame) | `FlyingEyeAI.CanSeePlayer()` `proxHit.transform` (line ~1058) / `hit.transform` (line ~1071): `RaycastHit.transform` creates a 40 B managed Transform wrapper |
| PenitentAI.Update | 208 B (same HUD set) | 528 B / 9 allocs (HUD sets + 1 x 40 B, site not isolated) | one 40 B wrapper, call site not resolved (callstacks were not captured by the profiler) |
| PenitentView.Update | 0 | 0 | none |
So the steady per-frame AI allocation is 0 B for Ghoul, Eye (except the rare Transform wrapper above) and Penitent; the remaining numbers come from the player-damage HUD path.

## Fix 2026-11-26 (lastKnown refresh)
Change under test: non-hunting ghoul without line of sight within 1 m of `_lastKnownPlayerPos` refreshes it to the player position;
`FlyingEyeAI.CanSeePlayer` compares colliders by reference. Compile: 0 errors (console ground truth 0 errors). Tests: GhoulAttackLoop 16,
FlyingEyeAttackPlanner 6, FlyingEyePrefab 23, EnemyAggressionProfile 4, AttackTokenChannel 8 passed, 0 failed.

Run-A protocol in TestLevel_Gauntlet (player 1,000,000 HP, not re-anchored, 45 s, 100 ms samples, ghoul slow = Chase, 3-8 m, < 0.3 m/s):
| Run | slow / Chase 3-8 m samples | slow % | B1 Ambush-arrived | B2 dir < 0.05 | B3 no ground ahead | slow but branch expects movement | player off navmesh |
|---|---|---|---|---|---|---|---|
| 1 | 173 / 735 | 23.5% | 24 | 5 | 23 | 121 | 57 / 364 = 15.7% |
| 2 | 124 / 690 | 18.0% | 14 | 2 | 0 | 108 | 63 / 395 = 15.9% |
Before the fix (run A, earlier, player off navmesh 64%): 61 slow samples with B2 = 20, B1 = 1, B3 = 0. B2 (stale last-known target) is now 2-5 samples, so the fix works.
The remaining slow samples are mostly "branch expects movement" (108-121) with `CanSeePlayer` false in about 80% of them: not explained by
the three zeroing branches, still open. Run 1 also shows 23 B3 (ledge guard) samples, run 2 none.

GC (Profiler, Araf hunt, 14 s): FlyingEyeAI.Update 0 B / 0 allocs over 1358 updates (was 3 x 40 B). Ghoul and Penitent only show the player-damage HUD
string sets (94/84/36/26 B). Penitent still has 1 x 40 B per run.
Deep Profile pass for the Penitent 40 B: NOT obtained. With Deep Profile on, my frame-analysis script ran for more than 20 minutes and blocked the
Editor main thread; the result was never returned. Deep Profile was switched back off and Play Mode stopped. Unresolved; best hypothesis remains the
first-touch managed wrapper for a ground `Collider` read via `RaycastHit.collider` in `PenitentView.BeginSlamTelegraph`.
