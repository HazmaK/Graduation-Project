# Unity 6.3 LTS — Breaking Changes

**Last verified:** 2026-02-13

This document tracks breaking API changes and behavioral differences between Unity 2022 LTS
(likely in model training) and Unity 6.3 LTS (current version). Organized by risk level.

## HIGH RISK — Will Break Existing Code

### Entities/DOTS API Complete Overhaul
**Versions:** Entities 1.0+ (Unity 6.0+)

```csharp
// ❌ OLD (pre-Unity 6, GameObjectEntity pattern)
public class HealthComponent : ComponentData {
    public float Value;
}

// ✅ NEW (Unity 6+, IComponentData)
public struct HealthComponent : IComponentData {
    public float Value;
}

// ❌ OLD: ComponentSystem
public class DamageSystem : ComponentSystem { }

// ✅ NEW: ISystem (unmanaged, Burst-compatible)
public partial struct DamageSystem : ISystem {
    public void OnCreate(ref SystemState state) { }
    public void OnUpdate(ref SystemState state) { }
}
```

**Migration:** Follow Unity's ECS migration guide. Major architectural changes required.

---

### Input System — Legacy Input Deprecated
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: Input class (deprecated)
if (Input.GetKeyDown(KeyCode.Space)) { }

// ✅ NEW: Input System package
using UnityEngine.InputSystem;
if (Keyboard.current.spaceKey.wasPressedThisFrame) { }
```

**Migration:** Install Input System package, replace all `Input.*` calls with new API.

---

### URP/HDRP Renderer Feature API Changes
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: ScriptableRenderPass.Execute signature
public override void Execute(ScriptableRenderContext context, ref RenderingData data)

// ✅ NEW: Uses RenderGraph API
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
```

**Migration:** Update custom render passes to use RenderGraph API.

---

## MEDIUM RISK — Behavioral Changes

### Addressables — Asset Loading Returns
**Versions:** Unity 6.2+

Asset loading failures now throw exceptions by default instead of returning null.
Add proper exception handling or use `TryLoad` variants.

```csharp
// ❌ OLD: Silent null on failure
var handle = Addressables.LoadAssetAsync<Sprite>("key");
var sprite = handle.Result; // null if failed

// ✅ NEW: Throws on failure, use try/catch or TryLoad
try {
    var handle = Addressables.LoadAssetAsync<Sprite>("key");
    var sprite = await handle.Task;
} catch (Exception e) {
    Debug.LogError($"Failed to load: {e}");
}
```

---

### Physics — Default Solver Iterations Changed
**Versions:** Unity 6.0+

Default solver iterations increased for better stability.
Check `Physics.defaultSolverIterations` if you rely on old behavior.

---

## LOW RISK — Deprecations (Still Functional)

### UGUI (Legacy UI)
**Status:** Deprecated but supported
**Replacement:** UI Toolkit

UGUI still works but UI Toolkit is recommended for new projects.

---

### Legacy Particle System
**Status:** Deprecated
**Replacement:** Visual Effect Graph (VFX Graph)

---

### Old Animation System
**Status:** Deprecated
**Replacement:** Animator Controller (Mecanim)

---

## Platform-Specific Breaking Changes

### WebGL
- **Unity 6.0+**: WebGPU is now the default (WebGL 2.0 fallback available)
- Update shaders for WebGPU compatibility

### Android
- **Unity 6.0+**: Minimum API level raised to 24 (Android 7.0)

### iOS
- **Unity 6.0+**: Minimum deployment target raised to iOS 13

---

## Migration Checklist

When upgrading from 2022 LTS to Unity 6.3 LTS:

- [ ] Audit all DOTS/ECS code (complete rewrite likely needed)
- [ ] Replace `Input` class with Input System package
- [ ] Update custom render passes to RenderGraph API
- [ ] Add exception handling to Addressables calls
- [ ] Test physics behavior (solver iterations changed)
- [ ] Consider migrating UGUI to UI Toolkit for new UI
- [ ] Update WebGL shaders for WebGPU
- [ ] Verify minimum platform versions (Android/iOS)

---

**Sources:**
- https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/upgrade-guide.html

---

## 6.3 LTS → 6.4

**Last verified:** 2026-11-12 — sources: https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity64 (U64, upgrade guide), https://docs.unity.com/en-us/engine/6000.6/manual/whats-new/unity64 (W64, What's New)

