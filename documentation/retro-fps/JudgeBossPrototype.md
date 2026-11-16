---
project: "retro-fps"
type: "AISystem"
tags:
  - project
  - memory
  - "retro-fps"
  - boss
  - ai
  - combat
---

# JudgeBossPrototype (Yargıç Boss Prototipi)

> [!abstract] İki Fazlı Yargıç Karşılaşması
> Unity 6.5 (`6000.5.5f1`), URP ve mevcut dövüş arayüzleri. Son güncelleme: 2026-11-14.

## Mimari ve Fazlar

`JudgeBossLoop` fazları, seçimleri ve süreleri Unity'den bağımsız yürütür. `JudgeBossAI`, `IDamageable`, `IStunnable`, `IEncounterEnemy` ve `IThreatCost` sözleşmeleriyle hareketi, vuruşları ve görselleri uygular. `JudgeBossConfig` saldırı/can ayarlarının kaynağıdır; sorgu, poz ve görsel ayrıntıları ilgili bileşenlerde serialize edilir. Sıcak yollarda sabit tamponlar ve NonAlloc fizik sorguları kullanılır.

- **Trial:** 4500 can; hareket 3 m/sn, tercih edilen mesafe 12 m, dönüş 240°/sn. `engageDelay = 3,0 sn` ilk telgraftan önce beklenir; sonraki normal boşluklar 1,0–1,6 sn.
- **Transition:** 3,0 sn dokunulmazlık; tehlikeler/kılıçlar temizlenir. Oyuncuya yatay 14 m/sn, yukarı 3 m/sn itiş verilir. Mevcut `StopGrapple(false)` ve `StopLunge()` API'leri önce çağrılır. Dash/meteor slam için durdurma API'si yoktur; geçiş içindeki ilk uygun kare beklenir, süre bitince bekleyen itiş iptal edilir.
- **Execution:** 5500 can; hareket 7 m/sn, normal boşluklar 0,6–1,0 sn. Idle/Recover sırasında oyuncuyla 2,2 m minimum ayrılık yumuşak geri çekilmeyle korunur.
- **Kneeling:** Tüm saldırılar ve telgraf parlamaları kapatılır. Gövde doygunluğu düşük, kırmızı emisyonsuz üçüncü materyale geçer. İlk 0,6 sn hasar yok sayılır; sonrasındaki ek hasar olayı bitiricidir. Aynı shotgun atışının çoklu hitbox vuruşları boss'u anında bitiremez.

## Saldırılar ve Config Varsayılanları

Süreler **hazırlık / aktif / toparlanma** sırasındadır.

| Saldırı | Süreler (sn) | Davranış |
| --- | --- | --- |
| Repulse (Kursu Darbesi) | 0,7 / anlık / 0,6 | Yalnız Trial: yatay 6 m içinde biriken 1,2 sn yakınlık; dışarıda aynı hızla azalır. Mevcut saldırı/toparlanma bitince normal seçimden önce gelir; Idle boşluğunu ve ilk engage beklemesini kesebilir. Kırmızı sınır + dolan disk, 7,5 m yarıçap. Zeminden boss pivotu +3 m yüksekliğe kadar 20 hasar, yatay 16 + yukarı 3 m/sn itiş. Parry yok; bitişten/iptalden itibaren 3,5 sn cooldown. |
| Verdict | 0,6 / 0,1 / 0,6 | Parry yapılabilen tek kılıç; 26 m/sn, 20 hasar. |
| Volley | 0,8 / 0,1 / 0,8 | Parry yapılabilen 5 kılıç, toplam 50° yayılım; halkada 6 görsel kılıç. |
| Guillotine | 0,5 / üç darbe / 0,6 | Darbeler 0,45 sn arayla başlar; her işaretin düşüş gecikmesi 0,9 sn, yarıçapı 2,2 m, hasarı 30. Parry yapılamaz. |
| Scale | 1,0 / 3,0 / 0,8 | 14 m içinde seçilir; 16 m kol, 120°/sn, 25 hasar. Hazırlıkta boss yaw +90° yönünde %30 kalınlıkta zararsız kol görünür. Aynı dünya yaw'ından 0,4 sn hızlanmayla döner; tekrar vurma aralığı 0,6 sn. İptal kolu gizler. Parry yapılamaz. |
| Reap | 0,55 / 0,2 / 0,6 | 6 m, 90° yarım açı, 30 hasar; parry yapılabilir. Vuruş aktif pencerenin ortasında, 0,1 sn'de uygulanır. Sürücü zaman adımını o sınırda böler; önceki kesinti vuruşu iptal eder. |
| ExecutionStrike | 0,8 / 0,15 / 1,5 | Boss'un 2,5 m önündeki 2,5 m yarıçaplı çember 35 hasar verir. Şok hattı çember kenarından başlar: 18 m/sn, 20 m yol, 20 hasar. Çember oyuncuya hasar verdiyse o aktivasyonun hattı hasar vermez. İkisine de parry yapılamaz. |
| Thrust | 0,5 / 0,5 / 0,7 | 9 m'den uzakta seçilir; 30 m/sn, en çok 15 m yol, 30 hasar. Oyuncuya 2,5 m kala ilerleme durur; aktif pencere içindeki isabet sorgusu sürer (`thrustHitRadius = 1,2 m`). Parry yapılabilir. |

