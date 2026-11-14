# PURGATOR — English / Turkish localization plan

Planning only; no implementation accompanies this document. Target: Unity 6000.5.5f1, URP, UGUI + TMP. Only this file is written for this task.

## Architecture and behavior

- **Authority:** add `LanguageSetting` (English/Turkish; stable codes `en`/`tr`) and a private serialized language field with a read-only getter to `GameSettingsData`. `ISettingsService.SetLanguage` / `SettingsService.SetLanguage` changes the live snapshot, stages committed persistence and emits `Changed` once; unchanged requests are no-ops. `GameSettings.Current.Language` is the sole language authority; views and tables do not own a second locale state.
- **First launch:** the Unity adapter maps exactly `Application.systemLanguage == SystemLanguage.Turkish` to Turkish, every other value (including Unknown) to English. Pass that result into defaults before loading preferences. `PlayerPrefsSettingsStore` reads/writes the exact string key `language`, values `tr`/`en`; absent or invalid codes use the system-derived default. Saved valid codes override the system. Preserve language through every snapshot constructor, Clamp, WithDisplay, volume/sensitivity/motion change, display confirm/revert and reload. Save uses the existing settings-close/application-quit flush policy.
- **Data:** keep one asset per existing UI domain, each containing both languages per entry: private `key`, existing serialized `value` retained as Turkish, new private `en`. This avoids duplicating keys across language assets, preserves Turkish serialization without lossy conversion and keeps all 71 existing keys. Build both ordinal lookup dictionaries in `RebuildCache`; `Get(key, language)` returns stored references, while compatible `Get(key)` delegates to the current settings language. Missing/empty translations fail authoring validation; runtime fallback is Turkish, then the key, with one diagnostic per key/language outside normal rendering.
- **Assembly boundary:** new Core `ILocalizedStringProvider.Get(key, language)` and `LocalizedStrings` registry expose strings to Gameplay; `StringTable` implements the interface in Menu. Core references neither Menu nor HUD. A Menu `LocalizationBootstrap` runs BeforeSceneLoad, calls idempotent `GameSettings.EnsureInitialized`, loads a Resources `LocalizationCatalog` holding a serialized reference to the combat table and registers that provider. This works when starting a gameplay scene directly, without loading the menu scene. Reset the Core registry at SubsystemRegistration; allow explicit provider injection/reset in tests. Existing asmdef references already suffice; do not add Gameplay → Menu or Core → Menu references.
- **Settings UX:** above SES/AUDIO, KONTROL/CONTROLS and GÖRÜNTÜ/DISPLAY add GENEL/GENERAL, first row Dil/Language. Reuse the existing `MenuCycleRow` prefab and P4 keyboard/gamepad/mouse cycling; order is Türkçe then English, with both names invariant across UI languages. Call SetLanguage immediately, keep the current focus, and open no revert prompt. On entering Settings focus Language; display-preview return still restores its actual opener. Replace controller assumptions about indices: Language 0, sliders 1–5, window mode 6, resolution 7, reduce motion 8, Back 9. Keep existing safe modal defaults and navigation traps.
- **Refresh lifecycle:** `LocalizedText` subscribes/unsubscribes to `GameSettings.Changed` in OnEnable/OnDisable, compares the language and assigns the new cached string immediately. Always refresh on enable, including previously hidden labels. Warm both languages' glyphs and TMP capacity at initialization/first rebuild; subsequent warmed Refresh calls perform no managed allocation. Editor preview uses an explicit language lookup, not a persistent runtime override.
- **Menu dynamic text:** SettingsPanelView rebuilds its 21 percentage strings and mode/motion options once on language change, then silently restores all selections and slider values. Resolution dimensions remain invariant. MainMenuController refreshes version and load-error text even when currently hidden. ConfirmDialogView rebuilds 0–10 countdown strings, invalidates `shownSeconds` and redraws the current remaining second without restarting the countdown or changing focus. Use the locale's cached culture at rebuild time; English percent places `%` after the number.
- **HUD refresh:** HudBindings keeps its existing Changed subscription and a last-language guard; on a real language change it invokes each text-owning view's refresh once, then redraws current data. This centralized route also reaches hidden children. LocalizedText handles static labels (health label if present, parry/hook labels, melee text, Araf warning/exit, death title/restart). HealthHudView is numeric-only; TrueDeathView owns gates/motion, not copy; neither needs a string cache. HudLayout/HudEventSystemGuard contain no words. Re-enable snapshots must catch changes missed while disabled.
- **HUD caches:** AbilityHudView re-resolves dash template and parry/hook words, invalidates numeric/state dirty flags and updates an active success word without resetting its expiry. EncounterRunHudView re-resolves wave/remaining/rooms templates and charge words, retains consumed-charge state and redraws unchanged counters. WeaponAmmoHudView re-resolves all three slot names and empty/warning words, redraws the equipped slot/ammo without triggering pickup flashes or changing weapon. HudBannerView rebuilds its 1000 wave and cleared-wave strings once, invalidates rendered identity and redraws current/merged lines; preserve current/pending semantic banners, queue clocks, priorities, holds and fades. Never call Initialize again to refresh: existing early-return guards would skip caches or reset presentation.
- **Popups:** extract pure `DamagePopupTextCache` from DamageNumberSystem's string construction; consume the Core provider and explicit language. Cache the five words below, their spaced prefixes/suffixes and all six 0–4095 label families outside drawing. On Changed, compare language, rebuild once and rebind all active pool entries using stored Kind/Amount, preserving positions, velocities, timers and critical/reflected precedence. Large amounts use the same localized prefix/suffix with preallocated GUIContent/digit pieces; no concatenation, lookup or formatting in Update/OnGUI/spawn. Allocate GUIContent pools only once. OnEnable catches the current language; OnDisable unsubscribes. The current Turkish-authored CRIT!, PARRY and HP tokens remain unchanged; ammo words visibly switch.
- **Transitions:** ArafTransition uses the same Core provider for enter/escape captions and remembers a semantic message kind, so a visible legacy UGUI Text caption updates immediately during FadeOut/FadeIn. RunDirector passes typed Enter/Escape identifiers instead of resolved strings. Language changes do not restart async fades, slow motion or cancellation. The existing true-death card remains the only live death prompt.
- **Performance/style:** allocations are permitted only during initialization and discrete language-cache rebuilds; steady-state rendering and unchanged-language settings events allocate zero. Retain fixed char buffers, dirty checks and numeric caches; never rebuild per frame or fetch components there. Store all strings pre-cased; never runtime ToUpper/ToUpperInvariant (Turkish İ/ı). Require ÇçĞğİıÖöŞşÜü, punctuation and both alphabets in TMP fonts and the transition/IMGUI font. All new serialized fields are private with getters, APIs have XML docs, pure policy/cache logic stays separate from Unity views, and the row is prefab-authored.
- **Builders/migration:** replace Turkish-only menu seeding and combat SerializedObject seeding with keyed bilingual upserts; fill absent/empty cells independently, preserve non-empty authored values and unknown keys, reject duplicates, rebuild caches once and mark/save only changes. Keep PopulateTurkishDefaults as a compatibility wrapper around the new non-destructive bilingual defaults method. Validate both languages, placeholder parity, glyphs and capacity; two consecutive population runs must produce identical entries/GUIDs. CombatHudBuilder also creates/updates the catalog and its combat-table reference. MainMenuBuilder produces the General row and the revised navigation/wiring; fit the added group/row inside the safe area at 720p using spacing/layout, retaining existing text-size floors.
- **Asset identity:** keep `StringTable_MainMenu_tr.asset` and `StringTable_CombatHud_tr.asset` at their current paths, with their current names and .meta GUIDs; the `_tr` suffix becomes legacy naming. Do not delete/recreate either table or change StringTable.cs's GUID/class/assembly. Existing prefab references survive. Generate the added row inside MainMenuCanvas; the existing MainMenu scene gets only builder-owned prefab-instance wiring changes. Catalog is a reference asset, not a second translation source.

