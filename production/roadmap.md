# PURGATOR — Yol Haritası ve Üretim Planı

- **Tarih:** 2026-11-17
- **Durum:** Taslak. Kapsam önerisi kullanıcı tarafından dosyaya yazılmak üzere onaylandı; aşağıdaki "Açık kararlar" henüz verilmedi.
- **Kaynak:** Üç salt okunur Codex (gpt-6.1-sol) taraması: tasarım vault'u (`documentation/retro-fps`, 28 not), Unity projesi (`Graduation Project/`), üretim belgeleri ve git geçmişi. Taramalar statiktir; Unity çalıştırılmadı, test koşulmadı.
- **Takvim uyarısı:** Hafta tarihleri tahminidir. Vize notu ("5 gün sonra", 2026-11-15) 20 Kasım'ı gösteriyor; W10–W15 buna göre hesaplandı. Kesin jüri tarihi hiçbir belgede yok.
- **Akademik takvim kaynağı:** `GP1_HW_W05_MileStone_NAME.docx` (W06–W15).

## 1. Özet

Mekanik tarafı güçlü, ama baştan sona oynanıp bitirilebilen bir oyun henüz yok. Kalan yaklaşık 6 haftanın işi yeni sistem değil; içerik, görsel/ses, elle test ve Windows build.

## 2. Mevcut durum

| Alan | Durum |
|---|---|
| Silahlar | 3: Tekrarlı Arbalet, İnfazcı Kılıcı, Gotik Pompalı (+ Meat Hook); Karşı Hüküm parry (Q/MMB) |
| Düşmanlar | 3: Ghoul Stalker, Nişancı Göz, Kefaretçi; elit varyantlar; eğitim kuklası |
| Boss | 1 prototip: Yargıç (2 faz, diz çökme bitirici) |
| Hareket | Quake tarzı air-strafe, bunny-hop, slide, 3 şarjlı dash |
| Koşu | Ölüm → Araf labirenti (9×9, 7 oda yuvası) → dönüş; koşu başına 1 Araf hakkı; seed'li rastgelelik |
| Araf içeriği | 6 oda tanımı, 6 kart, sunak, elit düşmanlar |
| Arayüz | Ana menü, savaş HUD'u, boss barı, pause, ortak ayarlar paneli, EN/TR canlı dil değişimi |
| Kod | 218 C# dosyası, 7 asmdef, 34 prefab, 5 WaveSet |
| Test | 64 EditMode test dosyası (son kayıt 803/803); PlayMode testi yok; CI yok |
| Sahneler | MainMenu (0), SampleScene (1), TestLevel_Gauntlet (2), Araf (3); `mert_level` build'de değil |
| Kanıt | `production/qa/evidence/` altında 83 ekran görüntüsü |

## 3. Eksikler

### 3.1 Oyun bütünlüğü
- Zafer akışı yok: Yargıç ölünce `onBossDefeated` boş, `RunPhase` içinde zafer durumu yok.
- Kat geçişi yok; `RunState` içinde kat indeksi alanı var ama çalışan bir tırmanış yok.
- "Kule" tek bir test seviyesi (Gauntlet: 3 oda, 4 koridor, boss arenası).
- Öğretici, koşu özeti, jenerik, bitiş sunumu yok.
- Tasarımda olup kodda olmayanlar: ruh taşı ve kalıcı yükseltmeler, aile boss kadrosu, hikâye/diyalog, silah loot'u, kayıt sistemi (yalnızca ayarlar PlayerPrefs'te).

### 3.2 Elle test
- Bütün doğrulamalar script ile yapıldı. Gerçek fare/klavye girişi, his ve denge ayarı her özellikte açık: kılıç, parry, Göz, Kefaretçi, kartlar, boss, pause.
- Resmî playtest raporu yok.

### 3.3 Görsel
- Düşmanlar, boss, kılıç mermisi ve oyuncu silahları Unity primitive mesh'leri.
- İçe aktarılmış tek model: `Level_Blackout.fbx`. Animasyon klibi ve Animator controller sıfır.
- 16 viewmodel materyal slotu boş (pembe).
- UI görselleri üretilmiş placeholder (27 PNG). Araf odaları küp tabanlı.
- Projeye özel shader ya da renderer feature yok; özel VFX prefab'ı yok.
- Sanat kitabı (art bible) yok.

### 3.4 Ses
- İçe aktarılmış ses klibi 0, müzik 0, AudioMixer 0.
- 26 sentezlenmiş efekt var (`CombatAudio`).
- Menü ses slotları atanmamış; müzik ayarı işlevsiz; SFX ayarı bazı kaynakları kapsamıyor.
- Kılıç, Kefaretçi ve Göz'ün ağır atış şarjı için özel ses yok.

