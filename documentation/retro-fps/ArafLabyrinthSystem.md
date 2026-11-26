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
> Seed'li ızgara koridorlar, merkez hub + 3 kanat, yüzen ada platform zincirleri, boşluk düşme mekaniği, perdeli 2x2 hücrelik oda yuvaları, dinamik zarla belirlenen oda içerikleri, 3 mühür odası, merkez kaçış portalı ve elit muhafız dövüşleri.

---

## 1. GENEL BAKIŞ VE MEKANİK DİNAMİKLERİ
- **Çok Bölümlü Prosedürel Labirent (Hub + 3 Kanat):** Araf tek bir 9×9 labirent yerine; merkez Hub (7×7, 2 oda, başlangıç = mühürlü portalın yer aldığı merkez hücre, K/D/B [N/E/W] çıkış ağızları) ve 3 Kanat labirentinden (7×7, 3 oda, başlangıç = hub'a bakan ağız hücresi, dikey Y ofsetleri: +2.4 m, -3.2 m, +4.0 m) oluşur. Yapı `ArafWorldGenerator` (Faz 2a, saf C#) tarafından üretilir ve `ArafLabyrinthDirector` tarafından bölüm bazında (`Section_i` kökleri altında) sahneye inşa edilir; oda yuvaları global olarak numaralandırılır (önce hub, ardından kanatlar). Kapalı kemik koridorların tavanları korunur.
- **Yüzen Ada Zincirleri (Island Chains):** Merkez hub çıkış ağızları ile kanat giriş ağızları arasında 4–6 yüzen platformluk (`Kit_IslandPlatform`, 5 m genişlik, 1.2 m kalınlık; platform kenar boşlukları 2–3.5 m, adım başı dikey yükselme farkı ≤ 0.8 m) ada parkurları yer alır.
- **Kara Perde (Veil) ve Dinamik Zar:** Oda içerikleri, oyuncu yuvanın kapısındaki kara perdeyi (veil) geçene kadar bilinmez; geçince oda tipi ağırlıklı zarla belirlenir ve ilgili prefab dinamik olarak doğar.
- **Kaçış Koşulu ve Merkez Portalı:** Merkez hub'ın başlangıç hücresinde mühürlü bir merkez portalı (`Araf_HubPortal.prefab`) bulunur; oyuncunun giriş noktası portaldan 2.2 m uzaklıktadır. Kanatlardaki 3 mühür toplanana kadar portal kilitlidir; 3 mühür de alındığında portal açılır ve kuleye dönüş sağlanır.
- **3 Mühür Odası:** Hub içerisinde mühür bulunmaz; 3 kanadın her birinde, kendi giriş ağzından en derin yuvada (BFS azami derinlik) yer alan 1 adet mühür odası (`ArafRoomKind.Seal`, `Room_Seal.prefab`, `ArafRoom_Seal.asset`, toplam 3 mühür) bulunur. Her mühür odasında tek bir elit dövüşü (2 Elit Ghoul) yer alır; oda temizlendiğinde mühür otomatik olarak alınır, `AltarAnchor` noktasında bir kart sunağı doğar ve Uyanış seviyesi +1 artar.
- **Ses İşaretçileri (Audio Beacon):** Henüz toplanmamış her mühür yuvasında 3D döngüsel ses işaretçisi (`ArafSealBeacon`, `Araf_SealBeacon.prefab`, yer tutucu klip `Sfx_Araf_SealBeacon.wav`: builder'ın `ArafToneSynth` ile ürettiği 4 sn döngüsel drone + çan) bulunur; mühür alındığında söner.
- **Boşluğa Düşme (Void Fall) ve Güvenli Zemin:** Labirentin en alt kotunun 12 m altındaki `ArafVoidFall` tetikleyicisi boşluğa düşen oyuncuya 20 hasar verir (`ArafFallRules` ile en az 1 HP kalacak şekilde önceden ölçeklenir, asla öldürmez) ve ardından son güvenli zemine (`ArafSafeGround`: boşluk üstünde ve ≥ 0.3 sn zeminle temas edilen son nokta) ışınlar. Boşluğa düşen düşmanlar doğrudan ölür.
- **Araf Yarası (Araf Wound):** Araf'a girişte oyuncuya -25 flat azami can cezası (`ArafWound`, `StatSheet` kaynak ID 1000) uygulanır (canlanmadan önce verilir). Araf'tan kaçışta (kulede canlanmadan önce) yara tamamen kaldırılır.
- **Uyanış (Awakening) ve Tehdit Dağılımı:** Kademe sayacı (`ArafAwakening`, 0..3); `ArafThreatDirector` koridor devriyelerini tüm bölümlere (hub ve kanatlar) dengeli yayar. Mühür alındığında (+1 Uyanış) yalnızca ilgili kanadın kendi ağzına uzanan dönüş yoluna uyanış dalgası indirir.
- **Atmosfer, Sis ve Gökyüzü (Faz 3):**
  - **Menekşe Palet:** Kemik materyali soğuk menekşe (0.42, 0.38, 0.50), zemin zifiri menekşe (0.07, 0.04, 0.11), perde koyu menekşe (0.02, 0, 0.04, 0.92) ve meşaleler soluk menekşe (0.62, 0.48, 1.0) ışıkla güncellendi. Kızıl renk yalnızca tehlike vurgularında (merkez portalı, mühür ışık sütunları, kapı bariyerleri) korunur.
  - **Uyanış Kademeli Sis:** Eski 6–38 m kızıl sis adalardan kanatları ve sütunları gizlediği için kaldırıldı; yerine lineer menekşe sis (0.10, 0.05, 0.16) getirildi. Görüş mesafesi Uyanış seviyesine göre kademeli daralır (Başlangıç: {20, 16, 12, 8} m, Bitiş: {140, 115, 90, 70} m). Değişimler `ArafAtmosphereDirector` tarafından yalnızca Araf içindeyken 3 saniyede pürüzsüz lerp edilir ve uyanış sting sesi tetiklenir (klip: `ArafToneSynth` yer tutucusu).
  - **Panoramik Gökyüzü ve Siyah Güneş:** Skybox sis filtrelerine tabi olmadığından (3D nesneler sisle örtüleceği için), gökyüzü `Skybox/Panoramic` üzerinde prosedürel `Tex_Araf_Sky.png` equirectangular dokusuyla (menekşe gradyan, ufukta Perlin nebula, yıldızlar, azimut 200 / yükseklik 25 açısında menekşe koronalı siyah güneş) sunulur. Güneş yönünden gelen `VioletRim` yönlü ışığı sahneyi aydınlatır.
  - **Süzülen Molozlar (Floating Debris):** Dünya sınırlarının 25–85 m çevresindeki kabukta 40 adet collidersız moloz (`Debris_Arch`, `Debris_Pillar`, `Debris_Monolith`) yavaş salınım ve dönüşle (bob/spin) süzülür (`ArafFloatingDebris` + `ArafDebrisPlanner`, seed: `RunStream.Cosmetic`).
  - **Post-FX:** `PostFx_Araf` profili `PostFxBuilder` üzerinden menekşe tonlarında derecelendirilmiştir (`RetroFPS/Build Post FX`).

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
| **Seal** | Sabit 3 Yuva (`ArafSealSlotPicker`) | 2 Elit Ghoul + Mühür Alma + Kart Sunağı. Temizlenince mühür toplanır, sunak doğar, Uyanış +1. Kapılar temizlenene kadar kapatılır. |

- **Sunaklar ve Kart Teklifleri:** Standart Combat ve Ambush odaları artık kart sunağı doğurmaz. Kart sunakları yalnızca mühür odalarında (ziyaret başına en fazla 3 teklif, `CardOfferRoller.ConfigureVisit`) çıkar.
- **Kart Nadirlik Dağılımı:** Sunak nadirlikleri mühür sırasına göre `ArafTuning` üzerinden belirlenir (1. mühür: sadece sıradan; 2. mühür: sıradan + nadir; 3. mühür: nadir ağırlıklı + yadigar). Geçici olarak tek kat olduğu için mühür sırasına göredir; ileride kat indeksine bağlanacaktır.
- **Elit Düşman Katsayıları:** Hasar ×1.4, Hız ×1.4, Can ×1.25.
- **Kapı Bariyerleri:** Savaş ve mühür odaları temizlenene kadar kapılarını (`Kit_DoorBarrier`) kapatır.

---

## 3. MİMARİ VE BİLEŞENLER
Namespace'ler: `RetroFPS.Run.Labyrinth` (`Scripts/Run/Labyrinth/`), `RetroFPS.Run.Araf` (`Scripts/Run/Araf/`), `RetroFPS.Run.Cards` (`Scripts/Run/Cards/`).

| Sınıf / Varlık | Türü / Konumu | Sorumluluğu |
| :--- | :--- | :--- |
| `LabyrinthTypes.cs` | POCO / Structs | `LabyrinthDir`, `GridPos`, `Doorway`, `LabyrinthSettings` (9×9, hücre 6 m, 7 oda, braid 0.15, oda arası boşluk 1, oda başına en çok 2 kapı). |
| `LabyrinthLayout` & `LabyrinthGenerator` | Saf C# | Saf C# labirent üretimi: Oda bloklarını yerleştir → koridorlarda iteratif recursive-backtracker → oda kapıları → braid ile çıkmaz sokakların bir kısmını aç → BFS derinlik. |
| `ArafWorldGenerator`, `ArafWorldLayout` & `ArafWorldSettings` | Saf C# POCO (`Scripts/Run/Labyrinth/`) | Saf C# çok bölümlü dünya üretimi (Faz 2a): 7×7 merkez hub (merkezde portal ve başlangıç, K/D/B ağızlar), dikey Y ofsetli (+2.4 m, -3.2 m, +4.0 m) 3 adet 7×7 kanat labirenti ve aralarındaki 4–6 platformluk ada zincirlerini (`ArafIslandChain`, `ArafPlatform`) deterministik olarak üretir. |
| `ArafSealSlotPicker` | Saf C# | En derin yuvadan başlayarak farthest-point algoritmasıyla birbirinden en uzak mühür yuvalarını seçer (`RunStream.ArafLayout` seed'i ile). Çok bölümlü yapıda her kanat için ayrı çağrılır. |
| `ArafRoomDefinition` & `ArafRoomTable` | ScriptableObject | Oda tanımları (ağırlık, minDepth, maxPerRun, prefab) ve varsayılan oda tablosu (`ArafRoomKind.Seal` tanımı içerir). |
| `ArafRoomRoller` | Saf C# | Saf C#, kalan yuvalar için ağırlıklı oda zarlama mantığı (mühür yuvaları önceden ayrılır). |
| `ArafRoom` & `ArafRoomTriggerRelay` | MonoBehaviour | Temel oda mantığı: Elit uygulama, temizlenme, shrine iyileştirme, ambush tetikleyicisi. |
| `ArafSealRoom` | MonoBehaviour | Mühür odası mantığı: 2 elit ghoul dövüşü, temizlenince mühür alma, `AltarAnchor` noktasında kart sunağı doğurma, Uyanış seviyesini artırma (+1). |
| `ArafSealBeacon` | MonoBehaviour | Toplanmamış mühür yuvalarında 3D döngüsel ses işaretçisi (`Araf_SealBeacon.prefab`); mühür toplanınca ses solarak söner. |
| `ArafSealLightPillar` | MonoBehaviour (`Scripts/Run/Labyrinth/`) | Toplanmamış mühür odalarının çatısından yükselen, dışarıdaki adalardan görülebilen collidersız kızıl ışık sütunu (`Araf_SealLightPillar.prefab`, `Mat_Araf_SealBeam`). Labirent koridor tavanları kapalı olduğu için yalnızca adalardan görünür; mühür alındığında solarak söner (`Extinguish()`) ve kesintiye uğrasa dahi sönük kalır. |
| `ArafTuning` | ScriptableObject | Araf dengeleme ayarları (`ArafTuning_Default.asset`): yara azami can cezası (-25), mühür sayısı (3), azami uyanış (3), mühür sırasına göre kart nadirlik çarpanları (`CardRarityMultipliers`), ada tehdit değerleri (`islandEyesAddedAtLevel {0,2,2,3}`, `islandPenitentsAddedAtLevel {0,0,1,1}`, `MaxAliveIslandEnemies = 10`, `EyeHoverHeight = 3.0 m`, `ConfigureIslandThreat`), atmosfer ve sis ayarları (`GetFogStart`, `GetFogEnd`, `fogStartByLevel {20, 16, 12, 8}`, `fogEndByLevel {140, 115, 90, 70}`, `FogColor (0.10, 0.05, 0.16)`, `DebrisCount = 40`, `DebrisMargin = 25`, `DebrisShellThickness = 60`, `ConfigureAtmosphere`). |
| `ArafAtmosphereDirector` | MonoBehaviour (`Scripts/Run/Araf/`) | Labirent direktörü nesnesinde yer alır. `RunEvents.OnEnteredAraf` ile uyanış seviyesine göre lineer menekşe sisi (0.10, 0.05, 0.16) anında uygular; `OnArafAwakeningChanged` ile sis mesafesini (start/end) `ArafTuning` değerlerine göre 3 sn boyunca pürüzsüz lerp eder ve `stingSource` üzerinden uyanış sting'i çalar (klip: `ArafToneSynth` yer tutucusu). Yalnızca Araf aktifken çalışır. |
| `ArafFloatingDebris` | MonoBehaviour (`Scripts/Run/Araf/`) | Labirent direktörü nesnesinde yer alır. `NavMeshReady` anında `ArafDebrisPlanner` ve `RunStream.Cosmetic` akışıyla dünya sınırlarının 25–85 m çevresindeki kabuğa 40 collidersız moloz parçası (`Debris_Arch`, `Debris_Pillar`, `Debris_Monolith`) saçar; `Update` içinde sıfır GC ile yavaş Y ekseni salınımı ve dönüş (bob/spin) uygular. |
| `ArafDebrisPlanner` | Saf C# Statik POCO (`Scripts/Run/Araf/`) | Moloz yerleşim algoritması (`Plan`): Belirtilen `worldBoundsLocal` etrafında [25, 85] m dış kabukta, Y ekseninde [bounds.min.y - 30, bounds.max.y + 40] m aralığında, parçalar arası ≥ 8 m mesafe ve 0.6–2.2 ölçekle deterministik pozisyon/rotasyon hesaplar (sıfır heap tahsisi). |
| `ArafToneSynth` | Saf C# Statik (`Scripts/Run/Araf/`) | Deterministik yer tutucu ses sentezi: `FillBeacon` (4 sn kusursuz döngü, 55/82.5 Hz drone + 2 sn'de bir 660/990 Hz çan), `FillSting` (2.5 sn, 48→28 Hz alçalan boom + süzülmüş gürültü + 220/233 Hz uyumsuz kabarma), `EncodeWav16` (mono PCM16 WAV). Builder bunları `Art/Audio/Araf/` altına yazar; testleri `ArafToneSynthTests`. |
| `ArafSealTracker` | Saf C# POCO | Mühür toplama ilerlemesi (`Total`, `Collected`, `IsComplete`, `TryCollect`). |
| `ArafAwakening` | Saf C# POCO | Uyanış kademesi sayacı (`Level`, `MaxLevel`, `Raise`). |
| `ArafWound` | Statik Sınıf | Araf'a girişte oyuncuya `StatSheet` üzerinden -25 flat azami can cezası uygular (kaynak ID: 1000), kaçışta kaldırır. |
| `ArafFallRules` | Saf C# Statik POCO (`Scripts/Run/Araf/`) | Boşluğa düşme hasar kuralı (`ComputeFallDamage`): `Mathf.Max(0, Mathf.Min(fallDamage, currentHealth - 1))` formülüyle 20 hasarı sınırlar; oyuncuyu asla öldürmez, daima en az 1 HP bırakır. |
| `ArafSafeGround` | MonoBehaviour (`Scripts/Run/Araf/`) | Labirent direktörü nesnesinde yer alır. Oyuncu Araf fazındayken ≥ 0.3 sn kesintisiz zemin üzerinde ve boşluk seviyesinin üzerinde kaldığında konumu ve rotasyonu (yalnızca yaw) güvenli nokta olarak kaydeder (`LastSafePosition`). |
| `ArafVoidFall` & `ArafVoidFallTrigger` | MonoBehaviour (`Scripts/Run/Araf/`) | Labirentin en alçak noktasının 12 m altına (`VoidHeightLocal`) geniş tetikleyici yerleştirir. Oyuncu düşünce `ArafFallRules` ile 20 hasar (min 1 HP) verip `ArafSafeGround` noktasına ışınlar (0.5 sn yeniden tetikleme koruması). Boşluğa düşen düşmanları anında yok eder (`TakeDamage` 100000). |
| `LabyrinthKit` | ScriptableObject | Palet ScriptableObject'i: Duvar, zemin, tavan, sütun, perde (veil), kapı bariyeri (`Kit_DoorBarrier`), meşale ve ada platformu (`Kit_IslandPlatform`) prefabları. |
| `ArafRoomSlot` & `ArafRoomSlotTrigger` | MonoBehaviour | 2×2 oda yuvaları ve kapıdaki perde tetikleyicisi. |
| `ArafLabyrinthDirector` | MonoBehaviour | `ArafLevel` köküne bağlıdır. `ArafWorldSettings` üzerinden `ArafWorldGenerator` ile dünyayı üretir; her bölüm için `Section_i` kökü altında geometriyi (zemin, tavan, duvarlar, sütunlar, meşaleler, slotlar/perdeler) ve aralardaki ada platformlarını (`Kit_IslandPlatform`) inşa eder. Odaları global olarak numaralandırır (önce hub, sonra kanatlar); her kanadın kendi ağzından en derin yuvasına 1 mühür yerleştirir (toplam 3 mühür). Her mühür yuvasının tavanına `ArafSealLightPillar` (`Araf_SealLightPillar.prefab`) yerleştirir; mühür alınınca `Extinguish()` çağırır. Başlangıç hücresine `Araf_HubPortal` ve 2.2 m ötesine giriş noktasını (`EntryPositionWorld`) koyar; `ArafVoidFall` ve `ArafSafeGround` bileşenlerini barındırır. `RuntimeNavMeshBaker` üzerinden tüm `_generated` yapısını (adalar dahil) tek seferde fırınlar (`NavMeshReady` olayı). |
| `ArafThreatDirector` | MonoBehaviour (`Scripts/Run/Araf/`) | Labirent direktörü nesnesinde yer alır. `NavMeshReady` anında seviye-0 devriyelerini ve her mühürde yeni devriyeleri (`patrolsAddedAtLevel {2,2,3,3}`, azami `MaxAlivePatrols = 12`) tüm bölümlere (hub ve kanatlar) round-robin olarak dağıtarak doğurur. Alınan her mühürde (`SealTaken(level, globalSlot)`) yuvanın ait olduğu kanadı tespit eder ve yalnızca o kanadın kendi çıkış ağzına giden dönüş yoluna uyanış dalgası (`waveSizeByLevel {0,3,4,5}`) indirir; kapılar kilitlenmez. Dalga Ghoulları ölene kadar 2 sn'de bir `ReceiveAlert` ile uyarılır. Faz 2c ile her mühürde adalara `ArafIslandThreatPlanner` üzerinden platform üstü 3 m süzülen Uçan Gözler (`Enemy_FlyingEye_Watcher`) ve platform zemininde Kefaretçiler (`Enemy_Penitent_Kefaretci`) doğurur (azami 10 canlı ada düşmanı sınırı; saldırıları oyuncuyu boşluğa itebilir). |
| `ArafThreatPlanner` | Saf C# Statik POCO (`Scripts/Run/Araf/`) | Koridor devriye ve dalga hücrelerini seçer (`PickPatrolCells`, `BuildReturnPath`, `PickWaveCells`). Mühür odasından başlangıç hücresine uzanan koridor yolunun %30 ile %85'i arasındaki koridor hücrelerini seçer; oyuncuya 14 m'den (`MinSpawnDistance`) yakın, oda içi veya başlangıç hücresindeki hücrelere doğurmaz. |
| `ArafIslandThreatPlanner` | Saf C# Statik POCO (`Scripts/Run/Araf/`) | Ada platformu tehdit seçim mantığı (`PickPlatforms`): Zincir başına ≥ 3 platform varsa ilk ve son platformu hariç tutar, oyuncuya ≥ 14 m mesafedeki adayları zincirler arasında round-robin ve deterministik olarak seçer. |

---

## 4. SEED AKIŞLARI
- `RunStream.Cosmetic` (4): Kozmetik rastgelelik (süzülen moloz parçalarının yerleşimi ve rotasyonları).
- `RunStream.ArafLayout` (5): Labirent yerleşimi (koridorlar, duvarlar, oda yuvaları ve `ArafSealSlotPicker` mühür yuvası seçimleri).
- `RunStream.ArafRooms` (6): Oda zarları.
- `RunStream.ArafThreat` (8): Araf tehdit doğuşları (koridor devriyeleri ve dönüş yolu dalgaları).
- **Determinizm:** Aynı seed → aynı labirent; oda sonuçları aynı seed + aynı keşif sırası ile birebir tekrar üretilir.

---

## 5. HUD VE BİLDİRİMLER
- `RunEvents.OnArafSealsChanged(collected, total)` ve `RunEvents.OnArafAwakeningChanged(level)` olayları tetiklenir (eski `OnArafRoomsChanged` kaldırıldı).
- `RunHUD` üzerinde "MÜHÜR x/3" ve tüm mühürler toplandığında "PORTAL AÇIK" bilgisi gösterilir.
- Uyarı banner'ı: "ARAF'TASIN — MÜHÜRLERİ TOPLA, PORTALA DÖN".
- Uyanış seviyesi HUD üzerinde sayısal olarak yer almaz.

---

## 6. SAHNE VE PREFAB BUILDER
- `ArafSceneBuilder.BuildAll()`:
  - Kit ve oda prefablarını (`Room_Seal.prefab`, `Araf_HubPortal.prefab`, `Araf_SealBeacon.prefab`, `Kit_IslandPlatform.prefab` [5 m en, 1.2 m kalınlık, küp slab, kemik materyali, BoxCollider, `GameLayers.GroundMask`] dahil), tanımları (`ArafRoom_Seal.asset`), `ArafRoomTable_Default`, `ArafTuning_Default.asset`, `LabyrinthKit_Araf` (ada platform referansı atanmış) ve Araf sahnesini (kök Y = +1000, `ArafLevel` üzerinde `ArafTuning`, `ArafVoidFall`, `ArafSafeGround` referansları) üretir.
  - **Faz 2c Builder Çıktıları:**
    - `Araf_SealLightPillar.prefab`: Kök `ArafSealLightPillar`, alt nesne "Beam" collidersız küp (0.9×60×0.9, yerel Y 30, `Mat_Araf_SealBeam` URP unlit/lit güçlü kızıl emisyonlu materyal, gölgesiz), alt nesne "Glow" kızıl nokta ışık (menzil 14, şiddet 3, gölgesiz); `ArafLabyrinthDirector.sealLightPillarPrefab` alanına atanır.
    - `ArafThreatDirector`: `islandEyePrefab` (`Enemy_FlyingEye_Watcher.prefab`) ve `islandPenitentPrefab` (`Enemy_Penitent_Kefaretci.prefab`) alanları bağlanır.
    - `ArafTuning_Default.asset`: `ConfigureIslandThreat(eyesAdded: {0,2,2,3}, penitentsAdded: {0,0,1,1}, maxAlive: 10, eyeHoverHeight: 3.0f)` çağrısı ile ada tehdit değerleri atanır.
  - **Faz 3 Builder Çıktıları (`ArafSceneBuilder` & `PostFxBuilder`):**
    - **Palet ve Materyaller:** `Mat_Araf_Bone` (0.42, 0.38, 0.50), `Mat_Araf_Floor` (0.07, 0.04, 0.11), `Mat_Araf_Veil` (0.02, 0, 0.04, 0.92); meşale ışığı soluk menekşe (0.62, 0.48, 1.0). Portal, mühür ışık sütunları ve kapı bariyerleri kızıl kalır.
    - **Sis ve Çevre (Atmosphere):** `ApplyAtmosphere()` ile lineer menekşe sis (0.10, 0.05, 0.16; başlangıç 20 m, bitiş 140 m), düz ambient (0.09, 0.06, 0.14).
    - **Gökyüzü:** Prosedürel equirectangular doku `Tex_Araf_Sky.png` (menekşe gradyan, ufukta Perlin nebula, yıldızlar, azimut 200 / yükseklik 25 açısında menekşe koronalı siyah güneş) `Mat_Araf_Sky` materyali (`Skybox/Panoramic`) ile sahnelenir (skybox sislenmediği için 3D güneş yerine panoramik skybox seçilmiştir). Güneş yönünden gelen gölgesiz `VioletRim` Directional Light (renk: 0.55, 0.42, 0.9, şiddet: 0.35).
    - **Moloz Prefabları:** `Prefabs/Araf/Kit/Debris_Arch.prefab` (iki 1×6×1 dikme + 6×1×1 lentolu kemer), `Debris_Pillar.prefab` (1.2×10×1.2 sütun), `Debris_Monolith.prefab` (3×8×1 monolit); kemik materyali, collidersız.
    - **Direktör Bileşenleri ve Tuning:** Araf direktör nesnesine `ArafAtmosphereDirector` (labyrinth ve 2D AudioSource `stingSource` bağlı; `awakeningSting` = builder'ın ürettiği yer tutucu `Sfx_Araf_AwakeningSting.wav`) ve `ArafFloatingDebris` (labyrinth ve 3 moloz prefabı bağlı) atanır. `ArafTuning_Default.asset` üzerine `ConfigureAtmosphere(new[]{20f,16f,12f,8f}, new[]{140f,115f,90f,70f}, new Color(0.10f,0.05f,0.16f), 40, 25f, 60f)` ile parametreler yazılır.
    - **Post-FX:** `PostFxBuilder.AuthorAraf` ile menekşe tonlarında profil oluşturulur (`colorFilter`: 0.86, 0.82, 1.0; split shadows: 0.42, 0.36, 0.62; highlights: 0.62, 0.55, 0.70; vignette: 0.10, 0, 0.16; bloom tint: 0.85, 0.72, 1.0; `RetroFPS/Build Post FX` menüsüyle yeniden üretilir).
  - Sütun prefabı düzeltildi (duvar yüksekliğinin 5 katına çıkma hatası giderilerek duvar boyuna eşitlendi).
  - Eski `ArafTrial` sınıfı ve pity/çıkış yapıları emekliye ayrıldı (kaldırıldı).

---

## 7. DOĞRULAMA VE TESTLER (2026-11-27)
- **EditMode Testleri:** 61 testin tamamı geçti:
  - 47 labirent üreteci testi: Determinizm, bağlantılılık, duvar simetrisi, oda boşlukları, kapı geometrisi.
  - 7 oda zarlayıcı testi: Çıkış tam 1 kez, son yuva garantisi, `minDepth`, `maxPerRun`, şans artışı.
  - 13 RNG / RunState testi.
- **Faz 1a & Faz 1b (2026-11-26):** Mühür sistemi, uyanış sayacı, koridor devriyeleri ve dönüş dalgaları doğrulandı.
- **Faz 2a & Faz 2b (2026-11-27 — Tamamlandı ve Doğrulandı):**
  - **EditMode:** 11 fixture eksiksiz geçti (`ArafFallRulesTests` vb.).
  - **Play Mode:** 4 farklı prosedürel yerleşim test edildi; yüzen ada zincirleri gerçek oyuncu hareket kontrolleriyle başarıyla aşıldı.
  - **Boşluk Düşüşü Doğrulaması:** Düşme hasarı ve teleport testi başarıyla tamamlandı (75 HP → 55 HP ve 10 HP → 1 HP; can asla 1'in altına inmedi ve oyuncu son güvenli zemine ışınlandı). Düşmanlar boşluğa düşünce anında öldü.
  - **Döngü ve Kaçış:** 3 kanattaki 3 mühür toplandı, merkez portal açıldı ve kuleye kaçış başarıyla gerçekleşti.
  - **Sıfır GC:** Çalışma zamanında kare başına 0 B çöp tahsisi (0 B/frame) korundu.
  - **Kanıt Dosyaları:** `production/qa/evidence/araf-p2b-*.png`.
- **Faz 2c — Mühür Işık Sütunları ve Ada Tehditleri (2026-11-27 — Kodlandı, İncelendi, Unity'de Doğrulandı: `araf-p3-0*`):**
  - **Mühür Işık Sütunları:** `ArafSealLightPillar` bileşeni ve `Araf_SealLightPillar.prefab` (`Mat_Araf_SealBeam` kızıl ışıma, collidersız, tavan üstü) hazırlandı. Labirent koridorları tavanlı kaldığı için yalnızca adalardan görülebilir; mühür alındığında solarak söner (`Extinguish()`) ve kesintiye uğrasa dahi sönük kalır.
  - **Ada Tehditleri:** `ArafIslandThreatPlanner` (saf C# statik, ada zincirlerinde round-robin, ilk/son platform hariç, oyuncuya ≥ 14 m mesafe) ve `ArafThreatDirector` ada doğurma entegrasyonu tamamlandı. Uyanış kademesine göre ada platformlarına Uçan Gözler ({0,2,2,3}, 3 m havada süzülen) ve Kefaretçiler ({0,0,1,1}, platform üstünde) doğurulur (toplam canlı ada düşmanı sınırı: 10). Düşmanların mevcut saldırıları (Eye atışları, Penitent slam itmesi) oyuncuyu ölümcül olmayan boşluğa düşürebilir.
  - **EditMode Testleri:** `ArafIslandThreatPlannerTests` ve genişletilen `ArafTuningTests` yazıldı. Unity'de geçti (2026-11-27).
- **Faz 3 — Atmosfer (Menekşe Palet, Gökyüzü, Süzülen Molozlar, Sis ve Sting) (2026-11-27 — Kodlandı, İncelendi, Unity'de Doğrulandı: `araf-p3-0*`):**
  - **Menekşe Palet:** Soğuk menekşe kemik (`Mat_Araf_Bone` 0.42/0.38/0.50), zifiri menekşe zemin (`Mat_Araf_Floor` 0.07/0.04/0.11), koyu menekşe perde (`Mat_Araf_Veil` 0.02/0/0.04/0.92) ve soluk menekşe meşaleler (0.62, 0.48, 1.0); tehlike vurguları (portal, mühür ışınları, kapı bariyerleri) kızıl bırakıldı.
  - **Uyanış Kademeli Sis:** Eski 6–38 m kızıl sis adalardan kanatları ve mühür sütunlarını gizlediği için kaldırıldı; yerine uyanış kademesine göre daralan lineer menekşe sis (0.10, 0.05, 0.16; başlangıç {20,16,12,8} m, bitiş {140,115,90,70} m) getirildi. `ArafAtmosphereDirector` sis mesafesini 3 saniyede pürüzsüz lerp eder ve uyanış sting'i çalar (klip `ArafToneSynth` yer tutucusu; yalnızca Araf aktifken çalışır).
  - **Panoramik Gökyüzü ve Siyah Güneş:** Skybox sisten etkilenmediği için (3D güneş nesnesi sisten gizleneceğinden) prosedürel `Tex_Araf_Sky.png` equirectangular dokusu (menekşe gradyan, Perlin nebula, yıldızlar, azimut 200 / yükseklik 25 menekşe koronalı siyah güneş) `Skybox/Panoramic` üzerinde fırınlandı; güneş yönünden gölgesiz `VioletRim` yönlü ışığı eklendi.
  - **Süzülen Molozlar:** `ArafFloatingDebris` ve `ArafDebrisPlanner` ile dünya sınırlarının 25–85 m kabuğunda 40 collidersız moloz (`Debris_Arch`, `Debris_Pillar`, `Debris_Monolith`), sıfır GC Y salınımı ve yavaş dönüş (`RunStream.Cosmetic`).
  - **Post-FX:** `PostFx_Araf` profili menekşe tonlarında yeniden derecelendirildi (`PostFxBuilder`).
  - **EditMode Testleri:** `ArafDebrisPlannerTests` ve genişletilmiş `ArafTuningTests` yazıldı. Unity'de geçti (2026-11-27).

---

## 8. BİLİNEN EKSİKLER VE SONRAKİ ADIMLAR
- Yön bulma dış mekanda (adalarda) ışık sütunlarıyla (`ArafSealLightPillar`), iç mekanda ise mühür ses işaretleriyle (`ArafSealBeacon`) sağlanır; ses klibi `ArafToneSynth` ile üretilmiş yer tutucudur (gerçek ses tasarımı ileride).
- `RunHUD` Araf yazısı koyu kızıl — kontrast düşük.
- Oda prefabları basit küp sahne formatındadır.
- Kart sunakları yalnızca mühür odalarında çıkar; standart Combat ve Ambush odalarında sunak doğmaz. Ayrıntılar için bkz. [[CurseBlessingCards]].
- Uyanış görsel/sis tepkisi, menekşe palet, gökyüzü ve molozlar Faz 3 ile kodlandı (gerçek ses klipleri ve zemin lav çatlakları hariç; beacon ve sting için `ArafToneSynth` ile kodla üretilen yer tutucu WAV'lar `Art/Audio/Araf/` altına yazılır; Unity'de doğrulandı).

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

### 9.1 Yapı — Merkez + 3 Kanat (Faz 2a & Faz 2b Tamamlandı)
- **Merkez labirent:** 7×7 grid (2 oda yuvası), başlangıç noktası ve mühürlü portal merkez hücrede yer alır; Kuzey, Doğu ve Batı (K/D/B) yönlerinde boşluğa açılan çıkış ağızları (mouths) bulunur.
- **3 Kanat labirenti:** Merkez etrafında 3 adet 7×7 labirent (kanat başına 3 oda yuvası). Başlangıç hücreleri hub'a bakan ağız hücreleridir. Dikey Y ofsetleri: +2.4 m, -3.2 m, +4.0 m (negatif Y kuralı ve bağımsız yükseklikler korunur).
- **Tavan Yapısı:** Kapalı kemik labirent koridorlarının tavanları korunur.
- **Ada Zincirleri:** Hub ağızları ile kanat ağızları arasında 4–6 yüzen platformluk (`Kit_IslandPlatform`, 5 m genişlik, 1.2 m kalınlık; 2–3.5 m açıklık, adım başı dikey yükselme farkı ≤ 0.8 m) parkurlar yer alır.
- **Boşluğa Düşme:** En alçak noktanın 12 m altına yerleştirilen `ArafVoidFall` tetikleyicisi düşen oyuncuya 20 hasar verir (`ArafFallRules` gereği en az 1 HP kalır, asla öldürmez) ve `ArafSafeGround` tarafından kaydedilen son güvenli platforma ışınlar. Düşen düşmanlar anında ölür.
- **Kod Durumu:** `ArafWorldGenerator` (Faz 2a, saf C#) dünyayı üretir; `ArafLabyrinthDirector` bölüm bazında (`Section_i` hiyerarşisinde) inşa eder. Determinizm ve bağlantılılık korunur.
- **Faz 2c Durumu:** Mühür odaları tavanından yükselen ışık sütunları ve ada tehditleri (Uçan Göz / Kefaretçi) kodlandı ve gözden geçirildi (Unity'de doğrulandı, 2026-11-27).

### 9.2 Hedef — 3 Mühür
- Her kanadın en derin noktasında (kanat girişinden BFS max derinlik) **mühür odası**: dövüş odası + mühür kaidesi. Labirent koridorları tavanını korur; labirent içi mühür yönlendirmesi 3D ses işaretçisi (`ArafSealBeacon`) ile sağlanır.
- **Mühür Işık Sütunları (Faz 2c'de kodlandı):** Henüz toplanmamış her mühür odası çatısından göğe yükselen uzun, collidersız kızıl ışık sütunu (`ArafSealLightPillar`, `Araf_SealLightPillar.prefab`, `Mat_Araf_SealBeam`). Labirent koridorları tavanlarını koruduğu için labirentin içinden görünmez; yalnızca dış mekandaki yüzen ada parkurlarından açık rehber olarak görünür. Mühür alındığında solarak söner (`Extinguish()`) ve kesintiye uğrasa dahi sönük kalır.
- Portal merkezde, 3 mühürle açılır. Diğer yuvalar (Shrine, Ossuary, Combat, Ambush) perde/zar sistemiyle kalır, labirent başına daha az yuva.
- HUD: "MÜHÜR x/3".

### 9.3 Tehdit — Uyanış (kademe 0 → 3)
- Giriş: kademe 0, koridorlarda seyrek devriye (Faz 1b'de kodlandı: `ArafThreatDirector`, `patrolsAddedAtLevel[0]` = 2 devriye, azami `MaxAlivePatrols` 12).
- Mühür odası tek bir dövüştür; mühür alındığında uyanış dalgası kapıları kilitlemeden dönüş yolu koridorlarına iner (Faz 1b'de kodlandı: kapılar kilitlenmez, `ArafThreatPlanner.BuildReturnPath` / `PickWaveCells` ile mühür odasından başlangıç hücresine giden yolun %30-%85'indeki koridor hücrelerine `waveSizeByLevel {0,3,4,5}` dalgası doğar; oyuncuya ≥14 m mesafe, odalarda ve başlangıç hücresinde doğmaz; Ghoullar 3.5 sn görüşsüzlükte takibi bıraktığı için ölene dek 2 sn'de bir `ReceiveAlert` ile uyarılır).
- Her mühür +1 kademe: tüm labirent koridorlarına devriye sürüleri (Faz 1b'de kodlandı: `patrolsAddedAtLevel {2,2,3,3}`, 12 cap); sis kapanır.
- **Ada Tehditleri (Faz 2c'de kodlandı):** Adalar kademe 0'da boştur. Alınan her mühürde (+1 uyanış) `ArafIslandThreatPlanner` ada platformlarını seçer (zincir başına ≥ 3 platform varsa ilk ve son platform hariç, oyuncuya ≥ 14 m mesafe, zincirler arası deterministik round-robin dağılım). Platformlar üzerine havada 3 m süzülen Uçan Gözler (`islandEyesAddedAtLevel {0,2,2,3}`, `EyeHoverHeight = 3.0 m`) ve platform zemininde Kefaretçiler (`islandPenitentsAddedAtLevel {0,0,1,1}`) doğar. Canlı ada düşmanı sayısı azami 10 (`MaxAliveIslandEnemies`) ile sınırlandırılır (`ArafTuning`). Bu düşmanların mevcut saldırıları (Uçan Göz mermileri ve Kefaretçi slam fırlatması) oyuncuyu platformdan ölümcül olmayan boşluğa düşürebilir (`ArafVoidFall`).
- 3. mühürden sonra merkeze dönüş = en uyanık Araf'ta final.
- Yeniden kullanılır: `IEliteScalable`, `ArafRoom` ambush, mevcut düşman prefabları (`Enemy_FlyingEye_Watcher`, `Enemy_Penitent_Kefaretci`).

### 9.4 Bedel ve derinlik
- **Araf Yarası:** `StatSheet` modifier'ı (-25 max can), girişte eklenir, kaçışta kaldırılır.
- **Sunak ve Nadirlik Modeli:** Eski derinlik kademesi tablosu kaldırıldı (boss öncesi bilerek ölmeyi ödüllendirdiği için iptal edildi). Kararlaştırılan model: Her mühür için 1 sunak (ziyaret başına en çok 3 teklif, `CardOfferRoller.ConfigureVisit`). Sunak nadirliği şimdilik mühür sırasına göre (1. mühür: sadece sıradan; 2. mühür: sıradan + nadir; 3. mühür: nadir ağırlıklı + yadigar), ileride kat indeksine bağlanacaktır.
- Sunaklar mühür odalarında çıkar. `CardOfferRoller.MaxOffersCap = 3` ile sınırlandırılır.
- Not: Yara Araf içinde kaldığı için açığı asıl kapatan sunakların mühür dövüşlerine bağlanması ve nadirlik ölçeklemesidir.

### 9.5 Atmosfer (Faz 3 — Kodlandı ve İncelendi; Gerçek Ses Klipleri ve Lav Çatlakları Hariç Tamamlandı)
- **Palet:** Kızıl → soğuk menekşe kemik (0.42, 0.38, 0.50), zifiri menekşe zemin (0.07, 0.04, 0.11), koyu menekşe perde (0.02, 0, 0.04, 0.92) ve soluk menekşe meşaleler (0.62, 0.48, 1.0). Kızıl yalnızca tehlike vurgusu olarak korunur (portal, mühür ışık sütunları, kapı bariyerleri; lav çatlakları ileride eklenecektir).
- **Sis ve Uyanış Kademesi:** Adalardan kanatları ve sütunları örten eski 6–38 m kızıl sis kaldırıldı; yerine lineer menekşe sis (0.10, 0.05, 0.16; başlangıç {20,16,12,8} m, bitiş {140,115,90,70} m) getirildi. `ArafAtmosphereDirector` Araf içerisindeyken seviye değişiminde 3 saniyelik lerp ile sisi pürüzsüz daraltır ve uyanış sting sesini tetikler (klip: `ArafToneSynth` yer tutucusu).
- **Gökyüzü:** Skybox sis filtrelerine takılmadığı için (3D güneş nesnesi sisle örtüleceğinden), `Tex_Araf_Sky.png` prosedürel equirectangular dokusu (menekşe gradyan, ufukta Perlin nebula, yıldızlar, azimut 200 / yükseklik 25 menekşe koronalı siyah güneş) `Skybox/Panoramic` shader'ı ile sahnelenir; güneş yönünden gelen `VioletRim` Directional Light ile desteklenir.
- **Süzülen Molozlar:** `ArafFloatingDebris` ve `ArafDebrisPlanner` (`RunStream.Cosmetic`) ile dünya sınırlarının 25–85 m dışındaki kabukta 40 adet collidersız parça (`Debris_Arch`, `Debris_Pillar`, `Debris_Monolith`) yavaş salınım ve dönüşle süzülür.
- **Post-FX:** `PostFx_Araf` profili `PostFxBuilder` ile menekşe tonlarında derecelendirilmiştir (`RetroFPS/Build Post FX`).

### 9.6 Fazlar
1. **Faz 1a — Mekanik Temeli (Tamamlandı):** Araf Yarası (-25 max HP), 3 mühür odası ve slot picker, pity/çıkış sisteminin kaldırılması, mühür sırasına göre sunak ve nadirlik çarpanları (`CardOfferRoller.ConfigureVisit` ile azami 3 teklif), `ArafSealBeacon` ses işaretçisi, Uyanış sayacı (`ArafAwakening`), yeni HUD mühür ve portal olayları (`OnArafSealsChanged`, `OnArafAwakeningChanged`).
2. **Faz 1b — Düşman Uyanışı ve Koridor Tehditleri (Tamamlandı):** Koridor devriyeleri (`ArafThreatDirector`, seviye-0 ve mühür başı artış, 12 devriye sınırı), mühür alındığında dönüş koridorlarına inen uyanış dalgaları (`ArafThreatPlanner`, dönüş yolunun %30–%85'i, kapılar kilitlenmez, periyodik Ghoul alarmı), Araf çalışma zamanı NavMesh entegrasyonu (`ArafLabyrinthDirector` + `RuntimeNavMeshBaker`, zemin katmanı fırınlama, straight-line geri çekilme).
3. **Faz 2a — Çok Bölümlü Dünya ve Ada Düzeni (Tamamlandı):** `ArafWorldGenerator`, `ArafWorldLayout`, `ArafWorldSettings` (saf C#). Merkez hub (7×7) + 3 kanat (7×7, dikey Y ofsetleri) ve aralarındaki ada zincirlerinin deterministik üretimi.
4. **Faz 2b — Sahne İnşası ve Boşluk Düşüşü (Tamamlandı, 2026-11-27):** `ArafLabyrinthDirector` çoklu bölüm inşası (`Section_i` kökleri, global slotlar), `Kit_IslandPlatform` (5 m, 1.2 m kalınlık), `ArafVoidFall`, `ArafSafeGround`, `ArafFallRules` (20 hasar, min 1 HP koruması, güvenli noktaya ışınlanma, düşman imhası), sütun prefabı yükseklik düzeltmesi. Unity'de 11 fixture ve Play Mode (4 düzen, 0 B/frame) ile doğrulandı.
5. **Faz 2c — Ada Tehditleri ve Görsel İşaretçiler (Kod Yazıldı, İnceleme Tamamlandı, Unity Doğrulaması Bekliyor):** Mühür odaları çatısından yükselen collidersız kızıl ışık sütunları (`ArafSealLightPillar`, `Araf_SealLightPillar.prefab`, `Mat_Araf_SealBeam`; labirent tavanlı olduğu için yalnızca adalardan görünür, mühür alınınca solarak söner ve sönük kalır) ve ada parkuru tehditleri (`ArafIslandThreatPlanner`, round-robin, ilk/son platform hariç, ≥ 14 m; uyanış kademesi başına Uçan Göz {0,2,2,3} ve Kefaretçi {0,0,1,1}, 10 canlı düşman sınırı; saldırıları oyuncuyu boşluğa itebilir). `ArafTuning` ve `ArafThreatDirector` entegrasyonu tamamlandı.
6. **Faz 3 — Atmosfer (Kodlandı, İncelendi, Unity'de Doğrulandı):** Gerçek ses klipleri ve lav çatlakları hariç tamamı uygulandı: Menekşe palet (kemik 0.42/0.38/0.50, zemin 0.07/0.04/0.11, perde 0.02/0/0.04/0.92, meşaleler 0.62/0.48/1.0; kızıl renk yalnızca portal, mühür ışınları ve kapı bariyerlerinde korundu). Uyanış kademesine göre kademeli lineer menekşe sis ({20,16,12,8} / {140,115,90,70} m, 3 sn lerp, eski 6–38 m kızıl sis kaldırıldı) ve `ArafAtmosphereDirector` (uyanış sting'i dahil; klip `ArafToneSynth` yer tutucusu). Gökyüzü: sisten etkilenmeyen `Skybox/Panoramic` üzerinde prosedürel `Tex_Araf_Sky.png` (menekşe gradyan, Perlin nebula, yıldızlar, azimut 200 / yükseklik 25 menekşe koronalı siyah güneş) ve `VioletRim` yönlü ışığı. Boşlukta süzülen 40 collidersız moloz parçası (`Debris_Arch`, `Debris_Pillar`, `Debris_Monolith`, 25–85 m kabukta, `ArafFloatingDebris` + `ArafDebrisPlanner`, `RunStream.Cosmetic`). `PostFx_Araf` menekşe renk derecelendirmesi (`PostFxBuilder`). Unity'de geçti (2026-11-27).

Ayarlanabilir ajan kararları: merkez + 3 kanat düzeni, düşme hasarı (1 HP bırakma), 7×7 boyut.

Referans görseller: boşlukta yüzen labirent adaları ve siyah güneş; mor gökyüzü altında kemik kemerli koridor ve kızıl çatlaklı zemin; uçurum üstünde gargoyle bloklu platform parkuru.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[CoreLoopAndPurgatory]] ➔ `implements_purgatory`
- [[RunRandomness]] ➔ `seeded_by`
- [[EnemyAIAndSpawning]] ➔ `spawns_elites`
- [[EnemyAIAndSpawning]] ➔ `uses_runtime_navmesh`
- ⬅️ [[ProjectOverview]] ➔ `features_mechanic` *(gelen)*

#### Faz 2c + 3 + yer tutucu ses Unity doğrulaması (2026-11-27)
Builder'lar temiz; 13 Araf test sınıfı geçti (ArafToneSynth 15, LabyrinthGenerator 44 dahil). Play Mode: hub içinde sütun görünmüyor; adadan sütun, gökyüzü, siyah güneş (az 200 / el 25, VioletRim ile aynı) ve 40 çarpışmasız moloz görünüyor; sis 20/140 → 16/115 → 12/90 → 8/70; ada düşmanları mühür 3'te 7 göz + 2 kefaretçi (sınır 10), en yakın 31.9 m; boşluğa düşüş HP 5 → 1, güvenli platforma dönüş; kaçışta sis kule değerlerine (14/60) döndü; oyun kodu ~0 B/kare. Notlar: gözler `FlyingEyeAI.hoverHeight` 2.6 yüzünden ~2.4 m'de süzülüyor (spec 3 m); kefaretçiler kenardan düşmedi. Kanıt: `production/qa/evidence/araf-p3-01..08`.

#### Düzeltme: kule mora boyanıyordu (2026-11-27)
Araf sahnesi kule oynanırken de additive yüklü durur. Faz 3'teki yönlü `VioletRim` ışığı `ArafLevel` köküne bağlıydı ve konumdan bağımsız olduğu için kuleyi de mora boyuyordu. Işık artık `ArafLevel/Content` altında, yalnızca Araf'tayken açık. **Kural:** Araf sahnesine eklenen her ışık, Volume veya global etki `Content` altına konur; Araf sahnesinde başka Volume, probe veya kamera yok. Kanıt: `production/qa/evidence/tower-color-01..03`.

## Faz 4 — Arenalar (Unity'de doğrulandı, 2026-11-27)

Bu bölüm önceki perde/kapı zarı ve üç teklif sınırı açıklamalarının yerini alır. Bütün odalar üretim sırasında kurulur ve koridordan görünür. ArafRooms akışında önce roller, sonra yuva sırasıyla oda seçimleri yapılır. Merkezde arena yoktur; her kanatta mevcut mühür yuvası SealArena, varsayılan bir ek yuva OptionalArena, diğerleri ağırlıklı Shrine/Ossuary olarak Calm olur.

Arena, kapıdan yaklaşık 2 m içeride başlar. Kızıl rünlü taş kapıların engelleri hemen kapanır; görseller 0.35 sn'de iner. Bitişte engeller hemen açılır, kapılar yükselip kapanır. Dalgalar başlangıç uyanışına sabitlenir: isteğe bağlı {2,2,3,3}, mühür {3,3,3,3}; her dalga ghoul {3,3,4,4} ve göz {0,1,1,2}, son dalga kefaretçi {0,0,1,1} içerir. Mühür finaline bir kefaretçi ve ilk kefaretçiye elit güçlendirmesi eklenir. En çok 12 canlı, 0.6 sn kızıl zemin uyarısı, oyuncudan en az 4 m yatay doğuş mesafesi; uygun nokta yoksa en uzak nokta kullanılır. Güncel dalgada en çok bir canlı kalınca sonraki dalga gelir; son dalga sonrası bütün dalgaların ölmesi gerekir. Canlılar 0.5 sn'de bir tekrar uyarılır, ölüm olayı vermeden yok olanlar da sayımdan çıkarılır. Arena kapatılıp açılırsa mevcut dalga korunur, bekleyen telegraph yeniden beklenir ve düşmanlar tekrar doğmaz.

Mühür arenası mevcut mühür → beacon/sütun sönmesi → uyanış → sunak zincirini korur. İsteğe bağlı arena, kendi sunak noktasında mevcut uyanışın arena nadirliğiyle kart sunar. Ziyaret bütçesi mühür + isteğe bağlı arena sayısıdır; CardOfferRoller üst sınırı 6 tekliftir.

ArafArenaEnemyActivator düşman davranışı bağdaştırıcısıdır (bağlandı, 2026-11-27). Doğuş sırası: `Activate` (IEncounterEnemy.OnSpawnFromDirector) → elit ölçekleme → `Hunt` (IHuntableEnemy.BeginHunt(player, profile)). Profil arena başlarken uyanışa göre `ArafArenaWaveTable.GetAggressionProfile(awakening)` ile bir kez seçilir (`aggressionByAwakening` = AggressionProfile_Araf_U0..U3, aralık dışı → son profil; ArafSceneBuilder bağlar). Tarama döngüsü av durmuşsa BeginHunt'ı yeniden çağırır; IHuntableEnemy olmayan düşmanlar IAlertableEnemy.ReceiveAlert yoluna düşer. Ayrıntı: [[EnemyAIAndSpawning]].

### Faz 4 doğrulama özeti (2026-11-27)
- EditMode: ArafArenaPlannerTests 15, ArafRoomRollerTests 6, CardOfferRollerTests 19, ArafToneSynthTests 15 — hepsi geçti. `ArafSceneBuilder.BuildAll()` = 0.
- Koşu: "Arenalar: 3 mühür, 3 isteğe bağlı; 5 sakin oda" (11 oda). Perde yok, tüm odalar üretimde kurulu.
- İsteğe bağlı arena: 2 dalga; 1 düşman kalınca sonraki dalga; temizlenince kapılar açıldı, kart sunağı çıktı. Doğuşlar oyuncudan ≥4 m, uyarı→doğuş ≈0.64 sn.
- Mühür arenası: 3 dalga; son dalgada elit Kefaretçi (400/400, taban 320); temizlenince mühür 1/3, uyanış 1, sunak.
- Kule rengi değişmedi (araf-p4-01 ↔ araf-p1b-07). Kanıt: `production/qa/evidence/araf-p4-*.png`.
- Açık: gerçek girdiyle elle oynanış testi yapılmadı (betikli ışınlama/öldürme). Düşman AI oturumunun `IHuntableEnemy`'si bağlanınca yalnızca `ArafArenaEnemyActivator` değişecek.