## Runtime string inventory and exclusions

- Scanned all project runtime C# text assignments, SetText, GUI.Label/Button, GUILayout/OnGUI and display-name consumers, plus prefab/scene text fields; Editor, tests and PlayerDebugHUD were excluded from player-copy extraction. No additional runtime gameplay script outside `_Project/Scripts` draws player words.
- Menu: table-bound LocalizedText; SettingsPanelView percentage/cycle caches; MainMenuController load-error/version; ConfirmDialogView countdown. MenuCycleRow only renders supplied options; `<`/`>` arrows are invariant glyphs.
- HUD: all table-bound labels and caches described above, including the nine existing combat keys beyond the spec's main list (extra labels, slot names and symbols). Numeric templates in HealthHudView (`{0}<size=20> / {1}</size>`, `+{0}`), WeaponAmmoHudView (`[{0}]`, `{0}<size=24> / {1}</size>`) and HudText digits contain no words and remain invariant.
- DamageNumberSystem.cs: both InitializeLabels and SetLabel's large-number path contain `CRIT! `, `PARRY `, ` HP`, ` OK`, ` SAÇMA`; replace all occurrences through the five popup keys, retaining spaces in cache assembly. Plain damage numbers and `+` are invariant. DamagePopupLabelPolicy classifies kinds only; no translation or behavior change is needed there.
- Run/ArafTransition.cs: live `ARAF` and `KAÇIŞ` are reached from RunDirector's entry/escape flow. Serialized `GERÇEK ÖLÜM\nYeniden başlamak için R` / TrueDeathMessage and ShowPersistentMessage have no runtime caller; retain as dormant compatibility data, never enable that obsolete restart instruction. RunDirector.TrueDeath hides the transition and raises the HUD card event.
- CrosshairHUD and PlayerDamageFeedback OnGUI draw geometry/textures only. WeaponBase/three weapon classes' English WeaponDisplayName defaults and WaveData's `Wave 1` do not reach the player HUD: it resolves slot keys and numbered wave templates; names otherwise feed debug/logging. Inspector attributes, object names, logs, validation messages and TutorialInfo Readme are developer/editor data.
- F2 PlayerDebugHUD remains English-only. Obsolete AmmoHUD/RunHUD remain excluded and unused. No new translated debug copy is proposed.

