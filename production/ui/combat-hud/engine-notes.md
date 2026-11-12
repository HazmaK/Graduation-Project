# Combat HUD — engine implementation plan

Planning only, 2026-11-12. Unity 6000.5.5f1, URP, uGUI + TMP, New Input System. This document is the only deliverable of this task; all files below are future work.
Authority: approved `design/ux/hud.md`, `visual-design.md`, inventory, project architectural standards, and inspected source/prefab/scene serialization. The visual spec supersedes older 60/65% plates, dim text, danger text, and the 85% death overlay in the UX prose: plates are #0C0A0A at 0.90; death overlay is black at 0.88.

## 1. Engine notes and decisions

### Assemblies and ownership
- Add `Scripts/UI/HUD/RetroFPS.HUD.asmdef`, root namespace `RetroFPS.UI.HUD`, referencing `RetroFPS.Core`, `RetroFPS.Gameplay`, `RetroFPS.Menu`, `UnityEngine.UI`, `Unity.TextMeshPro`, and `Unity.InputSystem`.
- Dependency direction: HUD → Gameplay → Core; HUD → Menu → Core; Editor → HUD; Tests → HUD. Existing Menu references Core/InputSystem/UI/TMP, never Gameplay/HUD. No cycle. Do not put the asmdef at `Scripts/UI/`: CrosshairHUD must remain Gameplay because weapons reference it.
- Put the one `HudConfig` type in Core (`RetroFPS.Presentation`), despite its name: it contains scalar presentation settings only, no Gameplay/Menu/TMP/HUD types. Gameplay's vignette/debug and HUD can then share the exact same asset without Gameplay → HUD.
- Reuse `GameSettings.Current`, `GameSettings.Changed(Action<GameSettingsData>)`, existing `StringTable.Get`/cache, `MenuTheme` palette/font references, and preallocated `MenuTween` instances. No new settings service, localization system, or tween library.
- New serialized fields are `[SerializeField] private`; exposed state uses getters/events. XML-document new types and public members. No `FindObjectOfType`; no heap allocation, component lookups, string formatting, LINQ, closures, or growing collections in Update/FixedUpdate/LateUpdate or callbacks they invoke.

### Canvas, layout, appearance
- One HUD Screen Space–Overlay Canvas, sorting order 100, one CanvasScaler: Scale With Screen Size, 1920×1080, Match Width Or Height = 1. Existing `ArafTransition` owns a separate transition Canvas at order 500; do not duplicate its transition function.
- Hierarchy: `CombatHud/Canvas/HudRoot/SafeArea/{BottomLeft,BottomRight,TopRight,Announcements}`; `Canvas/DeathLayer` is a final full-screen sibling outside the safe-area/scale container. EventSystem is a nonvisual sibling, not another canvas.
- `HudRoot` exposes the single config-driven scale knob (1.0 now, future 0.75–1.5). Recompute root dimensions/position inversely to scale so the child safe rectangle still maps to screen edges; merely scaling a stretched root would move corner anchors off-screen. Scale the death card content independently, never its full-screen backdrop.
- Safe rectangle is the intersection of `Screen.safeArea` and the screen rectangle inset by 5% on every edge. Convert pixels to root-local units. Recalculate only when resolution, safe area, or scale changes; cache the previous values. Corner anchors and pivots match; plates grow inward.
- Reference sizes: BL 400×150; BR 320×120; TR zone 400×190 with a 340-wide plate, heights 40/120/150 for exploration/combat/Araf. Banner 600×60 or 600×96, at 12–20% screen height. Keep the centre 40%×40% clear.
- Reserve parry/hook cells even when their graphics are hidden; do not toggle layout participation and shift other cells. Prebuild N dash pips from MaxDashCharges during binding; no runtime Instantiate in recharge/dash paths.
- Static rects/anchors for small plates; layout groups only for content rows that need them. Avoid nested layout rebuilds and ContentSizeFitter conflicts. Overflow must be resolved within the visual budget, never by shrinking critical text below 20 reference pixels.
- Follow all visual-design.md sizes, fonts, 360p ornament grid and shape cues. Bone/focus text only over gameplay; soot text only on opaque danger/focus chips. Crimson fills need opaque tracks. Warning-chip backing stays opaque while pulsing an outline/highlight, preserving its text contrast.
- Layer order: plates, tracks/fills, glyphs, text, warning chips, banner, death overlay. GraphicRaycaster on Canvas; decorative Images/TMP raycastTarget=false; only death backdrop/button intercept clicks. Gameplay CanvasGroup never blocks raycasts.

