# Visual Design Spec: Main Menu (PURGATOR)

> **Status**: Draft. Needs the user to choose a direction (A or B).
> **Author**: ux-designer (standing in for art-director, team.size: individual)
> **Date**: 2026-11-12
> **Source spec**: `design/ux/main-menu.md` (flows, zones, components, states, Section 12 accessibility)
> **Art bible**: none exists. Once a direction is approved, this document is the interim visual reference for the menu and should seed the art bible.
> **Engine**: Unity 6000.5.5f1, URP, UGUI + TextMeshPro. Reference resolution 1920x1080. Must work from 1280x720 to 3840x2160, and at 16:10 and 21:9.
> **Accessibility**: Standard tier (COMMITTED). Text >= 24 px @1080p. Contrast >= 4.5:1 for body text, >= 3:1 for large text and non-text UI (sigil, slider fill). Focus is never shown by colour alone. Reduce-motion variant required.

---

## 0. Shared Foundations (apply to both directions)

### 0.1 Canvas and scaling
- **Canvas Scaler**: Scale With Screen Size, reference 1920x1080, **Match = 1 (height)**. The layout is anchored left and the art fills the right side, so on a wider aspect (21:9) the extra width goes to the art, and the text never shrinks below its 1080p-relative size. At 16:10 the height scale is 1.11, so a 1728-unit-wide frame is what has to fit (checked against the zone widths in UX Sec. 5.2).
- **Safe area**: 5% margin container, as in UX Sec. 5.2.
- **Pixel grid for ornaments**: draw every pixel-art UI ornament (sigil, frames, dividers) on a **360p art-pixel grid**. That grid scales by a whole number at every common height: 720p = x2, 1080p = x3, 1440p = x4, 2160p = x6. All ornament textures use **Point filter, no mipmaps, no compression (RGBA32), Wrap Clamp**. At heights that are not a whole multiple (1200p, 4:3 modes), accept slight pixel unevenness. Do not switch to bilinear.
- **Text is not pixel-art**. TMP SDF text renders sharp at native resolution. That is what keeps it readable at 24 px. Most of the "retro" feel comes from the ornaments and the art, not the glyphs.

### 0.2 Left-column legibility guarantee (both directions)
UX Sec. 12 requires that contrast holds wherever the embers or smoke are. That is guaranteed by construction:
1. **Left darkening gradient** (`LeftScrim`): a full-height Image anchored left. It covers 0-50% of the width at the bg colour with alpha 0.92, then fades to alpha 0 between 50% and 62%.
2. **Ember budget**: no ember or smoke particle may spawn or drift into the 0-48% region (emitter shapes stay in the right ~55%; set particle `Collision`/kill planes or simply emitter bounds). Smoke may cross into the scrim, but only **below** the scrim layer in draw order.
3. **Worst-case background**: every text pair below is also computed against a worst-case left-column colour (W-bg). This assumes the scrim leaks a lit area of art. The W-bg ceiling is a **tuning acceptance check**: the scrim alpha must keep the sampled left-column colour at or below W-bg luminance.

### 0.3 Layer order (back to front)
`ArtSky` -> `ArtTowerFar` -> `ArtFog` -> `ArtTowerNear` -> `ArtTorchGlow` -> `Embers (ParticleSystem, UI-space camera or Screen Space - Camera canvas)` -> `Smoke` -> `LeftScrim` -> `Title / MenuList / Settings / VersionTag` -> `DimOverlay` -> `Dialog` -> `FadeOverlay`.

### 0.4 Contrast method
Ratios use the WCAG 2.x relative-luminance formula, (L1 + 0.05) / (L2 + 0.05), on the opaque hex values below. They are computed values, rounded to 2 decimals. Re-measure in engine with a colour picker on a screenshot. This is required by UX Sec. 14 ("measure with art animated and static").

---

## 1. Direction A: "Kan ve Kemik" (Blood and Bone)

**Concept**: Bone-white type and parchment ornament on soot black, with HUD crimson as the only accent. The menu reads like an inscription carved into the bone walls of Araf, lit by the tower's torches.

### 1.1 Palette tokens

