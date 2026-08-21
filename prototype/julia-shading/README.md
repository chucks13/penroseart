# PROTOTYPE: throwaway. Do not ship anything in this folder.

Desk simulation for auditioning Julia journeys and constants at the wall's real
900-Tile resolution. Round 5 opens on a perpetual boundary glide at a fixed,
legible mid-zoom altitude. The cold-load preset is the Siegel disk candidate,
and the cold-load look is Julia's approved absolute depth fog times relief.
Fractal dive, corridor, and breathing remain available under **Earlier rounds
(superseded)**. `build.py` reads the current wall geometry, Julia Standalone
Settings, and scene palettes. It writes one self-contained `out/sim.html` that
opens from `file://` without a server.

## The question

One Julia preset defines an entire Play Mode session. Round 5 asks whether a
camera can travel forever along a rich, fixed-altitude boundary band without
breathing, reversing, or sinking into locally poor deep windows. The primary
controls answer that question without opening another panel: preset, travel
speed, window, hold-to-boost, **New journey**, and pause. The picker contains the
five saved Standalone presets and six clearly marked candidates. Each candidate
keeps its breathing-mode center for the original preset-pair audition.

A Tile is one logical Penrose rhomb and the wall's smallest distinct visual area
(`CONTEXT.md`, **Tile** lines 21–23). The page models Standalone Mode, the
intentional self-running behavior without the musical clock
(`CONTEXT.md`, **Standalone Mode / Synced Mode** lines 227–229).

The page does not read BeatManager's Data Surface
(`CONTEXT.md`, **Data Surface** lines 199–201). It invents no Fill, Drop, Energy,
or Levels value (`CONTEXT.md`, **Fill** lines 173–175, **Drop** lines 177–179,
**Energy** lines 181–183, and **Levels** lines 189–191). It also does not acquire
or port a Waveform (`CONTEXT.md`, **Waveform** lines 249–251). Those are Synced
Mode facts outside this desk question.

## Runtime path modeled

1. **Wall geometry.** The build removes lines whose trimmed text starts with
   `//`, matching `WallDataText.StripComments`
   (`Assets/core/Runtime/WallData.cs:30–41`). It asserts the layout's 10,800
   Mesh floats and 900 consecutive two-triangle Tiles. Each Tile's four rhomb
   corners and center come from those six vertices. The build applies
   `Penrose.FullScale` and the runtime y-axis flip
   (`Assets/core/Runtime/Penrose.cs:12`, `184–205`). It also reproduces the
   rounded-center padding that gives `penrose.Bounds` its current 50-unit width
   (`Assets/core/Runtime/Penrose.cs:227–259`).

2. **Standalone Settings and presets.** The build parses Julia's saved
   Standalone Settings, including the breathing speed, `Depth`, window endpoints,
   morph orbit, edge framing, depth fog, relief, palette conditioning, hue rates,
   five constants, and their five paired view centers
   (`Assets/effects/Resources/EffectStandaloneSettings/JuliaSettings.asset:16–55`).
   The pairings match Julia's authored table
   (`Assets/effects/Julia.cs:84–103`). `build.py` adds six candidate pairs after
   the parsed table. The preset picker replaces the runtime's random preset Roll so
   every authored and candidate character can be inspected. Two live dials keep a
   desk-only center for each entry and rerun the edge-lock seed search after every
   adjustment.

   The last three candidates are labeled **BOUNDARY** because their filled Julia
   sets have empty interior: the postcritically finite dendrite `c = i`, the
   requested rounded Misiurewicz parameter `c = -0.1011 + 0.9563i`, and the
   upper-limb Misiurewicz parameter
   `c = -0.228155493653962 + 1.115142508039937i`. The last parameter's critical
   orbit lands on a fixed point after four steps. It was chosen as a standard
   postcritically finite counterpoint with a high, asymmetric dendrite rather than
   another real or rabbit-like silhouette.

3. **Boundary glide.** This is the default journey. The camera follows the
   `DE / window = 0.02` contour at a fixed base window of `1.2`, the rich part of
   the approved breathing band where several lobes fit on the wall. Navigation
   uses the standard exterior distance estimate with an escape radius of `256`.
   The larger radius makes the navigation field smoother than the display
   sampler near the contour.

   Each frame estimates the distance gradient with centered differences, takes a
   midpoint tangent step, and applies two Newton corrections back to the target
   contour. The default travel speed is `0.055 window/s`. **New journey** chooses
   another radial start angle and reverses the direction. The glide has no zoom
   cycle, endpoint, or rebase.

