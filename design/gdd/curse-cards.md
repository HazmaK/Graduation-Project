# Lanet/Lütuf Kartları (Curse Cards)

> **Durum**: Taslak
> **Kaynak**: tasarım onayı 2026-11-19
> **Yol kökü**: aşağıdaki dosya yolları `Graduation Project/Assets/_Project/Scripts/` altındadır.

## 1. Overview

Lanet/Lütuf kartları, Araf'ta temizlenen odalardan sonra sunulan, koşu boyunca geçerli
çift yönlü (lütuf ve lanet) kalıcı stat değişimleri sağlayan ilerleme sistemidir.
Oyuncunun risk-ödül dengesini ve yapı (build) kararlarını şekillendirir.

## 2. Player Fantasy

Her kart ruhani bir pazarlıktır: bir eksende muazzam bir lütuf elde edilirken, farklı
bir eksende bedeli anında hissedilen bir lanet üstlenilir. Seçimler rastgele güçlenme
değil, bilinçli ve radikal oyun tarzı dönüşümleridir.

## 3. Detailed Rules

- **Sunum Noktası:** Araf'ta temizlenen Combat veya Ambush odasından sonra altarda
  sunulur (`Run/Labyrinth/ArafLabyrinthDirector.cs HandleRoomCleared`).
- **Teklif Sınırı:** Koşu başına en fazla 2 teklif (`CardOfferRoller.MaxOffersPerVisit`).
- **Teklif Akışı:** Her teklifte 2 farklı, daha önce görülmemiş ve sahip olunmamış
  kart sunulur; oyuncu birini seçer veya teklifi reddeder.
- **Kapasite:** Envanter azami 16 kart barındırabilir; stat sayfası toplamda azami
  64 modifier destekler (`Core/Stats/StatSheet.cs:9`).
- **Nadirlik ve Ağırlıklar:**
  - Sıradan (Common): Ağırlık 10
  - Nadir (Rare): Ağırlık 5
  - Kadim (Relic): Ağırlık 1.5 (`CardCatalog` verisi)
- **Çekiliş Algoritması:** Ağırlıklı rezervuar örnekleme: `toplam += w`; eğer
  `Value01 × toplam < w` ise aday seçilir.
  - Havuzda 7 Sıradan / 5 Nadir / 3 Kadim kartla slot başına olasılıklar: ≈ %70 Sıradan / ≈ %25 Nadir / ≈ %4.5 Kadim.
  - Bir koşuda (toplam 4 slot) en az bir Kadim kart görme olasılığı: ≈ %17.
- **Görsel Bildirim:** Teklif ekranında nadirlik etiketi ve renkli kenarlık yer alır:
  - Sıradan: Kemik
  - Nadir: Mor
  - Kadim: Altın
- **Tasarım Kuralı:** Her kartın anlamlı bir lütfu ve farklı bir eksende anlamlı bir
  laneti vardır. Kadim kartlar uç ve oyun tarzı belirleyicidir ancak hiçbir Sıradan
  karttan kesin üstün değildir.

### Kart Tablosu (Card set v2 — catalog order)