### 3.5 Build ve yayın
- Windows build hiç kanıtlanmadı.
- Ürün adı "Graduation Project", şirket "DefaultCompany", ikon ve özel splash yok, uygulama kimliği şablon değeri.
- Sayısal performans bütçesi tanımlı değil (yalnızca W14 için "60+ FPS").

### 3.6 Bilinen hatalar
| # | Hata | Kaynak |
|---|---|---|
| B1 | Yargıç, Gauntlet'te her yeniden başlatmadan ~3 sn sonra oyuncuyu öldürüyor | `active.md` (kartlar) |
| B2 | Boss, oyuncu etrafında dönünce arena duvarına sıkışıyor | `active.md` (boss tur 4) |
| B3 | Gauntlet'te 5 spawn noktası geometrinin içinde | vault `WaveAndEncounterArchitecture.md` |
| B4 | `BasicEnemyAI.TakeDamage` spawn karesinde hasar alırsa NRE | `active.md` |
| B5 | Q spam'i ghoul'ları tek başına sersemletiyor olabilir (stunlock), CRIT 34 gösterimi | `active.md` (parry) |
| B6 | Gib'ler CharacterController'ı blokluyor, slam düşüşünü kısaltıyor | `active.md` (kılıç) |
| B7 | Yargıç kılıç mermileri pool'lanmıyor (`JudgeBossAI.cs:913` TODO) | kod |
| B8 | EditMode koşuları `SampleScene.unity` ve `Enemy_Ghoul_Stalker.prefab` dosyalarını yeniden yazıyor | `active.md` |
| B9 | Bir Ghoul, EncounterCleared sonrası saldırı jetonunu tutmuş (tek gözlem) | `active.md` |
| B10 | Sahne yeniden yüklenince HUD, faz Kule iken Araf metni gösterdi (incelenmedi) | `active.md` |
| B11 | Boss devre dışı kalınca boss barı ekranda kalıyor | `active.md` |
| B12 | Gauntlet'te boss odası kapıları yok | `active.md` |
| B13 | Elle yerleştirilen muhafızlar retry'da geri gelmiyor | vault |
| B14 | Hasar sayısı/VFX yolunda vuruş başına ~30 B tahsis (sıfır GC kuralına aykırı) | `active.md` |
| B15 | Saptırılan mermi collider içinde başlarsa tünelleyebiliyor | `active.md` (Göz) |

### 3.7 Mimari kural sapmaları
- 6 `OnGUI` metodu (hasar sayıları, hasar geri bildirimi, debug HUD, eski AmmoHUD/RunHUD, nişangâh).
- Public alanlar: `WaveSpawnDirector.cs:26–38`, `EnemyProjectileSettings.cs`, `ParryVFX.cs`, `PlayerDamageFeedback.cs:60–63`.
- 5 tekil `Physics.Raycast` çağrısı (`EncounterArea`, `HealthOrb`, `WaveSpawnDirector`).
- `[Obsolete]` sınıflar: `AmmoHUD`, `RunHUD` (`mert_level` kullandığı için duruyor).

### 3.8 Erişilebilirlik
- `design/accessibility-requirements.md` "Standard" seviyesini taahhüt ediyor; tuş atama, UI ölçekleme ve renk körlüğü desteği başlanmadı.

