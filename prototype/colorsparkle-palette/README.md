# PROTOTYPE: throwaway. Do not ship anything in this folder.

Desk audition for the palette rebuild that informed the approved ColorSparkle
design. `build.py` reads the real Penrose layout and scene palettes, then writes
one self-contained `out/sim.html`. The page opens from `file://` without a server
or network access. Its Neighbor-bloom experiment remains prototype behavior;
the production Effect uses crisp one-Tile glints.

## The question

The audition asked whether ColorSparkle could keep each palette color's
luminance character at sparkle birth, and whether rare Neighbor blooms could
make dark glints legible without turning ordinary dark sparkles into bright
points.

The page models one beat-still Effect. It does not read BeatManager, acquire a
Waveform, or invent Fill, Drop, Energy, or Levels values. A **Tile** is one
logical Penrose rhomb. A **Neighbor** is a Tile that shares one complete edge.
The **Buffer** is the 900-color frame. A **Roll** chooses the activation variant
and its activation-wide palette coordinate.

## Cold-load state

Opening `out/sim.html` with no clicks starts the audition at a fixed 60 Hz. The
first two conditioned scene palettes begin their 3-second crossfade immediately.
Palette auto-advance and automatic re-Roll are on. The variant lock is **Auto**,
and the pre-redesign comparison is off.

The baked `DEFAULTS` object contains the values stated in the brief:

| value | cold load |
| --- | ---: |
| Fade per Buffer frame | `0.98` |
| Floor level | `0` |
| Sparkles per second | `900` |
| Confetti chance | `0.25` |
| Per-sparkle hue | `0..1` |
| Palette coordinate | `0..1` |
| Glint chance | `0.003` |
| Dark glint threshold | `0.2` |
| Bright bloom fraction | `0.7` |
| Dark bloom luminance | `0.6` |
| Automatic re-Roll interval | `10` seconds |
| Variant lock | `auto` |
| Automatic re-Roll | on |
| Palette auto-advance | on |
| Pre-redesign comparison | off |

The brief names the conditioning controls but does not assign their defaults.
The page starts from the Angles Standalone preset already used by
`prototype/animateloops-palette`: target luminance `0.4`, minimum luminance
`0.12`, equalization `0.85`, hue spread reference `0.5`, maximum luminance scale
`4`, dark luminance threshold `0.03`, duplicate threshold `0.08`, and hue
redistribution `1`. The current `PaletteConditioning` struct includes
`HueSpreadReference`, so the page exposes that eighth control even though the
brief's parenthetical list omits it.

`build.py` compares its Python `DEFAULTS` value to a second literal transcription
of the brief. After it bakes the page, it parses `const DEFAULTS` back out of the
actual HTML and compares that object too. The build also checks the zero-click
boot wiring, the lack of external scripts and stylesheets, 900 Tiles, reciprocal
Neighbor links, 17 serialized palette definitions, and 16 unique palettes.

## Runtime path modeled

1. `build.py` removes full-line `//` comments from
   `Assets/StreamingAssets/penrose_layout.txt`, then parses its 10,800 Mesh
   floats and 900 Tile records. Each Tile contributes its real `neighbors` list.
   The page recovers four corners from the Tile's two source triangles and draws
   the 900-Tile wall.

2. The builder parses `paletteSource` from
   `Assets/Scenes/SampleScene.unity`. It reads all 17
   `DEFINE_GRADIENT_PALETTE` definitions, rejects the duplicate name, and expands
   the remaining 16 gradients to 32 RGB entries at `index / 32`. This follows
   `AnimPalette.processgradient`, `AnimPalette.Map2Palette`, and the established
   `prototype/animateloops-palette/build.py` parser. The scene now places `drum:`
   after `paletteSource`, so this builder terminates the field at the next
   two-space UnityYAML key instead of relying on the older `jsonSource` key.

3. The JavaScript conditioning functions mirror `GPalette.Conditioned` and its
   helpers. They calculate mean relative luminance and hue spread, apply uniform
   palette lift and luminance equalization, repair dark entries by borrowing
   hues from adjacent palette entries, collapse near-duplicates, and redistribute
   the remaining anchors by palette distance.

4. Both palette endpoints are conditioned before the page samples them. Cyclic
   sampling blends the last entry back to the first. During a transition, the
   page samples both conditioned endpoints at the same coordinate and linearly
   blends the results. This is the order used by
   `ConditionedPaletteCache.ReadCyclic`.

5. The audition uses a 60 Hz fixed-step Buffer. Every step retains
   `fadePerFrame` of each Tile's distance from the floor color. The floor color is
   the darkest entry in the current entry-by-entry crossfade, multiplied by
   `floorLevel`. A fractional birth accumulator produces a uniform cadence for
   rates that are not whole births per step. The default produces 15 births per
   step.