| Token | Hex | Use |
|---|---|---|
| `bg` | `#0C0A0A` | Camera clear / scrim base / FadeOverlay is pure `#000000` |
| `panel` | `#140F10` | Settings panel and dialog fill. Opaque (alpha 0.94 over art; contrast below assumes opaque value) |
| `text` | `#E8DCC0` | Menu items (normal), settings labels, dialog prompt, title |
| `text-dim` | `#A09684` | Group labels, values' units, version tag, unfocused rows in Settings |
| `focus` | `#FFF4DC` | Focused item text (brighter than `text`, warm white) |
| `accent` | `#DB143D` | HUD Araf crimson (0.86,0.08,0.24). Sigil core, focused slider fill, divider jewels, title underline. **Never used for text.** |
| `disabled` | `#5C5248` | Reserved (no disabled items on this screen) |
| `danger` | `#FF4D5E` | Error text "Seviye yüklenemedi", revert countdown number when <= 3 s (with text, never colour-only) |
| W-bg (worst case) | `#201818` | Worst-case left-column background for acceptance |

### 1.2 Computed contrast (WCAG)

| Foreground | on `bg` #0C0A0A | on `panel` #140F10 | on W-bg #201818 | Requirement | Result |
|---|---|---|---|---|---|
| `text` #E8DCC0 | 14.51 | 13.96 | 12.80 | 4.5 | Pass |
| `text-dim` #A09684 | 6.76 | 6.50 | 5.97 | 4.5 | Pass |
| `focus` #FFF4DC | 18.08 | 17.39 | 15.95 | 4.5 | Pass |
| `accent` #DB143D | 3.93 | 3.78 | 3.47 | 3.0 (non-text / large only) | Pass for sigil/fill; **fails 4.5 body text, so it is not used as text** |
| `danger` #FF4D5E | 6.09 | 5.86 | 5.37 | 4.5 | Pass |
| `disabled` #5C5248 | 2.59 | 2.49 | 2.29 | exempt (inactive UI) | n/a. Unused on this screen |

Greyscale focus check: normal `text` L = 0.72, `focus` L = 0.91. With the sigil and the underline bar (1.4) this is distinguishable in greyscale. The brightness step on its own would not be enough, which is why the shape cues exist.

Colour-vision note: under protanopia the crimson `accent` loses a lot of luminance. The sigil therefore always carries a **1-art-pixel `text`-coloured outline**, so its shape reads at >= 13:1 whatever colour vision the player has.

### 1.3 Typography

| Role | Font (licence) | Turkish coverage | Size @1080p | Notes |
|---|---|---|---|---|
| Title `PURGATOR` | **Grenze Gotisch** (SIL OFL 1.1, Google Fonts) | Ships a latin-ext subset (confirmed via Fontsource). Still run the glyph test string below | 132 px, weight 700, tracking +2% | Readable blackletter hybrid. Colour `text` with a 3 px `accent` underline rule (an ornament, not text). Shrink-to-fit in the title zone (UX 5.2) |
| Menu items | **Alegreya Sans SC** (OFL) | Large Latin coverage; **verify Turkish glyph coverage** with the test string | 44 px, Medium; focused 44 px Bold (scale 1.06) | Stored pre-cased (BAŞLA, AYARLAR, ÇIKIŞ) to avoid İ/ı casing bugs |
| Settings title (AYARLAR) | Grenze Gotisch | as above | 56 px | |
| Settings labels / values | Alegreya Sans SC | verify | 28 px Regular | Above the 24 px floor, with headroom for shrink-to-fit |
| Group labels (SES / KONTROL / GÖRÜNTÜ) | Alegreya Sans SC | verify | 24 px Bold, `text-dim`, tracking +12% | 24 px floor met |
| Dialog prompt / buttons | Alegreya Sans SC | verify | 32 px / 32 px | |
| Version tag | Alegreya Sans SC | verify | 24 px, `text-dim` | Floor is 24 px even for the lowest-priority text |

**Glyph test string** (must render with no fallback boxes in the TMP Font Asset): `PURGATOR BAŞLA AYARLAR ÇIKIŞ GERİ EVET HAYIR Ğğ Şş Çç İi Iı Öö Üü Müzik Görüntü Çözünürlük Fare Hassasiyeti`.
**TMP setup**: SDF atlas 2048x2048 (title) / 1024x1024 (UI). Use a custom character set of ASCII + `ÇçĞğİıÖöŞşÜü` + `%<>…`, padding 9 for the title and 5 for UI. Add a Dynamic fallback asset (OS fallback disabled) so a missing glyph shows as a glyph and never as a blank.
**Pixel alternative** (if the user wants a harder retro look on the UI font): **Pixelify Sans** (OFL; Fontsource lists 425 languages, **verify Turkish**). Use it at a minimum of 30 px for menu and settings text. It costs some legibility, so keep the title in Grenze Gotisch.

