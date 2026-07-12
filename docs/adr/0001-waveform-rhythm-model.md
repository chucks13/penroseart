# Waveform rhythm model

We replaced the seven hardcoded `beatVariant` integers with a data-driven **Waveform** model: a one-bar brightness envelope built from Humps merged end-to-end, described by a `sequence` of note-value tokens (`W H Q E S`) and a parallel `amplitude` string (`0–8`), plus per-Waveform `rounding` and phase `offset`. An always-running **Waveform Synthesizer** evaluates any spec against a live Bar Phase clock and returns `[0..1]`. Effects request rhythms inline, by Preset name, or by random draw from a curated Pool. Full model in `docs/waveform-system.md`; terms in `CONTEXT.md`.

## Humps are concatenated in time, never summed
A Waveform is an ordered run of Humps end-to-end — not overlapping waves added together. This keeps it an envelope (always `[0..1]`, trough at 0) rather than a signal, and makes evaluation a simple "which slot is the playhead in." The web "designer" app suggested an additive/half-cycle mental model; we explicitly rejected that. The app is a **Visual Tool** only — the runtime does not depend on it or its JSON.

## Amplitude is one digit `0–8`, and `0` is the gate
`÷8` gives nine clean eighth-steps landing exactly on `1.0`, and one digit per Hump keeps amplitude readable directly beneath the sequence as an equal-length string. There is **no separate gate or rest token**: Amplitude `0` = a silent Hump = a skipped beat (`8000` = measure start). Rejected: a separate on/off mask, or floats — both break the stacked single-character-per-Hump readability.

## Sixteenth is the fastest width; 32nd+ excluded
Not a notation limit — a safety limit. A 32nd note at typical tempo strobes the **entire wall** at ~17 Hz (flicker/seizure hazard) and is musically unneeded for this installation. The exclusion is deliberate, not an oversight.

## The Pool is a hand-editable text file in StreamingAssets, drawer-owned
Persisted as `penrose_waveforms.txt` in the `palettedata.txt` style (`DEFINE_WAVEFORM(name){ seq | amp | round | offset }`), read at runtime by raw C# in `BeatManager`. Chosen over a scene-serialized field or JSON so that (a) a human can author Waveforms by hand in any text editor, and (b) the Editor authoring side and the runtime synth side stay decoupled through a file.

The property drawer **owns** the file: load parses what it can, save rewrites the whole file canonically. We rejected comment-preserving merge and append-only modes as overcomplication — the cost is that hand-authored comments/formatting do not survive a drawer save. Accepted because the drawer is the primary editor and hand-editing is the bootstrap/fallback path.

## Malformation is logged, not substituted
A malformed spec (widths not summing to a bar, mismatched string lengths) is logged at load and otherwise tolerated — Bar Phase bounds every evaluation to one bar, so the worst case is one odd-looking bar that self-corrects on the next downbeat. We rejected silently falling back to the Beat Pulse: silent substitution hides authoring mistakes, and the failure is cheap.

## Consequences

- Call sites barely change: `BeatManager.GetBeatBrightness(...)` keeps its shape and swaps internals; `GetRandomVariant` becomes "draw a Waveform from the Pool." The ~18 effect call sites are untouched.
- Random selection by song energy/direction (OSC `energy_state`) is now a natural extension point on `GetRandomVariant`, but is deferred — the incoming OSC data isn't finalized.
- The visualization is bound to the synthesizer's `Evaluate`, so the drawer plot cannot drift from runtime behavior.

### Amendment (2026-07-11, Hunter, beat-data-interface effort)

The first two Consequences bullets are superseded — call sites do change, because a
better shape exists and preserving the old one was never the point. The synthesizer is
its own readable surface beside BeatManager (the base `synth` property beside
`beatManager`) offering **one evaluation primitive**: the envelope of a given Waveform at
the current Bar Phase, nullable when no clock runs. The provider-side conveniences
`GetBeatBrightness`/`GetBeatTime` are retired; brightness and time seasoning live
effect-side as the canonical base helpers, closing over the effect's held Waveform —
seasoning belongs to the only layer that knows it, and one primitive instead of three
spellings deepens the module. The deferred energy extension arrived as the Energy-set
draw (`Random(params Energy[])`): a Waveform's Energy is derived from its notation
(max of density tier and gap tier), never authored or stored. **Index addressing is
retired in every form** — effects hold Waveform *values*, acquired by draw, by Preset
name, or inline; a Pool position may change at any time. Everything else in this ADR
stands: the notation model, the Pool file, malformation handling, and the sixteenth
safety limit (re-affirmed on its own merits as an authored full-wall strobe hazard).
Vocabulary: CONTEXT.md (Waveform Synthesizer, Energy, Waveform Hold); contracts:
ADR-0012, ADR-0013, ADR-0015, ADR-0016.

### Amendment (2026-07-12, Hunter, Performer ownership correction)

The 2026-07-11 amendment's canonical base helpers and base-held Waveform are superseded by ADR-0017. Brightness/time mapping remains effect-side, but each concrete Effect or Transition owns its Waveform, acquisition timing, fallback, and explicit response math; no authoring base holds rhythm state or automatically acquires or replaces it. The synthesizer still offers the same acquisition and evaluation tools without deciding how any Performer uses them.
