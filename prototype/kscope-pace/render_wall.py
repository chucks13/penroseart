# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy", "pillow"]
# ///
# PROTOTYPE — throwaway (see README.md). Ports Kscope's FULL display path:
# sampling (with rotation) -> ConvertScreenBuffer tile mapping -> Mirror2/Mirror10
# group replication -> real Penrose rhomb geometry. Renders wall-view GIFs so the
# kaleidoscope LOOK — not the pre-mirror buffer — is what gets judged.
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUT = HERE / "out"

# ---- effect-layout frame (Penrose.Bounds doc: size (50,22) centered at zero) ----
BUF_W, BUF_H = 50, 22
MIN_X, MIN_Y = -25.0, -11.0
FULL_SCALE = 1.0 / 140.0
GAP_SCALE = 0.9
NEIGHBOR_COUNT = 4

PX_PER_UNIT = 10                      # wall-view raster scale
BPM = 120.0
BEAT_S = 60.0 / BPM
FPS = 15
BEATS = 8

PACE_MID = 1.125
PAN_PER_BEAT = 6.0
ROT_PER_BEAT = 0.1
MIRROR_SCALE = {"mirror2": 2.0, "mirror10": 1.0}
RAIL_LO, RAIL_HI = 0.4, 2.5

PANELS = ["slash.png", "smiley.png", "tech.png", "abstract1.png", "crystal.png", "ai2.png"]
ALPHAS = [0.0, 0.75]
LABEL_H = 18
GUTTER = 2


def parse_layout() -> dict:
    text = (REPO / "Assets/StreamingAssets/penrose_layout.txt").read_text()
    text = re.sub(r"^\s*//.*$", "", text, flags=re.M)
    return json.loads(text)


