# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy", "pillow"]
# ///
# PROTOTYPE — throwaway (see README.md). Ports Kscope's full display path and
# its exact Fill treatment, then builds the human viewer for every pool image.
import html
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

PX_PER_UNIT = 10
BPM = 120.0
BEAT_S = 60.0 / BPM
FPS = 15
BEATS = 4

PACE_MID = 1.125
PAN_PER_BEAT = 6.0
ROT_PER_BEAT = 0.1
MIRROR_SCALE = {"mirror2": 2.0, "mirror10": 1.0}
RAIL_LO, RAIL_HI = 0.4, 2.5

# Retained for the older historical renderers that import this module. It is
# now the complete pool, so no current renderer silently caps image coverage.
PANELS = [
    p.name
    for pool in ("color", "mono")
    for p in sorted((REPO / "Assets/StreamingAssets/images" / pool).glob("*.png"))
]
ALPHAS = [0.0, 0.75]
LABEL_H = 18
GUTTER = 2

FILL_BEATS = 2
MONO_SATURATION_FLOOR = 0.3
PALETTE_VIEW_INDEX = 2
FILL_SOURCE_LINES = "Assets/effects/Kscope.cs:624-637"
MONO_SOURCE_LINES = "Assets/effects/Kscope.cs:600-608"


def parse_layout() -> dict:
    """Load the same comment-tolerant layout data used by the runtime."""
    text = (REPO / "Assets/StreamingAssets/penrose_layout.txt").read_text()
    text = re.sub(r"^\s*//.*$", "", text, flags=re.M)
    return json.loads(text)


def pool_entries() -> list[dict[str, str]]:
    """Return every current Kscope image in stable color-then-mono order."""
    return [
        {"name": p.name, "pool": pool, "path": str(p)}
        for pool in ("color", "mono")
        for p in sorted((REPO / "Assets/StreamingAssets/images" / pool).glob("*.png"))
    ]


def tile_geometry(mesh: list[float]) -> tuple[np.ndarray, list[list[tuple[float, float]]]]:
    """Return centers and gap-shrunk triangle polygons in effect-layout units."""
    m = np.asarray(mesh, dtype=np.float64).reshape(-1, 3, 2)
    m = m * FULL_SCALE
    m[:, :, 1] *= -1.0
    tri0 = m[0::2]
    centers = (tri0[:, 0, :] + tri0[:, 2, :]) / 2.0
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
    """Port ScreenEffect.InitWeights, including its distance-as-weight quirk."""
    gx = MIN_X + np.arange(BUF_W)
    gy = MIN_Y + np.arange(BUF_H)
    gpx, gpy = np.meshgrid(gx, gy)
    gpos = np.stack([gpx.ravel(), gpy.ravel()], axis=1)
    d = np.linalg.norm(centers[:, None, :] - gpos[None, :, :], axis=2)
    idx = np.argsort(d, axis=1)[:, :NEIGHBOR_COUNT]
    nd = np.take_along_axis(d, idx, axis=1)
    weights = nd / nd.sum(axis=1, keepdims=True)
    return idx, weights


def seed_map(packed: list[int]) -> np.ndarray:
    """Return tile -> mirror-group first tile, exactly as Kscope.Draw copies it."""
    p = np.asarray(packed, dtype=np.int64)
    out = np.arange(900, dtype=np.int64)
    for group_index in range(p[0]):
        pointer = p[group_index + 1]
        tiles = p[pointer + 1 : pointer + 1 + p[pointer]]
        out[tiles] = tiles[0]
    return out


def neighbor_edges(layout: dict, seeds: np.ndarray) -> np.ndarray:
    """Return unique wall Neighbor edges whose tiles belong to different mirror groups."""
    pairs = set()
    for tile_index, tile in enumerate(layout["tiles"]):
        for neighbor in tile["neighbors"]:
            other = neighbor["tileIdx"]
            a, b = sorted((tile_index, other))
            if seeds[a] != seeds[b]:
                pairs.add((a, b))
    return np.asarray(sorted(pairs), dtype=np.int64)


