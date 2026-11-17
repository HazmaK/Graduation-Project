---
project: retro-fps
type: SystemArchitecture
tags: [rendering, urp, presentation, accessibility]
---

# PostFx: Is ve Kor

## Visual Direction ("Is ve Kor")
- **Palette:** Cool-violet soot shadows, warm torch highlights, HDR emissives blooming so telegraphs/projectiles pop, and light film grain.
- **Tuned 2026-11-17:** Contrast is kept low and exposure lifted because scenes are already very dark; the first pass (contrast +14/+24) crushed the tower and made Araf nearly black.

## Volume Layout & Ownership
All seven volumes are global, Default-layer children of `PostFx_Rig.prefab` (zero-weight volumes are disabled).

| Child | Priority | Profile | Weight Driver |
| :--- | :---: | :--- | :--- |
| `Volume_Base` | 10 | `PostFx_Base` | `PostProcessing` (1 or 0) |
| `Volume_Araf` | 20 | `PostFx_Araf` | `RunEvents.CurrentRun.Phase == InAraf` |
| `Volume_Boss` | 21 | `PostFx_Boss` | `BossEvents.IsEngaged && !inAraf` |
| `Volume_BossPhase2` | 22 | `PostFx_BossPhase2` | `BossWeight > 0 && BossEvents.Phase >= 2` |
| `Volume_LowHealth` | 30 | `PostFx_LowHealth` | `PlayerHealth` (30% → 12%) |
| `Volume_Death` | 31 | `PostFx_Death` | `PlayerHealth.IsDead` |
| `Volume_Runtime` | 50 | `PostFx_Runtime` (`DontSave`) | Always 1 (owned by `PostFxDirector`) |

- **Runtime-only parameters:** `Volume_Runtime` alone owns `ChromaticAberration.intensity`, `LensDistortion.intensity`, and `ColorAdjustments.postExposure`. Authored profiles never override them.
- **Pipeline fallback:** `Assets/Settings/SampleSceneProfile.asset` (bloom 0.25, vignette 0.2, Neutral tonemap) sits underneath and is what the player sees when Post-Processing is off.

## Authored Profiles (`Assets/_Project/Data/Rendering/PostFx/`)
- **`PostFx_Base`:** Tonemapping Neutral; Bloom (threshold 0.85, intensity 0.60, scatter 0.55, tint `(1, 0.90, 0.78)`, low-quality); ColorAdjustments (contrast 6, saturation -4, filter `(1, 0.96, 0.90)`); SplitToning (shadows `(0.43, 0.41, 0.56)`, highlights `(0.66, 0.52, 0.36)`, balance -10); Vignette (`(0.05, 0, 0.01)`, intensity 0.16, smoothness 0.35); FilmGrain (Medium1, intensity 0.20, response 0.8).
- **`PostFx_Araf`:** ColorAdjustments (contrast 8, saturation 6, filter `(1, 0.86, 0.80)`); SplitToning (shadows `(0.58, 0.38, 0.38)`, highlights `(0.64, 0.50, 0.36)`, balance 0); Vignette (`(0.22, 0, 0.02)`, intensity 0.26, smoothness 0.4); FilmGrain (intensity 0.36); Bloom (intensity 0.65, tint `(1, 0.72, 0.55)`).
- **`PostFx_Boss`:** ColorAdjustments (contrast 10, saturation -18, filter `(0.94, 0.96, 1)`); SplitToning (shadows `(0.40, 0.44, 0.58)`, highlights `(0.62, 0.57, 0.42)`, balance -5); Vignette (`(0, 0, 0.02)`, intensity 0.30).
- **`PostFx_BossPhase2`:** ColorAdjustments (saturation -10, filter `(1, 0.86, 0.82)`); Vignette (`(0.25, 0, 0.02)`, intensity 0.34); FilmGrain (intensity 0.30).
- **`PostFx_LowHealth`:** ColorAdjustments (saturation -35); Vignette (intensity 0.36).
- **`PostFx_Death`:** ColorAdjustments (saturation -90, contrast 20); Vignette (intensity 0.45); FilmGrain (intensity 0.45).

## `PostFxConfig` Tuning (`PostFxConfig_Default.asset`)
- **Blend speeds (weight/unscaled s):** `areaBlendSpeed` 1.5, `lowHealthBlendSpeed` 3, `deathBlendSpeed` 1.6, `deathReleaseSpeed` 4, `lowHealthStart` 0.30, `lowHealthFull` 0.12.
- **Static area:** `arafChromaticAberration` 0.10, `bossPhase2ChromaticAberration` 0.07, `arafLensDistortion` -0.05.
- **Pulses:** `damageCaMinimum` 0.25, `damageCaMaximum` 0.75, `damageForMaximumCa` 40, `damageDecaySeconds` 0.30; `hitstopCaFloor` 0.22; `parryCa` 0.35 (`parryCaDecaySeconds` 0.18), `parryExposure` 0.25 (`parryExposureDecaySeconds` 0.14); `dashDistortion` -0.16 (`dashDecaySeconds` 0.28); `minPulseInterval` 0.35.
- **Caps & Exposure (EV):** `chromaticAberrationCap` 0.9, `lensDistortionMinimum` -0.3, `exposureMinimum` -2, `exposureMaximum` 2, `brightnessEvRange` 1, `deathExposure` -0.6, `baseExposure` 0.45, `arafExposure` 0.60, `bossExposure` 0.15.

## Dynamic Reactions & Deliberate Exclusions
- **Triggers:** `PlayerHealth` (damage CA pulse, low health 30%→12%, death/revive), `HitstopManager.IsActive` (CA floor), `PlayerParryController.OnParrySuccess` (CA + exposure pulse), `PlayerMovement.OnDashStarted` (lens distortion pulse), `RunEvents` (Araf), `BossEvents` (boss / phase 2; Araf suppresses boss).
- **Not added:**
  - *Blade lunge/slam effect:* No public event; hitstop CA floor covers impacts without touching weapon classes.
  - *Pixelation / low-res pass, custom shaders, or renderer features:* Excluded to keep pipeline untouched.
  - *Damage/heal/low-health vignettes:* Remain `OnGUI` in `PlayerDamageFeedback`.

## Settings & Accessibility
- `PostProcessing` (on/off), `Brightness` (-1..+1 EV, always active), `ScreenEffects` (0..1, scales pulses and static area CA/distortion), `ReduceMotion` (disables all pulses).
- Parry exposure pulse is rate-limited to one start per `0.35 s` (`minPulseInterval`).
- Settings apply live while paused (`GamePause.IsPaused` freezes blend/pulse clocks, not `GameSettings.Changed`).

## Integration
- `RetroFPS/Build Post FX` (idempotent) authors `PostFx_Rig.prefab` and nests it under `Player.prefab`.
- No scene, pipeline asset, or renderer asset was changed. Assembly: `RetroFPS.PostFx`.

## Verification (2026-11-17, Editor, Script-Driven)
- **Passed:** Compile 0 errors; `PostFxMixerTests` 37/37; settings/string/pause fixtures pass; `Update` allocates 0 B; frame time off vs on within noise (~5.7 ms). Evidence: `production/qa/evidence/postfx-01..14` (+`08b`).
- **Not verified:** Real mouse/keyboard play, real-time look of pulses, real projectile VFX under the grade, main-menu host of the settings panel, built player.

## Open Tuning Points (Designer)
- Araf red cast strength.
- Boss phase-2 visual difference is slight.
- Low-health desaturation is hard to see under the `OnGUI` red overlay.
- Death look is hidden by the true-death card.
- HDR color grading mode / camera dithering left unchanged.