### Source binding and lifecycle
- Choose a nested `CombatHud.prefab` child of `Prefabs/Player/Player.prefab`, with player-source references serialized on a player-side `HudBindings` component. It injects sources into the nested HUD; the standalone HUD prefab must not contain illegal external references to the enclosing player prefab.
- Both `Assets/Scenes/TestLevel_Gauntlet.unity` and `SampleScene.unity` instantiate this Player prefab. The same tower player stays loaded while RunDirector preloads Araf additively and changes the active scene. Thus no HUD instance is added to Araf, and no HUD copies are added to tower scenes. MainMenu has no player and gets no HUD.
- Araf alone contains no player/RunDirector; opening that environment scene alone is not a playable run. Test Araf via the tower transition. The builder may validate it without adding a second player or UI.
- SampleScene currently lacks the RunDirector prefab present in Gauntlet: add one RunDirector prefab instance there as the sole target-scene prerequisite edit, so true death/Araf work during direct Play. It will inherit the extracted restart handler. Gauntlet/Araf scene contents need no HUD edits.
- `HudBindings` serializes PlayerHealth, PlayerMovement, PlayerParryController, WeaponController, PlayerAmmoInventory, and the shared config. Cache and validate in Awake, bind/subscribe in OnEnable, unsubscribe in OnDisable. Start takes a full snapshot after gameplay Awake/Start initialization; re-enable also snapshots. Missing local references may be resolved once at Start with `FindFirstObjectByType<T>()`, then remain absent with one diagnostic rather than repeated searches.
- Use `WaveSpawnDirector.ActiveDirector` plus `OnActiveDirectorChanged`, never nearest-room searches or encounter-area entry. On rebind detach the old director, accept null, attach new events, then snapshot without replaying old banners. Araf hides the wave block and clears its banners. Source null/run absent hides only the dependent content.
- Run phase names are `Tower`, `FallingToAraf`, `InAraf`, `EscapingAraf`, `TrueDeath` (not `Araf`). Snapshot `RunEvents.CurrentRun` and `ArafLabyrinthDirector.Active` room getters on binding; thereafter use events. Preserve Araf display across transition phases until Tower returns.
- Wave getter `CurrentWaveIndex` is zero-based (-1 before wave 1), but `OnWaveStarted` and `OnWaveCompleted` already emit one-based numbers. Add 1 only to getter snapshots, never to event payloads. OnEncounterRetried resets counters/queue; let OnWaveStarted announce DALGA 1 exactly once.

