#!/usr/bin/env python3
"""Build the throwaway ColorSparkle palette audition simulation."""

from __future__ import annotations

import json
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
EXPECTED_DEFINITIONS = 17
EXPECTED_UNIQUE_PALETTES = 16
ANIM_PALETTE_TRANSITION_TIME = 3.0

# This object contains only values stated in the redesign brief. Keep conditioning
# defaults separate because the brief names those controls without assigning values.
DEFAULTS = {
    "fadePerFrame": 0.98,
    "floorLevel": 0.0,
    "sparklesPerSecond": 900.0,
    "confettiChance": 0.25,
    "perSparkleHue": [0.0, 1.0],
    "coordinateRange": [0.0, 1.0],
    "glintChance": 0.003,
    "darkGlintThreshold": 0.2,
    "bloomFraction": 0.7,
    "darkBloomLuminance": 0.6,
    "autoRerollSeconds": 10.0,
    "variantLock": "auto",
    "autoReroll": True,
    "autoAdvance": True,
    "comparisonMode": False,
}

BRIEF_DEFAULTS = {
    "fadePerFrame": 0.98,
    "floorLevel": 0.0,
    "sparklesPerSecond": 900.0,
    "confettiChance": 0.25,
    "perSparkleHue": [0.0, 1.0],
    "coordinateRange": [0.0, 1.0],
    "glintChance": 0.003,
    "darkGlintThreshold": 0.2,
    "bloomFraction": 0.7,
    "darkBloomLuminance": 0.6,
    "autoRerollSeconds": 10.0,
    "variantLock": "auto",
    "autoReroll": True,
    "autoAdvance": True,
    "comparisonMode": False,
}

# The established palette prototypes use the Angles Standalone conditioning
# preset. HueSpreadReference is present in the current C# struct even though the
# brief's parenthetical field list omits it, so the page exposes it too.
CONDITIONING_DEFAULTS = {
    "targetLuminance": 0.4,
    "minimumLuminance": 0.12,
    "luminanceEqualization": 0.85,
    "hueSpreadReference": 0.5,
    "maximumLuminanceScale": 4.0,
    "darkLuminanceThreshold": 0.03,
    "duplicateThreshold": 0.08,
    "hueRedistribution": 1.0,
}

Color = tuple[float, float, float]


def lerp_color(start: Color, end: Color, amount: float) -> Color:
    """Linearly interpolates one normalized RGB color."""
    return tuple(
        a + ((b - a) * amount) for a, b in zip(start, end)
    )  # type: ignore[return-value]


def map_gradient(position: float, table: list[tuple[float, Color]]) -> Color:
    """Expands one serialized gradient position as the runtime parser does."""
    for left, right in zip(table, table[1:]):
        if left[0] <= position <= right[0]:
            span = right[0] - left[0]
            amount = 0.0 if span == 0.0 else (position - left[0]) / span
            return lerp_color(left[1], right[1], amount)
    return 0.0, 0.0, 0.0


def load_scene_palettes() -> list[dict[str, object]]:
    """Parses the real scene palette definitions into 32-entry RGB tables."""
    scene = SCENE_PATH.read_text(encoding="utf-8")
    source_match = re.search(
        r"^  paletteSource: '(.*?)'\n^  [A-Za-z_][A-Za-z0-9_]*:",
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
                tuple(channel / 255.0 for channel in values[index + 1 : index + 4]),
            )
            for index in range(0, len(values), 4)
        ]
        colors = [
            map_gradient(index / PALETTE_LENGTH, table)
            for index in range(PALETTE_LENGTH)
        ]
        palettes.append({"name": name, "colors": colors})

    assert len(palettes) == EXPECTED_UNIQUE_PALETTES, (
        f"expected {EXPECTED_UNIQUE_PALETTES} unique palettes, got {len(palettes)}"
    )
    return palettes


def load_layout() -> tuple[list[float], list[list[int]]]:
    """Loads the real 900-Tile mesh and each Tile's complete Neighbor list."""
    source = LAYOUT_PATH.read_text(encoding="utf-8-sig")
    stripped = "\n".join(
        line for line in source.splitlines() if not line.lstrip().startswith("//")
    )
    layout = json.loads(stripped)
    mesh = layout["Mesh"]
    tiles = layout["tiles"]

    assert len(mesh) == EXPECTED_MESH_FLOATS, (
        f"expected {EXPECTED_MESH_FLOATS} mesh floats, got {len(mesh)}"
    )
    assert len(mesh) // 12 == EXPECTED_TILES, (
        f"expected {EXPECTED_TILES} Tiles, got {len(mesh) // 12}"
    )
    assert len(tiles) == EXPECTED_TILES, (
        f"expected {EXPECTED_TILES} Tile records, got {len(tiles)}"
    )

    neighbors: list[list[int]] = []
    for tile_index, tile in enumerate(tiles):
        indices = [neighbor["tileIdx"] for neighbor in tile["neighbors"]]
        assert len(indices) == len(set(indices)), f"duplicate Neighbor on Tile {tile_index}"
        assert all(0 <= index < EXPECTED_TILES for index in indices), tile_index
        neighbors.append(indices)

    for tile_index, indices in enumerate(neighbors):
        for neighbor_index in indices:
            assert tile_index in neighbors[neighbor_index], (
                f"Neighbor link {tile_index}->{neighbor_index} is not reciprocal"
            )
    return mesh, neighbors


