#!/usr/bin/env python3
"""Build the throwaway AnimateLoops palette desk simulation."""

from __future__ import annotations

import colorsys
import json
import math
import re
from pathlib import Path


HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
LAYOUT_PATH = REPO / "Assets/StreamingAssets/penrose_layout.txt"
SCENE_PATH = REPO / "Assets/Scenes/SampleScene.unity"
OUTPUT_PATH = HERE / "out/sim.html"

PALETTE_LENGTH = 32
EXPECTED_MESH_FLOATS = 10_800
EXPECTED_TILES = 900
EXPECTED_GROUPS = 73
EXPECTED_DEFINITIONS = 17
EXPECTED_UNIQUE_PALETTES = 16

ANGLES_CONDITIONING = {
    "target_luminance": 0.4,
    "minimum_luminance": 0.12,
    "luminance_equalization": 0.85,
    "hue_spread_reference": 0.5,
    "maximum_luminance_scale": 4.0,
    "dark_luminance_threshold": 0.03,
    "duplicate_threshold": 0.08,
    "hue_redistribution": 1.0,
}

Color = tuple[float, float, float]


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
                colors[(source_index - distance + length) % length], dark_threshold
            )
            if usable:
                previous = (hue, saturation, distance)
        if following is None:
            usable, hue, saturation = try_read_hue_donor(
                colors[(source_index + distance) % length], dark_threshold
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
            colors, source_index, conditioning["dark_luminance_threshold"]
        )
    vivid = colorsys.hsv_to_rgb(hue, saturation, 1.0)
    target = max(
        conditioning["target_luminance"], conditioning["minimum_luminance"]
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
    anchors: list[Color], output_length: int, hue_redistribution: float
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
            (target - segment_start) / segment_length if segment_length > 0.0 else 0.0
        )
        output.append(
            lerp_color(anchors[segment], anchors[(segment + 1) % len(anchors)], amount)
        )
    return output


def condition_palette(
    colors: list[Color], conditioning: dict[str, float]
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
        1.0 - (math.sqrt((hue_x * hue_x) + (hue_y * hue_y)) / saturation_total)
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
                    color, luminance, equalization, palette_lift, conditioning
                )
            )
    anchors = collapse_near_duplicates(
        balanced, conditioning["duplicate_threshold"]
    )
    return redistribute(anchors, len(colors), conditioning["hue_redistribution"])


def map_gradient(position: float, table: list[tuple[float, Color]]) -> Color:
    for left, right in zip(table, table[1:]):
        if left[0] <= position <= right[0]:
            span = right[0] - left[0]
            amount = 0.0 if span == 0.0 else (position - left[0]) / span
            return lerp_color(left[1], right[1], amount)
    return 0.0, 0.0, 0.0


def load_scene_palettes() -> list[dict[str, object]]:
    scene = SCENE_PATH.read_text(encoding="utf-8")
    source_match = re.search(
        r"^  paletteSource: '(.*?)'\n^  jsonSource:", scene, flags=re.M | re.S
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
                tuple(channel / 255.0 for channel in values[index + 1 : index + 4]),
            )
            for index in range(0, len(values), 4)
        ]
        raw = [map_gradient(index / PALETTE_LENGTH, table) for index in range(PALETTE_LENGTH)]
        conditioned = condition_palette(raw, ANGLES_CONDITIONING)
        palettes.append({"name": name, "raw": raw, "conditioned": conditioned})

    assert len(palettes) == EXPECTED_UNIQUE_PALETTES, (
        f"expected {EXPECTED_UNIQUE_PALETTES} uniquely named palettes, got {len(palettes)}"
    )
    return palettes


def load_layout() -> tuple[list[float], list[dict[str, list[int]]]]:
    source = LAYOUT_PATH.read_text(encoding="utf-8-sig")
    stripped = "\n".join(
        line for line in source.splitlines() if not line.lstrip().startswith("//")
    )
    layout = json.loads(stripped)
    mesh = layout["Mesh"]
    assert len(mesh) == EXPECTED_MESH_FLOATS, (
        f"expected {EXPECTED_MESH_FLOATS} mesh floats, got {len(mesh)}"
    )
    assert len(mesh) // 12 == EXPECTED_TILES, (
        f"expected {EXPECTED_TILES} tiles, got {len(mesh) // 12}"
    )

    packed = layout["shapes"]["loops"]
    assert packed[0] == EXPECTED_GROUPS, (
        f"expected {EXPECTED_GROUPS} loop groups, got {packed[0]}"
    )
    groups = []
    for group_index in range(packed[0]):
        pointer = packed[group_index + 1]
        count = packed[pointer]
        start = pointer + 1
        tiles = packed[start : start + count]
        assert len(tiles) == count, f"truncated loop group {group_index}"
        assert all(0 <= tile < EXPECTED_TILES for tile in tiles), group_index
        groups.append(
            {
                "tiles": tiles,
                "packedIndices": list(range(start, start + count)),
            }
        )
    return mesh, groups


