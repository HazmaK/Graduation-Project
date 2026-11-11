---
project: "retro-fps"
type: "CodeArchitecture"
tags:
  - project
  - memory
  - "retro-fps"
  - dependencies
  - architecture
  - map
---

# CodebaseDependencyGraph (Kod Tabanı ve Bağımlılık Haritası)

> [!info] SİSTEMLER ARASI İLETİŞİM VE ÇAĞRI AKIŞI
> Bu doküman, projedeki tüm C# scriptlerinin birbirine nasıl bağlandığını, hangi sınıfın hangi arayüzü dinlediğini ve veri akış yönlerini gösterir.
> Bir scriptte değişiklik yaparken etkilenen diğer scriptleri buradan takip edin.

---

## 1. YÜKSEK SEVİYELİ MİMARİ AKIŞ DİYAGRAMI

```mermaid
flowchart TD
    subgraph INPUT["🎮 Girdi Katmanı (New Input System)"]
        InputAction["InputSystem_Actions"]
    end

    subgraph PLAYER["🏃 Oyuncu Çekirdeği"]
        PM["PlayerMovement\n(Quake Strafe, Slide, Dash)"]
        PL["PlayerLook\n(Mouse Look, Tilt, FOV, Shake)"]
        PH["PlayerHealth\n(Can, İyileşme, IDamageable)"]
        PP["PlayerParryController\n(Buckler, Yansıtma, Stun)"]
        PDF["PlayerDamageFeedback\n(IMGUI Vinyet, Flinch)"]
        PCF["PlayerCameraFeel + Spring\n(View Punch, Land Dip, Bob, Footsteps)"]
        WC["WeaponController\n(Silah Orkestrasyonu)"]
    end

    subgraph WEAPONS["⚔️ Silah Cephaneliği & Envanter"]
        WB["WeaponBase (Soyut Sınıf)"]
        PV["ProceduralViewmodel\n(Sway, Bob, Recoil)"]
        W1["RepeaterArbalestWeapon\n(Hitscan Seri Delici)"]
        W2["ExecutionerBladeWeapon\n(Geniş Savurma / Saplama)"]
        W3["GothicShotgunWeapon\n(Çifte Saçma & Meat Hook)"]
        AmmoInv["PlayerAmmoInventory\n(Bolts & Shells Rezervi)"]
    end

    subgraph COMBAT_CORE["🩸 Dövüş & Geri Bildirim Çekirdeği"]
        IDamage["IDamageable Interface"]
        IStun["IStunnable Interface"]
        IEncounter["IEncounterEnemy Interface"]
        IAlert["IAlertableEnemy Interface"]
        Events["CombatEvents (Core)\nOnEnemyKilled"]
        Lookup["CombatTargetLookup + CombatRaycast\n(Collider Önbelleği)"]
        
        Hitbox["Hitbox (Headshot/Limb Multiplier)"]
        Hitstop["HitstopManager (Micro-freeze)"]
        CombatVFX["CombatVFXManager (Gore/Spark)"]
        BloodDecal["BloodDecalManager (Kan Lekeleri)"]
        DamageNum["DamageNumberSystem (Retro Popups)"]
        CombatAudio["CombatAudio (Sentetik Sesler)"]
        HealthOrb["HealthOrb & HealthOrbManager\n(Adaptif Can Küresi Havuzu)"]
        HealthDropper["EnemyHealthDropper\n(Modüler Düşüş Tetikleyici)"]
        AmmoPick["AmmoPickup\n(Manyetik Cephane Kutusu)"]
        AmmoDrop["EnemyAmmoDropper\n(Akıllı Cephane Saçma)"]
    end

    subgraph ENEMIES["👹 Düşman Yapay Zekası"]
        Ghoul["BasicEnemyAI (Ghoul Stalker)"]
        FlyingEye["FlyingEyeAI (Uçan Göz)"]
        AlertHub["EnemyAlertHub (Sürü Alarmı)"]
        Projectile["EnemyProjectile (Plazma Mermisi)"]
        Dummy["CombatDummy (Antrenman Mankeni)"]
    end

    subgraph ENCOUNTER["🏰 Karşılaşma & Dalga Orkestrasyonu"]
        Director["WaveSpawnDirector (DOOM Pacing)"]
        Area["EncounterArea (Bölge Kilitleme)"]
        Trigger["EncounterTriggerVolume (Giriş Tetikleyici)"]
        SpawnPoint["EncounterSpawnPoint (Doğuş Noktası)"]
        Telegraph["OccultSpawnTelegraph (Okült Işıma)"]
    end

    subgraph UI_SYS["🖥️ Arayüz Katmanı"]
        HUD["CrosshairHUD (Dinamik Nişangah)"]
        DebugHUD["PlayerDebugHUD (Hız, State, FPS)"]
        AmmoHUD["AmmoHUD (Retro Cephane Paneli)"]
    end

    %% Bağlantılar
    InputAction --> PM & PL & PP & WC
    
    PL --> PV
    PM --> PH
    PM -.->|OnJumped / OnLanded / OnDashStarted / OnSlideStarted| PCF
    PM -.->|OnJumped / OnLanded| PV
    PCF -->|Position/RotationOffset| PL
    PCF -.->|Footstep / Land / Jump / Dash / Slide| CombatAudio
    PH -.->|OnDamageTaken| PDF
    PH -.->|OnDamageTaken| PL
    
    WC --> W1 & W2 & W3
    W1 --|> WB
    W2 --|> WB
    W3 --|> WB
    WB --> PV
    WB -.->|Tüketim| AmmoInv
    W3 -.->|StartGrapple / AddImpulse| PM
    W1 & W2 & W3 -.->|AddViewPunch / AddTrauma| PL
    W2 -.->|StartLunge / StartMeteorSlam / ApplySpeedBoost| PM
    W2 -.->|BloodHarvest Leech| PH
    W2 -.->|Slice & Deflect| Projectile
    
    W1 & W2 & W3 -->|CombatRaycast / OverlapSphere| Lookup
    Lookup -->|Binding| Hitbox
    Hitbox -->|TakeDamage| IDamage
    
    IDamage <|.. Ghoul
    IDamage <|.. FlyingEye
    IDamage <|.. Dummy
    IDamage <|.. PH
    
    IStun <|.. Ghoul
    IStun <|.. FlyingEye
    
    PP -->|Stun| IStun
    PP -->|Reflect| Projectile
    W2 -.->|Finisher / Meteor Slam Stun| IStun
    W3 -.->|MeatHook Stun| IStun
    
    Ghoul & FlyingEye -->|Tetikler| AlertHub
    AlertHub -->|Alert| IAlert
    IAlert <|.. Ghoul
    
    Ghoul & FlyingEye & Dummy -->|Hit & Death| Hitstop & CombatVFX & BloodDecal & CombatAudio
    W1 & W2 & W3 -->|Hasar Sayısı| DamageNum
    Ghoul & FlyingEye -->|RaiseEnemyKilled| Events
    Events -.->|OnEnemyKilled → Kill Confirm| HUD
    Ghoul & FlyingEye & Dummy -->|RegisterHierarchy| Lookup
    Ghoul & FlyingEye -->|Death Drop| HealthDropper -->|TryDropOrbs| HealthOrb
    Ghoul & FlyingEye -->|Death Drop| AmmoDrop -->|Spawn| AmmoPick
    HealthOrb -->|Magnetic Vacuum & Heal| PH
    HealthOrb -.->|Heal Popup| DamageNum
    HealthOrb -.->|Pickup Chime| CombatAudio
    AmmoPick -->|Magnetic Vacuum| AmmoInv
    AmmoPick -.->|Pickup Chime| CombatAudio
    
    Trigger -->|OnPlayerEnter| Area
    Area --> Director
    Director --> SpawnPoint & Telegraph
    Director -->|Spawns & Tracks| IEncounter
    IEncounter <|.. Ghoul
    IEncounter <|.. FlyingEye
    
    WC -.->|OnWeaponHit| HUD
    AmmoInv -.->|OnAmmoChanged| AmmoHUD
```

