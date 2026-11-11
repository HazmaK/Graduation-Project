---
project: "retro-fps"
type: "InterfaceContract"
tags:
  - project
  - memory
  - "retro-fps"
  - interfaces
  - combat
  - damage
---

# CombatAndDamageInterfaces (Dövüş ve Hasar Arayüzleri)

> [!abstract] AYRIK MİMARİNİN KALBİ (DECOUPLED ARCHITECTURE)
> Bu arayüzler, sistemlerin birbirinin iç mantığını bilmeden, doğrudan `GetComponent<ConcreteClass>()` bağımlılığı olmadan haberleşmesini sağlar.
> Projeye yeni bir düşman, silah veya kırılabilir çevre objesi ekleyen herhangi bir AI bu arayüzleri baz almak zorundadır.

---

## 1. `IDamageable` Arayüzü
- **Konum:** `Assets/_Project/Scripts/Combat/IDamageable.cs`
- **Namespace:** `RetroFPS`
- **Uygulayan Sınıflar:** `BasicEnemyAI`, `FlyingEyeAI`, `CombatDummy`, `PlayerHealth`

```csharp
namespace RetroFPS
{
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false);
    }
}
```

### Kurallar:
- Hasar veren tüm silahlar (`RepeaterArbalestWeapon`, `ExecutionerBladeWeapon`), patlamalar ve parry yansımaları bu arayüzü çağırır.
- `isCritical` parametresi kafa vuruşlarını (Headshot), zayıf nokta vuruşlarını veya zırhsız vuruşları belirtir.
- **Tek sahipli geri bildirim:** Uygulayan sınıf et darbe VFX'ini (`SpawnHitImpact(..., isFlesh: true)`) ve et sesini (`PlayFleshImpact`) kendi `TakeDamage`'inde üretir; çağıran silah üretmez ([[GameFeelArchitecture]]).
- Çalışma anında doğan uygulayıcılar `CombatTargetLookup.RegisterHierarchy(this)` çağırmalıdır (bkz. §5).

---

## 2. `IStunnable` Arayüzü
- **Konum:** `Assets/_Project/Scripts/Combat/IStunnable.cs`
- **Namespace:** `RetroFPS.Combat`
- **Uygulayan Sınıflar:** `BasicEnemyAI`, `FlyingEyeAI`

```csharp
namespace RetroFPS.Combat
{
    public interface IStunnable
    {
        void Stun(float duration, Vector3 knockbackDir);
    }
}
```

### Kurallar:
- Cellat Kılıcı kombo bitiricisi (`ExecutionerBladeWeapon` 3. vuruş / Meteor Slam) veya Gotik Pompalı kancası (`GothicShotgunWeapon` Meat Hook) düşmanı sersemletmek için bu metodu tetikler.
- *(Not: Çivili Buckler kalkanı oyun temposunu sadeleştirmek adına geçici olarak pasife alınmıştır; ilerde ihtiyaç duyulursa yeniden aktif edilebilir).*
- Düşman bu süre boyunca hareket edemez, saldırı başlatamaz ve animasyonunda sersemleme durumuna (`EnemyState.Stagger`) geçer.
- `knockbackDir` **yalnızca yön** olarak yorumlanır (normalize edilir; sıfır vektör = itme yok). İtme hızı uygulayan sınıfın `stunKnockbackSpeed` alanındadır (`BasicEnemyAI` 5.5, `FlyingEyeAI` 24.5).

---

## 3. `IEncounterEnemy` Arayüzü
- **Konum:** `Assets/_Project/Scripts/Combat/IEncounterEnemy.cs`
- **Namespace:** `RetroFPS.Combat`
- **Uygulayan Sınıflar:** `BasicEnemyAI`, `FlyingEyeAI`

```csharp
namespace RetroFPS.Combat
{
    public interface IEncounterEnemy
    {
        event System.Action<IEncounterEnemy> OnEnemyDeath;
        bool IsAlive { get; }
        Transform EnemyTransform { get; }
        GameObject EnemyGameObject { get; }
        void OnSpawnFromDirector(Vector3 spawnPosition, Quaternion spawnRotation);
        void OnDespawnToDirector();
    }
}
```

