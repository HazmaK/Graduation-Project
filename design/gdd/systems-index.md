# Systems Index: PURGATOR

> **Status**: Draft
> **Created**: 2026-11-17
> **Last Updated**: 2026-11-17
> **Source Concept**: design/game-brief.md

---

## Overview

PURGATOR hızlı tempolu bir retro boomer shooter / rogue-lite: oyuncu şeytani bir
kuleyi en alt kattan zirveye tırmanır, kapıları kilitlenen dalga odalarını temizler,
her katın sonunda şeytanlaşmış bir aile üyesiyle savaşır ve ilk ölümünde Araf
labirentinden kaçarak koşuya devam eder. Mekanik çekirdek (hareket, üç silah, parry,
üç düşman, dalgalar, Araf, kartlar, bir boss) kodda vardır; çok katlı kule, hikâye
sunumu, diğer boss'lar, zafer akışı ve kalıcı ilerleme EKSİK'tir. "Status" sütunu
kodun durumunu gösterir (2026-11-17 taraması); GDD'ler koddan tersine belgelenir.

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | Dövüş çekirdeği | Core | MVP | Implemented | design/gdd/combat-core.md | — |
| 2 | Stat katmanı | Core | MVP | Implemented | design/gdd/stat-layer.md | — |
| 3 | Seed'li rastgelelik | Core | MVP | Implemented | — | — |
| 4 | Girdi ve kontroller | Core | MVP | Implemented | — | — |
| 5 | Oyuncu hareketi | Gameplay | MVP | Implemented | design/gdd/player-movement.md | Girdi, Stat katmanı |
| 6 | Oyuncu canı, overheal, ölüm | Gameplay | MVP | Implemented | design/gdd/player-health.md | Dövüş çekirdeği, Stat katmanı |
| 7 | Karşı Hüküm (parry) | Gameplay | MVP | Implemented | — | Dövüş çekirdeği, Oyuncu canı, Girdi |
| 8 | Silah çekirdeği (slot, cephane, viewmodel) | Gameplay | MVP | Implemented | — | Dövüş çekirdeği, Girdi, Stat katmanı |
| 9 | Tekrarlı Arbalet | Gameplay | MVP | Implemented | — | Silah çekirdeği |
| 10 | İnfazcı Kılıcı | Gameplay | MVP | Implemented | — | Silah çekirdeği, Oyuncu hareketi, Oyuncu canı |
| 11 | Gotik Pompalı + Meat Hook | Gameplay | MVP | Implemented | — | Silah çekirdeği, Oyuncu hareketi |
| 12 | Düşman YZ çekirdeği | Gameplay | MVP | Implemented | — | Dövüş çekirdeği |
| 13 | Ghoul Stalker | Gameplay | MVP | Implemented | — | Düşman YZ çekirdeği |
| 14 | Nişancı Göz | Gameplay | MVP | Implemented | — | Düşman YZ çekirdeği, Karşı Hüküm |
| 15 | Kefaretçi | Gameplay | MVP | Implemented | — | Düşman YZ çekirdeği, Karşı Hüküm |
| 16 | Karşılaşma ve dalga direktörü | Gameplay | MVP | Implemented | — | Dövüş çekirdeği, Seed'li rastgelelik, Düşmanlar |
| 17 | Can küresi ve cephane düşüşü | Economy | MVP | Implemented | — | Dövüş çekirdeği, Oyuncu canı, Silah çekirdeği |
| 18 | Koşu yapısı (kule, Araf hakkı, gerçek ölüm) | Progression | MVP | Kısmen — kat geçişi ve zafer EKSİK | — | Oyuncu canı, Karşılaşma, Seed'li rastgelelik |
| 19 | Araf labirenti | Gameplay | Vertical Slice | Implemented | — | Koşu yapısı, Düşman YZ çekirdeği, Seed'li rastgelelik |
| 20 | Lanet/lütuf kartları | Progression | Vertical Slice | Implemented | — | Stat katmanı, Araf labirenti, Koşu yapısı |
| 21 | Boss çerçevesi + Yargıç | Gameplay | Vertical Slice | Implemented (prototip) | — | Dövüş çekirdeği, Koşu yapısı, Karşı Hüküm |
| 22 | Aile boss'ları | Gameplay | Alpha | EKSİK | — | Boss çerçevesi, Hikâye sunumu |
| 23 | Hikâye sunumu (2D giriş, boss diyaloğu) | Narrative | Alpha | EKSİK | — | Koşu yapısı |
| 24 | Kalıcı ilerleme (ruh cevheri) | Progression | Full Vision | EKSİK — KARAR BEKLİYOR | — | Koşu yapısı, Koşu kaydı |
| 25 | Öğretici | Meta | Alpha | EKSİK | — | Hareket, Silahlar, Karşı Hüküm |
| 26 | Oyun hissi (kamera, hitstop, VFX, gore, hasar sayıları) | Gameplay | MVP | Implemented | — | Dövüş çekirdeği, Oyuncu hareketi |
| 27 | Ses | Audio | Vertical Slice | Kısmen — müzik ve AudioMixer EKSİK | — | Dövüş çekirdeği, Menü/ayarlar |
| 28 | Atmosfer ve retro render | Gameplay | Vertical Slice | Implemented | — | — |
| 29 | HUD | UI | Vertical Slice | Implemented | design/ux/hud.md | Oyuncu canı, Silahlar, Koşu yapısı, Boss |
| 30 | Menü, pause, ayarlar, dil | UI | Vertical Slice | Implemented | design/ux/main-menu.md (pause için belge yok) | Girdi, Koşu yapısı |
| 31 | Koşu özeti, zafer ekranı, jenerik | UI | Alpha | EKSİK | — | Koşu yapısı |
| 32 | Koşu kaydı | Persistence | Full Vision | EKSİK (yalnızca ayarlar PlayerPrefs'te) | — | Koşu yapısı |

Not: brief ayrı bir sistem değildir (`design/game-brief.md`); onaylanan listedeki
0 numara budur.

---

## Categories

| Category | Description |
|----------|-------------|
| **Core** | Her şeyin dayandığı temel sistemler |
| **Gameplay** | Oyunu eğlenceli yapan sistemler |
| **Progression** | Oyuncunun koşu içinde ve koşular arasında ilerlemesi |
| **Economy** | Kaynak üretimi ve tüketimi |
| **Persistence** | Kayıt ve süreklilik |
| **UI** | Oyuncuya bilgi gösteren ekranlar |
| **Audio** | Ses ve müzik |
| **Narrative** | Hikâye ve diyalog sunumu |
| **Meta** | Çekirdek döngünün dışındaki sistemler |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Çekirdek döngü için gerekli | İlk oynanabilir | Önce tasarla |
| **Vertical Slice** | Tek, eksiksiz bir bölüm için gerekli | Demo | İkinci |
| **Alpha** | Tüm özellikler kaba hâlde | Alpha | Üçüncü |
| **Full Vision** | Cila ve olsa iyi olur | Beta / Yayın | Gerektikçe |

---

## Dependency Map

### Foundation Layer (no dependencies)

1. Dövüş çekirdeği — tüm hasar, stun ve karşılaşma sözleşmeleri
2. Stat katmanı — kartların ve oyuncu değerlerinin ortak hesaplayıcısı
3. Seed'li rastgelelik — dalga, Araf ve kart zarlarının kaynağı
4. Girdi ve kontroller

### Core Layer (depends on foundation)

1. Oyuncu hareketi — depends on: Girdi, Stat katmanı
2. Oyuncu canı — depends on: Dövüş çekirdeği, Stat katmanı
3. Karşı Hüküm — depends on: Dövüş çekirdeği, Oyuncu canı, Girdi
4. Silah çekirdeği ve üç silah — depends on: Dövüş çekirdeği, Girdi, Stat katmanı, Oyuncu hareketi
5. Düşman YZ çekirdeği ve üç düşman — depends on: Dövüş çekirdeği, Karşı Hüküm
6. Karşılaşma ve dalga direktörü — depends on: Dövüş çekirdeği, Seed'li rastgelelik, Düşmanlar
7. Can küresi ve cephane düşüşü — depends on: Dövüş çekirdeği, Oyuncu canı, Silah çekirdeği

### Feature Layer (depends on core)

1. Koşu yapısı — depends on: Oyuncu canı, Karşılaşma, Seed'li rastgelelik
2. Araf labirenti — depends on: Koşu yapısı, Düşman YZ çekirdeği
3. Lanet/lütuf kartları — depends on: Stat katmanı, Araf labirenti
4. Boss çerçevesi + Yargıç — depends on: Dövüş çekirdeği, Koşu yapısı, Karşı Hüküm
5. Aile boss'ları — depends on: Boss çerçevesi, Hikâye sunumu
6. Hikâye sunumu — depends on: Koşu yapısı
7. Kalıcı ilerleme — depends on: Koşu yapısı, Koşu kaydı
8. Öğretici — depends on: Hareket, Silahlar, Karşı Hüküm

### Presentation Layer (depends on features)

1. Oyun hissi — depends on: Dövüş çekirdeği, Oyuncu hareketi
2. Ses — depends on: Dövüş çekirdeği, Menü/ayarlar
3. Atmosfer ve retro render — bağımsız
4. HUD — depends on: Oyuncu canı, Silahlar, Koşu yapısı, Boss
5. Menü, pause, ayarlar, dil — depends on: Girdi, Koşu yapısı

### Polish Layer (depends on everything)

1. Koşu özeti, zafer ekranı, jenerik — depends on: Koşu yapısı
2. Koşu kaydı — depends on: Koşu yapısı

---

## Recommended Design Order

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
|-------|--------|----------|-------|----------|-------------|
| 1 | Dövüş çekirdeği | MVP | Foundation | game-designer | S (yazıldı) |
| 2 | Stat katmanı | MVP | Foundation | systems-designer | S |
| 3 | Oyuncu hareketi | MVP | Core | game-designer | M |
| 4 | Oyuncu canı, overheal, ölüm | MVP | Core | game-designer | S |
| 5 | Karşı Hüküm | MVP | Core | game-designer | S |
| 6 | Silah çekirdeği | MVP | Core | game-designer | S |
| 7 | Tekrarlı Arbalet | MVP | Core | systems-designer | S |
| 8 | İnfazcı Kılıcı | MVP | Core | systems-designer | M |
| 9 | Gotik Pompalı + Meat Hook | MVP | Core | systems-designer | S |
| 10 | Düşman YZ çekirdeği | MVP | Core | game-designer | M |
| 11 | Ghoul Stalker | MVP | Core | game-designer | S |
| 12 | Nişancı Göz | MVP | Core | game-designer | S |
| 13 | Kefaretçi | MVP | Core | game-designer | S |
| 14 | Karşılaşma ve dalga direktörü | MVP | Core | level-designer | M |
| 15 | Can küresi ve cephane düşüşü | MVP | Core | economy-designer | S |
| 16 | Koşu yapısı | MVP | Feature | game-designer | M |
| 17 | Araf labirenti | Vertical Slice | Feature | level-designer | M |
| 18 | Lanet/lütuf kartları | Vertical Slice | Feature | systems-designer | S |
| 19 | Boss çerçevesi + Yargıç | Vertical Slice | Feature | game-designer | M |
| 20 | Hikâye sunumu | Alpha | Feature | narrative-director | M |
| 21 | Aile boss'ları | Alpha | Feature | game-designer | L |
| 22 | Oyun hissi | MVP | Presentation | game-designer | M |
| 23 | Ses | Vertical Slice | Presentation | audio-director | S |
| 24 | Kalanlar (rastgelelik, girdi, atmosfer, pause, öğretici, özet/jenerik, kalıcı ilerleme, kayıt) | karışık | karışık | — | — |

---

## Circular Dependencies

- Kod düzeyinde `Combat ↔ Player ↔ Weapons` döngüsü var (vault:
  `CodebaseDependencyGraph`). Tasarım belgelerinde döngü, sözleşmelerin tamamı
  Dövüş çekirdeği GDD'sinde toplanarak kırılır.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| Dövüş çekirdeği | Technical | Darboğaz: silahlar, düşmanlar, boss ve parry buna bağlı | İlk yazılan GDD; değişiklikte bağımlı GDD'ler gözden geçirilir |
| Koşu yapısı | Design / Scope | Kat geçişi ve zafer durumu EKSİK; Araf, kartlar, boss, hikâye buna bağlı | Kat sayısı ve Araf'ın hikâye bağı kararı beklenir |
| Aile boss'ları | Scope | Kodda yok; sayı ve kimlikler kararlaştırılmadı; yol haritası jüri build'inden kesmeyi öneriyor | Karar verilene kadar GDD'de KARAR BEKLİYOR |
| Hikâye sunumu | Scope | Kodda hiçbir karşılığı yok | 2D durağan çizimlerle sınırlı (brief) |
| Kalıcı ilerleme | Scope | Vault'ta var, kodda yok, kayıt sistemi gerektiriyor | KARAR BEKLİYOR |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 32 |
| Design docs started | 4 (+ 2 UX belgesi: HUD, ana menü) |
| Design docs reviewed | 0 |
| Design docs approved | 0 |
| MVP systems designed | 4/19 |
| Vertical Slice systems designed | 0/7 |

---

## Next Steps

- [ ] Sıradaki GDD: Karşı Hüküm (parry)
- [ ] Açık kararlar: Yargıç hangi aile üyesi, ana boss'un adı, kat sayısı, Araf'ın
      hikâyedeki anlamı, kalıcı ilerleme, hasar tipi/zırh
- [ ] Her GDD tamamlanınca `/design-review`