### Update strategy and allocation budget
| Element | Trigger and dirty check | Storage/render rule |
|---|---|---|
| Health/overheal | New OnHealthChanged after every health mutation; existing OnDamageTaken/OnHealed for edge feedback, OnDied/OnRevived for snapshots | Snap fill; update integer text only when rounded value changes; normalized fill separately |
| Low health | Same health updates; inclusive HealthPercent <= HudConfig.LowHealthPercent | Shared Core predicate, default 0.30; no separate 30 HP/50/25 checks |
| Dash count | New OnDashChargesChanged; snapshot at bind | Cached n/N text, prebuilt pips |
| Dash recharge | Poll getter; quantize to cooldownFillSteps=20 | Fill write only when bucket changes; full charges hide recharge |
| Parry | Poll ShieldEnabled, IsParrying, IsOnCooldown, CooldownNormalized; OnParrySuccess(bool) | Binary active state, 20-step fill; success word for 250 ms; no active-window getter |
| Hook | OnWeaponSwitched chooses cached shotgun reference; poll CanFireAlt/HookCooldownNormalized only when equipped | HAZIR/BEKLE, no weapon primary cooldown display |
| Weapon/ammo | OnWeaponSwitched, OnAmmoChanged(type,cur,max), OnAmmoCollected(type,added) | Filter other ammo types; AmmoType.None shows YAKIN and hides count/bar; low <0.25, empty=0 |
| Wave/enemies | Director start/wave/remaining/completed/retried events | Contextual counters; remaining <=3 gets underline; completion hide after 300 ms |
| Araf | OnRunChanged, OnPhaseChanged, OnArafChargeChanged, OnArafRoomsChanged | Rooms/exit mutually exclusive with wave block; initial room snapshot prevents missed preload events |
| Banners | Encounter events → fixed current + one pending slot | No growing Queue, no event-time formatting; durations below |
| Death | OnTrueDeath only | Clear banners, hide gameplay group, show/focus death card |
| Debug run data | OnRunChanged caches seed; dirty-check floor and integer elapsed second | F2-only block; draw cached numeric pieces separately, no timer ToString in Update |
- Health presently decays overheal at 3.5 HP/s and optionally autoheals inside Update without OnHealed. New OnHealthChanged covers those paths too; relying on damage/heal events alone would leave a stale display. Do not repurpose OnHealed for decay/regen feedback.
- Prebuild integer strings 0..999, digit strings and 00..59 at initialization; resolve all StringTable keys before combat. Compose counters as separate static prefix/separator and cached numeric TMP fields. For format-bearing banner templates, build bounded variants at binding from actual wave counts, including pair/merge variants; use reusable fixed char buffers for unexpected larger values, never truncation to 999.
- Prewarm TMP text meshes/buffers for maximum supported lengths and Turkish glyphs before showing gameplay. Use cached strings or preallocated char-buffer APIs only on dirty values; no per-frame SetText(float), interpolation, StringBuilder.ToString, dynamic font-atlas growth, material creation, or coroutine allocation on event paths.
- Banner rules: arena cleared priority 1/hold 2200 ms, arena sealed and wave priority 2/1800 ms, wave cleared priority 3/1800 ms; 150 ms in/300 ms out. More important replaces; equal/lower waits unless current has <500 ms left. One pending slot, newer replaces pending; arena cleared cannot be preempted. Merge sealed+wave within 500 ms, and adjacent cleared→next-wave into two lines; coalesce same-kind repeats. True death/scene change clears both slots. Test these explicit cases to resolve the UX section's ambiguous “same-type” wording.
- UI timing/pulses use unscaled time; resource cooldowns retain their existing scaled gameplay timers. Use MenuTween.Tick/Evaluate, but bypass ResolveDuration's reduced 100 ms fade: the HUD requires zero-duration fades under ReduceMotion.
- Read ReduceMotion on enable and subscribe to GameSettings.Changed. A live change immediately finishes fades, removes pulses/flashes/press displacement, preserves parry success text and the 80 ms confirm gate, and makes low-state borders static. No shake; recurring pulses <=1 Hz.