Trial seçim ağırlıkları Verdict/Volley/Guillotine/Scale için 3 / 2 / 2 / 1,5'tir. Görüş yoksa yalnız Guillotine seçilir. Normal seçim hemen önceki saldırıyı tekrarlamaz. Execution havuzunda Reap/ExecutionStrike ağırlıkları 3 / 2; can %50'nin altındaysa Guillotine ağırlığı 1,5 ile geri gelir. 9 m üzerindeki mesafe, yol açıksa Thrust kuralını kullanır; zorunlu Thrust art arda seçilmez. Eski kilitlenmenin nedeni: engelde biten Thrust önceki saldırı olarak kalıyor, uzaktaki tek aday da tekrar yasağıyla eleniyordu; bu durumda Idle için üst süre sınırı yoktu.

`Context.PlayerReachable`, Thrust küre taramasının oyuncuya `thrustStopDistance` kala engellenmeden ilerleyebilmesini bildirir. Yol kapalıysa uzakta doğrudan Guillotine seçilir; bu güvenlik seçimi can sınırını ve tekrar yasağını aşabilir. Yakında normal yakın dövüş havuzu korunur ve Guillotine can sınırı olmadan havuza girer. Execution Idle toplamı `executionIdleWatchdog = 4 sn` sınırına ulaştığında, normal bekleme süresi veya aday eksikliği ne olursa olsun Guillotine zorlanır. Sayaç her Idle girişinde sıfırlanır; stun ve toparlanma bu süreye dahil değildir.

Can **%25'in altında**, oyuncu 9 m içindeyken Last Word: **Reap → Reap → ExecutionStrike**; bağlantı araları 0,25 sn, son toparlanma 2,0 sn. Bir zincirden sonra (kesilse bile) sıradaki seçim normal Execution kurallarından gelir; ardından zincir yeniden seçilebilir. Zincir içindeki iki Reap bilinçli tekrardır.

## Hasar, Stun ve Hover

**Yakından zıplayarak Blade sömürüsü (Task 4):** Verdict ateş anındaki gerçek oyuncu yüksekliğine nişan alır; her swing yalnız menzil/75° konisindeki mermileri yansıtır, Volley de buna dahildir. Volley'nin sabit 1,4 m hattının üzerinden atlamak ve Scale'in 0,1–0,8 m kolunu zıplayarak aşmak korunur. Guillotine her darbe için oyuncunun o anki XZ konumunu kilitler; eski yer merkezli küre yerine işaretli yarıçap boyunca zeminden `guillotineHitHeight = 12 m` yüksekliğe uzanan düşüş sütununu vurur. İşaretten yatay kaçış korunur. Görüş kapalı Trial artık Guillotine sonrası kilitlenmez; gerektiğinde Guillotine tekrarına izin verilir. Mermi çarpışması, atıcıyı en yakın hedef seçiminden önce eler; spawn noktasındaki ilk örtüşmeyi de NonAlloc sorguyla yakalar (boss altındaki Volley). Repulse, yakın kampı keser; tüm değerleri config'tedir, stun/bağışıklık sürelerini değiştirmez. Mevcut dash/lunge hasar bağışıklığı korunur; Repulse itişi lunge/kancayı durdurur, dash/slam bitene kadar gerekirse bekler. Builder, boss prefab'ının Hazards altına kırmızı `RepulseWarning` sınırını ve dolgu diskini ekler; mevcut hazard materyali kullanılır.

`PlayerHealth.TakeUnparryableDamage` yalnız parry kontrolünü atlar; dash dokunulmazlığı, hasar geri bildirimi/trauma, can ve hasar olayları ile ölüm akışı korunur. Guillotine, Scale, şok hattı ve ExecutionStrike çemberi bu API'yi kullanır. Parry bileşeni geçici kapatılmaz.

Yalnız başarılı yakın dövüş parry'si (2,2 sn) veya **bu Judge örneğinin fırlattığı** bir kılıcın yansıyarak boss'a çarpması (2,0 sn) stun verebilir. Sabit `_launchedSwords` tamponunda sahiplik aranır. Diğer düşman mermileri ve sıradan silah stun'ları reddedilir. Stun sırasında yeni stun kabul edilmez. Stun bitince 6,0 sn bağışıklık başlar; normal idle boşluğu aynı anda işler, bittiğinde boss saldırır. Bağışıklık ek pasif bekleme değildir.