---

## 2. MODÜLLER VE İLETİŞİM TABLOSU

| Modül | Scriptler | Dışa Bağımlılıklar (Çağırdığı) | Olaylar (Events) & Yayınlar |
| :--- | :--- | :--- | :--- |
| **Player** | `PlayerMovement`, `PlayerLook`, `PlayerHealth`, `PlayerParryController`, `PlayerDamageFeedback`, `Feel/PlayerCameraFeel`, `Feel/Spring` | `UnityEngine.InputSystem`, `IDamageable`, `IStunnable`, `CombatAudio` | `OnDamageTaken`, `OnDied`, `OnParrySuccess`, `OnGrappleEnded`, `OnJumped`, `OnLanded(impact)`, `OnDashStarted`, `OnSlideStarted`, `OnSlideEnded` |
| **Weapons** | `WeaponBase` (+ `CombatTargetLookup`, `CombatRaycast`), `WeaponController`, `ProceduralViewmodel`, `RepeaterArbalestWeapon`, `ExecutionerBladeWeapon`, `GothicShotgunWeapon`, `PlayerAmmoInventory` | `PlayerLook`, `PlayerMovement`, `Hitbox`, `IDamageable`, `IStunnable`, `CombatAudio`, `CombatVFXManager`, `HitstopManager`, `DamageNumberSystem` | `OnWeaponHit(isCrit)`, `OnWeaponSwitched(slot)`, `OnAmmoChanged(type, cur, max)` |
| **Enemies** | `BasicEnemyAI`, `FlyingEyeAI`, `EnemyAlertHub`, `EnemyProjectile`, `CombatDummy` | `IDamageable`, `IStunnable`, `IEncounterEnemy`, `PlayerHealth`, `CombatVFXManager`, `CombatAudio`, `HitstopManager`, `BloodDecalManager`, `CombatTargetLookup` | `OnEnemyDeath`, `CombatEvents.OnEnemyKilled`, `OnAlertTriggered` |
| **Encounter** | `WaveSpawnDirector`, `EncounterArea`, `EncounterTriggerVolume`, `EncounterSpawnPoint`, `OccultSpawnTelegraph` | `IEncounterEnemy`, `CombatAudio` | `OnWaveStarted`, `OnWaveCompleted`, `OnEncounterCleared` |
| **Feedback/VFX**| `CombatVFXManager`, `BloodDecalManager`, `HitstopManager`, `DamageNumberSystem`, `CombatAudio`, `ParryVFX` | Yok (Statik Tekil Yönetici / Olay Temelli Dinleyici) | Bağımsız havuzlanmış parçacık ve ses üretimi |
| **Pickups/Loot**| `HealthOrb`, `HealthOrbManager`, `EnemyHealthDropper`, `AmmoPickup`, `EnemyAmmoDropper` | `PlayerHealth`, `PlayerAmmoInventory`, `DamageNumberSystem`, `CombatAudio` | `TryDropOrbs`, `OnHealed`, `SpawnHealNumber`, `AddAmmo` |
| **UI** | `CrosshairHUD`, `PlayerDebugHUD`, `AmmoHUD` | `WeaponController`, `PlayerMovement`, `PlayerHealth`, `PlayerAmmoInventory`, `CombatAudio` | `WeaponController.OnWeaponHit`, `CombatEvents.OnEnemyKilled`, `PlayerAmmoInventory.OnAmmoChanged` dinler |

