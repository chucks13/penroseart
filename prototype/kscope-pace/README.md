# PROTOTYPE — throwaway. Do not ship anything in this folder.

Desk-wall investigation for ticket #150, Phase D: Kscope Fill definition across
the complete image pool and both mirror layouts. It extends the earlier
Sync-standardization prototype; the previous question and its rejected candidates
remain at the end of this file.

## The question

The black-and-white Fill identity is settled. This prototype asks which images lose
definition under that treatment, whether the source images or gray map cause the
loss, and whether candidate whole-image edits or alternative black-and-white maps
restore definition at the desk.

A Fill is a Synced Mode fact served by `BeatManager.Fill` through BeatManager's
Data Surface; Standalone Mode never reports one (`CONTEXT.md`, **Fill** lines
161–163, **BeatManager** lines 183–185, **Data Surface** lines 187–189, and
**Standalone Mode / Synced Mode** lines 215–217). The prototype simulates Fill
active with a flag and never reads OSC. The imported wire lane is documented at
`docs/osc-client-contract.md`, **`/rave/onair/fill_state`** lines 504–536; this
prototype does not alter or reinterpret it.

## Runtime path modeled

`render_wall.py` retains the full earlier wall path:

1. source-image sampling with rotation and mirror-repeat;
2. `ConvertScreenBuffer` four-nearest distance-weighted Tile mapping;
3. Mirror2 or Mirror10 group replication from each group's first Tile; and
4. the real rhomb geometry from `penrose_layout.txt`.

The Fill port models the treatment Kscope ran when this prototype was measured: a
mirror group's color started at `buffer[group[0]]`; while `beatManager.Fill.Active`,
it was converted RGB→HSV, `v = (h + s + v) % 1`, fully desaturated with `s = 0`,
converted back to RGB, and copied to the group — the modulo sum was there to
“assure there is brightness variation.” The runtime has since replaced that
treatment: Kscope now drains color across the whole buffer while holding Rec.709
relative luminance, with the live `FillContrast` knob pivoting the gray on mid-gray
(`Assets/effects/Kscope.cs`, the Fill pass in `Draw`). The Fill scorecards here
measure the superseded treatment, not the current one.

Mono images model `Assets/effects/Kscope.cs:600–608`: source red supplies the
palette coordinate and final brightness; the live palette supplies hue and
saturation; Synced Mode applies the `PaletteSaturationFloor` of 0.3. The palette
parser and interpolation port `AnimPalette`/`GPalette.read(i, true)` from
`Assets/core/helpers/GPalette.cs:603–637`, `674–680`, and `487–518`. Human renders
hold the real scene palette `bhw1_24_gp`; the scorecard repeats the exact Fill
measurement over all 16 steady scene palettes for each mono image.

Deliberate simplifications:

- palette cross-fades are represented by their 16 steady endpoints, not every
  intermediate pair/time;
- Beat Hue is fixed at zero; and
- the color pool's stochastic one-in-three channel-swap Roll
  (`Kscope.cs:440–452`) is skipped.

Those choices make every paired panel deterministic and isolate Fill. They also
mean mono palette-transition moments, beat-driven hue offsets, and channel-swapped
color variants remain residual wall risks.

## Measurements

`measure.py` covers all 14 current source images. Adjacent source texels are split
by RMS RGB change into:

- **flat**: at most 2.5 code values per channel;
- **gentle shading**: above flat and below 0.08 in normalized RGB; and
- **hard edge**: at least 0.08.

**Shading share** is gentle changes divided by all non-flat changes. It separates
an image with broad gradients from one whose changes are mostly hard steps. The
older decorrelation length `L` and unrelated-texel contrast remain in the table.

`scorecard.py` then runs four beats of every image × Mirror2/Mirror10 through the
wall path. It scores Neighbor boundaries between distinct mirror groups:

- **collapse %**: boundaries visible before Fill (OKLab Δ ≥ 0.08) that become
  near-identical after Fill (Δ < 0.03);
- **contrast retention %**: post/pre OKLab contrast across those boundaries;
- **brightness inversion %**: boundaries with a stable normal-lightness order
  (OKLab L difference ≥ 0.03) whose order reverses under Fill;
- **wrap %**: sampled group colors where `h+s+v ≥ 1` before modulo; and
- **gray bins**: occupied width-0.05 gray bins per frame.

The same boundaries are measured under two candidate controls: grayscale from HSV
value and from Rec.709 relative luminance. Both preserve the settled black-and-white
identity and per-group brightness variation. They are comparison evidence, not
recommendations to ship.