| # | id | TR name | EN name | Rarity | Modifiers (stat op value [cond]) |
|---|---|---|---|---|---|
| 1 | blood_debt | Kan Borcu | Blood Debt | Common | DamageDealt AddPercent +0.30; MaxHealth AddPercent -0.25 |
| 2 | starving_steel | Aç Çelik | Starving Steel | Rare | MeleeDamage AddPercent +0.45; HarvestHeal Multiply 1.75; OrbDropChance Multiply 0 |
| 3 | last_breath | Son Nefes | Last Breath | Rare | DamageDealt AddPercent +0.50 [BelowHalf]; MoveSpeed AddPercent +0.20 [BelowHalf]; HealReceived AddPercent -0.35 |
| 4 | bone_wings | Kemik Kanatlar | Bone Wings | Common | DashRechargeTime Multiply 0.8; MoveSpeed AddPercent +0.12; DamageTaken AddPercent +0.25 |
| 5 | brimming_chalice | Dolu Kadeh | Brimming Chalice | Common | OverhealCap AddFlat +50; FireRate AddPercent +0.35 [Overhealed]; MoveSpeed AddPercent -0.15 [BelowHalf] |
| 6 | glutton_barrel | Obur Namlu | Glutton's Barrel | Common | FireRate AddPercent +0.40; AmmoPickupAmount AddPercent -0.50 |
| 7 | iron_hide | Demir Post | Iron Hide | Common | DamageTaken AddPercent -0.20; MoveSpeed AddPercent -0.10 |
| 8 | carrion_crow | Leş Kargası | Carrion Crow | Common | AmmoPickupAmount AddPercent +0.75; OrbDropChance Multiply 0.75 |
| 9 | pilgrims_flask | Hacı Matarası | Pilgrim's Flask | Common | HealReceived AddPercent +0.35; OrbDropChance Multiply 1.5; FireRate AddPercent -0.12 |
| 10 | gilded_vein | Yaldızlı Damar | Gilded Vein | Rare | OverhealCap AddFlat +75; DamageTaken AddPercent -0.25 [Overhealed]; MaxHealth AddPercent -0.20 |
| 11 | executioners_oath | Cellat Yemini | Executioner's Oath | Rare | MeleeDamage AddPercent +0.75; DashRechargeTime Multiply 0.8; DamageDealt AddPercent -0.20 |
| 12 | bell_tower | Çan Kulesi | Bell Tower | Rare | FireRate AddPercent +0.50; DamageTaken AddPercent -0.10; MoveSpeed AddPercent -0.15; DashRechargeTime Multiply 1.2 |
| 13 | judges_scale | Yargıç Terazisi | Judge's Scale | Relic | DamageDealt AddPercent +0.60; DamageTaken AddPercent +0.40 |
| 14 | hollow_saint | Oyuk Aziz | Hollow Saint | Relic | DashRechargeTime Multiply 0.625; MoveSpeed AddPercent +0.25; MaxHealth AddPercent -0.25 |
| 15 | red_hour | Kızıl Saat | Red Hour | Relic | FireRate AddPercent +0.50; MeleeDamage AddPercent +0.50; MoveSpeed AddPercent +0.15; HealReceived AddPercent -0.25; OrbDropChance Multiply 0.5 |

Düzeltmeler sonrası en kötü çiftler (tümü GDD sınırları içinde):
- **MaxHP:** blood_debt + hollow_saint → 0.50x
- **Dash:** bone_wings + hollow_saint → 0.50x
- **MoveSpeed:** bell_tower + brimming_chalice [BelowHalf] → 0.70x
- **HealReceived:** last_breath + red_hour → 0.40x
- **HarvestHeal:** starving_steel + pilgrims_flask → 1.75 × 1.35 = 2.36x
- **Melee:** judges_scale + executioners_oath → 2.45x
- **Kasıtlı sinerji:** carrion_crow, glutton_barrel lanetini iptal eder (net cephane +%25).

## 4. Formulas

Kart modifier'ları stat katmanı formülünü aynen işletir (ayrıntılar için bkz.
`design/gdd/stat-layer.md` Bölüm 4):

`sonuç = max(0, (taban + Σflat) × max(0, 1 + Σpercent) × Πmultiply)`

- Seçilen kartın modifier'ları `StatSheet.BeginBatch()` / `EndBatch()` arasında
  kaynak kimliği olarak `card.id` verilerek eklenir.
- Koşullu modifier'lar (`BelowHalf`, `Overhealed`), `IStatContext` üzerinden canlı
  değerlendirilir.

## 5. Edge Cases

- Havuzda elenmemiş (görülmemiş/sahip olunmamış) kart kalmadığında teklif üretilmez.
- 16 kart envanter sınırı veya 64 modifier sınırı dolduğunda yeni kart eklenemez.
- `MaxHealth` eksilten kartlar (`AddPercent -0.25`) birikse dahi yüzde toplamı çarpanı
  negatife düşürmez; ancak taban canın kritik seviyenin altına inmemesi gerekir.
