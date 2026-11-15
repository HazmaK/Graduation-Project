# PURGATOR — in-game pause menu implementation plan

Planning only, 2026-11-15; Unity 6000.5.5f1, URP, UGUI + TMP, New Input System. This task creates this file only; every implementation operation below is future work.
Authority: supplied pause brief and binding user decisions; CLAUDE.md architectural standards; main-menu UX §§3/6/7; interaction patterns P1–P11; accessibility Cognitive Accessibility; inspected runtime code, builders and both prior engine-notes documents.
The brief overrides two existing rules: pause-root Back resumes (MainMenu-root Back remains inert); FallingToAraf/EscapingAraf and true death are excluded despite the accessibility document's “Pause anywhere” row. Record this unmet accessibility target here; do not silently claim compliance or edit that document in this task.

## 1. Design notes

### States, entry, exit and focus
| State | Accepted actions and result | Focus / ownership |
|---|---|---|
| Closed | Fresh Esc or gamepad Start → Root only when eligibility succeeds | Acquire pause before exposing UI; DEVAM / RESUME selected, no click required |
| Root | DEVAM or Esc/B/right-click → Closed; AYARLAR → Settings; ANA MENÜ → ConfirmMainMenu | Exactly three P1 items, explicit wrapping navigation; root alone interactive |
| Settings | Live setting requests; every resolution/window-mode change → RevertPrompt; Esc/B/right-click/GERİ → Root | First row = language, matching main menu; Back restores AYARLAR |
| RevertPrompt | Keep commits; Revert, Esc/B/right-click or expiry reverts → Settings | Trap focus, default Revert; restore the actual window-mode/resolution opener |
| ConfirmMainMenu | EVET → Leaving; HAYIR or Esc/B/right-click → Root | P5, default HAYIR / NO; restore ANA MENÜ on cancellation |
| Leaving | Reject every input; fade to black, then one Single load of MainMenu | Clear selection; loading latch survives until successful unload or explicit failure |

- Start is the global toggle: from any open, non-Leaving state, safely resolve a pending display preview by reverting, save committed settings, then resume. Esc inside the menu is P6 Back, one level per press. B/right-click never open a closed menu. Blocked requests are discarded, never queued for later.
- Eligibility is rechecked immediately before acquisition: gameplay scene, live/nondead player, no latched TrueDeathView.IsOpen, no scene-load latch, and phase Tower or InAraf. No RunDirector is acceptable for a live standalone gameplay test; MainMenu is always denied independently of run presence. FallingToAraf, EscapingAraf and TrueDeath always deny both Esc and Start.
- Snapshot RunEvents.CurrentRun on binding; subscribe to OnRunChanged, OnPhaseChanged and OnTrueDeath and detach symmetrically. Use both phase and death-card latch: HP=0/OnDied is not a substitute for true death. A request arriving during fall/escape does nothing to ArafTransition; its fade/slow motion continues. A request at true death leaves its existing P10 card, arming and focus untouched.
- If death, transition or external loading begins in the same frame after acquisition, that owner wins: cancel pending button work, revert preview, hide pause, release its time/audio ownership without relocking the cursor or reenabling dead-player controls. TrueDeathView.Show first cancels pause synchronously, before assigning card focus; do not rely on static-event subscriber ordering.
- Preserve P2 ~80 ms press lock, ignore repeats during press/fade, and recheck eligibility when delayed actions execute. Pause acquisition itself is immediate. A blocked/held toggle must be released and freshly pressed after eligibility returns. Coalesce Esc+Start+Cancel arriving together to one command per frame.
- P1: arrows/W/S, d-pad/stick, Tab/Shift+Tab wrap; Enter/Space/A/click submit; hover follows the single selection. P5 traps both navigation and pointer input; background clicks cannot clear selection. After panel activation, select the destination; retry once next frame by flag if EventSystem startup/rebinding delayed it.

