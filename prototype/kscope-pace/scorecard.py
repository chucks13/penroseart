# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy", "pillow"]
# ///
# PROTOTYPE — throwaway (see README.md). Ranks every Kscope pool image by
# definition loss under the exact Fill gray mapping, both mirror layouts.
import json
from pathlib import Path

import numpy as np

from render_wall import (
    BEAT_S,
    FPS,
    PALETTE_VIEW_INDEX,
    adjust_image,
    adjustment_label,
    apply_gray_mapping,
    candidate_adjustments,
    init_weights,
    load_rgb,
    load_scene_palettes,
    motion_state,
    neighbor_edges,
    parse_layout,
    pool_entries,
    prepare_source,
    rgb_to_hsv,
    seed_map,
    tile_geometry,
    wall_buffers,
)

HERE = Path(__file__).resolve().parent
OUT = HERE / "out"
SCORE_BEATS = 4
VISIBLE_EDGE_DELTA = 0.08
COLLAPSED_EDGE_DELTA = 0.03
ORDERABLE_LIGHTNESS_DELTA = 0.03
GRAY_BIN_WIDTH = 0.05
CANDIDATE_REPORTING_GAIN = 5.0


def srgb_to_oklab(rgb: np.ndarray) -> np.ndarray:
    """Convert sRGB code values to OKLab for perceptual boundary comparisons."""
    rgb = np.asarray(rgb, dtype=np.float64)
    linear = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    red, green, blue = np.moveaxis(linear, -1, 0)
    l_value = 0.4122214708 * red + 0.5363325363 * green + 0.0514459929 * blue
    m_value = 0.2119034982 * red + 0.6806995451 * green + 0.1073969566 * blue
    s_value = 0.0883024619 * red + 0.2817188376 * green + 0.6299787005 * blue
    l_root = np.cbrt(l_value)
    m_root = np.cbrt(m_value)
    s_root = np.cbrt(s_value)
    return np.stack(
        [
            0.2104542553 * l_root + 0.7936177850 * m_root - 0.0040720468 * s_root,
            1.9779984951 * l_root - 2.4285922050 * m_root + 0.4505937099 * s_root,
            0.0259040371 * l_root + 0.7827717662 * m_root - 0.8086757660 * s_root,
        ],
        axis=-1,
    )


def _blank_counts() -> dict[str, float]:
    """Create aggregation counters for one gray mapping."""
    return {
        "visible": 0,
        "collapsed": 0,
        "pre_contrast": 0.0,
        "post_contrast": 0.0,
        "orderable": 0,
        "inverted": 0,
        "wrap": 0,
        "groups": 0,
        "gray_bins": 0.0,
        "frames": 0,
    }


def _accumulate(
    counts: dict[str, float],
    normal: np.ndarray,
    mapped: np.ndarray,
    edges: np.ndarray,
    group_seeds: np.ndarray,
) -> None:
    """Accumulate one frame's boundary, ordering, wrap, and gray-bin evidence."""
    normal_lab = srgb_to_oklab(normal)
    mapped_lab = srgb_to_oklab(mapped)
    pre_difference = normal_lab[edges[:, 0]] - normal_lab[edges[:, 1]]
    post_difference = mapped_lab[edges[:, 0]] - mapped_lab[edges[:, 1]]
    pre_delta = np.linalg.norm(pre_difference, axis=1)
    post_delta = np.linalg.norm(post_difference, axis=1)
    visible = pre_delta >= VISIBLE_EDGE_DELTA
    orderable = np.abs(pre_difference[:, 0]) >= ORDERABLE_LIGHTNESS_DELTA

    counts["visible"] += int(visible.sum())
    counts["collapsed"] += int((visible & (post_delta < COLLAPSED_EDGE_DELTA)).sum())
    counts["pre_contrast"] += float(pre_delta[visible].sum())
    counts["post_contrast"] += float(post_delta[visible].sum())
    counts["orderable"] += int(orderable.sum())
    counts["inverted"] += int(
        (orderable & (pre_difference[:, 0] * post_difference[:, 0] < 0.0)).sum()
    )

    group_rgb = normal[group_seeds]
    group_hsv = rgb_to_hsv(group_rgb)
    counts["wrap"] += int((group_hsv.sum(axis=1) >= 1.0).sum())
    counts["groups"] += len(group_seeds)
    gray = mapped[group_seeds, 0]
    counts["gray_bins"] += len(np.unique(np.floor(gray / GRAY_BIN_WIDTH).astype(np.int64)))
    counts["frames"] += 1


