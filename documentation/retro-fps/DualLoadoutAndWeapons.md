---
project: "retro-fps"
type: "CombatDesign"
tags:
  - project
  - memory
  - "retro-fps"
  - combat
  - weapons
  - loadout
  - balance
---

# DualLoadoutAndWeapons (Dual-Loadout ve Silah Dövüş Dinamikleri)

> [!abstract] HIZLI SİLAH DEĞİŞİMİ VE DÖVÜŞ DENGESİ (COMBAT BALANCE)
> Hızlı tempolu çatışmalar için tasarlanmış ana silahlar (Menzilli Seri Arbalest, Ağır Cellat Kılıcı ve Gotik Pompalı) ile Karşı Hüküm (Counter-Verdict) savuşturma sisteminin sinerjisi ve denge parametreleri. Spiked Buckler kaldırıldı (2026-11-15).

---

## 1. DUAL-LOADOUT FELSEFESİ VE EKİPMAN DÖNGÜSÜ

```mermaid
flowchart LR
    subgraph ANA_SILAHLAR["Sağ El Ana Silahları (Loot & Swap)"]
        W1["Repeater Arbalest\nSeri Delici Cıvata (Menzilli)"]
        W2["Executioner Blade\n3'lü Kombo / Lunge / Meteor Slam / Sunder"]
        W3["Gothic Shotgun\nÇifte Saçma & Meat Hook"]
    end
    
    subgraph IKINCIL_TAKTIK["Karşı Hüküm (Counter-Verdict)"]
        KH["Q / MMB: Anlık Executioner Blade Savurması\nPencere içinde Mermi Yansıtma & Sersemletme"]
    end
    
    W1 <-->|Silah Değişimi (1/2/3 veya Tekerlek)| W2 <--> W3
    W1 & W2 & W3 -.->|Q / MMB (her silahtan)| KH
```

---

## 2. SİLAH DENGE VE KİNEMATİK PARAMETRELERİ

### 1. `Repeater Arbalest` (Seri Delici Arbalest - Slot 1)
- **Silah Rolü:** Yüksek tempolu menzilli baskı ateşi ve zırh delme.
- **Mermi Tipi:** Hızlı Hitscan cıvata mermisi ve doğrusal tracer fişeği.
- **Ateş Hızı:** Saniyede 8.5 atış (`fireRate = 8.5f`).
- **Hasar:** Vuruş başına 28 birim hasar (`damage = 28f`).
- **Geri Tepme Yaylanması (Recoil Bloom):**
  * Seri atış sürdürüldükçe koni açısı 2.4 dereceye kadar genişler.
  * Oyuncu parmağını tetikten çektiğinde 0.15 saniye içinde başlangıç hassasiyetine geri döner.
- **Vuruş Geri Bildirimi:** Hedefe çarptığında anlık kıvılcım ve kan damlası saçar; nişangaha hitmarker çizer.

