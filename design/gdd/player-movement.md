# Oyuncu Hareketi (Player Movement)

> **Durum**: Taslak
> **Kaynak**: koddan tersine belgeleme (2026-11-17)
> **Değerler**: `Graduation Project/Assets/_Project/Prefabs/Player/Player.prefab`
> (oyunda geçerli olan); betik varsayılanı farklıysa ayrıca belirtilir.
> **Betik**: `Graduation Project/Assets/_Project/Scripts/Player/PlayerMovement.cs`
> — aşağıdaki `:satır` gösterimleri bu dosyaya aittir.

## 1. Overview

Quake tarzı ivmelenme üzerine kurulu birinci şahıs hareket: yerde hızlı ivmelenme
ve sürtünme, havada yön verme ile hız taşıma (bunny-hop), kayma, üç şarjlı
dokunulmaz dash. Kanca çekişi, kılıç atılması ve çakılma da bu sistemin
durumlarıdır; tetikleyenleri silah GDD'lerindedir.

## 2. Player Fantasy

Durmak ölümdür. Oyuncu hızını zıplayarak taşır, kayarak kazanır, dash ile
tehlikenin içinden geçer.

## 3. Detailed Rules

**Durum önceliği** (her karede yalnızca biri): Dash > Kanca > Atılma (lunge) >
Çakılma (slam) > normal hareket. (`:310-349`)

**Yer:** sürtünme uygulanır, sonra hedef hıza ivmelenilir. Hedef hız: çömelirken
3.8, değilse yürüme hızı × hız takviyesi. (`:487-489`) İnişten sonra 0.08 sn
sürtünme uygulanmaz (bunny-hop penceresi); yalnızca çarpma hızı > 3 m/s ya da
havada kalma > 0.1 sn ise. (`:445-448`)

**Hava:** aynı ivmelenme, hedef hız 3.5. Yatay hız 11.5 tavanını aşamaz; tavanın
üstündeyse saniyede 2.5 m/s azalarak tavana iner. (`:499-511`)

**Zıplama:** dikey hız 7.2. Tuş 0.15 sn önceden basılabilir; zeminden ayrıldıktan
0.12 sn sonrasına kadar zıplanabilir. (`:113-114`, `:541-553`)

**Yerçekimi:** 25; düşerken × 1.3 = 32.5. (`:532-533`)

**Kayma:** yerdeyken, yatay hız > 6 iken çömelme tuşuna basılınca başlar. Hız en
az 13'e çıkar, yön o anki hareket yönüdür, 0.85 sn sürer ve erken bitirilemez;
sürtünme 3.2. Kapsül 2.0 → 1.0 m. Kayarken zıplanırsa hıza en fazla +1.5 eklenir.
(`:411-413`, `:565-597`, `:543-550`)

**Çömelme:** hız ≤ 6 iken tuşa basılınca; tuş bırakılınca kalkar. (`:416-423`)

**Dash:** Sol Shift. 3 şarj; şarj başına dolum 5 sn (Stat: DashRechargeTime).
0.25 sn sürer, baştan sona hasar almaz. Yön: girdi varsa girdi yönü, yoksa bakış
yönü; dikey hız sıfırlanır. Hız 30'dan yürüme hızı × 1.25'e iner, bitince yürüme
hızına ayarlanır. (`:639-679`, `:684-692`)

**Kanca çekişi:** hedefe sabit hızla çekilir (en az 16); 2.0 m'de ya da 1.6 sn
sonra biter. Space ile koparılırsa bakış yönüne hız × 0.95 + yukarı zıplama × 1.2
ile fırlatılır. (`:705-782`)

**Atılma / çakılma / hız takviyesi:** dışarıdan tetiklenir. Çakılma yatay hızı
keser ve sabit hızla aşağı indirir; yere değince hız sıfırlanır. Hız takviyesi
yürüme hızını, hava hedef hızını ve hava tavanını çarpar. (`:789-886`)

**Dış itki (`AddImpulse`):** dash, kanca, atılma ya da çakılma sırasında yok
sayılır. (`:269-282`)

**Tuşlar:** WASD hareket, Space zıplama, Sol Shift dash, Sol Ctrl ya da C
çömelme/kayma. (`:383-408`)

## 4. Formulas

- **İvmelenme:** `eklenecek = min(ivme × hedefHız × dt, hedefHız − (hız · yön))`.
  Yöndeki hız hedefe ulaştıysa ekleme yok. (`:519-527`)
- **Sürtünme:** `yeniHız = max(hız − hız × sürtünme × dt, 0)`. (`:472-473`)
- **Zıplama yüksekliği:** `zıplama² / (2 × yerçekimi)` = 7.2² / 50 ≈ 1.04 m.
- **Dash hızı:** `lerp(30, yürüme × 1.25, t^1.5)`, `t = geçen süre / 0.25`.
  (`:670-671`) Yürüme 8 iken ortalama hız 22 m/s → mesafe ≈ 5.5 m.
- **Kayma-zıplama bonusu:** `min(1.5, max(0, 11.5 + 4 − hız))`. (`:548`)
- **Kanca bitişi:** yatay hız > yürüme × 1.4 ise yürüme × 1.55'e indirilir, dikey
  hız en az 3.2. (`:743-745`)
- **Yürüme hızı:** `Stat(MoveSpeed, 8)`. (`:124`)
- **Üç şarjın dolumu:** 3 × 5 sn = 15 sn.

## 5. Edge Cases