4. **Glide boost and roll.** Hold **Boost** to multiply travel speed by `4` and
   ease the camera inward by `1.35` e-folds. Release the button to ease the camera
   back to the base window while travel continues. The inward and outward rates
   are `3.0` and `1.2` e-folds per second. Roll follows the travel heading through
   a damped target bounded to `±0.16` radians. It never accumulates into a
   constant twist.

5. **Morph.** The constant keeps Julia's `0.01` revolutions-per-second orbit.
   The authored presets use the full `0.0024 × window` radius. The three
   boundary-only candidates use a visible `×0.02` scale, which gives a
   `0.0000576` orbit at the default glide window. This is about `0.003` median
   Tile footprints. The small orbit keeps the boundary set connected at wide
   windows instead of breaking it into dust. Both dive comparisons scale their
   orbit from the rebase threshold so the constant does not jump at a rebase.

6. **Fractal dive comparison.** A seeded symbol sequence selects the inverse
   branches `g+(z) = sqrt(z - c)` and `g-(z) = -sqrt(z - c)`. Their nested
   composition gives the camera target. The current piece grows until the
   threshold. The page then applies `f(z) = z² + c` to the camera, drops the first
   branch symbol, and enters the next piece.

   **Steered** extends the sequence in retained four-symbol words. It probes nine
   in-frame positions around each child continuation and weights the choice toward
   mixed coverage. **Random** restores unweighted symbol rolls. The page gathers
   symbols in 32-symbol blocks and accepts a block only when every represented
   level has `|2z| >= 1.10`. The floor keeps each rebase expansive, but it also
   steers away from the branch points that made deep dives worth seeing. That is
   why this mode now lives under **Earlier rounds (superseded)**.

   The default dive threshold is `0.000006`. A rebase maps the center through
   `f`, multiplies the window by `|2z|`, subtracts `arg(2z)` from the roll, and
   advances the invariant hue phase by one hue step. Dive rate defaults to `0.35`
   e-folds per second. The hold button applies the same live `4` multiplier.

7. **Corridor comparison.** Corridor keeps the fixed-point journey. Every frame
   solves `beta = (1 + sqrt(1 - 4c)) / 2`, centers on `beta`, and uses
   `lambda = 2 beta` for its fixed-point wrap and roll. Its invariant coloring and
   the deliberately popping absolute-color comparison remain available.

8. **Breathing comparison.** **New journey** rolls a browser-random breathing
   speed in `[0.1, 0.3]` and a morph start phase. The window breathes between
   `0.002` and `5` with the parsed `Depth`, currently `0.75`. This matches the
   roles of `RollSessionJourney` (`Assets/effects/Julia.cs:482–510`). The two live
   center dials rerun the edge-lock search after each change. The camera uses the
   repelling fixed-point solve, the 12-level inverse-image address search, the
   continuous branch choice, and the final framing toward the preset's authored
   center (`Assets/effects/Julia.cs:617–717`).

9. **Escape sampling and baseline.** Every frame evaluates all eight authored AA
   offsets around every Tile center. The offsets alternate radii `1.2` and
   `1.2 × 0.55` (`Assets/effects/Julia.cs:257–271`, `356–367`). The smooth escape
   loop keeps the runtime's bailout and fractional `n + 1 - log2(log|z|)` result
   (`Assets/effects/Julia.cs:819–844`). Breathing mode and the absolute dive
   comparison keep the runtime's 100-iteration cap, square-root palette coordinate,
   and black non-escaping samples (`Assets/effects/Julia.cs:851–859`). Both
   invariant dives use 160 iterations so weak multipliers reach the
   distance-field limit before the cutoff. Eight sample colors average into each
   Tile exactly as `Draw` does (`Assets/effects/Julia.cs:724–811`). The escape
   sampler reuses one two-value result buffer on this hot path instead of
   allocating one array for every AA sample.

10. **Current color paths.** The page exposes both branches that Julia's
   `Reroll` chooses with its authored 0.5 palette chance
   (`Assets/effects/Julia.cs:513–525`). HSV mode uses the full-brightness hue
   wheel. Palette mode uses the selected real scene palette after Julia's saved
   `PaletteConditioning` values. `GPalette.Conditioned` performs luminance
   equalization, dark-stop repair, duplicate collapse, and redistribution
   (`Assets/core/helpers/GPalette.cs:169–204`, `215–483`).
   `ReadCyclic` wraps and blends the final entry back to the first
   (`Assets/core/helpers/GPalette.cs:527–548`). Standalone hue scroll is
   `0.05 + 1 × 0.25 = 0.30` revolutions per second
   (`Assets/effects/Julia.cs:71–81`, `Assets/effects/Julia.cs:742–765`).

