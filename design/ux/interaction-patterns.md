# Interaction Pattern Library

> **Status**: Draft
> **Author**: user + ux-designer
> **Last Updated**: 2026-11-12
> **Template**: Interaction Pattern Library

---

## Overview

Ana menü ve ileride oyun içi duraklatma menüsü için ortak etkileşim sözleşmesi.
Girdi: klavye/fare birincil, gamepad kısmi, dokunmatik yok. Gezinme UGUI `EventSystem`
+ Input System UI modülüyle yapılır. Erişilebilirlik Basic varsayımıyla ele alınır
(taahhütlü `design/accessibility-requirements.md` yok).

---

## Pattern Catalog

| # | Pattern | Category | Used In |
|---|---|---|---|
| P1 | Vertical Menu List | Navigation | Main menu, Settings |
| P2 | Menu Button States | Input | All buttons |
| P3 | Settings Row: Slider | Input | Settings (volume, sensitivity) |
| P4 | Settings Row: Cycle/Toggle | Input | Settings (window mode, resolution) |
| P5 | Confirm Dialog | Modal | Quit confirmation |
| P6 | Back / Cancel | Navigation | Settings, dialogs |
| P7 | Screen Transition | Feedback | Menu → game, panel changes |
| P8 | UI Audio Feedback | Feedback | All menus |
| P9 | Announcement Banner | Feedback | Combat HUD (top centre) |
| P10 | Armed Confirm Card | Modal | Combat HUD true-death card |
| P11 | Low-State Warning Chip | Feedback | Combat HUD (health, ammo, Araf warning) |

---

## Patterns

### P1 Vertical Menu List

**Category**: Navigation
**Used In**: Main menu, Settings

**Description**: Dikey liste, tek odaklı öğe. Ana gezinme deseni.

**Specification**:
- Tab / Shift+Tab okuma sırasına göre sonraki/önceki öğeye geçer; liste sonunda başa sarar.
- ↑/↓, W/S, d-pad/sol çubuk gezinir; liste sonunda başa sarar. Fare hover'ı odağı o öğeye taşır. Enter/Space/A/sol tık onaylar.
- Açılışta ilk öğe odaklıdır; klavye/gamepad ile gelen oyuncu tıklamadan gezinebilir. Menü sahnesinde imleç serbest (`Cursor.lockState = None`).
- Odak göstergesi renkten bağımsız: öğenin yanında işaret (sigil) + parlaklık/ölçek farkı.

**When to Use**: 2-7 seçenekli ana gezinme.
**When NOT to Use**: Çok sayıda öğe (kaydırmalı liste ayrı desen, şimdilik gerek yok).

### P2 Menu Button States

**Category**: Input
**Used In**: All buttons

**Description**: Buton durumları: Normal, Focus (= Hover), Pressed, Disabled.

**Specification**:
- Hover ve klavye/gamepad odağı aynı görünümdür (tek "focus" durumu).
- Disabled: görünür kalır, gri, odak almaz, onay sesi çalmaz. Nedeni etiketle belirtilir (ör. "Devam Et — yakında").
- Pressed: ~80 ms görsel tepki, sonra eylem. Eylem sonrası kısa girdi kilidi çift tetiklemeyi önler.
- Durumlar rengin yanı sıra şekil/ışıkla da ayırt edilir.

**When to Use**: Her tıklanabilir menü öğesi.
**When NOT to Use**: Slider ve cycle satırları (P3/P4 kendi durumlarını taşır).

### P3 Settings Row: Slider

**Category**: Input
**Used In**: Settings (volume, sensitivity)

**Description**: Sürekli değerli ayar satırı.

**Specification**:
- Etiket solda, değer sağda (yüzde). ←/→ ile %5 adım, basılı tutunca hızlanır; fare ile sürükleme. Onay gerekmez, değer anında uygulanır.
- Ses slider'ında değişim sırasında kısa örnek ses çalar.
- Sınırda (0/100) sadece durur; ses veya titreşim yok.

**When to Use**: Sürekli aralıklı değerler.
**When NOT to Use**: Birkaç ayrık seçenek (P4).

### P4 Settings Row: Cycle/Toggle

**Category**: Input
**Used In**: Settings (window mode, resolution)

**Description**: Ayrık seçenekler arasında dönen ayar satırı.