## Exact future file plan

Paths below are exact relative paths: **S** = `Graduation Project/Assets/_Project/Scripts/`, **A** = `Graduation Project/Assets/_Project/`, **T** = `Graduation Project/Assets/_Project/Tests/EditMode/`. Counts exclude automatically generated .meta files/folders. This implementation plan has **9 new + 36 modified files**; this task itself creates only `production/ui/localization/plan.md`.

| Action | Path | Responsibility |
|---|---|---|
| Create | S`Core/Settings/LanguageSetting.cs` | Language enum and pure code/default validation policy. |
| Create | S`Core/Localization/ILocalizedStringProvider.cs` | Explicit-language string lookup contract shared with Gameplay. |
| Create | S`Core/Localization/LocalizedStrings.cs` | Provider registration, lookup, reset and injection; no Menu dependency. |
| Create | S`UI/Menu/Data/LocalizationCatalog.cs` | Private serialized combat StringTable reference for boot composition. |
| Create | S`UI/Menu/Logic/LocalizationBootstrap.cs` | BeforeSceneLoad catalog load/registration, including direct gameplay boot. |
| Create | S`Combat/DamagePopupTextCache.cs` | Pure locale-dependent cached labels/prefixes/suffixes. |
| Create | A`Resources/Localization/LocalizationCatalog.asset` | Builder-generated provider reference retained in builds. |
| Create | T`LanguageDefaultsTests.cs` | Exhaustive system-language mapping and preference-code fallback coverage. |
| Create | T`LocalizationLiveSwitchTests.cs` | Real label/menu/HUD/transition refresh, lifecycle, boot and warmed-allocation checks. |
| Modify | S`Core/Settings/GameSettingsData.cs` | Language field/getter; preserve it in defaults, validation and snapshot copies. |
| Modify | S`Core/Settings/SettingsContracts.cs` | Add SetLanguage to ISettingsService. |
| Modify | S`Core/Settings/SettingsService.cs` | Immediate language change, committed persistence and one Changed notification. |
| Modify | S`Core/Settings/UnitySettingsAdapters.cs` | System-language adapter and `language` PlayerPrefs string round-trip. |
| Modify | S`Core/Settings/GameSettings.cs` | Supply system-derived language to startup defaults. |
| Modify | S`UI/Menu/Data/StringTable.cs` | Bilingual entries/lookups, Core interface and idempotent bilingual menu defaults. |
| Modify | S`UI/Menu/Views/LocalizedText.cs` | Settings-driven label refresh with balanced subscriptions and warm capacity. |
| Modify | S`UI/Menu/Views/SettingsPanelView.cs` | Language cycle plus language-aware percentage/mode/motion cache refresh. |
| Modify | S`UI/Menu/Views/MainMenuController.cs` | Revised focus indices/Back binding; refresh dynamic version/error copy. |
| Modify | S`UI/Menu/Views/ConfirmDialogView.cs` | Rebuild and redraw countdown without changing its deadline. |
| Modify | S`UI/Menu/Logic/MainMenuStateMachine.cs` | Add Language focus target and use it when opening Settings. |
| Modify | S`UI/HUD/Views/HudBindings.cs` | Dispatch guarded language refresh and redraw to initialized views. |
| Modify | S`UI/HUD/Views/AbilityHudView.cs` | Refresh words/templates while retaining cooldown/success state. |
| Modify | S`UI/HUD/Views/EncounterRunHudView.cs` | Refresh counters and consumed-charge copy without presentation resets. |
| Modify | S`UI/HUD/Views/WeaponAmmoHudView.cs` | Refresh slot identity and ammo warnings without pickup/weapon side effects. |
| Modify | S`UI/HUD/Views/HudBannerView.cs` | Rebuild localized variants and redraw live queue without resetting timers. |
| Modify | S`Combat/DamageNumberSystem.cs` | Use pure cache/Core provider and relabel live pooled popups on change. |
| Modify | S`Run/ArafTransition.cs` | Localize typed active enter/escape captions and subscribe to settings. |
| Modify | S`Run/RunDirector.cs` | Pass semantic Enter/Escape message identifiers to transition presentation. |
| Modify | S`Editor/MainMenuBuilder.cs` | Seed both languages and build first General/Language row, layout/navigation/wiring. |
| Modify | S`Editor/CombatHudBuilder.cs` | Bilingual combat/popup/transition upserts, catalog generation and glyph checks. |
| Modify | A`Data/UI/StringTable_MainMenu_tr.asset` | Preserve 32 keys and GUID; add English values and four settings/language keys. |
| Modify | A`Data/UI/StringTable_CombatHud_tr.asset` | Preserve 39 keys and GUID; add English values, five popup and two transition keys. |
| Modify | A`Prefabs/UI/MainMenu/MainMenuCanvas.prefab` | Generated General group, language row and silent settings bindings. |
| Modify | `Graduation Project/Assets/Scenes/MainMenu.unity` | Builder-owned instance wiring for ten selectable settings items. |
| Modify | T`SettingsServiceTests.cs` | Persistence/events and language preservation across all setting/display transactions. |
| Modify | T`StringTableTests.cs` | Explicit bilingual defaults, full key manifest, duplicates, fallback and builder idempotence. |
| Modify | T`MainMenuStateMachineTests.cs` | Language initial focus; preserve display opener/safe-default behavior. |
| Modify | T`DamageNumberLabelRegressionTests.cs` | Both languages, active-pool relabel and cached/large-amount equivalence. |
| Modify | T`CombatHudTextFitTests.cs` | Both-language rendered strings, longest names, warning and formatted counter fit. |
| Modify | `design/ux/main-menu.md` | Replace Turkish-only assumption; General row, keys and expansion budgets. |
| Modify | `design/ux/hud.md` | Bilingual keys, cache-refresh contract and missing character-budget clarification. |
| Modify | `design/ux/interaction-patterns.md` | Extend P4 to language; immediate apply with no display-revert behavior. |
| Modify | `documentation/retro-fps/MainMenuAndSettings.md` | Record authority, persistence, provider bootstrap and navigation changes. |
| Modify | `documentation/retro-fps/CombatHudArchitecture.md` | Record language cache refresh and popup/transition presentation dependencies. |
| Modify | `documentation/retro-fps/CodebaseDependencyGraph.md` | Record Menu implementation → Core contract ← Gameplay consumer edges. |

