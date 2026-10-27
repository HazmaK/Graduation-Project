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
- `PlayerParryController` tarafından yakın dövüş saldırısı savuşturulduğunda düşmana uygulanır.
- Ağır kılıç saplamaları veya yüksek darbe gücüne sahip özel saldırılar düşmanı sersemletmek için bu metodu tetikler.
- Düşman bu süre boyunca hareket edemez, saldırı başlatamaz ve animasyonunda sersemleme durumuna (`EnemyState.Stagger`) geçer.

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

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[CodebaseDependencyGraph]] ➔ `implements_graph`
- [[WaveAndEncounterArchitecture]] ➔ `supplies_encounter_contract`
- [[EnemyAIAndSpawning]] ➔ `governs_enemy_behavior`
- [[WeaponArsenalArchitecture]] ➔ `receives_damage_from_weapons`
- [[PlayerSubsystemArchitecture]] ➔ `implements_player_health`
- ⬅️ [[START_HERE_AI_CONSTITUTION]] ➔ `mandated_by_constitution` *(gelen)*