### 1.4 Focus sigil (A): "Kemik Mühür" (Bone Seal)
- **Shape**: a 9x11 art-pixel vertical **lozenge** (rotated square, slightly tall) with a 1-px vertical slit in the centre. Built as `text` outline, `accent` fill, `bg` slit. It reads as an eye or seal. Rendered at 27x33 px @1080p (x3).
- **Placement**: 16 px left of the focused item's text, vertically centred on the cap height. It snaps instantly to the new item (UX P7).
- **Second non-colour cue**: a **2-art-pixel-tall underline bar** (`accent` core with `text` ends, 6 px @1080p) under the focused item, as wide as the text. It is a 9-slice with fixed 2-px end caps.
- **Third cue**: `focus` text colour (L 0.91 vs 0.72) and Bold weight. Scale 1.06 only when reduce-motion is off.
- In greyscale, focus is identifiable from the sigil and the underline alone (UX 14 acceptance).

### 1.5 Button state visuals (A)

| State | Text | Sigil | Underline | Background | Other |
|---|---|---|---|---|---|
| Normal | `text`, Medium, scale 1.0 | hidden | hidden | none (transparent row, full-row hit area >= 56 px tall) | |
| Focus / Hover | `focus`, Bold, scale 1.06 (80 ms) | visible, steady (optional 0.5 Hz glow 85-100% alpha) | visible | none | identical for hover and keyboard (P2) |
| Pressed (80 ms) | `accent`-tinted shadow offset 3 px down, text moves 3 px down (1 art px) | sigil fill flips to `focus` for 80 ms | underline swaps to full `accent` | none | confirm SFX |
| Disabled (reserved) | `disabled`, Regular, plus a strike-through rule | hidden, never focusable | hidden | none | Not used on this screen. Continue is absent, not disabled (UX Sec. 6) |

Settings rows use the same states. A focused row also gets a `panel`-lighter row band, `#1E1718` at full row width. This is supplementary only; the sigil and underline still carry focus.

**Slider (P3)**: track `#2A2020`, 2 art px tall. Fill is `text-dim` (6.50:1 on panel) and becomes `accent` (3.78:1) when the row is focused. The thumb is a 3x5 art-px bone tick. The value always shows as `%` text.
**Cycle row (P4)**: `<` and `>` are pixel-art arrow glyphs in `text`. The focused row's arrows nudge 1 art px outward on change (static when reduce-motion is on).

### 1.6 Panels and dialog (A)
- **Ornament level**: medium-low. The frame is a thin bone border with small **vertebra corner pieces**. The panel never competes with the text.
- **Settings panel**: 9-slice frame, source texture 48x48 art px. Borders are 12 px each side (36 px @1080p). Fill is `panel` at alpha 0.94. The top edge has a centred crimson jewel ornament (separate Image, 15x7 art px). Group labels sit above a 1-art-px `text-dim` divider at 40% alpha.
- **Dialog (QuitConfirm / RevertPrompt)**: the same 9-slice in a heavier variant (2-px bone border plus inner 1-px `accent` hairline). The 10 s countdown is a numeric text in `text` 40 px, switching to `danger` at <= 3 s; the number itself is the information (UX Sec. 12).
- **DimOverlay**: `#000000` alpha 0.65 full screen.
- **Treatment**: the frames are drawn with dithered 2-tone shading (bone `#E8DCC0` and shadow `#7A6E5E`) and no anti-aliasing, point filtered.

### 1.7 Motion (A)

| Element | Default | Reduce-motion |
|---|---|---|
| Embers | 6-10/s from torch points in the right 55%. Rise 20-40 px/s (@1080p) with curl drift. Lifetime 4-7 s. Size 3 or 6 px (1-2 art px), square, point filtered. Colour over life `#FF8C2A` -> `#D8450F` -> alpha 0 | Prewarm, then `Pause()`: a frozen still frame (art stays populated but static) |
| Smoke | 2-3 large soft sprites drifting 6-10 px/s left-to-right in the lower third, alpha 0.10-0.18. Or scroll a noise RawImage uvRect at 0.004/s | Static (no scroll) |
| Torch glow | `ArtTorchGlow` additive Image, alpha modulated 0.85-1.0 by smoothed noise at <= 1.5 Hz. Low amplitude, never a flash | Fixed at 0.92 |
| Fades | First-load 600 ms ease-out; panel and dialog 200 ms; BAŞLA 600 ms ease-in (UX Sec. 10) | <= 100 ms or instant, per UX Sec. 10 |
| Sigil | Instant snap; focused text scale 1.0 -> 1.06 over 80 ms; optional 0.5 Hz glow | Snap only; no scale, no glow |
| Title | Static. Optional: the crimson underline brightens once on first-load fade end (300 ms) | Static |

