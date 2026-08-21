#!/usr/bin/env python3
"""Build the throwaway Julia shading desk simulation."""

from __future__ import annotations

import colorsys
import json
import math
import re
import statistics
from pathlib import Path


HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
LAYOUT_PATH = REPO / "Assets/StreamingAssets/penrose_layout.txt"
SCENE_PATH = REPO / "Assets/Scenes/SampleScene.unity"
SETTINGS_PATH = (
    REPO
    / "Assets/effects/Resources/EffectStandaloneSettings/JuliaSettings.asset"
)
PENROSE_PATH = REPO / "Assets/core/Runtime/Penrose.cs"
OUTPUT_PATH = HERE / "out/sim.html"

PALETTE_LENGTH = 32
EXPECTED_MESH_FLOATS = 10_800
EXPECTED_TILES = 900
EXPECTED_PRESETS = 5
EXPECTED_DEFINITIONS = 17
EXPECTED_UNIQUE_PALETTES = 16
EXPECTED_BOUNDS_WIDTH = 50.0
DIVE_RATE_DEFAULT = 0.35
DIVE_BOOST_MULTIPLIER_DEFAULT = 4.0
DIVE_WRAP_THRESHOLD = 0.000006
DIVE_HUE_STEP_DEFAULT = 0.04
FRACTAL_BRANCH_MULTIPLIER_FLOOR = 1.1
FRACTAL_TARGET_EPSILON_TILES = 0.05
TILE_SOFTENING_DEFAULT = 1.1
GLIDE_TRAVEL_SPEED_DEFAULT = 0.055
GLIDE_WINDOW_DEFAULT = 1.2
GLIDE_DISTANCE_RATIO = 0.02
GLIDE_BOOST_MULTIPLIER = 4.0
GLIDE_BOOST_DEPTH = 1.35
GLIDE_BOOST_IN_RATE = 3.0
GLIDE_BOOST_OUT_RATE = 1.2
GLIDE_GRADIENT_STEP_RATIO = 0.004
GLIDE_ROLL_RANGE = 0.16
GLIDE_ROLL_DAMPING = 2.5

CANDIDATE_PRESETS = (
    {
        "name": "Douady rabbit",
        "constant": (-0.1226, 0.7449),
        "viewCenter": (0.375, 0.25),
        "morphScale": 1.0,
    },
    {
        "name": "Basilica",
        "constant": (-1.0, 0.0),
        "viewCenter": (0.0, 0.575),
        "morphScale": 1.0,
    },
    {
        "name": "Siegel disk",
        "constant": (-0.3905, -0.5868),
        "viewCenter": (0.375, -0.125),
        "morphScale": 1.0,
    },
    {
        "name": "Dendrite c = i",
        "constant": (0.0, 1.0),
        "viewCenter": (0.0, 0.0),
        "morphScale": 0.02,
        "boundaryOnly": True,
    },
    {
        "name": "Misiurewicz",
        "constant": (-0.1011, 0.9563),
        "viewCenter": (0.0, 0.0),
        "morphScale": 0.02,
        "boundaryOnly": True,
    },
    {
        "name": "Upper-limb Misiurewicz",
        "constant": (-0.228155493653962, 1.115142508039937),
        "viewCenter": (-0.125, 0.25),
        "morphScale": 0.02,
        "boundaryOnly": True,
    },
)

Color = tuple[float, float, float]
Point = tuple[float, float]


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def lerp(start: float, end: float, amount: float) -> float:
    return start + ((end - start) * clamp01(amount))


def lerp_color(start: Color, end: Color, amount: float) -> Color:
    return tuple(lerp(a, b, amount) for a, b in zip(start, end))  # type: ignore[return-value]


def relative_luminance(color: Color) -> float:
    return (0.2126 * color[0]) + (0.7152 * color[1]) + (0.0722 * color[2])


def repeat(value: float) -> float:
    return value - math.floor(value)


def lerp_hue(start: float, end: float, amount: float) -> float:
    delta = repeat(end - start + 0.5) - 0.5
    return repeat(start + (delta * amount))


def palette_distance(a: Color, b: Color) -> float:
    hue_a, saturation_a, _ = colorsys.rgb_to_hsv(*a)
    hue_b, saturation_b, _ = colorsys.rgb_to_hsv(*b)
    hue_distance = abs(hue_a - hue_b)
    hue_distance = min(hue_distance, 1.0 - hue_distance) * 2.0
    hue_distance *= min(saturation_a, saturation_b)
    saturation_distance = abs(saturation_a - saturation_b)
    luminance_distance = abs(relative_luminance(a) - relative_luminance(b))
    return math.sqrt(
        (hue_distance * hue_distance)
        + (0.25 * saturation_distance * saturation_distance)
        + (luminance_distance * luminance_distance)
    )


def try_read_hue_donor(color: Color, dark_threshold: float) -> tuple[bool, float, float]:
    hue, saturation, _ = colorsys.rgb_to_hsv(*color)
    usable = relative_luminance(color) > dark_threshold and saturation > 0.05
    return usable, hue, saturation


def borrow_neighbour_hue(
    colors: list[Color], source_index: int, dark_threshold: float
) -> tuple[float, float]:
    previous: tuple[float, float, int] | None = None
    following: tuple[float, float, int] | None = None
    length = len(colors)
    for distance in range(1, length):
        if previous is None:
            usable, hue, saturation = try_read_hue_donor(
                colors[(source_index - distance + length) % length],
                dark_threshold,
            )
            if usable:
                previous = (hue, saturation, distance)
        if following is None:
            usable, hue, saturation = try_read_hue_donor(
                colors[(source_index + distance) % length],
                dark_threshold,
            )
            if usable:
                following = (hue, saturation, distance)
        if previous is not None and following is not None:
            break

    if previous is not None and following is not None:
        amount = previous[2] / float(previous[2] + following[2])
        return (
            lerp_hue(previous[0], following[0], amount),
            lerp(previous[1], following[1], amount),
        )
    if previous is not None:
        return previous[0], previous[1]
    if following is not None:
        return following[0], following[1]
    return 0.0, 1.0


def repair_dark_color(
    colors: list[Color], source_index: int, conditioning: dict[str, float]
) -> Color:
    source = colors[source_index]
    hue, saturation, value = colorsys.rgb_to_hsv(*source)
    if saturation <= 0.05 or value <= 0.0001:
        hue, saturation = borrow_neighbour_hue(
            colors,
            source_index,
            conditioning["dark_luminance_threshold"],
        )
    vivid = colorsys.hsv_to_rgb(hue, saturation, 1.0)
    target = max(
        conditioning["target_luminance"],
        conditioning["minimum_luminance"],
    )
    scale = min(target / relative_luminance(vivid), 1.0)
    return tuple(channel * scale for channel in vivid)  # type: ignore[return-value]


def equalize_luminance(
    source: Color,
    luminance: float,
    equalization: float,
    palette_lift: float,
    conditioning: dict[str, float],
) -> Color:
    target = lerp(
        luminance * palette_lift,
        conditioning["target_luminance"],
        equalization,
    )
    target = max(target, conditioning["minimum_luminance"])
    scale = target / luminance
    scale = min(scale, max(1.0, conditioning["maximum_luminance_scale"]))
    scale = min(scale, 1.0 / max(source))
    return tuple(channel * scale for channel in source)  # type: ignore[return-value]


def collapse_near_duplicates(colors: list[Color], threshold: float) -> list[Color]:
    threshold = max(0.0, threshold)
    anchors = [colors[0]]
    for color in colors[1:]:
        if palette_distance(anchors[-1], color) >= threshold:
            anchors.append(color)
    if len(anchors) > 1 and palette_distance(anchors[-1], anchors[0]) < threshold:
        anchors.pop()
    return anchors


def redistribute(
    anchors: list[Color],
    output_length: int,
    hue_redistribution: float,
) -> list[Color]:
    if len(anchors) == 1:
        return [anchors[0]] * output_length

    raw_distances = [
        palette_distance(anchor, anchors[(index + 1) % len(anchors)])
        for index, anchor in enumerate(anchors)
    ]
    average_distance = sum(raw_distances) / len(anchors)
    redistribution = clamp01(hue_redistribution)
    segment_lengths = [
        lerp(
            1.0,
            distance / average_distance if average_distance > 0.0 else 1.0,
            redistribution,
        )
        for distance in raw_distances
    ]
    path_length = sum(segment_lengths)
    output: list[Color] = []
    segment = 0
    segment_start = 0.0
    segment_end = segment_lengths[0]
    for index in range(output_length):
        target = index / float(output_length) * path_length
        while segment < len(anchors) - 1 and target > segment_end:
            segment_start = segment_end
            segment += 1
            segment_end += segment_lengths[segment]
        segment_length = segment_lengths[segment]
        amount = (
            (target - segment_start) / segment_length
            if segment_length > 0.0
            else 0.0
        )
        output.append(
            lerp_color(
                anchors[segment],
                anchors[(segment + 1) % len(anchors)],
                amount,
            )
        )
    return output


def condition_palette(
    colors: list[Color],
    conditioning: dict[str, float],
) -> list[Color]:
    luminances = [relative_luminance(color) for color in colors]
    hue_x = 0.0
    hue_y = 0.0
    saturation_total = 0.0
    for color in colors:
        hue, saturation, _ = colorsys.rgb_to_hsv(*color)
        radians = hue * 2.0 * math.pi
        hue_x += saturation * math.cos(radians)
        hue_y += saturation * math.sin(radians)
        saturation_total += saturation
    mean_luminance = sum(luminances) / len(colors)
    hue_spread = (
        1.0 - (math.hypot(hue_x, hue_y) / saturation_total)
        if saturation_total > 0.0
        else 0.0
    )
    equalization = clamp01(conditioning["luminance_equalization"]) * clamp01(
        hue_spread / max(0.001, conditioning["hue_spread_reference"])
    )
    palette_lift = (
        min(
            conditioning["target_luminance"] / mean_luminance,
            max(1.0, conditioning["maximum_luminance_scale"]),
        )
        if mean_luminance > 0.0
        else 1.0
    )

    balanced = []
    for index, (color, luminance) in enumerate(zip(colors, luminances)):
        if luminance <= conditioning["dark_luminance_threshold"]:
            balanced.append(repair_dark_color(colors, index, conditioning))
        else:
            balanced.append(
                equalize_luminance(
                    color,
                    luminance,
                    equalization,
                    palette_lift,
                    conditioning,
                )
            )
    anchors = collapse_near_duplicates(
        balanced,
        conditioning["duplicate_threshold"],
    )
    return redistribute(
        anchors,
        len(colors),
        conditioning["hue_redistribution"],
    )


def read_scalar(source: str, name: str) -> float:
    match = re.search(rf"^\s+{re.escape(name)}: (-?\d+(?:\.\d+)?)$", source, re.M)
    if match is None:
        raise RuntimeError(f"Julia setting {name} was not found")
    return float(match.group(1))


def read_range(source: str, name: str) -> tuple[float, float]:
    match = re.search(
        rf"^\s+{re.escape(name)}:\n\s+Min: (-?\d+(?:\.\d+)?)\n\s+Max: (-?\d+(?:\.\d+)?)$",
        source,
        re.M,
    )
    if match is None:
        raise RuntimeError(f"Julia range {name} was not found")
    return float(match.group(1)), float(match.group(2))


def read_vectors(source: str, name: str) -> list[Point]:
    marker = f"    {name}:\n"
    start = source.find(marker)
    if start < 0:
        raise RuntimeError(f"Julia vector table {name} was not found")
    tail = source[start + len(marker) :]
    block_lines = []
    for line in tail.splitlines():
        if not line.startswith("    - "):
            break
        block_lines.append(line)
    vectors = []
    for line in block_lines:
        match = re.search(r"\{x: (-?\d+(?:\.\d+)?), y: (-?\d+(?:\.\d+)?)\}", line)
        if match is None:
            raise RuntimeError(f"invalid Julia vector in {name}: {line}")
        vectors.append((float(match.group(1)), float(match.group(2))))
    return vectors