HTML = r'''<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>ColorSparkle palette audition</title>
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
      --good: #55d47a;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      min-width: 320px;
      background:
        radial-gradient(circle at 26% -12%, rgba(255, 116, 86, 0.13), transparent 36rem),
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
    button:focus-visible, select:focus-visible, input:focus-visible, summary:focus-visible {
      outline: 2px solid var(--focus);
      outline-offset: 2px;
    }

    main {
      width: min(1500px, calc(100% - 28px));
      margin: 0 auto;
      padding: 24px 0 40px;
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
      font-size: clamp(30px, 4vw, 52px);
      line-height: 1;
      letter-spacing: -0.045em;
    }

    .question {
      max-width: 850px;
      margin: 11px 0 0;
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
      grid-template-columns: minmax(0, 1fr) 390px;
      gap: 14px;
      align-items: start;
    }

    .stage, .controls {
      border: 1px solid var(--line);
      border-radius: 16px;
      background: rgba(17, 21, 29, 0.94);
      box-shadow: 0 18px 52px rgba(0, 0, 0, 0.24);
    }

    .stage {
      position: sticky;
      top: 14px;
      overflow: hidden;
    }

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
      background: rgba(2, 4, 8, 0.78);
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
      background: var(--good);
      content: "";
    }

    .live-badge.paused::before { background: var(--accent); }

    .state-bar {
      display: grid;
      grid-template-columns: 1.15fr 1.4fr 1fr 0.8fr;
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

    .actions {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 7px;
      margin-bottom: 14px;
    }

    .actions button { min-width: 0; padding-inline: 7px; }
    .primary { border-color: #77533d; background: #3a251c; }

    .control-group {
      padding: 14px 0;
      border-top: 1px solid var(--line);
    }

    .control-group:first-of-type { border-top: 0; padding-top: 0; }

    .control-title {
      display: block;
      margin: 0 0 10px;
      color: #dce3ec;
      font-size: 11px;
      font-weight: 750;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }

    .field + .field { margin-top: 11px; }
    .field-head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 4px;
    }

    .field-name { min-width: 0; }
    .field-value {
      flex: 0 0 auto;
      color: var(--accent);
      font-variant-numeric: tabular-nums;
    }

    input[type="range"] { width: 100%; accent-color: var(--accent-2); }
    input[type="checkbox"] { width: 18px; height: 18px; accent-color: var(--accent-2); }
    select { width: 100%; padding: 8px 9px; }

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

    .palette-strip {
      display: block;
      width: 100%;
      height: 34px;
      margin-top: 9px;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 7px;
      background: #05070a;
    }

    .strip-caption {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      margin-top: 5px;
      color: var(--muted);
      font-size: 11px;
    }

    .floor-chip {
      display: inline-block;
      width: 10px;
      height: 10px;
      margin-right: 5px;
      border: 1px solid rgba(255, 255, 255, 0.28);
      border-radius: 2px;
      vertical-align: -1px;
    }

    .two-up {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 9px;
    }

    .two-up .field + .field { margin-top: 0; }

    .hint {
      margin: 6px 0 0;
      color: var(--muted);
      font-size: 11px;
    }

    details.secondary {
      margin-top: 2px;
      padding: 10px 11px;
      border: 1px solid var(--line);
      border-radius: 10px;
      background: #0d1118;
    }

    details.secondary summary {
      color: #cbd3df;
      cursor: pointer;
      font-size: 12px;
      font-weight: 650;
    }

    details.secondary[open] summary { margin-bottom: 8px; }

    @media (max-width: 1040px) {
      .intro { align-items: start; flex-direction: column; }
      .facts { justify-content: flex-start; }
      .workspace { grid-template-columns: 1fr; }
      .stage { position: static; }
      .controls { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 20px; }
      .actions, details.secondary { grid-column: 1 / -1; }
      .control-group:nth-of-type(2) { border-top: 0; padding-top: 0; }
    }

    @media (max-width: 680px) {
      main { width: min(100% - 20px, 1500px); padding-top: 20px; }
      .canvas-wrap { min-height: 195px; }
      .state-bar { grid-template-columns: 1fr 1fr; }
      .state-item:nth-child(3) { border-top: 1px solid var(--line); border-left: 0; }
      .state-item:nth-child(4) { border-top: 1px solid var(--line); }
      .controls { display: block; }
      .control-group:nth-of-type(2) { border-top: 1px solid var(--line); padding-top: 14px; }
    }
  </style>
</head>
<body>
  <main>
    <header class="intro">
      <div>
        <p class="eyebrow">Prototype · throwaway desk wall</p>
        <h1>ColorSparkle palette audition</h1>
        <p class="question">Does palette luminance survive as the sparkle's character when dark colors stay dark, rare glints reveal their Neighbor Tiles, and the baseline remains beat-still?</p>
      </div>
      <div class="facts" aria-label="Baked source facts">
        <span class="fact">900 Tiles</span>
        <span class="fact">real Neighbor lists</span>
        <span class="fact">16 scene palettes</span>
        <span class="fact">fixed 60 Hz</span>
      </div>
    </header>

    <div class="workspace">
      <section class="stage" aria-label="Animated Penrose Wall simulation">
        <div class="canvas-wrap">
          <canvas id="wall"></canvas>
          <div class="live-badge" id="liveBadge">Running · fixed 60 Hz</div>
        </div>
        <div class="state-bar" aria-live="polite">
          <div class="state-item">
            <div class="state-label">Roll variant</div>
            <div class="state-value" id="variantState">Palette single</div>
          </div>
          <div class="state-item">
            <div class="state-label">Palette</div>
            <div class="state-value" id="paletteState">Crossfading</div>
          </div>
          <div class="state-item">
            <div class="state-label">Field</div>
            <div class="state-value" id="fieldState">900/s · fade 0.980</div>
          </div>
          <div class="state-item">
            <div class="state-label">Next Roll</div>
            <div class="state-value" id="rollState">10.0s</div>
          </div>
        </div>
      </section>

      <aside class="controls" aria-label="ColorSparkle audition controls">
        <div class="actions">
          <button class="primary" id="rollButton" type="button">Roll now</button>
          <button id="pauseButton" type="button">Pause</button>
          <button id="resetButton" type="button">Reset to proposal</button>
        </div>

        <section class="control-group" aria-labelledby="colorHeading">
          <h2 class="control-title" id="colorHeading">Color</h2>
          <label class="field" for="paletteSelect">
            <span class="field-head"><span class="field-name">Palette target</span><span class="field-value">3.0s crossfade</span></span>
            <select id="paletteSelect"></select>
          </label>
          <canvas class="palette-strip" id="paletteStrip" aria-label="Live conditioned palette swatch strip"></canvas>
          <div class="strip-caption">
            <span>Live conditioned palette</span>
            <span><i class="floor-chip" id="floorChip"></i><span id="floorLabel">floor #000000</span></span>
          </div>
          <label class="switch-row">
            <span class="switch-copy"><span class="switch-name">Auto-advance palettes</span><span class="switch-detail">Sequential · 3-second AnimPalette crossfade</span></span>
            <input id="autoAdvance" type="checkbox" checked>
          </label>
          <label class="field" for="variantLock">
            <span class="field-head"><span class="field-name">Variant lock</span><span class="field-value">Roll policy</span></span>
            <select id="variantLock">
              <option value="auto">Auto</option>
              <option value="single">Force single</option>
              <option value="scatter">Force scatter</option>
              <option value="confetti">Force confetti</option>
            </select>
          </label>
          <label class="field" for="confettiChance">
            <span class="field-head"><span class="field-name">Confetti chance</span><output class="field-value" id="confettiChanceValue"></output></span>
            <input id="confettiChance" type="range" min="0" max="1" step="0.01">
          </label>
          <div class="two-up">
            <label class="field" for="coordinateMin">
              <span class="field-head"><span class="field-name">Coordinate min</span><output class="field-value" id="coordinateMinValue"></output></span>
              <input id="coordinateMin" type="range" min="0" max="1" step="0.01">
            </label>
            <label class="field" for="coordinateMax">
              <span class="field-head"><span class="field-name">Coordinate max</span><output class="field-value" id="coordinateMaxValue"></output></span>
              <input id="coordinateMax" type="range" min="0" max="1" step="0.01">
            </label>
          </div>
          <div class="two-up">
            <label class="field" for="hueMin">
              <span class="field-head"><span class="field-name">Per-sparkle hue min</span><output class="field-value" id="hueMinValue"></output></span>
              <input id="hueMin" type="range" min="0" max="1" step="0.01">
            </label>
            <label class="field" for="hueMax">
              <span class="field-head"><span class="field-name">Per-sparkle hue max</span><output class="field-value" id="hueMaxValue"></output></span>
              <input id="hueMax" type="range" min="0" max="1" step="0.01">
            </label>
          </div>
        </section>

        <section class="control-group" aria-labelledby="sparkleHeading">
          <h2 class="control-title" id="sparkleHeading">Sparkle</h2>
          <label class="field" for="sparklesPerSecond">
            <span class="field-head"><span class="field-name">Sparkles per second</span><output class="field-value" id="sparklesPerSecondValue"></output></span>
            <input id="sparklesPerSecond" type="range" min="0" max="3600" step="30">
          </label>
          <p class="hint">The fixed-step cadence spreads births uniformly across 60 Buffer frames.</p>
        </section>

        <section class="control-group" aria-labelledby="glintsHeading">
          <h2 class="control-title" id="glintsHeading">Glints</h2>
          <label class="field" for="glintChance">
            <span class="field-head"><span class="field-name">Glint chance</span><output class="field-value" id="glintChanceValue"></output></span>
            <input id="glintChance" type="range" min="0" max="0.05" step="0.0005">
          </label>
          <label class="field" for="darkGlintThreshold">
            <span class="field-head"><span class="field-name">Dark glint threshold</span><output class="field-value" id="darkGlintThresholdValue"></output></span>
            <input id="darkGlintThreshold" type="range" min="0" max="1" step="0.01">
          </label>
          <label class="field" for="bloomFraction">
            <span class="field-head"><span class="field-name">Bright bloom fraction</span><output class="field-value" id="bloomFractionValue"></output></span>
            <input id="bloomFraction" type="range" min="0" max="1" step="0.01">
          </label>
          <label class="field" for="darkBloomLuminance">
            <span class="field-head"><span class="field-name">Dark bloom luminance</span><output class="field-value" id="darkBloomLuminanceValue"></output></span>
            <input id="darkBloomLuminance" type="range" min="0" max="1" step="0.01">
          </label>
        </section>

        <section class="control-group" aria-labelledby="fieldHeading">
          <h2 class="control-title" id="fieldHeading">Field</h2>
          <label class="field" for="fadePerFrame">
            <span class="field-head"><span class="field-name">Fade per Buffer frame</span><output class="field-value" id="fadePerFrameValue"></output></span>
            <input id="fadePerFrame" type="range" min="0.90" max="1" step="0.001">
          </label>
          <label class="field" for="floorLevel">
            <span class="field-head"><span class="field-name">Floor level</span><output class="field-value" id="floorLevelValue"></output></span>
            <input id="floorLevel" type="range" min="0" max="1" step="0.01">
          </label>
          <label class="switch-row">
            <span class="switch-copy"><span class="switch-name">Auto re-Roll</span><span class="switch-detail">Activation variety without clicks</span></span>
            <input id="autoReroll" type="checkbox" checked>
          </label>
          <label class="field" for="autoRerollSeconds">
            <span class="field-head"><span class="field-name">Re-Roll interval</span><output class="field-value" id="autoRerollSecondsValue"></output></span>
            <input id="autoRerollSeconds" type="range" min="2" max="30" step="0.5">
          </label>
        </section>

        <section class="control-group" aria-labelledby="conditioningHeading">
          <h2 class="control-title" id="conditioningHeading">Conditioning</h2>
          <label class="field" for="targetLuminance">
            <span class="field-head"><span class="field-name">Target luminance</span><output class="field-value" id="targetLuminanceValue"></output></span>
            <input id="targetLuminance" type="range" min="0.05" max="0.9" step="0.01">
          </label>
          <label class="field" for="minimumLuminance">
            <span class="field-head"><span class="field-name">Minimum luminance</span><output class="field-value" id="minimumLuminanceValue"></output></span>
            <input id="minimumLuminance" type="range" min="0" max="0.5" step="0.01">
          </label>
          <label class="field" for="luminanceEqualization">
            <span class="field-head"><span class="field-name">Luminance equalization</span><output class="field-value" id="luminanceEqualizationValue"></output></span>
            <input id="luminanceEqualization" type="range" min="0" max="1" step="0.01">
          </label>
          <label class="field" for="hueSpreadReference">
            <span class="field-head"><span class="field-name">Hue spread reference</span><output class="field-value" id="hueSpreadReferenceValue"></output></span>
            <input id="hueSpreadReference" type="range" min="0.05" max="1" step="0.01">
          </label>
          <label class="field" for="maximumLuminanceScale">
            <span class="field-head"><span class="field-name">Maximum luminance scale</span><output class="field-value" id="maximumLuminanceScaleValue"></output></span>
            <input id="maximumLuminanceScale" type="range" min="1" max="8" step="0.1">
          </label>
          <label class="field" for="darkLuminanceThreshold">
            <span class="field-head"><span class="field-name">Dark luminance threshold</span><output class="field-value" id="darkLuminanceThresholdValue"></output></span>
            <input id="darkLuminanceThreshold" type="range" min="0.001" max="0.25" step="0.001">
          </label>
          <label class="field" for="duplicateThreshold">
            <span class="field-head"><span class="field-name">Duplicate threshold</span><output class="field-value" id="duplicateThresholdValue"></output></span>
            <input id="duplicateThreshold" type="range" min="0" max="0.5" step="0.01">
          </label>
          <label class="field" for="hueRedistribution">
            <span class="field-head"><span class="field-name">Hue redistribution</span><output class="field-value" id="hueRedistributionValue"></output></span>
            <input id="hueRedistribution" type="range" min="0" max="1" step="0.01">
          </label>
        </section>

        <details class="secondary">
          <summary>Current shipped algorithm</summary>
          <label class="switch-row">
            <span class="switch-copy"><span class="switch-name">Use shipped Standalone look</span><span class="switch-detail">Full-value HSV · 50/50 activation Roll · no palette or glints</span></span>
            <input id="comparisonMode" type="checkbox">
          </label>
        </details>
      </aside>
    </div>
  </main>

  <script>
    const DATA = __PAYLOAD__;
    const DEFAULTS = __DEFAULTS__;
    const CONDITIONING_DEFAULTS = __CONDITIONING_DEFAULTS__;

    const ColorSparkleModel = (() => {
      const TILE_COUNT = 900;
      const FIXED_STEP = 1 / 60;
      const CURRENT_FADE = 0.98;
      const CURRENT_SPARKLES_PER_SECOND = 900;
      const CURRENT_RANDOM_THRESHOLD = 0.5;
      const CURRENT_WAVEFORM_FALLBACK = 1;
      const CURRENT_HUE_WRAP = 0.15;
      const PALETTE_HOLD_SECONDS = 7;

      const clamp01 = value => Math.max(0, Math.min(1, value));
      const wrap = value => value - Math.floor(value);
      const lerp = (start, end, amount) => start + ((end - start) * clamp01(amount));
      const lerpColor = (start, end, amount) => [
        lerp(start[0], end[0], amount),
        lerp(start[1], end[1], amount),
        lerp(start[2], end[2], amount)
      ];
      const relativeLuminance = color =>
        (0.2126 * color[0]) + (0.7152 * color[1]) + (0.0722 * color[2]);

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

      function rgbToHsv(color) {
        const maximum = Math.max(color[0], color[1], color[2]);
        const minimum = Math.min(color[0], color[1], color[2]);
        const delta = maximum - minimum;
        let hue = 0;
        if (delta !== 0) {
          if (maximum === color[0]) hue = ((color[1] - color[2]) / delta) % 6;
          else if (maximum === color[1]) hue = ((color[2] - color[0]) / delta) + 2;
          else hue = ((color[0] - color[1]) / delta) + 4;
          hue = wrap(hue / 6);
        }
        const saturation = maximum === 0 ? 0 : delta / maximum;
        return [hue, saturation, maximum];
      }

      function lerpHue(start, end, amount) {
        const delta = wrap(end - start + 0.5) - 0.5;
        return wrap(start + (delta * amount));
      }

      function paletteDistance(a, b) {
        const hsvA = rgbToHsv(a);
        const hsvB = rgbToHsv(b);
        let hueDistance = Math.abs(hsvA[0] - hsvB[0]);
        hueDistance = Math.min(hueDistance, 1 - hueDistance) * 2;
        hueDistance *= Math.min(hsvA[1], hsvB[1]);
        const saturationDistance = Math.abs(hsvA[1] - hsvB[1]);
        const luminanceDistance = Math.abs(relativeLuminance(a) - relativeLuminance(b));
        return Math.sqrt(
          (hueDistance * hueDistance) +
          (0.25 * saturationDistance * saturationDistance) +
          (luminanceDistance * luminanceDistance)
        );
      }

      function tryReadHueDonor(color, darkThreshold) {
        const hsv = rgbToHsv(color);
        return {
          usable: relativeLuminance(color) > darkThreshold && hsv[1] > 0.05,
          hue: hsv[0],
          saturation: hsv[1]
        };
      }

      function borrowNeighborHue(colors, sourceIndex, darkThreshold) {
        let previous = null;
        let next = null;
        for (let distance = 1; distance < colors.length && (!previous || !next); distance++) {
          if (!previous) {
            const donor = tryReadHueDonor(
              colors[(sourceIndex - distance + colors.length) % colors.length],
              darkThreshold
            );
            if (donor.usable) previous = { ...donor, distance };
          }
          if (!next) {
            const donor = tryReadHueDonor(
              colors[(sourceIndex + distance) % colors.length],
              darkThreshold
            );
            if (donor.usable) next = { ...donor, distance };
          }
        }
        if (previous && next) {
          const amount = previous.distance / (previous.distance + next.distance);
          return [
            lerpHue(previous.hue, next.hue, amount),
            lerp(previous.saturation, next.saturation, amount)
          ];
        }
        if (previous) return [previous.hue, previous.saturation];
        if (next) return [next.hue, next.saturation];
        return [0, 1];
      }

      function repairDarkColor(colors, sourceIndex, conditioning) {
        const source = colors[sourceIndex];
        let [hue, saturation, value] = rgbToHsv(source);
        if (saturation <= 0.05 || value <= 0.0001) {
          [hue, saturation] = borrowNeighborHue(
            colors,
            sourceIndex,
            conditioning.darkLuminanceThreshold
          );
        }
        const vivid = hsvToRgb(hue, saturation, 1);
        const target = Math.max(
          conditioning.targetLuminance,
          conditioning.minimumLuminance
        );
        const scale = Math.min(target / relativeLuminance(vivid), 1);
        return vivid.map(channel => channel * scale);
      }

      function equalizeLuminance(source, luminance, equalization, paletteLift, conditioning) {
        let target = lerp(
          luminance * paletteLift,
          conditioning.targetLuminance,
          equalization
        );
        target = Math.max(target, conditioning.minimumLuminance);
        let scale = target / luminance;
        scale = Math.min(scale, Math.max(1, conditioning.maximumLuminanceScale));
        scale = Math.min(scale, 1 / Math.max(source[0], source[1], source[2]));
        return source.map(channel => channel * scale);
      }

      function collapseNearDuplicates(colors, duplicateThreshold) {
        const threshold = Math.max(0, duplicateThreshold);
        const anchors = [colors[0]];
        for (const color of colors.slice(1)) {
          if (paletteDistance(anchors[anchors.length - 1], color) >= threshold) {
            anchors.push(color);
          }
        }
        if (
          anchors.length > 1 &&
          paletteDistance(anchors[anchors.length - 1], anchors[0]) < threshold
        ) {
          anchors.pop();
        }
        return anchors;
      }

      function redistribute(anchors, outputLength, hueRedistribution) {
        if (anchors.length === 1) {
          return Array.from({ length: outputLength }, () => [...anchors[0]]);
        }
        const rawDistances = anchors.map((anchor, index) =>
          paletteDistance(anchor, anchors[(index + 1) % anchors.length])
        );
        const averageDistance = rawDistances.reduce((sum, distance) => sum + distance, 0) /
          anchors.length;
        const redistribution = clamp01(hueRedistribution);
        const segmentLengths = rawDistances.map(distance =>
          lerp(1, averageDistance > 0 ? distance / averageDistance : 1, redistribution)
        );
        const pathLength = segmentLengths.reduce((sum, length) => sum + length, 0);
        const output = [];
        let segment = 0;
        let segmentStart = 0;
        let segmentEnd = segmentLengths[0];
        for (let index = 0; index < outputLength; index++) {
          const target = (index / outputLength) * pathLength;
          while (segment < anchors.length - 1 && target > segmentEnd) {
            segmentStart = segmentEnd;
            segment++;
            segmentEnd += segmentLengths[segment];
          }
          const segmentLength = segmentLengths[segment];
          const amount = segmentLength > 0 ? (target - segmentStart) / segmentLength : 0;
          output.push(lerpColor(
            anchors[segment],
            anchors[(segment + 1) % anchors.length],
            amount
          ));
        }
        return output;
      }

      function conditionPalette(colors, conditioning) {
        const luminances = colors.map(relativeLuminance);
        let hueX = 0;
        let hueY = 0;
        let saturationTotal = 0;
        for (const color of colors) {
          const [hue, saturation] = rgbToHsv(color);
          const radians = hue * 2 * Math.PI;
          hueX += saturation * Math.cos(radians);
          hueY += saturation * Math.sin(radians);
          saturationTotal += saturation;
        }
        const meanLuminance = luminances.reduce((sum, value) => sum + value, 0) /
          colors.length;
        const hueSpread = saturationTotal > 0
          ? 1 - (Math.sqrt((hueX * hueX) + (hueY * hueY)) / saturationTotal)
          : 0;
        const equalization = clamp01(conditioning.luminanceEqualization) *
          clamp01(hueSpread / Math.max(0.001, conditioning.hueSpreadReference));
        const paletteLift = meanLuminance > 0
          ? Math.min(
              conditioning.targetLuminance / meanLuminance,
              Math.max(1, conditioning.maximumLuminanceScale)
            )
          : 1;

        const balanced = colors.map((color, index) => {
          const luminance = luminances[index];
          return luminance <= conditioning.darkLuminanceThreshold
            ? repairDarkColor(colors, index, conditioning)
            : equalizeLuminance(color, luminance, equalization, paletteLift, conditioning);
        });
        return redistribute(
          collapseNearDuplicates(balanced, conditioning.duplicateThreshold),
          colors.length,
          conditioning.hueRedistribution
        );
      }

      function readCyclic(position, palette) {
        if (position < 0 || position >= 1) position = wrap(position);
        if (position <= 0 || palette.length === 1) return palette[0];
        const scaled = position * palette.length;
        const first = Math.floor(scaled);
        const second = (first + 1) % palette.length;
        return lerpColor(palette[first], palette[second], scaled % 1);
      }

      function liftToLuminance(color, target) {
        const [hue, saturation] = rgbToHsv(color);
        const vivid = hsvToRgb(hue, saturation, 1);
        const vividLuminance = relativeLuminance(vivid);
        if (target <= vividLuminance) {
          const scale = target / vividLuminance;
          return vivid.map(channel => channel * scale);
        }
        const liftedSaturation = saturation *
          ((1 - target) / (1 - vividLuminance));
        return hsvToRgb(hue, liftedSaturation, 1);
      }

      function create(data, settings) {
        const paletteTweenSteps = Math.round(data.transitionTime / FIXED_STEP);
        const paletteHoldSteps = Math.round(PALETTE_HOLD_SECONDS / FIXED_STEP);
        const buffer = new Float32Array(TILE_COUNT * 3);
        let conditionedPalettes = [];
        let currentPaletteIndex = 0;
        let nextPaletteIndex = data.palettes.length > 1 ? 1 : 0;
        let paletteTweenStepsRemaining = paletteTweenSteps;
        let paletteHoldStepsRemaining = paletteHoldSteps;
        let sparkleCarry = 0;
        let variant = "single";
        let singleCoordinate = 0;
        let currentRandomColor = false;
        let currentSingleHue = 0;
        let rerollRemaining = settings.autoRerollSeconds;
        let lastFloor = [0, 0, 0];

        function refreshConditioning() {
          conditionedPalettes = data.palettes.map(palette =>
            conditionPalette(palette.colors, settings.conditioning)
          );
        }

        function paletteProgress() {
          return paletteTweenStepsRemaining > 0
            ? 1 - (paletteTweenStepsRemaining / paletteTweenSteps)
            : 1;
        }

        function readAnimatedPalette(position) {
          const current = readCyclic(position, conditionedPalettes[currentPaletteIndex]);
          if (paletteTweenStepsRemaining <= 0) return current;
          const next = readCyclic(position, conditionedPalettes[nextPaletteIndex]);
          return lerpColor(current, next, paletteProgress());
        }

        function animatedEntries() {
          const current = conditionedPalettes[currentPaletteIndex];
          if (paletteTweenStepsRemaining <= 0) return current;
          const next = conditionedPalettes[nextPaletteIndex];
          const progress = paletteProgress();
          return current.map((color, index) => lerpColor(color, next[index], progress));
        }

        function floorColor() {
          const entries = animatedEntries();
          let darkest = entries[0];
          let darkestLuminance = relativeLuminance(darkest);
          for (const color of entries.slice(1)) {
            const luminance = relativeLuminance(color);
            if (luminance < darkestLuminance) {
              darkest = color;
              darkestLuminance = luminance;
            }
          }
          lastFloor = darkest.map(channel => channel * settings.floorLevel);
          return lastFloor;
        }

        function beginPaletteChange(index) {
          if (index === currentPaletteIndex && paletteTweenStepsRemaining <= 0) return;
          if (paletteTweenStepsRemaining > 0) currentPaletteIndex = nextPaletteIndex;
          nextPaletteIndex = index;
          paletteTweenStepsRemaining = paletteTweenSteps;
          paletteHoldStepsRemaining = paletteHoldSteps;
        }

        function advancePalette() {
          beginPaletteChange((currentPaletteIndex + 1) % conditionedPalettes.length);
        }

        function stepPalette() {
          if (paletteTweenStepsRemaining > 0) {
            paletteTweenStepsRemaining--;
            if (paletteTweenStepsRemaining === 0) {
              currentPaletteIndex = nextPaletteIndex;
              paletteHoldStepsRemaining = paletteHoldSteps;
            }
            return;
          }
          if (!settings.autoAdvance) return;
          paletteHoldStepsRemaining--;
          if (paletteHoldStepsRemaining === 0) advancePalette();
        }

        function proposalRoll() {
          if (settings.variantLock !== "auto") {
            variant = settings.variantLock;
          } else if (Math.random() < settings.confettiChance) {
            variant = "confetti";
          } else {
            variant = Math.random() < 0.5 ? "single" : "scatter";
          }
          singleCoordinate = lerp(
            settings.coordinateRange[0],
            settings.coordinateRange[1],
            Math.random()
          );
        }

        function currentRoll() {
          currentRandomColor = Math.random() > CURRENT_RANDOM_THRESHOLD;
          const activationHue = Math.random();
          currentSingleHue = (activationHue + CURRENT_WAVEFORM_FALLBACK) % CURRENT_HUE_WRAP;
        }

        function roll() {
          proposalRoll();
          currentRoll();
          rerollRemaining = settings.autoRerollSeconds;
          clearBuffer();
          if (settings.comparisonMode) lastFloor = [0, 0, 0];
          else floorColor();
        }

        function writeTile(tileIndex, color) {
          const offset = tileIndex * 3;
          buffer[offset] = color[0];
          buffer[offset + 1] = color[1];
          buffer[offset + 2] = color[2];
        }

        function sparkleColor() {
          if (variant === "confetti") {
            return hsvToRgb(lerp(
              settings.perSparkleHue[0],
              settings.perSparkleHue[1],
              Math.random()
            ), 1, 1);
          }
          const coordinate = variant === "single"
            ? singleCoordinate
            : lerp(settings.coordinateRange[0], settings.coordinateRange[1], Math.random());
          return readAnimatedPalette(coordinate);
        }

        function spawnProposalSparkle() {
          const tileIndex = Math.floor(Math.random() * TILE_COUNT);
          const color = sparkleColor();
          if (Math.random() >= settings.glintChance) {
            writeTile(tileIndex, color);
            return;
          }

          const luminance = relativeLuminance(color);
          const bloom = luminance >= settings.darkGlintThreshold
            ? color.map(channel => channel * settings.bloomFraction)
            : liftToLuminance(color, settings.darkBloomLuminance);
          for (const neighborIndex of data.neighbors[tileIndex]) {
            writeTile(neighborIndex, bloom);
          }
          writeTile(tileIndex, color);
        }

        function proposalStep() {
          const floor = floorColor();
          for (let tileIndex = 0; tileIndex < TILE_COUNT; tileIndex++) {
            const offset = tileIndex * 3;
            buffer[offset] = floor[0] + ((buffer[offset] - floor[0]) * settings.fadePerFrame);
            buffer[offset + 1] = floor[1] +
              ((buffer[offset + 1] - floor[1]) * settings.fadePerFrame);
            buffer[offset + 2] = floor[2] +
              ((buffer[offset + 2] - floor[2]) * settings.fadePerFrame);
          }
          sparkleCarry += settings.sparklesPerSecond * FIXED_STEP;
          const count = Math.floor(sparkleCarry);
          sparkleCarry -= count;
          for (let index = 0; index < count; index++) spawnProposalSparkle();
        }

        function currentStep() {
          for (let offset = 0; offset < buffer.length; offset++) {
            buffer[offset] *= CURRENT_FADE;
          }
          const count = Math.trunc(FIXED_STEP * CURRENT_SPARKLES_PER_SECOND);
          for (let index = 0; index < count; index++) {
            const hue = currentRandomColor ? Math.random() : currentSingleHue;
            writeTile(Math.floor(Math.random() * TILE_COUNT), hsvToRgb(hue, 1, 1));
          }
          lastFloor = [0, 0, 0];
        }

        function step() {
          stepPalette();
          if (settings.autoReroll) {
            rerollRemaining -= FIXED_STEP;
            if (rerollRemaining <= 0) roll();
          }
          if (settings.comparisonMode) currentStep();
          else proposalStep();
        }

        function clearBuffer() {
          buffer.fill(0);
          sparkleCarry = 0;
        }

        function reset() {
          refreshConditioning();
          currentPaletteIndex = 0;
          nextPaletteIndex = conditionedPalettes.length > 1 ? 1 : 0;
          paletteTweenStepsRemaining = paletteTweenSteps;
          paletteHoldStepsRemaining = paletteHoldSteps;
          roll();
          floorColor();
        }

        function state() {
          return {
            buffer,
            variant: settings.comparisonMode
              ? (currentRandomColor ? "Current · random HSV" : "Current · single HSV")
              : ({ single: "Palette single", scatter: "Palette scatter", confetti: "HSV confetti" })[variant],
            singleCoordinate,
            rerollRemaining,
            floor: lastFloor,
            palette: {
              currentIndex: currentPaletteIndex,
              nextIndex: nextPaletteIndex,
              transitioning: paletteTweenStepsRemaining > 0,
              progress: paletteProgress()
            }
          };
        }

        refreshConditioning();
        reset();
        return {
          step,
          roll,
          reset,
          clearBuffer,
          refreshConditioning,
          beginPaletteChange,
          readAnimatedPalette,
          state
        };
      }

      return { create, conditionPalette, readCyclic, relativeLuminance };
    })();

    const settings = {
      ...DEFAULTS,
      perSparkleHue: [...DEFAULTS.perSparkleHue],
      coordinateRange: [...DEFAULTS.coordinateRange],
      conditioning: { ...CONDITIONING_DEFAULTS }
    };

    const canvas = document.querySelector("#wall");
    const context = canvas.getContext("2d", { alpha: false });
    const liveBadge = document.querySelector("#liveBadge");
    const variantState = document.querySelector("#variantState");
    const paletteState = document.querySelector("#paletteState");
    const fieldState = document.querySelector("#fieldState");
    const rollState = document.querySelector("#rollState");
    const paletteSelect = document.querySelector("#paletteSelect");
    const paletteStrip = document.querySelector("#paletteStrip");
    const floorChip = document.querySelector("#floorChip");
    const floorLabel = document.querySelector("#floorLabel");
    const variantLock = document.querySelector("#variantLock");
    const autoAdvance = document.querySelector("#autoAdvance");
    const autoReroll = document.querySelector("#autoReroll");
    const comparisonMode = document.querySelector("#comparisonMode");
    const rollButton = document.querySelector("#rollButton");
    const pauseButton = document.querySelector("#pauseButton");
    const resetButton = document.querySelector("#resetButton");

    let running = true;
    let accumulator = 0;
    let lastTime = 0;
    let screenPaths = [];
    const model = ColorSparkleModel.create(DATA, settings);

    for (const [index, palette] of DATA.palettes.entries()) {
      const option = document.createElement("option");
      option.value = String(index);
      option.textContent = palette.name;
      paletteSelect.append(option);
    }

    const worldTiles = [];
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (let tileIndex = 0; tileIndex < 900; tileIndex++) {
      const unique = new Map();
      for (let point = 0; point < 6; point++) {
        const offset = (tileIndex * 12) + (point * 2);
        const x = DATA.mesh[offset];
        const y = DATA.mesh[offset + 1];
        unique.set(`${x},${y}`, [x, y]);
        minX = Math.min(minX, x);
        minY = Math.min(minY, y);
        maxX = Math.max(maxX, x);
        maxY = Math.max(maxY, y);
      }
      if (unique.size !== 4) throw new Error(`Tile ${tileIndex} does not have four rhomb corners`);
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
      canvas.width = Math.max(1, Math.round(rect.width * dpr));
      canvas.height = Math.max(1, Math.round(rect.height * dpr));
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
      drawPaletteStrip();
    }

    function colorCss(color) {
      return `rgb(${Math.round(color[0] * 255)} ${Math.round(color[1] * 255)} ${Math.round(color[2] * 255)})`;
    }

    function colorHex(color) {
      return `#${color.map(channel =>
        Math.round(channel * 255).toString(16).padStart(2, "0")
      ).join("")}`;
    }

    function drawWall(buffer) {
      const rect = canvas.getBoundingClientRect();
      context.fillStyle = "#010205";
      context.fillRect(0, 0, rect.width, rect.height);
      for (let tileIndex = 0; tileIndex < 900; tileIndex++) {
        const offset = tileIndex * 3;
        context.fillStyle = `rgb(${Math.round(buffer[offset] * 255)} ${Math.round(buffer[offset + 1] * 255)} ${Math.round(buffer[offset + 2] * 255)})`;
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
      for (let x = 0; x < Math.ceil(rect.width); x++) {
        strip.fillStyle = colorCss(model.readAnimatedPalette(x / Math.max(1, rect.width)));
        strip.fillRect(x, 0, 1, rect.height);
      }
    }

    function updateState() {
      const state = model.state();
      variantState.textContent = state.variant +
        (!settings.comparisonMode && state.variant === "Palette single"
          ? ` · ${state.singleCoordinate.toFixed(3)}`
          : "");
      const currentName = DATA.palettes[state.palette.currentIndex].name;
      const nextName = DATA.palettes[state.palette.nextIndex].name;
      paletteState.textContent = state.palette.transitioning
        ? `${currentName} → ${nextName} · ${Math.round(state.palette.progress * 100)}%`
        : currentName;
      fieldState.textContent = settings.comparisonMode
        ? "Current · 900/s · fade 0.980"
        : `${Math.round(settings.sparklesPerSecond)}/s · fade ${settings.fadePerFrame.toFixed(3)}`;
      rollState.textContent = settings.autoReroll
        ? `${Math.max(0, state.rerollRemaining).toFixed(1)}s`
        : "Manual";
      const floorHex = colorHex(state.floor);
      floorChip.style.background = floorHex;
      floorLabel.textContent = `floor ${floorHex}`;
      liveBadge.textContent = running ? "Running · fixed 60 Hz" : "Paused";
      liveBadge.classList.toggle("paused", !running);
      pauseButton.textContent = running ? "Pause" : "Resume";
      paletteSelect.value = String(state.palette.transitioning
        ? state.palette.nextIndex
        : state.palette.currentIndex);
      drawPaletteStrip();
    }

    const sliderBindings = [
      ["fadePerFrame", () => settings.fadePerFrame, value => { settings.fadePerFrame = value; }, value => value.toFixed(3)],
      ["floorLevel", () => settings.floorLevel, value => { settings.floorLevel = value; }, value => value.toFixed(2)],
      ["sparklesPerSecond", () => settings.sparklesPerSecond, value => { settings.sparklesPerSecond = value; }, value => Math.round(value).toString()],
      ["confettiChance", () => settings.confettiChance, value => { settings.confettiChance = value; }, value => value.toFixed(2)],
      ["coordinateMin", () => settings.coordinateRange[0], value => { settings.coordinateRange[0] = value; }, value => value.toFixed(2)],
      ["coordinateMax", () => settings.coordinateRange[1], value => { settings.coordinateRange[1] = value; }, value => value.toFixed(2)],
      ["hueMin", () => settings.perSparkleHue[0], value => { settings.perSparkleHue[0] = value; }, value => value.toFixed(2)],
      ["hueMax", () => settings.perSparkleHue[1], value => { settings.perSparkleHue[1] = value; }, value => value.toFixed(2)],
      ["glintChance", () => settings.glintChance, value => { settings.glintChance = value; }, value => value.toFixed(4)],
      ["darkGlintThreshold", () => settings.darkGlintThreshold, value => { settings.darkGlintThreshold = value; }, value => value.toFixed(2)],
      ["bloomFraction", () => settings.bloomFraction, value => { settings.bloomFraction = value; }, value => value.toFixed(2)],
      ["darkBloomLuminance", () => settings.darkBloomLuminance, value => { settings.darkBloomLuminance = value; }, value => value.toFixed(2)],
      ["autoRerollSeconds", () => settings.autoRerollSeconds, value => { settings.autoRerollSeconds = value; }, value => `${value.toFixed(1)}s`],
      ["targetLuminance", () => settings.conditioning.targetLuminance, value => { settings.conditioning.targetLuminance = value; }, value => value.toFixed(2)],
      ["minimumLuminance", () => settings.conditioning.minimumLuminance, value => { settings.conditioning.minimumLuminance = value; }, value => value.toFixed(2)],
      ["luminanceEqualization", () => settings.conditioning.luminanceEqualization, value => { settings.conditioning.luminanceEqualization = value; }, value => value.toFixed(2)],
      ["hueSpreadReference", () => settings.conditioning.hueSpreadReference, value => { settings.conditioning.hueSpreadReference = value; }, value => value.toFixed(2)],
      ["maximumLuminanceScale", () => settings.conditioning.maximumLuminanceScale, value => { settings.conditioning.maximumLuminanceScale = value; }, value => value.toFixed(1)],
      ["darkLuminanceThreshold", () => settings.conditioning.darkLuminanceThreshold, value => { settings.conditioning.darkLuminanceThreshold = value; }, value => value.toFixed(3)],
      ["duplicateThreshold", () => settings.conditioning.duplicateThreshold, value => { settings.conditioning.duplicateThreshold = value; }, value => value.toFixed(2)],
      ["hueRedistribution", () => settings.conditioning.hueRedistribution, value => { settings.conditioning.hueRedistribution = value; }, value => value.toFixed(2)]
    ];

    const conditioningIds = new Set([
      "targetLuminance", "minimumLuminance", "luminanceEqualization",
      "hueSpreadReference", "maximumLuminanceScale", "darkLuminanceThreshold",
      "duplicateThreshold", "hueRedistribution"
    ]);

    function syncSlidersFromSettings() {
      for (const [id, read, , format] of sliderBindings) {
        const value = read();
        document.querySelector(`#${id}`).value = String(value);
        document.querySelector(`#${id}Value`).textContent = format(value);
      }
    }

    for (const [id, , write, format] of sliderBindings) {
      const input = document.querySelector(`#${id}`);
      input.addEventListener("input", () => {
        const value = Number(input.value);
        write(value);
        document.querySelector(`#${id}Value`).textContent = format(value);
        if (conditioningIds.has(id)) model.refreshConditioning();
        updateState();
      });
    }

    paletteSelect.addEventListener("change", () => {
      model.beginPaletteChange(Number(paletteSelect.value));
      updateState();
    });

    variantLock.addEventListener("change", () => {
      settings.variantLock = variantLock.value;
      model.roll();
      updateState();
    });

    autoAdvance.addEventListener("change", () => {
      settings.autoAdvance = autoAdvance.checked;
      updateState();
    });

    autoReroll.addEventListener("change", () => {
      settings.autoReroll = autoReroll.checked;
      updateState();
    });

    comparisonMode.addEventListener("change", () => {
      settings.comparisonMode = comparisonMode.checked;
      model.roll();
      updateState();
    });

    rollButton.addEventListener("click", () => {
      model.roll();
      updateState();
    });

    pauseButton.addEventListener("click", () => {
      running = !running;
      updateState();
    });

    function resetSettings() {
      Object.assign(settings, DEFAULTS);
      settings.perSparkleHue = [...DEFAULTS.perSparkleHue];
      settings.coordinateRange = [...DEFAULTS.coordinateRange];
      settings.conditioning = { ...CONDITIONING_DEFAULTS };
      variantLock.value = settings.variantLock;
      autoAdvance.checked = settings.autoAdvance;
      autoReroll.checked = settings.autoReroll;
      comparisonMode.checked = settings.comparisonMode;
      running = true;
      syncSlidersFromSettings();
      model.reset();
      updateState();
    }

    resetButton.addEventListener("click", resetSettings);

    new ResizeObserver(resizeWall).observe(canvas);
    resetSettings();
    resizeWall();

    function tick(now) {
      if (!lastTime) lastTime = now;
      const elapsed = Math.min((now - lastTime) / 1000, 0.25);
      lastTime = now;
      if (running) {
        accumulator += elapsed;
        while (accumulator >= 1 / 60) {
          model.step();
          accumulator -= 1 / 60;
        }
      }
      const state = model.state();
      drawWall(state.buffer);
      updateState();
      requestAnimationFrame(tick);
    }

    requestAnimationFrame(tick);
  </script>
</body>
</html>
'''