### 3.9 Belge ve depo düzeni
- Vault'ta eski/çelişkili değerler: Arbalet 28 hasar / 22 hasar; kılıç diyagramı 45/55/95 / güncel 34/42/96; bitirici stun 1.2 sn / 1.0 sn; "boss yok" ibaresi; `ProjectOverview.md` son güncelleme 4 Kasım.
- `production/session-state/active.md` birçok yerde "commit edilmedi" diyor; işler main'de (`bdc39d7`).
- Çalışma ağacında 5 değişmiş dosya (2 sahne, 1 materyal, 2 düşman prefab'ı) ve izlenmeyen klasörler.
- Çöp: `Graduation Project/AgentScripts/` (47 dosya), `Assets/_Project/Scripts/production/session-logs/`, `Assets/_Project/Tests/.claude/`, `production/qa/evidence$1`.
- Eksik üretim belgeleri: oyun özeti (brief), sprint/hikâye kaydı, risk kaydı, QA planı, playtest raporu, yayın kontrol listesi, changelog.
- Oyun adı tutarsız: akademik belgede "PURGATORY'S ASCENT", oyunda "PURGATOR".
- `mert_level.unity` 3.59 MB, Gauntlet 1.85 MB: merge çakışması riski.

## 4. Final kapsam

### 4.1 Olacak (jüri build'i)
15–20 dakikalık tek bir koşu:

1. Öğretici giriş (hareket, dash, parry, kaynak döngüsü).
2. Kat 1: 3 savaş odası (Ghoul + Göz).
3. Kat 2: 3 savaş odası (Kefaretçi burada tanıtılır, karışık dalgalar).
4. Yargıç boss'u.
5. Zafer ekranı, koşu özeti, jenerik.

Ek olarak:
- Koşu başına 1 Araf hakkı; kart sayısı 6'dan 10–12'ye.
- 3 silah, 3 düşman ve elitler, 1 boss. Yeni mekanik eklenmez.
- 3 düşman, boss ve 3 silah için gerçek low-poly model ve basit animasyon.
- 4 müzik (menü, savaş, Araf, boss); kilit efektler için gerçek klipler; AudioMixer.
- Windows build, 60+ FPS, EN/TR.
- Erişilebilirlik hedefi "Basic"e indirilir (karar gerekir, bkz. bölüm 8).

### 4.2 Zaman kalırsa
- 4. düşman tipi.
- Hafif kalıcı ilerleme: 3 yükseltme (can, dash şarjı, parry açısı).
- Kule tarafında kart kaynağı.
- Tuş atama ekranı.
- Araf mini haritası.

### 4.3 Kesilecek
- Diğer aile boss'ları (lore metninde anılır).
- Ekstra oyun modları.
- Silah loot'u.
- Koşu kaydetme / devam etme.
- Addressables.
- Renk körlüğü paleti, UI ölçekleme.

## 5. Haftalık plan

| Hafta | Tarih (tahmini) | Hedef | Çıkış ölçütü |
|---|---|---|---|
| W09 | 16–22 Kas | Vize: gösterilebilir döngü | Menü → Gauntlet → ölüm → Araf → kart → dönüş → boss elle, kesintisiz oynanıyor |
| W10 | 23–29 Kas | Kat 1–2 ve bitiş | Koşu baştan sona bitirilebiliyor (placeholder görsellerle) |
| W11 | 30 Kas–6 Ara | Modeller ve UI | Primitive düşman/silah kalmadı; öğretici ve jenerik var |
| W12 | 7–13 Ara | Özellik dondurma, alfa | Ses/müzik bağlı; ilk Windows build çalışıyor |
| W13 | 14–20 Ara | Kör test (5 öğrenci) | Playtest raporu yazıldı; düzeltme listesi önceliklendirildi |
| W14 | 21–27 Ara | Paketleme | 60+ FPS ölçüldü; ikon/isim/splash; slaytlar hazır |
| W15 | 28 Ara–3 Oca | Jüri | Yedek build ve oynanış videosu hazır |

## 6. Ayrıntılı yapılacaklar

### W09 — Vize
- [ ] B1: Yargıç'ın yeniden başlatma sonrası oyuncuyu öldürmesini düzelt.
- [ ] Demo akışı için başlangıç konumu kararı (oyuncu şu an boss arenasında başlıyor; bilinçli test ayarı).
- [ ] Elle test turu (Hamza): kılıç kombo/lunge/slam, parry, Göz, Kefaretçi, kart seçimi, pause, boss. Bulguları not al.
- [ ] B5: Q spam stunlock'unu elle doğrula.
- [ ] Çalışma ağacındaki 5 değişmiş dosya için karar: commit ya da geri al.
- [ ] Çöp klasörleri sil (silme işlemleri ajanlara kapalı; kullanıcı yapar).
- [ ] `active.md` dosyasını şablondan yeniden oluştur (eski "commit edilmedi" kayıtları).

### W10 — Kat 1–2 ve bitiş
- [ ] Kat geçişi: `RunState` kat indeksi, kat sonu kapısı/portalı, sonraki katın yüklenmesi.
- [ ] `RunPhase` zafer durumu; `onBossDefeated` bağlantısı.
- [ ] Zafer ekranı ve koşu özeti (süre, öldürme, alınan kartlar, Araf kullanımı). Vault'taki "Faz 4" maddesi.
- [ ] `mert_level` içeriğini prefab odalara böl; yeni HUD'a geçir; build'e al.
- [ ] Kat 1 ve Kat 2 için WaveSet'ler (Kefaretçi Kat 2'de).
- [ ] B3, B12, B13: spawn noktaları, boss kapıları, muhafız retry'ı.
- [ ] B4, B9, B10, B11.
- [ ] `design/game-brief.md`: tek sayfalık oyun özeti (final kapsam buradan).