def load_julia_settings() -> dict[str, object]:
    source = SETTINGS_PATH.read_text(encoding="utf-8")
    constants = read_vectors(source, "JuliaConstants")
    view_centers = read_vectors(source, "PresetViewCenters")
    assert len(constants) == EXPECTED_PRESETS, (
        f"expected {EXPECTED_PRESETS} Julia constants, got {len(constants)}"
    )
    assert len(view_centers) == EXPECTED_PRESETS, (
        f"expected {EXPECTED_PRESETS} Julia view centers, got {len(view_centers)}"
    )

    conditioning = {
        "target_luminance": read_scalar(source, "TargetLuminance"),
        "minimum_luminance": read_scalar(source, "MinimumLuminance"),
        "luminance_equalization": read_scalar(source, "LuminanceEqualization"),
        "hue_spread_reference": read_scalar(source, "HueSpreadReference"),
        "maximum_luminance_scale": read_scalar(source, "MaximumLuminanceScale"),
        "dark_luminance_threshold": read_scalar(source, "DarkLuminanceThreshold"),
        "duplicate_threshold": read_scalar(source, "DuplicateThreshold"),
        "hue_redistribution": read_scalar(source, "HueRedistribution"),
    }
    breathing_min, breathing_max = read_range(source, "BreathingZoomSpeed")
    window_min, window_max = read_range(source, "WindowWidth")
    return {
        "breathingZoomSpeed": [breathing_min, breathing_max],
        "depth": read_scalar(source, "Depth"),
        "constantMorphRadius": read_scalar(source, "ConstantMorphRadius"),
        "constantMorphRate": read_scalar(source, "ConstantMorphRate"),
        "windowWidth": [window_min, window_max],
        "edgeLockFraming": read_scalar(source, "EdgeLockFraming"),
        "fogDensity": read_scalar(source, "FogDensity"),
        "fogBrightnessFloor": read_scalar(source, "FogBrightnessFloor"),
        "fogBrightensTowardSet": bool(
            read_scalar(source, "FogBrightensTowardSet")
        ),
        "reliefLightAzimuth": read_scalar(source, "ReliefLightAzimuth"),
        "reliefShadingDepth": read_scalar(source, "ReliefShadingDepth"),
        "paletteConditioning": conditioning,
        "paletteChance": read_scalar(source, "PaletteChance"),
        "hueBaseRate": read_scalar(source, "HueBaseRate"),
        "hueBeatRate": read_scalar(source, "HueBeatRate"),
        "hueCycleDrive": read_scalar(source, "HueCycleDrive"),
        "presets": [
            {"constant": constant, "viewCenter": view_center}
            for constant, view_center in zip(constants, view_centers)
        ],
    }


def map_gradient(position: float, table: list[tuple[float, Color]]) -> Color:
    for left, right in zip(table, table[1:]):
        if left[0] <= position <= right[0]:
            span = right[0] - left[0]
            amount = 0.0 if span == 0.0 else (position - left[0]) / span
            return lerp_color(left[1], right[1], amount)
    return 0.0, 0.0, 0.0


def load_scene_palettes(
    conditioning: dict[str, float],
) -> list[dict[str, object]]:
    scene = SCENE_PATH.read_text(encoding="utf-8")
    source_match = re.search(
        r"^  paletteSource: '(.*?)'\n^  jsonSource:",
        scene,
        flags=re.M | re.S,
    )
    if source_match is None:
        raise RuntimeError("SampleScene paletteSource was not found")
    definitions = re.findall(
        r"DEFINE_GRADIENT_PALETTE\(\s*([^)]*?)\s*\)\s*\{([^}]*)\}",
        source_match.group(1),
        flags=re.S,
    )
    assert len(definitions) == EXPECTED_DEFINITIONS, (
        f"expected {EXPECTED_DEFINITIONS} gradient definitions, got {len(definitions)}"
    )

    palettes: list[dict[str, object]] = []
    seen: set[str] = set()
    for raw_name, raw_data in definitions:
        name = "".join(raw_name.split())
        if name in seen:
            continue
        seen.add(name)
        values = [int(value) for value in re.findall(r"\d+", raw_data)]
        assert values and len(values) % 4 == 0, f"invalid gradient table for {name}"
        assert all(0 <= value <= 255 for value in values), name
        table = [
            (
                values[index] / 255.0,
                tuple(
                    channel / 255.0
                    for channel in values[index + 1 : index + 4]
                ),
            )
            for index in range(0, len(values), 4)
        ]
        raw = [
            map_gradient(index / PALETTE_LENGTH, table)
            for index in range(PALETTE_LENGTH)
        ]
        palettes.append(
            {
                "name": name,
                "conditioned": condition_palette(raw, conditioning),
            }
        )

    assert len(palettes) == EXPECTED_UNIQUE_PALETTES, (
        f"expected {EXPECTED_UNIQUE_PALETTES} uniquely named palettes, "
        f"got {len(palettes)}"
    )
    return palettes


def load_full_scale() -> float:
    source = PENROSE_PATH.read_text(encoding="utf-8")
    match = re.search(
        r"public const float FullScale = ([\d.]+)f / ([\d.]+)f;",
        source,
    )
    if match is None:
        raise RuntimeError("Penrose.FullScale was not found")
    return float(match.group(1)) / float(match.group(2))


def tile_from_mesh(
    mesh: list[float],
    tile_index: int,
    full_scale: float,
) -> dict[str, object]:
    unique: dict[Point, None] = {}
    for point_index in range(6):
        offset = (tile_index * 12) + (point_index * 2)
        unique[
            (
                mesh[offset] * full_scale,
                -mesh[offset + 1] * full_scale,
            )
        ] = None
    assert len(unique) == 4, (
        f"Tile {tile_index} resolved to {len(unique)} corners instead of 4"
    )
    points = list(unique)
    center_x = sum(point[0] for point in points) / len(points)
    center_y = sum(point[1] for point in points) / len(points)
    points.sort(key=lambda point: math.atan2(point[1] - center_y, point[0] - center_x))
    return {"center": [center_x, center_y], "polygon": points}


def median_tile_pitch(tiles: list[dict[str, object]]) -> float:
    centers = [tile["center"] for tile in tiles]
    nearest = []
    for index, center in enumerate(centers):
        nearest.append(
            min(
                math.hypot(
                    center[0] - other[0],
                    center[1] - other[1],
                )
                for other_index, other in enumerate(centers)
                if other_index != index
            )
        )
    return statistics.median(nearest)


def runtime_bounds_width(tiles: list[dict[str, object]]) -> float:
    min_x = 1_000_000.0
    max_x = -1_000_000.0
    for tile in tiles:
        x = tile["center"][0]
        min_x = round(min(min_x, x))
        max_x = round(max(max_x, x))
    return (max_x + 5) - (min_x - 5)


def load_layout() -> tuple[list[dict[str, object]], float, float]:
    source = LAYOUT_PATH.read_text(encoding="utf-8-sig")
    stripped = "\n".join(
        line
        for line in source.splitlines()
        if not line.lstrip().startswith("//")
    )
    layout = json.loads(stripped)
    mesh = layout["Mesh"]
    assert len(mesh) == EXPECTED_MESH_FLOATS, (
        f"expected {EXPECTED_MESH_FLOATS} mesh floats, got {len(mesh)}"
    )
    tile_count = len(mesh) // 12
    assert tile_count == EXPECTED_TILES, (
        f"expected {EXPECTED_TILES} Tiles, got {tile_count}"
    )
    full_scale = load_full_scale()
    tiles = [
        tile_from_mesh(mesh, index, full_scale)
        for index in range(tile_count)
    ]
    bounds_width = runtime_bounds_width(tiles)
    assert bounds_width == EXPECTED_BOUNDS_WIDTH, (
        f"expected Penrose bounds width {EXPECTED_BOUNDS_WIDTH}, got {bounds_width}"
    )
    return tiles, median_tile_pitch(tiles), bounds_width


