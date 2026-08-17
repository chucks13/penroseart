# PROTOTYPE — throwaway. Do not ship anything in this folder.

Desk-wall prototype that settled #150's Sync-standardization question (2026-08-17).

## The question

> Can Kscope's Sync settings be made to read more consistently across the image pool
> and both mirror layouts than the landed wall-units-per-beat design already does?

## The verdict

**No — the landed design is the answer.** The requirement is that the Beat Push
displaces the wall pattern by the same number of wall tiles per beat on every image;
the branch code already guarantees it (`render_push_demo.py` measures 24.89 px/beat/axis
on Mirror2 and 12.45 on Mirror10 for slash and ai2 identically — no image term exists
in the motion law). Two candidate "improvements" were built here and falsified at the
desk before any C# was written:

- **Per-image speed calibration** (motion scaled by measured content): visibly
  un-equalizes progression — ai2 races. Rejected at the desk.
- **Feature-scale normalization** (resample outlier images toward one feature-scale
  band): upscaled small images become same-color fields and the look changes. Rejected.

Byproduct kept: `scorecard.py` ranks the pool by measured kaleidoscope richness
(wedge color variety) — objective input for the parked texture-curation item.

## Contents

- `measure.py` — per-image correlation length L, contrast, factor tables (`out/measure.log`).
- `render.py` — v1: pre-mirror 50×22 buffer pans (superseded by the wall views).
- `render_wall.py` — full display-path port: sampling with rotation →
  ConvertScreenBuffer 4-nearest weighted tile mapping → Mirror2/Mirror10 group
  replication → real rhomb geometry from `penrose_layout.txt`.
- `render_wall_norm.py` — the rejected feature-scale normalization, kept as the record.
- `render_push_demo.py` — the current behavior's Beat Push, displacement measured.
- `scorecard.py` — full-pool richness ranking (`out/scorecard.log`).

## Run

    uv run measure.py          # writes out/measure.json (needed by the renderers)
    uv run render.py
    uv run render_wall.py
    uv run render_wall_norm.py
    uv run render_push_demo.py
    uv run scorecard.py

GIFs and inspection PNGs are not committed (regenerable, ~60 MB); run the scripts,
then open `out/viewer.html` in a normal browser — CMUX webviews don't animate GIFs.
Mono images render as raw grayscale here; the real effect palette-maps them.
