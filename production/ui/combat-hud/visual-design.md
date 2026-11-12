# Visual Design Spec: Combat HUD (PURGATOR)

> **Status**: Draft (Phase 2, art-director stand-in)
> **Last Updated**: 2026-11-12
> **Applies**: `design/ux/hud.md` (layout, states, data). Direction A "Kan ve Kemik" from `production/ui/main-menu/visual-design.md` sections 0 and 1 and 4.
> **Rule**: gameplay readability first. The HUD is a lighter relative of the menu: same tokens, same type, same 360p pixel grid, far less ornament.

---

## 1. Shared Foundations

- Canvas: Scale With Screen Size, reference 1920x1080, Match = 1 (height), 5% safe container (as hud.md section 9).
- Ornament grid: 360p art pixels (x3 at 1080p, x2 at 720p, x4 at 1440p, x6 at 2160p). Pixel textures: Point filter, no mips, RGBA32, Clamp. Text is TMP SDF (not pixel art).
- Layer order inside the HUD canvas: plates -> bars/tracks/fills -> glyphs -> text -> chips -> banner -> death overlay.
- All motion uses `Time.unscaledDeltaTime`; every motion row below is disabled/static under `GameSettings.Current.ReduceMotion`.

## 2. Tokens, Plate Opacity, Contrast

### 2.1 Tokens (menu tokens reused; HUD additions marked NEW)

| Token | Hex | HUD use |
|---|---|---|
| `bg` / `hud-plate` | `#0C0A0A` | Plate fill (alpha 0.90), soot text on chips |
| `panel` | `#140F10` | Death-card button fill (opaque) |
| `text` | `#E8DCC0` | All HUD text, outlines, filled pips/glyphs |
| `focus` | `#FFF4DC` | Active states, overheal hatch, death button focus text |
| `accent` | `#DB143D` | Bar fill, Araf sigil fill (always on an opaque sub-surface), underline rules. Never text |
| `danger` | `#FF4D5E` | Low-health border, opaque warning chips. Never text on a translucent plate |
| `text-dim` | `#A09684` | **Not used for any HUD text over gameplay** (fails worst case, see 2.3). Allowed only for decorative dividers |
| `border` (NEW, menu shadow tone) | `#7A6E5E` | 1 art-px plate border (decorative, never the only edge cue) |
| `track` (NEW, menu slider track) | `#2A2020` | Opaque bar track / pip slot / cooldown base |
| `type-bolts` | `#00E6FF` (0,0.9,1) | Bolt glyph tint and ammo bar fill only |
| `type-shells` | `#FFB326` (1,0.7,0.15) | Shell glyph tint and ammo bar fill only |
| `overlay` | `#000000` | Death overlay, alpha 0.88 |

### 2.2 Plate opacity (decision)

Plates are `#0C0A0A` at **alpha 0.90** (min 0.88). Method: worst-case backgrounds behind a plate are (Fire) torch/fire `#FF8C2A` (L 0.4018, sustained) and (Flash) white-hot muzzle flash `#FFFFFF` (L 1.0, transient <= 100 ms). Composite assumed in linear light (worst case; gamma-space UGUI blending is kinder): `L_bg = a * 0.003175 + (1 - a) * L_behind`. Bone text (L 0.7216) needs 4.5:1, so `L_bg <= 0.1215`:
- Over Fire: needs a >= 0.703. Over Flash: needs a >= 0.881. At a = 0.90 both pass. A 0.65 plate fails even over Fire (4.00:1), hence the hud.md budget row was raised from 65% to 90%.
- Cost: plates cover ~5.4% of the screen, so the opaque-ish look is acceptable; world visibility is kept by small plates and the clear centre.

### 2.3 Computed contrast (WCAG 2.x, rounded)

Surfaces: `P-dark` = plate over nothing bright (L 0.0032); `P-fire` = plate 0.90 over Fire (L 0.0430); `P-flash` = plate 0.90 over Flash (L 0.1029); opaque sub-surfaces are independent of the world.