def rasterize_tilemap(polys) -> np.ndarray:
    """Build a tile-index raster so a frame is a single color lookup."""
    width, height = int(50 * PX_PER_UNIT), int(22 * PX_PER_UNIT)
    img = Image.new("I", (width, height), 0)
    draw = ImageDraw.Draw(img)
    for tile_index, triangles in enumerate(polys):
        for triangle in triangles:
            points = [
                ((x - MIN_X) * PX_PER_UNIT, (11.0 - y) * PX_PER_UNIT)
                for x, y in triangle
            ]
            draw.polygon(points, fill=tile_index + 1)
    return np.asarray(img, dtype=np.int64)


def load_rgb(name: str) -> np.ndarray:
    """Load one pool image as floating-point RGB in [0,1]."""
    for pool in ("color", "mono"):
        path = REPO / "Assets/StreamingAssets/images" / pool / name
        if path.exists():
            return np.asarray(Image.open(path).convert("RGB"), dtype=np.float64) / 255.0
    raise FileNotFoundError(name)


def pool_for_name(name: str) -> str:
    """Return the source pool containing an image name."""
    for pool in ("color", "mono"):
        if (REPO / "Assets/StreamingAssets/images" / pool / name).exists():
            return pool
    raise FileNotFoundError(name)


def rgb_to_hsv(rgb: np.ndarray) -> np.ndarray:
    """Vectorized Unity-compatible RGB-to-HSV for RGB values in [0,1]."""
    rgb = np.asarray(rgb, dtype=np.float64)
    red, green, blue = np.moveaxis(rgb, -1, 0)
    maximum = np.max(rgb, axis=-1)
    minimum = np.min(rgb, axis=-1)
    delta = maximum - minimum
    saturation = np.divide(delta, maximum, out=np.zeros_like(delta), where=maximum != 0)
    hue = np.zeros_like(maximum)
    changing = delta != 0
    red_max = changing & (maximum == red)
    green_max = changing & ~red_max & (maximum == green)
    blue_max = changing & ~red_max & ~green_max
    hue[red_max] = ((green[red_max] - blue[red_max]) / delta[red_max]) % 6.0
    hue[green_max] = ((blue[green_max] - red[green_max]) / delta[green_max]) + 2.0
    hue[blue_max] = ((red[blue_max] - green[blue_max]) / delta[blue_max]) + 4.0
    hue = (hue / 6.0) % 1.0
    return np.stack([hue, saturation, maximum], axis=-1)


def hsv_to_rgb(hsv: np.ndarray) -> np.ndarray:
    """Vectorized Unity-compatible HSV-to-RGB for HSV values in [0,1]."""
    hsv = np.asarray(hsv, dtype=np.float64)
    hue, saturation, value = np.moveaxis(hsv, -1, 0)
    sector_float = (hue % 1.0) * 6.0
    sector = np.floor(sector_float).astype(np.int64) % 6
    fraction = sector_float - np.floor(sector_float)
    p = value * (1.0 - saturation)
    q = value * (1.0 - saturation * fraction)
    t = value * (1.0 - saturation * (1.0 - fraction))
    choices = (
        (value, t, p),
        (q, value, p),
        (p, value, t),
        (p, q, value),
        (t, p, value),
        (value, p, q),
    )
    out = np.empty_like(hsv)
    for index, channels in enumerate(choices):
        mask = sector == index
        for channel, values in enumerate(channels):
            out[..., channel][mask] = values[mask]
    return out


def _map_gradient(position: float, table: list[tuple[float, np.ndarray]]) -> np.ndarray:
    """Port AnimPalette.Map2Palette for one normalized position."""
    for left, right in zip(table, table[1:]):
        if left[0] <= position <= right[0]:
            span = right[0] - left[0]
            fraction = 0.0 if span == 0 else (position - left[0]) / span
            return left[1] + (right[1] - left[1]) * fraction
    return np.zeros(3, dtype=np.float64)


def load_scene_palettes() -> list[tuple[str, np.ndarray]]:
    """Port the scene's gradient-palette parse and 32-entry mapping path."""
    scene = (REPO / "Assets/Scenes/SampleScene.unity").read_text()
    source_match = re.search(
        r"^  paletteSource: '(.*?)'\n^  jsonSource:", scene, flags=re.M | re.S
    )
    if source_match is None:
        raise RuntimeError("SampleScene paletteSource was not found")
    source = source_match.group(1)
    definitions = re.findall(
        r"DEFINE_GRADIENT_PALETTE\(\s*([^)]*?)\s*\)\s*\{([^}]*)\}",
        source,
        flags=re.S,
    )
    palettes = []
    seen = set()
    for raw_name, raw_data in definitions:
        name = "".join(raw_name.split())
        if name in seen:
            continue
        seen.add(name)
        values = [int(value) for value in re.findall(r"\d+", raw_data)]
        table = [
            (values[index] / 255.0, np.asarray(values[index + 1 : index + 4]) / 255.0)
            for index in range(0, len(values), 4)
        ]
        mapped = np.asarray([_map_gradient(index / 32.0, table) for index in range(32)])
        palettes.append((name, mapped))
    if not palettes:
        raise RuntimeError("SampleScene paletteSource contained no gradient palettes")
    return palettes