### Reuse settings and menu presentation
- Extract the existing inline main-menu SettingsPanel into `P/Prefabs/UI/Shared/SettingsPanel.prefab`. Both menus nest that exact asset with the existing SettingsPanelView, MenuSlider and MenuCycleRow components. Reuse GameSettings.Service, ISettingsService, settings copy, ConfirmDialogView and RevertCountdown; no second settings model, cloned implementation, or MainMenuController on gameplay UI.
- Small extraction in Menu: IMenuInputContext exposes InputLocked, DefaultFocus, FindActive and Back; ISettingsPanelHost exposes CanEditSettings, CanPreviewDisplay and OpenDisplayPrompt. MainMenuController and PauseMenuController implement these contracts. SettingsPanelView binds ISettingsPanelHost; MenuFocusVisual binds IMenuInputContext. MenuInputRouter caches an interface from its serialized MonoBehaviour owner; keep the serialized `controller` name compatible with current assets.
- SettingsPanelView exposes read-only group/ordered-selectable access for both hosts. MainMenuController resolves the shared panel within its own canvas at Awake instead of trusting obsolete scene overrides for removed inline settings nodes. Preserve the canvas root/root-list/dialog local IDs and unrelated serialized values during extraction. Both builders use one SharedSettingsPanelBuilder, never copy its row-authoring logic.
- Reuse all current controls: master/music/SFX/UI volume, sensitivity, language, window mode, resolution, reduce motion; apply live, save on Back. Every display change starts 10 real seconds, default Revert; deadline beats an unfinished Keep press. Revert preview on forced close/disable/scene departure. Never use stale Screen dimensions as the committed revert target.
- Reuse MenuTheme_KanVeKemik, existing fonts, sigils, MenuTween and P2 states. Screen Space Overlay pause canvas order 200: HUD order 100 remains visible beneath, Araf transition order 500 remains above. Full-screen MenuTheme.Background scrim at theme.PanelAlpha (currently 0.94), with raycasts blocked while open; content within 5%/Screen.safeArea, 1920×1080 CanvasScaler. Dialogs and leaving fade are later children above the scrim.
- Root item text uses theme.MenuItemSize; all labels, prompts, values and buttons remain ≥24 px at 1080p, including wrapped bilingual confirmation copy. No gameplay art/particle duplication. Sigil + underline + brightness convey focus without colour alone. ReduceMotion applies live: no focus scale or pressed displacement, instant panel changes and ≤100 ms leaving fade; retain the 80 ms input lock and 10 s safety deadline.
- Existing MenuFocusVisual.Press currently offsets text even under ReduceMotion: remove that displacement under the shared setting and immediately reset it when enabled. Keep sigil/brightness feedback.