| Foreground (text or graphic) | on P-dark | on P-fire | on P-flash | Required | Result |
|---|---|---|---|---|---|
| `text` #E8DCC0 (L 0.7216) | 14.51 | 8.29 | 5.05 | 4.5 | Pass |
| `focus` #FFF4DC (L 0.9114) | 18.08 | 10.33 | 6.29 | 4.5 | Pass |
| `type-bolts` #00E6FF graphic (L 0.6382) | 12.9 | 7.40 | 4.50 | 3.0 | Pass |
| `type-shells` #FFB326 graphic (L 0.5363) | 11.0 | 6.30 | 3.84 | 3.0 | Pass |
| `danger` #FF4D5E border graphic (L 0.2738) | 6.09 | 3.48 | 2.12 | 3.0 | Pass except Flash (transient, chip also present) |
| `text-dim` #A09684 (L 0.3094) | 6.76 | 3.86 | 2.35 | 4.5 | **Fail: banned for HUD text** |
| `accent` #DB143D (L 0.1590) on plate | 3.93 | 2.25 | 1.37 | 3.0 | **Fail on plate: only used on opaque `track`/sigil base** |

Opaque pairs (world-independent, always evaluated):

| Pair | Ratio | Required | Result |
|---|---|---|---|
| soot `#0C0A0A` text on `danger` chip `#FF4D5E` | 6.09 | 4.5 | Pass |
| soot `#0C0A0A` text on `focus` chip `#FFF4DC` (parry ACTIVE) | 18.08 | 4.5 | Pass |
| `text` on `panel` `#140F10` (death button) | 13.96 | 4.5 | Pass |
| `accent` fill on `track` `#2A2020` (L 0.0163) | 3.15 | 3.0 | Pass |
| `text` bar outline / hollow glyph outline on `track` | 11.7 | 3.0 | Pass |
| `focus` hatch on `track` | 14.5 | 3.0 | Pass |
| `border` `#7A6E5E` on plate (decorative) | 3.97 on P-dark | n/a | Decorative only |
| `text` death title on overlay 0.88 over Flash (L_bg 0.12) | 4.54 | 4.5 (64 px also qualifies as large, 3.0) | Pass |

**Worst-case figure to quote: bone text on a 0.90 plate over a white-hot flash = 5.05:1 (8.29:1 over fire).** Re-measure with a colour picker on in-engine screenshots (required by the HUD acceptance criteria).

## 3. Typography (px @1080p; all critical >= 20)

| Role | Font | Size | Notes |
|---|---|---|---|
| Health number | Alegreya Sans SC Bold | 44 | `text`; `/max` 20 Bold; `+n` overheal 28 Bold `focus` |
| Labels and states (ATILMA, SAVUŞTUR, KANCA, HAZIR/BEKLE/AKTİF, BOŞ, ARAF lines) | Alegreya Sans SC Bold | 20, tracking +8% | Never Regular/dim |
| Ammo count | Alegreya Sans SC Bold | 48; `/max` 24 | `text` always, type colour never on text |
| Weapon name | Alegreya Sans SC Bold | 20 | With slot index |
| Wave counter / remaining / rooms | Alegreya Sans SC Bold | 28 / 24 / 24 | |
| Banner | Alegreya Sans SC Bold | 40; second merged line 28 | |
| Death title | Grenze Gotisch 700 | 64, tracking +2% | Underline rule 3 px `accent` |
| Death button | Alegreya Sans SC Bold | 32 | |

Placeholder: TMP default LiberationSans SDF (Turkish coverage via its fallback). Final: the menu's font assets (UI 1024x1024, custom charset ASCII + `ÇçĞğİıÖöŞşÜü` + `%/+!`). Text is pre-cased; numbers come from the cached int string table. Min size never shrinks below 20 (TMP auto-size min = 20).

## 4. Element Visuals

