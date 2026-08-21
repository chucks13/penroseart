# PROTOTYPE — throwaway. Do not ship anything in this folder.

Desk simulation of the palette lookup proposed for AnimateLoops — since renamed
`AnimateShapes`, whose foreground now uses the cyclic palette sampling this
prototype proposed (`Assets/effects/AnimateShapes.cs`). File cites below reference
`AnimateLoops.cs` as it stood at measurement time. It reads the current
wall geometry, Ring/Arc groups, and scene palettes when it builds. The generated
HTML is one self-contained file that opens from `file://` without a server.

## The question

AnimateLoops then made color crawl along each Ring and Arc by adding a fixed
per-Tile hue step to a per-frame hue advance. This prototype asks whether that
signature crawl survives when cyclic palette sampling replaces the hue wheel for
the foreground, the background, or both. It also lets the maintainer compare every
scene palette raw and with the Angles Standalone conditioning preset.

The prototype models the Standalone-look layers. Its optional **Simulated beat
pulse** is an explicit stand-in flag, not Synced Mode. In project vocabulary, a
Ring is a closed circuit, an Arc is its wall-clipped open form, and either group is
a Motif (`CONTEXT.md:45–47`, `61–67`). Standalone Mode is the intentional
self-running behavior without the musical clock (`CONTEXT.md:227–229`). The local
envelope is unipolar `[0..1]`, matching the range of a Waveform but not acquiring
or porting one (`CONTEXT.md:247–251`).

## Runtime path modeled

1. **Wall geometry and comments.** `build.py` removes lines whose trimmed text
   starts with `//`, matching `WallDataText.StripComments`
   (`Assets/core/Runtime/WallData.cs:30–41`). The layout declares 10,800 Mesh
   floats, two triangles per rhomb Tile, and Tile ownership by consecutive
   triangle pairs (`Assets/StreamingAssets/penrose_layout.txt:4–17` and
   `Assets/core/Runtime/WallData.cs:711–714`). The page recovers each rhomb's four
   corners from its two source triangles and draws all 900 Tiles.

2. **Ring and Arc groups.** The layout documents the packed Shape List format at
   `Assets/StreamingAssets/penrose_layout.txt:29–34`. Its serialized `loops` field
   becomes runtime `Rings` (`Assets/core/Runtime/WallData.cs:100–101`, `202–203`).
   `Reader.GetGroup` reads `pointer = packed[groupIndex + 1]`, then starts the Tile
   view at `pointer + 1` (`Assets/core/Runtime/WallData.cs:623–627`).
   `Group.PackedIndex` returns that global source-array position plus the local Tile
   index (`Assets/core/Runtime/WallData.cs:697–705`). The prototype embeds that
   global position for every Tile. It does not replace it with a group-local index.

3. **AnimateLoops Standalone look.** The authored defaults are 0.1 background hue
   cycles per second, 0.01 hue per Tile, and 0.01 group hue per rendered frame
   (`Assets/effects/AnimateLoops.cs:15–22`). Activation selects the Rings Shape
   List, seeds every group's hue and saturation randomly at brightness 1, and
   seeds the background hue randomly (`Assets/effects/AnimateLoops.cs:144–167`).
   Each frame reseeds one random group, advances the background using elapsed time,
   paints all 900 Tiles, renders every group at
   `storedHue + tileStep * PackedIndex`, and advances every stored group hue once
   (`Assets/effects/AnimateLoops.cs:177–249`).

4. **Hue-wheel baseline and palette experiment.** Baseline uses an HSV hue-wheel
   conversion with the foreground group's stored saturation and brightness.
   Palette mode keeps the same background position, stored foreground position,
   global packed-position gradient, and per-frame advance. For each selected
   layer, it replaces the final HSV lookup with the palette's full RGB result.
   `GPalette.ReadCyclic` wraps the coordinate, addresses all entries cyclically,
   joins the final entry back to the first, and either chooses the nearer entry or
   linearly blends the pair (`Assets/core/helpers/GPalette.cs:527–547`).

