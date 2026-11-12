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

> Yukarıdaki proje kuralları, aşağıdaki Claude Code Game Studios (CCGS) çerçevesinin
> genel kurallarıyla çelişirse **yukarıdakiler önceliklidir**.
> Unity projesi `Graduation Project/` klasöründedir (CCGS'nin varsayılan `src/` klasörü kullanılmaz).

---

# Claude Code Game Studios -- Game Studio Agent Architecture

Indie game development managed through coordinated Claude Code subagents
(framework: github.com/Donchitos/Claude-Code-Game-Studios, v1.3.0).

## Technology Stack

- **Engine**: Unity 6000.5.5f1 (Unity 6.5, URP)
- **Language**: C#
- **Build System**: Unity Build Pipeline
- **Asset Pipeline**: Unity Asset Import Pipeline + Addressables
- **Version Control**: Git

> Use the Unity specialist agents (`unity-specialist`, `unity-ui-specialist`,
> `unity-shader-specialist`, `unity-addressables-specialist`, `unity-dots-specialist`).
> Engine config lives in `project.yaml` (set by `/setup-engine unity`, 2026-11-12).

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

<!-- ENGINE-REFERENCE-IMPORT: the line under this comment is engine-specific,
     and there is none until an engine is set. /setup-engine, the grow's record
     step and /project-settings (engine.name) write
     @docs/engine-reference/<engine>/VERSION.md here for the chosen engine. Skills
     that need the pinned version read that file on demand either way. -->
@docs/engine-reference/unity/VERSION.md


## Technical Preferences

`project.yaml` at the repo root is the primary config store — engine, specialists,
naming, platform, performance, modes. Skills resolve it via `resolve_config`
(see `.claude/docs/config-resolution.md`).

`.claude/docs/technical-preferences.md` is the **legacy fallback**, read on demand
when a key is absent from `project.yaml`.

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
  (exempt: `active.md` appends, agent memory, a subagent's new file at the path
  its orchestrating skill named; see `.claude/rules/agent-memory.md`)
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction
- A skipped or dismissed question is not an answer: act on nothing, re-ask in
  plain text and wait
- Started outside this file's folder? Warn first: hooks, deny rules and the
  ask-first mode load only when Claude Code starts here

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** Run `/start` (existing work) or `/adopt` to audit this
> brownfield project against the template.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

Read `.claude/docs/context-management.md` on demand — it is a reference, not
session context. Two of its conventions are load-bearing:

- **`production/session-state/active.md` is the session checkpoint.** The file is
  the memory, not the conversation. Read it first after any compaction, crash, or
  `/clear`.
- **Helpers in `.claude/scripts/` emit observations, never verdicts.**