## Verification planned for implementation

1. EditMode defaults: parameterize every SystemLanguage value; Turkish alone → tr, all others → en. Cover missing, empty/unknown saved codes and valid saved code overriding the system; restore only touched PlayerPrefs keys, never DeleteAll.
2. EditMode service/persistence: SetLanguage changes live/committed snapshots, writes `language`, forwards exactly one event and leaves display/audio untouched; identical language causes no extra event. Save/recreate round-trips both codes. Pending display Save/Revert/Confirm and every other setter preserve language and never commit an unconfirmed display.
3. EditMode data: enumerate both actual serialized tables against the 82-key manifest below (36 menu, 46 combat), require non-whitespace tr AND en without consulting fallback, reject duplicate keys, verify placeholder indices/types and enforce available length limits. Preserve original 71 Turkish values. Populate twice; compare values/key count and asset GUIDs; authored non-empty edits survive. Assert catalog references the actual combat asset and the provider is ready before gameplay Awake in a direct-scene launch.
4. EditMode live label: use a real TMP label + LocalizedText with an injected settings service, bind/warm TR and EN glyphs/meshes, then switch and assert the already-visible text changes without reenabling it. After the first cache/TMP rebuild, measure repeated warmed Refresh/SetLanguage label updates with a per-thread allocation counter; assertions/setup/reflection are outside the measured interval. Require 0 managed bytes for that label path; do not apply this claim to an entire language event rebuilding banner/popup formatted caches. Verify disabled labels catch up on enable and repeated enable/disable leaves one/no subscriptions.
5. EditMode live state: switch while countdown, parry success, unchanged counters, empty ammo, merged/current/pending banners, Araf transition and armed true-death card are visible. Assert new copy plus identical deadlines, queue semantics, charges, weapon, focus, fade progress and death confirmation latch. Verify hidden labels before their next reveal, and non-language settings events do not rebuild caches.
6. EditMode popup production path: spawn all six kinds in TR, switch EN with entries still alive, then switch back; assert `+n OK` ↔ `+n BOLTS`, `+n SAÇMA` ↔ `+n SHELLS`, plus localized critical/parry/health words and unchanged normal digits. Check amounts 0, 4095, 4096 and int.MaxValue, reflected+critical precedence, pool reuse, active timers and multipart digit reconstruction. Warm caches before measuring allocation-free spawn/draw preparation.
7. Runtime/UI: after appropriate EditMode suites pass, inspect both languages at 1280×720, 1920×1080, 4K, 16:10 and 21:9; retain implementation evidence for menu/settings/dialogs/HUD/banners/death/Araf/popups. Check no clipping, English warning wraps within two lines, menu expansion budget ≥40%, Turkish glyphs and IMGUI font coverage. Profile 0 B steady-state HUD/popup frame allocations; measure the discrete popup/banner rebuild spike separately. Do not claim runtime validation from this planning-only task.