**Specification**:
- ←/→ veya Enter seçenekler arasında döner; fare ile oklara tık. Mevcut değer her zaman metinle görünür (ikon tek başına yetmez).
- Her çözünürlük ve her pencere modu değişiminde 10 sn "Onayla / Geri al" sayacı (P5 ile). Varsayılan: geri al.

**When to Use**: 2-6 ayrık seçenek, açık/kapalı anahtarlar.
**When NOT to Use**: Sürekli değerler (P3).

### P5 Confirm Dialog

**Category**: Modal
**Used In**: Quit confirmation

**Description**: Geri alınamaz eylemler için onay penceresi.

**Specification**:
- Arka plan kararır, odak diyalog içinde hapsolur, arkadaki menü girdi almaz.
- Varsayılan odak yıkıcı olmayan seçenekte ("Hayır" / "Vazgeç"). Esc/B = Hayır (P6).

**When to Use**: Çıkış, geri alınamaz eylemler.
**When NOT to Use**: Zararsız eylemler (Başla onay istemez).

### P6 Back / Cancel

**Category**: Navigation
**Used In**: Settings, dialogs

**Description**: Tek tutarlı "geri" girdisi.

**Specification**:
- Esc / gamepad B / fare sağ tık bir üst ekrana döner; en üst ekranda (ana menü) hiçbir şey yapmaz (kazara çıkış yok).
- Ayarlar'dan çıkışta değişiklikler zaten uygulanmış ve kaydedilmiş sayılır.
- Ekranda her zaman görünür bir "Geri" butonu da bulunur (fare kullanıcısı için).

**When to Use**: Her alt ekran ve modal.
**When NOT to Use**: Ana menü kökü.

### P7 Screen Transition

**Category**: Feedback
**Used In**: Menu → game, panel changes

**Description**: Ekranlar arası geçiş davranışı.

**Specification**:
- Paneller arası 0.15-0.25 sn çapraz fade. Menü → oyun: siyaha 0.6 sn karartma, sahne yüklenirken girdi kilitli, yükleme bitince oyun fade-in.
- "Hareket azalt" açıkken kayma/ölçek animasyonları kapanır; anlık geçiş veya çok kısa fade kalır.
- Tüm geçişler `Time.unscaledDeltaTime` kullanır (hitstop/timeScale menüyü etkilemez). Geçiş sırasında çift girdi engellenir.

**When to Use**: Her ekran/panel değişimi.
**When NOT to Use**: Odak değişimi (anlık).

### P8 UI Audio Feedback

**Category**: Feedback
**Used In**: All menus

**Description**: Menü sesleri.

**Specification**:
- Olaylar: Focus değişimi, Onay, Geri, Hata/Disabled, Slider adımı. Sesler ses-olay sistemi üzerinden tetiklenir; UI kodu doğrudan `AudioSource` çalmaz.
- Aynı olay en fazla ~50 ms'de bir çalar (hızlı gezinmede üst üste binmez).
- Her sesli geri bildirimin görsel karşılığı vardır; ses tek başına bilgi taşımaz.

**When to Use**: Her etkileşimli menü öğesi.
**When NOT to Use**: Pasif/dekoratif öğeler.

### P9 Announcement Banner

**Category**: Feedback
**Used In**: Combat HUD (top centre)

**Description**: Oyun akışındaki olayları (ARENA MÜHÜRLENDİ, DALGA n, DALGA n TEMİZLENDİ, ARENA TEMİZLENDİ) kısa süreliğine duyuran, girdi almayan tek slotlu bant. Kaynak: `design/ux/hud.md` §8, `HudBannerQueue`.

**Specification**:
- Ekranın %12-20 yüksekliğinde, merkezdeki %40'lık boş alanın dışında; plaka 600×60, iki satır birleşince 600×96.
- Tek gösterimdeki + tek bekleyen slot (büyüyen kuyruk yok). Öncelik: ARENA TEMİZLENDİ (1, 2.2 sn, kesilemez) > MÜHÜRLENDİ / DALGA n (2, 1.8 sn) > DALGA TEMİZLENDİ (3, 1.8 sn). Daha önemli olan hemen değiştirir; eşit/düşük olan bekler.
- Birleştirme penceresi (varsayılan 0.5 sn, `HudConfig`) içinde MÜHÜRLENDİ + DALGA 1 iki satırda birleşir; eskimiş bekleyen mesaj (daha yeni bir dalgadan önceki) atılır.
- 150 ms giriş / 300 ms çıkış, ölçeksiz zaman. Hareket azaltma açıkken anlık geçiş.
- Bilgi taşıyıcı değil, tekrar: aynı bilgi kalıcı dalga bloğunda da görünür, bu yüzden otomatik kapanması kabul edilir. Gerçek ölüm ve sahne değişimi bantı temizler.

