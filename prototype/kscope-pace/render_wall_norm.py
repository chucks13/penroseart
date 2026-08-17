# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy", "pillow"]
# ///
# PROTOTYPE — throwaway (see README.md). Tests FEATURE-SCALE normalization:
# each image's measured correlation length L is pulled into one authored band at
# load (box-average down for too-smooth, nearest up for too-fine, untouched when
# in band). Settings stay identical everywhere; no per-image speed factor exists.
# Renders wall-view GIFs to compare against the 1:1 baseline (wall_*_alpha_0.00).
import json
from pathlib import Path

import numpy as np
from PIL import Image

from measure import correlation_length, mad_curve, plateau
from render_wall import (
    FPS, BEATS, BEAT_S, PANELS, LABEL_H, GUTTER, MIRROR_SCALE,
    PACE_MID, PAN_PER_BEAT, ROT_PER_BEAT,
    parse_layout, tile_geometry, init_weights, seed_map, rasterize_tilemap,
    load_rgb, wall_frames, compose,
)

HERE = Path(__file__).resolve().parent
OUT = HERE / "out"

# Authored knobs of the normalization — global, image-independent.
TARGET_L = 4.0            # pool geometric mean measured at 1:1
BAND_LO, BAND_HI = 2.5, 8.0
MAX_UP, MAX_DOWN = 4.0, 16.0


def normalize(rgb: np.ndarray, l_measured: float) -> tuple[np.ndarray, float, str]:
    """Pull an out-of-band image's feature scale to TARGET_L; leave in-band images alone."""
    if BAND_LO <= l_measured <= BAND_HI:
        return rgb, 1.0, "in band"
    scale = TARGET_L / l_measured           # >1 zooms in (upscale), <1 zooms out
    scale = float(np.clip(scale, 1.0 / MAX_DOWN, MAX_UP))
    h, w = rgb.shape[:2]
    nw, nh = max(2, round(w * scale)), max(2, round(h * scale))
    img = Image.fromarray((rgb * 255).astype(np.uint8))
    resampled = img.resize((nw, nh), Image.NEAREST if scale > 1 else Image.BOX)
    out = np.asarray(resampled, dtype=np.float64) / 255.0
    return out, scale, ("upscaled" if scale > 1 else "box-averaged down")


def main() -> None:
    layout = parse_layout()
    centers, polys = tile_geometry(layout["Mesh"])
    nidx, nw_ = init_weights(centers)
    seeds = {m: seed_map(layout["shapes"][m]) for m in ("mirror2", "mirror10")}
    tilemap = rasterize_tilemap(polys)

    meta = json.loads((OUT / "measure.json").read_text())
    l_by_name = {img["name"]: img["L"] for img in meta["images"]}

    prepared, labels = {}, {}
    print(f"target L {TARGET_L}, band [{BAND_LO}, {BAND_HI}]")
    for name in PANELS:
        rgb = load_rgb(name)
        norm, scale, how = normalize(rgb, l_by_name[name])
        i = norm.mean(axis=2)
        l_after = correlation_length(mad_curve(i), plateau(i))
        h0, w0 = rgb.shape[:2]
        h1, w1 = norm.shape[:2]
        print(f"  {name:<16} L {l_by_name[name]:>6} -> {l_after:5.2f}   "
              f"{w0}x{h0} -> {w1}x{h1}   x{scale:.2f} ({how})")
        prepared[name] = norm
        labels[name] = f"{name.removesuffix('.png')}  x{scale:.2f} {how}"

    n_frames = int(BEATS * BEAT_S * FPS)
    stats = {}
    for mode in ("mirror2", "mirror10"):
        grids, panel_labels = [], []
        for name in PANELS:
            frames, variety = wall_frames(prepared[name], 1.0, mode,
                                          nidx, nw_, seeds[mode], tilemap, n_frames)
            grids.append(frames)
            panel_labels.append(labels[name])
            stats[(name, mode)] = variety
        caption = (f"WALL VIEW  {mode}  FEATURE-SCALE NORMALIZED  "
                   f"(same settings everywhere: Pan 6, Mid energy, rotation on, no push)")
        frames = compose(grids, panel_labels, caption, cols=3)
        path = OUT / f"wall_{mode}_norm.gif"
        frames[0].save(path, save_all=True, append_images=frames[1:],
                       duration=int(1000 / FPS), loop=0)
        print(f"wrote {path.name} ({path.stat().st_size / 1e6:.1f} MB)")

    print("\nwedge color variety, normalized (compare render_wall.log alpha=0 numbers):")
    for name in PANELS:
        print(f"  {name:<16} mirror2 {stats[(name, 'mirror2')]:.3f}   "
              f"mirror10 {stats[(name, 'mirror10')]:.3f}")


if __name__ == "__main__":
    main()