11. **Real scene palettes.** The scene contains 17 serialized
   `DEFINE_GRADIENT_PALETTE` definitions
   (`Assets/Scenes/SampleScene.unity:2018–2049`). The runtime rejects duplicate
   names and maps every accepted gradient to 32 entries at `x / 32`
   (`Assets/core/helpers/GPalette.cs:774–803`). The second `bhw1_05_gp` is
   rejected, leaving 16 uniquely named palettes. The build adapts the proven
   parser and conditioning port from
   `prototype/animateloops-palette/build.py:39–305`.

12. **Shading.** Glide and breathing modes use the original absolute coloring.
   Escaped AA samples use the square-root smooth-count palette coordinate,
   interior samples stay black, and the default stack is inverted
   squared-exponential depth fog times relief. All look values come from the
   parsed Standalone asset. The **Absolute coloring (pops at rebases)** toggle
   selects the same path in both dive comparisons.

   Glide applies the round-4 analytic exterior coverage after the approved color
   calculation. The coverage uses
   `q = DE / (window × TilePitch / boundsWidth)` and
   `1 - exp(-(q / 1.10)²)`. This is a Tile-scale area estimate, not a replacement
   hue, fog, or relief field. It suppresses point-sample cutoff flicker while
   keeping the approved absolute stack.

   Both invariant dives replace every rebase-variant input. Hue is
   `Repeat(smoothN × hueStep + hueScroll + rebasePhase, 1)`. Fog is shaped from the
   scale-free exterior distance `u = DE / window`, where the standard distance
   estimate is `DE = |z| × ln|z| / |z'|`. Relief fits the screen-space gradient of
   `ln(DE / window)` across the existing AA footprint and lights its normal from
   the same fixed direction.

   Dive-mode invariant shading also reconstructs at the Tile footprint instead of
   treating each sample as a point. With
   `q = DE / (window × TilePitch / boundsWidth)`, analytic exterior coverage is
   `1 - exp(-(q / softening)²)`. Fog evaluates a footprint-filtered distance
   `hypot(u, 0.5 × softening × TilePitch / boundsWidth)`, while hue and relief
   detail are weighted by fog amount times exterior coverage. The default
   softening is a live `1.10` Tile control. A sample at the cutoff has `q = 0`, no
   hue or relief detail, and the same reconstructed fog value approached by an
   escaping sample as `DE -> 0`; cutoff flips therefore stay continuous. Every
   new term is a ratio to `window`, so a rebase leaves the filtered field
   unchanged. The filter raises and widens the near-set floor compared with the
   round-3 point shading; that calmer but less deeply carved boundary is an
   intentional desk judgment. The fog-density, fog-floor, polarity,
   light-azimuth, relief-depth, and Tile-softening controls remain live.

   The absolute depth fog uses MazeFlyer's squared exponential and floor-to-one remap
   (`Assets/effects/MazeFlyer.cs:1350–1369`). Fat contours cap their local
   smooth-count frequency so both bright and dark stripes remain at least one
   median Tile pitch wide, following MazeFlyer's constant on-wall thickness rule
   (`Assets/effects/MazeFlyer.cs:1414–1453`). Relief's `1 - depth` floor follows
   Angles' directional-shade band (`Assets/effects/Angles.cs:1144–1151`).

The build asserts the parsed ground truths before writing the page: 900 Tiles,
five authored constants paired with five authored view centers, 17 serialized
palette definitions, 16 unique palettes, and the current 50-unit Penrose bounds
width. Source drift therefore stops regeneration instead of producing a plausible
stale simulation.

## Shading controls

| model | default | why this starts there |
| --- | --- | --- |
| Baseline | absolute: brightness `1` outside, `0` inside; invariant: boundary-limit color | Unshaded control path for the selected color system. |
| Distance rim | `1.5` Tile widths, strength `0.65`, bright polarity `+1` | Wide enough to survive one Tile while leaving enough field color to judge the cost. |
| Relief lighting | parsed azimuth `315°`, parsed depth `0.72` | Matches Julia's fixed runtime relief light. |
| Depth fog | parsed density `4`, floor `0.22`, inverted | Keeps Julia's dark-set/bright-exterior character; both invariant dives shape it in `u`. |
| Fat contours | `0.18` cycles per smooth count, contrast `0.55`, duty `50%` | Starts with broad equal bands and enough contrast to read without turning the palette into black stripes. |
| Fog + relief | the parsed fog and relief values above | Cold-load view: absolute runtime fields in boundary glide, with Tile-scale exterior coverage. |
| Rim + fog | the rim and fog defaults above | A cheap second stack that tests local boundary emphasis against global depth structure. |