def verify_cold_load(html: str) -> None:
    """Checks the actual baked DEFAULTS object and zero-click boot wiring."""
    assert DEFAULTS == BRIEF_DEFAULTS, "Python DEFAULTS drifted from the redesign brief"
    match = re.search(r"const DEFAULTS = (\{.*?\});", html, flags=re.S)
    assert match is not None, "baked DEFAULTS object was not found"
    assert json.loads(match.group(1)) == BRIEF_DEFAULTS, (
        "baked DEFAULTS object drifted from the redesign brief"
    )
    required_boot_wiring = (
        "let running = true;",
        "const model = ColorSparkleModel.create(DATA, settings);",
        "resetSettings();",
        "requestAnimationFrame(tick);",
        "settings.comparisonMode = comparisonMode.checked;",
        "settings.autoAdvance = autoAdvance.checked;",
        "settings.autoReroll = autoReroll.checked;",
    )
    for token in required_boot_wiring:
        assert token in html, f"cold-load wiring missing: {token}"
    assert "<script src=" not in html
    assert "<link rel=\"stylesheet\"" not in html


def main() -> None:
    """Builds and verifies the self-contained desk audition page."""
    mesh, neighbors = load_layout()
    palettes = load_scene_palettes()
    payload = json.dumps(
        {
            "mesh": mesh,
            "neighbors": neighbors,
            "palettes": palettes,
            "transitionTime": ANIM_PALETTE_TRANSITION_TIME,
        },
        separators=(",", ":"),
        allow_nan=False,
    )
    html = (
        HTML.replace("__PAYLOAD__", payload)
        .replace("__DEFAULTS__", json.dumps(DEFAULTS, separators=(",", ":")))
        .replace(
            "__CONDITIONING_DEFAULTS__",
            json.dumps(CONDITIONING_DEFAULTS, separators=(",", ":")),
        )
    )
    verify_cold_load(html)

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(html, encoding="utf-8")
    verify_cold_load(OUTPUT_PATH.read_text(encoding="utf-8"))
    neighbor_links = sum(len(indices) for indices in neighbors)
    print(
        f"wrote {OUTPUT_PATH} "
        f"({EXPECTED_TILES} Tiles, {neighbor_links} directed Neighbor links, "
        f"{len(palettes)} palettes, cold-load DEFAULTS verified)"
    )


if __name__ == "__main__":
    main()