### W11 — Modeller ve UI
- [ ] Ghoul, Göz, Kefaretçi, Yargıç modelleri; `Visual/Model` yuvalarına bağla (builder'lar bunu destekliyor).
- [ ] Arbalet, kılıç, pompalı viewmodel'leri; pembe materyal slotları.
- [ ] Animasyon: en az saldırı telgrafı, yürüme, ölüm.
- [ ] Öğretici giriş odası.
- [ ] Jenerik ekranı.
- [ ] 4–6 yeni kart.
- [ ] UI placeholder görsellerini değiştir (HUD ikonları, menü arka planı).
- [ ] Araf oda görselleri (küplerin yerine modüler kit).

### W12 — Özellik dondurma, alfa
- [ ] AudioMixer (Master / Müzik / SFX / UI); ayar kaydırıcılarını bağla.
- [ ] 4 müzik parçası ve geçişleri.
- [ ] Gerçek klipler: silah atışları, kılıç, parry, düşman telgrafları, boss saldırıları, menü.
- [ ] Denge turu: silah, düşman, kart sayıları; vault değerlerini eşitle.
- [ ] B2, B6, B7, B14, B15.
- [ ] İlk Windows build; build'de GC ve kare süresi ölçümü.
- [ ] Bu tarihten sonra yeni özellik yok.

### W13 — Kör test
- [ ] 5 dış öğrenciyle test; `production/qa/` altına playtest raporu.
- [ ] Yalnızca testten çıkan düzeltmeler (öğretici açıklığı, zorluk, okunabilirlik).
- [ ] Zaman kalırsa listesinden en fazla bir madde.

### W14 — Paketleme
- [ ] Profil: 60+ FPS, sıfır GC sıcak yollar.
- [ ] Ürün adı, şirket adı, ikon, splash, sürüm numarası.
- [ ] Yayın kontrol listesi.
- [ ] Savunma slaytları; vault'un son güncellemesi.

### W15 — Jüri
- [ ] Yalnızca kritik hata düzeltmesi.
- [ ] Yedek build (ikinci makinede denenmiş) ve oynanış videosu.

## 7. İş bölümü

| Kim | İş |
|---|---|
| Hamza | Elle test ve his ayarı, denge, model/animasyon kararları, oynanış sistemleri yönü |
| Mert | Kat 1–2 yerleşimi, modüler çevre, ışık |
| Codex sol / astra | Kat geçişi, zafer akışı, koşu özeti, kartlar, hata düzeltmeleri (net şartnameyle) |
| Claude Sonnet | Unity'de derleme, test, Play Mode doğrulama, ekran görüntüsü kanıtı |
| Gemini Hydrogen | Yazandan bağımsız kod incelemesi |
| Gemini Flash | Vault'taki eski değerlerin düzeltilmesi, belge güncellemeleri |

Vault'taki rol ayrımı (Geliştirici 1: model, düşman, silah, seviye; Geliştirici 2: UI, ses, akış) ile git geçmişi uyuşmuyor: HazmaK oynanış, sistemler ve UI'ı; Crow361 seviye ve arazi işlerini yapmış. Tablo git geçmişine göre yazıldı.

## 8. Açık kararlar

1. **Jüri tarihi:** W15 kesin olarak hangi gün?
2. **Modeller:** elde modelleme mi, hazır paket mi? W11'in tamamı buna bağlı.
3. **Kalıcı ilerleme (ruh taşı):** tasarımın temel vaadi; öneri "zaman kalırsa" listesi, çünkü kayıt sistemi gerektiriyor.
4. **Müzik:** akademik takvim "özgün ses/müzik" istiyor; kim üretecek?
5. **Erişilebilirlik:** "Standard" taahhüdü "Basic"e indirilsin mi?
6. **Oyun adı:** PURGATOR mu, PURGATORY'S ASCENT mi?

## 9. Riskler

| Risk | Etki | Önlem |
|---|---|---|
| Modeller W11'e yetişmez | Jüride primitive düşmanlar | Karar 2'yi bu hafta ver; hazır paket yedeği |
| Elle test geç başlar, his sorunları W13'te çıkar | Denge ve akış yeniden işlenir | W09'da ilk elle tur; her özellik sonrası 10 dk elle oynama |
| Sahne merge çakışmaları | Kayıp iş | Odaları prefab'a böl; sahneye tek kişi dokunur |
| Kapsam büyümesi | Bitmemiş oyun | W12 özellik dondurma; bölüm 4.3 listesi kapalı |
| Windows build ilk kez W12'de denenir | Geç çıkan build hataları | W10 sonunda deneme build'i |
| Ajan doğrulamaları script tabanlı | Gerçek girişte çalışmayan yollar | Kanıt ekran görüntüsüne ek elle onay |