---

## 2.1 ASSEMBLY, KATMAN VE PREFAB YERLEŞİMİ (2026-11-11)

- **Assembly'ler:** `RetroFPS.Core` (arayüzler, `AmmoType`, `GameLayers`, `CombatEvents`) ⬅ `RetroFPS.Gameplay` (tüm oynanış) ⬅ `RetroFPS.Editor`. Ayrıntı ve hedef bölünme: [[CollaborationAndGitWorkflow]].
- **Katmanlar:** `GameLayers` tüm fizik maskelerinin tek kaynağıdır. Silahlar ve parry `PlayerAttackMask`, düşman görüşü `EnemySightMask` (diğer düşmanların arkasını görür), engelden kaçınma `EnemyObstacleMask`, düşman mermisi `EnemyProjectileMask`, zemin taraması `GroundMask` kullanır.
- **Prefab'lar:** `Prefabs/Player/Player.prefab` (oyuncu, kamera, üç silah, HUD bileşenleri) ve `Prefabs/Systems/CombatSystems.prefab` (`CombatAudio`, `CombatVFXManager`, `DamageNumberSystem`, `HitstopManager`, `HealthOrbManager`). Yeni bir sahneye bu iki prefab sürüklenerek oyun kurulur. `HitstopManager` yalnızca `CombatSystems` içinde bulunmalıdır (lazy oluşturulmaz). İkinci bir kopya artık yalnızca kendi bileşenini (`Destroy(this)`) siler; `CombatAudio`, `CombatVFXManager` ve `DamageNumberSystem` ise eksikse erişimde/sahne yüklemesinde kendini oluşturur.