### Pause service, clocks and gameplay input
- Core/Gameplay owns the engine-independent IPauseService contract and PauseService POCO: IsPaused, SimulationBlocked, GameplayInputBlocked, TryPause, Resume, CancelForExclusiveFlow, PrepareSceneLeave, and Changed; injected environment abstraction captures/applies time, audio and cursor snapshots. SimulationBlocked covers pause and scene-leaving; input also covers the consumed resume frame/release latch. Gameplay PauseRuntime is the sole Unity adapter/lifecycle owner, attached once to Player; GamePause is the Core access point with SubsystemRegistration reset and test injection. UI never writes Time or AudioListener directly.
- Dependency direction: Gameplay → Core; Menu → Core; HUD → Gameplay/Menu/Core; new Pause → Gameplay/HUD/Menu/Core; Editor/Tests → Pause. No Gameplay → Pause/Menu/HUD reference. Phase/load/death eligibility is mapped by adapters into primitive POCO inputs; do not move RunPhase to Core or add a Core → Gameplay dependency.
- At accepted entry snapshot the exact Time.timeScale, Time.fixedDeltaTime, AudioListener.pause and cursor state once; block gameplay input, set timeScale=0, pause gameplay audio, unlock/show cursor. Leave fixedDeltaTime positive and unchanged by pause itself. Normal resume restores the captured scale/fixed step/audio state and locks/hides the cursor for the still-live gameplay owner. Do not hardcode resume to 1 or the physics step to 0.02.
- HitstopManager must stop decrementing its unscaled timer while IsPaused, reject new hitstop requests, and never write timeScale/fixedDeltaTime over pause. Preserve its remaining timer plus underlying pre-hitstop values. Resume restores the instantaneous pre-pause hitstop scale/step; the remaining hitstop then completes normally and restores its original baseline. Pausing during a zero-scale hitstop is still valid; timeScale==0 alone never means menu pause.
- Hitstop disable/destroy during pause cancels its timer and revises the service's saved restoration target to the underlying baseline without touching the live paused clock. Scene departure cancels hitstop without restoring stale snapshots. Capture the normal physics baseline before first hitstop (or from its saved underlying step if already active); loading cleanup must retain the project's authored fixed-step value, not assume 0.02.
- PauseRuntime polls Keyboard.current.escapeKey, Gamepad.current.startButton/buttonEast and Mouse.current.rightButton at an explicit early execution order before gameplay consumers; delegate UI requests through a Core sink contract injected by PauseBindings. Use null checks and press edges only. MenuInputRouter remains focus/Tab/Space fallback owner; its pause-host mode does not subscribe to Cancel/RightClick, so this early reader exclusively owns pause Back. Main-menu routing remains unchanged.
- Remove PlayerLook.Update's Esc cursor toggle. Gate click-to-relock, HandleLook and LateUpdate feel/FOV/trauma while paused; gate PlayerCameraFeel and ProceduralViewmodel so mouse deltas and unscaled animation cannot move the camera/weapon. Gate PlayerMovement before ReadInput/motion, PlayerParryController before input/query side effects, and WeaponController before switch, Tick and primary/alt fire; do not disable components or invoke OnUnequip/reset movement state to pause.
- Consume opening/closing input for the entire frame regardless of script order. GameplayInputBlocked remains true on the resume frame; suppress held fire/alt-fire and action buttons until released so clicking DEVAM, Back/right-click or confirming UI cannot fire, parry, dash or relock unexpectedly. Clear transient sampled input without zeroing saved velocity, dash/grapple state or cooldowns. UI navigation actions remain enabled; never modify InputSystem_Actions.inputactions or globally disable its UI map.
- Physics/scaled waits stop at timeScale=0, but Update still runs. Add cheap pause guards to the existing enemy/encounter/boss and projectile/hazard Update paths listed below, which can attack, hit overlaps or advance state at a zero-duration boundary. Keep current interfaces/WeaponBase and damage contracts; PlayerHealth rejects paused gameplay damage/heal/restart input. No unrelated combat refactor.
- RunDirector must not advance RunState.ElapsedSeconds while paused. Add a pause-aware unscaled gameplay/presentation clock to GamePause, advanced by PauseRuntime only outside pause; its unscaled delta becomes 0 during pause and its absolute value excludes paused duration. It continues through hitstop. HUD health/ability/ammo/encounter effects, banner queue timestamps/fades and crosshair hitmarkers use this clock consistently, preserving remaining durations without catch-up. Menu animations, UI audio throttling and display-revert countdown keep real unscaled time.
- DamageNumberSystem already uses scaled delta, so its popup lifetime/motion freezes naturally; retain that clock. Hide its OnGUI rendering while pause is visible. Also suppress CrosshairHUD, PlayerDamageFeedback and PlayerDebugHUD OnGUI while paused: IMGUI may otherwise composite above UGUI. Keep HUD objects active underneath and preserve their values; freeze debug gameplay shortcuts. Any retained legacy AmmoHUD/RunHUD also suppress drawing during pause; they are not migrated/deleted in this work.