5. **Real scene palettes.** The 17 `DEFINE_GRADIENT_PALETTE` definitions live in
   the scene's serialized `paletteSource`
   (`Assets/Scenes/SampleScene.unity:2018–2049`). The runtime rejects duplicate
   names, maps every accepted gradient to 32 entries at `x / 32`, and interpolates
   between authored stops (`Assets/core/helpers/GPalette.cs:774–803`, `815–828`,
   `839–863`). The parser adapts the already-proven source extraction in
   `prototype/kscope-pace/render_wall.py:204–244`. The second `bhw1_05_gp` is
   rejected, leaving 16 uniquely named palettes.

6. **Conditioning.** The build ports the complete `GPalette.Conditioned` pipeline
   with the Angles Standalone values: target luminance 0.4, minimum 0.12,
   equalization 0.85, hue-spread reference 0.5, maximum lift 4, dark threshold
   0.03, duplicate threshold 0.08, and full redistribution
   (`Assets/effects/Angles.cs:70–89`). Source statistics and linear relative
   luminance come from `Assets/core/helpers/GPalette.cs:103–125` and
   `Assets/core/helpers/ExtensionMethods.cs:80–82`. The orchestration is
   `GPalette.cs:169–204`; luminance equalization is `215–241`; dark-color repair is
   `250–275`; neighboring hue borrowing is `281–351`; donor selection and cyclic
   hue interpolation are `354–369`; duplicate collapse and palette distance are
   `375–415`; and cyclic redistribution is `422–483`.

The build asserts the parsed ground truths before writing the page: 900 Tiles, 73
Ring/Arc groups, 17 serialized gradient definitions, and 16 uniquely named scene
palettes. Source-data drift therefore stops regeneration instead of producing a
plausible stale simulation.

## Deliberate simplifications

- Fill and Drop responses are absent. No Fill, Drop, Levels, or Energy value is
  invented.
- The optional beat response is one fixed 120 BPM synthetic envelope applied as
  the Color response's `0.25 * envelope` position shift. It does not port OSC,
  BeatManager, Waveforms acquisition, or the Time Warp distortion. The runtime
  response magnitude is authored at `Assets/effects/AnimateLoops.cs:47–48` and
  applied at `198–203`.
- A selected palette is a steady endpoint. The simulation does not port
  AnimPalette rolls or palette cross-fades.
- Browser `Math.random()` supplies the same random roles and consumption cadence,
  but it does not reproduce Unity's random sequence.
- Ring hue advance remains per rendered frame, as authored. Browser animation
  frames are monitor-driven and are not Unity frames.
- Python performs conditioning in double precision with standard RGB/HSV
  conversion. Unity performs the source path with `float` and `Color`; colors can
  differ by a small amount near an HSV tie or a conditioning threshold.
- The canvas fits the source Mesh bounds directly. It does not port Unity camera,
  material, post-processing, or preview-scene settings.
- Palette mode intentionally uses the palette's full RGB result. A foreground
  group's random saturation and brightness remain visible in baseline mode but do
  not overwrite palette-authored saturation or conditioned luminance.

## Regenerate

Run from this directory:

```bash
uv run build.py
```

The command reads the runtime data and writes `out/sim.html`. Double-click that
file, or open it directly:

```bash
open out/sim.html
```

No server, package install, network request, or Unity session is required.

## Residual wall risks

- A monitor cannot predict LED gamut, calibrated brightness, black level, physical
  Tile gaps, diffusion, viewing distance, or how conditioning reads at wall scale.
- Browser frame pacing can make the frame-based foreground advance feel faster or
  slower than the show runtime. The background remains elapsed-time based in both.
- The desk view shows one random activation at a time. Different group hues,
  saturations, and one-group-per-frame reseeds can change which palette regions
  dominate a moment.
- Immediate palette switching makes comparison easy but omits live transition
  moments between palettes.
- The synthetic pulse tests whether a whole-picture position shift remains
  legible. It cannot predict the timing or feel of the runtime's acquired
  Waveforms.
