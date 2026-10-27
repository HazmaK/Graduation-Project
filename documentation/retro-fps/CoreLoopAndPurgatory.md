---
project: "retro-fps"
type: "GameMechanic"
tags:
  - project
  - memory
  - "retro-fps"
  - gameloop
  - roguelite
  - purgatory
  - progression
---

# CoreLoopAndPurgatory (Temel Oyun Döngüsü, Kule Tırmanışı ve Araf Mekaniği)

> [!abstract] ROGUE-LITE İLERLEME VE İKİNCİ ŞANS MEKANİĞİ
> Dikey gotik kule katlarında arena savaşları, lanetli aile üyeleriyle boss arındırma karşılaşmaları, can sıfırlandığında düşülen prosedürel Araf (Purgatory) labirenti ve gerçek kalıcı ölüm (True Permadeath).

---

## 1. DÖNGÜ VE YAŞAM AKIŞI

```mermaid
flowchart TD
    Baslangic["Kule Girişi (Zemin Kat)"] --> Katlar["Kat Tırmanışı & Karşılaşmalar\nEncounterArea / WaveSpawnDirector"]
    Katlar --> Boss["Kat Sonu: Lanetli Aile Üyesi Boss Savaşı"]
    Boss -->|Zafer| SonrakiKat["Sonraki Kat (Kule Zirvesine Doğru)"]
    
    Katlar & Boss -->|Can Sıfırlandı (PlayerHealth <= 0)| ArafGecisi["Araf'a Düşüş (Purgatory Labyrinth)\nİkinci Şans"]
    
    subgraph ARAF_LABIRENTI["Kemik Duvarlı Araf Labirenti"]
        ElitDusmanlar["Yüksek Hasarlı Elit Yaratıklar"]
        CursedLoots["Çift Taraflı Lanetli Eşyalar (Curse/Blessing)"]
        EscapePortal["Araf Kaçış Portalı"]
    end
    
    ArafGecisi --> ARAF_LABIRENTI
    EscapePortal -->|Başarıyla Kaçış| Katlar
    
    ARAF_LABIRENTI -->|Arafta İkinci Ölüm| TrueDeath["Kalıcı Ölüm (True Permadeath)\nTüm Silahlar & Geçici Eşyalar Silinir"]
    TrueDeath --> MetaProg["Kalıcı Pasif Ruh Geliştirmeleri (Meta-Progression)"]
    MetaProg --> Baslangic
```

---

## 2. KİLİT MEKANİK KURALLARI VE DİNAMİKLER

### 1. Dikey Kule Tırmanışı (Tower Ascent)
- **Yapı:** Her kat gotik mimariye sahip bir veya birkaç `EncounterArea` odasından oluşur.
- **Karşılaşma Mantığı:** Oyuncu odaya girdiğinde barikatlar kapanır, `WaveSpawnDirector` dalgaları başlatır; düşmanlar temizlenince kapılar açılır ve kat asansörüne / portalına geçiş verilir.
- **Boss Arındırma Savaşları:** Her bölümün sonunda kuleye hapsedilmiş ve canavara dönüşmüş gotik bir aile ferdiyle arındırma düellosu gerçekleşir.

### 2. Araf (Purgatory / İkinci Şans) Mekaniği
- **Tetiklenme:** Kuledeyken `PlayerHealth` sıfıra ulaştığında oyun anında "Game Over" ekranı vermez; karakter karanlık bir girdapla kemik duvarlı Araf labirentine çekilir.
- **Risk & Ödül:**
  * Düşmanlar kuledekilere göre %40 daha agresiftir ve yüksek hasar verir.
  * Labirentte bulunan eşyalar çift taraflıdır (örn. "+%50 Saldırı Hızı ancak -%30 Maksimum Can").
- **Kuleye Dönüş:** Labirentin merkezindeki kan portalına ulaşabilen oyuncu, kulede öldüğü kata kazandığı lanetli ganimetlerle canlı olarak geri döner.

### 3. Gerçek Kalıcı Ölüm (True Permadeath)
- **Koşul:** Oyuncu Araf labirentinde de canını sıfırlarsa ikinci bir şans verilmez.
- **Sıfırlama:** Mevcut koşudaki (run) tüm silahlar, cephaneler ve geçici güçlendirmeler silinir; kule sıfırlanır.
- **Kalıcı İlerleme (Meta-Progression):** Yalnızca kule boyunca toplanan arındırılmış ruh cevherleri korunur; bu cevherlerle kule girişindeki gotik sunakta kalıcı stat geliştirmeleri (Taban Can, Dash Dolum Hızı, Parry Kabul Açısı vb.) açılır.

---

## 3. BAŞKA BİR AI İÇİN MİMARİ ENTEGRASYON SÖZLEŞMESİ
1. `PlayerHealth.cs` sınıfında ölüm gerçekleştiğinde doğrudan sahne yüklemesi (`SceneManager.LoadScene`) yapmayın; `OnDied` olayını tetikleyerek oyun yöneticisinin Araf durumunu kontrol etmesine izin verin.
2. Araf labirenti mekaniği eklenirken `IEncounterEnemy` arayüzünü uygulayan elit düşmanlar kullanılmalıdır.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[WaveAndEncounterArchitecture]] ➔ `manages_tower_rooms`
- [[PlayerSubsystemArchitecture]] ➔ `monitors_player_death`
- [[CombatAndDamageInterfaces]] ➔ `evaluates_combat_stakes`
- ⬅️ [[ProjectOverview]] ➔ `features_mechanic` *(gelen)*
- ⬅️ [[START_HERE_AI_CONSTITUTION]] ➔ `mandated_by_constitution` *(gelen)*
