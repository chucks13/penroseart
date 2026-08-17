# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy", "pillow"]
# ///
# PROTOTYPE — throwaway (see README.md). Measures correlation length L for every
# Kscope pool image and prints the calibration-factor table for candidate alphas.
import json
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent / "out"
OUT.mkdir(exist_ok=True)

OFFSETS = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 128]
THRESHOLD = 0.6          # L = offset where MAD reaches this fraction of the plateau
RAIL_LO, RAIL_HI = 0.4, 2.5
ALPHAS = [0.0, 0.25, 0.5, 0.75, 1.0]

# Current wall tuning, Mirror10, Mid energy: 6 px/beat * 1.125 pace, per axis.
BASE_PX_PER_BEAT = 6.0 * 1.125


def load_intensity(path: Path) -> tuple[np.ndarray, np.ndarray]:
    """Returns (rgb float array HxWx3 in [0,1], intensity HxW = channel mean)."""
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32) / 255.0
    return rgb, rgb.mean(axis=2)


def mirror_tile(i: np.ndarray, pad: int) -> np.ndarray:
    """Extend by symmetric reflection — the same edge-duplicating fold Draw() uses."""
    return np.pad(i, ((0, pad), (0, pad)), mode="symmetric")


def mad_curve(i: np.ndarray) -> dict[int, float]:
    """Mean absolute intensity difference at each offset, averaged over X and Y,
    on the mirror-tiled plane (matches what the wall actually shows)."""
    h, w = i.shape
    maxd = OFFSETS[-1]
    # Tile enough that every offset stays in-domain even for the 12px image.
    reps_y = maxd // h + 2
    reps_x = maxd // w + 2
    t = i
    for _ in range(int(np.ceil(np.log2(max(reps_y, reps_x)))) + 1):
        t = mirror_tile(t, t.shape[0])[: t.shape[0] * 2, : t.shape[1] * 2]
    t = t[: h + maxd, : w + maxd]
    curve = {}
    for d in OFFSETS:
        mx = np.abs(t[:h, d : w + d] - t[:h, :w]).mean()
        my = np.abs(t[d : h + d, :w] - t[:h, :w]).mean()
        curve[d] = float((mx + my) / 2.0)
    return curve


def plateau(i: np.ndarray) -> float:
    """Expected |difference| between two unrelated texels — the decorrelated ceiling."""
    flat = i.ravel()
    rng = np.random.default_rng(0)
    return float(np.abs(flat - rng.permutation(flat)).mean())


def correlation_length(curve: dict[int, float], plat: float) -> float:
    """Smallest offset (log-interpolated) where MAD reaches THRESHOLD * plateau."""
    target = THRESHOLD * plat
    ds = sorted(curve)
    if curve[ds[0]] >= target:
        return float(ds[0])
    for a, b in zip(ds, ds[1:]):
        if curve[b] >= target:
            # interpolate in log2(offset)
            fa, fb = curve[a], curve[b]
            t = (target - fa) / (fb - fa)
            return float(2 ** (np.log2(a) + t * (np.log2(b) - np.log2(a))))
    return float(ds[-1])  # never decorrelates within 128px — capped


def measure(path: Path) -> dict:
    rgb, i = load_intensity(path)
    curve = mad_curve(i)
    plat = plateau(i)
    length = correlation_length(curve, plat)

    # Channel-swap invariance: the metric is the channel mean, so any permutation
    # must produce the identical L. Verify end-to-end anyway.
    swapped = rgb[:, :, [2, 1, 0]].mean(axis=2)
    l_swapped = correlation_length(mad_curve(swapped), plateau(swapped))
    assert abs(length - l_swapped) < 1e-6, f"channel swap changed L for {path.name}"

    # Spatial stability: L per quadrant for images big enough to have quadrants.
    h, w = i.shape
    quadrants = []
    if min(h, w) >= 128:
        for qy in (slice(0, h // 2), slice(h // 2, h)):
            for qx in (slice(0, w // 2), slice(w // 2, w)):
                q = i[qy, qx]
                quadrants.append(round(correlation_length(mad_curve(q), plateau(q)), 1))

    return {
        "name": path.name,
        "pool": path.parent.name,
        "w": w,
        "h": h,
        "plateau": round(plat, 4),
        "L": round(length, 2),
        "quadrant_L": quadrants,
        "curve": {d: round(v, 4) for d, v in curve.items()},
    }


def main() -> None:
    paths = sorted((REPO / "Assets/StreamingAssets/images/color").glob("*.png")) + sorted(
        (REPO / "Assets/StreamingAssets/images/mono").glob("*.png")
    )
    results = [measure(p) for p in paths]

    ls = np.array([r["L"] for r in results])
    l_ref = float(np.exp(np.log(ls).mean()))  # geometric mean of the pool

    print(f"pool: {len(results)} images | L_ref (geometric mean) = {l_ref:.1f}\n")
    hdr = f"{'image':<16}{'pool':<7}{'size':<10}{'contrast':<10}{'L':<8}quadrant L"
    print(hdr)
    print("-" * len(hdr))
    for r in sorted(results, key=lambda r: r["L"]):
        q = " ".join(str(x) for x in r["quadrant_L"]) if r["quadrant_L"] else "-"
        print(
            f"{r['name']:<16}{r['pool']:<7}{r['w']}x{r['h']:<6}"
            f"{r['plateau']:<10}{r['L']:<8}{q}"
        )

    print("\nfactor = clamp((L / L_ref)^alpha, %.1f, %.1f)" % (RAIL_LO, RAIL_HI))
    print("wall px/beat/axis = %.2f * factor | pattern %%/beat = wall / (2*width)\n" % BASE_PX_PER_BEAT)
    for alpha in ALPHAS:
        factors = np.clip((ls / l_ref) ** alpha, RAIL_LO, RAIL_HI)
        wall = BASE_PX_PER_BEAT * factors
        widths = np.array([r["w"] for r in results], dtype=float)
        pattern = wall / (2.0 * widths)
        print(f"alpha={alpha:<5} wall-rate spread {wall.max()/wall.min():6.1f}x   "
              f"pattern-rate spread {pattern.max()/pattern.min():6.1f}x")

    for alpha in ALPHAS:
        print(f"\nalpha={alpha} factors:")
        factors = np.clip((ls / l_ref) ** alpha, RAIL_LO, RAIL_HI)
        for r, f in sorted(zip(results, factors), key=lambda t: t[0]["L"]):
            print(f"  {r['name']:<16} L={r['L']:<7} factor={f:.2f}")

    with open(OUT / "measure.json", "w") as fh:
        json.dump({"l_ref": l_ref, "images": results}, fh, indent=1)
    print(f"\nwrote {OUT / 'measure.json'}")


if __name__ == "__main__":
    main()
