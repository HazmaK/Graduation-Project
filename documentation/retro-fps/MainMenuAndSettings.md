---
project: "retro-fps"
type: "SystemArchitecture"
tags:
  - project
  - memory
  - "retro-fps"
  - ui
  - settings
  - mainmenu
---

# MainMenuAndSettings (PURGATOR Ana Menü ve Ayarlar Sistemi)

> [!abstract] KAN VE KEMİK ANA MENÜ, KALICI AYARLAR
> PURGATOR ana menüsü (BAŞLA, AYARLAR, ÇIKIŞ) UGUI + TextMeshPro ile kuruludur. Ayarlar POCO servisinde tutulur, UI yalnızca istek gönderir ve olay dinler.
> - **Son Güncelleme:** 2026-11-12

---

## 1. GENEL BAKIŞ
- **Sahne:** `Assets/Scenes/MainMenu.unity`, Build Settings index 0. BAŞLA `TestLevel_Gauntlet` sahnesini asenkron yükler.
- **Menü satırları:** BAŞLA, AYARLAR, ÇIKIŞ (çıkış onay diyaloğu ile). AYARLAR altında ses, hassasiyet, pencere modu, çözünürlük ve hareket azaltma satırları bulunur.
- **Görsel yön:** "Kan ve Kemik". Koyu arka plan (`bg #0C0A0A`), kemik mühürü odak işareti, meşale ışığı, sis ve kıvılcım parçacıkları.
- **Teknoloji:** UGUI (Canvas, EventSystem + `InputSystemUIInputModule`) ve TextMeshPro. Yeni Input System kullanılır.
- **Erişilebilirlik:** Standart seviye (`design/accessibility-requirements.md`).

## 2. MİMARİ AKIŞ

```mermaid
flowchart LR
    BSL["GameResolutionManager<br/>(BeforeSceneLoad)"] --> GS["GameSettings / SettingsService<br/>(RetroFPS.Core, PlayerPrefs)"]
    GS -->|hassasiyet| PL["PlayerLook"]
    GS -->|SFX| CA["CombatAudio"]
    GS -->|Master| AL["AudioListener"]
    MC["MainMenuController"] <--> SM["MainMenuStateMachine<br/>(POCO)"]
    MC -->|ISettingsService istekleri| GS
    MC --> EV["MenuEvents"]
    EV --> ML["MenuLifecycle<br/>(sahne yükleme / çıkış)"]
```

- **Çekirdek ayarlar (`RetroFPS.Core`):** `BeforeSceneLoad` anında yüklenir. Oyun kodu (`PlayerLook`, `CombatAudio`) yalnızca Core'a bağımlıdır, menüye değil. Bu sayede doğrudan `TestLevel_Gauntlet` içinde Play Mode başlatıldığında da kalıcı ayarlar uygulanır.
- **Menü (`RetroFPS.UI.Menu`):** Durum makinesi POCO'dur ve EditMode testlerinde doğrudan çalışır. Controller ve görünüm bileşenleri durumu yalnızca çizer ve girdiyi iletir.

## 3. BİLEŞEN TABLOSU

### 3.1 Core/Settings (`Assets/_Project/Scripts/Core/Settings/`)

| Dosya | Sorumluluk |
|---|---|
| `GameSettingsData.cs` | Ayar değerleri struct'ı, `WindowModeSetting` enum, `Defaults`, `Clamp()`. |
| `SettingsContracts.cs` | `ISettingsService`, `ISettingsStore`, `IDisplayApplier`, `IAudioVolumeApplier` arayüzleri. |
| `SettingsService.cs` | POCO: yükle/sınırla, değişikliği uygula ve kaydet, ekran için beklemede/onayla/geri al, `Changed` olayı. |
| `ResolutionOptions.cs` | POCO: çözünürlük listesini tekilleştirir, 1280x720 altını eler, sıralar, en yakın eşleşmeyi bulur. |
| `SensitivityMapping.cs` | POCO: 0-1 değerini üstel bakış ölçeğine çevirir (0.5 = 1.0). |
| `UnitySettingsAdapters.cs` | `PlayerPrefsSettingsStore`, `ScreenDisplayApplier`, `ListenerVolumeApplier`. İnce motor adaptörleri. |
| `GameSettings.cs` | Statik erişim noktası: `EnsureInitialized()`, `Service`, `Current`, `Changed`, `Save()`. |

### 3.2 Menu/Logic (POCO, `RetroFPS.UI.Menu`)

