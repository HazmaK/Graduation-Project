---
project: "retro-fps"
type: "GameMechanic"
tags:
  - project
  - memory
  - "retro-fps"
  - purgatory
  - procedural
  - labyrinth
---

# ArafLabyrinthSystem (Araf Kemik Labirenti ve Oda Zarları)

> [!abstract] PROSEDÜREL ARAF LABİRENTİ VE ODA ZARLAMA SİSTEMİ
> Seed'li ızgara koridorlar, perdeli 2x2 hücrelik oda yuvaları, dinamik zarla belirlenen oda içerikleri, pity mekanizmalı kaçış portalı ve elit muhafız dövüşleri.

---

## 1. GENEL BAKIŞ VE MEKANİK DİNAMİKLERİ
- **Prosedürel Labirent:** Araf artık statik bir arena değil; seed'li, 9×9 grid koridorlar ve 2×2 hücrelik oda yuvalarından oluşan prosedürel bir kemik labirenttir.
- **Kara Perde (Veil) ve Dinamik Zar:** Oda içerikleri, oyuncu yuvanın kapısındaki kara perdeyi (veil) geçene kadar bilinmez; geçince oda tipi ağırlıklı zarla belirlenir ve ilgili prefab dinamik olarak doğar.
- **Kaçış Koşulu:** Kaçış portalı sadece **Çıkış (Exit)** odasında yer alır. Oyuncu çıkış odası denk gelene kadar labirenti keşfetmek zorundadır.
- **Pity (Çıkış Şansı Artışı):**
  - Taban çıkış şansı **%10**'dur.
  - Açılan her çıkış-dışı oda için şans **+%15** artar (maksimum %100).
  - `exitMinDepth = 3`: Başlangıç hücresine 3 hücreden yakın yuvada çıkış odası asla çıkmaz.
  - Son açılmamış yuva her zaman garantili çıkıştır. Bir run boyunca tam olarak tek bir çıkış odası üretilir.

---

## 2. ODA TİPLERİ VE İÇERİKLERİ
Tanımlar `Assets/_Project/Data/Araf/Rooms/`, prefablar `Assets/_Project/Prefabs/Araf/Rooms/` altında bulunur.