### Audio, scene departure and cleanup
- Choose AudioListener.pause=true for gameplay sources, preserving its previous value on normal resume. This covers pooled CombatAudio and independent encounter/telegraph sources without source enumeration or losing playhead positions. Audit gameplay sources to ensure ignoreListenerPause=false; guard CombatAudio emitters against paused requests. Do not mute by overwriting master/UI settings or AudioListener.volume.
- The pause prefab owns one existing MenuAudioPlayer with its AudioSource.ignoreListenerPause=true, 2D, playOnAwake=false. Configure this in MenuAudioPlayer initialization and the builder. Continue semantic MenuEvents.UiAudio → throttled receiver flow, including volume-preview feedback, respecting UiVolume and master. No extra music source; missing authored clips retain existing silent-placeholder behavior.
- ANA MENÜ confirmation uses the exact copy in §3; EVET latches Leaving once, saves settings and reverts any pending display preview. Validate MainMenu is loadable before fading/loading. Keep the game frozen and UI blocked through the unscaled fade; do not publish MenuEvents.StartRequested/FadeOutComplete or trigger MenuLifecycle's gameplay load.
- After black, PauseRuntime holds the loading/input latch, cancels hitstop, sets timeScale=1, restores the captured normal fixed step, explicitly unpauses AudioListener, clears pause-owned selection and unlocks/shows cursor, then issues one SceneManager.LoadSceneAsync("MainMenu", Single). No gameplay Update may run between cleanup and load completion; retain the service's scene-leaving simulation/input gate through the load. MainMenu loads independently of the active Araf scene and creates no pause instance.
- RunDirector tracks its async Araf preload/scene-load work and restart/transition flags via read-only eligibility state/events. Block opening during actual preload/loading as requested; keep run elapsed behavior unchanged outside pause. Cancel/finish pending Araf work before departure can unload its objects; avoid an outstanding activation-blocked preload stalling a Single load. Do not treat SetActiveScene(Araf) as a newly playable player instance.
- Preflight/load failure restores pause ownership (timeScale=0/audio paused), fades back, shows pause.main_menu_load_error, unlocks menu input and focuses ANA MENÜ. Do not leave a hidden frozen run. OnDisable/OnDestroy cleanup is idempotent: ordinary player teardown releases snapshots; Leaving teardown preserves destination cleanup and never restores hitstop/pause into MainMenu. Exclusive death/transition cleanup preserves its new owner's cursor. Pair subscriptions, rebind after additive events, reset statics with domain reload disabled.
- Zero GC: no new allocations, LINQ, strings/formatting, closures, coroutines, GetComponent or searches in Update/FixedUpdate/LateUpdate or their hot callbacks. Cache references/delegates/arrays/event data in setup; prewarm both languages, countdown strings and TMP capacity before combat. Resolve slider/locale caches outside ticking (precompute both locale variants for live switches); no runtime ToUpper. All serialized fields private; external state read-only. Use existing nonalloc physics APIs if a query is necessary; never FindObjectOfType.

## 2. Exact future file plan

Prefixes expand literally: `P = Graduation Project/Assets/_Project/`; `S = P/Scripts/`; `T = P/Tests/EditMode/`. N=create, M=modify; no deletions. Assemblies abbreviated Core/Menu/Gameplay/HUD/Pause/Editor/Tests mean `RetroFPS.<name>` (`Tests` = `RetroFPS.Tests.EditMode`). Unity-generated matching `.meta`/new-folder metadata accompany future assets and are excluded from counts. This document is the only present write.
Future manifest: 20 created files, 46 modified files, 0 deleted files; 7 of the creates are EditMode test files. Present task: 1 created Markdown file, 0 modified/deleted existing files.

