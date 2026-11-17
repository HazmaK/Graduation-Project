# Dövüş Çekirdeği (Combat Core)

> **Durum**: Taslak
> **Kaynak**: koddan tersine belgeleme (2026-11-17)
> **Kapsam**: hasar, sersemletme ve karşılaşma sözleşmeleri. Silah, düşman ve
> oyuncu canı kendi GDD'lerinde.
> **Yol kökü**: aşağıdaki dosya yolları `Graduation Project/Assets/_Project/Scripts/` altındadır.

## 1. Overview

Silahların, düşmanların, boss'un ve dalga direktörünün birbirini somut sınıf
olarak tanımadan konuşmasını sağlayan arayüz katmanı. Bir vuruş, vurulan
collider'dan hitbox'a, oradan varlığa akar; sersemletme, kalkan, elit ölçekleme
ve saldırı sırası ayrı küçük sözleşmelerdir.

## 2. Player Fantasy

Her vuruş okunur ve tutarlıdır: kafaya vurmak her düşmanda ödüllendirir,
sersemleyen düşman gerçekten durur, aynı anda en fazla iki düşman saldırır.

## 3. Detailed Rules

- **Hasar:** `IDamageable.TakeDamage(amount, hitPoint, hitNormal, isCritical)`.
  (`Core/Interfaces/IDamageable.cs:17`)
- **Hitbox:** gelen hasarı `damageMultiplier` ile çarpar, bölge kritikse kritik
  bayrağını açar, sonra sahibine iletir. (`Combat/Hitbox.cs:71-73`)
- **Sersemletme:** `IStunnable.Stun(duration, knockbackDir)`. Yön verilir,
  savrulma hızı her düşmanın kendisindedir. (`Core/Interfaces/IStunnable.cs:16`)
- **Sersemleme durumu:** `IStunState` kabul edilen her stun için artan bir sıra
  numarası verir; ödüller (Kan Hasadı) bunu okur. (`Combat/IStunState.cs`)
- **Yakın dövüş kalkanı:** `IMeleeGuard.BlocksMeleeFrom(attackerPosition)`.
  (`Combat/IMeleeGuard.cs:9`)
- **Kalkan kırma:** `IGuardBreakable.BreakGuard(duration)`.
  (`Core/Interfaces/IGuardBreakable.cs:7`)
- **Vekil collider:** zayıf nokta ve kalkan plakaları `IDamageProxy.DamageOwner`
  ile sahibini bildirir; aynı saldırıda tek vuruş sayılır ve Sunder işareti
  sahipte aranır. (`Combat/IDamageProxy.cs`, `Weapons/WeaponBase.cs:133-139`)
- **Yansıyan mermi cezası:** `IReflectPunishable.OnOwnProjectileReflectedHit(heavy, damage, point, normal)`.
  (`Core/Interfaces/IReflectPunishable.cs:9`)
- **Elit ölçekleme:** `IEliteScalable.ApplyEliteScaling(hasar, hız, can)`;
  tekrarlanan çağrı birikmez. (`Core/Interfaces/IEliteScalable.cs:7`)
- **Karşılaşma düşmanı:** `IEncounterEnemy` — ölüm olayı, havuzdan doğuş ve geri
  dönüş. (`Core/Interfaces/IEncounterEnemy.cs`)
- **Tehdit puanı:** `IThreatCost.ThreatCost`, dalga bütçesinde kullanılır.
  (`Core/Interfaces/IThreatCost.cs`)
- **Saldırı jetonu:** aynı anda en fazla 2 yakın dövüş saldırısı.
  (`Combat/EnemyAttackTokens.cs:8`)
- **Sürü alarmı:** `EnemyAlertHub.BroadcastAlert` yarıçap içindeki canlı
  düşmanları uyarır. (`Combat/EnemyAlertHub.cs:42`)
- **Yakın dövüş bağlamı:** `MeleeDamageScope` hasarın yakın dövüşten geldiğini
  bildirir; ölen düşman "infaz" ödülünü buna göre verir. (`Core/MeleeDamageScope.cs`)
- **Öldürme olayı:** `CombatEvents.OnEnemyKilled(position, wasCritical, wasGibbed)`.
  (`Core/CombatEvents.cs:6`)
