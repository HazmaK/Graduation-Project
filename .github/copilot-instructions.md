# GitHub Copilot Instructions (Unity 6 Retro FPS)

- Documentation & Architecture: All system architectures and contracts are documented in `documentation/retro-fps` (Obsidian Vault).
- Engine: Unity 6 (6000.x) with Universal Render Pipeline (URP).
- Input: `UnityEngine.InputSystem` exclusively.
- Zero-GC Hot Paths: Never call `GetComponent`, `FindAnyObjectByType`, or allocate heap objects inside `Update`/`FixedUpdate`.
- Encapsulation: `[SerializeField] private` for Inspector variables; public getter properties for external read access.
- Decoupling: Use `IDamageable`, `IStunnable`, `IEncounterEnemy`, and `System.Action` events. Do not tightly couple classes.
- Non-destructive extension: Derive from `WeaponBase` for new weapons; implement `IDamageable` and `IEncounterEnemy` for new enemies. Never pollute existing monolithic scripts.
- Prefab workflow: Never alter shared `.unity` scene files directly. Create modular prefabs.