def _finish(counts: dict[str, float]) -> dict[str, float | int]:
    """Convert aggregate counters to scorecard values."""
    return {
        "visible_edges": int(counts["visible"]),
        "collapse_pct": round(100.0 * counts["collapsed"] / max(1, counts["visible"]), 2),
        "contrast_retention_pct": round(
            100.0 * counts["post_contrast"] / max(1e-12, counts["pre_contrast"]), 2
        ),
        "inversion_pct": round(100.0 * counts["inverted"] / max(1, counts["orderable"]), 2),
        "wrap_pct": round(100.0 * counts["wrap"] / max(1, counts["groups"]), 2),
        "gray_bins": round(counts["gray_bins"] / max(1, counts["frames"]), 1),
    }


def score_prepared(
    prepared: np.ndarray,
    mode: str,
    nearest_indexes: np.ndarray,
    nearest_weights: np.ndarray,
    seeds: np.ndarray,
    edges: np.ndarray,
    mappings: tuple[str, ...] = ("exact", "value", "luminance"),
) -> dict[str, dict[str, float | int]]:
    """Score one prepared source over four beats of the full wall path."""
    counts = {mapping: _blank_counts() for mapping in mappings}
    group_seeds = np.unique(seeds)
    frame_count = int(SCORE_BEATS * BEAT_S * FPS)
    for frame_index in range(frame_count):
        pos_x, pos_y, angle = motion_state(prepared, mode, frame_index)
        normal, exact = wall_buffers(
            prepared,
            pos_x,
            pos_y,
            angle,
            nearest_indexes,
            nearest_weights,
            seeds,
        )
        for mapping in mappings:
            mapped = exact if mapping == "exact" else apply_gray_mapping(normal, mapping)
            _accumulate(counts[mapping], normal, mapped, edges, group_seeds)
    return {mapping: _finish(mapping_counts) for mapping, mapping_counts in counts.items()}


def _candidate_cost(adjustment: str) -> str:
    """Name the visible or authoring cost of a candidate source-image edit."""
    if adjustment.startswith("hue"):
        return "changes the image's normal color identity"
    if adjustment.startswith("sat"):
        return "flattens the image's normal color saturation"
    if adjustment.startswith("gamma"):
        return "changes normal brightness and contrast"
    if adjustment == "autocontrast":
        return "re-authors the image's full tonal range"
    if adjustment == "soft-shade":
        return "adds new texture structure not present in the source"
    return "none"


def _mean_exact_collapse(
    raw: np.ndarray,
    pool: str,
    adjustment: str,
    palettes: list[tuple[str, np.ndarray]],
    modes: tuple[str, ...],
    nearest_indexes: np.ndarray,
    nearest_weights: np.ndarray,
    seeds_by_mode: dict[str, np.ndarray],
    edges_by_mode: dict[str, np.ndarray],
) -> float:
    """Score a candidate across both layouts and, for mono, every scene palette."""
    adjusted = adjust_image(raw, pool, adjustment)
    palette_set = palettes if pool == "mono" else [palettes[min(PALETTE_VIEW_INDEX, len(palettes) - 1)]]
    values = []
    for _, palette in palette_set:
        prepared = prepare_source(adjusted, pool, palette)
        for mode in modes:
            result = score_prepared(
                prepared,
                mode,
                nearest_indexes,
                nearest_weights,
                seeds_by_mode[mode],
                edges_by_mode[mode],
                mappings=("exact",),
            )
            values.append(result["exact"]["collapse_pct"])
    return float(np.mean(values))


def _rank(values: np.ndarray) -> np.ndarray:
    """Return stable average-free ranks for correlation diagnostics."""
    order = np.argsort(values, kind="stable")
    ranks = np.empty_like(order, dtype=np.float64)
    ranks[order] = np.arange(len(values), dtype=np.float64)
    return ranks


