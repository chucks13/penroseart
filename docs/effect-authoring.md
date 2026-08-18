# Effect and Transition Authoring Guide

Effects are plain C# classes that fill a 900-color buffer for the Penrose Wall. They are discovered automatically at runtime, so most new visuals do not need scene or prefab changes.

## Create a new effect

1. Copy `Assets/effects/EmptyEffect.cs` and `Assets/effects/EmptyEffectSyncSettingsAsset.cs`.

2. Rename the copied files and the `EmptyEffect` class for the new Effect.

3. Rename `EmptyEffectStandaloneSettings`, `EmptyEffectSyncSettings`, and `EmptyEffectSyncSettingsAsset` for the new Effect. Update the copied `[CreateAssetMenu(...)]` file and menu names and the `[EffectSyncSettings(typeof(...))]` attribute.

4. Fill in the copied Effect Settings scaffold with the new Effect's authored values.

5. Remove `[RuntimeCatalogIgnore]` from the Effect copy.

6. Delete the `EXAMPLE` members that the Effect does not need and implement `Draw()`.

7. Enter Play Mode or run a compile/import check. Unity then generates both `.meta` files and compiles both classes.

8. Create the Sync Settings asset from the Tuning Window's Effects tab — compiling alone does not create it (see [Wire the Sync Settings path](#wire-the-sync-settings-path)).

The runtime catalog intentionally ignores `EmptyEffect`. Keep, rename, and fill in its Effect Settings scaffold. Delete the illustrative `EXAMPLE` members as needed.

### Start with Effect Settings