### Kurallar:
- `WaveSpawnDirector` bir düşmanı doğururken ve dalga takibini yürütürken bu arayüzü kullanır.
- Sıfır GC tahsisi için `OnEnemyDeath` olay delegesi düşman ölünce tetiklenir ve direktör kalan düşman sayısını günceller.
- Nesne havuzu (Object Pool) veya yeniden doğma mekanizmaları `OnSpawnFromDirector` ve `OnDespawnToDirector` üzerinden yönetilir.

---

## 4. `IAlertableEnemy` Arayüzü
- **Konum:** `Assets/_Project/Scripts/Combat/BasicEnemyAI.cs`
- **Namespace:** `RetroFPS.Combat`
- **Uygulayan Sınıflar:** `BasicEnemyAI`

```csharp
namespace RetroFPS.Combat
{
    public interface IAlertableEnemy
    {
        void Alert(Vector3 targetPosition);
    }
}
```

### Kurallar:
- Sürü alarmı (`EnemyAlertHub`) tarafından menzil içindeki müttefik düşmanları devriye modundan savaş moduna geçirmek için çağrılır.

---

## 5. `Hitbox.cs` Bileşeni
- **Konum:** `Assets/_Project/Scripts/Combat/Hitbox.cs`
- **Namespace:** `RetroFPS.Combat`
- **İşlevi:** Karakterlerin kafa, gövde veya bacak collider'larına takılır. Hasar çarpanı (`damageMultiplier`) ve `isCritical` bayrağını belirler.
- Raycast veya yakın dövüş temasında önce `Hitbox` aranır; bulunursa çarpanı hesaplayıp ebeveyndeki `IDamageable.TakeDamage` metoduna aktarır.
- **Çözümleme:** Silahlar `GetComponent` yerine `CombatTargetLookup.Get(collider)` kullanır (`internal`, `Weapons/WeaponBase.cs` içinde). Dönen `Binding`: `Hitbox`, `Damageable` (Hitbox varsa Hitbox), `Stunnable`, `Enemy` (`IEncounterEnemy`), `Entity` (`Hitbox.Target ?? Damageable`). Önbellek sahne yüklemesinde doldurulur; ıskada `GetComponentInParent`'a düşer ve sonucu (boş dahil) önbelleğe yazar. Runtime'da doğan `IDamageable`'lar `RegisterHierarchy(this)` çağırmalıdır. Ayrıntı: [[GameFeelArchitecture]].

---

## 6. `CombatEvents` (Statik Dövüş Olayları)
- **Konum:** `Assets/_Project/Scripts/Core/CombatEvents.cs` (assembly `RetroFPS.Core`)
- **Namespace:** `RetroFPS.Combat`

```csharp
public static class CombatEvents
{
    // position, wasCritical, wasGibbed
    public static event System.Action<Vector3, bool, bool> OnEnemyKilled;
    public static void RaiseEnemyKilled(Vector3 position, bool wasCritical, bool wasGibbed);
}
```

### Kurallar:
- `BasicEnemyAI` ve `FlyingEyeAI` `Die` içinde `OnEnemyDeath`'ten hemen önce yayınlar; `CrosshairHUD` kill confirm için dinler.
- Abonelik `OnEnable`/`OnDisable` çiftinde yapılmalıdır; statik olay `SubsystemRegistration`'da sıfırlanır.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[CodebaseDependencyGraph]] ➔ `implements_graph`
- [[WaveAndEncounterArchitecture]] ➔ `supplies_encounter_contract`
- [[EnemyAIAndSpawning]] ➔ `governs_enemy_behavior`
- [[WeaponArsenalArchitecture]] ➔ `receives_damage_from_weapons`
- [[PlayerSubsystemArchitecture]] ➔ `implements_player_health`
- [[GameFeelArchitecture]] ➔ `cached_by_target_lookup`
- ⬅️ [[START_HERE_AI_CONSTITUTION]] ➔ `mandated_by_constitution` *(gelen)*