- **Katmanlar:** Player, Enemy, Projectile, Pickup; sorgu maskeleri yalnızca
  `GameLayers` üzerinden alınır. (`Core/GameLayers.cs`)

## 4. Formulas

- `finalDamage = amount × hitbox.damageMultiplier` (`Combat/Hitbox.cs:71`)
- `finalCrit = isCritical OR hitbox.isCriticalZone` (`Combat/Hitbox.cs:72`)
- `sunderDamage = damage × SunderMarks.GetMultiplier(kökHedef)` — yalnızca ateşli
  silahlar. (`Weapons/WeaponBase.cs:138`)
- **Örnek:** çarpanı 2.0 olan kritik bölgeli hitbox'a 22 hasar → 22 × 2.0 = 44,
  kritik olarak iletilir.
- Çekirdekte genel bir kritik çarpanı YOK; kritik çarpanı hitbox'ın ya da silahın
  kendi değeridir.

## 5. Edge Cases

- Hitbox'ın sahibi bulunamazsa hasar sessizce düşer. (`Combat/Hitbox.cs:69`)
- Jeton serbest bırakma sıfırın altına inmez; oyun başında sayaç sıfırlanır.
  (`Combat/EnemyAttackTokens.cs:28`, `:33`)
- Yok edilmiş nesneye bağlı önbellek kaydı bir sonraki vuruşta yeniden çözülür.
  (`Weapons/WeaponBase.cs:232-236`)
- Tek sahne yüklemesinde hedef önbelleği temizlenir, eklemeli (Araf) yüklemede
  korunur. (`Weapons/WeaponBase.cs:202`)
- Katman TagManager'da tanımlı değilse Default kullanılır ve maskeden dışlanmaz.
  (`Core/GameLayers.cs:77-84`)

## 6. Dependencies

- **Bağımlı olduğu:** yok (temel katman).
- **Buna bağımlı:** silahlar, Karşı Hüküm, üç düşman, Yargıç, dalga direktörü,
  can/cephane düşüşü, Araf (elit ölçekleme), HUD (öldürme işareti).

## 7. Tuning Knobs

Aralıklar ÖNERİ'dir: kodda tanımlı değildir, elle test edilmemiştir.

| Değer | Varsayılan | Kaynak | Önerilen aralık | Etkisi |
|---|---|---|---|---|
| Hitbox hasar çarpanı | 1.0 (prefab başına ayarlanır) | `Combat/Hitbox.cs:15` | 0.25 – 3.0 | Kodda kullanılan en yüksek değer 2.0. 3.0 üstü tek vuruşla öldürmeye, 0.25 altı "vuruş işlemiyor" hissine gider. |
| Hitbox kritik bölge | kapalı | `Combat/Hitbox.cs:18` | açık / kapalı | Bölgeye her vuruşun kritik sayılması |
| Eşzamanlı saldırı jetonu | 2 | `Combat/EnemyAttackTokens.cs:8` | 1 – 4 | 1'de sürü pasif kalır; 4 üstünde aynı anda gelen saldırılar okunmaz. |

## 8. Acceptance Criteria

- Çarpanı 2.0 olan bir hitbox'a 10 hasar, sahibine 20 olarak ulaşır.
- Kritik bölgeli hitbox, kritik olmayan vuruşu kritik olarak iletir.
- Üçüncü jeton isteği reddedilir; bir jeton bırakılınca kabul edilir.
- Vekil collider'lı düşmana tek saldırıda tek hasar uygulanır.
- Elit ölçekleme iki kez çağrılınca değerler ikinci kez çarpılmaz.

## Açık Konular

- **EKSİK (KARAR BEKLİYOR):** hasar tipi, zırh ya da direnç kavramı kodda yok.
  Tasarımda gerekli mi, yoksa bilinçli olarak mı yok, kararlaştırılmadı.
- **EKSİK:** `CombatEvents` yalnızca öldürme olayını taşıyor; hasar ya da stun
  olayı yok.
- **Çelişki:** `CLAUDE.md` `IDamageable` için `RetroFPS.Combat` diyor; kodda
  `RetroFPS` ad alanında. Arayüz dosyaları iki klasöre dağılmış (`Combat/` ve
  `Core/Interfaces/`). Bu belge kodu esas alır.
