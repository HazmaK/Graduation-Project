# Stat Katmanı (Stat Layer)

> **Durum**: Taslak
> **Kaynak**: koddan tersine belgeleme (2026-11-17)
> **Yol kökü**: aşağıdaki dosya yolları `Graduation Project/Assets/_Project/Scripts/` altındadır.

## 1. Overview

Oyuncunun sayısal değerlerini (hasar, hız, can, iyileşme, düşüş şansı) tek bir
yerden değiştiren katman. Kartlar bu katmana modifier ekler; silahlar, hareket,
can ve düşüş sistemleri değeri okurken katmandan geçirir.

## 2. Player Fantasy

Alınan her kart anında hissedilir ve koşu boyunca sürer; bedeli de aynı netlikte
hissedilir.

## 3. Detailed Rules

- **12 stat:** DamageDealt, MeleeDamage, FireRate, MoveSpeed, DashRechargeTime,
  MaxHealth, DamageTaken, HealReceived, HarvestHeal, OrbDropChance,
  AmmoPickupAmount, OverhealCap. (`Core/Stats/StatId.cs:7-29`, `Count = 12` `:36`)
- **3 işlem:** AddFlat, AddPercent, Multiply. (`Core/Stats/StatModifier.cs:6-14`)
- **3 koşul:** Always, HealthBelowHalf, Overhealed. (`Core/Stats/StatModifier.cs:17-25`)
- Bir sayfada en fazla 64 modifier. (`Core/Stats/StatSheet.cs:9`)
- Her modifier bir kaynak kimliğiyle eklenir; kaynak kaldırılınca o kaynağın bütün
  modifier'ları gider. (`Core/Stats/StatSheet.cs:56`, `:69`)
- Okuma: `PlayerStats.Get(stat, baseValue)`. Aktif sayfa yoksa taban değer aynen
  döner. (`Core/Stats/PlayerStats.cs:17-18`)
- Koşullar değer okunurken canlı duruma göre değerlendirilir; bağlam
  `IStatContext` (can oranı, overheal durumu). (`Core/Stats/StatSheet.cs:117-126`,
  `Core/Stats/IStatContext.cs`)
- Toplu ekleme: `BeginBatch`/`EndBatch` arasında değişiklik olayı bir kez
  tetiklenir. (`Core/Stats/StatSheet.cs:29-41`)

Stat'ı okuyan sistemler:

| Stat | Okuyan |
|---|---|
| MaxHealth, DamageTaken, HealReceived, OverhealCap | `Player/PlayerHealth.cs:40`, `:160`, `:189`, `:213`, `:216` |
| MoveSpeed, DashRechargeTime | `Player/PlayerMovement.cs:124`, `:146`, `:687` |
| FireRate, DamageDealt | `Weapons/RepeaterArbalestWeapon.cs:142-281`, `Weapons/GothicShotgunWeapon.cs:55-245` |
| DamageDealt, MeleeDamage, HarvestHeal | `Weapons/ExecutionerBladeWeapon.cs:506`, `:572` |
| AmmoPickupAmount | `Combat/AmmoPickup.cs:157` |
| OrbDropChance | `Combat/HealthOrbManager.cs:205` |

## 4. Formulas

`sonuç = max(0, (taban + Σflat) × max(0, 1 + Σpercent) × Πmultiply)`
(`Core/Stats/StatSheet.cs:114`)

- `taban`: okuyan sistemin kendi değeri
- `flat`: aynı birimde ekleme
- `percent`: kesir (0.5 = +%50); aynı stat'ın yüzdeleri toplanır
- `multiply`: çarpan; aynı stat'ın çarpanları birbiriyle çarpılır
- **Örnek:** taban 100, flat +20, percent +0.5 ve −0.3, multiply 1.5
  → (100 + 20) × (1 + 0.2) × 1.5 = 216
- **HealthBelowHalf:** can oranı < 0.5 (eşitlik dahil değil).
  (`Core/Stats/StatSheet.cs:122`)

## 5. Edge Cases

- 64 doluyken ekleme reddedilir, `Add` false döner. (`Core/Stats/StatSheet.cs:58`)
- NaN ya da sonsuz değerli modifier reddedilir. (`Core/Stats/StatSheet.cs:58`)
- MaxHealth'e koşullu modifier reddedilir (can koşulu azami cana bağlı, döngü
  olur). (`Core/Stats/StatSheet.cs:60-61`)
- Yüzde toplamı −%100'ün altına inerse çarpan 0'da kalır; sonuç negatif olmaz.
  (`Core/Stats/StatSheet.cs:114`)
- Bağlam atanmamışsa koşullu modifier'lar uygulanmaz.
  (`Core/Stats/StatSheet.cs:122-123`)
- `Clear` bağlamı korur. (`Core/Stats/StatSheet.cs:88`)

## 6. Dependencies

- **Bağımlı olduğu:** yok.
- **Buna bağımlı:** oyuncu canı, oyuncu hareketi, üç silah, cephane ve can küresi
  düşüşü, lanet/lütuf kartları, koşu yapısı (sayfanın sahibi `RunState`).

## 7. Tuning Knobs

Aralıklar ÖNERİ'dir: kodda tanımlı değildir, elle test edilmemiştir.

| Değer | Kodda | Önerilen aralık | Etkisi |
|---|---|---|---|
| Modifier kapasitesi | 64 (`StatSheet.cs:9`) | sabit | 16 kart sınırıyla uyumlu olmalı |
| HealthBelowHalf eşiği | 0.5, kodda sabit (`StatSheet.cs:122`) | 0.3 – 0.5 | "Son nefes" kartlarının ne kadar sık devrede olduğu |

Stat başına nihai değer için önerilen sınırlar (tabana oran):

| Stat | Alt | Üst |
|---|---|---|
| DamageDealt, MeleeDamage | 0.5× | 2.5× |
| FireRate | 0.5× | 2.0× |
| MoveSpeed | 0.7× | 1.5× |
| DashRechargeTime | 0.5× | 2.0× |
| DamageTaken | 0.5× | 2.0× |
| HealReceived, HarvestHeal | 0.25× | 2.5× |
| AmmoPickupAmount | 0.5× | 2.0× |
| MaxHealth, OverhealCap | 0.5× | 2.0× |
| OrbDropChance | 0× | 2.0× |

## 8. Acceptance Criteria

- Örnek hesap 216 verir.
- Aktif sayfa yokken `PlayerStats.Get` taban değeri döndürür.
- 65. modifier eklenemez.
- Bir kaynağın kaldırılması yalnızca o kaynağın modifier'larını siler.
- Can oranı tam 0.5 iken HealthBelowHalf modifier'ı uygulanmaz.
- Değerlendirme sırasında bellek tahsisi olmaz.

## Açık Konular

- **EKSİK:** nihai değerde üst sınır yok. Kartlar birikirse DamageTaken ya da
  DashRechargeTime 0'a inebilir. Bölüm 7'deki sınırlar tasarım hedefidir, kodda
  uygulanmıyor.
- **Kural sapması:** HealthBelowHalf eşiği (0.5) kodda sabit; proje kuralı oynanış
  değerlerinin veriyle ayarlanmasını istiyor.
- OrbDropChance yalnızca can küresini etkiliyor; cephane düşüş şansı için ayrı bir
  stat yok.
