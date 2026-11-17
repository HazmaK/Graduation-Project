# Oyuncu Canı, Overheal ve Ölüm (Player Health)

> **Durum**: Taslak
> **Kaynak**: koddan tersine belgeleme (2026-11-17)
> **Değerler**: `Graduation Project/Assets/_Project/Prefabs/Player/Player.prefab:1081-1084`
> (betik varsayılanlarıyla aynı)
> **Betik**: `Graduation Project/Assets/_Project/Scripts/Player/PlayerHealth.cs`
> — aşağıdaki `:satır` gösterimleri bu dosyaya aittir.

## 1. Overview

Oyuncunun canını, aldığı hasarı, iyileşmeyi, azami canın üstündeki geçici fazla
canı (overheal) ve ölüm anını yönetir. Can kendiliğinden yenilenmez; yalnızca
savaşarak kazanılır. Ölümden sonra ne olacağına bu sistem değil, koşu yapısı karar
verir.

## 2. Player Fantasy

Can geri çekilerek değil saldırarak kazanılır. Fazla can kısa süreli bir
üstünlüktür; kullanılmazsa erir.

## 3. Detailed Rules

**Hasar alma sırası** (`:141-181`):

1. Simülasyon duraklatılmışsa ya da oyuncu ölüyse hasar yok.
2. Oyuncu dokunulmazsa (dash) hasar yok.
3. Karşı Hüküm saldırıyı savuşturursa hasar yok.
4. Hasar DamageTaken stat'ından geçer ve candan düşülür.
5. Hasar geri bildirimi tetiklenir.
6. Can 0'a inerse ölüm.

**Savuşturulamaz hasar:** 3. adımı atlar; dokunulmazlık yine korur. Yalnızca
Yargıç kullanıyor (5 saldırı: `Combat/Boss/JudgeShockLine.cs:122`,
`JudgeScaleSweep.cs:147`, `JudgeGuillotineStrike.cs:130`, `JudgeBossAI.cs:666`,
`:691`). (`:185-198`)

**İyileşme** (`:208-234`): miktar HealReceived stat'ından geçer. Overheal izinliyse
tavan = max(azami can, OverhealCap stat'ı); değilse azami can. Can zaten tavandaysa
iyileşme reddedilir. Tavanı çağıran belirler (can küresi `Combat/HealthOrb.cs:312`,
Kan Hasadı `Weapons/ExecutionerBladeWeapon.cs:506`, Araf sunağı
`Run/Labyrinth/ArafRoom.cs:236`); varsayılan 125.

**Overheal erimesi:** can azami canın üstündeyken saniyede 3.5 azalır. (`:352`)

**Otomatik yenilenme:** KAPALI. Açılırsa hasarsız 4.5 sn sonra saniyede 12.
(`:23-25`, `:355-362`)

**Azami can değişimi** (`:123-134`): azalırsa mevcut can yeni azami + önceki
overheal miktarına kırpılır, ama 1'in altına inmez. Artarsa can eklenmez. Azami can
1'in altına inemez. (`:40`)

**Ölüm** (`:236-269`): hareket, bakış, silah ve parry kapanır; imleç serbest kalır;
kamera yere iner ve yana yatar.

**Ölüm sonrası:** RunDirector varsa akışı o yönetir. Yoksa R, Space ya da Enter
sahneyi yeniden yükler; aktif oda retry'ı üstlenebilir. (`:274-293`, `:327-345`)

**Diriliş:** verilen noktada tam canla; tüm kontroller geri açılır. (`:298-325`)

## 4. Formulas

- `alınanHasar = Stat(DamageTaken, hasar)`; `can = max(0, can − alınanHasar)`
  **Örnek:** 100 can, 35 hasar, DamageTaken +%50 → 52.5 hasar → 47.5 can.
- `iyileşme = Stat(HealReceived, miktar)`; `can = min(tavan, can + iyileşme)`
- **Overheal erimesi:** can → azami can, 3.5 can/sn.
  **Örnek:** 150 → 100 yaklaşık 14.3 sn.
- `canOranı = can / azamiCan` (stat koşulları bunu okur). (`:42-49`)

## 5. Edge Cases

- Ölüyken hasar ve iyileşme yok sayılır. (`:144`, `:211`)
- 0 ya da negatif iyileşme reddedilir. (`:211`)
- Azami canı düşüren bir kart oyuncuyu öldüremez (can en az 1 kalır). (`:130`)
- Savuşturulan vuruş otomatik yenilenme sayacını yine sıfırlar. (`:156`)
- Hasar sonrası dokunulmazlık penceresi YOK: aynı karede gelen birden çok vuruşun
  hepsi uygulanır.
- RunDirector varken yerel yeniden başlatma tuşları çalışmaz. (`:280-283`, `:332`)

## 6. Dependencies

- **Bağımlı olduğu:** dövüş çekirdeği (`IDamageable`), stat katmanı (MaxHealth,
  DamageTaken, HealReceived, OverhealCap), oyuncu hareketi (dokunulmazlık), Karşı
  Hüküm, duraklatma.
- **Buna bağımlı:** koşu yapısı (ölüm sahibi), stat katmanı (koşul bağlamı),
  düşmanlar ve Yargıç (hasar veren), can küresi, İnfazcı Kılıcı (Kan Hasadı), Araf
  sunağı, HUD, hasar geri bildirimi.

## 7. Tuning Knobs

Aralıklar ÖNERİ'dir: kodda tanımlı değildir, elle test edilmemiştir.

| Değer | Prefab | Önerilen aralık | Etkisi |
|---|---|---|---|
| Azami can | 100 | 80 – 150 | Kaç vuruşta ölündüğü |
| Otomatik yenilenme | kapalı | kapalı | Açılırsa "savaşarak kazan" ekonomisi bozulur |
| Yenilenme gecikmesi | 4.5 sn | — | Kapalıyken etkisiz |
| Yenilenme hızı | 12 can/sn | — | Kapalıyken etkisiz |

Kodda sabit (ayarlanamıyor): overheal erimesi 3.5 can/sn (`:352`), varsayılan
overheal tavanı 125 (`:208`), ölüm kamerası yüksekliği 0.40 ve açısı (`:262-264`).

## 8. Acceptance Criteria

- 100 canlı oyuncu 35 hasar alınca 65 canı kalır.
- Dash sırasında alınan hasar canı değiştirmez.
- Overheal kapalı iyileşme canı azami canın üstüne çıkarmaz.
- 150 can, hasar almadan 14.3 sn (± 0.5) içinde 100'e iner.
- Hasarsız 30 sn beklenince 100'ün altındaki can artmaz.
- Can 0'a inince hareket, bakış, silah ve parry kapanır.
- Azami canı 100'den 70'e düşüren bir değişiklikte 100 canlı oyuncu 70'e iner;
  5 canlı oyuncu 5'te kalır.

## Açık Konular

- **EKSİK (KARAR BEKLİYOR):** hasar sonrası kısa dokunulmazlık yok.
- **Kural sapması:** overheal erime hızı ve varsayılan tavan kodda sabit.
- **Çelişki:** `RestartGame` `fixedDeltaTime`'ı 0.02'ye sabitliyor (`:278`); vault
  notu `PauseMenu` bu değerin asla sabit yazılmamasını söylüyor.
- **Kural sapması:** dash ile kaçınmada her seferinde `Debug.Log` metni üretiliyor
  (`:149`), bu bellek tahsisi demek.
