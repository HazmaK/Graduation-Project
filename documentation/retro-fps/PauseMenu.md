---
project: "retro-fps"
type: "SystemArchitecture"
tags:
  - project
  - memory
  - "retro-fps"
  - ui
  - pause
  - settings
  - mainmenu
---

# PauseMenu (PURGATOR Oyun İçi Duraklatma Menüsü)

> [!abstract] KAN VE KEMİK DURAKLATMA, TAM DURAKLATMA
> Esc veya gamepad Start oyunu tamamen durdurur (timeScale 0, sabit adım, imleç ve ses durumu). Menü POCO servislerde tutulan durumu okur, UI yalnızca istek gönderir. Ayarlar ana menüdeki paylaşılan `SettingsPanel` prefab'ını kullanır.
> - **Plan:** `production/ui/pause-menu/plan.md`
> - **Kanıt:** `production/qa/evidence/pause-01..06`
> - **Son Güncelleme:** 2026-11-16

---

## 1. GENEL BAKIŞ
- **Amaç:** Oyuncunun savaş, Araf geçişi veya run sırasında oyunu güvenle durdurup DEVAM, AYARLAR veya ANA MENÜ seçebilmesi. Bu, `design/accessibility-requirements.md` içindeki "Pause anywhere" hedefinin uygulamasıdır.
- **Girdi:** Esc veya gamepad Start açar ve kapatır. Menü içinde Esc, B veya sağ tık tek seviye geri gider. Açılış kuralları aşağıda.
- **Öğeler:** DEVAM (RESUME), YENİ KOŞU (NEW RUN), AYARLAR (SETTINGS), ANA MENÜ (MAIN MENU). Dört öğe dikey gezinme ve sarma kullanır.
- **YENİ KOŞU:** ANA MENÜ ile aynı onay kartını kullanır; varsayılan HAYIR, iptal/hata odağı YENİ KOŞU. `ConfirmNewRun` durumu tek `NewRunRequested` olayı üretir. `PauseRuntime.TryPrepareSceneLeave(true)` sonrasında `StartNewRun`, `RunDirector.RestartRunFromPause()` üzerinden kayıtlı `towerSceneName` hedefine async Single yükler; InAraf dahil mevcut koşu ve Araf hakkı silinir. Director yoksa aktif sahne adı yeniden yüklenir. Normal true-death `RestartRun()` değişmez. Hata `RecoverSceneLeave` ile pause sahipliğini geri kurar; `pause.new_run_load_error` gösterilir. Geçiş/ölüm/yükleme engelleri aynıdır.
- **ANA MENÜ:** "İlerleme kaybolur" onay diyaloğu açar. Varsayılan odak HAYIR'dır. EVET, `MainMenu` sahnesini bir kez yükler. Yükleme hatasında yeniden deneme mesajı gösterilir.
- **Teknoloji:** UGUI (Screen Space Overlay, sıralama 200), TextMeshPro, Yeni Input System. Kapsam `RetroFPS.Pause` assembly'sidir.

## 2. MİMARİ AKIŞ

```mermaid
flowchart LR
    IN["Esc / Start / sağ tık<br/>(PauseRuntime, erken yürütme sırası)"] --> PS["PauseService<br/>(POCO, RetroFPS.Core)"]
    PS --> GP["GamePause<br/>(Core erişim noktası)"]
    PR["PauseRuntime<br/>(Gameplay, tek Unity adaptörü)"] --> PS
    PR -->|Time, AudioListener, Cursor| ENV["Ortam adaptörü"]
    PR -->|UI istekleri| PB["PauseBindings"]
    PB --> SM["PauseMenuStateMachine<br/>(POCO, RetroFPS.UI.Pause.Logic)"]
    SM --> PC["PauseMenuController<br/>(View, RetroFPS.UI.Pause.Views)"]
    PC --> SP["Shared SettingsPanel<br/>(ISettingsPanelHost)"]
    PC --> EV["MenuEvents / MenuAudioPlayer"]
```

### 2.1 Katmanlar
- **Core/Gameplay (POCO):** `IPauseService` ve `PauseService`. Sorumluluklar: `IsPaused`, `SimulationBlocked`, `GameplayInputBlocked`, `TryPause`, `Resume`, `CancelForExclusiveFlow`, `PrepareSceneLeave`, `Changed`. Saat, ses ve imleç anlık görüntülerini ortam soyutlaması üzerinden alır ve uygular.
- **Core erişim noktası:** `GamePause`. `SubsystemRegistration` ile sıfırlanır ve testte enjekte edilebilir.
- **Gameplay adaptörü:** `PauseRuntime`. Tek Unity sahibidir, `Player` üzerine bir kez eklenir. Girdiyi erken yürütme sırasında okur ve UI isteklerini Core sink sözleşmesi üzerinden iletir.
- **UI durum makinesi (POCO):** `PauseMenuStateMachine`. Durumlar: Closed, Root, Settings, RevertPrompt, ConfirmMainMenu, ConfirmNewRun, Leaving. Prompt ve hata anahtarı seçimi POCO'dadır; tek onay görünümü `sceneLeavePrompt` üzerinden güncellenir, LocalizedText ile yarışmaz. İki dilin tüm prompt/hata metinleri kurulumda ısıtılır.
- **UI görünümü:** `PauseMenuController`. `PauseBindings` ile bağlanır. Ayarlar için `ISettingsPanelHost` uygular.
- **Bağımlılık yönü:** Gameplay → Core, Menu → Core, HUD → Gameplay/Menu/Core, Pause → Gameplay/HUD/Menu/Core. Gameplay hiçbir zaman Pause, Menu veya HUD'a referans vermez. `RunPhase` Core'a taşınmadı.

