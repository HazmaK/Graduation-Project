---
project: "retro-fps"
type: "SystemConstitution"
tags:
  - project
  - memory
  - "retro-fps"
  - ai-constitution
  - rules
  - architecture
---

# START_HERE_AI_CONSTITUTION (AI Geliştirici Anayasası)

> [!danger] TÜM YAPAY ZEKÂ VE GELİŞTİRİCİLER İÇİN ZORUNLU OKUMA
> Bu projeye bağlanan **herhangi bir yapay zekâ modeli** (Antigravity, Cursor, Claude, ChatGPT, Copilot vb.) veya insan geliştirici kod yazmadan veya düzenlemeden önce bu anayasadaki kuralları **istisnasız** uygulamak zorundadır.
> Bu kuralların dışına çıkmak performans kaybına (GC Spike), spagetti koda ve Git çakışmalarına (Merge Conflict) yol açar.

---

## 1. MİMARİ VE PLATFORM ÇERÇEVESİ
- **Oyun Motoru:** Unity 6 (6000.x)
- **Render Pipeline:** Universal Render Pipeline (URP)
- **Girdi Sistemi (Input):** Yalnızca New Input System (`UnityEngine.InputSystem`). Eski `Input.GetKeyDown/GetAxis` kullanımı KESİNLİKLE YASAKTIR.
- **Hedef:** Hızlı tempolu, retro boomer shooter fiziğine sahip (Quake/ULTRAKILL), modüler ve yüksek performanslı mimari.

---

## 2. KOD YAZIM VE PERFORMANS KURALLARI (ZERO ALLOCATION)
1. **Sıfır GC (Garbage Collection) Toleransı:**
   - Sıcak yollarda (`Update`, `FixedUpdate`, `LateUpdate`) ASLA heap tahsisi (`new`, string birleştirme, LINQ) yapılamaz.
   - `GetComponent<T>()`, `FindAnyObjectByType<T>()` gibi aramalar `Update` içinde ASLA çağrılamaz. Tüm referanslar `Awake()` veya `OnEnable()` anında önbelleğe (cache) alınmalıdır.
   - Fizik sorgularında sıfır bellek ayıran `NonAlloc` versiyonları (örn. `Physics.RaycastNonAlloc`, `Physics.OverlapSphereNonAlloc`) kullanılmalıdır.
   - Geçici veri paketleri ve matematiksel yükler için C# `class` yerine `readonly struct` tercih edilmelidir.
2. **Kapsülleme (Encapsulation) ve Inspector Standartları:**
   - Bir değişkeni Inspector'da göstermek için ASLA `public` yapılmaz.
   - DAİMA `[SerializeField] private` kullanılır ve mantıklı varsayılan değerler atanır.
   - Dışarıdan salt okunur (read-only) erişim için public getter property tanımlanır: `public float Speed => speed;`.
3. **Eski (Deprecated) API Yasağı:**
   - `FindObjectOfType<T>()` yerine Unity 6 uyumlu `FindFirstObjectByType<T>()` veya `FindAnyObjectByType<T>()` kullanılmalıdır.
   - Gereksiz Coroutine zincirleri yerine modern timer yapıları, async/await veya UniTask kullanılmalıdır.

---

## 3. BAŞKA BİR AI DEĞİŞİKLİK YAPTIĞINDA BUG OLUŞMASINI ÖNLEYEN SÖZLEŞME
1. **Mevcut Çalışan Kodları Ezmeme (Non-Destructive Extension):**
   - Bir AI yeni bir mekanik eklerken, mevcut `BasicEnemyAI.cs`, `WeaponController.cs` veya `PlayerMovement.cs` içerisine 500 satırlık spagetti `if-else` blokları ekleyemez.
   - Yeni davranışlar **Arayüzler (Interfaces)** veya **Olaylar (C# Action Events)** üzerinden bağlanmalıdır:
     * Yeni düşman mı eklenecek? `IDamageable`, `IStunnable` ve `IEncounterEnemy` arayüzlerini uygulayan ayrı bir script yazılmalıdır.
     * Yeni silah mı eklenecek? `WeaponBase` sınıfından türetilen izole bir script yazılmalıdır.
2. **Ayrık Mimari (Decoupling):**
   - Sistem A, Sistem B'nin iç detaylarını doğrudan manipüle edemez.
   - İletişim C# delegeleri (`System.Action`, `System.Func`), ScriptableObject Event'leri veya paylaşılan arayüzler üzerinden yürütülür.

---

## 4. İKİ KİŞİLİK EKİP ÇALIŞMASI VE GIT PROTOKOLÜ
1. **Sahne (.unity) Dosyası Çakışması Yasağı:**
   - Ana sahne (`SampleScene.unity` veya `MainScene.unity`) ortak çalışma alanı DEĞİLDİR.
   - Her geliştirici kendi sandbox sahnesinde çalışır (örn: `Ali_Sandbox.unity`, `Veli_Sandbox.unity`).
   - Sahnede oluşturulan her varlık (Düşman, Silah, UI, Çevre) **Prefab** haline getirilir. Ana sahneye sadece tamamlanmış prefab'lar eklenir.
2. **Assembly Definitions (.asmdef):**
   - Kod tabanı fiziksel modüllere ayrılmalıdır (`Core`, `Player`, `Combat`, `Weapons`, `UI`).
   - Bir AI kod yazarken modül sınırlarına saygı duymak zorundadır.

---

## 5. SİNAPTİK BİLGİ MERKEZİ BAĞLANTILARI
Herhangi bir sisteme müdahale etmeden önce ilgili sinaptik düğümü mutlaka inceleyin:
- [[CodebaseDependencyGraph]] ➔ Tüm C# scriptlerinin bağımlılık ve olay haritası
- [[PlayerSubsystemArchitecture]] ➔ Oyuncu hareket, kamera, can ve parry mimarisi
- [[WeaponArsenalArchitecture]] ➔ Silah tabanı, viewmodel ve silah değiştirici
- [[EnemyAIAndSpawning]] ➔ FSM yapay zekâ, sürü alarmı ve düşman mermileri
- [[WaveAndEncounterArchitecture]] ➔ DOOM/Ultrakill tarzı dalga yöneticisi ve karşılaşmalar
- [[CombatAndDamageInterfaces]] ➔ Hasar, sersemletme ve karşılaşma arayüzleri
- [[HitstopAndAudioFeedback]] ➔ Vuruş tokluğu, sentetik ses ve hasar sayıları
- [[CombatVFXAndDecalSystem]] ➔ Kan lekesi, kor kıvılcım ve gore parçalanma
- [[CollaborationAndGitWorkflow]] ➔ İki kişilik ekip iş akışı ve Git kuralları

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[ProjectOverview]] ➔ `governs_architecture`
- [[CodebaseDependencyGraph]] ➔ `enforces_graph_integrity`
- [[CollaborationAndGitWorkflow]] ➔ `defines_workflow`
- ⬅️ [[MultiAgentSharedMemory]] ➔ `regulates_ai_agents` *(gelen)*