All motion uses `Time.unscaledDeltaTime`. No element exceeds 3 luminance flashes per second (well under the flash threshold; the deferred flash warning is not triggered by the menu).

---

## 2. Direction B: "Kül ve Mum" (Ash and Candle)

**Concept**: Cold ash-blue iron type on deep blue-black, with candle and torch amber as the accent. The menu reads like an iron plaque on a cold tower wall, with a single candle as the light. The world's warm torchlight becomes the UI's focus colour.

### 2.1 Palette tokens

| Token | Hex | Use |
|---|---|---|
| `bg` | `#080A0E` | Scrim base; FadeOverlay pure `#000000` |
| `panel` | `#10141A` | Settings / dialog fill, alpha 0.94 (contrast uses opaque value) |
| `text` | `#C8D2DC` | Menu items, labels, prompt, title |
| `text-dim` | `#8A96A4` | Group labels, version tag, unfocused rows |
| `focus` | `#FFE6B8` | Focused item text (candlelit white) |
| `accent` | `#FF8C2A` | Torch amber (matches world torch #FF8C2A). Sigil flame, focused slider fill, rivets |
| `disabled` | `#4A525C` | Reserved |
| `danger` | `#FF5A4A` | Error text, countdown <= 3 s |
| W-bg (worst case) | `#181C24` | Worst-case left-column background |

### 2.2 Computed contrast (WCAG)

| Foreground | on `bg` #080A0E | on `panel` #10141A | on W-bg #181C24 | Requirement | Result |
|---|---|---|---|---|---|
| `text` #C8D2DC | 12.93 | 12.06 | 11.14 | 4.5 | Pass |
| `text-dim` #8A96A4 | 6.58 | 6.14 | 5.67 | 4.5 | Pass |
| `focus` #FFE6B8 | 16.28 | 15.18 | 14.03 | 4.5 | Pass |
| `accent` #FF8C2A | 8.52 | 7.95 | 7.34 | 4.5 | Pass (can be used as text too) |
| `danger` #FF5A4A | 6.43 | 5.99 | 5.54 | 4.5 | Pass |
| `disabled` #4A525C | 2.50 | 2.33 | 2.16 | exempt | n/a |

Greyscale focus check: `text` L = 0.64, `focus` L = 0.81, plus the sigil and rule. Amber `accent` keeps high luminance under all common colour-vision deficiencies, so the sigil survives better than A's crimson.

### 2.3 Typography

| Role | Font (licence) | Turkish coverage | Size @1080p | Notes |
|---|---|---|---|---|
| Title | **Cinzel** (OFL, Google Fonts) or **Cinzel Decorative** | **Verify Turkish glyph coverage** (latin-ext believed present, not confirmed) | 120 px, weight 700, tracking +6% | Roman inscription capitals, cold and monumental |
| Menu items | **Cinzel** 600 | verify | 40 px; focused Bold, scale 1.06 | Caps only. Its wide letterforms need the 14-char budget checked |
| Settings / dialog / labels | **Alegreya Sans SC** (OFL) | verify | 28 / 32 / 24 px | Same body font as A, which keeps the fallback chain simple |
| Version tag | Alegreya Sans SC | verify | 24 px `text-dim` | |

Same glyph test string and TMP setup as 1.3. Fallback if Cinzel fails the Turkish test: **Grenze** (non-Gotisch sibling, OFL, latin-ext) at the same sizes.

### 2.4 Focus sigil (B): "Mum Alevi" (Candle Flame)
- **Shape**: a 7x13 art-pixel **flame silhouette** above a 5x3 iron candle stub, with a 1-art-px dark core. Rendered at 21x48 px @1080p. A flame is a clearly different silhouette from anything else on screen.
- **Placement**: 16 px left of the focused text, baseline-aligned (the stub sits on the baseline).
- **Second cue**: a 1-art-px amber rule above and below the focused item text, at 60% of text width. These are "iron brackets" with rivet ends.
- **Third cue**: `focus` text plus Bold and scale 1.06.
- **Animation**: a 3-frame flame flicker sprite at 6 fps (looks fluid at x3 pixels). **Reduce-motion: frame 1 only.**

### 2.5 Button state visuals (B)

| State | Text | Sigil | Rules | Background |
|---|---|---|---|---|
| Normal | `text`, scale 1.0 | hidden | hidden | none |
| Focus / Hover | `focus`, Bold, 1.06 | flame visible (flicker or static) | amber brackets visible | faint amber radial glow behind the item, `#FF8C2A` at alpha 0.08 (decorative only) |
| Pressed (80 ms) | text 3 px down; colour `accent` | flame flares: frame 3 held for 80 ms | rules thicken to 2 art px | glow alpha 0.16 |
| Disabled (reserved) | `disabled` + strike rule | hidden | hidden | none |

Slider: track `#222A34`; fill `text-dim`, becoming `accent` when focused (7.95:1); the thumb is an iron rivet. Cycle arrows are iron chevrons in `text`.

### 2.6 Panels and dialog (B)
- **Ornament level**: low. It is **riveted iron plate**: flat `panel` fill, a 2-art-px border in `#3A4654` with 1-px highlight `#5A6878`, and 3x3 rivets at the corners and every 32 art px along the edges. 9-slice source 48x48, borders 12.
- Dialog: same plate plus a small candle in the top-centre notch (it reuses the sigil sprite at 50%, static).
- DimOverlay: `#000000` alpha 0.65.
- Shading is ordered-dither 2-tone, no AA, point filtered.

### 2.7 Motion (B)
Same ember, smoke, fade and reduce-motion rules as 1.7, with two differences. The sigil flame flicker (6 fps) also stops under reduce-motion. The ember colour is the same (world torches). That makes the right-side art warmer than the cool UI, which is the direction's intended contrast.

---

## 3. Recommendation

**Recommended: Direction A ("Kan ve Kemik").**

1. **Same-game continuity**: A reuses the HUD's Araf crimson `#DB143D` and near-black panel directly. The menu, the RunHUD and Araf-ready signalling then form one visual language. B's cold blue appears nowhere in the HUD or the world lighting.
2. **World fit**: bone parchment type matches the bone-walled Araf labyrinth and the gore tone. The warm palette sits naturally in the torch-lit, very-low-ambient scene. B puts a cool UI against warm art, which is attractive but reads more "cold cathedral" than "purgatory furnace".
3. **Hierarchy**: in A, crimson is rare (sigil, underline, jewels), so it marks focus strongly against the bone type. In B, amber appears in both the sigil and the embers, so the focus colour competes with the ambient motion in the right half.
4. **A's costs, and how they are handled**: crimson fails 4.5:1 as text (3.93:1), so it is restricted to non-text and large ornament, where 3:1 applies. Its protanopia luminance loss is covered by the bone outline on the sigil and the shape-based underline. B has the stronger raw accessibility margins (its amber passes as text at 7.95:1+). If the user later finds A too dark or too "red-on-red" in the real art, **Hybrid A+**, which adds B's candle-flame sigil to A's palette, is a cheap fallback.

The final decision belongs to the user.

---

## 4. Shared Asset Manifest

Legend: **Proc** = can be built procedurally in Unity for the placeholder (solid colour Images, gradients, TMP, built-in particle textures, a tiny generated Texture2D). Every row has a placeholder path, so **the menu is fully functional with zero final art**. "Final" = supplied later by the user (3D modeller) or an artist.

### 4.1 Fonts

| Asset | Source | Format | Atlas | Placeholder | Proc? |
|---|---|---|---|---|---|
| Title font (A: Grenze Gotisch / B: Cinzel) | Google Fonts, OFL | .ttf -> TMP SDF Font Asset | 2048x2048, SDF, padding 9, static custom charset + dynamic fallback | TMP default `LiberationSans SDF` with its dynamic fallback (Liberation Sans covers Turkish Latin-ext) | Yes (placeholder) |
| UI font (Alegreya Sans SC; optional Pixelify Sans) | Google Fonts, OFL | .ttf -> TMP SDF | 1024x1024, padding 5 | LiberationSans SDF | Yes (placeholder) |
| OFL licence texts | font packages | .txt in `Assets/Fonts/<Font>/OFL.txt` | n/a | n/a | n/a. Required for redistribution |

### 4.2 Background / scene art

| Asset | Dimensions | Format | Filter | Placeholder (procedural) | Final art note |
|---|---|---|---|---|---|
| `ArtSky` | 640x360 (art px), displayed full-bleed cover | PNG RGBA32, no mips | Point | Vertical gradient Image: top `#0C0A0A` -> horizon `#2A0E0A` (A) / `#0A1018` (B). Use a 1x64 generated gradient Texture2D | 21:9: extend horizontally to 840x360 or mirror-extend the edge columns, so there are no seams (UX 5.2) |
| `ArtTowerFar` | 640x360 with alpha | PNG RGBA32 | Point | Solid `#151012` stacked rectangles forming a stepped tower silhouette (UGUI Images) on the right 55% | Ideal fit for the user: **render the 3D tower in Unity at 640x360 to a RenderTexture, then bake to PNG** (low-poly, point-filtered, auto-consistent with the in-game look) |
| `ArtFog` | 640x120, tiling horizontally | PNG RGBA, alpha | Point (or Bilinear for a softer feel) | RawImage with a generated value-noise Texture2D (128x32), uvRect scroll | |
| `ArtTowerNear` | 640x360 alpha | PNG RGBA32 | Point | Darker silhouette rectangles plus 2 torch bracket squares | Same 3D-render bake |
| `ArtTorchGlow` | 64x64 per torch | PNG, additive material | Bilinear (soft) | Built-in `Default-Particle` sprite tinted `#FF8C2A`, alpha 0.4, additive UI material | |
| `LeftScrim` | 1x256 horizontal gradient, stretched | Generated Texture2D or PNG | Bilinear | Procedural: alpha 0.92 to 0.92 to 0 (0/50/62%) in `bg` colour | Stays procedural in the final build |
| Title logotype (optional) | n/a | n/a | n/a | TMP text `menu.title` | Deferred to art-director. If an image is used, text key alt is kept (UX 13) |

### 4.3 UI ornaments (pixel art, 360p grid, all Point / no mips / RGBA32 / Clamp)

| Asset | Art-px size | @1080p | 9-slice borders | Placeholder | Proc? |
|---|---|---|---|---|---|
| `Sigil_A_BoneSeal` | 9x11 | 27x33 | n/a | Generated Texture2D from a hard-coded 9x11 pixel mask (built at edit time, not runtime). Or Unity built-in `UISprite` rotated 45° and tinted `accent`, with an outline component | Yes |
| `Sigil_B_CandleFlame` (3 frames) | 7x16 each (strip 21x16) | 21x48 | n/a | Single frame from a pixel mask; or TMP glyph `▲` tinted amber as a stopgap | Yes |
| `FocusUnderline` | 16x2 | stretch x 6 | L2 R2 | Solid `accent` Image, 6 px tall | Yes |
| `PanelFrame_A` (bone/vertebrae) / `PanelFrame_B` (riveted iron) | 48x48 | 144x144 source scale | 12 all sides | Built-in `UISprite` (rounded 9-slice) tinted `panel`, plus an `Outline` component in `text-dim` at 2 px | Yes |
| `DialogFrame` (heavy variant) | 48x48 | | 12 | Same as panel plus a second inset Outline in `accent` | Yes |
| `PanelJewel_A` / `DialogCandle_B` | 15x7 / 7x16 | | n/a | omit, or a small `accent` square | Yes |
| `GroupDivider` | 8x1 tiling | stretch x 3 | L1 R1 | Solid Image `text-dim` alpha 0.4, 3 px tall | Yes |
| `SliderTrack` / `SliderFill` / `SliderThumb` | 8x2 / 8x2 / 3x5 | | L1 R1 | Solid Images (track, fill colours above); thumb = small solid rect | Yes |
| `CycleArrowL` / `CycleArrowR` | 5x7 | 15x21 | n/a | TMP text `<` `>` in the UI font | Yes |

### 4.4 Overlays

| Asset | Spec | Proc? |
|---|---|---|
| `DimOverlay` | Full-screen Image, `#000000` alpha 0.65, raycast target ON (blocks background clicks, P5) | Yes (final too) |
| `FadeOverlay` | Full-screen Image `#000000`, top canvas sort order, alpha animated, raycast ON while fading (input blocker, P7) | Yes (final too) |

### 4.5 Particle textures

| Asset | Dimensions | Format / filter | Placeholder | Proc? |
|---|---|---|---|---|
| `Ember` | 2x2 (1 art px each, square) | PNG RGBA, Point | Generated 2x2 white Texture2D, or the built-in `Default-ParticleSystem` material with a tiny start size. Square point-filtered dots read as pixel embers | Yes (final too) |
| `SmokePuff` | 64x64 soft | PNG RGBA, Bilinear | Built-in `Default-Particle` texture, alpha 0.12, large size (200-400 px) | Yes |
| Particle material | UI-compatible | URP `Particles/Unlit`, Additive (embers) / Alpha (smoke) | Built-in | Yes |

Implementation note for ui-programmer and unity-ui-specialist: UGUI does not sort ParticleSystems inside a Screen Space - Overlay canvas. Use a **Screen Space - Camera** canvas for the art and particle layers, with a dedicated UI camera and sorting layers, and keep the text canvas above it. Particle systems must not instantiate at runtime (zero-GC rule). Use prewarm and fixed `maxParticles` (embers <= 64, smoke <= 8).

### 4.6 UI SFX (routed via `UiAudioEvent`, P8; never AudioSource from UI)

| Event | Description | Length | Format | Placeholder |
|---|---|---|---|---|
| `Focus` | dry bone/iron tick, low volume | 40-80 ms | WAV 48 kHz 16-bit mono, Decompress On Load, preload | generate with a free sfxr-style tool (jsfxr/ChipTone, output royalty-free) or silence |
| `Confirm` | heavier knock + short low resonance | 150-250 ms | same | same |
| `Back` | muted reverse knock, lower pitch than Confirm | 120-200 ms | same | same |
| `SliderStep` | soft tick, pitch rises slightly with value | 30-60 ms | same | same |
| `VolumePreview` (per bus) | sample on the bus being adjusted (P3) | 300-500 ms | same | reuse Confirm on each bus |
| `DialogOpen` | low bell / chain rattle | 300-500 ms | same | reuse Confirm |
| `StartRun` | descending gong / door slam that rings into the 600 ms fade | 0.8-1.5 s | same | reuse Confirm, pitched down |
| `Error` | dull thud (load failure). Paired with the visible message | 200-300 ms | same | reuse Back |
| `MenuAmbience` (optional) | wind + distant fire drone loop, Music bus | 30-60 s loop | OGG/Vorbis stereo, Streaming | none (silence) |

Slider boundary: silent stop (UX Sec. 7.2). No asset needed.

### 4.7 Placeholder-first build order (menu fully functional before any final art)
1. TMP LiberationSans + palette tokens as a `MenuTheme` ScriptableObject (colour tokens, font refs, sizes). Switching A/B or A+ then becomes a data swap, which follows the data-driven rule.
2. Procedural `LeftScrim`, `DimOverlay`, `FadeOverlay`, solid panel and slider Images.
3. Generated-mask sigil and solid underline. All focus and state logic can be verified now (greyscale screenshot test).
4. Rectangle-silhouette tower, gradient sky, built-in particle embers and smoke.
5. Swap in OFL fonts after the glyph test passes.
6. Swap in pixel ornaments, then the baked 3D-render tower layers, as the user supplies them. No layout changes required.

---

## 5. Acceptance Additions (visual)
- [ ] Greyscale screenshot: the focused item is identifiable in the root menu, Settings and both dialogs.
- [ ] Sampled left-column colour at <= W-bg luminance at 3 ember positions and with smoke, in both reduce-motion states.
- [ ] Ornament pixels are evenly sized at 1280x720, 1920x1080, 2560x1440 and 3840x2160.
- [ ] The glyph test string renders with no fallback boxes in every TMP Font Asset used.
- [ ] No particles in the 0-48% width region.
- [ ] Screenshots retained in `production/qa/evidence/` per state (UX Sec. 6).

## 6. Open Items
- Direction choice A / B / A+ (user).
- Turkish glyph verification: Alegreya Sans SC, Cinzel and Pixelify Sans are unverified; Grenze Gotisch latin-ext is confirmed via Fontsource but still needs the in-engine test.
- Final title treatment (text vs logotype) remains deferred.
- Art bible: promote the chosen direction's tokens into one once approved.

Reference used for font subset confirmation: Fontsource Grenze Gotisch (https://fontsource.org/fonts/grenze-gotisch/about), Fontsource Pixelify Sans (https://fontsource.org/fonts/pixelify-sans/about).