### Contracts, runtime, shared views and editor assembly wiring
| Op | Exact path | Assembly | One-line responsibility |
|---|---|---|---|
| N | S/Core/Gameplay/PauseContracts.cs | Core | IPauseService, environment snapshot/adapter and menu-request sink contracts; no Unity/UI/RunPhase types |
| N | S/Core/Gameplay/PauseService.cs | Core | Pause ownership, eligibility, saved restoration targets, exclusive-flow/loading rules and resume input latches |
| N | S/Core/Gameplay/GamePause.cs | Core | Service/test injection, pause-aware unscaled clock, static reset and allocation-free consumer access |
| N | S/Run/PauseRuntime.cs | Gameplay | Early device polling, Unity time/audio/cursor adapter, run/load guards and Single MainMenu loading |
| N | S/UI/Menu/Logic/MenuHostContracts.cs | Menu | IMenuInputContext and ISettingsPanelHost shared by both controllers |
| N | S/UI/Pause/RetroFPS.Pause.asmdef | Pause | References Core, Gameplay, Menu, HUD, Unity.InputSystem, UnityEngine.UI, Unity.TextMeshPro |
| N | S/UI/Pause/Logic/PauseMenuStateMachine.cs | Pause | Closed/Root/Settings/RevertPrompt/ConfirmMainMenu/Leaving, press lock, countdown and logical focus |
| N | S/UI/Pause/Views/PauseMenuController.cs | Pause | Shared host implementation, focus/context rendering, settings events, input consumption and localized error |
| N | S/UI/Pause/Views/PauseBindings.cs | Pause | Player-side serialized injection of runtime, pause view, health, HUD guard and true-death view |
| M | S/UI/Menu/Views/MainMenuController.cs | Menu | Implement host interfaces and bind shared settings child without relying on removed inline-node scene overrides |
| M | S/UI/Menu/Views/SettingsPanelView.cs | Menu | Replace concrete controller binding; expose ordered controls/group and prewarm both locale value caches |
| M | S/UI/Menu/Views/ConfirmDialogView.cs | Menu | Prebuild both languages' countdown strings during setup, avoiding formatting in live locale callbacks |
| M | S/UI/Menu/Views/MenuFocusVisual.cs | Menu | Bind interface input gate; suppress/reset pressed displacement under ReduceMotion |
| M | S/UI/Menu/Views/MenuInputRouter.cs | Menu | Cache generalized host, accept late EventSystem binding and skip Back subscriptions for pause-host mode |
| M | S/UI/Menu/Views/MenuAudioPlayer.cs | Menu | Initialize UI source ignoreListenerPause=true while preserving semantic events and volume behavior |
| N | S/Editor/SharedSettingsPanelBuilder.cs | Editor | Single reusable settings-panel authoring/extraction, ordered-control and bilingual layout validation |
| N | S/Editor/PauseMenuBuilder.cs | Editor | Idempotent pause prefab/player wiring, key upserts, shared settings extraction and no-scene validation |
| M | S/Editor/MainMenuBuilder.cs | Editor | Delegate settings authoring to shared builder and preserve existing main-menu behavior/IDs |
| M | S/Editor/CombatHudBuilder.cs | Editor | Keep its Player wiring compatible and invoke pause player-wiring after HUD creation without recursive builds |
| M | S/Editor/RetroFPS.Editor.asmdef | Editor | Add RetroFPS.Pause reference |
| M | T/RetroFPS.Tests.EditMode.asmdef | Tests | Add RetroFPS.Pause reference; existing UI/InputSystem references retained |

