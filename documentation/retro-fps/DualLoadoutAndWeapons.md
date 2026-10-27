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
        W2["Executioner Blade\nGeniş Savurma / Sivri Saplama (Melee)"]
    end
    
    subgraph IKINCIL_TAKTIK["Sol El Sabit Ekipmanı"]
        Buckler["Spiked Buckler (Parry Kalkanı)\nMermi Yansıtma & Sersemletme"]
    end
    
    W1 <-->|Hızlı Silah Değişimi (Q / Tekerlek)| W2
    W1 & W2 <-->|Anında Parry İptali (Q / MMB)| Buckler
```

---

## 2. SİLAH DENGE VE KİNEMATİK PARAMETRELERİ

### 1. `Repeater Arbalest` (Seri Delici Arbalest)
- **Silah Rolü:** Yüksek tempolu menzilli baskı ateşi ve zırh delme.
- **Mermi Tipi:** Hızlı Hitscan cıvata mermisi ve doğrusal tracer fişeği.
- **Ateş Hızı:** Saniyede 8.5 atış (`fireRate = 8.5f`).
- **Hasar:** Vuruş başına 28 birim hasar (`damage = 28f`).
- **Geri Tepme Yaylanması (Recoil Bloom):**
  * Seri atış sürdürüldükçe koni açısı 2.4 dereceye kadar genişler.
  * Oyuncu parmağını tetikten çektiğinde 0.15 saniye içinde başlangıç hassasiyetine geri döner.
- **Vuruş Geri Bildirimi:** Hedefe çarptığında anlık kıvılcım ve kan damlası saçar; nişangaha hitmarker çizer.

### 2. `Executioner Blade` (Cellat Kılıcı)
- **Silah Rolü:** Yüksek hasarlı kalabalık biçme ve tekil zırh delme.
- **Birincil Saldırı (Sol Tık - Yatay Geniş Savurma):**
  * **Menzil & Açı:** 2.8 metre menzil, 110 derecelik yatay ön koni.
  * **Hasar:** Darbe başına 55 birim taban hasar.
  * **Çoklu Hitbox Önleme Tamponu (Anti-Duplicate):** Kılıç tek bir savurmada aynı hedefin kafa ve gövde collider'larına ayrı ayrı çarpıp çift hasar vermesin diye `HashSet<IDamageable>` tamponu kullanır. Her düşman tek bir savurmadan yalnızca 1 kez hasar alır.
- **İkincil Saldırı (Sağ Tık - Sivri Saplama):**
  * **Menzil & Hasar:** 3.6 metre menzil, 110 birim yüksek zırh delici hasar.
  * **Hitstop:** İsabet anında `HitstopManager.TriggerHitstop(0.08f)` mikro dondurması ile ağır tokluk hissi sağlar.
- **Görsel Sunum ve Vuruş Kinematiği:**
  * Görsel netliği korumak adına ekran ortasını kaplayan yapay hilal/trail şeritleri kullanılmaz.
  * Vuruş hissi; prosedürel viewmodel savurma kinematiği, kamera ileri atılma sarsıntısı ve et temas partikülleriyle sağlanır.

### 3. `Spiked Buckler` (Çivili Savuşturma Kalkanı)
- **Savuşturma (Parry) Penceresi:** 0.35 saniye aktif kabul süresi.
- **Toparlanma & Bekleme:** 0.20 saniye toparlanma, 0.50 saniye bekleme süresi.
- **Mermi Yansıtma (Deflection):**
  * Baktığınız yöne doğru 130 derecelik kabul açısı ve 4.2 metre tarama menzili.
  * Mermiyi crosshair yönüne 1.7x hız ve 2.5x hasar çarpanıyla geri püskürtür; mermi rengini elektro maviye dönüştürür.
- **Yakın Dövüş Sersemletme (Stun):**
  * Ghoul vb. yakın dövüş saldırılarını %100 bloke eder ve saldıran düşmanı 2.2 saniye boyunca hareketsiz bırakır (`IStunnable.Stun`).

---

## 3. BAŞKA BİR AI İÇİN DENGELEME KURALLARI
1. Yeni bir silah dengelenirken Arbalest'in DPS'i (saniyede ~238 hasar) ve Kılıcın tek vuruşluk saplama hasarı (110 hasar) referans alınmalıdır.
2. Yakın dövüş silahlarında mutlaka `HashSet<IDamageable>` ile çoklu darbe koruması uygulanmalıdır.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[WeaponArsenalArchitecture]] ➔ `implemented_by_code`
- [[CombatAndDamageInterfaces]] ➔ `applies_damage_to_interfaces`
- [[HitstopAndAudioFeedback]] ➔ `triggers_feedback`
- [[CombatVFXAndDecalSystem]] ➔ `triggers_vfx`
- ⬅️ [[ProjectOverview]] ➔ `utilizes_combat` *(gelen)*
- ⬅️ [[PlayerMovementArchitecture]] ➔ `synergizes_with` *(gelen)*