- **`Object.Destroy` now calls `OnDisable` on components of all descendants** (matching `DestroyImmediate`). Remove any manual code that propagates OnDisable to deeper children — it now causes duplicate calls. *(U64, Assets and media)*
- **URP Compatibility Mode removed** for custom render passes; the `URP_COMPATIBILITY_MODE` define is gone. Custom passes must use Render Graph. *(U64, Graphics)*
- **SRP Core: `IVolume` interface removed** — use `VolumeGizmoDrawer`. Physics is now a soft dependency: local volumes stop working if `com.unity.modules.physics` is disabled. *(U64, Graphics)*
- `LightShadowCasterMode.NonLightmappedOnly` → `ShadowMask`, `Everything` → `DistanceShadowMask` (auto-updated). *(U64)*
- Asset Database artifact dependencies narrowed: a ScriptedImporter / AssetPostprocessor may stop re-running; add `AssetImportContext.DependsOnSourceAsset` / `DependsOnArtifact`. *(U64)*
- PVRTC texture format removed (use ASTC/ETC). *(W64)*
- Project Auditor is built into the Editor; some analyzer APIs became internal. *(U64)*
- UGUI / TextMeshPro / UI Toolkit: the 6.4 upgrade guide lists **no** breaking changes for these.

## 6.4 → 6.5

**Last verified:** 2026-11-12 — sources: https://docs.unity.com/en-us/engine/6000.5/manual/upgrade-guides/upgrade-guide-unity65 (U65, upgrade guide), https://docs.unity.com/en-us/engine/6000.5/manual/whats-new/unity65 (W65, What's New)

- **`EntityId` replaces `InstanceID` — now compile errors:** `Object.GetInstanceID`, `Resources.InstanceIDToObject`, `Selection.instanceIDs`. Use the `EntityId` versions (e.g. `go.GetEntityId()`). `EntityId` is 8 bytes: never cast to `int`, never round-trip through `ToString`, use it directly as a dictionary key (not `GetHashCode()`). *(U65, Programming)*
- **Obsolete GameObject/Component shortcut properties removed:** `rigidbody`, `rigidbody2D`, `camera`, `light`, `animation`, `constantForce`, `renderer`, `audio`, `networkView`, `collider`, `collider2d`, `hingeJoint`, `particleSystem`; plus `GameObject.AddComponent(string)`, `PlayAnimation`/`StopAnimation`, `SampleAnimation` (→ `AnimationClip.SampleAnimation`). Use `GetComponent<T>()` / `AddComponent<T>()`. *(U65)*
- Some obsolete warnings promoted to errors — NOT SOURCEABLE: the full list is not stated at the U65 page.
- **Render Pipeline Core no longer depends on `com.unity.ugui`.** A project that got uGUI only through URP must add `com.unity.ugui` itself. This project lists `com.unity.ugui` 2.5.0 explicitly in its manifest, so it is unaffected. *(U65, Graphics)*
- Rendering Debugger now uses UI Toolkit; uGUI-based custom debug-control classes removed. *(U65)*
- Obsolete ModelImporter APIs are compile errors (`isFileScaleUsed`→`useFileScale`, `normalImportMode`→`importNormals`, `optimizeMesh`→`optimizeMeshPolygons`/`optimizeMeshVertices`, `resampleRotations`→`resampleCurves`, `splitTangentsAcrossSeams`→`tangentImportMode`, `tangentImportMode`→`importTangents`). *(U65)*
- Serialization-rules Roslyn analyzer: new warnings/errors for a missing `[Serializable]` or `[SerializeReference]` on structs/primitives. *(U65)*
- Editor log is project-local: `<Project>/Logs/Editor.log` (`Application.consoleLogPath`; `-useGlobalLog` restores the old location). *(U65)*
- BCL: `System.Text.Json`, `System.Collections.Immutable`, `System.Reflection.Metadata`, `System.Runtime.CompilerServices.Unsafe` are now supplied by Unity; user copies are dropped from builds. *(U65)*
- `LowLevelPhysics2D` → `PhysicsCore2D` (`Unity.U2D.Physics`); ECS `Entities.ForEach`, `Job.WithCode`, `IAspect` removed; VR module removed; Android min API 26 / Gradle 9.1. *(U65 — not relevant to this PC 3D project)*
