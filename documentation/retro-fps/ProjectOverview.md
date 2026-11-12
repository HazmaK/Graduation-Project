---
project: "retro-fps"
type: "GameDesign"
tags:
  - project
  - memory
  - "retro-fps"
  - gamedesign
  - index
---

# ProjectOverview (Ana Proje ve Sistem Merkezi)

> [!abstract] Sinaptik Varlık Bilgisi
> - **Proje:** `retro-fps`
> - **Tür:** `GameDesign`
> - **Son Güncelleme:** 2026-11-04 01:27:00

## Gözlemler (Hafıza Kayıtları)
- **Tür:** Fast-Paced Retro Boomer Shooter / Rogue-lite / Dark Fantasy. İlham: DUSK, ULTRAKILL, Quake 1, Devil Daggers.
- **Teknoloji:** Unity 6 (6000.5.5f1), Universal Render Pipeline (URP), New Input System (`UnityEngine.InputSystem`). Sıfır GC tahsisi kuralı.
- **Görsel Dil:** Düşük poligonlu modeller, point-filtered düşük çözünürlüklü dokular, yüksek kontrastlı gotik aydınlatma, anatomik gore ve parçalanma.
- **Modelleme/Kod İşbölümü:** Kullanıcı modellemeci, Antigravity Lead Gameplay & Systems Architect rolündedir.

## Mimari ve Sistem Bağlantıları
- **Anayasa ve Kurallar:** [[START_HERE_AI_CONSTITUTION]]
- **Kod ve Bağımlılık Haritası:** [[CodebaseDependencyGraph]]
- **Ekip ve Git Protokolü:** [[CollaborationAndGitWorkflow]]
- **Temel Döngü ve Araf Mekaniği:** [[CoreLoopAndPurgatory]]
- **Araf Labirenti ve Oda Zarları:** [[ArafLabyrinthSystem]]
- **Oyuncu Hareket Fiziği:** [[PlayerMovementArchitecture]]
- **Oyuncu Alt Sistemleri:** [[PlayerSubsystemArchitecture]]
- **Silah Dövüş Dinamikleri:** [[DualLoadoutAndWeapons]]
- **Silah Kod Mimarisi ve Viewmodel:** [[WeaponArsenalArchitecture]]
- **Düşman Algı ve Hissiyat:** [[EnemyAIAndCombatFeel]]
- **Düşman Tipleri ve Mermiler:** [[EnemyAIAndSpawning]]
- **Dövüş ve Hasar Arayüzleri:** [[CombatAndDamageInterfaces]]
- **Can Küresi ve Düşüş Sistemi:** [[HealthOrbAndDropSystem]]
- **Karşılaşma ve Dalga Direktörü:** [[WaveAndEncounterArchitecture]]
- **Seed'li Rastgelelik ve Dalga Havuzları:** [[RunRandomness]]
- **Görsel Efekt ve Parçalanma:** [[CombatVFXAndDecalSystem]]
- **Vuruş Tokluğu ve Ses:** [[HitstopAndAudioFeedback]]
- **Oyun Hissi (Kamera, Spring, Kill Confirm):** [[GameFeelArchitecture]]
- **Çevre, Aydınlatma ve Atmosfer:** [[EnvironmentAndAudioAtmosphere]]
- **Ana Menü ve Ayarlar:** [[MainMenuAndSettings]]
- **Savaş HUD'u:** [[CombatHudArchitecture]]

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[START_HERE_AI_CONSTITUTION]] ➔ `governs_architecture`
- [[CodebaseDependencyGraph]] ➔ `mapped_by`
- [[CollaborationAndGitWorkflow]] ➔ `defines_collaboration`
- [[CoreLoopAndPurgatory]] ➔ `features_mechanic`
- [[ArafLabyrinthSystem]] ➔ `features_mechanic`
- [[RunRandomness]] ➔ `features_system`
- [[PlayerMovementArchitecture]] ➔ `utilizes_movement`
- [[PlayerSubsystemArchitecture]] ➔ `utilizes_player`
- [[DualLoadoutAndWeapons]] ➔ `utilizes_combat`
- [[WeaponArsenalArchitecture]] ➔ `utilizes_weapons`
- [[EnemyAIAndCombatFeel]] ➔ `utilizes_ai`
- [[EnemyAIAndSpawning]] ➔ `spawns_enemies`
- [[CombatAndDamageInterfaces]] ➔ `utilizes_interfaces`
- [[HealthOrbAndDropSystem]] ➔ `utilizes_health_drops`
- [[WaveAndEncounterArchitecture]] ➔ `orchestrates_encounters`
- [[CombatVFXAndDecalSystem]] ➔ `utilizes_vfx`
- [[HitstopAndAudioFeedback]] ➔ `utilizes_feedback`
- [[GameFeelArchitecture]] ➔ `utilizes_game_feel`
- [[EnvironmentAndAudioAtmosphere]] ➔ `utilizes_atmosphere`
- [[MainMenuAndSettings]] ➔ `features_ui`
- [[CombatHudArchitecture]] ➔ `features_ui`
- ⬅️ [[MultiAgentSharedMemory]] ➔ `manages_project` *(gelen)*