### True death, restart, and input
- Only `RunEvents.OnTrueDeath` opens the card. `PlayerHealth.OnDied`, FallingToAraf and InAraf never open it; HP can show 0 until revive. Do not reconstruct/show the card from Phase alone; normal subscription exists before death. Re-enabling an already shown view may restore its own latched card state.
- Actual restart ownership is split today: PlayerDebugHUD draws/calls PlayerHealth.RestartGame; PlayerHealth handles local R/Space/Enter and retries; RunDirector separately handles true-death keys/reload. Extract the presentation request receiver into `RunRestartHandler`, remove the debug card/call and RunDirector's parallel key handling. Retain PlayerHealth.RestartGame API and non-run retry behavior for compatibility; ExternalDeathControl already gates those keys in a run.
- `TrueDeathView` emits Core `HudRequests.NewRunRequested` only once after confirmation (80 ms unscaled lock/pressed feedback, then request; reduced motion keeps the lock without displacement). Latch until unload, disable button immediately; never mutate run/health/scene state from HUD.
- Gameplay `RunRestartHandler` subscribes to that Core event and calls RunDirector.RestartRun(). RunDirector validates TrueDeath and transition/restart latch, restores timeScale=1/fixedDeltaTime=0.02, then loads its saved towerSceneName with LoadSceneMode.Single. Never reload GetActiveScene() from Araf and never invoke RetryInterceptor for true death. New RunDirector creates a fresh RunState/Araf charge; fixedSeed retains its authored deterministic semantics.
- RunDirector.TrueDeath currently calls `transition.ShowPersistentMessage` (opaque order-500 overlay and old R instruction). Replace that call with `transition.Hide()` before RaiseTrueDeath; the HUD owns the only true-death title/backdrop. Araf fades stay unchanged and cover the order-100 HUD during travel.
- No EventSystem/InputSystemUIInputModule was found in Gauntlet, SampleScene, Araf or their gameplay prefabs by script-GUID scan. MainMenu's MenuEventSystem is scene-local. Bundle an EventSystem+InputSystemUIInputModule in CombatHud; guard it at startup and scene load to ensure exactly one enabled system across additive loading. Never disable an unrelated existing system; disable the bundled duplicate and bind to the existing compatible system.
- Builder serializes importer-owned InputActionReference subassets into module fields (`m_ActionsAsset`, `m_PointAction`, `m_MoveAction`, `m_SubmitAction`, `m_LeftClickAction`, etc.) via SerializedObject; property setters can save fileID:0 references. Use existing Assets/InputSystem_Actions.inputactions UI map; ensure explicit Enter, Space, gamepad buttonSouth Submit bindings and mouse Click (deduplicate the existing */{Submit} binding).
- On open: cursor None/visible, activate card, make button interactable, select it after activation using EventSystem.SetSelectedGameObject, and reapply focus if initialization order requires a next-frame retry using a flag, not a coroutine. Set deselectOnBackgroundClick=false. No Back/cancel handler. PlayerHealth already disables look/movement/weapons on death, preventing click-to-relock.
- Avoid reusing MenuInputRouter/MainMenuController or instantiating the entire menu canvas. MenuFocusVisual depends on menu controller gating and emits menu audio events: reuse its assets/design and MenuTween, implement the small death focus/press behavior in TrueDeathView without a menu controller dependency.

## 2. Exact future file plan

Paths use `P = Graduation Project/Assets/_Project/`, `S = P/Scripts/`, `T = P/Tests/EditMode/`. Expand these prefixes literally. N=create, M=modify, D=delete. Unity generates/updates matching `.meta` files and new-folder metas; preserve existing GUIDs. No implementation files are written by this planning task.
Manifest totals: 46 new files (32 table entries + 14 PNGs), 22 modified files, 2 deleted files, excluding Unity-generated metadata and this planning document. This task itself creates one Markdown file and changes nothing else.

### POCO logic and contracts
| Op | Path | Assembly / namespace | Responsibility |
|---|---|---|---|
| N | S/Core/Presentation/HudNumberCache.cs | Core / RetroFPS.Presentation | Shared prebuilt integer/digit/time pieces and bounded buffer writer, also usable by Gameplay debug |
| N | S/Core/Presentation/HudLowStateRules.cs | Core / RetroFPS.Presentation | Pure inclusive low-health, strict low-ammo and empty predicates with zero-max guards |
| N | S/Core/Presentation/HudRequests.cs | Core / RetroFPS.Presentation | NewRunRequested event + RequestNewRun(), subsystem reset; no scene/UI dependency |
| N | S/UI/HUD/Logic/HealthDisplayModel.cs | HUD / RetroFPS.UI.HUD | Current/base/overflow numeric decomposition and clamped base fill |
| N | S/UI/HUD/Logic/CooldownDisplayCache.cs | HUD / RetroFPS.UI.HUD | Normalized readiness bucket and dirty-state computation |
| N | S/UI/HUD/Logic/HudBannerQueue.cs | HUD / RetroFPS.UI.HUD | Fixed two-slot priority, dedup, merge and unscaled timing |
| N | S/UI/HUD/Logic/TrueDeathGate.cs | HUD / RetroFPS.UI.HUD | Event-latched visibility, 80 ms confirmation gate, one request per card |
| N | S/Combat/DamagePopupLabelPolicy.cs | Gameplay / RetroFPS.Combat | Pure resolved popup kind shared by cache selection, style and scale |