## Full bilingual string table

Existing Turkish cells below are the authored asset values, decoded from Unity YAML; new rows use current runtime tokens or the binding user decision. All keys are retained verbatim. MainMenu has 32 existing + 4 new; CombatHud has 39 existing + 7 new = **82 total keys**.

Limits: main-menu §13 specifies title 16, menu actions 14, settings labels 26, cycle values 18, dialog prompts 40, dialog buttons 10, load errors 40. Apply the settings-label budget to settings headings/groups as well. `—` means no character maximum is specified (version/percent formats, HUD §8, popups/transitions); do not invent a spec limit. CombatHudBuilder's existing >200-character authored-template guard and HudText's 256-char buffer are technical capacity checks, not UX limits. Validate substituted numeric values and rich-text output separately. **No proposed English value exceeds a specified limit; HUD/popup/transition fit remains a measured layout check.**

### MainMenu asset (36 keys)

| key | tr (current authored value; new where noted) | en (proposed) | max chars |
|---|---|---|---|
| menu.title | PURGATOR | PURGATOR | 16 |
| menu.start | BAŞLA | START | 14 |
| menu.settings | AYARLAR | SETTINGS | 14 |
| menu.quit | ÇIKIŞ | QUIT | 14 |
| menu.version_format | v{0} | v{0} | — |
| menu.load_error | Seviye yüklenemedi | Level could not be loaded | 40 |
| settings.title | AYARLAR | SETTINGS | 26 |
| settings.master_volume | Ana Ses | Master Volume | 26 |
| settings.music_volume | Müzik | Music | 26 |
| settings.sfx_volume | Ses Efektleri | Sound Effects | 26 |
| settings.ui_volume | Arayüz Sesi | UI Volume | 26 |
| settings.group.audio | SES | AUDIO | 26 |
| settings.group.controls | KONTROL | CONTROLS | 26 |
| settings.group.display | GÖRÜNTÜ | DISPLAY | 26 |
| settings.mouse_sensitivity | Fare Hassasiyeti | Mouse Sensitivity | 26 |
| settings.window_mode | Pencere Modu | Window Mode | 26 |
| settings.window_mode.fullscreen | Tam Ekran | Fullscreen | 18 |
| settings.window_mode.borderless | Çerçevesiz | Borderless | 18 |
| settings.window_mode.windowed | Pencere | Windowed | 18 |
| settings.resolution | Çözünürlük | Resolution | 26 |
| settings.reduce_motion | Hareketi Azalt | Reduce Motion | 26 |
| settings.on | AÇIK | ON | 18 |
| settings.off | KAPALI | OFF | 18 |
| settings.back | GERİ | BACK | 14 |
| settings.percent_format | %{0} | {0}% | — |
| display.revert_title | Görüntü ayarları korunsun mu? | Keep these display settings? | 40 |
| display.revert_countdown_format | {0} saniye içinde geri alınacak | Reverting in {0} seconds | 40 |
| display.keep | Onayla | Keep | 10 |
| display.revert | Geri al | Revert | 10 |
| quit.prompt | Oyundan çıkılsın mı? | Quit the game? | 40 |
| quit.yes | EVET | YES | 10 |
| quit.no | HAYIR | NO | 10 |
| settings.group.general | GENEL | GENERAL | 26 |
| settings.language | Dil | Language | 26 |
| language.tr | Türkçe | Türkçe | 18 |
| language.en | English | English | 18 |