6. A Roll chooses HSV confetti with `confettiChance`. Otherwise it chooses
   palette single or palette scatter 50/50. Palette single retains one cyclic
   coordinate for the activation. Palette scatter chooses a coordinate for every
   sparkle. Confetti chooses a full-saturation, full-value hue for every sparkle.
   Every Roll clears the carried Buffer before the new activation begins.

7. An ordinary sparkle writes its birth color to one uniformly random Tile. A
   bright glint writes the center color and multiplies that RGB color by
   `bloomFraction` for every Neighbor Tile. A dark glint keeps the center as-is.
   Its Neighbor bloom preserves hue and solves for `darkBloomLuminance`, reducing
   saturation only when full value at the authored saturation cannot reach the
   requested luminance.

8. The collapsed **Pre-redesign algorithm** group enables the Standalone lane
   captured when this audition was built, at the requested fixed 60 Hz. Each
   Buffer frame multiplies all colors by `0.98`, calculates
   `int((1 / 60) * 900) == 15` births, and writes full-saturation, full-value HSV
   to random Tiles. Its Roll uses the captured `Random.value > 0.5` split. The
   single-color lane rolls activation hue in
   `0..1`, adds the Standalone Waveform fallback endpoint `1`, and applies
   `% 0.15`. The random lane rolls a fresh full-wheel hue for every birth.

## Differences from C# conditioning and crossfade

These are the complete known differences between the JavaScript approximation
and `GPalette.cs`:

- JavaScript uses double-precision `Number`. Unity uses `float`, `Mathf`, and
  `Color`. A value can cross an HSV, duplicate-collapse, or dark-entry threshold
  on a different side when it lies within float rounding distance of that
  threshold.
- The page implements RGB-to-HSV and HSV-to-RGB in JavaScript. The formulas match
  the Unity operations, including cyclic hue interpolation, but Unity's exact
  branch and tie behavior is not shared code. Equal-channel ties can differ by a
  tiny amount.
- Python expands the serialized gradient stops in double precision before it
  bakes the raw entries. `AnimPalette.Map2Palette` uses Unity floats and
  `Color.Lerp`, so a baked raw channel can differ by float rounding.
- The page omits alpha. Scene palette entries are opaque, and Canvas draws opaque
  Tiles. `GPalette.Conditioned` preserves the source alpha channel.
- `AnimPalette.Change` chooses a random palette and only starts when no transition
  is active. The page advances sequentially so every palette appears hands-free.
  Selecting a palette during a transition promotes the current target to the
  current endpoint, then starts a new 3-second transition to the selected target.
- The page starts a crossfade immediately on load. C# starts on palette zero and
  waits for a caller to invoke `AnimPalette.Change`.
- C# decrements a seconds-based tween with `Time.deltaTime`. The page represents
  the same 3-second duration as exactly 180 fixed steps. Its 7-second desk-only
  hold is exactly 420 steps.
- `ConditionedPaletteCache` derives only the current and next endpoints and
  refreshes them by owner revision. The page eagerly conditions all 16 endpoints
  whenever a conditioning control moves. Endpoint colors and sampling order use
  the same math. The amount and timing of conditioning work differ.
- The browser discards frame debt beyond 0.25 seconds after a stalled or hidden
  tab. Unity receives its own rendered-frame deltas. Within active page time, all
  Buffer math still runs in fixed `1/60` steps.
- Browser `Math.random()` supplies every Roll, Tile, coordinate, hue, and glint
  choice. It does not reproduce Unity's random sequence or consumption order.

The dark-glint Neighbor-bloom solve and sequential palette hold remain desk-only.
Floor selection during a palette crossfade and the palette/confetti coordinate
variants now have production counterparts, but this page retains the audition's
algorithms and settings rather than sharing runtime code.

## Regenerate

Run this command from this directory:

```bash
python3 build.py
```

The command reads the runtime sources, writes `out/sim.html`, and prints the
verified Tile, Neighbor-link, palette, and cold-load counts. Open the result:

```bash
open out/sim.html
```

No package install, network request, OSC path, BeatManager read, or Unity session
is required.

## Residual wall risks

- A monitor cannot predict LED gamut, calibrated brightness, black level,
  physical Tile gaps, diffusion, viewing distance, or room light. Dark palette
  entries and the dark-core glint need judgment on the wall.
- Canvas has no physical light spill. A dark glint's Neighbor bloom can read more
  separate from its core here than it will on the installation.
- The fixed 60 Hz desk cadence answers the original audition question. Production
  uses rendered-frame deltas for birth carry and per-frame fade, so this page did
  not establish cadence equivalence with the wall.
- The comparison covers the pre-redesign Standalone lane. It intentionally does not
  simulate Synced Mode, Fill, or Drop because the brief excludes beat data from
  this audition.
- Browser random sequences change the balance of palette regions and the timing
  of rare glints between runs. Use the variant lock when comparing one color mode.
- A palette transition can temporarily move the darkest entry or compress
  luminance differences. Endpoint judgments alone do not cover the 3-second
  blend.