## 3. AÇILMA VE ENGELLEME KURALLARI
- **Açılabilir durum:** Oyun sahnesi, canlı ve ölü olmayan oyuncu, `TrueDeathView.IsOpen` latch'i yok, sahne yükleme latch'i yok, faz Tower veya InAraf.
- **Engellenen durumlar:** FallingToAraf, EscapingAraf, true death kartı, sahne yükleme ve MainMenu. Engelleme Esc ve Start için geçerlidir.
- **Araf geçişi:** Düşme ve kaçış sırasında istek ArafTransition'a hiçbir şey yapmaz. Fade ve slow motion devam eder.
- **True death:** Mevcut P10 kartı, arming ve odak değişmez.
- **Aynı karede sahiplik çakışması:** Ölüm, geçiş veya dış yükleme başlarsa o sahip kazanır. Bekleyen buton işi iptal edilir, önizleme geri alınır, pause gizlenir. Kaynak imleci yeniden kilitlemez ve ölü oyuncunun kontrollerini açmaz.
- **Basılı tutma:** Bloklanan veya tutulan toggle serbest bırakılıp yeniden basılmadan açılmaz. Esc, Start ve iptal aynı karede gelirse tek komut işlenir.
- **Girdi kilidi:** P2 yaklaşık 80 ms basma kilidi, geçiş sırasında tekrarlar yok sayılır. Gecikmeli işlemlerde uygunluk yeniden denetlenir.

## 4. GİRDİ, SAAT VE SES KURALLARI
- **Saat:** Açılışta `Time.timeScale` ve `Time.fixedDeltaTime` bir kez yakalanır. `timeScale = 0` atanır. Normal devamda yakalanan değerler geri yüklenir. Sabit adım `0.02` değerine sabitlenmez.
- **Ses:** Gameplay kaynakları `AudioListener.pause = true` ile durdurulur. Önceki değer normal devamda geri yüklenir. Ana ses veya `AudioListener.volume` değiştirilmez.
- **UI sesi:** Pause prefab'ındaki `MenuAudioPlayer` kendi `AudioSource`'unda `ignoreListenerPause = true` kullanır. Ses ayarı ve UI sesi throttle akışı çalışmaya devam eder.
- **İmleç:** Açılışta kilit kalkar ve görünür olur. Devamda gameplay sahibi için tekrar kilitlenir ve gizlenir. `PlayerLook` artık Esc ile imleç açıp kapatmaz.
- **Girdi:** Gameplay girdisi kapalıdır. Devam karesinde de kapalı kalır ve basılı tuşların bırakılması beklenir. Ateş, alternatif ateş ve aksiyon düğmeleri bırakılana kadar bastırılır. UI gezinme eylemleri açık kalır. `InputSystem_Actions.inputactions` değiştirilmez.
- **Hitstop:** `HitstopManager`, pause açıkken birimsiz zamanlayıcısını azaltmaz, yeni hitstop isteklerini reddeder ve `timeScale` veya `fixedDeltaTime` değerini pause'un üzerine yazmaz. Kalan hitstop devamda kendi tabanına döner.
- **Birimsiz oyun saati:** `GamePause` içindeki saat `PauseRuntime` tarafından yalnızca pause dışında ilerletilir. RunDirector süresi, HUD sağlık, yetenek ve cephane efektleri, banner zamanlamaları ve hitmarker'lar bu saati kullanır. Menü animasyonları, UI ses throttle'ı ve 10 sn ekran geri alma sayacı gerçek birimsiz zamanı kullanır.
- **OnGUI:** Pause açıkken `DamageNumberSystem`, `CrosshairHUD`, `PlayerDamageFeedback` ve `PlayerDebugHUD` çizimi bastırılır. HUD objeleri aktif kalır.

## 5. ANA MENÜYE DÖNÜŞ
- EVET, ayarları kaydeder ve bekleyen ekran önizlemesini geri alır. Girdi ve oyun durur, siyah fade birimsiz zamanla yürür.
- Fade sonrası `PauseRuntime` yükleme latch'ini tutar, hitstop'u iptal eder, `timeScale = 1` yapar, sabit adımı geri yükler, `AudioListener.pause`'u açıkça kapatır ve imleci açar.
- Tek `SceneManager.LoadSceneAsync("MainMenu", Single)` çağrısı yapılır. Yükleme bitene kadar gameplay Update çalışmaz.
- Doğrulama veya yükleme hatasında pause sahipliği geri kurulur, `pause.main_menu_load_error` gösterilir ve odak ANA MENÜ'ye gider.

