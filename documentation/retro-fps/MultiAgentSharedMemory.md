---
project: "retro-fps"
type: "SystemArchitecture"
tags:
  - project
  - memory
  - "retro-fps"
  - multi-agent
  - synaptic-graph
  - obsidian
---

# MultiAgentSharedMemory (Çoklu Ajan ve Ekip Ortak Sinaptik Hafızası)

> [!abstract] CANLI OBSIDIAN VAULT & MCP BELLEK SENKRONİZASYONU
> Bu düğüm, projede çalışan geliştiricilerin (Sen ve Ekip Arkadaşın) ve kullanılan tüm yapay zekâ ajanlarının (`Antigravity`, `Claude`, `Cursor`, `ChatGPT` vb.) ortak mimari hafıza merkezidir.

---

## 1. OTOMATİK ALGILAMA VE ÇİFT YÖNLÜ SENKRONİZASYON PROTOKOLÜ

1. **Tek Doğruluk Kaynağı (`documentation/retro-fps/`):**
   - Her görev öncesinde ilgili alt sistem notları ve `Assets/_Project/Scripts/` altındaki güncel C# dosyaları taranarak ekip arkadaşının yaptığı yenilikler (yeni scriptler, event'ler, prefab bağlantıları veya doküman güncellemeleri) otomatik olarak tespit edilir.
2. **Otonom Obsidian Ağ Güncellemesi:**
   - Projeye yeni bir oyun kuralı, silah, düşman, arayüz veya alt sistem eklendiğinde ya da mevcut bir mimari kural değiştirildiğinde:
     * İlgili teknik döküman (örn. [[WeaponArsenalArchitecture]], [[EnemyAIAndSpawning]], [[PlayerSubsystemArchitecture]]) anında güncellenir.
     * [[CodebaseDependencyGraph]] içerisindeki Mermaid akış diyagramı ve modül tablosuna yeni bağlantılar işlenir.
     * Obsidian Graph View bütünlüğünü korumak için YAML frontmatter etiketleri ve `## İlişkiler (Sinaptik Ağ Bağlantıları)` altındaki `[[WikiLink]]` düğümleri eksiksiz güncellenir.
3. **Paylaşımlı MCP Sinaptik Hafızası (`memory`):**
   - Kalıcı mimari kararlar aynı zamanda arka planda `[retro-fps]` önekli düğümler olarak MCP bilgi grafına kaydedilir.

---

## İlişkiler (Sinaptik Ağ Bağlantıları)
- [[START_HERE_AI_CONSTITUTION]] ➔ `regulates_ai_agents`
- [[ProjectOverview]] ➔ `manages_project`
- [[CodebaseDependencyGraph]] ➔ `tracks_dependencies`
- [[CollaborationAndGitWorkflow]] ➔ `enforces_team_sync`