- Y < 0 olursa oyuncu Y = 0.05'e taşınır, dikey hız sıfırlanır. (`:363-370`)
- Yukarı fırlatılırken zemin teması yok sayılır. (`:433`)
- Oyun duraklatılmışken girdi ve zıplama tamponu sıfırlanır. (`:286-291`)
- Dash sırasında yeni dash başlatılamaz. (`:641`)
- Kayarken dash başlarsa kayma durumu kapatılmıyor (kanca, atılma ve çakılma
  kapatıyor). Oyun içinde sonucu doğrulanmadı. (`:639-663`)
- Dolum süresi 0 ya da altına inerse şarj her karede dolar. (`:687`)

## 6. Dependencies

- **Bağımlı olduğu:** stat katmanı (MoveSpeed, DashRechargeTime), duraklatma.
- **Buna bağımlı:** İnfazcı Kılıcı (atılma, çakılma, hız takviyesi), Gotik Pompalı
  (kanca, geri tepme itkisi), Kefaretçi (fırlatma), oyuncu canı (dokunulmazlık),
  kamera hissi, HUD (dash şarjları), can küresi (dash'te çekim menzili).

## 7. Tuning Knobs

Aralıklar ÖNERİ'dir: kodda tanımlı değildir, elle test edilmemiştir.

| Değer | Prefab | Önerilen aralık | Etkisi |
|---|---|---|---|
| Yürüme hızı | 8 | 7 – 10 | Temel tempo |
| Çömelme hızı | 3.8 | 3 – 5 | Çömelirken hareket |
| Yer ivmesi | 18 | 12 – 25 | Yön değiştirme keskinliği |
| Yer sürtünmesi | 8.5 | 6 – 12 | Durma mesafesi |
| İniş sürtünme muafiyeti | 0.08 sn | 0.05 – 0.15 | Bunny-hop kolaylığı |
| Hava ivmesi | 4 | 2 – 8 | Havada yön verme |
| Hava hedef hızı | 3.5 | 2 – 5 | Strafe ile hız kazanımı |
| Hava hız tavanı | 11.5 | 10 – 16 | Bunny-hop üst hızı |
| Tavan üstü azalma | 2.5 m/s² | 1 – 6 | Fazla hızın ne kadar taşındığı |
| Zıplama kuvveti | 7.2 | 6 – 9 | Yükseklik (1.04 m) |
| Yerçekimi | 25 | 20 – 32 | Havada kalma süresi |
| Düşüş çarpanı | 1.3 | 1.0 – 1.8 | İnişin sertliği |
| Kayma hızı | 13 | 11 – 16 | Kayma başlangıç hızı |
| Kayma sürtünmesi | 3.2 | 2 – 5 | Kaymanın sönümü |
| Kayma asgari hızı | 6 (betik varsayılanı 4) | 4 – 7 | Kaymanın ne kolay başladığı |
| Kayma süresi | 0.85 sn | 0.6 – 1.2 | Kayma uzunluğu |
| Kayma-zıplama bonusu | 1.5 | 0 – 3 | Kaymadan zıplamanın ödülü |
| Dash hızı | 30 | 24 – 36 | Mesafe (5.5 m) |
| Dash süresi | 0.25 sn | 0.18 – 0.35 | Dokunulmazlık penceresi |
| Dash şarjı | 3 | 2 – 4 | Art arda dash sayısı |
| Dash dolum süresi | 5 sn (betik varsayılanı 1.2) | 1 – 6 | Dash'in ne kadar idareli kullanıldığı |

Kodda sabit (ayarlanamıyor): zıplama tamponu 0.15 sn, coyote 0.12 sn, kanca durma
mesafesi 2.0 m, kanca azami süre 1.6 sn. (`:89-90`, `:113-114`)

## 8. Acceptance Criteria

- Düz zeminde ileri koşuda yatay hız 8 m/s'ye ulaşır ve aşmaz.
- Yerden zıplamada tepe yüksekliği 1.04 m (± 0.05).
- Havada strafe ile yatay hız 11.5 m/s'yi aşmaz.
- Hız 6'nın üstündeyken çömelme tuşu kayma, altındayken çömelme başlatır.
- Dash 0.25 sn sürer; bu sürede alınan hasar uygulanmaz.
- Üç dash arka arkaya atılabilir, dördüncüsü atılamaz.
- Bir şarj 5 sn'de dolar.
- Dash sırasında uygulanan dış itki hızı değiştirmez.

## Açık Konular

- **KARAR (2026-11-17):** dash dolum süresi 5 sn'dir (prefab). Betik varsayılanı
  (`:46`) ve vault notu `PlayerMovementArchitecture` hâlâ 1.2 sn yazıyor; ikisi de
  bu belgeyle uyumsuz.
- **Çelişki:** kayma asgari hızı prefab'da 6, betikte 4 (`:33`). Bu belge prefab
  değerini esas alır.
- Betikteki yorum dash mesafesini "~7 metre" diyor (`:669`); formül 5.5 m veriyor.
- **EKSİK:** oyun girdisi yalnızca klavye/fareden okunuyor; gamepad ile hareket yok
  ve tuş atama mümkün değil. `InputSystem_Actions` dosyası oynanışta
  kullanılmıyor. `design/accessibility-requirements.md` tuş atamayı taahhüt ediyor.
- **Kural sapması:** dört zamanlama değeri kodda sabit.
- Çömelme hızı MoveSpeed stat'ından etkilenmiyor.