| Oda Tipi | Ağırlık / Kural | İçerik ve Davranış |
| :--- | :--- | :--- |
| **Combat_Ghouls** | Ağırlık: 3 | 3 Elit Ghoul. Kapılar (`Kit_DoorBarrier`) oda temizlenene kadar kapatılır. |
| **Combat_Mixed** | Ağırlık: 2 | 2 Elit Ghoul + 1 Elit Uçan Göz (Flying Eye). Kapılar temizlenene kadar kapatılır. |
| **Ambush** | Ağırlık: 1.5 (`minDepth = 2`) | Odanın ortasına girince 3 Ghoul uyanır ve saldırır; kapılar kapatılır. |
| **Shrine** | Ağırlık: 1 (Run başına maks 1) | Sunak odası: 50 can kazandırır (150'ye kadar overheal sağlar). Kapı mühürlemez. |
| **Ossuary** | Ağırlık: 1.5 | Kemiklik / Cephane odası: Cephane takviyesi sağlar. Kapı mühürlemez. |
| **Exit** | Dinamik Pity Zarı | 2 Elit Muhafız Ghoul + Mühürlü Kan Portalı. Muhafızlar ölünce portal açılır. |

- **Elit Düşman Katsayıları:** Hasar ×1.4, Hız ×1.4, Can ×1.25.
- **Kapı Bariyerleri:** Savaş odaları temizlenene kadar kapılarını (`Kit_DoorBarrier`) kapatır.

---

## 3. MİMARİ VE BİLEŞENLER
Namespace: `RetroFPS.Run.Labyrinth` (`Scripts/Run/Labyrinth/`).

| Sınıf / Varlık | Türü / Konumu | Sorumluluğu |
| :--- | :--- | :--- |
| `LabyrinthTypes.cs` | POCO / Structs | `LabyrinthDir`, `GridPos`, `Doorway`, `LabyrinthSettings` (9×9, hücre 6 m, 7 oda, braid 0.15, oda arası boşluk 1, oda başına en çok 2 kapı). |
| `LabyrinthLayout` & `LabyrinthGenerator` | Saf C# | Saf C# labirent üretimi: Oda bloklarını yerleştir → koridorlarda iteratif recursive-backtracker → oda kapıları → braid ile çıkmaz sokakların bir kısmını aç → BFS derinlik. |
| `ArafRoomDefinition` & `ArafRoomTable` | ScriptableObject | Oda tanımları (ağırlık, minDepth, maxPerRun, prefab) ve varsayılan oda tablosu. |
| `ArafRoomRoller` | Saf C# | Saf C#, pity kurallarına göre oda türü zarlama mantığı. |
| `ArafRoom` & `ArafRoomTriggerRelay` | MonoBehaviour | Oda mantığı: Elit uygulama, temizlenme, portal mührü, shrine iyileştirme, ambush tetikleyicisi. |
| `LabyrinthKit` | ScriptableObject | Palet ScriptableObject'i: Duvar, zemin, tavan, sütun, perde (veil), kapı bariyeri (`Kit_DoorBarrier`), meşale prefabları. |
| `ArafRoomSlot` & `ArafRoomSlotTrigger` | MonoBehaviour | 2×2 oda yuvaları ve kapıdaki perde tetikleyicisi. |
| `ArafLabyrinthDirector` | MonoBehaviour | `ArafLevel` köküne bağlı; Araf sahnesi arka planda yüklenirken `Start()`'ta labirenti `Content/Labyrinth_Generated` altına kurar, `EntryPoint`'i başlangıç hücresine taşır, kapı tetikleyicisinde odayı zarlar. |

---

## 4. SEED AKIŞLARI
- `RunStream.ArafLayout` (5): Labirent yerleşimi (koridorlar, duvarlar ve oda yuvaları).
- `RunStream.ArafRooms` (6): Oda zarları.
- **Determinizm:** Aynı seed → aynı labirent; oda sonuçları aynı seed + aynı keşif sırası ile birebir tekrar üretilir.

---

## 5. HUD VE BİLDİRİMLER
- `RunEvents.OnArafRoomsChanged(revealed, total, exitFound)` olayı tetiklenir.
- `RunHUD` üzerinde "ODALAR x/7" ve çıkış odası açıldığında "ÇIKIŞ BULUNDU" bilgisi gösterilir.

---

## 6. SAHNE VE PREFAB BUILDER
- `ArafSceneBuilder.BuildAll()`:
  - Kit ve oda prefablarını, tanımları, `ArafRoomTable_Default`, `LabyrinthKit_Araf` ve Araf sahnesini (kök Y = +1000) üretir.
  - Eski `ArafTrial` sınıfı emekliye ayrıldı (silindi).

---

## 7. DOĞRULAMA VE TESTLER (2026-11-11)
- **EditMode Testleri:** 61 testin tamamı geçti:
  - 47 labirent üreteci testi: Determinizm, bağlantılılık, duvar simetrisi, oda boşlukları, kapı geometrisi.
  - 7 oda zarlayıcı testi: Çıkış tam 1 kez, son yuva garantisi, `minDepth`, `maxPerRun`, şans artışı.
  - 13 RNG / RunState testi.
- **Play Mode Testi:** 9×9, 7 oda, 394 nesne oluşturuldu; Ossuary → Combat → Exit (şans %25 → %40) akışı izlendi, muhafızlar ölünce portal açıldı, kuleye dönüş başarılı oldu.

---

## 8. BİLİNEN EKSİKLER VE SONRAKİ ADIMLAR
- Minimap / harita yok (labirentte kaybolma riski).
- `RunHUD` Araf yazısı koyu kızıl — kontrast düşük.
- Oda prefabları basit küp sahne formatındadır.
- Faz 3 lanet / lütuf kartları, temizlenen Combat ve Ambush odalarındaki sunaklardan (altar) gelir; ayrıntılar için bkz. [[CurseBlessingCards]].

---

## 9. YENİDEN TASARIM (2026-11-26) — ONAYLANDI, HENÜZ KODLANMADI

> [!warning] Bu bölüm planlanan tasarımdır. Yukarıdaki 1–8. bölümler hâlâ koddaki mevcut durumu anlatır. Fazlar kodlandıkça yukarıdaki bölümler güncellenecek.

### Çözülen problemler
- **Erken ölüm açığı:** Kat 1'in ilk dalgasında ölmek de 2 kart teklifi veriyordu; oyuncu bilerek ölüp kartlı devam ediyordu.
- **Anlamsız labirent:** Çıkış rastgele pity zarıyla belirleniyordu; keşfin oyuncuya faydası yoktu, koridorlar boştu.
- **Küçük ve kolay:** Tek 9×9 labirent, 7 oda, 3-5 düşman.
- **Atmosfer:** Kızıl tonlar cehennemi andırıyordu, ürpertici değildi.

### Kullanıcı kararları
| Konu | Karar |
| :--- | :--- |
| Ölüm bedeli | Derinlik ölçekleme + giriş vergisi (Araf Yarası) |
| Araf Yarası | Girişte -25 max can; **sadece Araf içinde** geçerli, kaçınca her zaman silinir |
| Hedef | 3 mühür toplanır; portal 3 mühürle açılır; pity çıkış zarı kalkar |
| Tehdit | **Uyanış + boşluk** (öldürülemez avcı reddedildi: hızlı tempoya uymuyor, çok yeni sistem ister) |
| Kart ödülü | Sunak sayısı + nadirlik derinliğe göre |
| Mekan | İç mekan (kapalı kemik labirentler) + dış mekan (boşlukta adalar) iç içe: labirentte başlanır, labirentin bölümleri arası adalarla geçilir |
| Sıra | Önce mekanik, sonra yapı, sonra atmosfer |

### 9.1 Yapı — Merkez + 3 kanat
- **Merkez labirent:** Başlangıç ve mühürlü portal. Etrafında **3 kanat labirenti** (~7×7, hücre 6 m), kapalı kemik kemerli koridorlar (tavansız, gökyüzü görünür).
- Her labirentin ağzı boşluğa açılır → **ada / köprü / platform parkuru** → sonraki labirent. Adalardan tüm yapı havada görünür (yön bulma).
- Labirentler farklı Y yüksekliklerinde; hepsi Y > 0 (negatif Y kuralı korunur).
- **Boşluğa düşme:** 20 hasar + son güvenli platforma geri atılma; hasar öldürebilir (→ Gerçek Ölüm). *(Ajan kararı, ayarlanabilir.)*
- Kod: `LabyrinthGenerator` her bölüm için yeniden kullanılır; yeni POCO bölümleri ve ada zincirlerini yerleştirir; seed determinizmi korunur.

### 9.2 Hedef — 3 Mühür
- Her kanadın en derin noktasında (kanat girişinden BFS max derinlik) **mühür odası**: dövüş odası + mühür kaidesi + duvarların üstünden görünen kızıl ışık sütunu (minimap yerine yön göstergesi).
- Portal merkezde, 3 mühürle açılır. Diğer yuvalar (Shrine, Ossuary, Combat, Ambush) perde/zar sistemiyle kalır, labirent başına daha az yuva.
- HUD: "MÜHÜR x/3".

### 9.3 Tehdit — Uyanış (kademe 0 → 3)
- Giriş: kademe 0, koridorlarda seyrek devriye.
- Her mühür +1 kademe: o odada pusu dalgası; tüm labirent koridorlarına devriye sürüleri; sis kapanır; adalara Uçan Göz, köprülere Kefaretçi (slam fırlatması + Göz mermileri oyuncuyu boşluğa iter).
- 3. mühürden sonra merkeze dönüş = en uyanık Araf'ta final.
- Yeniden kullanılır: `IEliteScalable`, `ArafRoom` ambush, mevcut düşman prefabları.

### 9.4 Bedel ve derinlik
- **Araf Yarası:** `StatSheet` modifier'ı (-25 max can), girişte eklenir, kaçışta kaldırılır.
- **Derinlik kademesi** (katta temizlenen karşılaşma/dalga oranı):

| Kademe | Koşul | Sunak | Nadirlik |
| :--- | :--- | :--- | :--- |
| 0 | < %34 | 1 | Sadece sıradan |
| 1 | %34–67 | 2 | Sıradan + nadir |
| 2 | ≥ %67 veya boss | 3 | Nadir ağırlıklı |

- Sunaklar mühür odalarında çıkar. `CardOfferRoller.MaxOffersPerVisit = 2` sabiti kademeye bağlı değere döner.
- Not: Yara Araf içinde kaldığı için açığı asıl kapatan derinlik ölçeklemesidir. Hâlâ erken ölmek cazipse kademe 0 sunak sayısı 0'a çekilir.

### 9.5 Atmosfer
- Palet kızıl → mor/menekşe: sis, ambient, `PostFx_Araf` profili. Kızıl yalnızca tehlike vurgusu (lav çatlakları, mühür ışınları, portal).
- Gökyüzü: siyah güneş + nebula; etrafta süzülen kemer/sütun/monolit parçaları.
- Ses: Araf ambiyansı, fısıltılar, uyanış kademe sting'i (klip yoksa placeholder).

### 9.6 Fazlar
1. **Mekanik** (mevcut tek labirent üzerinde, placeholder görsel): yara, derinlik kademesi, 3 mühür, pity kaldırma, uyanış + koridor devriyeleri, sunak ölçekleme, HUD. POCO testleri: kademe hesabı, uyanış durumu, mühür takibi, teklif zarı.
2. **Yapı:** merkez + 3 kanat, ada zincirleri, düşme/geri atılma, ışık sütunları. Determinizm + bağlantılılık testleri.
3. **Atmosfer:** mor palet, gökyüzü, süzülen parçalar, post-fx, ses.

Ayarlanabilir ajan kararları: merkez + 3 kanat düzeni, düşme hasarı 20 ve öldürebilmesi, 7×7 boyut, %34 / %67 kademe sınırları.

Referans görseller: boşlukta yüzen labirent adaları ve siyah güneş; mor gökyüzü altında kemik kemerli koridor ve kızıl çatlaklı zemin; uçurum üstünde gargoyle bloklu platform parkuru.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[CoreLoopAndPurgatory]] ➔ `implements_purgatory`
- [[RunRandomness]] ➔ `seeded_by`
- [[EnemyAIAndSpawning]] ➔ `spawns_elites`
- ⬅️ [[ProjectOverview]] ➔ `features_mechanic` *(gelen)*