HTML = r'''<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Julia shading: desk prototype</title>
  <style>
    :root {
      color-scheme: dark;
      --page: #080a0f;
      --panel: #11151d;
      --panel-2: #171c26;
      --line: #2b3340;
      --text: #eef1f5;
      --muted: #929eaf;
      --accent: #ffc266;
      --accent-2: #ff7456;
      --focus: #83d5ff;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      min-width: 320px;
      background:
        radial-gradient(circle at 25% -10%, rgba(255, 116, 86, 0.12), transparent 34rem),
        var(--page);
      color: var(--text);
      font: 14px/1.45 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }

    button, select, input { font: inherit; }

    button, select {
      border: 1px solid var(--line);
      border-radius: 9px;
      background: var(--panel-2);
      color: var(--text);
    }

    button { padding: 9px 12px; cursor: pointer; }
    button:hover, select:hover { border-color: #4a5669; }
    button:focus-visible, select:focus-visible, input:focus-visible {
      outline: 2px solid var(--focus);
      outline-offset: 2px;
    }

    main {
      width: min(1500px, calc(100% - 28px));
      margin: 0 auto;
      padding: 24px 0 36px;
    }

    .intro {
      display: flex;
      align-items: end;
      justify-content: space-between;
      gap: 24px;
      margin-bottom: 16px;
    }

    .eyebrow {
      margin: 0 0 5px;
      color: var(--accent);
      font-size: 11px;
      font-weight: 750;
      letter-spacing: 0.12em;
      text-transform: uppercase;
    }

    h1 {
      margin: 0;
      font-size: clamp(28px, 4vw, 50px);
      line-height: 1;
      letter-spacing: -0.045em;
    }

    .question {
      max-width: 820px;
      margin: 10px 0 0;
      color: #c7cfda;
      font-size: 15px;
    }

    .facts {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      gap: 7px;
    }

    .fact {
      padding: 5px 8px;
      border: 1px solid var(--line);
      border-radius: 999px;
      color: var(--muted);
      font-size: 11px;
      white-space: nowrap;
    }

    .workspace {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 360px;
      gap: 14px;
      align-items: start;
    }

    .stage, .controls {
      border: 1px solid var(--line);
      border-radius: 16px;
      background: rgba(17, 21, 29, 0.94);
      box-shadow: 0 18px 52px rgba(0, 0, 0, 0.24);
    }

    .stage { overflow: hidden; }

    .canvas-wrap {
      position: relative;
      aspect-ratio: 50 / 22;
      min-height: 280px;
      background: #010205;
    }

    #wall {
      display: block;
      width: 100%;
      height: 100%;
    }

    .live-badge {
      position: absolute;
      top: 11px;
      left: 11px;
      padding: 5px 8px;
      border: 1px solid rgba(255, 255, 255, 0.14);
      border-radius: 999px;
      background: rgba(2, 4, 8, 0.76);
      color: #dce3ec;
      font-size: 11px;
      backdrop-filter: blur(7px);
    }

    .live-badge::before {
      display: inline-block;
      width: 7px;
      height: 7px;
      margin-right: 6px;
      border-radius: 50%;
      background: #55d47a;
      content: "";
    }

    .live-badge.paused::before { background: var(--accent); }

    .state-bar {
      display: grid;
      grid-template-columns: 1.25fr 1.25fr 0.8fr 0.8fr;
      border-top: 1px solid var(--line);
    }

    .state-item {
      min-width: 0;
      padding: 11px 13px;
    }

    .state-item + .state-item { border-left: 1px solid var(--line); }
    .state-label {
      color: var(--muted);
      font-size: 10px;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    .state-value {
      overflow: hidden;
      margin-top: 2px;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-variant-numeric: tabular-nums;
    }

    .controls { padding: 14px; }

    .round-five-brief {
      margin: 0 0 14px;
      padding: 10px 11px;
      border: 1px solid #79533a;
      border-radius: 9px;
      background: #251b16;
      color: #f0d5bd;
      font-size: 12px;
      line-height: 1.4;
    }

    .round-five-brief strong { color: #fff4e8; }

    .control-group + .control-group,
    .parameter-panel + .parameter-panel {
      margin-top: 15px;
      padding-top: 15px;
      border-top: 1px solid var(--line);
    }

    .control-title {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 8px;
      color: #dce3ec;
      font-size: 11px;
      font-weight: 750;
      letter-spacing: 0.07em;
      text-transform: uppercase;
    }

    .hint {
      margin: 7px 0 0;
      color: var(--muted);
      font-size: 11px;
    }

    .model-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 5px;
    }

    .model-grid label { min-width: 0; }
    .model-grid input { position: absolute; opacity: 0; pointer-events: none; }
    .model-grid span {
      display: block;
      overflow: hidden;
      padding: 8px 7px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: #0d1118;
      color: var(--muted);
      text-align: center;
      text-overflow: ellipsis;
      white-space: nowrap;
      cursor: pointer;
    }

    .model-grid input:checked + span {
      border-color: #79533a;
      background: #342219;
      color: var(--text);
    }

    select { width: 100%; padding: 8px 9px; }
    select + select { margin-top: 7px; }

    .copy-field {
      width: 100%;
      margin-top: 9px;
      padding: 8px 9px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: #0d1118;
      color: #dce3ec;
      font: 11px/1.4 ui-monospace, SFMono-Regular, Consolas, monospace;
    }

    .palette-strip {
      display: block;
      width: 100%;
      height: 20px;
      margin-top: 8px;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 5px;
      background: #05070a;
    }

    .slider-row + .slider-row { margin-top: 10px; }
    select + .slider-row { margin-top: 12px; }
    .slider-head {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 4px;
    }
    .slider-value {
      color: var(--accent);
      font-variant-numeric: tabular-nums;
    }
    input[type="range"] { width: 100%; accent-color: var(--accent-2); }

    .check-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding-top: 4px;
    }
    .check-row input { width: 18px; height: 18px; accent-color: var(--accent-2); }

    .parameter-panel[hidden] { display: none; }
    .baseline-note[hidden] { display: none; }

    .baseline-note {
      padding: 9px 10px;
      border: 1px solid #394252;
      border-radius: 8px;
      background: #0d1118;
      color: var(--muted);
      font-size: 11px;
    }

    .actions {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 7px;
    }
    .actions .primary { border-color: #79533a; background: #342219; }
    .actions .boosting { border-color: #d89b66; background: #5a311d; }
    #boostButton { touch-action: none; }

    .primary-boost {
      width: 100%;
      margin: 9px 0 7px;
      border-color: #79533a;
      background: #342219;
      touch-action: none;
    }

    .primary-boost.boosting {
      border-color: #d89b66;
      background: #5a311d;
    }

    .earlier-rounds summary {
      color: #aeb7c4;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      cursor: pointer;
    }

    .earlier-rounds[open] summary { margin-bottom: 10px; }

    @media (max-width: 980px) {
      .intro { align-items: start; flex-direction: column; }
      .facts { justify-content: flex-start; }
      .workspace { grid-template-columns: 1fr; }
      .controls {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 16px;
      }
      .control-group + .control-group,
      .parameter-panel + .parameter-panel {
        margin: 0;
        padding: 0;
        border: 0;
      }
    }

    @media (max-width: 640px) {
      main { width: min(100% - 18px, 1500px); padding-top: 18px; }
      .canvas-wrap { min-height: 190px; }
      .state-bar { grid-template-columns: 1fr 1fr; }
      .state-item:nth-child(3) { border-top: 1px solid var(--line); border-left: 0; }
      .state-item:nth-child(4) { border-top: 1px solid var(--line); }
      .controls { display: block; }
      .control-group + .control-group,
      .parameter-panel + .parameter-panel {
        margin-top: 15px;
        padding-top: 15px;
        border-top: 1px solid var(--line);
      }
    }
  </style>
</head>
<body>
  <main>
    <header class="intro">
      <div>
        <p class="eyebrow">Prototype · throwaway desk wall</p>
        <h1>Julia preset audition</h1>
        <p class="question">Can Julia travel forever without diving past the wall's useful resolution? Judge a constant-altitude boundary glide at the rich mid-zoom band, then use the earlier rounds only when a comparison earns the detour.</p>
      </div>
      <div class="facts" aria-label="Parsed source facts">
        <span class="fact">900 Tiles</span>
        <span class="fact">5 authored + 6 candidates</span>
        <span class="fact">16 scene palettes</span>
        <span class="fact">Boundary glide</span>
      </div>
    </header>

    <div class="workspace">
      <section class="stage" aria-label="Julia wall simulation">
        <div class="canvas-wrap">
          <canvas id="wall"></canvas>
          <div class="live-badge" id="liveBadge">Running</div>
        </div>
        <div class="state-bar">
          <div class="state-item">
            <div class="state-label">Shading</div>
            <div class="state-value" id="modelState">Baseline</div>
          </div>
          <div class="state-item">
            <div class="state-label">Preset</div>
            <div class="state-value" id="presetState"></div>
          </div>
          <div class="state-item">
            <div class="state-label">Window</div>
            <div class="state-value" id="windowState"></div>
          </div>
          <div class="state-item">
            <div class="state-label">Hue rate</div>
            <div class="state-value" id="hueState"></div>
          </div>
        </div>
      </section>

      <aside class="controls">
        <p class="round-five-brief"><strong>Round 5 — judge this:</strong> the boundary glide follows Julia's coastline at fixed altitude. Hold boost to travel faster and push in. Press New journey for another start and direction.</p>

        <section class="control-group">
          <div class="control-title">Boundary glide · current round</div>
          <select id="presetSelect" aria-label="Julia preset"></select>
          <div id="glideControls">
            <div class="slider-row">
              <div class="slider-head"><label for="glideSpeed">Travel speed</label><span class="slider-value" id="glideSpeedValue"></span></div>
              <input id="glideSpeed" type="range" min="0.01" max="0.2" step="0.005">
            </div>
            <div class="slider-row">
              <div class="slider-head"><label for="glideWindow">Window · altitude</label><span class="slider-value" id="glideWindowValue"></span></div>
              <input id="glideWindow" type="range" min="0.35" max="2.5" step="0.05">
            </div>
          </div>
          <button class="primary-boost" id="boostButton" type="button">Hold boost</button>
          <div class="actions">
            <button class="primary" id="journeyButton" type="button">New journey</button>
            <button id="pauseButton" type="button">Pause</button>
          </div>
          <p class="hint" id="journeyState"></p>
        </section>

        <section class="control-group">
          <div class="control-title">Approved look</div>
          <div class="model-grid">
            <label><input type="radio" name="model" value="baseline"><span>Baseline</span></label>
            <label><input type="radio" name="model" value="distance"><span>Distance rim</span></label>
            <label><input type="radio" name="model" value="relief"><span>Relief</span></label>
            <label><input type="radio" name="model" value="fog"><span>Depth fog</span></label>
            <label><input type="radio" name="model" value="contours"><span>Fat contours</span></label>
            <label><input type="radio" name="model" value="fog-relief" checked><span>Fog + relief</span></label>
            <label><input type="radio" name="model" value="rim-fog"><span>Rim + fog</span></label>
          </div>
          <select id="colorSelect" aria-label="Julia color path">
            <option value="palette">Conditioned scene palette</option>
            <option value="hsv">HSV rainbow</option>
          </select>
          <select id="paletteSelect" aria-label="Scene palette"></select>
          <canvas class="palette-strip" id="paletteStrip"></canvas>
          <input class="copy-field" id="presetPairText" type="text" aria-label="Copyable Julia constant and view center" readonly spellcheck="false">
          <p class="hint">Cold load uses the original absolute fog-plus-relief shading. Look changes preserve the journey.</p>
        </section>

        <details class="control-group earlier-rounds" id="earlierRounds">
          <summary>Earlier rounds (superseded)</summary>
          <select id="journeyMode" aria-label="Journey mode">
            <option value="glide" selected>Boundary glide · Round 5</option>
            <option value="fractal">Fractal dive</option>
            <option value="corridor">Corridor · fixed point</option>
            <option value="breathing">Breathing</option>
          </select>
          <div id="diveControls">
            <label class="check-row" for="diveAbsoluteColoring"><span>Absolute coloring (pops at rebases)</span><input id="diveAbsoluteColoring" type="checkbox"></label>
            <div id="fractalControls">
              <div class="slider-row">
                <div class="slider-head"><label for="branchChoice">Branch choice</label><span class="slider-value">A/B</span></div>
                <select id="branchChoice" aria-label="Fractal branch choice">
                  <option value="steered" selected>Steered</option>
                  <option value="random">Random</option>
                </select>
              </div>
            </div>
            <div class="slider-row">
              <div class="slider-head"><label for="diveHueStep">Hue band density</label><span class="slider-value" id="diveHueStepValue"></span></div>
              <input id="diveHueStep" type="range" min="0.005" max="0.1" step="0.001">
            </div>
            <div class="slider-row">
              <div class="slider-head"><label for="tileSoftening">Tile softening</label><span class="slider-value" id="tileSofteningValue"></span></div>
              <input id="tileSoftening" type="range" min="0.25" max="2.5" step="0.05">
            </div>
            <div class="slider-row">
              <div class="slider-head"><label for="diveRate">Dive rate</label><span class="slider-value" id="diveRateValue"></span></div>
              <input id="diveRate" type="range" min="0.05" max="1" step="0.01">
            </div>
            <div class="slider-row">
              <div class="slider-head"><label for="diveBoostMultiplier">Boost multiplier</label><span class="slider-value" id="diveBoostMultiplierValue"></span></div>
              <input id="diveBoostMultiplier" type="range" min="1" max="8" step="0.25">
            </div>
            <div class="slider-row">
              <div class="slider-head"><label for="diveWrapThreshold">Rebase threshold</label><span class="slider-value" id="diveWrapThresholdValue"></span></div>
              <input id="diveWrapThreshold" type="range" min="0.000001" max="0.002" step="0.000001">
            </div>
          </div>
          <div id="breathingCenterControls" hidden>
            <div class="slider-row">
              <div class="slider-head"><label for="viewCenterX">View center x</label><span class="slider-value" id="viewCenterXValue"></span></div>
              <input id="viewCenterX" type="range" min="-1.75" max="1.75" step="0.0005">
            </div>
            <div class="slider-row">
              <div class="slider-head"><label for="viewCenterY">View center y</label><span class="slider-value" id="viewCenterYValue"></span></div>
              <input id="viewCenterY" type="range" min="-1.35" max="1.35" step="0.0005">
            </div>
            <p class="hint">Each preset keeps its own desk center. Moving either dial re-runs the edge-lock seed search.</p>
          </div>
        </details>

        <div class="baseline-note" id="baselineNote">
          Boundary glide, breathing, and absolute dive modes show current Julia color: full brightness outside and black inside. The invariant dive modes keep the 8-sample AA but use linear hue bands and the shared boundary-limit color.
        </div>

        <section class="parameter-panel" data-models="distance rim-fog" hidden>
          <div class="control-title">Distance rim</div>
          <div class="slider-row">
            <div class="slider-head"><label for="rimThickness">Thickness</label><span class="slider-value" id="rimThicknessValue"></span></div>
            <input id="rimThickness" type="range" min="0.25" max="5" step="0.05">
          </div>
          <div class="slider-row">
            <div class="slider-head"><label for="rimStrength">Strength</label><span class="slider-value" id="rimStrengthValue"></span></div>
            <input id="rimStrength" type="range" min="0" max="1" step="0.01">
          </div>
          <div class="slider-row">
            <div class="slider-head"><label for="rimPolarity">Polarity</label><span class="slider-value" id="rimPolarityValue"></span></div>
            <input id="rimPolarity" type="range" min="-1" max="1" step="0.1">
          </div>
        </section>

        <section class="parameter-panel" data-models="relief fog-relief" hidden>
          <div class="control-title">Relief lighting</div>
          <div class="slider-row">
            <div class="slider-head"><label for="lightAzimuth">Light azimuth</label><span class="slider-value" id="lightAzimuthValue"></span></div>
            <input id="lightAzimuth" type="range" min="0" max="360" step="1">
          </div>
          <div class="slider-row">
            <div class="slider-head"><label for="reliefDepth">Shading depth</label><span class="slider-value" id="reliefDepthValue"></span></div>
            <input id="reliefDepth" type="range" min="0" max="1" step="0.01">
          </div>
        </section>

        <section class="parameter-panel" data-models="fog fog-relief rim-fog" hidden>
          <div class="control-title">Depth fog</div>
          <div class="slider-row">
            <div class="slider-head"><label for="fogDensity">Density</label><span class="slider-value" id="fogDensityValue"></span></div>
            <input id="fogDensity" type="range" min="0.1" max="8" step="0.1">
          </div>
          <div class="slider-row">
            <div class="slider-head"><label for="fogFloor">Brightness floor</label><span class="slider-value" id="fogFloorValue"></span></div>
            <input id="fogFloor" type="range" min="0" max="0.8" step="0.01">
          </div>
          <label class="check-row" for="fogInvert"><span>Invert near/far</span><input id="fogInvert" type="checkbox"></label>
        </section>

        <section class="parameter-panel" data-models="contours" hidden>
          <div class="control-title">Fat contours</div>
          <div class="slider-row">
            <div class="slider-head"><label for="bandFrequency">Band frequency</label><span class="slider-value" id="bandFrequencyValue"></span></div>
            <input id="bandFrequency" type="range" min="0.02" max="1" step="0.01">
          </div>
          <div class="slider-row">
            <div class="slider-head"><label for="bandContrast">Band contrast</label><span class="slider-value" id="bandContrastValue"></span></div>
            <input id="bandContrast" type="range" min="0" max="1" step="0.01">
          </div>
          <div class="slider-row">
            <div class="slider-head"><label for="bandDuty">Bright duty</label><span class="slider-value" id="bandDutyValue"></span></div>
            <input id="bandDuty" type="range" min="0.2" max="0.8" step="0.01">
          </div>
          <p class="hint">Local frequency is capped so both bright and dark bands remain at least one median Tile pitch wide.</p>
        </section>
      </aside>
    </div>
  </main>

  <script>
    "use strict";

    const DATA = __PAYLOAD__;
    const ITERATIONS = 100;
    const DIVE_ITERATIONS = 160;
    const LN2 = 0.6931472;
    const EDGE_LOCK_DEPTH = 12;
    const REPELLING_FIXED_POINT_MAGNITUDE_SQUARED = 0.25;
    const AA_SAMPLES = 8;
    const AA_RADIUS = 1.2;
    const AA_INNER_RING_SCALE = 0.55;
    const RELIEF_SLOPE = 0.28;
    const DIVE_LOG_DISTANCE_FLOOR = 0.000001;
    const FRACTAL_BRANCH_BLOCK_SIZE = 32;
    const FRACTAL_STEERING_LOOKAHEAD = 4;
    const FRACTAL_TAIL_ERROR_SCALE = 16;
    const FRACTAL_TAIL_SEED = [2, 0];
    const GLIDE_CORRECTION_ITERATIONS = 2;
    const GLIDE_START_SEARCH_STEPS = 28;
    const GLIDE_OUTER_RADIUS = 4;
    const GLIDE_ESCAPE_RADIUS_SQUARED = 65536;
    const STEERING_PROBE_OFFSETS = [
      [0, 0], [-0.32, 0], [0.32, 0], [0, -0.24], [0, 0.24],
      [-0.25, -0.18], [0.25, -0.18], [-0.25, 0.18], [0.25, 0.18]
    ];
    const LIGHT_ELEVATION = 55 * Math.PI / 180;

    const MODEL_NAMES = {
      baseline: "Baseline",
      distance: "Distance rim",
      relief: "Relief lighting",
      fog: "Depth fog",
      contours: "Fat contours",
      "fog-relief": "Fog + relief",
      "rim-fog": "Rim + fog"
    };

    const presets = DATA.settings.presets.concat(DATA.candidatePresets).map(
      function (entry) {
        return Object.assign(
          { morphScale: 1, boundaryOnly: false },
          entry
        );
      }
    );
    const presetViewCenters = presets.map(function (entry) {
      return entry.viewCenter.slice();
    });

    const defaults = {
      model: "fog-relief",
      presetIndex: 7,
      journeyMode: "glide",
      glideSpeed: DATA.glide.travelSpeed,
      glideWindow: DATA.glide.window,
      glideTileSoftening: true,
      branchChoice: "steered",
      diveAbsoluteColoring: false,
      tileBandLimit: true,
      diveHueStep: DATA.dive.hueStep,
      tileSoftening: DATA.dive.tileSoftening,
      diveRate: DATA.dive.rate,
      diveBoostMultiplier: DATA.dive.boostMultiplier,
      diveWrapThreshold: DATA.dive.wrapThreshold,
      colorPath: "palette",
      paletteIndex: Math.max(0, DATA.palettes.findIndex(function (palette) {
        return palette.name === "bhw1_24_gp";
      })),
      rimThickness: 1.5,
      rimStrength: 0.65,
      rimPolarity: 1,
      lightAzimuth: DATA.settings.reliefLightAzimuth,
      reliefDepth: DATA.settings.reliefShadingDepth,
      fogDensity: DATA.settings.fogDensity,
      fogFloor: DATA.settings.fogBrightnessFloor,
      fogInvert: !DATA.settings.fogBrightensTowardSet,
      bandFrequency: 0.18,
      bandContrast: 0.55,
      bandDuty: 0.5
    };
    const settings = Object.assign({}, defaults);

    const canvas = document.getElementById("wall");
    const context = canvas.getContext("2d", { alpha: false });
    const liveBadge = document.getElementById("liveBadge");
    const modelState = document.getElementById("modelState");
    const presetState = document.getElementById("presetState");
    const windowState = document.getElementById("windowState");
    const hueState = document.getElementById("hueState");
    const journeyState = document.getElementById("journeyState");
    const presetSelect = document.getElementById("presetSelect");
    const glideControls = document.getElementById("glideControls");
    const glideSpeed = document.getElementById("glideSpeed");
    const glideSpeedValue = document.getElementById("glideSpeedValue");
    const glideWindow = document.getElementById("glideWindow");
    const glideWindowValue = document.getElementById("glideWindowValue");
    const journeyMode = document.getElementById("journeyMode");
    const diveControls = document.getElementById("diveControls");
    const fractalControls = document.getElementById("fractalControls");
    const branchChoice = document.getElementById("branchChoice");
    const diveAbsoluteColoring = document.getElementById("diveAbsoluteColoring");
    const diveHueStep = document.getElementById("diveHueStep");
    const diveHueStepValue = document.getElementById("diveHueStepValue");
    const tileSoftening = document.getElementById("tileSoftening");
    const tileSofteningValue = document.getElementById("tileSofteningValue");
    const diveRate = document.getElementById("diveRate");
    const diveRateValue = document.getElementById("diveRateValue");
    const diveBoostMultiplier = document.getElementById("diveBoostMultiplier");
    const diveBoostMultiplierValue = document.getElementById("diveBoostMultiplierValue");
    const diveWrapThreshold = document.getElementById("diveWrapThreshold");
    const diveWrapThresholdValue = document.getElementById("diveWrapThresholdValue");
    const boostButton = document.getElementById("boostButton");
    const breathingCenterControls = document.getElementById("breathingCenterControls");
    const viewCenterX = document.getElementById("viewCenterX");
    const viewCenterY = document.getElementById("viewCenterY");
    const viewCenterXValue = document.getElementById("viewCenterXValue");
    const viewCenterYValue = document.getElementById("viewCenterYValue");
    const presetPairText = document.getElementById("presetPairText");
    const colorSelect = document.getElementById("colorSelect");
    const paletteSelect = document.getElementById("paletteSelect");
    const paletteStrip = document.getElementById("paletteStrip");
    const pauseButton = document.getElementById("pauseButton");
    const journeyButton = document.getElementById("journeyButton");
    const baselineNote = document.getElementById("baselineNote");

    const controls = {
      rimThickness: document.getElementById("rimThickness"),
      rimStrength: document.getElementById("rimStrength"),
      rimPolarity: document.getElementById("rimPolarity"),
      lightAzimuth: document.getElementById("lightAzimuth"),
      reliefDepth: document.getElementById("reliefDepth"),
      fogDensity: document.getElementById("fogDensity"),
      fogFloor: document.getElementById("fogFloor"),
      fogInvert: document.getElementById("fogInvert"),
      bandFrequency: document.getElementById("bandFrequency"),
      bandContrast: document.getElementById("bandContrast"),
      bandDuty: document.getElementById("bandDuty")
    };

    const valueLabels = {
      rimThickness: document.getElementById("rimThicknessValue"),
      rimStrength: document.getElementById("rimStrengthValue"),
      rimPolarity: document.getElementById("rimPolarityValue"),
      lightAzimuth: document.getElementById("lightAzimuthValue"),
      reliefDepth: document.getElementById("reliefDepthValue"),
      fogDensity: document.getElementById("fogDensityValue"),
      fogFloor: document.getElementById("fogFloorValue"),
      bandFrequency: document.getElementById("bandFrequencyValue"),
      bandContrast: document.getElementById("bandContrastValue"),
      bandDuty: document.getElementById("bandDutyValue")
    };

    function wrap(value) {
      return value - Math.floor(value);
    }

    function isDiveMode() {
      return settings.journeyMode === "fractal" ||
        settings.journeyMode === "corridor";
    }

    function lerpNumber(start, end, amount) {
      return start + ((end - start) * amount);
    }

    function hsvToRgb(hue, saturation, value) {
      hue = wrap(hue);
      const sector = hue * 6;
      const index = Math.floor(sector);
      const fraction = sector - index;
      const p = value * (1 - saturation);
      const q = value * (1 - (saturation * fraction));
      const t = value * (1 - (saturation * (1 - fraction)));
      return [
        [value, t, p], [q, value, p], [p, value, t],
        [p, q, value], [t, p, value], [value, p, q]
      ][index % 6];
    }

    function readCyclic(position, palette) {
      position = wrap(position);
      if (position <= 0 || palette.length === 1) return palette[0];
      const scaled = position * palette.length;
      const first = Math.floor(scaled);
      const second = (first + 1) % palette.length;
      const fraction = scaled % 1;
      return [
        lerpNumber(palette[first][0], palette[second][0], fraction),
        lerpNumber(palette[first][1], palette[second][1], fraction),
        lerpNumber(palette[first][2], palette[second][2], fraction)
      ];
    }

    function buildAaOffsets() {
      const offsets = [];
      for (let sample = 0; sample < AA_SAMPLES; sample++) {
        const angle = ((sample + 0.5) / AA_SAMPLES) * 2 * Math.PI;
        const radius = AA_RADIUS * (sample % 2 === 0 ? 1 : AA_INNER_RING_SCALE);
        offsets.push([Math.cos(angle) * radius, Math.sin(angle) * radius]);
      }
      return offsets;
    }

    const aaOffsets = buildAaOffsets();
    const aaDenominatorX = aaOffsets.reduce(function (sum, offset) {
      return sum + (offset[0] * offset[0]);
    }, 0);
    const aaDenominatorY = aaOffsets.reduce(function (sum, offset) {
      return sum + (offset[1] * offset[1]);
    }, 0);

    function complexSquareRoot(value) {
      const magnitude = Math.hypot(value[0], value[1]);
      return [
        Math.sqrt((magnitude + value[0]) * 0.5),
        (value[1] >= 0 ? 1 : -1) * Math.sqrt((magnitude - value[0]) * 0.5)
      ];
    }

    function distanceSquared(a, b) {
      const x = a[0] - b[0];
      const y = a[1] - b[1];
      return (x * x) + (y * y);
    }

    function sampleEscape(a, b, iterationLimit, result) {
      let derivativeX = 1;
      let derivativeY = 0;
      let squareA = a * a;
      let squareB = b * b;
      let iteration = 0;
      while (iteration < iterationLimit) {
        if (squareA + squareB > 4) break;

        const nextDerivativeX = 2 * ((a * derivativeX) - (b * derivativeY));
        const nextDerivativeY = 2 * ((a * derivativeY) + (b * derivativeX));
        const nextA = squareA - squareB + journey.morphedConstant[0];
        const nextB = (2 * a * b) + journey.morphedConstant[1];
        derivativeX = nextDerivativeX;
        derivativeY = nextDerivativeY;
        a = nextA;
        b = nextB;
        squareA = a * a;
        squareB = b * b;
        iteration++;
      }

      if (iteration === iterationLimit) {
        result[0] = iterationLimit;
        result[1] = 0;
        return result;
      }
      const logZn = Math.log(squareA + squareB) * 0.5;
      const nu = Math.log(logZn / LN2) / LN2;
      const smooth = Math.max(0, iteration + 1 - nu);
      const magnitude = Math.sqrt(squareA + squareB);
      const derivativeMagnitude = Math.hypot(derivativeX, derivativeY);
      const distance = derivativeMagnitude > 0
        ? magnitude * Math.log(magnitude) / derivativeMagnitude
        : Number.POSITIVE_INFINITY;
      result[0] = smooth;
      result[1] = distance;
      return result;
    }

    const edgeLockChain = Array.from(
      { length: EDGE_LOCK_DEPTH + 1 },
      function () { return [0, 0]; }
    );

    const journey = {
      breathingZoomSpeed: 0,
      zoomBreathPhase: 0,
      constantMorphPhase: 0,
      hueScroll: 0,
      diveHueScroll: 0,
      morphedConstant: [0, 0],
      viewCenter: [0, 0],
      viewRoll: 0,
      lambdaMagnitude: 1,
      lambdaArgument: 0,
      escapeDepthOffset: 0,
      rebaseCount: 0,
      boostHeld: false,
      branchSeed: 0,
      branchRandomState: 0,
      branchSymbols: [],
      branchPoints: [],
      tailErrorBound: Number.POSITIVE_INFINITY,
      levelBaseRoll: 0,
      levelZoomProgress: 0,
      levelZoomSpan: 1,
      glideDirection: 1,
      glideTargetDistance: 0,
      glideDistance: 0,
      glideHeading: 0,
      glideBoostDepth: 0,
      glideLastStep: 0,
      lastJourneyMode: "",
      edgeLockPoint: [0, 0],
      edgeLockInitialized: false,
      window: DATA.settings.windowWidth[1]
    };

    function preset() {
      return presets[settings.presetIndex];
    }

    function activeViewCenter() {
      return presetViewCenters[settings.presetIndex];
    }

    function updateCorridorFixedPoint() {
      const discriminantRoot = complexSquareRoot([
        1 - (4 * journey.morphedConstant[0]),
        -4 * journey.morphedConstant[1]
      ]);
      const first = [
        (1 + discriminantRoot[0]) * 0.5,
        discriminantRoot[1] * 0.5
      ];
      const second = [
        (1 - discriminantRoot[0]) * 0.5,
        -discriminantRoot[1] * 0.5
      ];
      const beta = (2 * Math.hypot(first[0], first[1])) > 1 ? first : second;
      journey.viewCenter = beta;
      journey.lambdaMagnitude = 2 * Math.hypot(beta[0], beta[1]);
      journey.lambdaArgument = Math.atan2(beta[1], beta[0]);
    }

    function applyJulia(value) {
      return [
        (value[0] * value[0]) - (value[1] * value[1]) +
          journey.morphedConstant[0],
        (2 * value[0] * value[1]) + journey.morphedConstant[1]
      ];
    }

    function unwrapAngle(reference, angle) {
      let delta = angle - reference;
      while (delta > Math.PI) delta -= 2 * Math.PI;
      while (delta < -Math.PI) delta += 2 * Math.PI;
      return reference + delta;
    }

    function setFractalMultiplier(resetAngle) {
      const principalArgument = Math.atan2(
        journey.viewCenter[1],
        journey.viewCenter[0]
      );
      journey.lambdaMagnitude = 2 * Math.hypot(
        journey.viewCenter[0],
        journey.viewCenter[1]
      );
      journey.lambdaArgument = resetAngle
        ? principalArgument
        : unwrapAngle(journey.lambdaArgument, principalArgument);
    }

    function branchRandomUnit() {
      let value = journey.branchRandomState;
      value ^= value << 13;
      value ^= value >>> 17;
      value ^= value << 5;
      journey.branchRandomState = value >>> 0;
      return journey.branchRandomState / 0x100000000;
    }

    function randomBranchSymbol() {
      return branchRandomUnit() < 0.5 ? -1 : 1;
    }

    function computeFractalChain(symbols, previousPoints) {
      const points = Array.from(
        { length: symbols.length },
        function () { return [0, 0]; }
      );
      let value = FRACTAL_TAIL_SEED;
      for (let index = symbols.length - 1; index >= 0; index--) {
        const positive = complexSquareRoot([
          value[0] - journey.morphedConstant[0],
          value[1] - journey.morphedConstant[1]
        ]);
        const negative = [-positive[0], -positive[1]];
        const previous = previousPoints[index];
        const point = previous
          ? (distanceSquared(positive, previous) <=
              distanceSquared(negative, previous) ? positive : negative)
          : (symbols[index] > 0 ? positive : negative);
        points[index] = point;
        value = point;
      }
      return points;
    }

    function fractalTailErrorBound(points) {
      let inverseExpansion = 1;
      for (let index = 0; index < points.length; index++) {
        inverseExpansion /= 2 * Math.hypot(points[index][0], points[index][1]);
      }
      return FRACTAL_TAIL_ERROR_SCALE * inverseExpansion;
    }

    function fractalTargetTolerance(windowWidth) {
      return windowWidth / DATA.boundsWidth * DATA.tilePitch *
        DATA.dive.targetEpsilonTiles;
    }

    function probeBranchStructure(target, probeWindow) {
      const tileFootprint = probeWindow / DATA.boundsWidth * DATA.tilePitch;
      let score = 0;
      let minimum = 1;
      let maximum = 0;
      for (let index = 0; index < STEERING_PROBE_OFFSETS.length; index++) {
        const offset = STEERING_PROBE_OFFSETS[index];
        const sample = sampleEscape(
          target[0] + (offset[0] * probeWindow),
          target[1] + (offset[1] * probeWindow),
          DIVE_ITERATIONS,
          escapeSample
        );
        const coverage = tileExteriorCoverage(sample[1], tileFootprint);
        score += 4 * coverage * (1 - coverage);
        minimum = Math.min(minimum, coverage);
        maximum = Math.max(maximum, coverage);
      }
      return 0.25 + score + (maximum - minimum);
    }

    function scoreBranchWord(symbols, baseIndex) {
      const points = computeFractalChain(symbols, journey.branchPoints);
      let score = 0;
      for (let index = baseIndex; index < symbols.length; index++) {
        const probeWindow = settings.diveWrapThreshold *
          2 * Math.hypot(points[index][0], points[index][1]);
        score += probeBranchStructure(points[index], probeWindow) /
          (1 + ((index - baseIndex) * 0.25));
      }
      return { symbols: symbols, points: points, score: score };
    }

    function steeredBranchWord(symbols) {
      const positiveSymbols = symbols.concat(1);
      const negativeSymbols = symbols.concat(-1);
      for (let index = 1; index < FRACTAL_STEERING_LOOKAHEAD; index++) {
        positiveSymbols.push(randomBranchSymbol());
        negativeSymbols.push(randomBranchSymbol());
      }
      const positive = scoreBranchWord(positiveSymbols, symbols.length);
      const negative = scoreBranchWord(negativeSymbols, symbols.length);
      const positiveProbability = positive.score /
        (positive.score + negative.score);
      return branchRandomUnit() < positiveProbability
        ? positive.symbols
        : negative.symbols;
    }

    function appendFractalBranchBlock() {
      while (true) {
        const symbols = journey.branchSymbols.slice();
        const targetLength = symbols.length + FRACTAL_BRANCH_BLOCK_SIZE;
        while (symbols.length < targetLength) {
          if (settings.branchChoice === "steered") {
            const extended = steeredBranchWord(symbols);
            symbols.push.apply(
              symbols,
              extended.slice(symbols.length, targetLength)
            );
          } else {
            symbols.push(randomBranchSymbol());
          }
        }
        const points = computeFractalChain(symbols, journey.branchPoints);
        const safe = points.every(function (point) {
          return 2 * Math.hypot(point[0], point[1]) >=
            DATA.dive.branchMultiplierFloor;
        });
        if (safe) {
          journey.branchSymbols = symbols;
          journey.branchPoints = points;
          return;
        }
      }
    }

    function ensureFractalTail(windowWidth) {
      journey.tailErrorBound = fractalTailErrorBound(journey.branchPoints);
      while (
        journey.branchSymbols.length === 0 ||
        journey.tailErrorBound > fractalTargetTolerance(windowWidth)
      ) {
        appendFractalBranchBlock();
        journey.tailErrorBound = fractalTailErrorBound(journey.branchPoints);
      }
    }

    function updateFractalTarget(resetAngle) {
      if (journey.branchSymbols.length > 0) {
        journey.branchPoints = computeFractalChain(
          journey.branchSymbols,
          journey.branchPoints
        );
      }
      ensureFractalTail(journey.window);
      journey.viewCenter = journey.branchPoints[0].slice();
      setFractalMultiplier(resetAngle);
    }

    function initializeFractalBranches(seed) {
      journey.branchSeed = seed >>> 0;
      journey.branchRandomState = journey.branchSeed;
      journey.branchSymbols = [];
      journey.branchPoints = [];
      journey.tailErrorBound = Number.POSITIVE_INFINITY;
      updateFractalTarget(true);
    }

    function initializeEdgeLock(fixedPoint) {
      let bestAddress = 0;
      let bestDistance = Number.POSITIVE_INFINITY;
      const addressCount = 1 << EDGE_LOCK_DEPTH;
      for (let address = 0; address < addressCount; address++) {
        let candidate = fixedPoint.slice();
        for (let depth = 0; depth < EDGE_LOCK_DEPTH; depth++) {
          candidate = complexSquareRoot([
            candidate[0] - journey.morphedConstant[0],
            candidate[1] - journey.morphedConstant[1]
          ]);
          if ((address & (1 << depth)) !== 0) {
            candidate = [-candidate[0], -candidate[1]];
          }
        }
        const candidateDistance = distanceSquared(candidate, activeViewCenter());
        if (candidateDistance < bestDistance) {
          bestAddress = address;
          bestDistance = candidateDistance;
        }
      }

      edgeLockChain[0] = fixedPoint.slice();
      for (let depth = 1; depth <= EDGE_LOCK_DEPTH; depth++) {
        let point = complexSquareRoot([
          edgeLockChain[depth - 1][0] - journey.morphedConstant[0],
          edgeLockChain[depth - 1][1] - journey.morphedConstant[1]
        ]);
        if ((bestAddress & (1 << (depth - 1))) !== 0) {
          point = [-point[0], -point[1]];
        }
        edgeLockChain[depth] = point;
      }
      journey.edgeLockPoint = edgeLockChain[EDGE_LOCK_DEPTH].slice();
      journey.edgeLockInitialized = true;
    }

    function updateEdgeLock(windowWidth) {
      const discriminantRoot = complexSquareRoot([
        1 - (4 * journey.morphedConstant[0]),
        -4 * journey.morphedConstant[1]
      ]);
      const first = [
        (1 + discriminantRoot[0]) * 0.5,
        discriminantRoot[1] * 0.5
      ];
      const second = [
        (1 - discriminantRoot[0]) * 0.5,
        -discriminantRoot[1] * 0.5
      ];

      if (!journey.edgeLockInitialized) {
        const stronger = distanceSquared(first, [0, 0]) >= distanceSquared(second, [0, 0])
          ? first
          : second;
        initializeEdgeLock(stronger);
      } else {
        const firstRepels = distanceSquared(first, [0, 0]) >
          REPELLING_FIXED_POINT_MAGNITUDE_SQUARED;
        const secondRepels = distanceSquared(second, [0, 0]) >
          REPELLING_FIXED_POINT_MAGNITUDE_SQUARED;
        if (firstRepels !== secondRepels) {
          edgeLockChain[0] = (firstRepels ? first : second).slice();
        } else {
          edgeLockChain[0] =
            distanceSquared(first, edgeLockChain[0]) <=
            distanceSquared(second, edgeLockChain[0])
              ? first.slice()
              : second.slice();
        }

        for (let depth = 1; depth <= EDGE_LOCK_DEPTH; depth++) {
          const positiveRoot = complexSquareRoot([
            edgeLockChain[depth - 1][0] - journey.morphedConstant[0],
            edgeLockChain[depth - 1][1] - journey.morphedConstant[1]
          ]);
          const negativeRoot = [-positiveRoot[0], -positiveRoot[1]];
          edgeLockChain[depth] =
            distanceSquared(positiveRoot, edgeLockChain[depth]) <=
            distanceSquared(negativeRoot, edgeLockChain[depth])
              ? positiveRoot
              : negativeRoot;
        }
        journey.edgeLockPoint = edgeLockChain[EDGE_LOCK_DEPTH].slice();
      }

      const framingX = activeViewCenter()[0] - journey.edgeLockPoint[0];
      const framingY = activeViewCenter()[1] - journey.edgeLockPoint[1];
      const framingMagnitude = Math.hypot(framingX, framingY);
      const directionX = framingMagnitude > 0 ? framingX / framingMagnitude : 0;
      const directionY = framingMagnitude > 0 ? framingY / framingMagnitude : 0;
      journey.viewCenter = [
        journey.edgeLockPoint[0] +
          (DATA.settings.edgeLockFraming * windowWidth * directionX),
        journey.edgeLockPoint[1] +
          (DATA.settings.edgeLockFraming * windowWidth * directionY)
      ];
    }

    function resetEdgeLock() {
      journey.edgeLockPoint = activeViewCenter().slice();
      journey.viewCenter = activeViewCenter().slice();
      journey.edgeLockInitialized = false;
    }

    function glideDistanceAt(point) {
      let a = point[0];
      let b = point[1];
      let derivativeX = 1;
      let derivativeY = 0;
      let squareA = a * a;
      let squareB = b * b;
      let iteration = 0;
      while (
        iteration < DIVE_ITERATIONS &&
        squareA + squareB <= GLIDE_ESCAPE_RADIUS_SQUARED
      ) {
        const nextDerivativeX = 2 *
          ((a * derivativeX) - (b * derivativeY));
        const nextDerivativeY = 2 *
          ((a * derivativeY) + (b * derivativeX));
        const nextA = squareA - squareB + journey.morphedConstant[0];
        const nextB = (2 * a * b) + journey.morphedConstant[1];
        derivativeX = nextDerivativeX;
        derivativeY = nextDerivativeY;
        a = nextA;
        b = nextB;
        squareA = a * a;
        squareB = b * b;
        iteration++;
      }
      if (iteration === DIVE_ITERATIONS) return 0;
      const magnitude = Math.sqrt(squareA + squareB);
      return magnitude * Math.log(magnitude) /
        Math.hypot(derivativeX, derivativeY);
    }

    function glideFieldAt(point, gradientStep) {
      const left = glideDistanceAt([point[0] - gradientStep, point[1]]);
      const right = glideDistanceAt([point[0] + gradientStep, point[1]]);
      const down = glideDistanceAt([point[0], point[1] - gradientStep]);
      const up = glideDistanceAt([point[0], point[1] + gradientStep]);
      return {
        distance: glideDistanceAt(point),
        gradient: [
          (right - left) / (2 * gradientStep),
          (up - down) / (2 * gradientStep)
        ]
      };
    }

    function correctGlideAltitude(point, targetDistance, gradientStep) {
      const corrected = point.slice();
      for (let index = 0; index < GLIDE_CORRECTION_ITERATIONS; index++) {
        const field = glideFieldAt(corrected, gradientStep);
        const gradientSquared =
          (field.gradient[0] * field.gradient[0]) +
          (field.gradient[1] * field.gradient[1]);
        const correction = (field.distance - targetDistance) /
          gradientSquared;
        corrected[0] -= field.gradient[0] * correction;
        corrected[1] -= field.gradient[1] * correction;
      }
      return {
        point: corrected,
        field: glideFieldAt(corrected, gradientStep)
      };
    }

    function findGlideStart(angle, targetDistance) {
      const directionX = Math.cos(angle);
      const directionY = Math.sin(angle);
      let innerRadius = 0;
      let outerRadius = GLIDE_OUTER_RADIUS;
      for (let index = 0; index < GLIDE_START_SEARCH_STEPS; index++) {
        const radius = (innerRadius + outerRadius) * 0.5;
        const distance = glideDistanceAt([
          directionX * radius,
          directionY * radius
        ]);
        if (distance < targetDistance) innerRadius = radius;
        else outerRadius = radius;
      }
      return [directionX * outerRadius, directionY * outerRadius];
    }

    function initializeGlide(startAngle, direction) {
      journey.glideDirection = direction;
      journey.glideBoostDepth = 0;
      journey.window = settings.glideWindow;
      applyConstantMorph();
      journey.glideTargetDistance = DATA.glide.distanceRatio * journey.window;
      const gradientStep = journey.window * DATA.glide.gradientStepRatio;
      const start = findGlideStart(
        startAngle,
        journey.glideTargetDistance
      );
      const corrected = correctGlideAltitude(
        start,
        journey.glideTargetDistance,
        gradientStep
      );
      journey.viewCenter = corrected.point;
      journey.glideDistance = corrected.field.distance;
      const tangentX = -journey.glideDirection * corrected.field.gradient[1];
      const tangentY = journey.glideDirection * corrected.field.gradient[0];
      journey.glideHeading = Math.atan2(tangentY, tangentX);
      journey.viewRoll = DATA.glide.rollRange *
        Math.sin(journey.glideHeading);
      journey.glideLastStep = 0;
    }

    function updateGlide(deltaSeconds) {
      const boostTarget = journey.boostHeld ? DATA.glide.boostDepth : 0;
      const boostRate = journey.boostHeld
        ? DATA.glide.boostInRate
        : DATA.glide.boostOutRate;
      const boostAmount = 1 - Math.exp(-boostRate * deltaSeconds);
      journey.glideBoostDepth = lerpNumber(
        journey.glideBoostDepth,
        boostTarget,
        boostAmount
      );
      journey.window = settings.glideWindow *
        Math.exp(-journey.glideBoostDepth);
      applyConstantMorph();

      journey.glideTargetDistance = DATA.glide.distanceRatio * journey.window;
      const gradientStep = journey.window * DATA.glide.gradientStepRatio;
      const current = correctGlideAltitude(
        journey.viewCenter,
        journey.glideTargetDistance,
        gradientStep
      );
      const currentGradientLength = Math.hypot(
        current.field.gradient[0],
        current.field.gradient[1]
      );
      const tangentX = -journey.glideDirection *
        current.field.gradient[1] / currentGradientLength;
      const tangentY = journey.glideDirection *
        current.field.gradient[0] / currentGradientLength;
      const speedMultiplier = journey.boostHeld
        ? DATA.glide.boostMultiplier
        : 1;
      const travelDistance = settings.glideSpeed * journey.window *
        speedMultiplier * deltaSeconds;
      const midpoint = [
        current.point[0] + (tangentX * travelDistance * 0.5),
        current.point[1] + (tangentY * travelDistance * 0.5)
      ];
      const midpointField = glideFieldAt(midpoint, gradientStep);
      const midpointGradientLength = Math.hypot(
        midpointField.gradient[0],
        midpointField.gradient[1]
      );
      const midpointTangentX = -journey.glideDirection *
        midpointField.gradient[1] / midpointGradientLength;
      const midpointTangentY = journey.glideDirection *
        midpointField.gradient[0] / midpointGradientLength;
      const proposed = [
        current.point[0] + (midpointTangentX * travelDistance),
        current.point[1] + (midpointTangentY * travelDistance)
      ];
      const corrected = correctGlideAltitude(
        proposed,
        journey.glideTargetDistance,
        gradientStep
      );
      journey.glideLastStep = Math.hypot(
        corrected.point[0] - journey.viewCenter[0],
        corrected.point[1] - journey.viewCenter[1]
      );
      journey.viewCenter = corrected.point;
      journey.glideDistance = corrected.field.distance;
      journey.glideHeading = Math.atan2(
        midpointTangentY,
        midpointTangentX
      );
      const desiredRoll = DATA.glide.rollRange *
        Math.sin(journey.glideHeading);
      const rollAmount = 1 - Math.exp(
        -DATA.glide.rollDamping * deltaSeconds
      );
      journey.viewRoll = lerpNumber(
        journey.viewRoll,
        desiredRoll,
        rollAmount
      );
    }

    function applyConstantMorph() {
      const orbitAngle = journey.constantMorphPhase * 2 * Math.PI;
      const orbitWindow = isDiveMode()
        ? settings.diveWrapThreshold
        : journey.window;
      const orbitRadius = DATA.settings.constantMorphRadius * orbitWindow *
        preset().morphScale;
      journey.morphedConstant = [
        preset().constant[0] + (Math.cos(orbitAngle) * orbitRadius),
        preset().constant[1] + (Math.sin(orbitAngle) * orbitRadius)
      ];
    }

    function advanceDivePhase() {
      journey.diveHueScroll = wrap(
        journey.diveHueScroll + settings.diveHueStep
      );
      journey.escapeDepthOffset += 1;
      journey.rebaseCount += 1;
    }

    function startFractalLevel() {
      journey.levelBaseRoll = journey.viewRoll;
      journey.levelZoomProgress = 0;
      journey.levelZoomSpan = Math.log(
        journey.window / settings.diveWrapThreshold
      );
    }

    function applyFractalRebase() {
      const multiplierMagnitude = journey.lambdaMagnitude;
      const multiplierArgument = journey.lambdaArgument;
      const mappedCenter = applyJulia(journey.viewCenter);
      journey.viewRoll = journey.levelBaseRoll + multiplierArgument;
      journey.window *= multiplierMagnitude;
      journey.viewRoll -= multiplierArgument;
      advanceDivePhase();

      journey.branchSymbols.shift();
      journey.branchPoints.shift();
      journey.branchPoints[0] = mappedCenter;
      journey.viewCenter = mappedCenter;
      setFractalMultiplier(true);
      journey.tailErrorBound = fractalTailErrorBound(journey.branchPoints);
      startFractalLevel();
    }

    function updateFractalDive(zoomAmount) {
      journey.viewRoll = journey.levelBaseRoll +
        (journey.lambdaArgument *
          (journey.levelZoomProgress / journey.levelZoomSpan));
      while (zoomAmount > 0) {
        const zoomToRebase = Math.log(
          journey.window / settings.diveWrapThreshold
        );
        if (zoomAmount < zoomToRebase) {
          journey.window *= Math.exp(-zoomAmount);
          journey.levelZoomProgress += zoomAmount;
          journey.viewRoll = journey.levelBaseRoll +
            (journey.lambdaArgument *
              (journey.levelZoomProgress / journey.levelZoomSpan));
          return;
        }

        const previousLength = journey.branchSymbols.length;
        ensureFractalTail(settings.diveWrapThreshold);
        if (journey.branchSymbols.length !== previousLength) {
          journey.viewCenter = journey.branchPoints[0].slice();
          setFractalMultiplier(false);
        }
        journey.window = settings.diveWrapThreshold;
        journey.levelZoomProgress += zoomToRebase;
        journey.viewRoll = journey.levelBaseRoll + journey.lambdaArgument;
        zoomAmount -= zoomToRebase;
        applyFractalRebase();
      }
    }

    function updateCorridorDive(zoomAmount) {
      journey.window *= Math.exp(-zoomAmount);
      journey.viewRoll += zoomAmount * journey.lambdaArgument /
        Math.log(journey.lambdaMagnitude);
      while (journey.window < settings.diveWrapThreshold) {
        journey.window *= journey.lambdaMagnitude;
        journey.viewRoll -= journey.lambdaArgument;
        advanceDivePhase();
      }
    }

    function newJourney() {
      const continuingGlide = settings.journeyMode === "glide" &&
        journey.lastJourneyMode === "glide";
      const speed = DATA.settings.breathingZoomSpeed;
      journey.breathingZoomSpeed = lerpNumber(speed[0], speed[1], Math.random());
      journey.zoomBreathPhase = 0;
      if (!continuingGlide) {
        journey.constantMorphPhase = Math.random();
        journey.hueScroll = 0;
      }
      journey.diveHueScroll = 0;
      journey.morphedConstant = preset().constant.slice();
      journey.window = settings.journeyMode === "glide"
        ? settings.glideWindow
        : DATA.settings.windowWidth[1];
      journey.viewRoll = 0;
      journey.escapeDepthOffset = 0;
      journey.rebaseCount = 0;
      applyConstantMorph();
      if (settings.journeyMode === "glide") {
        const glideDirection = continuingGlide
          ? -journey.glideDirection
          : (Math.random() < 0.5 ? -1 : 1);
        initializeGlide(
          Math.random() * 2 * Math.PI,
          glideDirection
        );
      } else if (settings.journeyMode === "fractal") {
        const seed = (Math.floor(Math.random() * 0xffffffff) + 1) >>> 0;
        initializeFractalBranches(seed);
        startFractalLevel();
      } else if (settings.journeyMode === "corridor") {
        updateCorridorFixedPoint();
      } else {
        resetEdgeLock();
        updateEdgeLock(journey.window);
      }
      journey.lastJourneyMode = settings.journeyMode;
    }

    function updateJourney(deltaSeconds) {
      const hueRate = DATA.settings.hueBaseRate +
        (DATA.settings.hueCycleDrive * DATA.settings.hueBeatRate);
      journey.hueScroll = wrap(journey.hueScroll + (hueRate * deltaSeconds));

      journey.constantMorphPhase = wrap(
        journey.constantMorphPhase +
        (DATA.settings.constantMorphRate * deltaSeconds)
      );

      if (settings.journeyMode === "glide") {
        journey.escapeDepthOffset = 0;
        updateGlide(deltaSeconds);
      } else if (settings.journeyMode === "fractal") {
        applyConstantMorph();
        updateFractalTarget(false);
        const activeRate = settings.diveRate *
          (journey.boostHeld ? settings.diveBoostMultiplier : 1);
        updateFractalDive(activeRate * deltaSeconds);
      } else if (settings.journeyMode === "corridor") {
        applyConstantMorph();
        updateCorridorFixedPoint();
        const activeRate = settings.diveRate *
          (journey.boostHeld ? settings.diveBoostMultiplier : 1);
        updateCorridorDive(activeRate * deltaSeconds);
      } else {
        applyConstantMorph();
        const sinAmount = Math.sin(journey.zoomBreathPhase);
        const remapped = (1 - sinAmount) * 0.5;
        const windowRange = DATA.settings.windowWidth;
        const breathWindow = windowRange[1] *
          (1 + ((remapped - 1) * DATA.settings.depth));
        journey.window = Math.max(
          windowRange[0],
          Math.min(windowRange[1], breathWindow)
        );
        journey.zoomBreathPhase += journey.breathingZoomSpeed * deltaSeconds;
        journey.viewRoll = 0;
        journey.escapeDepthOffset = 0;
        updateEdgeLock(journey.window);
      }
    }

    function colorAt(position) {
      return settings.colorPath === "palette"
        ? readCyclic(
            wrap(position),
            DATA.palettes[settings.paletteIndex].conditioned
          )
        : hsvToRgb(wrap(position), 1, 1);
    }

    function absoluteEscapeColor(smoothEscape) {
      return colorAt(
        Math.sqrt(smoothEscape / ITERATIONS) + journey.hueScroll
      );
    }

    function invariantEscapeColor(smoothEscape) {
      return colorAt(
        (smoothEscape * settings.diveHueStep) +
        journey.hueScroll +
        journey.diveHueScroll
      );
    }

    function invariantLimitColor() {
      return colorAt(journey.hueScroll);
    }

    function reliefBrightness(gradientX, gradientY) {
      let normalX = -gradientX * RELIEF_SLOPE;
      let normalY = -gradientY * RELIEF_SLOPE;
      let normalZ = 1;
      const normalLength = Math.hypot(normalX, normalY, normalZ);
      normalX /= normalLength;
      normalY /= normalLength;
      normalZ /= normalLength;

      const azimuth = settings.lightAzimuth * Math.PI / 180;
      const horizontal = Math.cos(LIGHT_ELEVATION);
      const lightX = Math.cos(azimuth) * horizontal;
      const lightY = Math.sin(azimuth) * horizontal;
      const lightZ = Math.sin(LIGHT_ELEVATION);
      const lambert = Math.max(
        0,
        (normalX * lightX) + (normalY * lightY) + (normalZ * lightZ)
      );
      return (1 - settings.reliefDepth) + (settings.reliefDepth * lambert);
    }

    function rimBrightness(distanceEstimate, scale) {
      const distanceTiles = distanceEstimate / (scale * DATA.tilePitch);
      const normalized = distanceTiles / settings.rimThickness;
      const rim = Math.exp(-(normalized * normalized));
      const brightRim = 1 - (settings.rimStrength * (1 - rim));
      const darkRim = 1 - (settings.rimStrength * rim);
      const brightAmount = (settings.rimPolarity + 1) * 0.5;
      return lerpNumber(darkRim, brightRim, brightAmount);
    }

    function fogBrightness(smoothEscape) {
      const proximity = Math.sqrt(smoothEscape / ITERATIONS);
      const exponent = settings.fogDensity * (1 - proximity);
      const nearSet = Math.exp(-(exponent * exponent));
      const shaped = settings.fogInvert ? 1 - nearSet : nearSet;
      return settings.fogFloor + ((1 - settings.fogFloor) * shaped);
    }

    function distanceFogAmount(distanceRatio) {
      return 1 - Math.exp(
        -(settings.fogDensity * settings.fogDensity * distanceRatio)
      );
    }

    function distanceFogBrightness(exteriorAmount) {
      return settings.fogInvert
        ? settings.fogFloor + ((1 - settings.fogFloor) * exteriorAmount)
        : 1 - ((1 - settings.fogFloor) * exteriorAmount);
    }

    function tileExteriorCoverage(
      distanceEstimate,
      tileFootprint,
      softening = settings.tileSoftening
    ) {
      const distanceTiles = distanceEstimate /
        (tileFootprint * softening);
      return 1 - Math.exp(-(distanceTiles * distanceTiles));
    }

    function contourFrequency(gradientMagnitude) {
      const smoothSpanPerTile = gradientMagnitude * DATA.tilePitch;
      const narrowDuty = Math.min(settings.bandDuty, 1 - settings.bandDuty);
      const maximum = narrowDuty / Math.max(0.0001, smoothSpanPerTile);
      return Math.min(settings.bandFrequency, maximum);
    }

    function contourBrightness(smoothEscape, frequency) {
      const stripe = wrap(smoothEscape * frequency) < settings.bandDuty ? 1 : 0;
      return 1 - (settings.bandContrast * (1 - stripe));
    }

    function shadingBrightness(
      distanceEstimate,
      scale,
      relief,
      contour,
      fog
    ) {
      const rim = rimBrightness(distanceEstimate, scale);
      if (settings.model === "distance") return rim;
      if (settings.model === "relief") return relief;
      if (settings.model === "fog") return fog;
      if (settings.model === "contours") return contour;
      if (settings.model === "fog-relief") return fog * relief;
      if (settings.model === "rim-fog") return rim * fog;
      return 1;
    }

    const output = new Float32Array(EXPECTED_TILE_BUFFER_LENGTH());
    const smoothSamples = new Float64Array(AA_SAMPLES);
    const distanceSamples = new Float64Array(AA_SAMPLES);
    const logDistanceSamples = new Float64Array(AA_SAMPLES);
    const escapeSample = new Float64Array(2);

    function EXPECTED_TILE_BUFFER_LENGTH() {
      return DATA.tiles.length * 3;
    }

    function renderFrame() {
      const scale = journey.window / DATA.boundsWidth;
      const tileFootprint = scale * DATA.tilePitch;
      const rollCosine = Math.cos(journey.viewRoll);
      const rollSine = Math.sin(journey.viewRoll);
      const invariantColoring = isDiveMode() &&
        !settings.diveAbsoluteColoring;
      const glideSoftening = settings.journeyMode === "glide" &&
        settings.glideTileSoftening;
      const iterationLimit = invariantColoring ? DIVE_ITERATIONS : ITERATIONS;
      const limitColor = invariantColoring ? invariantLimitColor() : null;
      for (let tileIndex = 0; tileIndex < DATA.tiles.length; tileIndex++) {
        const center = DATA.tiles[tileIndex].center;
        let smoothMean = 0;
        let logDistanceMean = 0;
        for (let sampleIndex = 0; sampleIndex < AA_SAMPLES; sampleIndex++) {
          const offset = aaOffsets[sampleIndex];
          const localX = center[0] + offset[0];
          const localY = center[1] + offset[1];
          const sampleX = journey.viewCenter[0] +
            (((localX * rollCosine) + (localY * rollSine)) * scale);
          const sampleY = journey.viewCenter[1] +
            (((-localX * rollSine) + (localY * rollCosine)) * scale);
          const sample = sampleEscape(
            sampleX,
            sampleY,
            iterationLimit,
            escapeSample
          );
          smoothSamples[sampleIndex] = sample[0];
          distanceSamples[sampleIndex] = sample[1];
          smoothMean += sample[0];
          if (invariantColoring) {
            const logDistance = Math.log(
              (sample[1] / journey.window) + DIVE_LOG_DISTANCE_FLOOR
            );
            logDistanceSamples[sampleIndex] = logDistance;
            logDistanceMean += logDistance;
          }
        }
        smoothMean /= AA_SAMPLES;
        logDistanceMean /= AA_SAMPLES;

        let gradientX = 0;
        let gradientY = 0;
        for (let sampleIndex = 0; sampleIndex < AA_SAMPLES; sampleIndex++) {
          const centered = smoothSamples[sampleIndex] - smoothMean;
          gradientX += aaOffsets[sampleIndex][0] * centered;
          gradientY += aaOffsets[sampleIndex][1] * centered;
        }
        gradientX /= aaDenominatorX;
        gradientY /= aaDenominatorY;
        const gradientMagnitude = Math.hypot(gradientX, gradientY);
        let reliefGradientX = gradientX;
        let reliefGradientY = gradientY;
        if (invariantColoring) {
          reliefGradientX = 0;
          reliefGradientY = 0;
          for (let sampleIndex = 0; sampleIndex < AA_SAMPLES; sampleIndex++) {
            const centered = logDistanceSamples[sampleIndex] - logDistanceMean;
            reliefGradientX += aaOffsets[sampleIndex][0] * centered;
            reliefGradientY += aaOffsets[sampleIndex][1] * centered;
          }
          reliefGradientX /= aaDenominatorX;
          reliefGradientY /= aaDenominatorY;
        }
        const relief = reliefBrightness(reliefGradientX, reliefGradientY);
        const frequency = contourFrequency(gradientMagnitude);

        let red = 0;
        let green = 0;
        let blue = 0;
        for (let sampleIndex = 0; sampleIndex < AA_SAMPLES; sampleIndex++) {
          const rawSmoothEscape = smoothSamples[sampleIndex];
          if (!invariantColoring && rawSmoothEscape >= iterationLimit) continue;
          const smoothEscape = rawSmoothEscape + journey.escapeDepthOffset;
          const distanceEstimate = distanceSamples[sampleIndex];
          const distanceRatio = distanceEstimate / journey.window;
          const filteredDistanceRatio = invariantColoring &&
            settings.tileBandLimit
            ? Math.hypot(
                distanceRatio,
                (tileFootprint / journey.window) *
                  settings.tileSoftening * 0.5
              )
            : distanceRatio;
          const fogAmount = invariantColoring
            ? distanceFogAmount(filteredDistanceRatio)
            : 1;
          const detailAmount = invariantColoring && settings.tileBandLimit
            ? fogAmount * tileExteriorCoverage(
                distanceEstimate,
                tileFootprint
              )
            : fogAmount;
          const hueColor = invariantColoring
            ? invariantEscapeColor(rawSmoothEscape)
            : absoluteEscapeColor(smoothEscape);
          const color = invariantColoring
            ? [
                lerpNumber(limitColor[0], hueColor[0], detailAmount),
                lerpNumber(limitColor[1], hueColor[1], detailAmount),
                lerpNumber(limitColor[2], hueColor[2], detailAmount)
              ]
            : hueColor;
          const contour = contourBrightness(smoothEscape, frequency);
          const fog = invariantColoring
            ? distanceFogBrightness(fogAmount)
            : fogBrightness(smoothEscape);
          const sampleRelief = invariantColoring
            ? lerpNumber(1, relief, detailAmount)
            : relief;
          const brightness = shadingBrightness(
            distanceEstimate,
            scale,
            sampleRelief,
            contour,
            fog
          );
          const coverage = glideSoftening
            ? tileExteriorCoverage(
                distanceEstimate,
                tileFootprint,
                DATA.glide.tileSoftening
              )
            : 1;
          red += color[0] * brightness * coverage;
          green += color[1] * brightness * coverage;
          blue += color[2] * brightness * coverage;
        }
        const outputOffset = tileIndex * 3;
        output[outputOffset] = red / AA_SAMPLES;
        output[outputOffset + 1] = green / AA_SAMPLES;
        output[outputOffset + 2] = blue / AA_SAMPLES;
      }
      return output;
    }

    const worldBounds = DATA.tiles.reduce(function (bounds, tile) {
      tile.polygon.forEach(function (point) {
        bounds.minX = Math.min(bounds.minX, point[0]);
        bounds.minY = Math.min(bounds.minY, point[1]);
        bounds.maxX = Math.max(bounds.maxX, point[0]);
        bounds.maxY = Math.max(bounds.maxY, point[1]);
      });
      return bounds;
    }, {
      minX: Number.POSITIVE_INFINITY,
      minY: Number.POSITIVE_INFINITY,
      maxX: Number.NEGATIVE_INFINITY,
      maxY: Number.NEGATIVE_INFINITY
    });

    let screenPaths = [];

    function resizeWall() {
      const rect = canvas.getBoundingClientRect();
      const dpr = window.devicePixelRatio || 1;
      canvas.width = Math.round(rect.width * dpr);
      canvas.height = Math.round(rect.height * dpr);
      context.setTransform(dpr, 0, 0, dpr, 0, 0);
      const padding = 10;
      const scale = Math.min(
        (rect.width - (padding * 2)) / (worldBounds.maxX - worldBounds.minX),
        (rect.height - (padding * 2)) / (worldBounds.maxY - worldBounds.minY)
      );
      const drawnWidth = (worldBounds.maxX - worldBounds.minX) * scale;
      const drawnHeight = (worldBounds.maxY - worldBounds.minY) * scale;
      const offsetX = (rect.width - drawnWidth) * 0.5;
      const offsetY = (rect.height - drawnHeight) * 0.5;
      screenPaths = DATA.tiles.map(function (tile) {
        const path = new Path2D();
        tile.polygon.forEach(function (point, index) {
          const x = offsetX + ((point[0] - worldBounds.minX) * scale);
          const y = offsetY + ((worldBounds.maxY - point[1]) * scale);
          if (index === 0) path.moveTo(x, y);
          else path.lineTo(x, y);
        });
        path.closePath();
        return path;
      });
      drawPaletteStrip();
    }

    function rgbCss(buffer, offset) {
      return "rgb(" +
        Math.round(buffer[offset] * 255) + " " +
        Math.round(buffer[offset + 1] * 255) + " " +
        Math.round(buffer[offset + 2] * 255) + ")";
    }

    function drawWall(buffer) {
      const rect = canvas.getBoundingClientRect();
      context.fillStyle = "#010205";
      context.fillRect(0, 0, rect.width, rect.height);
      for (let tileIndex = 0; tileIndex < DATA.tiles.length; tileIndex++) {
        context.fillStyle = rgbCss(buffer, tileIndex * 3);
        context.fill(screenPaths[tileIndex]);
      }
    }

    function drawPaletteStrip() {
      const rect = paletteStrip.getBoundingClientRect();
      const dpr = window.devicePixelRatio || 1;
      paletteStrip.width = Math.max(1, Math.round(rect.width * dpr));
      paletteStrip.height = Math.max(1, Math.round(rect.height * dpr));
      const strip = paletteStrip.getContext("2d");
      strip.setTransform(dpr, 0, 0, dpr, 0, 0);
      const palette = DATA.palettes[settings.paletteIndex].conditioned;
      for (let x = 0; x < Math.ceil(rect.width); x++) {
        const color = readCyclic(x / Math.max(1, rect.width), palette);
        strip.fillStyle = "rgb(" +
          Math.round(color[0] * 255) + " " +
          Math.round(color[1] * 255) + " " +
          Math.round(color[2] * 255) + ")";
        strip.fillRect(x, 0, 1, rect.height);
      }
      paletteStrip.hidden = settings.colorPath !== "palette";
      paletteSelect.hidden = settings.colorPath !== "palette";
    }

    function formatComplex(value) {
      const sign = value[1] < 0 ? "−" : "+";
      return value[0].toFixed(5) + " " + sign + " " +
        Math.abs(value[1]).toFixed(5) + "i";
    }

    function formatScalar(value) {
      return String(Number(value.toFixed(6)));
    }

    function formatVector(value) {
      return "(" + formatScalar(value[0]) + ", " +
        formatScalar(value[1]) + ")";
    }

    function presetLabel(index) {
      return index < DATA.authoredPresetCount
        ? "Authored " + (index + 1)
        : "CANDIDATE · " +
          (presets[index].boundaryOnly ? "BOUNDARY · " : "") +
          presets[index].name;
    }

    function updateParameterVisibility() {
      baselineNote.hidden = settings.model !== "baseline";
      glideControls.hidden = settings.journeyMode !== "glide";
      diveControls.hidden = !isDiveMode();
      fractalControls.hidden = settings.journeyMode !== "fractal";
      breathingCenterControls.hidden = settings.journeyMode !== "breathing";
      document.querySelectorAll(".parameter-panel").forEach(function (panel) {
        panel.hidden = !panel.dataset.models.split(" ").includes(settings.model);
      });
    }

    function updateControlLabels() {
      valueLabels.rimThickness.textContent =
        settings.rimThickness.toFixed(2) + " Tile widths";
      valueLabels.rimStrength.textContent = settings.rimStrength.toFixed(2);
      valueLabels.rimPolarity.textContent =
        settings.rimPolarity > 0 ? "bright +" + settings.rimPolarity.toFixed(1) :
        settings.rimPolarity < 0 ? "dark " + settings.rimPolarity.toFixed(1) :
        "balanced";
      valueLabels.lightAzimuth.textContent =
        Math.round(settings.lightAzimuth) + "°";
      valueLabels.reliefDepth.textContent =
        settings.reliefDepth.toFixed(2) + " · floor " +
        (1 - settings.reliefDepth).toFixed(2);
      valueLabels.fogDensity.textContent = settings.fogDensity.toFixed(1);
      valueLabels.fogFloor.textContent = settings.fogFloor.toFixed(2);
      valueLabels.bandFrequency.textContent =
        settings.bandFrequency.toFixed(2) + " cycles/count";
      valueLabels.bandContrast.textContent = settings.bandContrast.toFixed(2);
      valueLabels.bandDuty.textContent =
        Math.round(settings.bandDuty * 100) + "% bright";
    }

    let running = true;
    let smoothedFps = 60;
    let smoothedFrameMilliseconds = 1000 / 60;

    function updateState() {
      modelState.textContent = MODEL_NAMES[settings.model];
      presetState.textContent =
        presetLabel(settings.presetIndex) + " · c " +
        formatComplex(preset().constant) +
        (preset().morphScale !== 1
          ? " · morph ×" + preset().morphScale.toFixed(2)
          : "");
      windowState.textContent = (isDiveMode()
        ? journey.window.toExponential(3)
        : journey.window.toFixed(4)) + " complex units";
      const hueRate = DATA.settings.hueBaseRate +
        (DATA.settings.hueCycleDrive * DATA.settings.hueBeatRate);
      hueState.textContent = hueRate.toFixed(2) + " rev/s";
      journeyState.textContent = settings.journeyMode === "glide"
        ? "Boundary glide · absolute " + MODEL_NAMES[settings.model] +
          (settings.glideTileSoftening ? " · Tile AA" : " · point sampled") +
          " · " + settings.glideSpeed.toFixed(3) + " window/s" +
          (journey.boostHeld
            ? " × " + DATA.glide.boostMultiplier.toFixed(1) +
              " · pushing in"
            : "") +
          " · altitude " +
          (journey.glideDistance / journey.window).toFixed(4) +
          " · " + (journey.glideDirection > 0 ? "counterclockwise" : "clockwise")
        : settings.journeyMode === "fractal"
        ? "Fractal dive · " +
          (settings.diveAbsoluteColoring ? "absolute color · pops" : "invariant color") +
          " · " + (settings.branchChoice === "steered" ? "steered" : "random") +
          " · " + settings.diveRate.toFixed(2) + " e-fold/s" +
          (journey.boostHeld ? " × " + settings.diveBoostMultiplier.toFixed(2) : "") +
          " · |λₖ| " + journey.lambdaMagnitude.toFixed(4) +
          " · level " + journey.rebaseCount +
          " · K " + journey.branchSymbols.length +
          " · seed " + journey.branchSeed.toString(16).padStart(8, "0")
        : settings.journeyMode === "corridor"
        ? "Corridor · fixed point · " +
          (settings.diveAbsoluteColoring ? "absolute color · pops" : "invariant color") +
          " · " + settings.diveRate.toFixed(2) + " e-fold/s" +
          (journey.boostHeld ? " × " + settings.diveBoostMultiplier.toFixed(2) : "") +
          " · |λ| " + journey.lambdaMagnitude.toFixed(4) +
          " · wraps " + journey.rebaseCount
        : "Breath " + journey.breathingZoomSpeed.toFixed(3) +
          " rad/s · morph " + DATA.settings.constantMorphRate.toFixed(3) + " rev/s";
      liveBadge.textContent = running
        ? "Running · " + Math.round(smoothedFps) + " fps · " +
          smoothedFrameMilliseconds.toFixed(1) + " ms/frame"
        : "Paused · " + smoothedFrameMilliseconds.toFixed(1) + " ms/frame";
      liveBadge.classList.toggle("paused", !running);
      pauseButton.textContent = running ? "Pause" : "Resume";
      const center = activeViewCenter();
      viewCenterX.value = String(center[0]);
      viewCenterY.value = String(center[1]);
      viewCenterXValue.textContent = formatScalar(center[0]);
      viewCenterYValue.textContent = formatScalar(center[1]);
      glideSpeed.value = String(settings.glideSpeed);
      glideSpeedValue.textContent = settings.glideSpeed.toFixed(3) +
        " window/s";
      glideWindow.value = String(settings.glideWindow);
      glideWindowValue.textContent = settings.glideWindow.toFixed(2) +
        " complex units";
      journeyMode.value = settings.journeyMode;
      branchChoice.value = settings.branchChoice;
      diveAbsoluteColoring.checked = settings.diveAbsoluteColoring;
      diveHueStep.value = String(settings.diveHueStep);
      diveHueStepValue.textContent = settings.diveHueStep.toFixed(3) +
        " cycles/iteration";
      tileSoftening.value = String(settings.tileSoftening);
      tileSofteningValue.textContent = settings.tileSoftening.toFixed(2) +
        " Tile";
      diveRate.value = String(settings.diveRate);
      diveRateValue.textContent = settings.diveRate.toFixed(2) + " e-fold/s";
      diveBoostMultiplier.value = String(settings.diveBoostMultiplier);
      diveBoostMultiplierValue.textContent = "×" +
        settings.diveBoostMultiplier.toFixed(2);
      diveWrapThreshold.value = String(settings.diveWrapThreshold);
      diveWrapThresholdValue.textContent = settings.diveWrapThreshold.toExponential(2);
      boostButton.textContent = settings.journeyMode === "glide"
        ? "Hold boost · ×" + DATA.glide.boostMultiplier.toFixed(1) +
          " · push " + DATA.glide.boostDepth.toFixed(2) + " e-folds"
        : "Hold boost · ×" + settings.diveBoostMultiplier.toFixed(2);
      boostButton.classList.toggle("boosting", journey.boostHeld);
      boostButton.setAttribute("aria-pressed", String(journey.boostHeld));
      const recordedCenter = settings.journeyMode === "breathing"
        ? center
        : journey.viewCenter;
      presetPairText.value =
        "c = " + formatVector(preset().constant) +
        "; view center = " + formatVector(recordedCenter);
      updateParameterVisibility();
      updateControlLabels();
    }

    presets.forEach(function (entry, index) {
      const option = document.createElement("option");
      option.value = String(index);
      option.textContent =
        presetLabel(index) + " · c " + formatComplex(entry.constant);
      presetSelect.append(option);
    });

    DATA.palettes.forEach(function (palette, index) {
      const option = document.createElement("option");
      option.value = String(index);
      option.textContent = palette.name;
      paletteSelect.append(option);
    });

    Object.keys(controls).forEach(function (name) {
      const control = controls[name];
      if (control.type === "checkbox") control.checked = settings[name];
      else control.value = String(settings[name]);
      control.addEventListener("input", function () {
        settings[name] = control.type === "checkbox"
          ? control.checked
          : Number(control.value);
        updateState();
      });
      control.addEventListener("change", function () {
        settings[name] = control.type === "checkbox"
          ? control.checked
          : Number(control.value);
        updateState();
      });
    });

    document.querySelectorAll("input[name='model']").forEach(function (input) {
      input.addEventListener("change", function () {
        settings.model = input.value;
        updateState();
      });
    });

    presetSelect.value = String(settings.presetIndex);
    journeyMode.value = settings.journeyMode;
    colorSelect.value = settings.colorPath;
    paletteSelect.value = String(settings.paletteIndex);

    presetSelect.addEventListener("change", function () {
      settings.presetIndex = Number(presetSelect.value);
      if (settings.journeyMode === "glide" || isDiveMode()) {
        newJourney();
      } else {
        applyConstantMorph();
        resetEdgeLock();
        updateEdgeLock(journey.window);
      }
      updateState();
    });

    glideSpeed.addEventListener("input", function () {
      settings.glideSpeed = Number(glideSpeed.value);
      updateState();
    });

    glideWindow.addEventListener("input", function () {
      settings.glideWindow = Number(glideWindow.value);
      if (settings.journeyMode === "glide") updateGlide(0);
      updateState();
    });

    journeyMode.addEventListener("change", function () {
      settings.journeyMode = journeyMode.value;
      journey.boostHeld = false;
      newJourney();
      updateState();
    });

    branchChoice.addEventListener("change", function () {
      settings.branchChoice = branchChoice.value;
      journey.window = DATA.settings.windowWidth[1];
      journey.viewRoll = 0;
      journey.diveHueScroll = 0;
      journey.escapeDepthOffset = 0;
      journey.rebaseCount = 0;
      applyConstantMorph();
      initializeFractalBranches(journey.branchSeed);
      startFractalLevel();
      updateState();
    });

    diveAbsoluteColoring.addEventListener("change", function () {
      settings.diveAbsoluteColoring = diveAbsoluteColoring.checked;
      updateState();
    });

    diveHueStep.addEventListener("input", function () {
      settings.diveHueStep = Number(diveHueStep.value);
      journey.diveHueScroll = wrap(journey.rebaseCount * settings.diveHueStep);
      updateState();
    });

    tileSoftening.addEventListener("input", function () {
      settings.tileSoftening = Number(tileSoftening.value);
      updateState();
    });

    diveRate.addEventListener("input", function () {
      settings.diveRate = Number(diveRate.value);
      updateState();
    });

    diveBoostMultiplier.addEventListener("input", function () {
      settings.diveBoostMultiplier = Number(diveBoostMultiplier.value);
      updateState();
    });

    diveWrapThreshold.addEventListener("input", function () {
      settings.diveWrapThreshold = Number(diveWrapThreshold.value);
      journey.window = DATA.settings.windowWidth[1];
      journey.viewRoll = 0;
      journey.diveHueScroll = 0;
      journey.escapeDepthOffset = 0;
      journey.rebaseCount = 0;
      applyConstantMorph();
      if (settings.journeyMode === "fractal") {
        updateFractalTarget(true);
        startFractalLevel();
      } else {
        updateCorridorFixedPoint();
      }
      updateState();
    });

    boostButton.addEventListener("pointerdown", function (event) {
      event.preventDefault();
      journey.boostHeld = true;
      updateState();
    });

    function releaseBoost() {
      journey.boostHeld = false;
      updateState();
    }

    window.addEventListener("pointerup", releaseBoost);
    window.addEventListener("pointercancel", releaseBoost);
    window.addEventListener("blur", releaseBoost);

    function updateViewCenter(axis, control) {
      activeViewCenter()[axis] = Number(control.value);
      resetEdgeLock();
      updateEdgeLock(journey.window);
      updateState();
    }

    viewCenterX.addEventListener("input", function () {
      updateViewCenter(0, viewCenterX);
    });

    viewCenterY.addEventListener("input", function () {
      updateViewCenter(1, viewCenterY);
    });

    colorSelect.addEventListener("change", function () {
      settings.colorPath = colorSelect.value;
      drawPaletteStrip();
      updateState();
    });

    paletteSelect.addEventListener("change", function () {
      settings.paletteIndex = Number(paletteSelect.value);
      drawPaletteStrip();
      updateState();
    });

    pauseButton.addEventListener("click", function () {
      running = !running;
      updateState();
    });

    journeyButton.addEventListener("click", function () {
      newJourney();
      running = true;
      updateState();
    });

    window.__juliaDesk = {
      DATA: DATA,
      presets: presets,
      settings: settings,
      journey: journey,
      applyConstantMorph: applyConstantMorph,
      initializeGlide: initializeGlide,
      updateGlide: updateGlide,
      glideDistanceAt: glideDistanceAt,
      glideFieldAt: glideFieldAt,
      newJourney: newJourney,
      isRunning: function () { return running; },
      initializeFractalBranches: initializeFractalBranches,
      updateFractalTarget: updateFractalTarget,
      startFractalLevel: startFractalLevel,
      applyFractalRebase: applyFractalRebase,
      updateFractalDive: updateFractalDive,
      updateJourney: updateJourney,
      renderFrame: renderFrame
    };

    new ResizeObserver(resizeWall).observe(canvas);
    newJourney();
    resizeWall();
    updateState();

    let lastTime = 0;
    let lastStateUpdate = 0;

    function tick(now) {
      const deltaSeconds = lastTime ? (now - lastTime) / 1000 : 1 / 60;
      lastTime = now;
      if (deltaSeconds > 0) {
        smoothedFps += (((1 / deltaSeconds) - smoothedFps) * 0.05);
      }
      if (running) updateJourney(deltaSeconds);
      const frameStart = performance.now();
      drawWall(renderFrame());
      smoothedFrameMilliseconds += (
        (performance.now() - frameStart) - smoothedFrameMilliseconds
      ) * 0.05;
      if (now - lastStateUpdate > 160) {
        updateState();
        lastStateUpdate = now;
      }
      requestAnimationFrame(tick);
    }

    requestAnimationFrame(tick);
  </script>
</body>
</html>
'''