- Teklif reddedildiğinde sunulan 2 kart görülmüş olarak işaretlenir ve bir sonraki
  teklife gelmez.

## 6. Dependencies

- **Bağımlı olduğu:** `design/gdd/stat-layer.md` (Stat katmanı), Araf labirenti
  (`Run/Labyrinth/ArafLabyrinthDirector.cs`), Koşu yapısı (`RunState`), Seed'li rastgelelik.
- **Buna bağımlı:** Kart seçim UI ekranı; stat katmanı üzerinden dolaylı olarak
  oyuncu sağlığı, hareketi ve silah sistemleri.

## 7. Tuning Knobs

Ağırlıklar, kart parametreleri ve sınır kontrolleri ÖNERİ niteliğindedir (güvenli
aralıklar `design/gdd/stat-layer.md` Bölüm 7'den alınmıştır):

| Değer | Kodda / Katalogda | Önerilen aralık | Etkisi |
|---|---|---|---|
| Sıradan Ağırlığı | 10 | Sabit | Sıradan kart frekansı |
| Nadir Ağırlığı | 5 | 3 – 6 | Nadir kart frekansı |
| Kadim Ağırlığı | 1.5 | 1.0 – 2.0 | Kadim kartların çıkma sıklığı |
| MaxOffersPerVisit | 2 | 1 – 3 | Araf ziyareti başına teklif adedi |
| Envanter Kapasitesi | 16 | 12 – 16 | Koşu başına azami kart sayısı |

Stat başına nihai değer için güvenli sınırlar (ÖNERİ - tabana oran):
- DamageDealt, MeleeDamage: [0.5×, 2.5×]
- FireRate: [0.5×, 2.0×]
- MoveSpeed: [0.7×, 1.5×]
- DashRechargeTime: [0.5×, 2.0×]
- DamageTaken: [0.5×, 2.0×]
- HealReceived, HarvestHeal: [0.25×, 2.5×]
- AmmoPickupAmount: [0.5×, 2.0×]
- MaxHealth, OverhealCap: [0.5×, 2.0×]
- OrbDropChance: [0×, 2.0×]

## 8. Acceptance Criteria

- 15 kart katalogda bu sırada ve eksiksiz tanımlıdır.
- Sıfır ağırlıklı kademe tekliflerde hiç gelmez.
- 2000 tohumlu çekişte 10'a 1.5 oranında Kadim payı 0.08 – 0.18 aralığındadır.
- Aynı tohum (seed) her zaman aynı kart tekliflerini üretir.
- Tüm 2'li kombinasyonlar Bölüm 7'deki güvenli sınırların içinde kalır.

## Açık Konular

- **EKSİK:** Nihai stat sınırları kodda uygulanmıyor (`design/gdd/stat-layer.md`
  Açık Konular'daki eksiklik; kart kombinasyonlarıyla stat'lar aşırı uçlara kayabilir).
- **Mekanik Çeşitliliği:** Kartlar yalnızca mevcut 12 stat'ı kullanmaktadır; yeni
  oynanış mekaniği kazandıran kartlar henüz tasarlanmamıştır.
- **Denge Doğrulaması:** Kartların sayısal dengesi ve oynanış hissi henüz elle
  oynanarak test edilmemiştir.
- **Hasar × atış hızı sınırsız:** tek tek stat'lar sınırda kalsa da çarpımları
  sınırlanmıyor. En güçlü kombinasyon Yargıç Terazisi + Çan Kulesi ≈ 2.4× silah
  çıktısı (/balance-check 2026-11-20). Oynanış testinden sonra değerlendirilecek.
- **Denge geçişi 2026-11-20:** Çan Kulesi alınan hasar -%20 → -%10 (Nadir için
  fazla güçlüydü); Yaldızlı Damar fazla can sınırı +50 → +75 (Dolu Kadeh'in
  gerisinde kalıyordu).