### CombatHud asset, including shared gameplay words (46 keys)

| key | tr (current authored value; new where noted) | en (proposed) | max chars |
|---|---|---|---|
| hud.health.label | CAN | HEALTH | — |
| hud.dash.label | ATILMA | DASH | — |
| hud.parry.label | PARRY (was SAVUŞTUR; did not fit, user decision 2026-11-14) | PARRY | — |
| hud.parry.ready | HAZIR | READY | — |
| hud.parry.active | AKTİF | ACTIVE | — |
| hud.parry.cooldown | BEKLE | WAIT | — |
| hud.parry.success | SAVUŞTURULDU | PARRIED | — |
| hud.hook.label | KANCA | HOOK | — |
| hud.hook.ready | HAZIR | READY | — |
| hud.hook.cooldown | BEKLE | WAIT | — |
| hud.ammo.empty | BOŞ | EMPTY | — |
| hud.weapon.melee | YAKIN | MELEE | — |
| hud.ammo.bolts | OK | BOLTS | — |
| hud.ammo.shells | SAÇMA | SHELLS | — |
| hud.wave.counter | DALGA {0}/{1} | WAVE {0}/{1} | — |
| hud.wave.remaining | KALAN {0} | LEFT {0} | — |
| hud.wave.label | DALGA | WAVE | — |
| hud.wave.remainingLabel | KALAN | LEFT | — |
| hud.araf.roomsLabel | ODALAR | ROOMS | — |
| hud.araf.ready | ARAF: HAZIR | ARAF: READY | — |
| hud.araf.used | ARAF: KULLANILDI | ARAF: USED | — |
| hud.araf.warning | ARAF'TASIN — ÇIKIŞ ODASINI BUL | IN ARAF — FIND THE EXIT ROOM | — |
| hud.araf.rooms | ODALAR {0}/{1} | ROOMS {0}/{1} | — |
| hud.araf.exitFound | ÇIKIŞ BULUNDU | EXIT FOUND | — |
| hud.banner.arenaSealed | ARENA MÜHÜRLENDİ | ARENA SEALED | — |
| hud.banner.wave | DALGA {0} | WAVE {0} | — |
| hud.banner.waveCleared | DALGA {0} TEMİZLENDİ | WAVE {0} CLEARED | — |
| hud.banner.arenaCleared | ARENA TEMİZLENDİ | ARENA CLEARED | — |
| hud.truedeath.title | GERÇEK ÖLÜM | TRUE DEATH | — |
| hud.truedeath.restart | TEKRAR DENE | TRY AGAIN | — |
| hud.weapon.RepeaterArbalest | ARBALET | ARBALEST | — |
| hud.weapon.ExecutionerBlade | CELLAT KILICI | EXECUTIONER | — |
| hud.weapon.GothicShotgun | GOTİK POMPALI | GOTHIC SHOTGUN | — |
| hud.symbol.slash | / | / | — |
| hud.symbol.plus | + | + | — |
| hud.symbol.warning | ! | ! | — |
| hud.symbol.slotOpen | [ | [ | — |
| hud.symbol.slotClose | ] | ] | — |
| hud.dash.counter | ATILMA {0}/{1} | DASH {0}/{1} | — |
| popup.critical | CRIT! | CRIT! | — |
| popup.reflected | PARRY | PARRY | — |
| popup.health | HP | HP | — |
| popup.bolts | OK | BOLTS | — |
| popup.shells | SAÇMA | SHELLS | — |
| transition.araf.enter | ARAF | ARAF | — |
| transition.araf.escape | KAÇIŞ | ESCAPE | — |

New rows are `settings.group.general`, `settings.language`, `language.tr`, `language.en`, all five `popup.*` and both `transition.*`. Popup punctuation/spacing and invariant numbers compose the exact labels: `CRIT! n`, `PARRY n`, `+n HP`, `+n OK/BOLTS`, `+n SAÇMA/SHELLS`. ARAF is a proper noun; its warning supplies the English context. PURGATOR is unchanged.

Top implementation risks: BeforeSceneLoad/provider readiness on direct scene boot; stale caches/hidden labels and accidental banner/death/transition resets; large popup/banner rebuild allocations on a switch; added settings-row fit and unspecified HUD/popup width budgets; Turkish font coverage and preserving serialized asset GUIDs. Resolve with the targeted tests and visual/performance checks above.
