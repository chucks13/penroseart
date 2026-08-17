# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy", "pillow"]
# ///
# PROTOTYPE — throwaway (see README.md). Ports Kscope's Draw() sampling and renders
# side-by-side pace GIFs so the calibration can be judged by eye before any C# exists.
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUT = HERE / "out"

BUF_W, BUF_H = 50, 22            # Kscope screen buffer
SCALE = 6                        # nearest-neighbor upscale for viewing
LABEL_H = 18
GUTTER = 2

BPM = 120.0
BEAT_S = 60.0 / BPM
FPS = 15
BEATS = 12
PACE_MID = 1.125                 # Mid Energy on the current 0.75..1.5 range
PAN_PER_BEAT = 6.0               # PanWallUnitsPerBeat, Mirror10 scale 1
RAIL_LO, RAIL_HI = 0.4, 2.5

PANELS = ["slash.png", "smiley.png", "tech.png", "abstract1.png", "crystal.png", "ai2.png"]
ALPHAS = [0.0, 0.5, 0.75, 1.0]

# On-Beat Push comparison: strength 7 * triangle pulse * typical gated Low ~0.27.
PUSH_PEAK = 7.0 * 0.27


def load_rgb(name: str) -> np.ndarray:
    for pool in ("color", "mono"):
        p = REPO / "Assets/StreamingAssets/images" / pool / name
        if p.exists():
            return np.asarray(Image.open(p).convert("RGB"), dtype=np.uint8)
    raise FileNotFoundError(name)


def sample(rgb: np.ndarray, pos_x: float, pos_y: float) -> np.ndarray:
    """Exact port of Draw()'s per-pixel fold: center, offset, abs, mirror-repeat."""
    h, w = rgb.shape[:2]
    xs = np.arange(BUF_W, dtype=np.float64) - BUF_W / 2
    ys = np.arange(BUF_H, dtype=np.float64) - BUF_H / 2
    gx, gy = np.meshgrid(xs, ys)                 # rotation is zero in this prototype
    x2 = np.abs(gx + pos_x)
    y2 = np.abs(gy + pos_y)
    xp = x2.astype(np.int64) // w
    yp = y2.astype(np.int64) // h
    x2 = x2 % w
    y2 = y2 % h
    x2 = np.where(xp & 1 != 0, (w - 1) - x2, x2)
    y2 = np.where(yp & 1 != 0, (h - 1) - y2, y2)
    return rgb[y2.astype(np.int64), x2.astype(np.int64)]


def panel_frames(rgb: np.ndarray, factor: float, n_frames: int, push: bool) -> list[np.ndarray]:
    """Diagonal pan at the calibrated rate; fixed start so every panel is leveled."""
    h, w = rgb.shape[:2]
    pos_x, pos_y = 0.37 * w, 0.37 * h
    frames = []
    for f in range(n_frames):
        frames.append(sample(rgb, pos_x, pos_y))
        t = f / FPS
        phase = (t / BEAT_S) % 1.0
        pace = PACE_MID + (PUSH_PEAK * abs(1.0 - 2.0 * phase) if push else 0.0)
        rate = PAN_PER_BEAT * pace * factor / BEAT_S   # wall px / s per axis
        pos_x += rate / FPS
        pos_y += rate / FPS
    return frames


def compose(grids: list[list[np.ndarray]], labels: list[str], caption: str, cols: int) -> list[Image.Image]:
    n_frames = len(grids[0])
    rows = (len(grids) + cols - 1) // cols
    pw, ph = BUF_W * SCALE, BUF_H * SCALE
    total_w = cols * pw + (cols - 1) * GUTTER
    total_h = rows * (ph + LABEL_H) + (rows - 1) * GUTTER + LABEL_H
    out = []
    for f in range(n_frames):
        canvas = Image.new("RGB", (total_w, total_h), (18, 18, 18))
        draw = ImageDraw.Draw(canvas)
        draw.text((4, 3), caption, fill=(220, 220, 220))
        for i, frames in enumerate(grids):
            r, c = divmod(i, cols)
            x0 = c * (pw + GUTTER)
            y0 = LABEL_H + r * (ph + LABEL_H + GUTTER)
            up = np.repeat(np.repeat(frames[f], SCALE, axis=0), SCALE, axis=1)
            canvas.paste(Image.fromarray(up), (x0, y0))
            draw.text((x0 + 4, y0 + ph + 2), labels[i], fill=(180, 180, 180))
        out.append(canvas)
    return out


def save_gif(frames: list[Image.Image], path: Path) -> None:
    frames[0].save(path, save_all=True, append_images=frames[1:],
                   duration=int(1000 / FPS), loop=0)
    print(f"wrote {path} ({path.stat().st_size / 1e6:.1f} MB, {len(frames)} frames)")


def main() -> None:
    meta = json.loads((OUT / "measure.json").read_text())
    l_ref = meta["l_ref"]
    l_by_name = {img["name"]: img["L"] for img in meta["images"]}
    images = {name: load_rgb(name) for name in PANELS}
    n_frames = int(BEATS * BEAT_S * FPS)

    for alpha in ALPHAS:
        grids, labels = [], []
        for name in PANELS:
            factor = float(np.clip((l_by_name[name] / l_ref) ** alpha, RAIL_LO, RAIL_HI))
            grids.append(panel_frames(images[name], factor, n_frames, push=False))
            labels.append(f"{name.removesuffix('.png')}  x{factor:.2f}")
        caption = (f"alpha={alpha}   {BPM:.0f} BPM, Mid energy, Mirror10, no push"
                   f"   (alpha=0 is today's behavior)")
        frames = compose(grids, labels, caption, cols=3)
        save_gif(frames, OUT / f"pace_alpha_{alpha:.2f}.gif")

    # Push readability: the two pool extremes, today vs calibrated.
    grids, labels = [], []
    for alpha in (0.0, 0.75):
        for name in ("slash.png", "ai2.png"):
            factor = float(np.clip((l_by_name[name] / l_ref) ** alpha, RAIL_LO, RAIL_HI))
            grids.append(panel_frames(images[name], factor, n_frames, push=True))
            labels.append(f"{name.removesuffix('.png')}  alpha={alpha}  x{factor:.2f}")
    frames = compose(grids, labels,
                     "On-Beat Push (strength 7, typical Low): top row today, bottom row alpha=0.75",
                     cols=2)
    save_gif(frames, OUT / "push_compare.gif")


if __name__ == "__main__":
    main()
