# Game Brief: PURGATOR

## In your words
Hikaye kurgusu: karakterimizin tüm ailesi kötücül şeytanlar tarafından çocukken
tutsak edilmiştir ve karakterimiz bunun acısıyla yanıp tutuşur, çok çalışır büyür
güçlenir ve öcünü almak üzere kuleye gider, düşmanları kolaylıkla alt eder ve bu
kötülüğü yapan main bossa ulaşır, buraya kadar animasyonsuz 2d çizimlerle hikaye
oyuncuya aktarılır. Son bossta oyun başlar, boss karakterle dalga geçer, bu kadar
kolay alt edebileceğini mi sandın der ve karakterimizi de diğer sevdikleri gibi
kuleye hapseder ve karakterimiz kulenin en alt katından başlar, küçüklü büyüklü
şeytani varlıklarla savaşarak üst katlara giderek ana bossu öldürmek ister, her
katın sonunda şeytanlaşmış bir sevdiğiyle boss fight yaparak onu şeytani güçlerden
kurtarır, kat kat aşarak en üst katta son bossu keser ve kötülük biter.

**Pitch:** Ailesini kurtarmak için şeytani bir kuleyi en alt kattan zirveye kadar
kat kat biçen, hızlı tempolu bir retro boomer shooter. *(inferred (strong))*

**Genre:** Retro boomer shooter / rogue-lite / dark fantasy *(vault: ProjectOverview)*

**Controls:** WASD hareket, Space zıplama, Sol Shift dash, Ctrl/C eğilme-kayma,
sol tık ateş, sağ tık alternatif ateş, Q veya orta tık Karşı Hüküm (parry),
1/2/3 silah, Esc duraklat. *(koddan: PlayerMovement.cs, WeaponController.cs,
PlayerParryController.cs)*

**Hooks:**
- Hikâye son boss'ta başlar: oyuncu kaybeder, kuleye hapsedilir ve en alttan
  tırmanır. *(you said)*
- Her katın sonundaki boss şeytanlaşmış bir sevdiğidir; yenmek onu kurtarır.
  *(you said)*
- Ölüm oyunu bitirmez: koşu başına bir kez Araf labirentine düşülür, kaçan
  kuleye döner. *(koddan; hikâye metninde yok — KARAR BEKLİYOR: hikâyeye bağı)*

## Look and references
- Look: düşük poligonlu modeller, point-filtered düşük çözünürlüklü dokular,
  yüksek kontrastlı gotik aydınlatma, gore ve parçalanma. *(vault: ProjectOverview)*
- References: DUSK, ULTRAKILL, Quake 1, Devil Daggers. *(vault: ProjectOverview)*
- View: 3D · Camera: birinci şahıs.
- Hikâye girişi: animasyonsuz 2D çizimler. *(you said)* — kodda EKSİK.

## First minute
2D çizimlerle geçmiş anlatılır; oyun ana boss'un karşısında başlar, boss
karakterle alay eder ve onu kulenin en altına hapseder. Oyuncu en alt katta
kontrolü alır. *(you said)* — giriş sunumu ve bu sahne kodda EKSİK; öğretici EKSİK.

## Challenge
KARAR BEKLİYOR. Kodda olan: kapıları kilitlenen dalga karşılaşmaları, 3 düşman
tipi ve elit varyantları, 2 fazlı Yargıç boss'u.

## Core loop
- Odaya gir, kapılar kapanır, dalgaları temizle. *(koddan: WaveSpawnDirector)*
- Öldürdükçe can küresi ve cephane düşer; otomatik can yenilenmez. *(koddan)*
- Kat sonunda şeytanlaşmış aile üyesiyle savaş, kurtar, üst kata çık. *(you said)*
  — kat geçişi kodda EKSİK.
- Ölürsen Araf'a düş, kart seç, portaldan kaç, odanın başına dön. *(koddan)*

## Goal & fail state
- Hedef: en üst katta ana boss'u öldürmek. *(you said)* — zafer durumu kodda EKSİK.
- Başarısızlık: Araf hakkı kullanıldıktan sonra kulede ölmek ya da Araf'ta
  ölmek = gerçek ölüm, koşu sıfırlanır. *(koddan: RunDirector)*

## Features
- [x] Hareket: air-strafe, bunny-hop, slide, 3 şarjlı dash
- [x] 3 silah: Tekrarlı Arbalet, İnfazcı Kılıcı, Gotik Pompalı + Meat Hook
- [x] Karşı Hüküm (parry)
- [x] 3 düşman: Ghoul Stalker, Nişancı Göz, Kefaretçi (+ elitler)
- [x] Dalga karşılaşmaları
- [x] Araf labirenti ve 6 lanet/lütuf kartı
- [x] Yargıç boss'u (şeytanlaşmış aile üyesi; hangisi — KARAR BEKLİYOR)
- [ ] Kat geçişi ve çok katlı kule (kat sayısı — KARAR BEKLİYOR)
- [ ] Ana boss (kimliği ve tasarımı EKSİK)
- [ ] Diğer aile üyesi boss'ları
- [ ] Hikâye girişi (2D çizimler) ve açılış boss sahnesi
- [ ] Zafer ekranı

## Out of scope
KARAR BEKLİYOR. production/roadmap.md jüri build'i için şunları kesmeyi öneriyor
(henüz onaylanmadı): diğer aile boss'ları, silah loot'u, koşu kaydı, Addressables,
renk körlüğü paleti, UI ölçekleme.
- Kalıcı ilerleme (ruh cevheri): vault'ta var, kodda yok — KARAR BEKLİYOR.

## Build order
KARAR BEKLİYOR (production/roadmap.md W09–W15 planı taslak durumda).

---
**Who it's for / what they feel:** KARAR BEKLİYOR.

**Games you love / don't:** DUSK, ULTRAKILL, Quake 1, Devil Daggers *(vault)*.