HTML = r'''<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>AnimateLoops — palette crawl prototype</title>
  <style>
    :root {
      color-scheme: dark;
      --page: #090b10;
      --panel: #121620;
      --panel-2: #191f2b;
      --line: #2b3342;
      --text: #f2f4f8;
      --muted: #9aa5b5;
      --accent: #ffb454;
      --accent-2: #ff7a59;
      --focus: #8bd5ff;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      min-width: 320px;
      background:
        radial-gradient(circle at 30% -10%, rgba(255, 122, 89, 0.12), transparent 36rem),
        var(--page);
      color: var(--text);
      font: 15px/1.45 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }

    button, select, input { font: inherit; }

    button, select {
      border: 1px solid var(--line);
      border-radius: 10px;
      background: var(--panel-2);
      color: var(--text);
    }

    button { cursor: pointer; }
    button:hover { border-color: #4a566d; }
    button:focus-visible, select:focus-visible, input:focus-visible {
      outline: 2px solid var(--focus);
      outline-offset: 2px;
    }

    main {
      width: min(1440px, calc(100% - 32px));
      margin: 0 auto;
      padding: 28px 0 40px;
    }

    .intro {
      display: flex;
      align-items: end;
      justify-content: space-between;
      gap: 24px;
      margin-bottom: 18px;
    }

    .eyebrow {
      margin: 0 0 6px;
      color: var(--accent);
      font-size: 12px;
      font-weight: 750;
      letter-spacing: 0.12em;
      text-transform: uppercase;
    }

    h1 {
      margin: 0;
      font-size: clamp(28px, 4vw, 52px);
      font-weight: 760;
      letter-spacing: -0.045em;
      line-height: 1;
    }

    .question {
      max-width: 760px;
      margin: 12px 0 0;
      color: #c7cfda;
      font-size: 16px;
    }

    .truths {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      gap: 8px;
    }

    .chip {
      padding: 6px 9px;
      border: 1px solid var(--line);
      border-radius: 999px;
      background: rgba(18, 22, 32, 0.8);
      color: var(--muted);
      font-size: 12px;
      white-space: nowrap;
    }

    .workspace {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 340px;
      gap: 16px;
      align-items: start;
    }

    .stage, .controls {
      border: 1px solid var(--line);
      border-radius: 18px;
      background: rgba(18, 22, 32, 0.92);
      box-shadow: 0 16px 50px rgba(0, 0, 0, 0.24);
    }

    .stage { overflow: hidden; }

    .canvas-wrap {
      position: relative;
      aspect-ratio: 50 / 22;
      min-height: 260px;
      background: #020307;
    }

    #wall {
      display: block;
      width: 100%;
      height: 100%;
    }

    .live-badge {
      position: absolute;
      top: 12px;
      left: 12px;
      padding: 6px 9px;
      border: 1px solid rgba(255, 255, 255, 0.14);
      border-radius: 999px;
      background: rgba(3, 5, 9, 0.72);
      backdrop-filter: blur(8px);
      color: #dce3ec;
      font-size: 12px;
    }

    .live-badge::before {
      display: inline-block;
      width: 7px;
      height: 7px;
      margin-right: 7px;
      border-radius: 50%;
      background: #52d273;
      content: "";
    }

    .live-badge.paused::before { background: var(--accent); }

    .state-bar {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      border-top: 1px solid var(--line);
    }

    .state-item {
      min-width: 0;
      padding: 12px 14px;
    }

    .state-item + .state-item { border-left: 1px solid var(--line); }
    .state-label { color: var(--muted); font-size: 11px; text-transform: uppercase; letter-spacing: 0.08em; }
    .state-value { overflow: hidden; margin-top: 2px; text-overflow: ellipsis; white-space: nowrap; }

    .controls {
      padding: 16px;
    }

    .control-group + .control-group {
      margin-top: 18px;
      padding-top: 18px;
      border-top: 1px solid var(--line);
    }

    .control-title {
      display: block;
      margin-bottom: 8px;
      color: #dce3ec;
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }

    .hint { margin: 7px 0 0; color: var(--muted); font-size: 12px; }

    .segmented {
      display: grid;
      grid-template-columns: repeat(var(--columns), 1fr);
      gap: 5px;
      padding: 4px;
      border-radius: 12px;
      background: #0c1017;
    }

    .segmented label { min-width: 0; }
    .segmented input { position: absolute; opacity: 0; pointer-events: none; }
    .segmented span {
      display: block;
      overflow: hidden;
      padding: 8px 6px;
      border: 1px solid transparent;
      border-radius: 8px;
      color: var(--muted);
      text-align: center;
      text-overflow: ellipsis;
      white-space: nowrap;
      cursor: pointer;
    }

    .segmented input:checked + span {
      border-color: #435069;
      background: var(--panel-2);
      color: var(--text);
    }

    select { width: 100%; padding: 9px 10px; }

    .palette-compare {
      display: grid;
      gap: 7px;
      margin-top: 10px;
    }

    .strip-row { display: grid; grid-template-columns: 66px 1fr; gap: 8px; align-items: center; }
    .strip-label { color: var(--muted); font-size: 11px; }
    .palette-strip {
      display: block;
      width: 100%;
      height: 22px;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 6px;
      background: #05070a;
    }

    .switch-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 14px;
      padding: 5px 0;
    }

    .switch-copy { min-width: 0; }
    .switch-name { display: block; }
    .switch-detail { display: block; color: var(--muted); font-size: 11px; }
    .switch-row input { width: 18px; height: 18px; accent-color: var(--accent-2); }

    .slider-row + .slider-row { margin-top: 12px; }
    .slider-head { display: flex; justify-content: space-between; gap: 12px; margin-bottom: 5px; }
    .slider-value { color: var(--accent); font-variant-numeric: tabular-nums; }
    input[type="range"] { width: 100%; accent-color: var(--accent-2); }

    .actions { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
    .actions button { padding: 10px 12px; }
    .primary { border-color: #77533d; background: #3a251c; }

    .simulated {
      margin-top: 8px;
      padding: 9px 10px;
      border: 1px solid #5c442c;
      border-radius: 9px;
      background: rgba(255, 180, 84, 0.07);
      color: #d8c2a4;
      font-size: 12px;
    }

    @media (max-width: 960px) {
      .intro { align-items: start; flex-direction: column; }
      .truths { justify-content: flex-start; }
      .workspace { grid-template-columns: 1fr; }
      .controls { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; }
      .control-group + .control-group { margin: 0; padding: 0; border: 0; }
    }

    @media (max-width: 620px) {
      main { width: min(100% - 20px, 1440px); padding-top: 20px; }
      .canvas-wrap { min-height: 190px; }
      .state-bar { grid-template-columns: 1fr; }
      .state-item + .state-item { border-top: 1px solid var(--line); border-left: 0; }
      .controls { display: block; }
      .control-group + .control-group { margin-top: 18px; padding-top: 18px; border-top: 1px solid var(--line); }
    }
  </style>
</head>
<body>
  <main>
    <header class="intro">
      <div>
        <p class="eyebrow">Prototype — throwaway desk wall</p>
        <h1>AnimateLoops palette crawl</h1>
        <p class="question">Does cyclic palette sampling preserve the signature crawl, and how do the 16 scene palettes read raw versus conditioned when applied to the Ring/Arc foreground, the background, or both?</p>
      </div>
      <div class="truths" aria-label="Parsed source facts">
        <span class="chip">900 Tiles</span>
        <span class="chip">73 Ring/Arc Motifs</span>
        <span class="chip">16 scene palettes</span>
      </div>
    </header>

    <div class="workspace">
      <section class="stage" aria-label="Animated Penrose Wall simulation">
        <div class="canvas-wrap">
          <canvas id="wall"></canvas>
          <div class="live-badge" id="liveBadge">Running</div>
        </div>
        <div class="state-bar" aria-live="polite">
          <div class="state-item">
            <div class="state-label">Color lookup</div>
            <div class="state-value" id="modeState">Hue wheel · current runtime</div>
          </div>
          <div class="state-item">
            <div class="state-label">Palette table</div>
            <div class="state-value" id="paletteState">Raw · blended</div>
          </div>
          <div class="state-item">
            <div class="state-label">Motion</div>
            <div class="state-value" id="motionState">0.010/frame · 0.010/Tile</div>
          </div>
        </div>
      </section>

      <aside class="controls" aria-label="Simulation controls">
        <div class="control-group">
          <span class="control-title">Mode</span>
          <div class="segmented" style="--columns: 2">
            <label><input type="radio" name="mode" value="baseline" checked><span>Hue wheel</span></label>
            <label><input type="radio" name="mode" value="palette"><span>Palette</span></label>
          </div>
          <p class="hint">Hue wheel is the current <code>HSVToRGB</code> baseline.</p>
        </div>

        <div class="control-group">
          <span class="control-title">Palette application</span>
          <div class="segmented" style="--columns: 3">
            <label><input type="radio" name="application" value="foreground" checked><span>Foreground</span></label>
            <label><input type="radio" name="application" value="background"><span>Background</span></label>
            <label><input type="radio" name="application" value="both"><span>Both</span></label>
          </div>
          <p class="hint">The other layer stays on the hue wheel. This setting only changes Palette mode.</p>
        </div>

        <div class="control-group">
          <label class="control-title" for="paletteSelect">Scene palette</label>
          <select id="paletteSelect"></select>
          <div class="palette-compare">
            <div class="strip-row"><span class="strip-label">Authored</span><canvas class="palette-strip" id="rawStrip"></canvas></div>
            <div class="strip-row"><span class="strip-label" id="sampledLabel">Sampled</span><canvas class="palette-strip" id="sampledStrip"></canvas></div>
          </div>
        </div>

        <div class="control-group">
          <span class="control-title">Palette reading</span>
          <label class="switch-row">
            <span class="switch-copy"><span class="switch-name">Conditioning</span><span class="switch-detail">Angles Standalone preset</span></span>
            <input id="conditioning" type="checkbox">
          </label>
          <label class="switch-row">
            <span class="switch-copy"><span class="switch-name">Blend entries</span><span class="switch-detail">Includes last → first wrap</span></span>
            <input id="blend" type="checkbox" checked>
          </label>
        </div>

        <div class="control-group">
          <span class="control-title">Motion</span>
          <label class="slider-row">
            <span class="slider-head"><span>Crawl speed</span><output class="slider-value" id="crawlValue">0.010/frame</output></span>
            <input id="crawl" type="range" min="0" max="0.04" step="0.001" value="0.01">
          </label>
          <label class="slider-row">
            <span class="slider-head"><span>Tile step</span><output class="slider-value" id="tileValue">0.010/Tile</output></span>
            <input id="tileStep" type="range" min="0" max="0.04" step="0.001" value="0.01">
          </label>
        </div>

        <div class="control-group">
          <span class="control-title">Optional response</span>
          <label class="switch-row">
            <span class="switch-copy"><span class="switch-name">Simulated beat pulse</span><span class="switch-detail">120 BPM · +0.25 maximum position shift</span></span>
            <input id="beatPulse" type="checkbox">
          </label>
          <div class="simulated">Synthetic unipolar envelope only. No OSC, BeatManager, Fill, Drop, Levels, or Energy.</div>
        </div>

        <div class="control-group actions">
          <button class="primary" id="pauseButton" type="button">Pause</button>
          <button id="resetButton" type="button">Reset authored values</button>
        </div>
      </aside>
    </div>
  </main>

  <script>
    const DATA = __PAYLOAD__;

    const AnimateLoopsModel = (() => {
      const TILE_COUNT = 900;
      const BACKGROUND_HUE_RATE = 0.1;
      const BEAT_BPM = 120;
      const BEAT_SHIFT = 0.25;

      const wrap = value => value - Math.floor(value);

      function hsvToRgb(hue, saturation, value) {
        hue = wrap(hue);
        const sector = hue * 6;
        const index = Math.floor(sector);
        const fraction = sector - index;
        const p = value * (1 - saturation);
        const q = value * (1 - saturation * fraction);
        const t = value * (1 - saturation * (1 - fraction));
        return [
          [value, t, p], [q, value, p], [p, value, t],
          [p, q, value], [t, p, value], [value, p, q]
        ][index % 6];
      }

      function readCyclic(position, palette, blend) {
        position = wrap(position);
        if (position <= 0 || palette.length === 1) return palette[0];
        const scaled = position * palette.length;
        const first = Math.floor(scaled);
        const second = (first + 1) % palette.length;
        const fraction = scaled % 1;
        if (!blend) return fraction < 0.5 ? palette[first] : palette[second];
        return [
          palette[first][0] + ((palette[second][0] - palette[first][0]) * fraction),
          palette[first][1] + ((palette[second][1] - palette[first][1]) * fraction),
          palette[first][2] + ((palette[second][2] - palette[first][2]) * fraction)
        ];
      }

      function create(data) {
        let background = 0;
        let effectTime = 0;
        let groups = [];
        let buffer = new Float32Array(TILE_COUNT * 3);

        function reset() {
          background = Math.random();
          effectTime = 0;
          groups = data.groups.map(() => ({ h: Math.random(), s: Math.random(), v: 1 }));
        }

        function lookup(position, saturation, brightness, layer, settings, palette) {
          const paletteApplies = settings.mode === "palette" &&
            (settings.application === "both" || settings.application === layer);
          return paletteApplies
            ? readCyclic(position, palette, settings.blend)
            : hsvToRgb(position, saturation, brightness);
        }

        function frame(deltaSeconds, advance, settings) {
          const source = data.palettes[settings.paletteIndex];
          const palette = settings.conditioning ? source.conditioned : source.raw;

          if (advance) {
            effectTime += deltaSeconds;
            groups[Math.floor(Math.random() * groups.length)] = {
              h: Math.random(), s: Math.random(), v: 1
            };
            background = wrap(background + (deltaSeconds * BACKGROUND_HUE_RATE));
          }

          const beatPhase = wrap(effectTime * BEAT_BPM / 60);
          const envelope = settings.beatPulse ? Math.pow(1 - beatPhase, 4) : 0;
          const hueShift = envelope * BEAT_SHIFT;
          const backgroundColor = lookup(
            background + hueShift, 1, 1, "background", settings, palette
          );
          for (let tile = 0; tile < TILE_COUNT; tile++) {
            const offset = tile * 3;
            buffer[offset] = backgroundColor[0];
            buffer[offset + 1] = backgroundColor[1];
            buffer[offset + 2] = backgroundColor[2];
          }

          for (let groupIndex = 0; groupIndex < data.groups.length; groupIndex++) {
            const shape = data.groups[groupIndex];
            const color = groups[groupIndex];
            for (let j = 0; j < shape.tiles.length; j++) {
              const phase = color.h + (settings.tileStep * shape.packedIndices[j]) + hueShift;
              const rgb = lookup(phase, color.s, color.v, "foreground", settings, palette);
              const offset = shape.tiles[j] * 3;
              buffer[offset] = rgb[0];
              buffer[offset + 1] = rgb[1];
              buffer[offset + 2] = rgb[2];
            }
            if (advance) color.h = wrap(color.h + settings.crawlSpeed);
          }
          return { buffer, envelope };
        }

        reset();
        return { frame, reset };
      }

      return { create, readCyclic };
    })();

    const canvas = document.querySelector("#wall");
    const context = canvas.getContext("2d", { alpha: false });
    const paletteSelect = document.querySelector("#paletteSelect");
    const rawStrip = document.querySelector("#rawStrip");
    const sampledStrip = document.querySelector("#sampledStrip");
    const conditioning = document.querySelector("#conditioning");
    const blend = document.querySelector("#blend");
    const crawl = document.querySelector("#crawl");
    const tileStep = document.querySelector("#tileStep");
    const beatPulse = document.querySelector("#beatPulse");
    const pauseButton = document.querySelector("#pauseButton");
    const resetButton = document.querySelector("#resetButton");
    const liveBadge = document.querySelector("#liveBadge");
    const modeState = document.querySelector("#modeState");
    const paletteState = document.querySelector("#paletteState");
    const motionState = document.querySelector("#motionState");
    const crawlValue = document.querySelector("#crawlValue");
    const tileValue = document.querySelector("#tileValue");
    const sampledLabel = document.querySelector("#sampledLabel");

    const defaults = {
      mode: "baseline",
      application: "foreground",
      paletteIndex: Math.max(0, DATA.palettes.findIndex(p => p.name === "bhw1_24_gp")),
      conditioning: false,
      blend: true,
      crawlSpeed: 0.01,
      tileStep: 0.01,
      beatPulse: false
    };
    const settings = { ...defaults };
    let running = true;
    let screenPaths = [];
    let lastTime = 0;
    let lastStateUpdate = 0;
    let smoothedFps = 60;
    const model = AnimateLoopsModel.create(DATA);

    for (const [index, palette] of DATA.palettes.entries()) {
      const option = document.createElement("option");
      option.value = String(index);
      option.textContent = palette.name;
      paletteSelect.append(option);
    }

    const worldTiles = [];
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (let tile = 0; tile < 900; tile++) {
      const unique = new Map();
      for (let point = 0; point < 6; point++) {
        const offset = (tile * 12) + (point * 2);
        const x = DATA.mesh[offset];
        const y = DATA.mesh[offset + 1];
        unique.set(`${x},${y}`, [x, y]);
        minX = Math.min(minX, x); minY = Math.min(minY, y);
        maxX = Math.max(maxX, x); maxY = Math.max(maxY, y);
      }
      if (unique.size !== 4) throw new Error(`Tile ${tile} does not resolve to four rhomb corners`);
      const points = [...unique.values()];
      const centerX = points.reduce((sum, point) => sum + point[0], 0) / points.length;
      const centerY = points.reduce((sum, point) => sum + point[1], 0) / points.length;
      points.sort((a, b) =>
        Math.atan2(a[1] - centerY, a[0] - centerX) -
        Math.atan2(b[1] - centerY, b[0] - centerX)
      );
      worldTiles.push(points);
    }

    function resizeWall() {
      const rect = canvas.getBoundingClientRect();
      const dpr = window.devicePixelRatio || 1;
      canvas.width = Math.round(rect.width * dpr);
      canvas.height = Math.round(rect.height * dpr);
      context.setTransform(dpr, 0, 0, dpr, 0, 0);
      const padding = 12;
      const scale = Math.min(
        (rect.width - (padding * 2)) / (maxX - minX),
        (rect.height - (padding * 2)) / (maxY - minY)
      );
      const drawnWidth = (maxX - minX) * scale;
      const drawnHeight = (maxY - minY) * scale;
      const offsetX = (rect.width - drawnWidth) / 2;
      const offsetY = (rect.height - drawnHeight) / 2;
      screenPaths = worldTiles.map(points => {
        const path = new Path2D();
        points.forEach(([x, y], index) => {
          const screenX = offsetX + ((x - minX) * scale);
          const screenY = offsetY + ((maxY - y) * scale);
          if (index === 0) path.moveTo(screenX, screenY);
          else path.lineTo(screenX, screenY);
        });
        path.closePath();
        return path;
      });
      drawPaletteStrips();
    }

    function rgbCss(buffer, offset) {
      return `rgb(${Math.round(buffer[offset] * 255)} ${Math.round(buffer[offset + 1] * 255)} ${Math.round(buffer[offset + 2] * 255)})`;
    }

    function drawWall(buffer) {
      const rect = canvas.getBoundingClientRect();
      context.fillStyle = "#020307";
      context.fillRect(0, 0, rect.width, rect.height);
      for (let tile = 0; tile < 900; tile++) {
        context.fillStyle = rgbCss(buffer, tile * 3);
        context.fill(screenPaths[tile]);
      }
    }

    function drawStrip(target, palette) {
      const rect = target.getBoundingClientRect();
      const dpr = window.devicePixelRatio || 1;
      target.width = Math.max(1, Math.round(rect.width * dpr));
      target.height = Math.max(1, Math.round(rect.height * dpr));
      const stripContext = target.getContext("2d");
      stripContext.setTransform(dpr, 0, 0, dpr, 0, 0);
      for (let x = 0; x < Math.ceil(rect.width); x++) {
        const rgb = AnimateLoopsModel.readCyclic(x / Math.max(1, rect.width), palette, settings.blend);
        stripContext.fillStyle = `rgb(${Math.round(rgb[0] * 255)} ${Math.round(rgb[1] * 255)} ${Math.round(rgb[2] * 255)})`;
        stripContext.fillRect(x, 0, 1, rect.height);
      }
    }

    function drawPaletteStrips() {
      const palette = DATA.palettes[settings.paletteIndex];
      drawStrip(rawStrip, palette.raw);
      drawStrip(sampledStrip, settings.conditioning ? palette.conditioned : palette.raw);
      sampledLabel.textContent = settings.conditioning ? "Conditioned" : "Sampled";
    }

    function selected(name) {
      return document.querySelector(`input[name="${name}"]:checked`).value;
    }

    function syncSettingsFromControls() {
      settings.mode = selected("mode");
      settings.application = selected("application");
      settings.paletteIndex = Number(paletteSelect.value);
      settings.conditioning = conditioning.checked;
      settings.blend = blend.checked;
      settings.crawlSpeed = Number(crawl.value);
      settings.tileStep = Number(tileStep.value);
      settings.beatPulse = beatPulse.checked;
      crawlValue.textContent = `${settings.crawlSpeed.toFixed(3)}/frame`;
      tileValue.textContent = `${settings.tileStep.toFixed(3)}/Tile`;
      drawPaletteStrips();
      updateState();
    }

    function setControlsFromSettings() {
      document.querySelector(`input[name="mode"][value="${settings.mode}"]`).checked = true;
      document.querySelector(`input[name="application"][value="${settings.application}"]`).checked = true;
      paletteSelect.value = String(settings.paletteIndex);
      conditioning.checked = settings.conditioning;
      blend.checked = settings.blend;
      crawl.value = String(settings.crawlSpeed);
      tileStep.value = String(settings.tileStep);
      beatPulse.checked = settings.beatPulse;
      syncSettingsFromControls();
    }

    function applicationName(value) {
      return { foreground: "foreground", background: "background", both: "both layers" }[value];
    }

    function updateState(envelope = 0) {
      modeState.textContent = settings.mode === "baseline"
        ? "Hue wheel · current runtime"
        : `Palette · ${applicationName(settings.application)}`;
      paletteState.textContent = `${settings.conditioning ? "Conditioned" : "Raw"} · ${settings.blend ? "blended" : "stepped"} · ${DATA.palettes[settings.paletteIndex].name}`;
      motionState.textContent = `${settings.crawlSpeed.toFixed(3)}/frame · ${settings.tileStep.toFixed(3)}/Tile${settings.beatPulse ? ` · pulse ${envelope.toFixed(2)}` : ""}`;
      liveBadge.textContent = running ? `Running · ${Math.round(smoothedFps)} fps` : "Paused";
      liveBadge.classList.toggle("paused", !running);
      pauseButton.textContent = running ? "Pause" : "Resume";
    }

    document.querySelectorAll("input[name='mode'], input[name='application']").forEach(input => {
      input.addEventListener("change", syncSettingsFromControls);
    });
    [paletteSelect, conditioning, blend, crawl, tileStep, beatPulse].forEach(input => {
      input.addEventListener("input", syncSettingsFromControls);
      input.addEventListener("change", syncSettingsFromControls);
    });

    pauseButton.addEventListener("click", () => {
      running = !running;
      updateState();
    });

    resetButton.addEventListener("click", () => {
      Object.assign(settings, defaults);
      running = true;
      model.reset();
      setControlsFromSettings();
    });

    new ResizeObserver(resizeWall).observe(canvas);
    setControlsFromSettings();
    resizeWall();

    function tick(now) {
      const deltaSeconds = lastTime ? (now - lastTime) / 1000 : 1 / 60;
      lastTime = now;
      if (deltaSeconds > 0) {
        const instantaneous = 1 / deltaSeconds;
        smoothedFps += (instantaneous - smoothedFps) * 0.05;
      }
      const result = model.frame(deltaSeconds, running, settings);
      drawWall(result.buffer);
      if (now - lastStateUpdate > 150) {
        updateState(result.envelope);
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
    mesh, groups = load_layout()
    palettes = load_scene_palettes()
    payload = json.dumps(
        {"mesh": mesh, "groups": groups, "palettes": palettes},
        separators=(",", ":"),
        allow_nan=False,
    )
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(HTML.replace("__PAYLOAD__", payload), encoding="utf-8")
    print(
        f"wrote {OUTPUT_PATH} "
        f"({EXPECTED_TILES} tiles, {EXPECTED_GROUPS} groups, {len(palettes)} palettes)"
    )


if __name__ == "__main__":
    main()
