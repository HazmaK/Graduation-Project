# Combat HUD — sampled contrast (2026-11-12)

Measured from 1920x1080 Play Mode captures in this folder (TestLevel_Gauntlet). Text colour = mean of the
brightest 3% pixels in the label box (darkest 3% for soot-on-red chips); background = the plate/chip pixel
at the 25th/50th luminance percentile. Anti-aliasing pulls sampled text toward the background, so these are
lower bounds on authored contrast. Greyscale copies: `combat-hud-0*-grey.png`.

| Element | Capture | Ratio | Requirement | Result |
|---|---|---|---|---|
| Health number | 01 | 10.95:1 | 3:1 (large) | PASS |
| Dash label | 01 | 9.17:1 | 4.5:1 | PASS |
| Weapon name | 01 | 8.96:1 | 4.5:1 | PASS |
| Ammo count | 01 | 10.96:1 | 3:1 (large) | PASS |
| Araf status | 01 | 9.17:1 | 4.5:1 | PASS |
| Health number under low-health vignette | 03 | 5.27:1 | 3:1 (large) | PASS |
| True-death title | 04 | 15.05:1 | 3:1 (large) | PASS |
| True-death button | 04 | 17.50:1 | 4.5:1 | PASS |
| Araf warning chip (soot on red) | 06 | 4.77:1 | 4.5:1 | PASS (narrow) |
| Low-health "!" glyph chip under vignette | 03 | 3.22:1 | 3:1 (non-text graphic, WCAG 1.4.11) | PASS as graphic; weak — the OnGUI vignette tints it |

Not measured: 720p / 4K / 16:10 / 21:9 captures, bright-fire and muzzle-flash backgrounds (Gauntlet lighting is dark).
Script: sampled with a PIL script (session scratchpad), boxes chosen by hand from the captures.