### 2. `Executioner Blade` (Cellat Kılıcı - Slot 2)
- **Silah Rolü:** Tempo/infaz odaklı yakın dövüş, mermi kesme, zincir hücum ve ateşli silahlar için Sunder hazırlığı.
- **Birincil Saldırı (Sol Tık - 3'lü Kombo, her savurmada yeni basış, 0.15 s girdi tamponu):**
  * **Adım 1 (Slash):** `34` hasar, `0.22 s` bekleme süresi (`3.4 m / 85°`).
  * **Adım 2 (Backhand):** `42` hasar, `0.22 s` bekleme süresi (`3.4 m / 85°`).
  * **Adım 3 (Finisher):** `71.11 × 1.35` garantili kritik = `96` hasar, `0.10 s` kurma (wind-up), `0.44 s` bekleme süresi, `1.0 s` sersemletme (`3.91 m / 63.75°`). Hayatta kalan hedeflere `4 s` **Sunder (Yarık)** uygular (yalnızca ateşli silah hasarını `×1.25` artırır).
  * **Kombo Hafızası & Hız Keskinliği:** `0.70 s` hareketsizlikte sıfırlanır. Yatay hız `≤8 m/s` → `×1.00`, `≥13 m/s` → `×1.35` kombo hasarı. Kalkanlı hedefe (`IMeleeGuard`) tek başına vurursa seker (`0` hasar, 1. adıma sıfırlama, `+0.30 s` bekleme).
- **Mermi Kesme:**
  * Savurmalar ön konideki düşman mermilerini (`2.8 m`, `±75°`) kesip yok eder (`EnemyProjectile.Cut()`); yansıtmaz (yansıtma yalnızca Counter-Verdict'tedir).
- **İkincil Saldırı (Sağ Tık - Çift Fonksiyonlu Hareket Darbesi):**
  * **Düz / Yerde (Executioner's Lunge):** `95` hasar, `28 m/s` hızla `0.22 s` (`≈6.2 m`), **dokunulmazlık (i-frame) yok**, yolundaki mermileri (`1.8 m`, `±90°`) her karede keser. Bekleme `2.2 s`; en az bir öldürmede `0.10 s` (**infaz zinciri / chain reset**). **Blood Harvest** yalnızca halihazırda sersemlemiş hedefe vuruşta `+20 HP` + `1.5 s` `×1.35` hız verir (sersemleme sekansı başına bir kez, hücum başına bir ödül; düz hücum öldürmesi iyileştirmez).
  * **Havada Aşağı Bakarken (Meteor Slam):** Düşme yüksekliği `h` (`1 m → 6 m`) ile ölçeklenir: `45 → 120` hasar, `3.0 → 5.5 m` yarıçap, `0.5 → 1.8 s` sersemletme; `h ≥ 3.5 m` guard break (`2 s`) uygular. İnişten sonra `1.0 s` bekleme süresi.
- **Görsel Sunum ve Vuruş Kinematiği:** Prosedürel viewmodel savurma, lunge saplama ve çakılma animasyonları, hitstop mikro dondurması ve dinamik pitch/çan sesleri.

### 3. `Gothic Shotgun` (Gotik Pompalı & Meat Hook - Slot 3)
- **Silah Rolü:** Yakın temas yüksek burst hasar ve düşmana çekilerek mesafe kapatma.
- **Birincil Ateş:** Geniş konili çift namlulu kurşun saçması (Pellet spread). Yakın mesafeden Ghoul ve uçan gözleri paramparça eder.
- **İkincil Ateş (Meat Hook / Grapple):**
  * Namlunun altındaki kancayı fırlatır. Yalnızca düşman hedeflere (`IDamageable`) kenetlenir.
  * Oyuncuyu 29f sabit hızla (`hookPullSpeed`) düşmana doğru çeker (`PlayerMovement.StartGrapple`). Düşman hareket etse bile dinamik olarak takip eder.
  * Hedefe 2.0m kala kanca çözülür ve oyuncu yüksek hız ivmesiyle düşmanın tepesinde pompalı veya kılıçla ölümcül kombinasyon yapabilir.

### 4. `Karşı Hüküm` (Counter-Verdict - Spiked Buckler yerine)
- **Kaldırılma:** Spiked Buckler kalkanı kaldırıldı; yerine bu yetenek geldi (2026-11-15). Kalkan, `enableShield` ve sol el soketi artık kodda yoktur.
- **Modülerlik:** `PlayerParryController.enableCounter` ile açılıp kapanır (varsayılan: açık).
- **Girdi ve Hızlı Vuruş:** `Q` veya `Fare Orta Tuşu (MMB)` her silahta çalışır. Executioner Blade'in anlık savurma vuruşu oynatılır (`WeaponController.TryPlayQuickBlade()`): aktif silahın görseli 0.25 saniye gizlenir ve kılıç görseli gösterilir; bu sürede ateş ve silah değişimi girdileri yok sayılır.
- **Hasar:** Savurma 25 hasar (`quickSwipeDamage`) ile kılıç konisine vurur. Kombo ilerlemez, kombo/cooldown durumu değişmez ve mermi kesmez.
- **Savuşturma Penceresi:** 0.18s saldırı penceresi (`counterActiveWindow`). Boşa çıkarsa 0.35s toparlanma kilitlenmesi (`counterWhiffRecovery`); ayrı cooldown yoktur.
- **Başarı:** Her başarılı savuşturma sonraki vuruşu hemen kullanılabilir yapar ve pencereyi açık tutar; tek vuruşta birden fazla mermi yansıtılabilir.
- **Kabul Açısı:** 110 derece koni (`counterAngle`).
- **Yansıtma & Sersemletme:** Düşman mermilerini crosshair'a 1.7x hız ve 2.5x hasarla yansıtır; önden gelen yakın dövüş darbesini pencere içinde bloke edip saldırganı 2.2 saniye sersemletir (`IStunnable.Stun`).

---

## 3. BAŞKA BİR AI İÇİN DENGELEME KURALLARI
1. Yeni bir silah dengelenirken Arbalest'in DPS'i (saniyede ~238 hasar) ve Kılıcın en ağır tek vuruşları (Finisher: 96, Lunge: 95 hasar) referans alınmalıdır.
2. Yakın dövüş silahlarında mutlaka `HashSet<IDamageable>` ile çoklu darbe koruması uygulanmalıdır.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[WeaponArsenalArchitecture]] ➔ `implemented_by_code`
- [[CombatAndDamageInterfaces]] ➔ `applies_damage_to_interfaces`
- [[HitstopAndAudioFeedback]] ➔ `triggers_feedback`
- [[CombatVFXAndDecalSystem]] ➔ `triggers_vfx`
- ⬅️ [[ProjectOverview]] ➔ `utilizes_combat` *(gelen)*
- ⬅️ [[PlayerMovementArchitecture]] ➔ `synergizes_with` *(gelen)*