---

## 3. BAŞKA BİR AI'IN UYMASI GEREKEN ENTEGRASYON KURALLARI

1. **Yeni Silah Eklerken:**
   - Asla `WeaponController.cs` kodunu değiştirmeyin.
   - `RetroFPS.Weapons.WeaponBase`'den kalıtım alan yeni bir sınıf yazın (örn: `ShotgunWeapon.cs`).
   - `Slot`, `CanFirePrimary`, `CanFireAlt`, `FirePrimary()` ve `FireAlt()` metodlarını uygulayın.
   - Hasar verirken hedefi `CombatTargetLookup.Get(collider)` ile çözün (Hitbox → `IDamageable`); ışınlar için `CombatRaycast.Nearest` kullanın.

2. **Yeni Düşman Eklerken:**
   - Asla `BasicEnemyAI.cs`'yi kopyalayıp içine farklı davranış tıkıştırmayın.
   - Yeni düşman scriptiniz mutlaka şu arayüzleri uygulamalıdır:
     * `IDamageable` ➔ Hasar alabilmesi için
     * `IStunnable` ➔ Parry ve kalkan vuruşlarından etkilenmesi için
     * `IEncounterEnemy` ➔ `WaveSpawnDirector` tarafından doğurulabilmesi ve dalga takibine girmesi için
     * `IAlertableEnemy` ➔ Sürü alarmından haberdar olabilmesi için

3. **Geri Bildirimleri (VFX / Audio) Tetiklerken:**
   - Düşman veya silah sınıflarında ses ve partikül instantiate etmeyin.
   - Tekil servisleri kullanın:
     * `CombatVFXManager.Instance.SpawnHitImpact(pos, normal, isCrit, isFlesh)`
     * `BloodDecalManager.Instance.SpawnDecal(pos, normal)`
     * `HitstopManager.Instance.TriggerHitstop(duration, timeScale)`
     * `DamageNumberSystem.Instance.SpawnDamageNumber(pos, damage, isCrit)`
     * `CombatAudio.Instance.PlayFleshImpact(pos, isCrit)` / `PlayClipAt(clip, pos, volume, pitch)`
   - **Tek sahip kuralı:** Et VFX'i ve et sesi hasar alan varlığın `TakeDamage`'inde üretilir; silah yalnızca hasar sayısı, duvar efekti ve kendi tepkisini üretir. Ölüm tepkileri `CombatEvents.OnEnemyKilled` üzerinden dinlenir ([[GameFeelArchitecture]]).

4. **Kamera ve Hareket Hissi Eklerken:**
   - `CameraHolder` transform'unu yalnızca `PlayerLook` yazar; yeni efektler `PlayerCameraFeel` / `AddViewPunch` / `AddTrauma` / `DoFovKick` ile eklenir.
   - Oyuncuya dış kuvvet `PlayerMovement.AddImpulse` ile verilir; tepkiler hareket olaylarına abone olarak eklenir.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[START_HERE_AI_CONSTITUTION]] ➔ `governed_by_constitution`
- [[CombatAndDamageInterfaces]] ➔ `defines_interfaces`
- [[WaveAndEncounterArchitecture]] ➔ `coordinates_encounters`
- [[WeaponArsenalArchitecture]] ➔ `coordinates_weapons`
- [[EnemyAIAndSpawning]] ➔ `coordinates_enemies`
- [[PlayerSubsystemArchitecture]] ➔ `coordinates_player`
- [[HealthOrbAndDropSystem]] ➔ `coordinates_health_drops`
- [[HitstopAndAudioFeedback]] ➔ `coordinates_feedback`
- [[GameFeelArchitecture]] ➔ `coordinates_game_feel`
- ⬅️ [[ProjectOverview]] ➔ `mapped_by` *(gelen)*