Hover yüksekliği boss kök pivotundan ölçülür: Trial düz arena tabanının 4,5 m üzerinde kalır. Execution hedefi **boss'un ve oyuncunun altındaki zeminden yüksek olanının** `hoverHeightExecution = 0,5 m` üzeridir; `executionClimbSpeed = 6 m/sn` ile yükselir. `maxFloatOverHeight = 2 m`, arena tabanından ölçülen aşılabilir engel üst sınırıdır. NonAlloc kapsül taraması alçak set bulursa önce yükselir, açıklık oluşunca yatay hareket sürer; ön zemin taraması yükselirken hedef irtifanın tekrar düşmesini önler. Duvar/sütunlar aşılabilir set sayılmaz. Dikey ve yatay hareket CharacterController üzerinden çözülür; doğrudan Y konumu yazılarak geometri delinmez. Builder, `groundReference` alanına düz tabandaki `Boss_PlayerCheckpoint` noktasını bağlar; taban retry'da yenilenir. Stun iniş hızı `stunSinkDuration = 0,5 sn` ile hesaplanır; destekleyen zemin inişi sınırlar.

## Cephane İkmali ve Blood Harvest

`BossEncounter.ammoCaches` dört köşe noktasını yönetir. `ArenaAmmoCache`, dövüş başlayınca ilk coroutine dağıtımında beklemeden bir `AmmoPickup` üretir; toplanınca `respawnDelay = 18 sn` sonra yeniler. İlk üretim için 18 sn beklenmez. Paketin ömrü sınırsızdır; yerde kaybolarak sahte bir yeniden dolum başlatmaz. Sürekli görünen kemik kaide yerini belirtir; `readyVisual` paketin hazır olduğunu gösterir, dolarken gizlenir. Yenilgi, reset/Araf ve disable ikmali kapatır ve mevcut paketi temizler; retry ısınması bitip boss yeniden engage olunca açılır.

Kabul edilen parry/yansıtılmış kılıç stun'ı `stunRewardAmmoPickups = 1`, Transition başlangıcı `transitionAmmoPickups = 2` paket ister. Küçük `ArenaAmmoRewards` bileşeni istekleri coroutine içinde dağıtır; reset bekleyen ve alınmamış ödülleri temizler. NonAlloc duvar kırpma, aşağı zemin taraması ve boşluk denetimi arena içinde uygun iniş arar; güvenli nokta bulunamazsa geometri içine paket üretmez. `AmmoRewardUtility.ChooseType`, dalga sonu ödülüyle ortak **en düşük envanter doluluk oranı** kuralını kullanır; eşitlikte rastgele seçim korunur. Miktarlar serialize edilir: fişek **6**, ok **20**. Mevcut `AmmoPickup` prefab'ı atanabilir; atanmazsa dalga ödülündeki `CreatePickupGameObject` mekanizması kullanılır. Araf Kemikhane'nin kaide üstünde mevcut AmmoPickup kullanması da korunur; yeni pickup türü yoktur. Paket üretimi Update/LateUpdate yerine spawn dağıtımında yapılır.

Güncel Cellat Kılıcı tasarımında (`lungeCooldown = 2.2 s`, `BloodHarvestTracker`) Blood Harvest yalnızca **halihazırda sersemlemiş** bir düşmana hücum edildiğinde `+20 HP` ve `1.5 s` `×1.35` hız verir (sersemleme sekansı başına bir kez, hücum başına bir ödül); düz hücum öldürmesi artık iyileştirmez. `IStunState.StunSequence` yalnız kabul edilen yeni stun'ı ayırt eder. Judge Idle/Recover, Transition ve Kneeling stun değildir ve kılıç sersemletmesi reddedilir. Rotasyon: finisher → Sunder → pompalı (`405` tam isabet hasarı). Ghoul ve Flying Eye hareket/hasar/stun süreleri değiştirilmez.

## Araf, Retry ve HUD

`BossEncounter`, `WaveSpawnDirector` gibi kaydedilmiş checkpoint ve `IRetryableEncounter` kullanır. `CanResumeAfterDeath`: aktif/bitmemiş karşılaşma, oyuncu ve kayıtlı checkpoint gerektirir; checkpoint Transform yoksa başlangıç oyuncu pozu kaydedilir. `RetryInterceptor` ve `RunEvents.ActiveEncounter` başlangıç/retry'da birlikte kaydedilir; yalnız kendi kaydı serbest bırakılır.