### Existing gameplay and HUD consumers
| Op | Exact path | Assembly | One-line responsibility |
|---|---|---|---|
| M | S/Combat/HitstopManager.cs | Gameplay | Suspend hitstop timer/requests and arbitrate live writes, deferred restoration and leave cancellation |
| M | S/Player/PlayerLook.cs | Gameplay | Remove Esc cursor toggle; gate click relock/look and LateUpdate unscaled feel |
| M | S/Player/PlayerMovement.cs | Gameplay | Gate input and movement updates without losing velocity/active ability state |
| M | S/Player/PlayerParryController.cs | Gameplay | Gate input, reflection queries and procedural animation while gameplay input is blocked |
| M | S/Player/PlayerHealth.cs | Gameplay | Gate paused health mutations/restart input; retain death/revive ownership and cursor behavior |
| M | S/Player/Feel/PlayerCameraFeel.cs | Gameplay | Freeze unscaled camera effects during menu pause |
| M | S/Weapons/WeaponController.cs | Gameplay | Gate switching, weapon Tick, fire/alt-fire and click relock; require fresh fire after UI close |
| M | S/Weapons/ProceduralViewmodel.cs | Gameplay | Freeze mouse-driven sway, pose and recoil processing during pause |
| M | S/Run/RunDirector.cs | Gameplay | Publish load/transition eligibility, gate elapsed accumulation and coordinate pending Araf work on departure |
| M | S/Combat/CombatAudio.cs | Gameplay | Suppress new gameplay emissions while paused without changing volume or pool ownership |
| M | S/Combat/DamageNumberSystem.cs | Gameplay | Suppress above-menu OnGUI and paused spawns; retain scaled popup lifetime/motion |
| M | S/Player/PlayerDamageFeedback.cs | Gameplay | Suppress OnGUI over pause, retain existing scaled effect timers |
| M | S/Player/PlayerDebugHUD.cs | Gameplay | Freeze debug shortcuts and hide OnGUI over pause |
| M | S/UI/CrosshairHUD.cs | Gameplay | Use pause-aware unscaled marker clock and suppress OnGUI over pause |
| M | S/UI/AmmoHUD.cs | Gameplay | Suppress retained legacy OnGUI over pause; no migration/deletion |
| M | S/UI/RunHUD.cs | Gameplay | Freeze retained legacy elapsed display and suppress OnGUI over pause |
| M | S/Combat/BasicEnemyAI.cs | Gameplay | Skip attack/state Update work while simulation is paused/leaving |
| M | S/Combat/FlyingEyeAI.cs | Gameplay | Skip attack/state Update work while simulation is paused/leaving |
| M | S/Combat/WaveSpawnDirector.cs | Gameplay | Skip spawning/wave boundary work while simulation is paused/leaving |
| M | S/Combat/Boss/BossEncounter.cs | Gameplay | Skip bounds auto-trigger and warmup completion while paused/leaving |
| M | S/Combat/Boss/JudgeBossAI.cs | Gameplay | Gate Update/LateUpdate attack, push and pose work while paused/leaving |
| M | S/Combat/EnemyProjectile.cs | Gameplay | Gate initial-overlap/collision Update side effects at zero movement |
| M | S/Weapons/RepeaterArbalestWeapon.cs | Gameplay | Gate embedded bolt-projectile Update boundary/arrival processing |
| M | S/Combat/Boss/JudgeGuillotineStrike.cs | Gameplay | Gate zero-duration impact boundary and overlap damage |
| M | S/Combat/Boss/JudgeScaleSweep.cs | Gameplay | Gate active sweep overlap damage while paused/leaving |
| M | S/Combat/Boss/JudgeShockLine.cs | Gameplay | Gate overlap damage even when travel step is zero |
| M | S/Combat/Boss/JudgeSwordRing.cs | Gameplay | Freeze LateUpdate sword-ring pose/attachment work |
| M | S/UI/HUD/Views/TrueDeathView.cs | HUD | Cancel pause synchronously before death focus; retain P10 release/arming rules |
| M | S/UI/HUD/Views/HealthHudView.cs | HUD | Use pause-aware unscaled clock for warning pulse/damage-edge deadlines |
| M | S/UI/HUD/Views/AbilityHudView.cs | HUD | Use pause-aware unscaled clock for dash flash/parry-success deadlines |
| M | S/UI/HUD/Views/WeaponAmmoHudView.cs | HUD | Use pause-aware unscaled clock for ammo pulse/pickup flash |
| M | S/UI/HUD/Views/EncounterRunHudView.cs | HUD | Use pause-aware unscaled absolute/delta clock for room flash/completed fade |
| M | S/UI/HUD/Views/HudBannerView.cs | HUD | Use the same pause-aware clock for enqueue, queue expiry and opacity; no catch-up |

### Assets and prefab integration
| Op | Exact path | Assembly / owner | One-line responsibility |
|---|---|---|---|
| N | P/Prefabs/UI/Shared/SettingsPanel.prefab | Menu | Exact shared panel asset used by both menus; all local controls internally wired |
| N | P/Prefabs/UI/PauseMenu/PauseMenu.prefab | Pause/Menu | Canvas, scrim, three root items, nested shared settings, two dialogs, local fade and UI audio |
| M | P/Prefabs/UI/MainMenu/MainMenuCanvas.prefab | Menu | Replace inline settings with shared nested panel through prefab-only extraction |
| M | P/Prefabs/Player/Player.prefab | Gameplay/Pause | Add PauseRuntime/PauseBindings and nested PauseMenu beside existing CombatHud |
| M | P/Data/UI/StringTable_MainMenu_tr.asset | Menu StringTable | Non-destructively upsert §3 keys with both locales; retain existing/unknown entries |