The scorecard also tests bounded whole-image candidates: global hue rotations and
saturation scales for color sources, value gamma, 2–98% value autocontrast, and a
soft ±11% value texture. It reports and renders a candidate only when mean collapse
falls by at least five percentage points across both layouts (and all scene palettes
for mono). Any benefit must be judged beside its normal-color cost.

## Desk findings

Image character alone does not explain the measured loss. Shading share correlates
*positively* with exact-map collapse in this pool (Pearson +0.594; rank +0.618),
rather than flat/hard character predicting the loss. `slash.png`, the only source
with 0% shading share, loses 0.74% of visible Mirror2 boundaries and 0.52% in
Mirror10; `abstract1.png`, which is 69.4% locally flat, loses 0% in either layout.
The highest-loss sources are gradient-rich or mixed: `anstract3.png` 33.58/38.74%,
`ball.png` 29.84/32.71%, `abstract2.png` 22.52/34.23%, `fingermesh.png`
23.60/28.68%, `lattice.png` 18.21/32.65%, and `ai2.png` 18.53/29.12%
(Mirror2/Mirror10). The source-character hypothesis is therefore not supported as
a pool-wide cause, though source color relationships still determine which
boundaries the gray map collapses.

The gray mapping is the stronger mechanism in this desk model. Exact modulo gray
collapses 16.25% of pre-visible boundaries across the 28 image/layout cases. The
HSV-value control collapses 4.42% and Rec.709 luminance 1.87%. The exact map also
reverses normal brightness ordering on many surviving boundaries, while its modulo
wrap is exercised by 85.89% of Mirror2 and 87.87% of Mirror10 sampled groups. A
wrap is exposure to the discontinuity, not proof that a particular boundary failed.

Mirror10 averages 18.07% collapse versus Mirror2's 14.42%. The largest layout
penalties are `lattice.png` (+14.44 points), `abstract2.png` (+11.71), and
`ai2.png` (+10.59). Mono remains palette-sensitive despite preserving source
brightness: across the 16 steady scene palettes, `crystal.png` ranges 0–42.06%
collapse and `latticebw.png` 0–49.65%. The fixed `bhw1_24_gp` viewer cases are
8.37/9.61% and 11.63/11.05%, respectively.

Six whole-image candidates cleared the five-point reporting threshold. These are
best-in-tested-set desk comparisons, not ship recommendations:

| image | candidate | mean exact collapse | cost |
| --- | --- | ---: | --- |
| `abstract2.png` | add ±11% soft texture shading | 28.38% → 14.49% | invents texture structure |
| `ai2.png` | global hue +60° | 23.83% → 4.53% | changes normal color identity |
| `anstract3.png` | saturation ×0.5 | 36.16% → 12.11% | flattens normal saturation |
| `ball.png` | saturation ×0.5 | 31.27% → 16.48% | flattens normal saturation |
| `fingermesh.png` | saturation ×0.5 | 26.14% → 13.80% | flattens normal saturation |
| `lattice.png` | 2–98% value autocontrast | 25.43% → 7.79% | re-authors the tonal range |

No tested whole-image edit cleared five points for the other eight images. The
viewer includes both normal and Fill panels so the maintainer can judge whether any
measured rescue is worth its ordinary-look cost.

## Regenerate

Run from this directory, in this order:

```bash
uv run measure.py | tee out/measure.log
uv run scorecard.py | tee out/fill_scorecard.log
uv run render_wall.py | tee out/render_wall.log
```

The scripts write:

- `out/measure.json` — complete-pool image-character data;
- `out/fill_scorecard.json` — all image/layout metrics, mono palette ranges,
  candidate audit, and selected source-image candidates;
- `out/fill_*.gif` — 28 normal/exact-Fill pairs, one for every image/layout;
- `out/check_mapping_*.png` — 28 normal/exact/value/luminance comparisons;
- `out/check_pool_*.png` — baseline/candidate normal/Fill comparisons where the
  candidate cleared the reporting threshold; and
- `out/viewer.html` — the human view, ordered as image character, Fill scorecard,
  every normal-vs-Fill animation, pool-edit candidates, and gray-map candidates.

Open `out/viewer.html` in a normal browser; CMUX webviews do not animate GIFs. The
current viewer artifacts total about 91.6 MB including retained outputs from the
earlier question. GIF and inspection PNG batches are regenerable and ignored.

## Earlier Sync-standardization question

The earlier desk result remains settled: the landed wall-units-per-beat design is
the answer. `render_push_demo.py` measures the same displacement for small and large
source images because the motion law has no image term. Per-image speed calibration
made `ai2.png` race, and feature-scale normalization changed small images into
same-color fields. The retained historical scripts are:

- `render.py` — pre-wall 50×22 buffer views;
- `render_wall_norm.py` — rejected feature-scale normalization; and
- `render_push_demo.py` — landed Beat Push displacement measurement.