| Dosya | Sorumluluk |
|---|---|
| `MainMenuStateMachine.cs` | Durumlar (FirstLoad, Root, Settings, RevertPrompt, QuitConfirm, Loading), geçişler, geri kuralları, girdi kilidi. |
| `MenuTween.cs` | Gecikmesiz 0-1 tween, kolaylaştırma eğrileri, hareket azaltma süresi. |
| `RevertCountdown.cs` | 10 sn geri alma sayacı. |
| `HoldAccelerator.cs` | Basılı tutmada adım hızlanması. |
| `UiAudioThrottle.cs` | Aynı UI sesi için 50 ms sınırlama. |
| `MenuEvents.cs` | Statik olaylar: `StartRequested`, `QuitConfirmed`, `PanelChanged`, `UiAudio`, `FadeOutComplete`, `LoadFailed`. |

### 3.3 Menu/Data (ScriptableObject)

| Dosya | Sorumluluk |
|---|---|
| `MenuTheme.cs` | Renk belirteçleri, TMP font referansları, yazı boyutları, zamanlamalar, parçacık parametreleri. |
| `StringTable.cs` | Anahtar-değer tablosu. `Get(key)` eksik anahtarda anahtarın kendisini döner ve uyarı verir. |

### 3.4 Menu/Views (MonoBehaviour)

| Dosya | Sorumluluk |
|---|---|
| `MainMenuController.cs` | Durum makinesini barındırır, CanvasGroup ve seçimi yönetir, olayları yayar. |
| `MenuInputRouter.cs` | Cancel/RightClick ile geri, Tab ile gezinme, odak koruyucu. |
| `MenuFocusVisual.cs` | Seçim görselleri, fare üzerine gelince seçim, basma görseli. |
| `MenuSlider.cs` | `Slider` alt sınıfı, 5% adımlı, basılı tutma hızlanması. |
| `MenuCycleRow.cs` | Sol/sağ ile seçenek döngüsü. |
| `SettingsPanelView.cs` | 8 ayar satırını `ISettingsService`'e bağlar. |
| `ConfirmDialogView.cs` | Genel onay diyaloğu (çıkış ve geri alma). |
| `ScreenFader.cs` | Karartma katmanı, geçiş sırasında raycast engeller. |
| `LocalizedText.cs` | TMP etiketini `StringTable` anahtarından doldurur. |
| `MenuAmbientMotion.cs` | Meşale ışığı, sis kayması, hareket azaltmada parçacık duraklatma. |
| `MenuAudioPlayer.cs` | `MenuEvents.UiAudio` dinler, `UiVolume` ile çalar. |
| `MenuLifecycle.cs` | Sahneyi asenkron yükler, hata olayı üretir, çıkışta ayarları kaydeder. |

## 4. KURALLAR
- **UI oyun durumunu sahiplenmez.** Olay yayar, durumu değiştirmez.
- **Sıfır GC.** Güncelleme döngülerinde tahsis yok, metin birleştirme yok.
- **Metinler `StringTable` anahtarlarıyla.** Ekranda sabit metin yazılmaz.
- **Ayarlar UI üzerinden değişir.** UI `PlayerPrefs`'e yazmaz, yalnızca `ISettingsService` istekleri gönderir.
- **İlk açılış varsayılanı:** kenarlıksız masaüstü çözünürlüğü.
- **Her çözünürlük veya pencere modu değişiminde 10 sn geri alma sayacı.** Süre dolar, Geri dönülürse veya "Geri al" seçilirse önceki kayıtlı değerler yeniden uygulanır.

## 5. YENİDEN ÜRETME
- Unity Editor menüsü: **`RetroFPS/Build Main Menu`**.
- TMP Essentials yoksa builder önce bunu içe aktarır ve durur. Menü komutu tekrar çalıştırılır.
- Builder idempotenttir: var olan varlıkları yükler veya oluşturur. Play Mode ve kirli sahne kontrolü yapar.

## 6. BİLİNEN EKSİKLER
- **UI ölçekleme ertelendi.** Yalnızca CanvasScaler (1920x1080, yükseklik eşleşmesi) ile çalışır.
- **KONTROLLER ekranı (tuş atama) ayrıdır.** Bu menüde yoktur.
- **Müzik sesi bağlanacak kaynak yok.** Müzik ayarı kaydedilir ama etkisi yoktur.
- **SFX yalnızca `CombatAudio`'ya bağlı.** `EncounterArea`, `WaveSpawnDirector` ve `OccultSpawnTelegraph` kendi AudioSource'larını kullanır, ayar bu kaynaklara uygulanmaz.
- **Yer tutucu font ve sanat.** Görseller `Placeholder` klasöründedir.

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[CoreLoopAndPurgatory]] ➔ `launches_run`
- [[PlayerSubsystemArchitecture]] ➔ `receives_sensitivity`
- [[HitstopAndAudioFeedback]] ➔ `receives_sfx_volume`
- ⬅️ [[ProjectOverview]] ➔ `features_ui` *(gelen)*
- [[CombatHudArchitecture]] ➔ `shares_theme_and_strings`