def palette_read(values: np.ndarray, palette: np.ndarray) -> np.ndarray:
    """Port GPalette.read(i, true) for a 32-entry palette."""
    wrapped = np.mod(values, 1.0)
    scaled = wrapped * (len(palette) - 1)
    first = np.floor(scaled).astype(np.int64)
    second = np.minimum(first + 1, len(palette) - 1)
    fraction = (scaled - first)[..., None]
    return palette[first] + (palette[second] - palette[first]) * fraction


def apply_mono_palette(rgb: np.ndarray, palette: np.ndarray) -> np.ndarray:
    """Port Kscope's Synced mono path: palette hue/sat, source brightness, S>=0.3."""
    brightness = rgb[..., 0]
    mapped = palette_read(brightness, palette)
    hsv = rgb_to_hsv(mapped)
    hsv[..., 1] = np.maximum(hsv[..., 1], MONO_SATURATION_FLOOR)
    hsv[..., 2] = brightness
    return hsv_to_rgb(hsv)


def prepare_source(rgb: np.ndarray, pool: str, palette: np.ndarray) -> np.ndarray:
    """Return what Kscope feeds into screen sampling for the modeled activation."""
    return apply_mono_palette(rgb, palette) if pool == "mono" else rgb


def sample(rgb: np.ndarray, pos_x: float, pos_y: float, angle: float) -> np.ndarray:
    """Exact Draw port: center, rotate, offset, abs, and mirror-repeat."""
    height, width = rgb.shape[:2]
    xs = np.arange(BUF_W, dtype=np.float64) - BUF_W / 2
    ys = np.arange(BUF_H, dtype=np.float64) - BUF_H / 2
    gx, gy = np.meshgrid(xs, ys)
    cosine, sine = np.cos(angle), np.sin(angle)
    x2 = np.abs(cosine * gx - sine * gy + pos_x)
    y2 = np.abs(sine * gx + cosine * gy + pos_y)
    xp = x2.astype(np.int64) // width
    yp = y2.astype(np.int64) // height
    x2 = x2 % width
    y2 = y2 % height
    x2 = np.where((xp & 1) != 0, (width - 1) - x2, x2)
    y2 = np.where((yp & 1) != 0, (height - 1) - y2, y2)
    return rgb[y2.astype(np.int64), x2.astype(np.int64)]


def apply_gray_mapping(rgb: np.ndarray, mapping: str) -> np.ndarray:
    """Apply the exact Fill gray map or one black-and-white desk candidate."""
    hsv = rgb_to_hsv(rgb)
    if mapping == "exact":
        # Kscope.cs: assure there is brightness variation, then fully desaturate.
        gray = np.mod(hsv[..., 0] + hsv[..., 1] + hsv[..., 2], 1.0)
    elif mapping == "value":
        gray = hsv[..., 2]
    elif mapping == "luminance":
        linear = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
        luminance = np.sum(linear * np.asarray([0.2126, 0.7152, 0.0722]), axis=-1)
        gray = np.where(
            luminance <= 0.0031308,
            luminance * 12.92,
            1.055 * np.power(luminance, 1.0 / 2.4) - 0.055,
        )
    else:
        raise ValueError(f"unknown gray mapping: {mapping}")
    return np.repeat(gray[..., None], 3, axis=-1)