- Standalone PauseMenu contains no external player/HUD references and no EventSystem. Player-side PauseBindings serializes cross-child references at the enclosing Player prefab level and injects them. Use CombatHud's HudEventSystemGuard.ActiveSystem/BundledSystem; rebind when it changes. Do not add a second guard, module or EventSystem; MainMenu has no Player/pause prefab.
- Builder opens prefab contents and saves only changed assets, preserving GUIDs/authored values; no scene-level overrides. Do not run MainMenuBuilder.BuildAll or CombatHudBuilder.BuildAll from PauseMenuBuilder (both have scene-writing behavior); call narrow shared-asset/Player helpers only. Build order: shared settings → main-menu canvas patch → pause prefab → Player wiring. Running builders again must retain one HUD and one pause instance.
- UI module references remain owned by CombatHud: validate persistent m_ActionsAsset, m_PointAction, m_MoveAction, m_SubmitAction, m_CancelAction, m_LeftClickAction, m_RightClickAction and m_ScrollWheelAction through SerializedObject after save/reload. Any helper rebuilding the module must assign importer-owned InputActionReference subassets this way, never runtime references that serialize fileID:0. Space fallback remains deduplicated with Submit by the press latch.
- Build inactive hierarchies with required Image/Selectable dependencies and private serialized references before activation: Selectable.Awake can run in edit mode. Reload asset references after any NewScene(Single), or avoid creating a new scene. Reuse existing art/font/theme/dialog assets; ConfirmDialogView needs no new type. A label written by a view (countdown, cycle/slider value, error) must not also have LocalizedText; static captions/prompts do.
- No .unity file, InputSystem_Actions.inputactions, TimeManager.asset, build settings or localization registry changes. Never modify mert_level.unity. Direct Gauntlet/Sample gameplay inherits Player.prefab; tower player persists through additive Araf. Araf alone stays an environment, without a second player/UI. Existing scene overrides that suppress inherited prefab children are a validation risk, not permission to patch scenes.

### EditMode tests and implementation verification
| Op | Exact path | Assembly | Required evidence |
|---|---|---|---|
| N | T/PauseMenuStateMachineTests.cs | Tests | All six states/actions, Root resume, nested Back, opener focus, Start toggle, press lock, one leave request, deadline beats Keep |
| N | T/PauseServiceTests.cs | Tests | Fake environment snapshots, unusual/zero scales and fixed steps, audio/cursor restore, duplicate calls, exclusive-flow/disable/leave idempotence |
| N | T/PauseEligibilityTests.cs | Tests | Actual adapter phase mapping: Tower/InAraf allowed; fall/escape/death, dead/card-open, MainMenu, Araf preload and scene load denied; fresh-press rule |
| N | T/PauseHitstopTests.cs | Tests | Real HitstopManager: pause halfway, timer/scale stay frozen, exact instantaneous resume then underlying restoration; disable/destroy/leave cannot restore stale values |
| N | T/PauseInputAndClockTests.cs | Tests | Opening-frame and resume-frame gating, held fire/release, no cursor relock, one Back per frame, clock/banner duration freeze and elapsed run exclusion |
| N | T/PauseStringTableTests.cs | Tests | Exact TR/EN new-key manifest, no blank/fallback/duplicates, pre-casing/Turkish glyph coverage, shared settings keys preserved after upsert |
| N | T/PausePrefabTests.cs | Tests | Both menus use same settings asset, ownership, one Player pause/HUD/compatible EventSystem, modal navigation, non-null persistent input refs after reload, ≥24 px and UI audio exception |

