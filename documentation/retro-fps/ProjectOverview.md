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
> - **Son Güncelleme:** 2026-10-27 21:40:00

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
- **Oyuncu Hareket Fiziği:** [[PlayerMovementArchitecture]]
- **Oyuncu Alt Sistemleri:** [[PlayerSubsystemArchitecture]]
- **Silah Dövüş Dinamikleri:** [[DualLoadoutAndWeapons]]
- **Silah Kod Mimarisi ve Viewmodel:** [[WeaponArsenalArchitecture]]
- **Düşman Algı ve Hissiyat:** [[EnemyAIAndCombatFeel]]
- **Düşman Tipleri ve Mermiler:** [[EnemyAIAndSpawning]]
- **Dövüş ve Hasar Arayüzleri:** [[CombatAndDamageInterfaces]]
- **Karşılaşma ve Dalga Direktörü:** [[WaveAndEncounterArchitecture]]
- **Görsel Efekt ve Parçalanma:** [[CombatVFXAndDecalSystem]]
- **Vuruş Tokluğu ve Ses:** [[HitstopAndAudioFeedback]]
- **Çevre, Aydınlatma ve Atmosfer:** [[EnvironmentAndAudioAtmosphere]]

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[START_HERE_AI_CONSTITUTION]] ➔ `governs_architecture`
- [[CodebaseDependencyGraph]] ➔ `mapped_by`
- [[CollaborationAndGitWorkflow]] ➔ `defines_collaboration`
- [[CoreLoopAndPurgatory]] ➔ `features_mechanic`
- [[PlayerMovementArchitecture]] ➔ `utilizes_movement`
- [[PlayerSubsystemArchitecture]] ➔ `utilizes_player`
- [[DualLoadoutAndWeapons]] ➔ `utilizes_combat`
- [[WeaponArsenalArchitecture]] ➔ `utilizes_weapons`
- [[EnemyAIAndCombatFeel]] ➔ `utilizes_ai`
- [[EnemyAIAndSpawning]] ➔ `spawns_enemies`
- [[CombatAndDamageInterfaces]] ➔ `utilizes_interfaces`
- [[WaveAndEncounterArchitecture]] ➔ `orchestrates_encounters`
- [[CombatVFXAndDecalSystem]] ➔ `utilizes_vfx`
- [[HitstopAndAudioFeedback]] ➔ `utilizes_feedback`
- [[EnvironmentAndAudioAtmosphere]] ➔ `utilizes_atmosphere`
- ⬅️ [[MultiAgentSharedMemory]] ➔ `manages_project` *(gelen)*