### Data types, authored assets, assembly definitions
| Op | Path | Assembly / namespace | Responsibility |
|---|---|---|---|
| N | S/Core/Presentation/HudConfig.cs | Core / RetroFPS.Presentation | Scalar SO; single lowHealthPercent=0.30 plus UX section 11 knobs and transient durations |
| N | S/UI/HUD/Data/HudTheme.cs | HUD / RetroFPS.UI.HUD | SO references MenuTheme tokens/fonts, adds HUD sprites, track/border/ammo colours and typography |
| N | S/UI/HUD/RetroFPS.HUD.asmdef | HUD / RetroFPS.UI.HUD | Assembly references listed in section 1 |
| N | P/Data/UI/HudConfig_Default.asset | Core HudConfig / n/a | One shared config assigned to HUD, vignette, debug; plateAlpha=0.90 |
| N | P/Data/UI/HudTheme_KanVeKemik.asset | HUD HudTheme / n/a | Links existing MenuTheme_KanVeKemik and menu seal/fonts; HUD tokens/sprites |
| N | P/Data/UI/StringTable_CombatHud_tr.asset | Menu StringTable / n/a | All hud.md section 8 keys plus explicit weapon-slot keys below |
| M | S/Editor/RetroFPS.Editor.asmdef | Editor / RetroFPS.Editor | Add RetroFPS.HUD reference |
| M | S/RetroFPS.Gameplay.asmdef | Gameplay / RetroFPS | Explicit UnityEngine.UI reference for existing ArafTransition's UI types; never add HUD/Menu references |
| M | Graduation Project/Assets/InputSystem_Actions.inputactions | Input System asset / n/a | Persistent UI Submit bindings for Enter/Space/buttonSouth; preserve gameplay maps |
- StringTable type needs no changes: builder authors its serialized entries and calls RebuildCache. Preserve MainMenu table/defaults. Add `hud.weapon.RepeaterArbalest`, `hud.weapon.ExecutionerBlade`, `hud.weapon.GothicShotgun` using current display names; slot lookup uses fixed cached keys, never enum.ToString in combat. Warning copy: `ARAF'TASIN — Çıkış odasını bul` from RunHUD; store authored Turkish casing, never runtime ToUpper.
- Keep all timing/threshold knobs in HudConfig, all colours/fonts/sprites in HudTheme/MenuTheme. Do not introduce a second low-health scalar/fallback constant in Gameplay. Builder validates all three consumers reference the same config asset, including explicit PlayerDamageFeedback on Player so its AddComponent fallback is not needed.

### Views and runtime adapters (MonoBehaviours)
| Op | Path | Assembly / namespace | Responsibility |
|---|---|---|---|
| N | S/UI/HUD/Views/HudBindings.cs | HUD / RetroFPS.UI.HUD | Player-side serialized sources; subscriptions, snapshots, director/run rebinding and settings fan-out |
| N | S/UI/HUD/Views/HudLayout.cs | HUD / RetroFPS.UI.HUD | Safe-area/scale compensation and resolution dirty checks |
| N | S/UI/HUD/Views/HealthHudView.cs | HUD / RetroFPS.UI.HUD | Base HP, +overflow text, low-state chip/border and damage edge |
| N | S/UI/HUD/Views/AbilityHudView.cs | HUD / RetroFPS.UI.HUD | Dash pips/recharge, optional buckler, shotgun hook and parry success |
| N | S/UI/HUD/Views/WeaponAmmoHudView.cs | HUD / RetroFPS.UI.HUD | Localized name/slot, melee state, ammo count/bar/low/empty |
| N | S/UI/HUD/Views/EncounterRunHudView.cs | HUD / RetroFPS.UI.HUD | Wave/remaining/Araf mutually exclusive context rows |
| N | S/UI/HUD/Views/HudBannerView.cs | HUD / RetroFPS.UI.HUD | Cached one/two-line banner graphics, MenuTween opacity |
| N | S/UI/HUD/Views/TrueDeathView.cs | HUD / RetroFPS.UI.HUD | Card, focus sigil, cursor, submit gate and request emission |
| N | S/UI/HUD/Views/HudEventSystemGuard.cs | HUD / RetroFPS.UI.HUD | Bundled-system duplicate guard and active EventSystem binding |
| N | S/Run/RunRestartHandler.cs | Gameplay / RetroFPS.Run | Scene-local Core request subscriber with serialized RunDirector reference |

