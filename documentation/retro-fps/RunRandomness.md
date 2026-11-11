---
project: "retro-fps"
type: "SystemArchitecture"
tags:
  - project
  - memory
  - "retro-fps"
  - randomness
  - roguelite
---

# RunRandomness (Seed'li Deterministik Rastgelelik ve Dalga Havuzu Sistemi)

> [!abstract] SEED'Lİ, DETERMINİSTİK, SIFIR-GC RASTGELELİK
> Seed'li, deterministik, sıfır-GC rastgelelik; aynı seed → aynı run (demo/test için).

---

## 1. MİMARİ VE ÇEKİRDEK YAPILAR

### RunRandom (`Scripts/Core/RunRandom.cs`)
- **Namespace:** `RetroFPS.Run`
- **Algoritma:** SplitMix64 ile tohum genişletme + xoshiro/xorshift çekirdek.
- **API:**
  - `NextUInt()`
  - `Value01()`
  - `Range(int, int)` (max hariç)
  - `Range(float, float)`
  - `Chance(float)`
  - `WeightedIndex(float[], count)` (-1 = geçerli ağırlık yok)
  - `Shuffle<T>(T[], count)`
  - `Derive(masterSeed, RunStream)`

### Akışlar (`RunStream`)
- **Tanımlı Akışlar:**
  - `Encounter` (oda dalga seçimi)
  - `Loot` (Faz 3 lanet/lütuf için ayrıldı)
  - `Araf` (portal + düşman yerleşimi)
  - `Cosmetic`
- **Tasarım Kuralı:** Oyun mantığı seed'li akışları kullanır; VFX/ses/gib gibi kozmetik rastgelelik `UnityEngine.Random`'da kalır (aksi halde seed tekrar üretilemez).

### Erişim ve Yaşam Döngüsü
- `RunState.GetStream(stream)` (önbellekli)
- `RunEvents.CurrentRun`, `RunEvents.GetStream(stream)` (RunDirector yoksa TickCount tohumlu yedek akış).
- `RunDirector`, `[DefaultExecutionOrder(-500)]` ile diğer bileşenlerin Awake'inden önce `SetCurrentRun` yapar.
- Sabit seed: RunDirector Inspector `fixedSeed` (0 = rastgele).

---

## 2. DALGA HAVUZLARI VE SEÇİM MEKANİZMASI

### WaveSetPool (`Scripts/Combat/WaveSetPool.cs`)
- **Menü Yolu:** `RetroFPS/Combat/Wave Set Pool`
- **Yapı:** Ağırlıklı girdiler, ağırlık ≤ 0 = hariç; `avoidRepeatsInRun` shuffle-bag (run seed değişince sıfırlanır).
- **Çalışma Prensibi:** `WaveSpawnDirector.waveSetPool` atanırsa Awake'te bir kez seçer (`_runtimeWaveSet`), serialized `waveSet` yedek olarak kalır; retry/Araf dönüşünde aynı set korunur.
- **Örnek Varlık:** `Data/Waves/WaveSetPool_Gauntlet.asset` (R1/R2/R3, Gauntlet'in 3 odası her run'da karışık sırayla çeker).

### Bilinen Sınırlar
- Odalar ortak `Encounter` akışından sırayla çektiği için deterministiklik, sahnedeki `WaveSpawnDirector` Awake sırasının sabit olmasına dayanır (testlerde sabitti; Unity bunu garanti etmez). Gerekirse ileride oda kimliğine göre sıralı seçim yapılabilir.

---

## 3. TESTLER VE DOĞRULAMA

- **EditMode Testleri:** `Assets/_Project/Tests/EditMode/` (`RetroFPS.Tests.EditMode` asmdef) — 13 test (RunRandom determinism/sınırlar/ağırlık dağılımı/shuffle, RunState akış önbelleği ve Araf hakkı), hepsi geçti (2026-11-11).
- **Play Mode Doğrulaması:** Seed 4242 iki kez → aynı oda/set eşleşmesi ve aynı Araf portal konumu; farklı seed'ler → farklı eşleşmeler.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[CoreLoopAndPurgatory]] ➔ `seeds_run`
- [[WaveAndEncounterArchitecture]] ➔ `randomizes_wave_sets`
- ⬅️ [[ProjectOverview]] ➔ `features_system` *(gelen)*
