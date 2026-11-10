---
project: "retro-fps"
type: "WorkflowProtocol"
tags:
  - project
  - memory
  - "retro-fps"
  - git
  - collaboration
  - multi-dev
  - workflow
---

# CollaborationAndGitWorkflow (İki Kişilik Ekip ve AI İşbirliği Protokolü)

> [!important] ÇAKIŞMASIZ (CONFLICT-FREE) VE SPAGETTİSİZ GELİŞTİRME PROTOKOLÜ
> İki geliştiricinin ve onların kullandığı yapay zekâ asistanlarının aynı projede birbirlerinin kodunu bozmadan, sahne çakışması (Merge Conflict) yaşamadan ve spagetti kod üretmeden çalışmasını sağlayan katı kurallar bütünü.

---

## 1. SORUMLULUK ALANLARI VE KLASÖR İZOLASYONU

| Geliştirici | Birincil Sorumluluk Alanı | İzin Verilen Klasörler | Dokunulmaması Gereken Klasörler |
| :--- | :--- | :--- | :--- |
| **Geliştirici 1 (Sen)** | 3D Modelleme, Düşman Tasarımı, Silah Fiziği, Seviye Tasarımı | `Assets/_Project/Models/`<br>`Assets/_Project/Prefabs/Enemies/`<br>`Assets/_Project/Scripts/Combat/`<br>`Assets/_Project/Scripts/Player/` | Arkadaşının geliştirdiği UI veya Ses sistemlerinin iç sınıfları |
| **Geliştirici 2 (Arkadaşın)** | UI Tasarımı, Ses Mimarisi, Ekstra Oyun Modları veya Bölüm Akışı | `Assets/_Project/Scripts/UI/`<br>`Assets/_Project/Audio/`<br>`Assets/_Project/Prefabs/UI/`<br>`Assets/_Project/Scenes/Friend_Sandbox.unity` | Düşman FSM sınıfları (`BasicEnemyAI`), Silah çekirdeği (`WeaponBase`), Oyuncu hareket fiziği |

---

## 2. UNITY SAHNE (.UNITY) ÇATIŞMALARINA KARŞI 3 ALTIN KURAL

Unity'de iki kişi aynı anda `SampleScene.unity` dosyasını değiştirirse Git bunu otomatik birleştiremez ve sahne bozulur. Bunu önlemek için:

1. **Ortak Sahneye Dokunma Yasağı:**
   - Ana sahne (`SampleScene.unity` veya `MainTower.unity`) bir kişinin kontrolünde olmalıdır.
   - Her geliştirici kendi sandbox sahnesinde çalışır:
     * `Assets/Scenes/Dev_User_Sandbox.unity`
     * `Assets/Scenes/Dev_Friend_Sandbox.unity`
2. **Her Şey Prefab'dır (Prefab-First Workflow):**
   - Sahnede yeni bir düşman, yeni bir tuzak, yeni bir kapı veya yeni bir UI tasarladığınızda, onu sahnede bırakmayın; `Assets/_Project/Prefabs/` altına **Prefab** olarak kaydedin.
   - Ana sahneye sadece tamamlanmış prefab'lar sürüklenip bırakılır.
3. **Prefab Değişikliklerinde Nested Prefab ve Variant:**
   - Bir prefab'ın ana yapısını bozmamak için üzerine sağ tıklayıp `Create > Prefab Variant` kullanın.

---

## 3. SPAGETTİ KODA KARŞI: ASSEMBLY DEFINITIONS (.ASMDEF)

Kodların birbirine doğrudan bağlanmasını ve başka bir AI'ın rastgele `GetComponent` atmasını engellemek için kod tabanı Assembly Definition'lar ile fiziksel olarak sınırlandırılır.

### Mevcut Durum (2026-11-11, uygulandı)

| Assembly | Klasör | İçerik | Referanslar |
| :--- | :--- | :--- | :--- |
| `RetroFPS.Core` | `Scripts/Core/` | `IDamageable`, `IStunnable`, `IEncounterEnemy` (`Core/Interfaces/`), `AmmoType`, `GameLayers`, `GameResolutionManager` | Yok (yalnızca UnityEngine) |
| `RetroFPS.Gameplay` | `Scripts/` (Player, Weapons, Combat, UI, Environment) | Tüm oynanış kodu | `RetroFPS.Core`, `Unity.InputSystem` |
| `RetroFPS.Editor` | `Scripts/Editor/` | `QuickSetup` (yalnızca Editor platformu) | `RetroFPS.Core`, `RetroFPS.Gameplay`, `Unity.InputSystem` |