### Editor builder, prefab and texture outputs
| Op | Path | Assembly / namespace | Responsibility |
|---|---|---|---|
| N | S/Editor/CombatHudBuilder.cs | Editor / RetroFPS.Editor | Idempotent data/prefab builder, player wiring, legacy migration, SampleScene director insertion, validation |
| N | S/Editor/HudPlaceholderArt.cs | Editor / RetroFPS.Editor | Generate the 14 named pixel-mask PNG assets below and configure sprite importers |
| N | P/Prefabs/UI/CombatHud/CombatHud.prefab | HUD components / n/a | Single reusable HUD canvas plus bundled input system, initially hidden death card |
| M | P/Prefabs/Player/Player.prefab | Gameplay + HUD / n/a | Nested HUD, player bindings, explicit damage feedback/config; remove AmmoHUD; serialized debug showHUD=false |
| M | P/Prefabs/Systems/RunDirector.prefab | Gameplay / n/a | Remove RunHUD; add/wire RunRestartHandler; preserve existing run settings |
| M | S/Editor/QuickSetup.cs | Editor / RetroFPS.Editor | Replace AmmoHUD creation with CombatHudBuilder's idempotent player-HUD wiring |
| M | S/Editor/ArafSceneBuilder.cs | Editor / RetroFPS.Editor | Stop recreating RunHUD; attach/wire restart handler when rebuilding director prefab |
| M | Graduation Project/Assets/Scenes/SampleScene.unity | Scene / n/a | Add missing RunDirector prefab instance only; inherit player HUD |
| M | Graduation Project/Assets/Scenes/mert_level.unity | Scene / n/a | Remove its standalone AmmoHUD component before class deletion; set its standalone debug showHUD=false |
- New art files, each at `P/Art/UI/CombatHud/Placeholder/`: `HUD_Plate_9S.png`, `HUD_Chip_9S.png`, `HUD_Bar_Track.png`, `HUD_Bar_Fill.png`, `HUD_Cross.png`, `HUD_Diamond.png`, `HUD_Shield.png`, `HUD_Hook.png`, `HUD_Weapon_Bolt.png`, `HUD_Weapon_Shell.png`, `HUD_Weapon_Blade.png`, `HUD_Sigil_Araf.png`, `HUD_Arrow.png`, `HUD_Hatch.png`. These are 14 files; one Diamond sprite serves solid/hollow/radial variants.
- Reuse `P/Art/UI/MainMenu/Placeholder/Sigil_A_BoneSeal.png` and pressed seal; no duplicate font/texture copies. New sprites: Point, no mipmaps, RGBA32, Clamp; exact masks/sizes/slice borders per visual manifest. Hatch uses Image.Type.Tiled; verify tiling with packed/unpacked sprites and the chosen importer settings.
- Builder follows MainMenuBuilder's saved-scene/dirty-scene guards and setup restoration, operates on prefab contents, saves only changed assets/scenes, preserves GUIDs/authored tuning, and never rebuilds whole gameplay scenes. Reload assets after any NewScene(Single). Build Button/Image/RectTransform dependencies and serialized view refs before activation: Selectable.Awake can run in edit mode. Fully qualify UnityEngine.UI types.
- Legacy removal order: update both editor creators, remove Player/RunDirector/mert serialized components, scan all .cs/.prefab/.unity/.asset references and GUIDs, then delete both classes/metas only when no consumers remain. Current non-class code references are QuickSetup→AmmoHUD and ArafSceneBuilder→RunHUD; current serialized references are exactly the two prefabs plus mert_level's AmmoHUD. No wholesale scene rewrite.