**Plate (all corner plates)**: fill `hud-plate` 0.90; 1 art-px (3 px) `border` line; corners chamfered by 1 art px; no vertebra corners, no jewels (ornament level LOW, lighter than the menu's medium-low). 9-slice: source 12x12 art px, borders 2 art px (6 px). Padding 12 px. Exception ornament: none. Sizes per hud.md section 3.2 (BL 400x150, BR 320x120, TR 340 wide, banner 600x60/96).

**Health (BL row 1)**: sigil `HUD_Cross` 9x9 art px (27 px), `text`. Number 44 `text`. Bar: 376x12 px, opaque `track`, 1 art-px `text` outline, fill `accent` left to right, 3 px `text` leading tick at the fill edge (shape cue for amount, not colour). Overheal: overflow segment past the right end, 6 px diagonal stripes alternating `focus`/`track` (`HUD_Hatch`), with `+n` 28 px `focus`. LOW (<= `lowHealthPercent`): plate border becomes 3 art px (9 px) in `danger`; opaque `danger` chip 36x36 px with soot `!` 28 px Bold left of the number; number stays `text`. Pulse: border alpha 1.0 <-> 0.55 at 1 Hz; Reduce Motion: static full alpha. Damage: 120 ms `focus` edge flash on the lost chunk (Reduce Motion: snap).

**Dash (BL row 2, cell 1)**: line 1 up to N diamond pips 9x9 art px (27 px), 8 px gap: ready = solid `text` with 1 art-px `text` outline on `track` slot; spent = hollow (slot `track`, outline only); recharging pip = `Radial360` fill in `text`. Line 2: `ATILMA 3/3` 20 Bold.

**Parry (BL row 2, cell 2; only when buckler enabled)**: line 1 `HUD_Shield` 9x9 art px + `SAVUŞTUR` 20. Line 2 state word: HAZIR = shield solid `text`, word on plain plate; AKTİF = opaque `focus` chip with soot word, shield filled `focus`; BEKLE = shield hollow outline, `Radial360` sweep over a `track` base, word on plate. Shape + word always, never colour alone. Success: 250 ms `focus` glyph flash (Reduce Motion: word `SAVUŞTURULDU` only).

**Meat hook (BL row 2, cell 3; shotgun only)**: identical to Parry with `HUD_Hook` glyph, `KANCA`, HAZIR / BEKLE.

**Weapon + ammo (BR 320x120)**: row 1: weapon glyph 12x9 art px (36x27), tinted `type-bolts` / `type-shells` (blade: `text`) with 1 art-px `text` outline, then name + `[n]` 20 Bold. Row 2: count 48 `text` + `/max` 24. Row 3: bar 296x12 px, `track`, `text` outline, fill in the type colour. Colour is identity only: name, glyph silhouette and numbers carry the meaning. LOW (< 0.25): opaque `danger` chip `!`; EMPTY: opaque `danger` chip `BOŞ` (soot 20 Bold), bar shows outline only. Pulse 1 Hz on the chip alpha (static under Reduce Motion). Melee: glyph + `YAKIN`, no count/bar.

**Wave and Araf block (TR, 340 wide)**: `DALGA n/t` 28 `text`; `KALAN 7` 24 + 6 px remaining bar (`track`, `text` fill, outline); remaining <= 3: underline 3 px `text`. Araf status line: sigil `HUD_Sigil_Araf` 5x7 art px (15x21): READY = `accent` fill with 1 art-px `text` outline (>= 13:1 outline, per menu protanopia rule), USED = hollow outline on `track`; text 20 Bold `text` (`ARAF: HAZIR` / `ARAF: KULLANILDI`). In Araf: warning = opaque `danger` chip with soot 20 Bold text and `!`; `ODALAR r/t` 24; `ÇIKIŞ BULUNDU` 20 with `HUD_Arrow` 5x5 art px glyph. Room reveal: 200 ms `focus` flash on the number (Reduce Motion: none).

**Top-centre banner**: plate 600x60 (single) or 600x96 (two lines: 40 + 28), alpha 0.90, `border`, text `text` 40 Bold centred, 3 px `accent` underline rule under the text (decorative, not information). Fade 150 in / 300 out; Reduce Motion: instant. ARENA CLEARED uses `focus` text (brighter, same size) plus the rule at 6 px: emphasis by size/weight of rule, not colour.

**True-death card**: full-screen `overlay` alpha 0.88 (L_bg <= 0.12 even over a white flash). Title `GERÇEK ÖLÜM` Grenze Gotisch 64 `text`, 3 px `accent` rule beneath (120 px wide, centred). Button 360x72 px: fill `panel` opaque, 2 art-px (6 px) `text` border, text `TEKRAR DENE` 32 Bold `text`. Focused (always, on open): text `focus`, Kemik Mühür sigil 27x33 px 16 px left of the text, 6 px underline (accent core, `text` ends) as in menu 1.4. Pressed: 80 ms sigil fill flips to `focus`, text drops 3 px. Fade 400 ms (Reduce Motion: instant). Cursor visible.

## 5. Motion Summary

| Element | Default | Reduce-motion |
|---|---|---|
| Low-health border / low-ammo chip pulse | 1 Hz, alpha 1.0-0.55 | Static |
| Damage chunk flash, dash pip brighten, ammo pickup pulse, room reveal flash | 100-200 ms | None |
| Parry success flash | 250 ms | Word swap only |
| Banner fades | 150 / 300 ms | Instant |
| Wave block fade-out | 300 ms | Instant |
| Death card fade, button press | 400 ms / 80 ms | Instant / 80 ms |

No element exceeds 1 Hz; no shake.

## 6. Asset Manifest (all procedural/placeholder, built by an editor script, e.g. `HudPlaceholderArt.cs`, same pattern as `MainMenuPlaceholderArt`)

| Asset | Size (art px) | @1080p | Placeholder (procedural) |
|---|---|---|---|
| `HUD_Plate_9S` | 12x12, border 2 | 6 px borders | Generated Texture2D from a hard-coded mask (1 px `border` line, chamfered corners, soot fill), Point/RGBA32/Clamp |
| `HUD_Chip_9S` | 6x6, border 1 | | Solid 9-slice, tinted at runtime `danger` / `focus` |
| `HUD_Bar_Track` / `HUD_Bar_Fill` | 8x4 (L1 R1) | 12 px tall | Solid Images `track` / fill colours + 1 art-px outline Image |
| `HUD_Cross` | 9x9 | 27x27 | Hard-coded pixel mask |
| `HUD_Diamond` (pip) | 9x9 | 27x27 | Hard-coded mask; filled / hollow / radial variants via Image tint + `Radial360` |
| `HUD_Shield`, `HUD_Hook` | 9x9 | 27x27 | Hard-coded masks (or TMP glyph stopgap) |
| `HUD_Weapon_Bolt`, `_Shell`, `_Blade` | 12x9 | 36x27 | Hard-coded masks, white, tinted at runtime |
| `HUD_Sigil_Araf` | 5x7 | 15x21 | Mask (menu Kemik Mühür scaled down) |
| `HUD_Arrow` | 5x5 | 15x15 | Mask |
| `HUD_Hatch` | 4x4 tile | 12 px | Mask, used as Tiled sprite (Clamp still) |
| `Sigil_A_BoneSeal` (death button) | 9x11 | 27x33 | Reuse menu asset |
| Fonts | | | TMP LiberationSans SDF placeholder; final Alegreya Sans SC Bold + Grenze Gotisch (reuse menu font assets and OFL texts) |
| Theme asset | | | `HudTheme` ScriptableObject holding the tokens above (no hex in scripts); colourblind palette swaps edit this asset only |
| Overlay | | | Solid Image `#000000` |

## 7. Acceptance Additions (visual)

- [ ] Plates alpha 0.90 (>= 0.88); bone text measured >= 4.5:1 on a screenshot with `#FF8C2A` and `#FFFFFF` behind a plate.
- [ ] No HUD text uses `text-dim` or `accent`; danger/accent never carry text on a translucent plate (chips are opaque).
- [ ] Every critical text >= 20 px @1080p; all ornaments on the 360p grid, Point filtered.
- [ ] Greyscale screenshot: low health, low/empty ammo, parry/hook ready vs cooldown vs active, dash pips, Araf ready/used remain distinguishable.
- [ ] Banner two-line plate <= 600x96; combat + banner <= 10% of screen (hud.md budget 9.49%).

## 8. Open Items

- Muzzle-flash and fire brightness assumed (white-hot, #FF8C2A); re-measure against the real lighting in the Gauntlet/Araf scenes.
- Whether linear- or gamma-space UI blending is active in the project (linear assumed as the worst case).
- Weapon/glyph pixel art is placeholder; final art to be supplied.
