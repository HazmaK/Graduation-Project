# Claude Code / Claude Desktop Project Guide

## AI CONSTITUTION & MEMORY
- Bu proje retro boomer shooter tarzında hızlı tempolu bir Unity 6 oyunudur.
- Projenin sinaptik hafızası ve tüm sistem bağımlılıkları proje kökündeki `documentation/retro-fps` (Obsidian Vault) üzerinde kayıtlıdır. Mimari güncellemeler buraya yazılır.

## STRICT ARCHITECTURAL STANDARDS
1. **Unity 6 & URP:**
   - Universal Render Pipeline kullanılır.
   - Sadece New Input System (`UnityEngine.InputSystem`) kullanılır.
   - `FindObjectOfType` KESİNLİKLE kullanılmaz (`FindFirstObjectByType` veya `FindAnyObjectByType` kullanılır).
2. **Zero GC in Hot Paths:**
   - `Update`, `FixedUpdate`, `LateUpdate` içinde heap tahsisi (`new`, string format/concat, `GetComponent`) yasaktır.
   - Fizik sorgularında `Physics.RaycastNonAlloc` ve `Physics.OverlapSphereNonAlloc` kullanılır.
3. **Encapsulation:**
   - Sadece `[SerializeField] private` kullanılır. Asla public değişken açılmaz. Dış erişim read-only getter (`public float Speed => speed;`) ile sağlanır.
4. **Decoupled Extension:**
   - Yeni mekanikler mevcut sınıfları şişirerek değil, `RetroFPS.Combat` arayüzleri (`IDamageable`, `IStunnable`, `IEncounterEnemy`) ve `WeaponBase` soyut sınıfı üzerinden genişletilerek eklenir.
   - Sahne dosyalarında merge conflict yaşamamak için tüm nesneler `Prefab` olarak tasarlanır.