### Minimal existing gameplay changes: exact API additions
| Op / file | Assembly / namespace | Exact change and reason |
|---|---|---|
| M S/Player/PlayerHealth.cs | Gameplay / RetroFPS | Add `event Action<float,float> OnHealthChanged` (current,max), invoked after successful damage/heal/revive and actual decay/autoheal mutations; existing feedback events stay. No global OverhealCap getter: Heal accepts a per-source cap (HealthOrb serializes it; blade passes 125), so select approved +n-text-only fallback without hatch/overflow segment |
| M S/Player/PlayerMovement.cs | Gameplay / RetroFPS | Add `int MaxDashCharges`, `float DashRechargeNormalized` (0→1 recovered fraction, guarded duration, 1 when full), `event Action<int,int> OnDashChargesChanged` (cur,max); emit after spend/recharge and teleport/reset, reset recharge timer on teleport |
| M S/Player/PlayerParryController.cs | Gameplay / RetroFPS | Add `bool ShieldEnabled => enableShield`; keep IsParrying/IsOnCooldown/CooldownNormalized and OnParrySuccess; no active-window/ready event needed |
| M S/Weapons/GothicShotgunWeapon.cs | Gameplay / RetroFPS.Weapons | Add `float HookCooldownNormalized` = clamped remaining `_altCooldownTimer / hookCooldown`, guarded nonpositive duration; 0=ready, view uses 1-value for recovered fill |
| M S/Run/RunState.cs | Gameplay / RetroFPS.Run | Add `event Action<bool> OnArafChargeChanged` (used), emit once on false→true; add `float ElapsedSeconds` and `void AdvanceElapsedTime(float unscaledDeltaSeconds)`, no UnityEngine dependency; ignore negative delta/freeze in TrueDeath |
| M S/Run/RunEvents.cs | Gameplay / RetroFPS.Run | Add `event Action<RunState> OnRunChanged` and `event Action<bool> OnArafChargeChanged`; SetCurrentRun detaches old state event, attaches/forwards new, emits current run and charge snapshot; reset both static events and detach on subsystem reset |
| M S/Run/RunDirector.cs | Gameplay / RetroFPS.Run | Update advances state with unscaled delta in all non-TrueDeath phases; remove direct key restart; add `void RestartRun()` guarded single-load API using saved tower scene; replace persistent death message with transition.Hide before OnTrueDeath |
| M S/Player/PlayerDamageFeedback.cs | Gameplay / RetroFPS | Replace serialized absolute lowHealthThreshold with shared HudConfig reference; both hurt-audio low predicate and heartbeat read HealthPercent via Core rule; OnGUI vignette rendering otherwise stays |
| M S/Player/PlayerDebugHUD.cs | Gameplay / RetroFPS | showHUD=false plus prefab values; F2 includes seed/floor/mm:ss, remove F3 behavior and all combat/parry banners/death rendering/restart call; use shared threshold; no searches from OnGUI; debug panel remains top-left, bounds/scroll prevent overlap with BL/TC |
| M S/Combat/DamageNumberSystem.cs | Gameplay / RetroFPS.Combat | Resolve reflected before crit for label AND style/scale (normalize IsCrit=false when reflected); add `void SpawnAmmoNumber(Vector3 worldPos,int amount,AmmoType type)`; cached +n OK/+n SAÇMA kinds and large-value digit fallback, cleared pool flags on reuse; preserve OnGUI pool/motion |
| M S/Combat/AmmoPickup.cs | Gameplay / RetroFPS.Combat | Call SpawnAmmoNumber(position,added,ammoType), never SpawnDamageNumber(...,true); show actual added amount only when >0 |
| D S/UI/AmmoHUD.cs | Gameplay / RetroFPS.UI | Delete with .meta after all references removed |
| D S/UI/RunHUD.cs | Gameplay / RetroFPS.UI | Delete with .meta after migration of player rows and debug block |
- No changes to WeaponBase, WeaponController, PlayerAmmoInventory (actual path `S/Weapons/PlayerAmmoInventory.cs`), WaveSpawnDirector, CrosshairHUD, hitmarker or combat interfaces are required. Use existing extension points; no general weapon cooldown framework or unrelated mechanics.