Use the canonical [`CONTEXT.md` Effect configuration terms](../CONTEXT.md#effect-configuration). They are Effect Settings, Standalone Defaults, Sync Defaults, Standalone Settings, and Sync Settings. [`ADR-0013`](adr/0013-standalone-settings-join-the-editor.md) records the standing decision, superseding [`ADR-0012`](adr/0012-an-effects-standalone-look-is-fixed-its-mechanism-is-not.md). This guide shows its code layout and does not redefine the vocabulary.

Put `// Standalone Defaults` and `// Sync Defaults` before runtime state. Put values that shape Standalone Mode under Standalone Defaults. Put values that author musical response under Sync Defaults. Keep structural literals and runtime state outside both blocks. Treat each randomization range as a setting. Author both bounds instead of leaving them inline in `Random.Range(...)`. Carry a two-ended range as a [`FloatRange`](../Assets/core/Effects/FloatRange.cs) or an [`IntRange`](../Assets/core/Effects/IntRange.cs) — `IntRange` is inclusive-min/exclusive-max, matching `Random.Range(int, int)`. Both types carry their own Rails, and the shared [`NumericRangeDrawer`](../Assets/Editor/NumericRangeDrawer.cs) draws every range as editable Rails, exact endpoint fields, and a two-thumb slider. One-ended values stay scalar.

Per ADR-0013, a fitted Effect also carries a saved Standalone Settings asset with the same contract as its Sync Settings asset: serialized, live-tweakable in Play Mode from the Effects tab, and restorable at any moment to the in-file Standalone Defaults, which remain the one authored record of the look. The Standalone path mirrors the Sync path below — `[EffectStandaloneSettings(typeof(...))]` on the Effect, `EffectStandaloneSettingsProvider.Resolve(typeof(<EffectName>), StandaloneDefaults)` at activation, and the asset under `Resources/EffectStandaloneSettings/<EffectName>Settings`. [`Waterfall`](../Assets/effects/Waterfall.cs) and its [`WaterfallStandaloneSettingsAsset`](../Assets/effects/WaterfallStandaloneSettingsAsset.cs) are the fitted reference.

The Effect defines its carrier shape. These three calibration Effects establish proven shapes.

| Calibration | Proven shape |
| --- | --- |
| [`Tunnel`](../Assets/effects/Tunnel.cs), its [`TunnelStandaloneSettingsAsset`](../Assets/effects/TunnelStandaloneSettingsAsset.cs), and its [`TunnelSyncSettingsAsset`](../Assets/effects/TunnelSyncSettingsAsset.cs) | Scalar authored constants build mode-specific `FloatRange` values through object initializers. The Roll selects the active mode's ranges before randomization. |
| [`Ripple`](../Assets/effects/Ripple.cs) and its [`RippleSyncSettingsAsset`](../Assets/effects/RippleSyncSettingsAsset.cs) | The Effect moves tuned inline literals into settings. It dual-homes the `Waveform.Lerp` to-slot as a Standalone Default and a Sync Default. The call site selects one with `beatManager.IsSynced`. |
| [`CrystalGrowth`](../Assets/effects/CrystalGrowth.cs), its [`CrystalGrowthStandaloneSettingsAsset`](../Assets/effects/CrystalGrowthStandaloneSettingsAsset.cs), and its [`CrystalGrowthSyncSettingsAsset`](../Assets/effects/CrystalGrowthSyncSettingsAsset.cs) | A large field-based settings pair dual-homes sixteen picture-shaping values with identical numbers, selected off `BeatManager.IsSynced`. Endpoint pairs live in `FloatRange`/`IntRange` values whose Rails carry the per-use slider bounds. |

Choose the shape that fits the Effect. Do not force common fields or mechanically copy one calibration.

The scaffold standardizes where authored values live and how an Effect reaches its Sync Settings — nothing more. Re-rolls, Grid response, Waveform use, and every musical mapping remain the Effect's own decisions ([ADR-0013](adr/0013-standalone-settings-join-the-editor.md)).

#### Wire the Sync Settings path

The copied `EmptyEffect` skeleton already wires the Sync Settings path. Rename and fill in each piece. Do not delete and rebuild the structure.

1. Rename `EmptyEffectStandaloneSettings` and `EmptyEffectSyncSettings` for the new Effect.

   The Standalone Settings type carries the fixed Standalone Settings in code. The Sync Settings type defines the serializable saved shape. Replace the placeholder fields. Keep suitable `[Range]` or `[Min]` bounds on Inspector values.

2. Update the copied `StandaloneSettings`, `SyncDefaults`, and `SyncSettings` properties with the new type names.

   The Standalone Settings property builds a fresh value from Standalone Defaults. The Sync Defaults property builds a fresh value from the file-local Sync Defaults. The Sync Settings property holds the saved or fallback value for the current activation.

3. Rename `EmptyEffectSyncSettingsAsset.cs` and `EmptyEffectSyncSettingsAsset` for the new Effect. Update the copied `[CreateAssetMenu(...)]` file and menu names. Update the copied `[EffectSyncSettings(typeof(...))]` attribute. The asset stores the serialized Sync Settings for its Effect. The restore method copies the current file-local Sync Defaults over the saved copy.

4. Call `EffectSyncSettingsProvider.Resolve(typeof(<EffectName>), SyncDefaults)` wherever the Effect refreshes its settings — activation (`OnStart`) is the usual spot, and whether it also refreshes elsewhere is the Effect's choice. The provider loads `Resources/EffectSyncSettings/<EffectName>Settings`. When no asset exists, the provider uses the supplied file-local Sync Defaults. The provider consumes no `UnityEngine.Random`, so resolution never disturbs an Effect's roll order.

The compile/import step imports and compiles the scripts. It does not create a Sync Settings asset. Create the saved asset from one of two surfaces.

- The Tuning Window **Effects** tab: select the Effect and use its **Create Sync Settings Asset** button (or **Create Standalone Settings Asset** for the Standalone side), or use the tab's **Create Missing Settings** toolbar button for the whole catalog.

- The **Window > Penrose > Create Missing Effect Sync Settings** and **Create Missing Effect Standalone Settings** menu items.

Never hand-create the `.asset` file. Commit the Unity-generated `.asset` and `.meta` with the Effect.

To write a tuned look back into the file, use the Effects tab's **Copy Defaults Update** button (one per settings panel). It copies the Effect's authored defaults block to the clipboard with only the numeric literals replaced by the current saved values — doc comments, names, and formatting intact — ready to paste over the old block. The button never writes a file: defaults still change only by editing the source ([ADR-0012](adr/0012-an-effects-standalone-look-is-fixed-its-mechanism-is-not.md)/[0013](adr/0013-standalone-settings-join-the-editor.md)). Rails are not written back; the saved asset carries them, and the button's log notes when a Rail differs from what the defaults would seed.

Read musical-response values from the resolved Sync Settings.

Classify each authored value by the mode that reads it. A value that only Standalone rendering reads is a Standalone Default. A value that only Synced rendering reads is a Sync Default. A value that both modes read is dual-homed. A dual-homed value keeps a fixed value in the Standalone Defaults and a live value in the Sync Defaults. The call site selects one with `beatManager.IsSynced`.

A mode reads a slot when a change to that value can change the rendering of that mode, however rare the state. The `Waveform.Lerp` to-slot in Ripple shows the dual-home shape.

Fill and Drop are Synced Mode facts. The running clock is what carries them, so `Fill.Active` and `Drop.Active` are never true in Standalone Mode. A value read only inside a Fill- or Drop-gated branch is therefore a Sync Default, never dual-homed, and a Drop slowdown window is likewise Sync-only — `Before.Decay` rests at one in Standalone Mode, so the window cannot reach Standalone rendering.

A dual-homed slot needs both values. An operator can edit either mode's saved settings while the wall runs. One shared value would let a live tweak to one mode change the other mode's look. Two authored values keep each mode on its own value, so the two modes stay independent.

Two consequences follow from that selection. They are properties of the pattern, so they hold for every Effect that dual-homes a value and no Effect restates them.

A dual-homed value changes discontinuously. The call site selects one of the two authored values per frame, and nothing eases between them. So once the Sync copy is tuned away from the Standalone value, the output jumps the moment `beatManager.IsSynced` flips — mid-phrase included, because `IsSynced` reports beat position and not the presence of music. Smoothing an input does not smooth this: Angles eases `smoothedEnergy` and still jumps, because the jump is in the endpoints that `smoothedEnergy` interpolates between.

A Sync Setting baked into an `Init`-time cache is only half-live. The cache is built once, so a Play Mode edit reaches the call-site term and never reaches the baked term, and the shape moves in part. Cache the invariant part alone and apply the setting per frame, hoisted out of any per-element loop. Angles caches bare normalized rank in `frontRank` and applies its soft-edge width in `Draw` for this reason.

[`Flock`](../Assets/effects/Flock.cs) is the advanced reference for a production music-reactive effect. Its source is organized in
reading order—signal hierarchy, artistic tuning, runtime state, lifecycle, frame pipeline, musical mappings,
and simulation—and documents why each musical source controls its particular visual consequence. Start from
`EmptyEffect`; consult `Flock` when adding Routines, calibrated levels, Standalone behavior, Fill/Drop
choreography, persistent trails, or a stateful simulation.

## Rhythm cheat sheet

| I want... | Use |
| --- | --- |
| Pulse with the beat | `waveform.Lerp(from, to)` |
| A rhythm that varies over four bars | `Routine.Of(...)` + `routine.Lerp(from, to)` |
| Calmer / busier rhythms | `waveforms.Random(Energy.Low)` / `(Energy.High)` |
| Change rhythm as the music moves | Call the Waveform selection again in `OnNewGrid()` |
| Tension rising through a Fill or Drop | `beatManager.Fill.In.Build()` / `Drop.In.Build()` |
| A flash that fades after the Drop | `beatManager.Drop.In.Decay(beats)` |
| Slow into an approaching Drop | `beatManager.Drop.Before.Decay(8)` |
| Charge up as a Drop approaches | `beatManager.Drop.Before.Build(8)` |
| Rise into the next Chorus | `beatManager.Chorus.Before.Build(32)` |
| Wind down toward the Outro | `beatManager.Outro.Before.Decay(64)` |
| Shape within the current section | `beatManager.Chorus.In.Build()`. Any Phrase Handle works. |
| React to audio loudness | `beatManager.Levels.Smoothed` / `.Peak` |
| Know whether a live beat is usable | `beatManager.IsSynced` |
| A deliberate look with no music | Choose the `to` endpoint. It is the fallback. |

Optional musical facts are nullable. Use `?? fallback` or `is { } value`. Booleans and Pulses are never null. See [`Flock`](../Assets/effects/Flock.cs) for a complete production example.

## Create a new transition

1. Copy `Assets/transitions/EmptyTransition.cs`.
2. Rename the file and class to the new transition name.
3. Remove `[RuntimeCatalogIgnore]` from the copy.
4. Adjust its Runway/Tail settings and implement the A-to-B blend in `Draw()`.
5. Enter Play Mode or run a compile/import check so Unity generates the new `.meta` file and compiles the class.

`EmptyTransition` is likewise ignored by the runtime catalog. Its comments explain the transition lifecycle, A-to-B progress, Runways/Tails, and the same BeatManager/Waveforms tools available to effects. Transition Repertoire's Runway and Tail also participate in track-sheet planning: the Director casts Transitions that fit the space they are given, and no Transition's Runway or Tail crosses a Drop or Fill moment (see [`TrackCueSheet`](../Assets/core/Switching/TrackCueSheet.cs) and ADRs 0009-0011).

## Choose a base class

| Base class | Use when | Typical output path |
| --- | --- | --- |
| `EffectBase` | The effect works directly on Penrose tiles. | Write directly to `buffer[i]` for each tile. |
| `ScreenEffect` | The algorithm is easier in rectangular 2D coordinates. | Write to `screenBuffer`, then call `ConvertScreenBuffer(...)`. |
| `MixerBase` | The effect owns child effects and combines or transforms their buffers. | Create child effects in `OnStart()`, draw them, then combine child buffers into `buffer`. |

## Required methods

Every concrete effect must implement:

```csharp
public override string DebugText()
public override void Draw()
public override void OnEnd()
```

`OnEnd()` is currently reserved; the controller does not call it. Implement it as an empty method unless the lifecycle is changed later.

## Lifecycle

```text
Init()       once after reflection creates the catalog instance
OnStart()   whenever the effect becomes active
UpdateTime() called by Controller before Draw()
OnNewGrid() once when this Effect observes the Grid return to one
OnNewPhrase() once when this Effect observes a new Phrase begin
Draw()      every active frame
OnEnd()     not currently called
```

`OnNewGrid()` and `OnNewPhrase()` are base hooks on `EffectBase`, both raised from `UpdateTime()` by comparing a captured value with that Effect's prior observation. `OnNewGrid()` fires when `Grid.Beat` returns to one. `OnNewPhrase()` fires when `Phrase.BeatsRemaining` returns to `Phrase.LengthBeats`, which is the phrase's first beat; the remaining count is the test rather than `Phrase.Name`, because consecutive phrases may share a name. Override either to re-roll a look, switch palette, or acquire a new Waveform in step with the music. An effect nested in a mixer only receives them if the mixer calls the child's `UpdateTime()`.

The two boundaries are not independent. The Grid is phrase-relative, so a phrase boundary is expected to restart it and an Effect overriding both should expect both on the same frame at a phrase start. Standalone Mode carries no Phrase, so `OnNewPhrase()` never fires there — an Effect that moves work onto the Phrase keeps a Standalone cadence on `OnNewGrid()`. Pairing them lets one Effect run two rates: `Tunnel` changes palette on every Grid and re-rolls its shape only on the Phrase.

Use `Init()` for reusable setup that depends on `Controller.Instance`, `penrose`, or `tiles` existing.

Use `OnStart()` for per-activation state: random parameters, child effect selection, Waveform acquisition/alignment, or clearing persistent buffers.

Use `Draw()` for the frame algorithm. A valid draw writes every slot in `buffer`, unless the effect intentionally uses trails/fading and documents that behavior.

## Useful base members

| Member | Meaning |
| --- | --- |
| `buffer` | This effect's 900-color output frame. |
| `penrose` | The active Penrose model and JSON data. |
| `tiles` | Cached tile metadata from `Penrose.Tiles`. |
| `effectTime` | Seconds since the effect's randomized seed time. |
| `effectDelta` | Current frame delta time. |
| `APalette` | Shared animated palette for all effects. |
| `beatManager` | Shared read-only wire facts and derived musical values. |
| `waveforms` | Shared Waveform acquisition tools. |
| `waveform` | Public non-null artistic configuration. Effects acquire it explicitly; owners may share, replace, or assign `waveforms.None`. |

## Catalog identity

The runtime catalog is sorted by type full name. For a fixed set of classes, indexes are deterministic. Adding, removing, or renaming an effect can still shift indexes.

For debugging and force-selection, prefer names over numeric indexes. `forceEffectName` matches effect names by case-insensitive substring.

## Child effects in mixers

Mixer effects create child effects manually by calling `GetRandomEffect()`. Those children are not top-level catalog instances and are not driven by `Controller` directly.

A mixer that creates a child effect should usually call:

```csharp
child.Init();
child.OnStart();
```

After `OnStart()`, the mixer may directly configure the child. Assign the mixer's `waveform` for unison, assign `waveforms.None` to suppress child Waveform response, or leave it unchanged for independent behavior. These are ordinary object assignments, not runtime modes. If unison or suppression must persist across Grid wraps, reapply the assignment after `child.UpdateTime()` and before `child.Draw()`, because the child's Grid hook may acquire a new Waveform.

Waveform response reads from the value itself. Use `waveform.Envelope` for a raw `[0..1]` lift, or `waveform.Lerp(from, to)` when the rhythm maps between two artistic endpoints. Without live placement, `Envelope` is 0 and `Lerp` returns `to`, preserving the effect's explicit Standalone response without nullable syntax.

Then, inside `Draw()`:

```csharp
child.UpdateTime();
child.Draw();
```

`MixerBase.GetRandomEffect()` avoids returning other mixers, preventing recursive mixer trees.

## Wall geometry available to effects

`EffectBase.Init()` gives every Effect the active `penrose` model and caches its `tiles` array. The runtime loads `Assets/StreamingAssets/penrose_layout.txt`; its `Mesh` coordinates are the source for both the Unity preview mesh and the effect-visible center, coarse position, tile angle, radius, and polar angle. Geometry is therefore part of the Effect interface, not preview-only data.

### Coordinate system and bounds

Tile centers use **effect-layout units**. The origin is the raw mesh's `(0,0)`. Runtime mesh generation flips the JSON y-axis, so JSON `+y` becomes runtime `-y`; x keeps its sign. In the current layout, center x spans `-19.554..19.554` and center y spans `-8.814..9.529`.

Both angular fields use degrees with zero at `+x` and positive angles counter-clockwise in the runtime coordinate plane. `radius` spans `0.803..20.922` effect-layout units.

`penrose.Bounds` is an effect-layout convenience used by `ScreenEffect`. It is computed from iteratively rounded tile centers plus fixed asymmetric padding, then forced to center zero. It is not computed from mesh vertices and must not be interpreted as a Unity world-space bound. The current value is centered at `(0,0,0)` with size `(50,22,0)`.

### `TileData` fields

Each entry in `tiles` has exactly these nine fields:

| Field | Unit / domain | Meaning |
| --- | --- | --- |
| `center` | effect-layout units (`Vector2`) | Exact effect-facing tile center derived from the mesh after the y-axis flip. |
| `coarsePosition` | coarse integer position units (`Vector2Int`) | Lossy bucket computed component-wise as `(int)(rawCenter / 100 + 0.5)`. It is not a tile identity: 900 tiles occupy 846 distinct values. |
| `neighbors` | tile indexes plus unitless edge-class labels | Full-edge adjacency entries. Neighbor counts are `{1:6, 2:50, 3:36, 4:808}` tiles. |
| `section` | integer identifier `0..17` | Physical build section. Every section has 50 tiles, is connected, and the 18 sections form three spatial rows of six rather than radial wedges. |
| `edgeAndSeamDistance` | Neighbor steps | Shortest adjacency distance from the union of wall-edge tiles and tiles touching another section. Values/counts are `{0:371, 1:295, 2:182, 3:52}`. |
| `type` | `0` or `1` | Rhomb Type: `0` is fat (angles near 72/108 degrees, 566 tiles); `1` is thin (near 36/144 degrees, 334 tiles). |
| `tileangle` | degrees | Directed bearing of the tile's short diagonal. It is not a corner or interior angle. |
| `radius` | effect-layout units | Distance from the raw mesh origin to `center`. |
| `angle` | degrees | Polar bearing of `center`. |

`edgeAndSeamDistance` is not a concentric ring: its distance-zero set alone spans radius `0.803..20.922`, the full radial range of the current wall.

Every one of the 3,446 directed neighbor entries has a reciprocal entry and corresponds to a shared mesh edge. `neighbor.type` does not identify which perimeter edge is involved and is not the neighboring tile's Rhomb Type. It is a reciprocal edge-class label — the Penrose matching rules' "edge color" — where fat-fat edges carry `3`, thin-thin edges carry `2`, and mixed edges carry `4` or `5`. No runtime behavior currently interprets this label.

`GetRandomNeighbor()` makes one uniformly random selection from the existing adjacency entries. It does not retry or filter the selected entry.

### Packed Shape Lists

Several Effects use the ten packed Shape Lists under `penrose.Layout.shapes`, especially `TileShapes`, `Petals`, `AnimateLoops`, `ShapeGlitch`, `Mirror`, `Kscope`, and `Lightning`. Each named accessor, such as `penrose.Layout.shapes.Rings`, returns an allocation-free `LayoutData.ShapeList.Reader` over both the packed groups and their shared load-derived geometry facts; the packed arrays are private serialization details. Every list uses the same encoding:

```text
shape[0]                         = group count N
shape[1..N]                      = N absolute pointers into this array
shape[pointer]                   = tile count L for that group
shape[pointer + 1 .. pointer + L] = L direct tile indexes for that group
```

The first value is the number of groups, not the number of tile indexes. The pointer table is indirect; the payload of each pointed-to group is a direct ordered tile list.

| Shape List | Groups | Observed current-layout property |
| --- | ---: | --- |
| `Rings` | 73 | Closed Rings and wall-clipped open Arcs; serialized as `loops` by the data-file contract. |
| `Stars` | 45 | Every group is exactly five tiles, forms a closed adjacency cycle, and contains only fat rhombs. |
| `Lines0` | 7 | Line Ribbon family. |
| `Lines1` | 15 | Line Ribbon family. |
| `Lines2` | 17 | Line Ribbon family. |
| `Lines3` | 15 | Line Ribbon family. |
| `Lotusballs` | 49 | Lotusball groups. |
| `Starballs` | 32 | Starball groups. |
| `Mirror2` | 446 | Two-tile mirror groups covering 892 tiles. |
| `Mirror10` | 163 | Variable-size mirror groups that collectively cover all 900 tiles. |

One `Lines2` group repeats Tile `466` in consecutive positions. The raw group preserves that observed layout property; the reader's shared Line Ribbon position treats the repeated entry as one geometric Tile so the rest of the Ribbon keeps its intended normalized travel.

## Reacting to musical structure (Fill and Drop)

A **Fill** is a short musical transition in the tail of a Phrase, running from its marked start beat through the final beat. A **Drop** is the climactic Phrase of a track. An Effect can use direct state and Stock Envelopes from the BeatManager Data Surface for both. The following pattern comes from `Tunnel` and applies to an Effect with a value that moves continuously.

### 1. Advertise the capability

Override `Repertoire` so `TrackCueSheet.Build(...)` can assign this effect to ride through those Anchors:

```csharp
public override Repertoire Repertoire => Repertoire.HandlesFill | Repertoire.HandlesDrop;
```

Only an Effect advertises `HandlesFill` and `HandlesDrop` through its Repertoire. The Director uses these flags to put a capable Effect on the wall before an Anchor. The Effect then rides through the Anchor. An Anchor excludes the Runway and Tail of every `Transition`. `TransitionRepertoire` does not include Fill or Drop tags. An override can replace the baked Effect at runtime, so the baked Cast is a plan, not a guarantee.

### 2. Find the motion term, and never scale `effectTime`

Identify the one accumulator that drives the look (for `Tunnel`: `phase = (i * tileIndexPhaseStep + cyclePhase + fillScroll + dropScroll + radialPhase) % 1f`). `effectTime` is seeded with a large random offset (0–14400s), so multiplying `effectTime*speed` to "speed up" teleports the phase. Instead, keep a **separate bounded accumulator** per response and integrate the served cycle-phase advance into it each frame:

```csharp
fillScroll = Mathf.Repeat(fillScroll + (cyclePhaseAdvance * SyncSettings.FillScrollRateMultiplier * fillEnv), 1f);
dropScroll = Mathf.Repeat(dropScroll - (cyclePhaseAdvance * SyncSettings.DropReverseScrollRateMultiplier * dropEnv), 1f);
// phase = (i * tileIndexPhaseStep + cyclePhase + fillScroll + dropScroll + radialPhase) % 1f
```

`Mathf.Repeat(…, 1f)` keeps each accumulator in `[0,1)` so it never drifts. Fill adds (`+`), Drop subtracts (`-`) — make the two motions **opposite** so the Drop reads as an inversion of the Fill, not just "more of it."

### 3. Read direct structure values and envelopes

Fill and Drop each keep their raw countdown fields beside readable interpretations (`Active`, `BeatsRemaining`, `BeatsUntil`, and `Progress`). Their envelopes hang off two spans: `In` runs through the active event, `Before` approaches the next one across a window of whole beats that you must name. `Tunnel` uses `In` directly:

```csharp
fillEnv = beatManager.Fill.In.Build();
dropEnv = beatManager.Drop.In.Decay(SyncSettings.DropBars * 4);
```

Use `Before` for anticipation. It is total — resting as if the event were infinitely far, so `Before.Decay` reads 1 and `Before.Build` reads 0 whenever nothing is coming, including Standalone Mode. That makes `Before.Decay` safe to multiply straight into a delta with no null handling:

```csharp
localDelta *= beatManager.Drop.Before.Decay(8);   // slow down leading to the drop
```

`EffectBase` already packages that approach-and-enter slowdown two ways, so don't hand-roll it. To slow the Effect's whole clock, override `DropSlowdownBeats`; `UpdateTime()` then applies the slowdown while it integrates the frame, and `Draw()` sees an `effectTime`/`effectDelta` pair that is already correct:

```csharp
/// <summary>The bands slow their scroll over the eight beats leading into a Drop.</summary>
protected override int DropSlowdownBeats => 8;
```

To slow one local value instead — a synthesized delta, a speed multiplier — call `DropSlowdown(value)` directly and leave the clock alone:

```csharp
float localDelta = DropSlowdown(beatMode < 2 ? effectDelta + (0.05f * rhythm) : effectDelta);
```

Never rewind `effectTime` inside `Draw()` to retrofit a slowdown onto the clock. `UpdateTime()` has already integrated the frame by then, so the un-add is both redundant and easy to order wrong against values sampled earlier in the method.

#### Shape the effect around song sections

Song Structure exposes seven flat handles on BeatManager: `Intro`, `Up`, `Down`, `Verse`, `Bridge`, `Chorus`, and `Outro`. The wire's `unknown` phrase type gets no handle. Each handle uses the same `Before` and `In` spans as Drop and Fill:

```csharp
float chorusBuild = beatManager.Chorus.Before.Build(32); // rise into the next chorus
float outroFade = beatManager.Outro.Before.Decay(64);    // wind down toward the outro
```

`Before` requires a whole-beat window, and `In` defaults to the covering section's own length when no window is supplied. Both move continuously within each beat and shape their normalized position linearly: `Build` is the position and `Decay` is one minus it. On the final beat, `Before.Decay(window)` starts at `1 / window` and continues toward zero as the section approaches.

These handles follow the Focus deck's generation-gated Song Structure cursor automatically, so an Effect never handles nulls or deck changes. They are positional: Loops and needle-drops read from the cursor's current section, and `Before` targets the next ordinal occurrence — during a chorus, `Chorus.Before` means the following chorus. When no applicable structure or clock exists, the type does not recur, or BeatManager is in Standalone Mode, every envelope rests at zero except `Before.Decay`, which rests at one.

Read `Active`, `BeatsRemaining`/`BeatsUntil`, `LengthBeats`, and `Remaining` when the Effect needs wire facts. If an Effect needs an onset, retain its own prior `Active` value and compare locally; BeatManager deliberately exposes state, not one-frame event flags.

### 4. Acquire Waveforms explicitly

The base exposes the sibling `waveforms` root and neutral public `waveform` configuration, but it performs no acquisition. A concrete Effect chooses when and what to draw:

```csharp
public override void OnStart()
{
    waveform = waveforms.Random(Energy.Low, Energy.Mid);
}

protected override void OnNewGrid()
{
    waveform = waveforms.Random(Energy.Low, Energy.Mid);
}

// In Draw():
float brightness = waveform.Lerp(SyncSettings.BrightnessFloor, standaloneSettings.Brightness);
```

Transitions follow the same ownership rule: expose their own public artistic configuration when it should be tunable, acquire explicitly in their concrete lifecycle, and choose their own `Envelope`/`Lerp` mapping.

### 5. Fold envelopes into every consequence, and expose them

One envelope can drive several visual results (scroll **and** ring compression) so the gesture feels coherent: `ringCompression = 1f + FillRingCompression*fillEnv + DropRingCompression*dropEnv`. Make every magnitude a named, documented Sync Setting, and surface the live envelopes on `DebugText()` (`FILL 0.83`, `DROP 0.41`) so they can be tuned on the wall instead of by guessing.

### Keep artistic policy in the Performer

BeatManager and Waveforms provide shared musical facts, Edges, Stock Envelopes, and acquisition tools. The concrete Effect or Transition owns how those inputs affect color, motion, timing, fallback, and local state. Do not add automatic acquisition, replacement, or response policy to an authoring base. The existing `EffectBase.UpdateTime()` → `OnNewGrid()` and `OnNewPhrase()` hooks are narrow shared seams for the captured Grid wrap and Phrase start Edges; overriding either remains a concrete Effect decision.

A Mixer is still one Effect to the rest of the runtime. It privately owns its child Effects and can directly set their public artistic state, including sharing a held Waveform or assigning `waveforms.None`; it does not publish child policy as a second runtime system.

## Documentation expectations for new effects

Each new effect should include a class summary explaining:

- what it renders;
- which base class it uses and why;
- important data sources, such as tile geometry, shapes, palette, beat, or child effects;
- any intentional trails, persistent buffers, randomization, or performance tradeoffs.

Add comments for non-obvious math or mapping decisions. Avoid comments that merely restate assignments.