- Test service/adapter behavior with injected input/environment samples; invoke real gated consumers where feasible, inspect state and absence of emitted fire/parry/damage. Do not claim EditMode simulates a real OS cursor, audio device or async scene activation; verify those in Play Mode/build. Isolate globals and restore Time/fixed step/listener/cursor/settings/static state in teardown.
- Regression suite includes existing MainMenuStateMachine, settings/display countdown, menu input-prefab, localization and HUD/death tests. Prefab tests fail if required generated assets are missing, never Assert.Ignore. Builder test runs twice in an isolated asset fixture and compares GUIDs, references, counts and untouched scene/action hashes.
- Play Mode/build: Esc/Start from Tower and InAraf; Back at every depth; language change and display timeout while paused; controls held on entry/exit; hitstop at several scales; active boss/projectile/dash/grapple freeze; death/transition same-frame race; true-death arming; additive preload blocking; return from active Araf loads MainMenu once with normal time/audio and free cursor. Verify direct scene Play and domain-reload-disabled reentry.
- Visual/audio/profile checks: 720p/1080p/4K and wide aspect, TR/EN text fit, grayscale focus, bright gameplay through scrim, OnGUI suppression, live ReduceMotion, keyboard/gamepad/mouse paths, paused gameplay voices and audible UI/previews. Warm then profile 0 B/frame in gameplay and paused ticking, including navigation and safety countdown; initialization/load operations are outside the hot-loop budget. This planning task runs no Unity tests or builders.

## 3. New bilingual string keys (pre-cased)

Use existing StringTable + LocalizedText and Core LanguageSetting; retain the current LocalizedStrings combat provider rather than replacing it with a pause-only table. Static pause labels use the shared main-menu table directly, consistent with existing menus. New entries are authored by PauseMenuBuilder through UpsertEntries; StringTable's type/default manifest need no change.

| key | tr | en |
|---|---|---|
| pause.resume | DEVAM | RESUME |
| pause.main_menu | ANA MENÜ | MAIN MENU |
| pause.main_menu_prompt | Ana menüye dönülsün mü? Bu koşudaki ilerleme kaybolacak. | Return to the main menu? This run's progress will be lost. |
| pause.main_menu_load_error | Ana menü yüklenemedi. Tekrar deneyin. | Could not load the main menu. Try again. |

Reuse `menu.settings` = AYARLAR / SETTINGS, `quit.yes` = EVET / YES and `quit.no` = HAYIR / NO. Reuse all `settings.*`, `language.*` and `display.*` keys unchanged; no redundant pause-specific versions. No extra title/hint is introduced that would need an unspecified key.

## 4. Risks

- Highest: time writers (HitstopManager, ArafTransition, PlayerHealth.RestartGame, RunDirector) and teardown order can overwrite pause or resurrect its snapshot. Explicit ownership, synchronous exclusive-flow handoff, hitstop integration tests and a scene-leaving latch are required.
- Settings extraction can invalidate existing scene overrides/local file IDs; patch prefab contents, bind the shared panel locally at runtime, and validate MainMenu without editing its scene. Broad builder entry points currently write scenes and must not be used for pause integration.
- timeScale=0 does not stop Update, raw input, unscaled camera/HUD clocks, IMGUI or zero-step overlap attacks. Listed consumers need guards/clock changes; no pause claim is complete until active combat, held-input and overlay tests pass.
- Araf preload/async cancellation, active-scene changes and duplicate EventSystems can strand loading or focus. Scene departure must coordinate pending work and reuse the guard's active system; no active-scene-based restart/load decision.
- AudioListener.pause affects every listener source; any gameplay source incorrectly marked ignoreListenerPause leaks sound, and unmarked UI becomes silent. Existing optional sound clips may be unassigned; verify real authored clips before claiming audible UI acceptance.
- New Turkish prompt is substantially longer than the quit prompt; wrapped dialog layout, glyph prewarm and ≥24 px floor need visual verification. Shared ReduceMotion fix must preserve main-menu behavior and press locks.
- Accessibility “pause anywhere” remains deliberately unmet for transitions/run end under the binding brief. Existing scene-level Player overrides or standalone legacy UI can defeat prefab inheritance; report failing validation without changing scenes, especially mert_level.unity.