### EditMode tests and verification
| Op | Path | Assembly / namespace | Coverage |
|---|---|---|---|
| M | T/RetroFPS.Tests.EditMode.asmdef | Tests / RetroFPS.Tests.EditMode | Add HUD, UnityEngine.UI and Unity.TextMeshPro for model/prefab assertions |
| N | T/HudDisplayModelTests.cs | Tests / RetroFPS.Tests.EditMode | Health 0/max/overheal, zero maxima; health inclusive 0.30 and ammo strict 0.25; cooldown boundaries, numeric >999 and cached output |
| N | T/HudBannerQueueTests.cs | Tests / RetroFPS.Tests.EditMode | Priorities, never-preempt clear, 499/500 ms edges, merge pairs, one pending, duplicate suppression, retry/death/scene reset |
| N | T/HudDeathFlowTests.cs | Tests / RetroFPS.Tests.EditMode | OnDied/Araf never opens; OnTrueDeath opens once; one request after 80 ms; repeat submit ignored, reduced-motion gate preserved |
| N | T/DamageNumberLabelRegressionTests.cs | Tests / RetroFPS.Tests.EditMode | Actual spawned pool entries: crit+reflected→PARRY cyan/no crit scale (including amount 0), normal crit unchanged; heal/4096+/pool reuse flag reset; AmmoPickup Collect yields +actual OK/SAÇMA, never CRIT/HP, full inventory yields none |
| N | T/CombatHudPrefabTests.cs | Tests / RetroFPS.Tests.EditMode | Serialized nested sources/config identity, one canvas/scaler, input refs nonzero after reload, focus wiring, string keys/Turkish glyph coverage, no legacy components/GUIDs, no HUD in MainMenu |
| M | T/RunStateTests.cs | Tests / RetroFPS.Tests.EditMode | Charge event once, run switching detach/snapshot, elapsed accumulation through Araf and freeze on true death, new run reset |
- Popup regression tests inspect private pool/GUIContent via test-side reflection after calling production spawn/collection APIs; assert resolved visual kind and label, not just the pure helper. Avoid a public mutable debug API. No Unity editor/test runs are claimed by this plan.
- Implementation verification: compile Core→Gameplay/Menu→HUD→Editor/Tests; run existing and new EditMode suite, including existing menu input-prefab tests after action changes. Exercise direct Gauntlet/Sample Play, two active rooms, Araf preload/enter/escape/retry, true death while Araf is active, repeat restart, disable/re-enable and domain-reload-disabled subscription cleanup.
- In Play Mode verify Enter/Space/A/click separately, cursor/focus/no Back, one load/request, zero Araf card, seed/floor/timer only F2, no duplicate OnGUI banners. Warm-up then profile HUD allocations on damage/heal/decay, dash/parry/hook/ammo pickup/banner transitions; target 0 B/frame including those paths, not only idle.
- Capture 720p/1080p/4K, 16:10/21:9, 1.0 and prospective 1.5 scale, grey-scale, ReduceMotion toggled live, white flash/fire backgrounds. Measure centre clearance, plate area (exploration <=6%, combat incl banner <=10%), glyph/text size and >=4.5:1 contrast after OnGUI compositing.

## 3. Placeholder assets and open risks
- Real/reused infrastructure: settings service, StringTable, MenuTheme and MenuTween, menu seal, input action asset, gameplay events and player prefab. Existing menu font wiring currently uses LiberationSans SDF plus MainMenu_DynamicFallback; Alegreya Sans SC Bold/Grenze Gotisch final SDFs are not assumed present. Validate Turkish characters, punctuation, +/slash and names and prewarm the existing fallback before combat; final font/art replacement needs no gameplay edits.
- All 14 HUD textures are procedural placeholders with final import/slice contracts. Final pixel glyphs and font assets remain art dependencies; no image-generation or imported art is needed for this planning task.
- Risk 1 — death/restart ownership and draw ordering: existing persistent transition overlay, two keyboard restart paths, additive active-scene selection, UI module reference serialization, and OnGUI draw order can mask/duplicate the card. Centralize requests and explicitly test in Araf and with repeated input.
- Risk 2 — zero-GC correctness: health changes outside events, TMP capacity/fallback growth, debug numeric formatting, large popup values and merge variants require prewarming and end-to-end profiling. A cached label helper alone does not prove 0 B/frame.
- Risk 3 — layout/readability: variable source overheal caps require the approved text-only fallback; long Turkish warning/state strings and N dash pips may exceed BL/TR bounds, especially at 720p/150%. Validate within area/centre budgets; 75–150% user scaling remains deferred. Re-measure 0.90 plate contrast with the retained OnGUI vignette on top.
- Further intentional limits: mert_level is not in build settings and receives legacy-reference cleanup only; pause UX and final colourblind palettes remain deferred. No independent Araf player, soul-gem display, boss bar, stamina, reload, minimap, kill feed or damage-feedback migration is included.
- The project vault records architecture, but this task explicitly permits one output file only. Record the implemented architecture in `documentation/retro-fps` during a later authorized implementation task; do not modify that vault in this planning pass.
