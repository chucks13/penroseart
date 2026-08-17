# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy", "pillow"]
# ///
# PROTOTYPE — throwaway (see README.md). Full-pool scorecard for the parked
# texture-pool-curation item: per image, measured feature scale (L), contrast,
# and wedge color variety on the desk wall under the CURRENT motion law.
import json
from pathlib import Path

from render_wall import (
    FPS, BEAT_S, parse_layout, tile_geometry, init_weights, seed_map,
    rasterize_tilemap, load_rgb, wall_frames,
)

HERE = Path(__file__).resolve().parent
OUT = HERE / "out"
BEATS = 4


def main() -> None:
    layout = parse_layout()
    centers, polys = tile_geometry(layout["Mesh"])
    nidx, nw = init_weights(centers)
    seeds = {m: seed_map(layout["shapes"][m]) for m in ("mirror2", "mirror10")}
    tilemap = rasterize_tilemap(polys)
    n_frames = int(BEATS * BEAT_S * FPS)

    meta = json.loads((OUT / "measure.json").read_text())
    rows = []
    for img in meta["images"]:
        name = img["name"]
        rgb = load_rgb(name)
        _, v2 = wall_frames(rgb, 1.0, "mirror2", nidx, nw, seeds["mirror2"], tilemap, n_frames)
        _, v10 = wall_frames(rgb, 1.0, "mirror10", nidx, nw, seeds["mirror10"], tilemap, n_frames)
        rows.append((name, img["pool"], f"{img['w']}x{img['h']}", img["plateau"], img["L"], v2, v10))

    rows.sort(key=lambda r: r[5] + r[6], reverse=True)
    hdr = f"{'image':<16}{'pool':<7}{'size':<10}{'contrast':<10}{'L':<8}{'variety m2':<12}{'variety m10':<12}"
    print(hdr)
    print("-" * len(hdr))
    for name, pool, size, plat, l, v2, v10 in rows:
        print(f"{name:<16}{pool:<7}{size:<10}{plat:<10}{l:<8}{v2:<12.3f}{v10:<12.3f}")


if __name__ == "__main__":
    main()
