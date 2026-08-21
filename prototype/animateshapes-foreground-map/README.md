# AnimateShapes foreground map

Renders the Penrose Wall layout as an SVG with every tile drawn as its rhombus:
green for tiles that belong to a Circle/Arc group in the layout's `loops` Shape
List (AnimateShapes' foreground), red for every other tile (the background
field), black edge lines between tiles.

It reads `Assets/StreamingAssets/penrose_layout.txt` — the same file
`Controller.cs` loads — and decodes the packed shape format `WallData.cs`
defines, so the picture is ground truth for which tiles AnimateShapes'
foreground groups overwrite: 566 of the 900 tiles are foreground, 334 are
background.

## Run

```bash
python3 penrose_foreground_map.py
```

Stdlib only. Writes `out/penrose-foreground-map.svg` and prints the
foreground/background tile counts.
