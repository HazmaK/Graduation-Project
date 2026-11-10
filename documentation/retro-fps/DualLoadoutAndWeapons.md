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
> Hızlı tempolu çatışmalar için tasarlanmış ana silahlar (Menzilli Seri Arbalest ve Ağır Cellat Kılıcı) ile sol el taktiksel savuşturma kalkanının (Spiked Buckler) sinerjisi ve denge parametreleri.

---

## 1. DUAL-LOADOUT FELSEFESİ VE EKİPMAN DÖNGÜSÜ

```mermaid
flowchart LR
    subgraph ANA_SILAHLAR["Sağ El Ana Silahları (Loot & Swap)"]
        W1["Repeater Arbalest\nSeri Delici Cıvata (Menzilli)"]
        W2["Executioner Blade 2.0\n3'lü Kombo / Lunge / Meteor Slam / Deflect"]
        W3["Gothic Shotgun\nÇifte Saçma & Meat Hook"]
    end
    
    subgraph IKINCIL_TAKTIK["Sol El Modüler Ekipmanı"]
        Buckler["Spiked Buckler (İsteğe Bağlı Parry)\nMermi Yansıtma & Sersemletme"]
    end
    
    W1 <-->|Silah Değişimi (1/2/3 veya Tekerlek)| W2 <--> W3
    W1 & W2 & W3 -.->|Parry Aktifken (Q / MMB)| Buckler
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

### 2. `Executioner Blade` (Cellat Kılıcı 2.0 - Slot 2)
- **Silah Rolü:** Yakın dövüş infazı, mermi kesme, agresif hücum ve alan sersemletme.
- **Birincil Saldırı (Sol Tık - 3'lü Akıcı Kombo):**
  * **Hit 1 (Yatay Kesik):** Hızlı sağdan sola darbe (`damage = 45f`, `range = 3.4m`).
  * **Hit 2 (Ters Açı Kesik):** Hızlı soldan sağa geri savurma (`damage = 55f`, `range = 3.4m`).
  * **Hit 3 (Ağır Finisher):** Yukarıdan aşağı infaz darbesi (`damage = 95f`, `range = 3.4m`, `knockback = 12m/s`). İsabet ettiği düşmanı 1.2 saniye sersemletir (`IStunnable.Stun`).
  * **Kombo Hafızası:** Vuruşlar arası 0.70s tolerans süresi bulunur.
- **Mermi Dilimleme (Projectile Slicing):**
  * Savurma açısına giren düşman plazma mermilerini (`EnemyProjectile`) havada keser ve crosshair hedefine geri saptırır (`Deflect`).
- **İkincil Saldırı (Sağ Tık - Çift Fonksiyonlu Hareket Darbesi):**
  * **Düz / Yerde (Executioner's Lunge):** 8-10 metre ileriye şimşek hızında atılma (`damage = 95f`, `lungeSpeed = 28f`, I-Frames). Sersemlemiş düşmana vurulduğunda veya düşman öldürüldüğünde **Blood Harvest** tetiklenir: Oyuncuya **+25 HP Can Sömürüsü** ve 1.5s boyunca **+%35 Adrenalin Hız Bonusu** verir.
  * **Havada Aşağı Bakarken (Meteor Slam):** Oyuncu havadayken yere doğru -34f hızla çakılır. Yere indiğinde 5.5 metre çapında 80 alan hasarı vurur ve tüm düşmanları 2.2 saniye sersemletir (`IStunnable.Stun`).
- **Görsel Sunum ve Vuruş Kinematiği:** Prosedürel viewmodel savurma, lunge saplama ve çakılma animasyonları, hitstop mikro dondurması ve dinamik pitch/çan sesleri.

### 3. `Gothic Shotgun` (Gotik Pompalı & Meat Hook - Slot 3)
- **Silah Rolü:** Yakın temas yüksek burst hasar ve düşmana çekilerek mesafe kapatma.
- **Birincil Ateş:** Geniş konili çift namlulu kurşun saçması (Pellet spread). Yakın mesafeden Ghoul ve uçan gözleri paramparça eder.
- **İkincil Ateş (Meat Hook / Grapple):**
  * Namlunun altındaki kancayı fırlatır. Yalnızca düşman hedeflere (`IDamageable`) kenetlenir.
  * Oyuncuyu 29f sabit hızla (`hookPullSpeed`) düşmana doğru çeker (`PlayerMovement.StartGrapple`). Düşman hareket etse bile dinamik olarak takip eder.
  * Hedefe 2.0m kala kanca çözülür ve oyuncu yüksek hız ivmesiyle düşmanın tepesinde pompalı veya kılıçla ölümcül kombinasyon yapabilir.

### 4. `Spiked Buckler` (Çivili Savuşturma Kalkanı - Opsiyonel Sol El)
- **Modülerlik:** `PlayerParryController.enableShield` ile tercihe bağlıdır (varsayılan: pasif).
- **Savuşturma Penceresi:** 0.35s aktif süre, 0.50s bekleme.
- **Yansıtma & Sersemletme:** Düşman mermilerini crosshair'a 1.7x hız ve 2.5x hasarla yansıtır; yakın dövüş saldırılarını %100 bloke edip düşmanı 2.2 saniye sersemletir (`IStunnable.Stun`).

---

## 3. BAŞKA BİR AI İÇİN DENGELEME KURALLARI
1. Yeni bir silah dengelenirken Arbalest'in DPS'i (saniyede ~238 hasar) ve Kılıcın en ağır tek vuruşları (Finisher ve Lunge: 95 hasar) referans alınmalıdır.
2. Yakın dövüş silahlarında mutlaka `HashSet<IDamageable>` ile çoklu darbe koruması uygulanmalıdır.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[WeaponArsenalArchitecture]] ➔ `implemented_by_code`
- [[CombatAndDamageInterfaces]] ➔ `applies_damage_to_interfaces`
- [[HitstopAndAudioFeedback]] ➔ `triggers_feedback`
- [[CombatVFXAndDecalSystem]] ➔ `triggers_vfx`
- ⬅️ [[ProjectOverview]] ➔ `utilizes_combat` *(gelen)*
- ⬅️ [[PlayerMovementArchitecture]] ➔ `synergizes_with` *(gelen)*
