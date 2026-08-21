# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy", "pillow"]
# ///
# PROTOTYPE — throwaway (see README.md). Demonstrates the CURRENT branch behavior,
# nothing proposed: the Beat Push displaces the wall pattern by the same wall-tile
# amount per beat on every image. Wall view, slash vs ai2, both mirror modes, with
# a beat-pulse indicator. No per-image factor of any kind exists in this render.
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from render_wall import (
    FPS, BEAT_S, MIRROR_SCALE, PACE_MID, PAN_PER_BEAT, ROT_PER_BEAT,
    parse_layout, tile_geometry, init_weights, seed_map, rasterize_tilemap,
    load_rgb, sample, compose,
)

HERE = Path(__file__).resolve().parent
OUT = HERE / "out"

BEATS = 8
PUSH_PEAK = 7.0 * 0.27          # OnBeatPushStrength * typical gated Normalized Low
IMAGES = ["slash.png", "ai2.png"]


def push_frames(rgb, mode, nidx, nw, seeds, tilemap, n_frames):
    h, w = rgb.shape[:2]
    pos_x, pos_y = 0.37 * w, 0.37 * h
    angle = 0.0
    dt = 1.0 / FPS
    frames = []
    displaced = 0.0
    for f in range(n_frames):
        screen = sample(rgb, pos_x, pos_y, angle).reshape(-1, 3)
        buffer = (screen[nidx] * nw[:, :, None]).sum(axis=1)
        buffer = buffer[seeds]
        colors = np.vstack([[[0.0, 0.0, 0.0]], buffer])
        img = (np.clip(colors[tilemap], 0, 1) * 255).astype(np.uint8)

        # Beat-pulse indicator: the wire triangle, 1 on the beat, 0 halfway to the next.
        phase = (f * dt / BEAT_S) % 1.0
        tri = abs(1.0 - 2.0 * phase)
        pil = Image.fromarray(img)
        d = ImageDraw.Draw(pil)
        v = int(40 + 215 * tri)
        d.rectangle([6, 6, 26, 26], fill=(v, v, v), outline=(90, 90, 90))
        frames.append(np.asarray(pil))

        # Exact current motion law: one shared wall-space rate, no image term anywhere.
        pace = PACE_MID + PUSH_PEAK * tri
        rate = PAN_PER_BEAT * pace * MIRROR_SCALE[mode] / BEAT_S
        pos_x += rate * dt
        pos_y += rate * dt
        angle += ROT_PER_BEAT / PAN_PER_BEAT * rate * dt
        displaced += rate * dt
    return frames, displaced / BEATS


def main() -> None:
    layout = parse_layout()
    centers, polys = tile_geometry(layout["Mesh"])
    nidx, nw = init_weights(centers)
    seeds = {m: seed_map(layout["shapes"][m]) for m in ("mirror2", "mirror10")}
    tilemap = rasterize_tilemap(polys)
    n_frames = int(BEATS * BEAT_S * FPS)

    grids, labels = [], []
    for mode in ("mirror2", "mirror10"):
        for name in IMAGES:
            rgb = load_rgb(name)
            frames, per_beat = push_frames(rgb, mode, nidx, nw, seeds[mode], tilemap, n_frames)
            grids.append(frames)
            labels.append(f"{name.removesuffix('.png')} — {mode} — {per_beat:.2f} wall px/beat/axis")
            print(f"{name:<12} {mode:<9} displacement {per_beat:.4f} wall px/beat/axis")
    caption = ("CURRENT BEHAVIOR — Beat Push firing (white square = wire beat pulse). "
               "Identical wall displacement per beat on every image; no image term exists.")
    frames = compose(grids, labels, caption, cols=2)
    path = OUT / "push_current_wall.gif"
    frames[0].save(path, save_all=True, append_images=frames[1:],
                   duration=int(1000 / FPS), loop=0)
    print(f"wrote {path.name} ({path.stat().st_size / 1e6:.1f} MB)")


if __name__ == "__main__":
    main()
