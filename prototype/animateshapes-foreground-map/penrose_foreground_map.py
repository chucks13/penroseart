# Renders the Penrose Wall layout as an SVG: every tile drawn as its rhombus with
# black edge lines, filled green when the tile belongs to a Circle/Arc group in the
# layout's `loops` Shape List (AnimateShapes' foreground) and red otherwise (background).
#
# Reads Assets/StreamingAssets/penrose_layout.txt (JSON with // comments), the same
# file Controller.cs loads, and decodes the packed shape format WallData.cs defines:
# packed[0] = group count, packed[1..count] = group pointers, packed[ptr] = tile
# count followed by that many tile indices.
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
LAYOUT = REPO / "Assets/StreamingAssets/penrose_layout.txt"
OUT = Path(__file__).resolve().parent / "out" / "penrose-foreground-map.svg"

FOREGROUND_FILL = "#2e9e4f"  # tiles in a loops group (Circle/Arc foreground)
BACKGROUND_FILL = "#c43a3a"  # every other tile (background field)
EDGE = "#000000"


def packed_group_tiles(packed):
    """Yield each group's tile-index list from a WallData packed shape array."""
    count = packed[0]
    for gi in range(count):
        ptr = packed[gi + 1]
        yield packed[ptr + 1 : ptr + 1 + packed[ptr]]


def tile_polygon(mesh, tile_index):
    """Return the tile's rhombus corners from its two mesh triangles.

    The layout Mesh packs triangles sequentially, two per tile (12 values: 2
    triangles x 3 x,y vertices). The triangles share an edge; the 4 unique
    vertices ordered by angle around their centroid form the rhombus outline.
    Falls back to the raw 6 vertices if dedup does not leave 4.
    """
    chunk = mesh[tile_index * 12 : tile_index * 12 + 12]
    points = [(chunk[i], chunk[i + 1]) for i in range(0, 12, 2)]
    unique = list(dict.fromkeys(points))
    if len(unique) != 4:
        return points
    cx = sum(p[0] for p in unique) / 4
    cy = sum(p[1] for p in unique) / 4
    from math import atan2

    unique.sort(key=lambda p: atan2(p[1] - cy, p[0] - cx))
    return unique


def main():
    data = json.loads(re.sub(r"//[^\n]*", "", LAYOUT.read_text()))
    mesh = data["Mesh"]
    tiles = data["tiles"]
    foreground = set()
    for group in packed_group_tiles(data["shapes"]["loops"]):
        foreground.update(group)

    xs = [mesh[i] for i in range(0, len(mesh), 2)]
    ys = [mesh[i] for i in range(1, len(mesh), 2)]
    pad = 40
    min_x, max_x = min(xs) - pad, max(xs) + pad
    min_y, max_y = min(ys) - pad, max(ys) + pad

    polygons = []
    for index in range(len(tiles)):
        corners = tile_polygon(mesh, index)
        fill = FOREGROUND_FILL if index in foreground else BACKGROUND_FILL
        # Flip y so the wall renders the way the simulator shows it (y up).
        pts = " ".join(f"{x},{max_y + min_y - y}" for x, y in corners)
        polygons.append(f'<polygon points="{pts}" fill="{fill}" stroke="{EDGE}" stroke-width="4"/>')

    svg = (
        f'<svg xmlns="http://www.w3.org/2000/svg" '
        f'viewBox="{min_x} {min_y} {max_x - min_x} {max_y - min_y}">\n'
        f'<rect x="{min_x}" y="{min_y}" width="{max_x - min_x}" height="{max_y - min_y}" fill="#111"/>\n'
        + "\n".join(polygons)
        + "\n</svg>\n"
    )
    OUT.write_text(svg)
    print(f"tiles={len(tiles)} foreground={len(foreground)} background={len(tiles) - len(foreground)}")
    print(f"wrote {OUT}")


if __name__ == "__main__":
    sys.exit(main())