## 6. BUILDER VE PREFAB
- **Prefab:** `Prefabs/UI/Pause/PauseMenu.prefab`, `Prefabs/Player/Player.prefab` içinde iç içe (nested) durur.
- **Ayarlar:** `Prefabs/UI/Shared/SettingsPanel.prefab` ana menü ile aynı asset'tir. Dil satırı dahil GENEL bölümü aynıdır. Her iki menü de `SharedSettingsPanelBuilder` kullanır.
- **Yeniden üretme:** Unity Editor menüsü `RetroFPS/Build Pause Menu`. İdempotenttir. Çift iç içe örnekleri kaldırır.
- **Metinler:** Builder çalıştırıldıktan sonra `StringTable_MainMenu_tr` içinde 43 giriş. Pause anahtarları: `pause.resume`, `pause.new_run`, `pause.new_run_prompt`, `pause.new_run_load_error`, `pause.main_menu`, `pause.main_menu_prompt`, `pause.main_menu_load_error`. Builder mevcut prefab'a yalnız eksik NewRun düğmesini ekler, mevcut GUID/referansları korur ve tekrar çalıştırıldığında kopya üretmez.

## 7. TESTLER
- **EditMode:** `PauseServiceTests`, `PauseHitstopTests`, `PauseInputAndClockTests`, `PauseEligibilityTests`, `PauseMenuStateMachineTests`, `PauseStringTableTests`, `PausePrefabTests`.
- **Önceki sonuç:** EditMode 478/479 geçti. Tek başarısız test bilinen, pause ile ilgisi olmayan bir sahne override sorunudur.
- **New Run doğrulaması (2026-11-16):** Yedi assembly standalone derlendi; Unity dışında 36 pause/main-menu POCO NUnit vakası geçti. Yeni adapter/prefab/43-giriş manifest testleri derlenir; Unity çalıştırılmadı ve üretilen asset'ler değiştirilmedi. Unity'de doğrulandı (handoff fix sonrası): derleme temiz; builder idempotent; Pause* 127/127, StringTableTests 15/15, CombatHudPrefabTests 9/9, MainMenuStateMachineTests 15/15; Play Mode'da Kule'den ve Araf'tan New Run, gerçek ölüm sonrası TEKRAR DENE ve ANA MENÜ akışları geçti (yeni runtime servisi sahipleniyor, imleç Locked, Esc yeniden çalışıyor). Kanıt: pause-07..11.

### New Run service handoff fix (2026-11-16)
- `PauseRuntime.OnEnable` may replace a departing `PauseService` (`IsLeaving`), while a live non-departing owner still rejects duplicates. The old runtime's teardown and retained Single-load callback only write the environment while its service is still current; neither clears nor overwrites a successor.
- `PauseSnapshot.SceneLeave` defines the destination baseline: time scale 1, captured normal fixed step, audio unpaused, and a locked/hidden gameplay cursor versus a free/visible Main Menu cursor. Both initial departure and unreplaced late cleanup use this policy.
- True-death `RunDirector.RestartRun` uses ordinary, non-leaving teardown: its old runtime releases the global service before destination activation, so it does not retain the stale leaving owner. Its guards and load behavior remain unchanged.
- Regression coverage includes both sceneLoaded callback orders, successor activation before old teardown, normal true-death teardown, destination baselines and failure recovery. Unity scene activation remains a Play Mode verification step.
- Standalone validation: all seven assemblies compile; 80 engine-free pause-service and pause/main-menu state-machine NUnit cases pass. Adapter and prefab tests compile but require Unity to execute; Unity was not launched for this fix.

## 8. KANIT
- `production/qa/evidence/pause-01` ile `pause-06` arasındaki ekran görüntüleri: kök menü, ayarlar, geri alma diyaloğu, ana menü onayı ve hata durumu.

## 9. KURALLAR
- **Sıfır GC:** Update, FixedUpdate ve LateUpdate içinde tahsis yok. Metin birleştirme yok. Önbellek kurulumda yapılır.
- **Kapsülleme:** Yalnızca `[SerializeField] private`. Dış erişim salt okunur getter ile.
- **UI oyun durumunu sahiplenmez.** `Time` ve `AudioListener` değerlerine UI doğrudan yazmaz.
- **Metinler `StringTable` anahtarlarıyla.**

## 10. BİLİNEN EKSİKLER
- **Ayarların ses bağlantısı:** Müzik ayarının bağlı kaynağı yoktur (bkz. [[MainMenuAndSettings]]).
- **Araf geçişi ve true death:** Bilerek engellidir. "Pause anywhere" hedefi tam olarak değil, kısmen karşılanmıştır.

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[ProjectOverview]] ➔ `features_ui` *(gelen)*
- [[MainMenuAndSettings]] ➔ `shares_settings_panel`
- [[CombatHudArchitecture]] ➔ `pauses_hud_clocks`
- [[CoreLoopAndPurgatory]] ➔ `pauses_run`