- `RetroFPS.Core` **hiçbir oynanış assembly'sine referans veremez.** Yeni arayüz, enum ve sözleşmeler buraya eklenir.
- Bir dosyayı taşırken `.meta` dosyası da birlikte taşınmalıdır (Unity Editor içinden sürükleyin); aksi halde prefab/sahne referansları kopar.

### Hedef Durum (henüz uygulanmadı)

`Player` / `Weapons` / `Combat` / `UI` ayrı assembly'lere bölünecek. Bunun önünde şu **döngüsel bağımlılıklar** var ve önce bunlar kırılmalıdır:

- `Combat ➔ Player`: `BasicEnemyAI`, `FlyingEyeAI`, `HealthOrb`, `AmmoPickup` doğrudan `PlayerMovement` / `PlayerHealth` arıyor.
- `Combat ➔ Weapons`: `AmmoPickup`, `EnemyAmmoDropper` doğrudan `PlayerAmmoInventory` kullanıyor.
- `Weapons ➔ UI`: Silahlar `CrosshairHUD` arıyor (olay ile değiştirilmeli).
- `Player ➔ Weapons/Combat`: `PlayerDebugHUD` `WeaponController` ve `WaveSpawnDirector` okuyor.

Çözüm deseni: hedef bilgisi için Core'da bir arayüz (örn. `IPlayerTarget`), UI'a giden çağrılar için C# event.

> [!tip] ASMDEF GÜCÜ
> Bölünme tamamlandığında UI klasöründeki bir script, `RetroFPS.Combat` referansı eklenmediği sürece doğrudan `BasicEnemyAI` sınıfını çağıramaz; proje derlenmez! Bu durum hem arkadaşınızı hem de onun AI'ını **Event-Driven (Olay Güdümlü)** çalışmaya zorlar.

---

## 4. GIT BRANCH VE PULL REQUEST (PR) AKIŞI

```mermaid
gitGraph
    commit id: "Initial Core"
    branch feat/friend-ui
    checkout feat/friend-ui
    commit id: "Add HealthBar View"
    commit id: "Connect to EventBus"
    checkout main
    branch feat/user-shotgun
    checkout feat/user-shotgun
    commit id: "Create ShotgunWeapon"
    commit id: "Add Shotgun Prefab"
    checkout main
    merge feat/user-shotgun
    checkout feat/friend-ui
    merge main
    checkout main
    merge feat/friend-ui
```

1. **Asla Doğrudan `main` Branch'ine Push Yapmayın:**
   - Her yeni özellik ayrı bir branch'te geliştirilir:
     * `git checkout -b feat/arkadas-ui-hud`
     * `git checkout -b feat/sen-yeni-dusman-boss`
2. **Birleştirmeden Önce Kontrol Listesi (Pre-Merge Checklist):**
   - [ ] Unity Console'da sıfır hata ve sıfır derleme uyarısı var mı?
   - [ ] `Update()` / `FixedUpdate()` içinde `GetComponent`, `Find` veya string concatenation yapılmış mı? (Sıfır GC kuralı)
   - [ ] Yeni kod mevcut sistemleri bozmadan arayüz (`IDamageable`) üzerinden mi konuşuyor?
   - [ ] Değişiklikler sadece ilgili prefab'a mı uygulandı, yoksa ortak sahne dosyası da yanlışlıkla değiştirildi mi?

---

## 5. BAŞKA BİR AI KULLANIRKEN VERİLMESİ GEREKEN PROMPT ŞABLONU
Arkadaşınız kendi AI'ına (Cursor, Claude, Copilot vb.) bir görev verirken şu şablonu kullanmalıdır:

```markdown
Bu projede Unity 6, URP ve New Input System kullanılmaktadır.
Lütfen kod yazmadan önce:
1. 'START_HERE_AI_CONSTITUTION.md' dosyasındaki Zero-GC ve encapsulation kurallarına uy.
2. 'CodebaseDependencyGraph.md' dosyasındaki bağımlılıkları incele.
3. Diğer geliştiricinin kodlarına doğrudan GetComponent ile bağlanma; 'CombatAndDamageInterfaces.md' arayüzlerini ve C# Action delegelerini kullan.
4. Hot path'lerde (Update) bellek tahsisi yapma.
```

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[START_HERE_AI_CONSTITUTION]] ➔ `governed_by_constitution`
- [[CodebaseDependencyGraph]] ➔ `protects_graph`
- ⬅️ [[ProjectOverview]] ➔ `defines_collaboration` *(gelen)*