`SuspendForPurgatory` boss'u sıfırlar, saldırıları ve boss barını temizler, bariyerleri açar, kaydı bırakır ve askıya alma bilgisini korur. Bileşenin geçici kapanması bu bilgiyi silmez. `RunDirector` bekleyen karşılaşmayı saklar; Araf dönüşünde `ResumeAfterPurgatory` çağırır. Oyuncu checkpoint'te dirilir; boss tam Trial canına ve spawn pozuna döner. Bariyerler kapanır ve kayıt yenilenir. `retryWarmupDuration = 2,5 sn` ardından `Engage` Trial'ı ve boss barını yeniden açar. İlk telgraf ayrıca 3,0 sn engage gecikmesini bekler. Bitmiş karşılaşma yeniden başlamaz. Bounds denetimi, çocuk arena trigger'ı ve sahne açılışında içerideki oyuncu için başlangıcı destekler.

## Builder ve Bilinen Borçlar

**Menü: `RetroFPS/Build Judge Boss`** — Play Mode dışında, Gauntlet kaydedildikten sonra çalıştırılır. Mevcut config değerleri korunur. Builder config, URP materyalleri, `Boss_Judge` / `Sword_Judge` prefab'ları ve Gauntlet placeholder bağlantılarını Unity API'leriyle günceller. `kneelingMaterial` prefab'a, `groundReference` sahne örneğindeki checkpoint'e bağlanır. `ArenaAmmoRewards` boss prefab'ına eklenip `ammoRewards` alanına bağlanır. `Judge_ArenaAmmoCache` kaide prefab'ı oluşturulur/güncellenir; `readyVisual` bağlanır. Dört örnek encounter altında `Boss_AmmoCache_0..3` adlarıyla idempotent bulunur/oluşturulur, arena bounds köşelerinden 3 m içeri ve tabana yerleştirilir, `ammoCaches` dizisine bağlanır. Gauntlet konumları x=±19, z=205/243'tür; x=±17 sütunlardan, sunaktan ve duvarlardan açıktır. Araf kemik materyali varsa kullanılır, yoksa aynı renkte Judge kemik materyali üretilir. Yeni süre/miktar/float ayarları serialized varsayılanlardan gelir; mevcut tasarımcı tuning'i ezilmez. Kemik beyazı maskede ve mat altın cübbe kenarlarında ölçülü emisyon; Guillotine işaretinde kenarı dahil tam opak, parlak materyal kullanılır. Eski transparan materyal ayarları yeniden build'de temizlenir.

- **Mermi havuzlama:** Flying Eye ile aynı Instantiate/Initialize mekanizması korunur. Sabit istek tamponunu tek kalıcı coroutine Update/LateUpdate dışında boşaltır; fırlatma yerinde önceden ısıtılmış havuz için TODO bulunur. `EnemyProjectile` bu geçişte değiştirilmez. Reset, faz değişimi, bitirici ve disable mevcut kılıçları temizler.
- **Gauntlet boss kapıları:** TestLevelBuilder henüz boss odası kapısı üretmez; bariyer dizisi boş kalabilir. Builder yalnız boss placeholder / `I_BossRoom` altındaki kapıları tüketir; başka odaların kapılarını kullanmaz.
- **Geçici sanat:** İlkel gövde, kılıçlar ve efektler placeholder'dır; nihai model/animasyon ve arena görsel/fizik kontrolü bekler.
- **Doğrulama sınırı (Task 7):** Unity ve builder çalıştırılmadı; `.meta`, prefab/sahne/asset YAML ve commit üretilmedi. Core, Gameplay, Menu, HUD, Editor ve EditMode test kaynakları Unity 6000.5.5f1 referanslarıyla bağımsız C# derlemesini geçti. Unity dışında Mono ile gerçek NUnit assertion'larını yürüten saf JudgeBossLoop/BloodHarvestTracker denetimi: **54 geçti, 0 başarısız**. Bu denetim fizik/CharacterController, pickup zamanlaması ve görselleri çalıştırmaz; oluşturulan varlıklar Editor'da yeniden build sonrası dais, duvar/sütun, reset ve ikmal toplama akışıyla kontrol edilmelidir.

## İlişkiler (Sinaptik Ağ Bağlantıları)

- [[ProjectOverview]] ➔ `indexed_by`
- [[CombatAndDamageInterfaces]] ➔ `utilizes_interfaces`
- [[EnemyAIAndSpawning]] ➔ `utilizes_projectiles`
- [[PlayerMovementArchitecture]] ➔ `utilizes_movement`
- [[WaveAndEncounterArchitecture]] ➔ `shares_retry_contract`
- [[CoreLoopAndPurgatory]] ➔ `resumes_after_araf`
- [[CombatHudArchitecture]] ➔ `reports_boss_health`