**When to Use**: Oyuncunun eyleme geçmesi gerekmeyen, kalıcı bir göstergede de yansıyan durum değişiklikleri.
**When NOT to Use**: Eylem gerektiren bilgi (P10 kullan) veya sürekli bir durum (P11 ya da kalıcı satır kullan).

### P10 Armed Confirm Card

**Category**: Modal
**Used In**: Combat HUD true-death card (`GERÇEK ÖLÜM` + `TEKRAR DENE`)

**Description**: Oyun içi bir anda açılan, tek onay butonlu tam ekran kart. Oyuncunun o sırada basılı tuttuğu ya da arka arkaya bastığı tuşlar onu kazara tetiklememeli. Kaynak: `TrueDeathView`, `TrueDeathGate`.

**Specification**:
- Yalnızca kendi olayıyla açılır (`RunEvents.OnTrueDeath`); Araf'a geçişte kart yoktur.
- Açılınca imleç serbest ve görünür, buton odaklı (P1 odak işareti). Geri/İptal davranışı yoktur.
- **Arming:** onay, fade-in bitene ve en az `HudConfig.deathArmSeconds` (varsayılan 0.6 sn, ölçeksiz) geçene kadar yok sayılır. Ayrıca her onay girdisi (Enter, Space, gamepad A, sol tık) kart açıldıktan sonra en az bir kez bırakılmış olmalıdır.
- Onay 80 ms basma geri bildirimi verir ve isteği tam bir kez yollar (`HudRequests.RequestNewRun`); sonra buton kilitlenir. HUD oyun durumunu kendisi değiştirmez.
- Hareket azaltma: arming aynen kalır, sadece basma kayması kalkar.

**When to Use**: Oyun sırasında, oyuncunun eli kontroller üzerindeyken açılan ve geri alınamaz bir eylemi onaylatan ekranlar.
**When NOT to Use**: Menüden açılan diyaloglar (P5 yeterli).

### P11 Low-State Warning Chip

**Category**: Feedback
**Used In**: Combat HUD — düşük can (≤ %30), düşük/boş cephane, Araf uyarısı

**Description**: Kritik bir kaynak ya da durum için renkten bağımsız uyarı.

**Specification**:
- Opak `#FF4D5E` chip üzerinde isli siyah metin/ikon; arka plan oyun sahnesinden bağımsız olduğu için kontrast korunur (ölçülen: Araf uyarısı 4.77:1, "!" ikonu 3.22:1 grafik eşiği).
- Renk tek başına bilgi taşımaz: `!` ikonu + kalın çerçeve + sayı/metin birlikte.
- Eşikler veriden (`HudConfig`): düşük can tek eşik %30, vinyet de aynı değeri okur.
- Nabız en fazla 1 Hz; hareket azaltma açıkken sabit çerçeve.

**When to Use**: Oyuncunun hemen tepki vermesi gereken kaynak durumları.
**When NOT to Use**: Kalıcı bir hedef ya da bilgi satırı için (Phase 4 review'una göre Araf uyarısının bu deseni kullanması tartışmalı; daha sakin bir satır stiline geçirilmesi açık bir soru).

---

## Gaps & Patterns Needed

- Kaydırmalı liste, tuş atama (rebind) satırı, tooltip, toast/bildirim
- Duraklatma menüsü girişi
- Ruh cevheri, lanet/lütuf eşyası ve boss barı HUD desenleri (ertelendi)

---

## Open Questions

- `design/player-journey.md` yok: menü bağlamı varsayıma dayanıyor.
- Erişilebilirlik seviyesi taahhüt edilmedi (`/ux-design accessibility`).
- Ses olay sisteminin UI karşılığı henüz yok (P8 uygulanırken tanımlanacak).
- Yerelleştirme: mevcut HUD'lar Türkçe, çok dilli yapı planı belirsiz.