def _correlation(a: list[float], b: list[float]) -> tuple[float, float]:
    """Return Pearson and rank correlation for two per-image measurements."""
    left = np.asarray(a, dtype=np.float64)
    right = np.asarray(b, dtype=np.float64)
    return (
        float(np.corrcoef(left, right)[0, 1]),
        float(np.corrcoef(_rank(left), _rank(right))[0, 1]),
    )


def main() -> None:
    """Measure every image/layout, select pool-edit candidates, and write JSON."""
    OUT.mkdir(exist_ok=True)
    layout = parse_layout()
    centers, _ = tile_geometry(layout["Mesh"])
    nearest_indexes, nearest_weights = init_weights(centers)
    modes = ("mirror2", "mirror10")
    seeds_by_mode = {mode: seed_map(layout["shapes"][mode]) for mode in modes}
    edges_by_mode = {
        mode: neighbor_edges(layout, seeds_by_mode[mode]) for mode in modes
    }
    palettes = load_scene_palettes()
    view_palette_name, view_palette = palettes[min(PALETTE_VIEW_INDEX, len(palettes) - 1)]
    measure_data = json.loads((OUT / "measure.json").read_text())
    character_by_name = {image["name"]: image for image in measure_data["images"]}

    rows = []
    mono_palette_ranges = {}
    image_means = {}
    for entry in pool_entries():
        name, pool = entry["name"], entry["pool"]
        raw = load_rgb(name)
        prepared = prepare_source(raw, pool, view_palette)
        image_rows = []
        for mode in modes:
            scores = score_prepared(
                prepared,
                mode,
                nearest_indexes,
                nearest_weights,
                seeds_by_mode[mode],
                edges_by_mode[mode],
            )
            row = {"name": name, "pool": pool, "layout": mode, **scores}
            rows.append(row)
            image_rows.append(row)
        image_means[name] = float(np.mean([row["exact"]["collapse_pct"] for row in image_rows]))

        if pool == "mono":
            palette_values = []
            for palette_name, palette in palettes:
                palette_prepared = prepare_source(raw, pool, palette)
                for mode in modes:
                    score = score_prepared(
                        palette_prepared,
                        mode,
                        nearest_indexes,
                        nearest_weights,
                        seeds_by_mode[mode],
                        edges_by_mode[mode],
                        mappings=("exact",),
                    )["exact"]["collapse_pct"]
                    palette_values.append({"palette": palette_name, "layout": mode, "collapse_pct": score})
            values = [value["collapse_pct"] for value in palette_values]
            mono_palette_ranges[name] = {
                "min": round(min(values), 2),
                "median": round(float(np.median(values)), 2),
                "max": round(max(values), 2),
                "samples": palette_values,
            }

    image_candidates = {}
    candidate_audit = {}
    for entry in pool_entries():
        name, pool = entry["name"], entry["pool"]
        raw = load_rgb(name)
        scores = {}
        for adjustment in candidate_adjustments(pool):
            scores[adjustment] = _mean_exact_collapse(
                raw,
                pool,
                adjustment,
                palettes,
                modes,
                nearest_indexes,
                nearest_weights,
                seeds_by_mode,
                edges_by_mode,
            )
        best = min(scores, key=scores.get)
        baseline = scores["none"]
        gain = baseline - scores[best]
        candidate_audit[name] = {
            "pool": pool,
            "best_adjustment": best,
            "baseline_collapse_pct": round(baseline, 2),
            "best_collapse_pct": round(scores[best], 2),
            "gain_points": round(gain, 2),
            "tested": {key: round(value, 2) for key, value in scores.items()},
        }
        if best != "none" and gain >= CANDIDATE_REPORTING_GAIN:
            image_candidates[name] = {
                "pool": pool,
                "adjustment": best,
                "baseline_collapse_pct": round(baseline, 2),
                "candidate_collapse_pct": round(scores[best], 2),
                "gain_points": round(gain, 2),
                "cost": _candidate_cost(best),
            }

    names = [entry["name"] for entry in pool_entries()]
    shading = [character_by_name[name]["shading_share_pct"] for name in names]
    hard_share = [100.0 - value for value in shading]
    collapse = [image_means[name] for name in names]
    shading_pearson, shading_rank = _correlation(shading, collapse)
    hard_pearson, hard_rank = _correlation(hard_share, collapse)
    exact_values = [row["exact"]["collapse_pct"] for row in rows]
    value_values = [row["value"]["collapse_pct"] for row in rows]
    luminance_values = [row["luminance"]["collapse_pct"] for row in rows]

    summary = {
        "image_count": len(names),
        "layout_count": len(modes),
        "view_palette": view_palette_name,
        "scene_palette_count": len(palettes),
        "shading_share_vs_exact_collapse_pearson": round(shading_pearson, 3),
        "shading_share_vs_exact_collapse_rank": round(shading_rank, 3),
        "hard_change_share_vs_exact_collapse_pearson": round(hard_pearson, 3),
        "hard_change_share_vs_exact_collapse_rank": round(hard_rank, 3),
        "mean_exact_collapse_pct": round(float(np.mean(exact_values)), 2),
        "mean_value_collapse_pct": round(float(np.mean(value_values)), 2),
        "mean_luminance_collapse_pct": round(float(np.mean(luminance_values)), 2),
    }
    output = {
        "metrics": {
            "visible_edge_oklab_delta": VISIBLE_EDGE_DELTA,
            "collapsed_edge_oklab_delta": COLLAPSED_EDGE_DELTA,
            "orderable_lightness_delta": ORDERABLE_LIGHTNESS_DELTA,
            "gray_bin_width": GRAY_BIN_WIDTH,
            "score_beats": SCORE_BEATS,
        },
        "summary": summary,
        "rows": sorted(rows, key=lambda row: row["exact"]["collapse_pct"], reverse=True),
        "image_candidates": dict(sorted(image_candidates.items())),
        "candidate_audit": candidate_audit,
        "mono_palette_ranges": mono_palette_ranges,
    }
    path = OUT / "fill_scorecard.json"
    path.write_text(json.dumps(output, indent=2))

    print(
        f"pool: {len(names)} images x {len(modes)} layouts | "
        f"mono model: {len(palettes)} real scene palettes; viewer={view_palette_name}"
    )
    header = (
        f"{'image':<16}{'pool':<7}{'layout':<10}{'collapse%':<12}"
        f"{'kept%':<9}{'invert%':<10}{'wrap%':<8}{'bins':<7}"
        f"{'value collapse':<16}luma collapse"
    )
    print(header)
    print("-" * len(header))
    for row in output["rows"]:
        exact = row["exact"]
        print(
            f"{row['name']:<16}{row['pool']:<7}{row['layout']:<10}"
            f"{exact['collapse_pct']:<12.2f}{exact['contrast_retention_pct']:<9.2f}"
            f"{exact['inversion_pct']:<10.2f}{exact['wrap_pct']:<8.2f}"
            f"{exact['gray_bins']:<7.1f}{row['value']['collapse_pct']:<16.2f}"
            f"{row['luminance']['collapse_pct']:.2f}"
        )

    print("\nmechanism controls:")
    print(
        f"  mean collapsed boundaries: exact {summary['mean_exact_collapse_pct']:.2f}% | "
        f"HSV value {summary['mean_value_collapse_pct']:.2f}% | "
        f"Rec.709 luminance {summary['mean_luminance_collapse_pct']:.2f}%"
    )
    print(
        f"  shading share vs exact loss: Pearson {shading_pearson:+.3f}, "
        f"rank {shading_rank:+.3f}"
    )
    print(
        f"  hard-change share vs exact loss: Pearson {hard_pearson:+.3f}, "
        f"rank {hard_rank:+.3f}"
    )

    print("\ncandidate pool edits (reported at >=5 percentage-point collapse reduction):")
    if image_candidates:
        for name, candidate in image_candidates.items():
            print(
                f"  {name:<16} {adjustment_label(candidate['adjustment']):<32} "
                f"{candidate['baseline_collapse_pct']:6.2f}% -> "
                f"{candidate['candidate_collapse_pct']:6.2f}% "
                f"({candidate['gain_points']:+.2f} points); {candidate['cost']}"
            )
    else:
        print("  none of the tested whole-image edits cleared the reporting threshold")

    for name, palette_range in mono_palette_ranges.items():
        print(
            f"mono palette sensitivity {name}: exact collapse "
            f"{palette_range['min']:.2f}..{palette_range['max']:.2f}% "
            f"(median {palette_range['median']:.2f}%)"
        )
    print(f"\nwrote {path}")


if __name__ == "__main__":
    main()