def main() -> None:
    settings = load_julia_settings()
    palettes = load_scene_palettes(settings["paletteConditioning"])
    tiles, tile_pitch, bounds_width = load_layout()
    payload = json.dumps(
        {
            "tiles": tiles,
            "tilePitch": tile_pitch,
            "boundsWidth": bounds_width,
            "settings": settings,
            "authoredPresetCount": EXPECTED_PRESETS,
            "candidatePresets": CANDIDATE_PRESETS,
            "glide": {
                "travelSpeed": GLIDE_TRAVEL_SPEED_DEFAULT,
                "window": GLIDE_WINDOW_DEFAULT,
                "distanceRatio": GLIDE_DISTANCE_RATIO,
                "boostMultiplier": GLIDE_BOOST_MULTIPLIER,
                "boostDepth": GLIDE_BOOST_DEPTH,
                "boostInRate": GLIDE_BOOST_IN_RATE,
                "boostOutRate": GLIDE_BOOST_OUT_RATE,
                "gradientStepRatio": GLIDE_GRADIENT_STEP_RATIO,
                "rollRange": GLIDE_ROLL_RANGE,
                "rollDamping": GLIDE_ROLL_DAMPING,
                "tileSoftening": TILE_SOFTENING_DEFAULT,
            },
            "dive": {
                "rate": DIVE_RATE_DEFAULT,
                "boostMultiplier": DIVE_BOOST_MULTIPLIER_DEFAULT,
                "wrapThreshold": DIVE_WRAP_THRESHOLD,
                "hueStep": DIVE_HUE_STEP_DEFAULT,
                "branchMultiplierFloor": FRACTAL_BRANCH_MULTIPLIER_FLOOR,
                "targetEpsilonTiles": FRACTAL_TARGET_EPSILON_TILES,
                "tileSoftening": TILE_SOFTENING_DEFAULT,
            },
            "palettes": palettes,
        },
        separators=(",", ":"),
        allow_nan=False,
    )
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(
        HTML.replace("__PAYLOAD__", payload),
        encoding="utf-8",
    )
    print(
        f"wrote {OUTPUT_PATH} "
        f"({len(tiles)} Tiles, {len(settings['presets'])} authored presets, "
        f"{len(CANDIDATE_PRESETS)} candidates, "
        f"{len(palettes)} palettes, median Tile pitch {tile_pitch:.4f})"
    )


if __name__ == "__main__":
    main()