def wall_buffers(
    rgb: np.ndarray,
    pos_x: float,
    pos_y: float,
    angle: float,
    nearest_indexes: np.ndarray,
    nearest_weights: np.ndarray,
    seeds: np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    """Run sampling, ConvertScreenBuffer, mirror replication, then exact Fill."""
    screen = sample(rgb, pos_x, pos_y, angle).reshape(-1, 3)
    unmirrored = (screen[nearest_indexes] * nearest_weights[:, :, None]).sum(axis=1)
    normal = unmirrored[seeds]
    return normal, apply_gray_mapping(normal, "exact")


def render_buffer(buffer: np.ndarray, tilemap: np.ndarray) -> np.ndarray:
    """Paint one 900-tile buffer through the real rhomb raster."""
    colors = np.vstack([[[0.0, 0.0, 0.0]], buffer])
    return (np.clip(colors[tilemap], 0.0, 1.0) * 255).astype(np.uint8)


def motion_state(rgb: np.ndarray, mode: str, frame_index: int, factor: float = 1.0) -> tuple[float, float, float]:
    """Return the deterministic position/rotation used for one comparison frame."""
    height, width = rgb.shape[:2]
    motion_scale = PACE_MID * MIRROR_SCALE[mode] * factor / BEAT_S
    elapsed = frame_index / FPS
    return (
        0.37 * width + motion_scale * PAN_PER_BEAT * elapsed,
        0.37 * height + motion_scale * PAN_PER_BEAT * elapsed,
        motion_scale * ROT_PER_BEAT * elapsed,
    )


def wall_frames(
    rgb,
    factor,
    mode,
    nearest_indexes,
    nearest_weights,
    seeds,
    tilemap,
    n_frames,
    fill_mapping=None,
):
    """Compatibility renderer used by the historical pace scripts and scorecards."""
    frames, variety = [], []
    for frame_index in range(n_frames):
        pos_x, pos_y, angle = motion_state(rgb, mode, frame_index, factor)
        normal, exact_fill = wall_buffers(
            rgb, pos_x, pos_y, angle, nearest_indexes, nearest_weights, seeds
        )
        buffer = normal if fill_mapping is None else (
            exact_fill if fill_mapping == "exact" else apply_gray_mapping(normal, fill_mapping)
        )
        variety.append(float(buffer.std(axis=0).mean()))
        frames.append(render_buffer(buffer, tilemap))
    return frames, float(np.mean(variety))


def compose(grids, labels, caption, cols):
    """Compose same-sized frame grids; retained for the historical renderers."""
    n_frames = len(grids[0])
    rows = (len(grids) + cols - 1) // cols
    panel_height, panel_width = grids[0][0].shape[:2]
    total_width = cols * panel_width + (cols - 1) * GUTTER
    total_height = rows * (panel_height + LABEL_H) + (rows - 1) * GUTTER + LABEL_H
    output = []
    for frame_index in range(n_frames):
        canvas = Image.new("RGB", (total_width, total_height), (14, 14, 14))
        draw = ImageDraw.Draw(canvas)
        draw.text((4, 3), caption, fill=(220, 220, 220))
        for index, frames in enumerate(grids):
            row, column = divmod(index, cols)
            x0 = column * (panel_width + GUTTER)
            y0 = LABEL_H + row * (panel_height + LABEL_H + GUTTER)
            canvas.paste(Image.fromarray(frames[frame_index]), (x0, y0))
            draw.text((x0 + 4, y0 + panel_height + 2), labels[index], fill=(180, 180, 180))
        output.append(canvas)
    return output


def compose_columns(images: list[np.ndarray], labels: list[str], caption: str) -> Image.Image:
    """Compose wall rasters in one labeled horizontal comparison."""
    panel_height, panel_width = images[0].shape[:2]
    total_width = len(images) * panel_width + (len(images) - 1) * GUTTER
    canvas = Image.new("RGB", (total_width, panel_height + LABEL_H * 2), (14, 14, 14))
    draw = ImageDraw.Draw(canvas)
    draw.text((4, 3), caption, fill=(225, 225, 225))
    for index, (image, label) in enumerate(zip(images, labels)):
        x0 = index * (panel_width + GUTTER)
        canvas.paste(Image.fromarray(image), (x0, LABEL_H))
        draw.text((x0 + 4, panel_height + LABEL_H + 2), label, fill=(190, 190, 190))
    return canvas


def save_gif(frames: list[Image.Image], path: Path) -> None:
    """Write one regenerable comparison GIF."""
    frames[0].save(
        path,
        save_all=True,
        append_images=frames[1:],
        duration=int(1000 / FPS),
        loop=0,
    )


def adjust_image(rgb: np.ndarray, pool: str, adjustment: str) -> np.ndarray:
    """Apply one candidate whole-image pool edit; never used by runtime code."""
    if adjustment == "none":
        return rgb
    hsv = rgb_to_hsv(rgb)
    if adjustment.startswith("hue"):
        degrees = int(adjustment.removeprefix("hue"))
        hsv[..., 0] = np.mod(hsv[..., 0] + degrees / 360.0, 1.0)
    elif adjustment.startswith("sat"):
        scale = float(adjustment.removeprefix("sat"))
        hsv[..., 1] = np.clip(hsv[..., 1] * scale, 0.0, 1.0)
    elif adjustment.startswith("gamma"):
        gamma = float(adjustment.removeprefix("gamma"))
        hsv[..., 2] = np.power(hsv[..., 2], gamma)
    elif adjustment == "autocontrast":
        low, high = np.percentile(hsv[..., 2], (2, 98))
        if high > low:
            hsv[..., 2] = np.clip((hsv[..., 2] - low) / (high - low), 0.0, 1.0)
    elif adjustment == "soft-shade":
        height, width = hsv.shape[:2]
        yy, xx = np.meshgrid(
            np.linspace(0.0, 1.0, height),
            np.linspace(0.0, 1.0, width),
            indexing="ij",
        )
        ramp = 0.78 + 0.22 * (0.5 + 0.5 * np.sin(2.0 * np.pi * (xx + yy)))
        hsv[..., 2] = np.clip(hsv[..., 2] * ramp, 0.0, 1.0)
    else:
        raise ValueError(f"unknown image adjustment: {adjustment}")
    adjusted = hsv_to_rgb(hsv)
    if pool == "mono":
        value = rgb_to_hsv(adjusted)[..., 2]
        adjusted = np.repeat(value[..., None], 3, axis=-1)
    return adjusted


def candidate_adjustments(pool: str) -> list[str]:
    """Return the bounded desk-only image edits tested for one pool."""
    common = ["none", "gamma0.75", "gamma1.25", "autocontrast", "soft-shade"]
    if pool == "mono":
        return common
    return common + [
        "hue-90",
        "hue-60",
        "hue-30",
        "hue30",
        "hue60",
        "hue90",
        "sat0.5",
        "sat0.75",
    ]


def adjustment_label(adjustment: str) -> str:
    """Return a compact human label for a candidate pool edit."""
    if adjustment == "none":
        return "no pool edit"
    if adjustment.startswith("hue"):
        return f"global hue {int(adjustment.removeprefix('hue')):+d}°"
    if adjustment.startswith("sat"):
        return f"global saturation ×{float(adjustment.removeprefix('sat')):.2g}"
    if adjustment.startswith("gamma"):
        return f"value gamma {float(adjustment.removeprefix('gamma')):.2g}"
    if adjustment == "autocontrast":
        return "2–98% value autocontrast"
    if adjustment == "soft-shade":
        return "add ±11% soft texture shading"
    return adjustment


def _render_fill_gif(
    prepared: np.ndarray,
    name: str,
    pool: str,
    mode: str,
    nearest_indexes: np.ndarray,
    nearest_weights: np.ndarray,
    seeds: np.ndarray,
    tilemap: np.ndarray,
) -> str:
    """Render the required normal-vs-exact-Fill animation for one image/layout."""
    frames = []
    for frame_index in range(int(FILL_BEATS * BEAT_S * FPS)):
        pos_x, pos_y, angle = motion_state(prepared, mode, frame_index)
        normal, fill = wall_buffers(
            prepared, pos_x, pos_y, angle, nearest_indexes, nearest_weights, seeds
        )
        frames.append(
            compose_columns(
                [render_buffer(normal, tilemap), render_buffer(fill, tilemap)],
                ["normal", "Fill — exact (h+s+v) mod 1"],
                f"{name} — {pool} — {mode}",
            )
        )
    filename = f"fill_{pool}_{Path(name).stem}_{mode}.gif"
    save_gif(frames, OUT / filename)
    return filename


def _render_mapping_comparison(
    prepared: np.ndarray,
    name: str,
    pool: str,
    mode: str,
    nearest_indexes: np.ndarray,
    nearest_weights: np.ndarray,
    seeds: np.ndarray,
    tilemap: np.ndarray,
) -> str:
    """Render normal plus exact/value/luminance black-and-white mappings."""
    frame_index = int(0.75 * BEAT_S * FPS)
    pos_x, pos_y, angle = motion_state(prepared, mode, frame_index)
    normal, exact = wall_buffers(
        prepared, pos_x, pos_y, angle, nearest_indexes, nearest_weights, seeds
    )
    image = compose_columns(
        [
            render_buffer(normal, tilemap),
            render_buffer(exact, tilemap),
            render_buffer(apply_gray_mapping(normal, "value"), tilemap),
            render_buffer(apply_gray_mapping(normal, "luminance"), tilemap),
        ],
        ["normal", "exact modulo", "candidate: HSV value", "candidate: Rec.709 luminance"],
        f"GRAY-MAPPING CANDIDATES — {name} — {mode}",
    )
    filename = f"check_mapping_{pool}_{Path(name).stem}_{mode}.png"
    image.save(OUT / filename)
    return filename


def _render_pool_candidate(
    raw: np.ndarray,
    pool: str,
    palette: np.ndarray,
    adjustment: str,
    name: str,
    mode: str,
    nearest_indexes: np.ndarray,
    nearest_weights: np.ndarray,
    seeds: np.ndarray,
    tilemap: np.ndarray,
) -> str:
    """Render baseline and candidate-image normal/Fill states side by side."""
    baseline = prepare_source(raw, pool, palette)
    adjusted_raw = adjust_image(raw, pool, adjustment)
    adjusted = prepare_source(adjusted_raw, pool, palette)
    frame_index = int(0.75 * BEAT_S * FPS)
    state = motion_state(baseline, mode, frame_index)
    normal, fill = wall_buffers(
        baseline, *state, nearest_indexes, nearest_weights, seeds
    )
    adjusted_normal, adjusted_fill = wall_buffers(
        adjusted, *state, nearest_indexes, nearest_weights, seeds
    )
    image = compose_columns(
        [
            render_buffer(normal, tilemap),
            render_buffer(fill, tilemap),
            render_buffer(adjusted_normal, tilemap),
            render_buffer(adjusted_fill, tilemap),
        ],
        ["normal", "exact Fill", f"candidate normal", f"candidate Fill"],
        f"POOL-EDIT CANDIDATE — {name} — {mode} — {adjustment_label(adjustment)}",
    )
    filename = f"check_pool_{pool}_{Path(name).stem}_{mode}.png"
    image.save(OUT / filename)
    return filename


def _table(rows: list[list[str]], headings: list[str]) -> str:
    """Build one escaped HTML table."""
    header = "".join(f"<th>{html.escape(str(value))}</th>" for value in headings)
    body = "".join(
        "<tr>" + "".join(f"<td>{html.escape(str(value))}</td>" for value in row) + "</tr>"
        for row in rows
    )
    return f"<table><thead><tr>{header}</tr></thead><tbody>{body}</tbody></table>"


def write_viewer(
    measure_data: dict,
    score_data: dict,
    fill_artifacts: dict[tuple[str, str], str],
    mapping_artifacts: dict[tuple[str, str], str],
    pool_artifacts: dict[tuple[str, str], str],
    palette_name: str,
) -> None:
    """Write the complete human inspection view."""
    character_rows = []
    for image in sorted(measure_data["images"], key=lambda item: item["shading_share_pct"]):
        character_rows.append([
            image["name"],
            image["pool"],
            f"{image['w']}×{image['h']}",
            f"{image['plateau']:.4f}",
            f"{image['L']:.2f}",
            f"{image['flat_pct']:.1f}",
            f"{image['shaded_pct']:.1f}",
            f"{image['hard_edge_pct']:.1f}",
            f"{image['shading_share_pct']:.1f}",
        ])

    score_rows = []
    for row in score_data["rows"]:
        exact = row["exact"]
        score_rows.append([
            row["name"], row["pool"], row["layout"],
            f"{exact['collapse_pct']:.1f}",
            f"{exact['contrast_retention_pct']:.1f}",
            f"{exact['inversion_pct']:.1f}",
            f"{exact['wrap_pct']:.1f}",
            str(exact["gray_bins"]),
            f"{row['value']['collapse_pct']:.1f}",
            f"{row['luminance']['collapse_pct']:.1f}",
        ])

    candidate_rows = []
    for name, candidate in score_data["image_candidates"].items():
        candidate_rows.append([
            name,
            candidate["pool"],
            adjustment_label(candidate["adjustment"]),
            f"{candidate['baseline_collapse_pct']:.1f}",
            f"{candidate['candidate_collapse_pct']:.1f}",
            f"{candidate['gain_points']:+.1f}",
            candidate["cost"],
        ])

    fill_cards = []
    mapping_cards = []
    pool_cards = []
    for entry in pool_entries():
        name, pool = entry["name"], entry["pool"]
        for mode in ("mirror2", "mirror10"):
            fill_cards.append(
                f'<article><h3>{html.escape(name)} · {mode}</h3>'
                f'<img src="{fill_artifacts[(name, mode)]}" loading="lazy"></article>'
            )
            mapping_cards.append(
                f'<article><h3>{html.escape(name)} · {mode}</h3>'
                f'<img src="{mapping_artifacts[(name, mode)]}" loading="lazy"></article>'
            )
            if name in score_data["image_candidates"]:
                pool_cards.append(
                    f'<article><h3>{html.escape(name)} · {mode}</h3>'
                    f'<img src="{pool_artifacts[(name, mode)]}" loading="lazy"></article>'
                )

    page = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Kscope Fill desk-wall investigation</title>
<style>
body {{ margin: 0; padding: 24px; background: #111; color: #ddd; font: 15px/1.45 system-ui, sans-serif; }}
h1,h2,h3 {{ color: #fff; }} h2 {{ margin-top: 42px; }} h3 {{ font-size: 14px; margin: 0 0 8px; }}
p,li {{ max-width: 95ch; }} code {{ color: #ffd580; }}
.grid {{ display: grid; grid-template-columns: repeat(auto-fit,minmax(620px,1fr)); gap: 18px; }}
article {{ background: #1b1b1b; border: 1px solid #333; padding: 10px; overflow-x: auto; }}
img {{ display: block; width: 100%; min-width: 600px; height: auto; image-rendering: auto; }}
table {{ border-collapse: collapse; width: 100%; font-size: 13px; }}
th,td {{ border: 1px solid #3a3a3a; padding: 5px 7px; text-align: right; }}
th:first-child,td:first-child,th:nth-child(2),td:nth-child(2) {{ text-align: left; }}
th {{ position: sticky; top: 0; background: #272727; }} .note {{ color: #aaa; }}
</style></head><body>
<h1>Kscope Fill — desk-wall investigation</h1>
<p>The black-and-white Fill identity is settled. This viewer isolates what happens to definition under the exact runtime map. Fill is simulated by a flag; this prototype does not read OSC. A Fill is a Synced Mode fact served by BeatManager's Data Surface; Standalone Mode never reports one (<code>CONTEXT.md</code>: Fill, BeatManager, Data Surface, Standalone Mode / Synced Mode; <code>docs/osc-client-contract.md</code>: <code>/rave/onair/fill_state</code>).</p>
<p>The port follows <code>{FILL_SOURCE_LINES}</code>: each mirror group's color comes from its first Tile; while Fill is active, RGB→HSV, <code>v=(h+s+v)%1</code>, <code>s=0</code>, HSV→RGB, then the group is copied. The runtime WHY is preserved: “assure there is brightness variation.”</p>
<p>Mono images use the real palette shape from <code>SampleScene.unity</code>, the <code>GPalette.read(..., true)</code> interpolation, the Synced <code>PaletteSaturationFloor={MONO_SATURATION_FLOOR}</code>, and source-image brightness as in <code>{MONO_SOURCE_LINES}</code>. The fixed human-view palette is <code>{html.escape(palette_name)}</code>; the scorecard also measures every scene palette. Beat Hue is fixed at zero and the color pool's stochastic 1-in-3 channel-swap Roll is skipped so every comparison isolates Fill.</p>

<h2>1. Pool image character</h2>
<p class="note">Flat/shaded/hard are proportions of source Neighbor texel changes. Shading share is gentle changes divided by all non-flat changes; low values mean the image's changes are dominated by hard steps.</p>
{_table(character_rows, ['image','pool','size','contrast','L','flat %','shade %','hard %','shading share %'])}

<h2>2. Fill scorecard</h2>
<p class="note">Collapse is the percentage of pre-visible adjacent mirror-group boundaries (OKLab Δ≥0.08) that become near-identical (Δ&lt;0.03). Contrast retention is post/pre OKLab boundary contrast. Inversion compares adjacent normal brightness order with Fill gray order. Wrap is the share of groups where h+s+v crosses 1 before modulo. HSV-value and Rec.709 columns are candidate controls, not ship recommendations.</p>
{_table(score_rows, ['image','pool','layout','exact collapse %','contrast kept %','inversion %','wrap %','gray bins','value collapse %','luminance collapse %'])}

<h2>3. Every image: normal vs exact Fill</h2>
<p>Every pool image appears below in Mirror2 and Mirror10. Each animation runs the same sampling, rotation, ConvertScreenBuffer weights, mirror replication, and motion state on both sides.</p>
<div class="grid">{''.join(fill_cards)}</div>

<h2>4. Candidate pool-level image edits</h2>
<p>These are candidate source-image edits under the current exact mapping. They are selected by mean collapsed-boundary reduction across both layouts, not recommended to ship. The normal view is included because an improvement during Fill can cost the ordinary color look.</p>
{_table(candidate_rows, ['image','pool','candidate edit','baseline collapse %','candidate collapse %','gain points','cost'])}
<div class="grid">{''.join(pool_cards) if pool_cards else '<p>No tested pool edit improved collapse by the reporting threshold.</p>'}</div>

<h2>5. Candidate black-and-white mappings</h2>
<p>Both candidates preserve the settled black-and-white identity and retain per-group brightness variation. HSV value removes hue/saturation and keeps <code>v</code>; Rec.709 converts relative luminance back to display gray. Neither uses modulo, so these panels isolate the mapping mechanism.</p>
<div class="grid">{''.join(mapping_cards)}</div>
</body></html>"""
    (OUT / "viewer.html").write_text(page)


def main() -> None:
    """Generate full-pool Fill, candidate, and viewer artifacts."""
    OUT.mkdir(exist_ok=True)
    layout = parse_layout()
    centers, polygons = tile_geometry(layout["Mesh"])
    nearest_indexes, nearest_weights = init_weights(centers)
    seeds_by_mode = {
        mode: seed_map(layout["shapes"][mode]) for mode in ("mirror2", "mirror10")
    }
    tilemap = rasterize_tilemap(polygons)
    palettes = load_scene_palettes()
    palette_name, palette = palettes[min(PALETTE_VIEW_INDEX, len(palettes) - 1)]

    measure_data = json.loads((OUT / "measure.json").read_text())
    score_path = OUT / "fill_scorecard.json"
    if not score_path.exists():
        raise FileNotFoundError("run `uv run scorecard.py` before `uv run render_wall.py`")
    score_data = json.loads(score_path.read_text())

    fill_artifacts = {}
    mapping_artifacts = {}
    pool_artifacts = {}
    for entry in pool_entries():
        name, pool = entry["name"], entry["pool"]
        raw = load_rgb(name)
        prepared = prepare_source(raw, pool, palette)
        selected = score_data["image_candidates"].get(name)
        for mode in ("mirror2", "mirror10"):
            fill_artifacts[(name, mode)] = _render_fill_gif(
                prepared,
                name,
                pool,
                mode,
                nearest_indexes,
                nearest_weights,
                seeds_by_mode[mode],
                tilemap,
            )
            mapping_artifacts[(name, mode)] = _render_mapping_comparison(
                prepared,
                name,
                pool,
                mode,
                nearest_indexes,
                nearest_weights,
                seeds_by_mode[mode],
                tilemap,
            )
            if selected is not None:
                pool_artifacts[(name, mode)] = _render_pool_candidate(
                    raw,
                    pool,
                    palette,
                    selected["adjustment"],
                    name,
                    mode,
                    nearest_indexes,
                    nearest_weights,
                    seeds_by_mode[mode],
                    tilemap,
                )

    write_viewer(
        measure_data,
        score_data,
        fill_artifacts,
        mapping_artifacts,
        pool_artifacts,
        palette_name,
    )
    outputs = [path for path in OUT.iterdir() if path.suffix.lower() in {".gif", ".png", ".html"}]
    total_mb = sum(path.stat().st_size for path in outputs) / 1e6
    print(f"palette model: {palette_name} ({len(palettes)} scene palettes; mono S floor {MONO_SATURATION_FLOOR})")
    print(f"rendered {len(pool_entries())} images x 2 layouts; no image cap")
    print(f"wrote {OUT / 'viewer.html'}")
    print(f"viewer artifacts currently total {total_mb:.1f} MB in {OUT}")


if __name__ == "__main__":
    main()
