---
tags: [system, combat, settings, difficulty]
updated: 2026-11-27
---

# DifficultySystem (Zorluk: Kolay / Normal / Zor)

Oyuncunun Ayarlar menüsünden seçtiği zorluk. **Normal = kimlik değerleri** (bugünkü oyun birebir). Değerler veri odaklıdır; her seviye bir `DifficultyPreset` ScriptableObject'idir. Hiçbir çarpan mevcut sistemlerin yerine geçmez, onlarla **çarpılarak / toplanarak birleşir** (elit ölçekleme, Araf U0–U3 av profilleri, dinamik zorluk).

## 1. Akış

`SettingsPanelView` (Zorluk satırı) ➔ `ISettingsService.SetDifficulty` ➔ `GameSettingsData.Difficulty` + PlayerPrefs `"difficulty"` (int: Easy=0, Normal=1, Hard=2; geçersiz değer Normal'e onarılır) ➔ `GameSettings.Changed` ➔ `GameDifficulty` (statik, `RetroFPS.Combat`) aktif `DifficultyModifiers` anlık görüntüsünü günceller ve `EnemyAttackTokens.SetLimitOffset` uygular.

- Tablo: `Assets/_Project/Resources/Difficulty/DifficultyPresetTable.asset` (`Resources.Load`, `LocalizationCatalog` ile aynı desen; sahne düzenlemesi yok). Eksikse uyarı + Normal.
- Edit Mode'da `GameDifficulty.Current` kimliktir; testler `SetForTests` / `ResetForTests` kullanır.

## 2. Değerler ve uygulama noktaları

| Ayar | Kolay | Normal | Zor | Nerede |
|---|---|---|---|---|
| `playerDamageTakenMultiplier` | 0.6 | 1 | 1.3 | `PlayerHealth.TakeDamage` ve `TakeUnparryableDamage`, `PlayerStats(DamageTaken)` sonrasında (mermi, tehlike, Araf düşme hasarı dahil) |
| `enemyHealthMultiplier` | 0.8 | 1 | 1.2 | Ghoul / Penitent / Uçan Göz azami can |
| `enemySpeedMultiplier` | 0.9 | 1 | 1.1 | hareket hızı (elit × av × zorluk) |
| `enemyAttackCooldownMultiplier` | 1.3 | 1 | 0.85 | Ghoul `SetCooldownScale(av × zorluk)`, Göz light/heavy, Penitent `PenitentAttackLoop.SetCooldownScale` |
| Jeton ofseti Melee/Slam/RangedHeavy | −1/0/−1 | 0/0/0 | +1/0/+1 | `EnemyAttackTokens.CapacityOf` |
| `waveSpawnIntervalMultiplier` | 1.25 | 1 | 0.85 | `WaveSpawnDirector.UpdateSpawning` (DDA çarpanı × zorluk) |

**Formüller**
- Can: `maxHealth = base × elitCan × zorlukCan`
- Hız: `speed = base × elitHız × avHız × zorlukHız` (Ghoul flankSpeed'e elit çarpanı yine uygulanmaz)
- Bekleme: `cooldown = base × profil.AttackCooldownMultiplier × zorlukBekleme`
- Jeton: `kapasite = max(1, (çağıranLimiti > 0 ? çağıranLimiti : kanalVarsayılanı) + ofset)` ➔ Araf U-profili melee 3 Zor'da 4, Kolay'da 2; kule varsayılanı 2 Zor'da 3, Kolay'da 1.

## 3. Zamanlama ve kurallar

- Düşmanlar zorluğu **doğuşta** okur (`Awake` + `Respawn`/`OnSpawnFromDirector`). Koşu ortasında değişiklik yeni doğuşları etkiler; oyuncu hasar çarpanı ve jeton limitleri anında etkilidir.
- Tower (av dışı) düşmanlarına tam saldırganlık profili **uygulanmaz** (kullanıcı kararı 2026-11-27: yalnızca skaler çarpanlar). Feint/kuşatma yarıçapı yalnız Araf av modunda kalır.
- Elit ölçekleme (`IEliteScalable`) değişmedi; ayrı çarpan olarak birleşir.
- Sıcak yolda tahsis yok: yalnızca önbelleğe alınmış float'lar okunur.

## 4. UI

- Satır `Difficulty`, Genel grubunda Dil'in altında; navigasyon sırası 14 kontrol (Language, Difficulty, Slider0…Back).
- Panel 1040x900 korundu; satır ve başlık aralığı 48 ➔ 44 birim (720p'de 29 piksel). Back 830 merkezde, 858'de biter (876 içerik).
- Odak hedefleri artık indeks değil görünüm referansıdır: `SettingsPanelView.MasterVolumeRow / WindowModeRow / ResolutionRow` (Main ve Pause menü).
- Dizeler: `settings.difficulty` (Zorluk/Difficulty), `settings.difficulty.easy|normal|hard` (Kolay|Normal|Zor). Prefab `SharedSettingsPanelBuilder.Build()` ile yeniden üretildi.
- Kanıt: `production/qa/evidence/difficulty-settings-menu-2026-11-27.png`, `difficulty-settings-menu-hard-2026-11-27.png`.

## 5. Dosyalar

- Yeni: `Core/Settings/DifficultySetting.cs`, `Combat/Difficulty/{DifficultyModifiers, DifficultyPreset, DifficultyPresetTable, GameDifficulty}.cs`, `Resources/Difficulty/*.asset`, `Tests/EditMode/DifficultyTests.cs`.
- Değişen: `GameSettingsData`, `SettingsContracts`, `SettingsService`, `UnitySettingsAdapters`, `SettingsPanelView`, `MainMenuController`, `PauseMenuController`, `SharedSettingsPanelBuilder`, `MainMenuBuilder` (metin sığma doğrulaması), `StringTable` + `StringTable_MainMenu_tr`, `EnemyAttackTokens`, `BasicEnemyAI`, `PenitentAI`, `PenitentAttackLoop`, `FlyingEyeAI`, `WaveSpawnDirector`, `PlayerHealth`, `PausePrefabTests`.

## 6. Ayar düğmeleri (tuning)

Değerleri `Resources/Difficulty/DifficultyPreset_*.asset` üzerinden değiştirin. Normal asset'i kimlik değerlerinde kalmalı (`DifficultyTests.PresetTable_NormalIsIdentity_EasyAndHardPullOppositeWays` bunu korur). 2026-11-26 AI yeniden tasarımından sonra kule zorlaştı (temel senaryoda Ghoul saldırısı 34 ➔ 83); Kolay bunu dengelemek için ilk ayar noktasıdır.

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[EnemyAIAndSpawning]] ➔ `scales_enemy_stats`
- [[MainMenuAndSettings]] ➔ `persisted_by_settings`
- [[WaveAndEncounterArchitecture]] ➔ `scales_wave_pacing`
- [[ArafLabyrinthSystem]] ➔ `composes_with_hunt_profiles`
