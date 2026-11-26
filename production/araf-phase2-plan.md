# Araf Faz 2 — Yapı planı (TASLAK, onay bekliyor)

> 2026-11-26 gecesi kullanıcı uyurken hazırlandı. Kod yazılmadı. Tasarım kaynağı: vault `ArafLabyrinthSystem.md` §9.1–9.3.
> Faz 1a + 1b kodda ve doğrulandı (commit yok).

## Hedef
Tek 9×9 labirent yerine **merkez + 3 kanat**: kapalı (tavanlı) kemik labirentler, aralarında boşlukta yüzen ada/köprü parkurları. Oyuncu merkezde başlar, her kanadın en derininde bir mühür var, 3 mühürle merkez portalı açılır. Boşluğa düşmek öldürmez.

## Sayısal temel (koddan, Player.prefab değerleri)
- Zıplama: `jumpForce 7.2`, `gravity 25`, düşüşte ×1.3 → tepe ≈ **1.04 m**, aynı yüksekliğe havada kalma ≈ 0.54 s.
- Yatay: yürüme 8 m/s → zıplamayla ≈ **4.3 m**; bunny-hop tavanı 11.5 m/s → ≈ 6.2 m; dash 30 m/s × 0.25 s = **7.5 m** (3 şarj, prefabda yeniden dolum 5 s).
- Öneri parkur kuralları: normal boşluk **2–3.5 m**, yükselme her adımda **≤ 0.8 m**, isteğe bağlı "dash boşluğu" **≤ 6 m** (kanat başına en fazla 1, alternatif yolu olmadan değil). Ajan Navigasyonu: adalar arası NavMeshLink yok → yakın dövüşçüler kendi adasında 5–7 m uzakta pusuya yatar, kenardan düşmez (navigasyon devir notu).

## Önerilen yapı (karar bekleyen değerler ✱)
| Parça | Öneri |
|---|---|
| Merkez | ✱ 7×7, 1–2 normal oda (Shrine/Ossuary), başlangıç = portal hücresi, kenarında 3 "ağız" (boşluğa açılan kapı) |
| Kanatlar | ✱ 3 × 7×7, her biri 2–3 normal oda + 1 mühür odası (kanat ağzından BFS en derin yuva) |
| Yükseklik | Merkez Y=1000, kanatlar ✱ Y=1004 / 1008 / 1012 (hepsi Y>0 kuralına uyar) |
| Ada zinciri | ✱ ağız→ağız 4–6 platform, toplam yükseliş kanadın Y farkı kadar; platform 4×4…6×6 m, kemik kemer/sütun parçalarıyla |
| Düşme | Ada altında öldürmeyen tetik: 20 hasar ama en az 1 can bırakır + son güvenli platforma ışınla (karar verildi) |
| Yön | Mühür odalarının üstünde tavanı delen kızıl ışık sütunu (yalnız adalardan görünür); labirent içinde ses işareti sürer |
| Tehdit | Uyanış ≥1'de adalara Uçan Göz, köprü/geniş platformlara Kefaretçi (itme + mermi = boşluğa itme). Dalga dönüş yolu artık kanat içi yol + ada zinciri |

## Kod değişikliği (taslak dosya listesi)
**Saf C# (testli)**
- `LabyrinthGenerator` / `LabyrinthSettings`: zorunlu başlangıç hücresi + kenar "ağız" açıklıkları (mevcut testler korunur; varsayılan davranış değişmez).
- Yeni `ArafWorldLayout` + `ArafWorldGenerator`: bölümler (merkez + kanatlar), bölüm konum/yükseklikleri, ağız eşleşmeleri, ada zinciri platformları (konum/boyut, boşluk ve yükselme kurallarıyla). Seed: `RunStream.ArafLayout`.
- `ArafSealSlotPicker`: kanat başına 1 mühür (ağızdan en derin) modu.
- `ArafThreatPlanner`: bölümler arası dönüş yolu (kanat içi yol + ada zinciri + merkez içi yol).
- Testler: determinism, her bölüm bağlantılı, ağızlar karşılıklı, platform boşlukları ≤ 3.5 m (dash boşluğu hariç), yükselme ≤ 0.8 m, tüm Y > 0, platformlar labirentlerle çakışmıyor.

**Çalışma zamanı**
- `ArafLabyrinthDirector`: birden çok bölüm kurar (geometri döngüsü bölüm başına), ağız kapıları, mühür/portal dağılımı; NavMesh tek kökte bir kez.
- Yeni `ArafIslandBuilder` (platform + kemer/sütun parça yerleşimi), `ArafVoidFall` (düşme tetiği), `ArafSafeGround` (son güvenli nokta; oyuncunun yere basma durumundan, Update'te sıfır alloc), `ArafSealLightPillar`.
- `ArafThreatDirector`: ada tehditleri (Göz/Kefaretçi prefabları), bölüm bazlı devriye dağıtımı.
- `ArafSceneBuilder`: ada/sütun/ışın prefabları, ayar varlığı.

**Riskler**
- Bu en büyük faz; önerilen bölme: **2a** saf düzen + testler → **2b** sahne geometrisi + adalar + düşme → **2c** ada tehditleri + ışık sütunları + ayar.
- Yürüme/zıplama hissi ancak gerçek oyunla ayarlanır (doğrulama ajanı ışınla test eder, his doğrulanamaz).
- Mevcut seed'lerin labirent düzeni değişir (kayıtlı seed yok, sorun beklenmiyor).

## Sabah sorulacaklar
1. Bölüm boyutları/oda sayıları ve Y yükseklikleri öneri gibi kalsın mı?
2. Dash boşluğu olsun mu (kanat başına en fazla 1, opsiyonel)?
3. 2a → 2b → 2c sırasıyla başlayayım mı?