Pause on a frame before switching models. The model switch does not reset the
preset, breath, morph, edge-lock chain, hue scroll, or color path.
The badge reports a rolling average of render-and-draw milliseconds per frame so
slow page rendering can be separated from shading flicker.

## Deliberate simplifications

- The page models Standalone Mode only. There is no Fill dive, Drop spin or
  blowout, Levels response, Energy response, or Waveform-driven value. The boost
  button is a manual desk input that previews an acceleration ratio. It does not
  acquire, invent, or simulate Fill.
- The preset and color-path pickers replace Julia's activation randomness for
  comparison. The six candidate presets and every live center adjustment stay
  in browser memory. In glide mode, **New journey** keeps the morph and hue
  phases, chooses another start angle, and reverses direction. In breathing mode,
  it rolls the authored speed range and the morph start phase. Browser
  `Math.random()` does not reproduce Unity's random sequence. Fractal dive uses a
  seeded xorshift stream, and the page shows its seed.
- The selected scene palette is a steady endpoint. The page does not port
  `AnimPalette` rolls or palette cross-fades.
- The glide navigation distance field is prototype behavior, not runtime code.
  Glide and breathing preserve Julia's current hue, fog, and relief calculations.
  Both invariant dive comparisons change the hue coordinate, fog curve, relief
  field, interior color, and iteration budget to make a rebase an identity.
- Both relief paths use a least-squares gradient fitted across Julia's eight AA
  samples. Neither is an analytic potential normal.
- "One Tile width" uses the current layout's median nearest-center pitch in
  effect-layout units. Rhombs vary in type and orientation, so no one scalar is
  every Tile's physical width.
- Python conditions palettes in double precision. Browser JavaScript also uses
  doubles. Unity uses `float`, `Color`, and `Mathf`, so values can differ slightly
  near escape, HSV, or conditioning thresholds.
- The canvas fits the wall outline directly. It does not port Unity camera,
  preview-scene, material, or post-processing settings.

## Regenerate

Run from this directory:

```bash
python3 build.py
```

The command reads the runtime sources and writes `out/sim.html`. Open it directly:

```bash
open out/sim.html
```

No server, package install, network request, OSC path, BeatManager read, or Unity
session is required.

## Residual wall risks

- A monitor cannot predict LED gamut, calibrated brightness, black level,
  diffusion, physical Tile gaps, viewing distance, or room light.
- Canvas fills have no light spill between neighboring rhombs. On the wall, a
  bright rim or contour may bleed into the dark side and read thicker.
- The desk's median-pitch rule cannot predict whether thin and fat Tiles give the
  same apparent rim or contour width at show distance.
- Browser frame pacing and JavaScript escape-loop cost can change motion cadence.
  Julia's authored rates remain elapsed-time based, but a dropped desk frame
  still changes what the eye sees.
- The glide follows one closed exterior distance contour. It runs forever, but it
  eventually returns to earlier coastline instead of generating a unique path
  forever. **New journey** chooses another start and direction on that contour.
- Morph moves the contour while the camera travels. The two Newton corrections
  keep the measured default run on level, but another preset, speed, window, or
  boost combination can need different navigation constants.
- The dive uses JavaScript double precision. The `0.000006` threshold leaves too
  little precision for an assumed Unity `float` port near unit-sized centers. A
  runtime design needs local coordinates, doubles, or a separately proven wider
  band.
- Fractal rebase uses the derivative at the camera center. The quadratic term is
  not zero across the frame. The threshold makes that term invisible in the
  generated double-precision buffer; it does not turn the rebase into a global
  identity.
- The numeric gate uses one fixed branch seed per preset. **New journey** can roll
  another accepted sequence whose cutoff-edge samples produce a larger delta.
- The page compares steady color endpoints. A palette transition can temporarily
  compress luminance differences that look clear at either endpoint.
- Eight AA samples still estimate relief and analytic coverage at the same
  resolution they are trying to improve. The footprint curve removes point-sample
  discontinuities but is not an exact area integral of a Julia branch; a model can
  look stable here and still shimmer when the physical viewer moves.
