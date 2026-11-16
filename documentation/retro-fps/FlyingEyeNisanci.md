---
project: "retro-fps"
type: "AISystem"
tags:
  - project
  - memory
  - "retro-fps"
  - enemy
  - ai
  - flying-eye
  - projectile
  - parry
  - combat
---

# FlyingEyeNisanci (Uçan Göz "Nişancı" Yeniden Tasarımı)

> [!abstract] OYUNCUNUN ÖNCE ÖLDÜRMEK İSTEYECEĞİ DÜŞMAN
> Uçan Göz artık hafif bir strafe ile sabit hedef almak yerine, oyuncuyu öngörülü nişanla vuran, iki saldırı tipi (hafif ok ve ağır "Hüküm Küresi") kullanan ve parry ile cezalandırılabilen bir nişancı. Dalga sistemi, saldırı jetonları ve elit ölçeklendirme korunmuştur.

---

## 1. AMAÇ VE GEREKÇE

Ölçülen temel durum (eski sürüm, gerçek dalgada):
- **31 atıştan 20'si Ghoul'lara çarptı.** Mermiler Ghoul collider'larını dost ateşi gibi vuruyordu.
- **12 atıştan 0 isabet** yanal koşan oyuncuya karşı. Nişan öngörüsüz (lead yok) yapılıyordu.
- Strafe hızı **1.7 m/s** (`flySpeed 3.8 × 0.45`). Göz her atışta 0.44 sn duruyordu.
- Saldırı jetonu hiç kullanılmıyordu; yaklaşık **2.8 sn'de bir** atış yapılıyordu.

Hedef: oyuncunun öncelikli olarak öldürmek isteyeceği, okunabilir ama tehlikeli bir düşman. Parry ile güçlü bir karşılık vermek.

---

## 2. SALDIRI SETİ

| Özellik | Hafif Ok (Light) | Ağır "Hüküm Küresi" (Heavy) |
| :--- | :--- | :--- |
| Hazırlık (windup) | 0.45 sn | 1.1 sn |
| Hız | 16 m/s | 9 m/s |
| Hasar (temel) | 18 | 35 |
| Çarpışma yarıçapı | 0.35 m | 0.6 m |
| Görsel ölçek | 0.40 | 0.90 |
| Ömür | 5 sn | 7 sn |
| Öngörü (lead factor / üst sınır) | 0.6 / 0.6 sn | 0.9 / 0.8 sn |
| Yansıma yönelimi (homing) | 360°/sn, 75° konide | 360°/sn, 180° (her yön) |
| Renk | Mor/magenta `(0.85, 0.15, 0.95)` | Sıcak turuncu `(1.0, 0.35, 0.05)` |
| Saldırı jetonu | Gerekmez | Gerekir |
| Sonraki bekleme | 1.6 sn | 2.2 sn |

- **Hazırlık sırasında** Göz tamamen durağandır ve oyuncuyu takip etmeye devam eder (cezalandırma penceresi).
- **Ağır saldırıda** nişan, launch'tan **0.15 sn önce kilitlenir** (`heavyAimLockSeconds`); son anda yön değiştiren oyuncu kaçabilir. Kilitlenince çizgi beyaza döner.
- **Hafif saldırıda** nişan launch anında hesaplanır.
- Hazırlık iptal olursa (stagger, stun, hedef kaybı) atış yapılmaz ve bekleme en az **1.0 sn** olur (`cancelledAttackCooldown`).