def tile_geometry(mesh: list[float]) -> tuple[np.ndarray, list[list[tuple[float, float]]]]:
    """Centers and gap-shrunk triangle polygons in effect-layout units (y flipped)."""
    m = np.asarray(mesh, dtype=np.float64).reshape(-1, 3, 2)   # 1800 triangles
    m = m * FULL_SCALE
    m[:, :, 1] *= -1.0
    # GenerateTiles: center = midpoint of the first triangle's first/third verts.
    tri0 = m[0::2]
    centers = (tri0[:, 0, :] + tri0[:, 2, :]) / 2.0
    # GenerateMesh gap: shrink each triangle toward its (a+c)/2 midpoint.
    polys = []
    for k in range(len(m) // 2):
        tile_polys = []
        for tri in (m[2 * k], m[2 * k + 1]):
            middle = (tri[0] + tri[2]) / 2.0
            shrunk = middle + (tri - middle) * GAP_SCALE
            tile_polys.append([(x, y) for x, y in shrunk])
        polys.append(tile_polys)
    return centers, polys


def init_weights(centers: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Port of ScreenEffect.InitWeights: 4 nearest screen-grid points per tile,
    weight = distance / total (the shipped weighting, quirks included)."""
    gx = MIN_X + np.arange(BUF_W)          # xSpacing = 50/50 = 1
    gy = MIN_Y + np.arange(BUF_H)
    gpx, gpy = np.meshgrid(gx, gy)          # (H, W)
    gpos = np.stack([gpx.ravel(), gpy.ravel()], axis=1)          # index = y*W + x
    d = np.linalg.norm(centers[:, None, :] - gpos[None, :, :], axis=2)   # (900, 1100)
    idx = np.argsort(d, axis=1)[:, :NEIGHBOR_COUNT]
    nd = np.take_along_axis(d, idx, axis=1)
    w = nd / nd.sum(axis=1, keepdims=True)
    return idx, w


def seed_map(packed: list[int]) -> np.ndarray:
    """Tile -> its mirror group's first tile (Draw copies buffer[group[0]] to the group)."""
    p = np.asarray(packed, dtype=np.int64)
    out = np.arange(900, dtype=np.int64)
    for g in range(p[0]):
        ptr = p[g + 1]
        tiles = p[ptr + 1 : ptr + 1 + p[ptr]]
        out[tiles] = tiles[0]
    return out


def rasterize_tilemap(polys) -> np.ndarray:
    """One-time tile-index map so each frame is a pure color lookup."""
    w, h = int(50 * PX_PER_UNIT), int(22 * PX_PER_UNIT)
    img = Image.new("I", (w, h), 0)
    draw = ImageDraw.Draw(img)
    for k, tris in enumerate(polys):
        for tri in tris:
            pts = [((x - MIN_X) * PX_PER_UNIT, (11.0 - y) * PX_PER_UNIT) for x, y in tri]
            draw.polygon(pts, fill=k + 1)
    return np.asarray(img, dtype=np.int64)


def load_rgb(name: str) -> np.ndarray:
    for pool in ("color", "mono"):
        p = REPO / "Assets/StreamingAssets/images" / pool / name
        if p.exists():
            return np.asarray(Image.open(p).convert("RGB"), dtype=np.float64) / 255.0
    raise FileNotFoundError(name)


def sample(rgb: np.ndarray, pos_x: float, pos_y: float, angle: float) -> np.ndarray:
    """Exact Draw() port with rotation: center, rotate, offset, abs, mirror-repeat."""
    h, w = rgb.shape[:2]
    xs = np.arange(BUF_W, dtype=np.float64) - BUF_W / 2
    ys = np.arange(BUF_H, dtype=np.float64) - BUF_H / 2
    gx, gy = np.meshgrid(xs, ys)
    c, s = np.cos(angle), np.sin(angle)
    x2 = np.abs(c * gx - s * gy + pos_x)
    y2 = np.abs(s * gx + c * gy + pos_y)
    xp = x2.astype(np.int64) // w
    yp = y2.astype(np.int64) // h
    x2 = x2 % w
    y2 = y2 % h
    x2 = np.where(xp & 1 != 0, (w - 1) - x2, x2)
    y2 = np.where(yp & 1 != 0, (h - 1) - y2, y2)
    return rgb[y2.astype(np.int64), x2.astype(np.int64)]        # (H, W, 3)


def wall_frames(rgb, factor, mode, nidx, nw, seeds, tilemap, n_frames):
    """Full pipeline per frame; returns frames plus a wedge-variety statistic."""
    h, w = rgb.shape[:2]
    pos_x, pos_y = 0.37 * w, 0.37 * h
    angle = 0.0
    motion_scale = PACE_MID * MIRROR_SCALE[mode] * factor / BEAT_S
    dt = 1.0 / FPS
    frames, variety = [], []
    for _ in range(n_frames):
        screen = sample(rgb, pos_x, pos_y, angle).reshape(-1, 3)     # index = y*W + x
        buffer = (screen[nidx] * nw[:, :, None]).sum(axis=1)         # ConvertScreenBuffer
        buffer = buffer[seeds]                                       # mirror replication
        variety.append(float(buffer.std(axis=0).mean()))
        colors = np.vstack([[[0.0, 0.0, 0.0]], buffer])              # 0 = background
        img = (np.clip(colors[tilemap], 0, 1) * 255).astype(np.uint8)
        frames.append(img)
        pos_x += motion_scale * PAN_PER_BEAT * dt
        pos_y += motion_scale * PAN_PER_BEAT * dt
        angle += motion_scale * ROT_PER_BEAT * dt
    return frames, float(np.mean(variety))


def compose(grids, labels, caption, cols):
    n_frames = len(grids[0])
    rows = (len(grids) + cols - 1) // cols
    ph, pw = grids[0][0].shape[:2]
    total_w = cols * pw + (cols - 1) * GUTTER
    total_h = rows * (ph + LABEL_H) + (rows - 1) * GUTTER + LABEL_H
    out = []
    for f in range(n_frames):
        canvas = Image.new("RGB", (total_w, total_h), (14, 14, 14))
        draw = ImageDraw.Draw(canvas)
        draw.text((4, 3), caption, fill=(220, 220, 220))
        for i, frames in enumerate(grids):
            r, c = divmod(i, cols)
            x0 = c * (pw + GUTTER)
            y0 = LABEL_H + r * (ph + LABEL_H + GUTTER)
            canvas.paste(Image.fromarray(frames[f]), (x0, y0))
            draw.text((x0 + 4, y0 + ph + 2), labels[i], fill=(180, 180, 180))
        out.append(canvas)
    return out


def main() -> None:
    layout = parse_layout()
    centers, polys = tile_geometry(layout["Mesh"])
    print(f"tile centers x [{centers[:,0].min():.2f}, {centers[:,0].max():.2f}] "
          f"y [{centers[:,1].min():.2f}, {centers[:,1].max():.2f}] (bounds ±25/±11)")
    nidx, nw = init_weights(centers)
    seeds = {m: seed_map(layout["shapes"][m]) for m in ("mirror2", "mirror10")}
    for m in ("mirror2", "mirror10"):
        p = layout["shapes"][m]
        print(f"{m}: {p[0]} groups, {len(np.unique(seeds[m]))} unique seed tiles")
    tilemap = rasterize_tilemap(polys)

    meta = json.loads((OUT / "measure.json").read_text())
    l_ref = meta["l_ref"]
    l_by_name = {img["name"]: img["L"] for img in meta["images"]}
    images = {name: load_rgb(name) for name in PANELS}
    n_frames = int(BEATS * BEAT_S * FPS)

    stats = {}
    for alpha in ALPHAS:
        for mode in ("mirror2", "mirror10"):
            grids, labels = [], []
            for name in PANELS:
                factor = float(np.clip((l_by_name[name] / l_ref) ** alpha, RAIL_LO, RAIL_HI))
                frames, variety = wall_frames(images[name], factor, mode,
                                              nidx, nw, seeds[mode], tilemap, n_frames)
                grids.append(frames)
                labels.append(f"{name.removesuffix('.png')}  x{factor:.2f}")
                if alpha == 0.0:
                    stats[(name, mode)] = variety
            caption = (f"WALL VIEW  {mode}  alpha={alpha}  "
                       f"({BPM:.0f} BPM, Mid energy, rotation on, no push; alpha=0 is today)")
            frames = compose(grids, labels, caption, cols=3)
            path = OUT / f"wall_{mode}_alpha_{alpha:.2f}.gif"
            frames[0].save(path, save_all=True, append_images=frames[1:],
                           duration=int(1000 / FPS), loop=0)
            print(f"wrote {path.name} ({path.stat().st_size / 1e6:.1f} MB)")

    print("\nwedge color variety at alpha=0 (mean per-frame tile-color std; "
          "higher = more visible kaleidoscope structure):")
    for name in PANELS:
        m2, m10 = stats[(name, "mirror2")], stats[(name, "mirror10")]
        print(f"  {name:<16} mirror2 {m2:.3f}   mirror10 {m10:.3f}")


if __name__ == "__main__":
    main()