> [!note] Hafif ok şarj küresi
> Hafif hazırlıkta mor/magenta küçük bir şarj küresi de gösterilir (`lightChargeScaleFraction` = ağır kürenin 0.45'i) ve gövde tint'i mora kayar. Nişan çizgisi yalnız ağır saldırıdadır.

---

## 3. PLANLAYICI (L, L, H) VE SALDIRI JETONU

**`FlyingEyeAttackPlanner`** (POCO, `RetroFPS.Combat`): `new FlyingEyeAttackPlanner(lightsBeforeHeavy = 2)`.
- Sıra: **L, L, H, L, L, H ...**
- `WantsHeavy` = en az 2 hafif atış yapıldı mı (durum değiştirmez).
- `Next(heavyAllowed)`: `WantsHeavy && heavyAllowed` ise Heavy üretir ve sayacı sıfırlar; değilse Light üretir ve sayacı artırır. Yani jeton alınamazsa Göz hafif atar ama ağır istemeye devam eder.
- Spawn ve yeniden doğuşta `Reset()` çağrılır.

**Jeton kuralı** (`EnemyAttackTokens`, Ghoul ile ortak havuz):
- Yalnızca **ağır** saldırı jeton alır. Hafif ok asla jeton tutmaz.
- Hazırlık başında `WantsHeavy` ise `TryAcquire()` denenir; alınırsa Heavy hazırlanır, alınamazsa Light hazırlanır.
- Ağır hazırlık boyunca jeton tutulur, **launch anında** bırakılır.
- Jeton **tam bir kez** bırakılır (`ReleaseToken`, bool korumalı). Bırakma noktaları: launch, hazırlık iptali, stagger, `Stun`, ölüm, `OnDespawnToDirector`, `OnDisable`, yeniden doğuş.

---

## 4. HAREKET

- **Combat strafe:** `flySpeed × 0.9` (≈3.4 m/s; eski 1.7 m/s). Yön 1.2–2.4 sn'de bir değişir. Mesafe ideal aralıktaysa (`preferredDistance 9.5` ± `approachDistanceSlack 2.5`) yanal süzülür; çok yakınsa (`minDistance 5`) geri çekilir; çok uzaksa yaklaşır.
- **Reposition (yeni durum):** Her launch sonrası **0.55 sn**, oyuncu etrafında teğet yönde `flySpeed × 2.4` patlama. Yan duvara çarparsa (`CollisionFlags.Sides`) yön değişir. Bu sürede yeni bir **hover hedef yüksekliği** seçilir (`[minFlyHeight + 0.3, maxFlyHeight]` aralığında rastgele). Sonra Combat'a dönülür.
- **Hover:** Sabit `hoverHeight` artık hedef değildir; hover hedefi `_hoverTarget` olarak rastgele değişir. Yükseklik **zemine göredir**: min 1.8 m, max 3.6 m (eski not "3-6 m" yanlıştı). Bob dalgalanması korunur.
- **Hit jink:** Combat içinde, stagger yaratmayan bir hasarda, **1.2 sn** iç bekleme ile **0.3 sn** yanal patlama (`flySpeed × 2.0`). Windup ve Reposition sırasında tetiklenmez.
- **Windup:** Tamamen durağan; oyuncuya bakar; hover düzeltmesi uygulanmaz.
- Lose-sight, alert hub (`alertRadius 12`), patrol, knockback ve pause kapısı değişmedi.

---

## 5. PARRY ETKİLEŞİMİ

### 5.1 Yansıma yönelimi (reflect homing)
- Parry ile yansıyan mermi, `ReflectHomingDegPerSec > 0` ise **orijinal atıcıya** doğru döner (`Quaternion.RotateTowards`, her Update).
- Yönelim yalnızca atıcı aktif, hayatta (`IEncounterEnemy.IsAlive`) ve mermi ileri yönünden `ReflectHomingMaxAngle` içindeyse çalışır.
- Eski `Initialize(...)` ile başlatılan mermiler yönelim yapmaz (0).

### 5.2 `IReflectPunishable` (cezalandırma)
Yansıyan mermi **kendi atıcısına** çarparsa `TakeDamage` yerine `OnOwnProjectileReflectedHit(heavy, damage, point, normal)` çağrılır. Hitstop ve `OnDeflectedImpact` olayı aynen kalır.

| Durum | Sonuç |
| :--- | :--- |
| Hafif yansıdı | `TakeDamage(damage, ..., critical: true)`. Yansıtılmış hasar 18 × 2.5 = 45, kritik olduğu için stagger uygular. |
| Ağır yansıdı, normal Göz | Garantili öldürme: `TakeDamage(max(damage, canı + 1))`, gib sayısı 10 ve `SpawnOccultEnergyBurst(…, true)`. |
| Ağır yansıdı, **elit** | `TakeDamage(0.45 × maxHealth)`. Ölmediyse **Grounded** stun. |
| Grounded stun (elit) | **3.0 sn** yerde (Stagger, `_grounded`). Hover hedefi 0.7 m (zemin üzeri), kayma yok, `IsStunned` true, `_stunSequence` artar. Bekleme = süre + 0.6 sn. Süre bitince yükselir ve Combat'a döner. Bıçak hamlesi bu yüksekliğe erişebilir. |

### 5.3 Normal `Stun` ve parry
- `Stun(duration, dir)`: windup iptal, `_cooldownTimer = max(kalan, süre + 0.6)`. Grounded iken itme sıfırdır.

---

## 6. DOST ATEŞİ KURALI

- **Yansımamış** düşman mermisi, `IEncounterEnemy` olan **diğer** düşmanların collider'larından **geçer**: etki yok, hasar yok. Atıcının kendisi de yok sayılır. Ayrım `CombatTargetLookup.Get(collider).Enemy` ile yapılır (önbellekli, `GetComponentInParent` yok).
- **Yansıtılmış** mermi normal şekilde düşmanlara çarpar.
- En yakın çarpışma seçimi de atıcıyı atladığı gibi bu düşmanları atlar. Fizik maskesi değişmedi (düşman kökleri Default katmanında).
- Ölçülen etki: 31 atıştan 20'si Ghoul'a gidiyordu; bu kural bunu hedefler (ölçüm yeniden alınmalı).

---

## 7. HAVUZ (`EyeProjectilePool`)

- Statik havuz. `Launch(shooter, position, direction, in settings)` pasif bir orb bulur ya da oluşturur, ayarları uygular ve etkinleştirir. Sıfır tahsis (warm-up sonrası).
- `Prewarm(6)` her Göz'ün `Awake`'inde çağrılır.
- Orb: primitive sphere, trigger collider, `GameLayers.Projectile`, `EnemyProjectile`, `TrailRenderer`. Kök `Eye Projectile Pool` (`HideInHierarchy`, play'de `DontDestroyOnLoad`).
- **Tek paylaşımlı** orb malzemesi (URP Unlit) ve trail malzemesi (Particles Unlit). Renk `MaterialPropertyBlock` ile verilir (yansımamış = `settings.Color`, yansımış = cyan). Trail gradyanları statik önbellekli.
- Havuzdaki orb `Release()` ile `SetActive(false)` olur. Judge boss gibi havuzsuz mermiler hâlâ `Destroy` kullanır.
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` ile statikler sıfırlanır (domain reload kapalıyken de güvenli).
- Oyuncu parry listesi (`PlayerParryController[]`) yalnızca boşken veya yok edilmiş giriş varsa yenilenir.
- `EnemyProjectileSettings` bir `struct`'tır ve Eye iki ayar yapısını Awake/elit ölçeklendirmede **bir kez** kurar.

---

## 8. POCO / VIEW AYRIMI VE TESTLER

| Sınıf | Tür | Sorumluluk |
| :--- | :--- | :--- |
| `FlyingEyeAI` | MonoBehaviour | Durum makinesi, jeton, hasar, stagger, elit, ölüm/yeniden doğuş. Sunum yapmaz. |
| `FlyingEyeView` | MonoBehaviour (sunum) | Renk, emisyon, şarj küresi, nişan çizgisi, flash, grounded flicker. Oyun durumu tutmaz. `Tick` AI tarafından çağrılır. |
| `FlyingEyeAttackPlanner` | POCO | L-L-H sırası. |
| `ProjectileLead` | Statik POCO | Kısmi öngörü (intercept) yönü, hız sıfır veya çözüm yoksa düz nişan. |

- `FlyingEyeView` tüm referansları null-safe kullanır. Eksik sanat parçası hata vermez. Ayrıca `MaterialPropertyBlock` kullanır ve property ID'lerini önbellekler.
- `FlyingEyeAI` içinde `EnsureFeaturesExist`, malzeme onarım kodu ve `SetColor/ClearFlash` kaldırıldı.
- EditMode testleri (`Assets/_Project/Tests/EditMode/`): `ProjectileLeadTests`, `FlyingEyeAttackPlannerTests`, `EnemyProjectileFriendlyFireTests`, `FlyingEyePrefabTests`.

---

## 9. PREFAB HİYERARŞİSİ

Mevcut `Enemy_FlyingEye_Watcher.prefab` yerinde güncellenir (GUID ve referanslar korunur). Kök bileşenler ve serileştirilmiş değerler aynı kalır.

```text
Enemy_FlyingEye_Watcher          (CharacterController, FlyingEyeAI, FlyingEyeView, droppers)
├─ Visual                        ← sadece sanat, collider ve script YOK; kullanıcı gerçek modelle değiştirir
│   └─ Body_Sclera, Eye_Iris, Pupil_Visual, Horn_Top, Horn_L, Horn_R,
│      Tendril_Center, Tendril_L, Tendril_R
├─ Hitboxes                      ← oynanış collider'ları, sanattan bağımsız
│   └─ Hitbox_Pupil              (SphereCollider r=0.18, kritik bölge, Hitbox)
├─ Sockets
│   ├─ Socket_FirePoint          (yerel 0, 0, 0.65)
│   ├─ Socket_Telegraph          (yerel 0, 0, 0.75)
│   └─ Socket_DeathFx            (yerel 0, 0, 0)
└─ Telegraph
    ├─ ChargeOrb                 (placeholder küre, collider yok, başlangıçta kapalı, ölçek 0)
    └─ AimLine                   (LineRenderer, 2 nokta, dünya uzayı, genişlik 0.04, kapalı)
```

- Placeholder malzemeler: `Assets/_Project/Art/Enemies/FlyingEye/Placeholder/Mat_Eye_ChargeOrb.mat`, `Mat_Eye_AimLine.mat` (URP Unlit). Çalışma anında oluşturulmaz.
- `Pupil_Head` yerine `Pupil_Visual` kullanılır; collider `Hitbox_Pupil`'a taşınmıştır.

---

## 10. BUILDER MENÜSÜ

- Menü: **`RetroFPS/Build Flying Eye`** (`Scripts/Editor/FlyingEyeBuilder.cs`).
- Play Mode'da çalışmaz. Prefab yoksa hata verir.
- `PrefabUtility.LoadPrefabContents` → düzenle → `SaveAsPrefabAsset` → `UnloadPrefabContents`. İdempotenttir: ikinci çalıştırma fark üretmez.
- `public static ConfigureRoot(GameObject)` editör kurulum araçları (ör. `QuickSetup`) tarafından da kullanılabilir.
- Tüm `FlyingEyeView` ve `FlyingEyeAI.view / firePoint` referansları `SerializedObject` ile bağlanır.

---

## 11. AYAR TABLOSU (Kodda Serileştirilmiş Varsayılanlar)

| Bileşen | Alan | Varsayılan |
| :--- | :--- | :--- |
| `FlyingEyeAI` | `maxHealth` / `respawnTime` / `threatCost` | 90 / 4.0 / 2 |
| `FlyingEyeAI` | `flySpeed` / `combatStrafeFactor` / `repositionSpeedFactor` | 3.8 / 0.9 / 2.4 |
| `FlyingEyeAI` | `repositionDuration` / `hoverTargetInset` | 0.55 sn / 0.3 m |
| `FlyingEyeAI` | `minFlyHeight` / `maxFlyHeight` / `groundedHeight` | 1.8 / 3.6 / 0.7 (zemine göre) |
| `FlyingEyeAI` | `hoverSpeedFactor` / `rotationSpeed` | 2 / 8 |
| `FlyingEyeAI` | `preferredDistance` / `minDistance` / `approachDistanceSlack` | 9.5 / 5 / 2.5 |
| `FlyingEyeAI` | `detectionRange` / `fieldOfView` / `attackRange` / `loseSightDistance` | 22 / 125° / 20 / 26 |
| `FlyingEyeAI` | `lightsBeforeHeavy` / `projectilePrewarmCount` | 2 / 6 |
| `FlyingEyeAI` | `aimHeight` / `fallbackFireOffset` | 1.0 / 0.65 |
| `FlyingEyeAI` | Hafif: `lightWindup` / `lightCooldown` / `lightProjectileSpeed` | 0.45 / 1.6 / 16 |
| `FlyingEyeAI` | Hafif: `projectileDamage` / `lightRadius` / `lightScale` / `lightLifetime` | 18 / 0.35 / 0.40 / 5 |
| `FlyingEyeAI` | Hafif: `lightLead` / `lightMaxLead` | 0.6 / 0.6 sn |
| `FlyingEyeAI` | Hafif yansıma: `lightReflectTurnRate` / `lightReflectAngle` | 360°/sn / 75° |
| `FlyingEyeAI` | Ağır: `heavyWindup` / `heavyCooldown` / `heavySpeed` | 1.1 / 2.2 / 9 |
| `FlyingEyeAI` | Ağır: `heavyDamage` / `heavyRadius` / `heavyScale` / `heavyLifetime` | 35 / 0.6 / 0.9 / 7 |
| `FlyingEyeAI` | Ağır: `heavyLead` / `heavyMaxLead` | 0.9 / 0.8 sn |
| `FlyingEyeAI` | Ağır yansıma: `heavyReflectTurnRate` / `heavyReflectAngle` | 360°/sn / 180° |
| `FlyingEyeAI` | `heavyAimLockSeconds` / `cancelledAttackCooldown` | 0.15 / 1.0 sn |
| `FlyingEyeAI` | `jinkDuration` / `jinkSpeedFactor` / `jinkCooldown` | 0.3 / 2.0 / 1.2 sn |
| `FlyingEyeAI` | `eliteHeavyReflectDamageFraction` / `groundedStunDuration` | 0.45 / 3.0 sn |
| `FlyingEyeAI` | `stunRecoveryCooldown` / `reflectedKillMargin` | 0.6 sn / 1 |
| `FlyingEyeAI` | `reflectedHeavyGibCount` / `normalGibCount` | 10 / 6 |
| `FlyingEyeAI` | `staggerDamageThreshold` / `damageStaggerDuration` | 30 / 0.3 sn |
| `FlyingEyeAI` | `hitKnockbackFactor` / `min-max HitKnockback` / `knockbackDecay` | 0.12 / 2–9 / 7 |
| `FlyingEyeAI` | `stunKnockbackSpeed` | 24.5 |
| `FlyingEyeAI` | `criticalHitstop` / `stunHitstop` / `deathHitstop` | (0.04, 0.03) / (0.08, 0.02) / (0.07, 0.03) |
| `FlyingEyeView` | `chargeMinScale` / `chargeMaxScale` / `pulseFrequency` | 0.1 / 1.0 / 18 rad/sn |
| `FlyingEyeView` | `lightChargeScaleFraction` | 0.45 |
| `FlyingEyeView` | `glowIntensity` / `groundedFlickerFrequency` / `groundedMinTint` | 3 / 24 rad/sn / 0.35 |
| `EnemyProjectile` | `Deflect` varsayılanı: hız / hasar çarpanı | ×1.7 / ×2.5 |
| `EnemyProjectile` | `deflectedScale` / `heavyDeflectScaleMultiplier` | 0.55 / 1.15 |
| `EnemyProjectile` | `reflectedHitstopDuration` / `reflectedHitstopTimeScale` | 0.08 / 0.02 |

---

## 12. UYUMLULUK NOTLARI VE AÇIK DURUM

- **Dalga sistemi:** `IEncounterEnemy` ve `IThreatCost` (2) korunur. `OnSpawnFromDirector` `autoRespawn`'ı kapatır; `OnDespawnToDirector` jetonu bırakır. Bkz. [[WaveAndEncounterArchitecture]].
- **Jetonlar:** Yalnız ağır saldırı jeton kullanır. Ghoul ile aynı havuz; jeton hiçbir durumda sızmamalı (her bırakma noktası korunur).
- **Elit ölçeklendirme:** `IEliteScalable` kurallarına uyar. Hasar (hafif ve ağır), hız ve can taban değerlerinden bir kez çarpılır; `Respawn` sırasında sıfırlanır. Ağır hasar da dahildir.
- **Pause:** `FlyingEyeAI.Update` ve `EnemyProjectile.Update` `GamePause.IsSimulationBlocked` kapısıyla başlar. `FlyingEyeView.Tick` AI'den çağrıldığı için onunla birlikte durur.
- **Judge boss:** `EnemyProjectile.Initialize` (eski yol) değişmedi; yönelim 0 ve havuzsuz `Destroy`.
- **Kod durumu (2026-11-16):** Hafif yansıma 360°/sn / 75° (ilk değer 120°/sn / 30° idi; yansıtılan ok 8 denemede 1 isabet ettiği için yükseltildi, sonrası 12'de 9) ve hafif hazırlıktaki mor şarj küresi kodda ve prefab'da uygulanmıştır. Eski `projectileSpeed` / `attackWindup` / `attackCooldown` alanları kaldırılmıştır.
- **Doğrulanmamış / açık:** Tüm ölçümler script ile sürülmüştür; elle oynanış testi ve sayıların ayarı beklemektedir. Gym'de çalışma anında kurulan Gözler `FlyingEyeView` olmadan (görselsiz yedek yol) çalışır. Ağır şarj için ses yoktur.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[EnemyAIAndSpawning]] ➔ `specialises_enemy_ai`
- [[EnemyAIAndCombatFeel]] ➔ `shares_sensory_rules`
- [[CombatAndDamageInterfaces]] ➔ `implements_reflect_punish`
- [[WaveAndEncounterArchitecture]] ➔ `controlled_by_wave_director`
- [[GameFeelArchitecture]] ➔ `presents_hit_and_charge_feedback`
- [[CodebaseDependencyGraph]] ➔ `implements_enemy_core`
- ⬅️ [[START_HERE_AI_CONSTITUTION]] ➔ `mandated_by_constitution` *(gelen)*
