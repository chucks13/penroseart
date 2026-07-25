# Architecture Decision Records

**Doctrine: everything serves the performers.** Effects and Transitions are the product; every other
module exists to inform them. Modules inform, never restrict or command. BeatManager is the single
musical source — it reads the wire and contrives derived values — and performers interpret those
musical facts for themselves. No module re-derives a musical fact BeatManager already serves.

Format authority is the domain-modeling skill's `ADR-FORMAT.md`, never a neighbouring ADR. Every ADR
carries a `Status:` line directly under its title: `proposed | accepted | deprecated | superseded by ADR-NNNN`.
A superseded ADR is kept for the record of why the thing existed — never deleted, never edited to look current.

## Subsystem index

### BeatManager / rhythm — `Assets/core/Rhythm/`

The one musical source. Reads the Rave OSC wire, contrives derived values, and serves both through one
shallow immutable surface.

| ADR | What it governs |
| --- | --- |
| [0018](0018-beat-manager-exposes-shallow-musical-values.md) | **Current surface shape.** One shallow immutable surface grouped by meaning. Supersedes the doorway/Span/Edge hierarchy. |
| [0012](0012-data-sources-serve-immutable-nullable-data.md) | Published data is immutable and availability-honest. **Governs BeatManager Data Surfaces only** — its 2026-07-25 scope section says so explicitly. |
| [0013](0013-single-lane-wire-facts-served-once.md) | Never drop a wire lane; never serve a datum twice. |
| [0002](0002-nullable-contrived-rhythm-queries.md) | `null` means "not available", sentinels never cross the surface, consumers own their Standalone response. |
| [0010](0010-wire-fed-timing.md) | Grid and Phrase truth come from the wire, not local synthesis. |
| [0007](0007-standalone-mode-source-of-truth.md) | The running 4-count (`IsSynced`) is the single Standalone/Synced mode authority. |
| [0009](0009-cyclic-grid-vs-song-phrase-naming.md) | The cyclic phase-keeping unit is **Grid** (nominally 4 bars of 4, but phrase-relative — never assume 16 beats); song structure is **Phrase**. Wire vocabulary is law at the surface. |
| [0001](0001-waveform-rhythm-model.md) | The Waveform notation model and its hand-editable Pool file. |
| [0015](0015-the-hub-owns-musical-moment-identity.md) | *Superseded by 0018.* Hub-owned Edges and Stock Envelopes. |
| [0006](0006-on-air-timing-phase-determiner.md) | *Superseded by 0011.* The locally determined Grid/Phrase, before the wire carried it. |

Terms: Grid, Grid Beat, Grid Boundary, Phrase, Bar Phase, Waveform, Hump, Routine, Energy, Levels,
Drop, Fill, Loop, Standalone Mode, Synced Mode, Contrived Value.

### Director — planning and casting — `Assets/core/Switching/Director.cs`, `TrackCueSheet.cs`

Decides *what* performs. Builds one track-scoped Cue Sheet per player at track load, hands the on-air
player's sheet to the Switcher, and answers the Switcher's questions. It times nothing.

| ADR | What it governs |
| --- | --- |
| [0019](0019-track-scoped-cue-sheets.md) | **What a Cue Sheet is.** One deterministic full-length plan per player, Effect and Transition baked in, a pure function of (structure, seed). All selection lives behind `TrackCueSheet.Build`. |
| [0004](0004-director-switcher-sequencing-dual-mode.md) | The Director/Switcher split itself, and beat-denominated timing. |
| [0011](0011-wire-change-director.md) | **Position comes from the wire; nothing keeps a self-ticked count.** Its 2026-07-25 note also records that energy casting was removed a second time — affinity flags stay on Performers as declarations with no casting consumer. |
| [0005](0005-phrase-cue-sheets-and-armed-cues.md) | *Superseded by 0019.* The phrase-scoped sheet and the Loaded → Locked → Executing lifecycle. |
| [0008](0008-synthetic-phrase-cue-fallback.md) | *Superseded by 0010.* The synthetic 64-beat phrase window. Historical only. |

Terms: Cue Sheet, Cue Mark, Anchor, Cast, deal / shuffle bag, structure generation, focus player,
minimum gap (16 beats), maximum gap (64 beats / 4 Grids), Repertoire, Deck.

### Switcher — cue execution — `Assets/core/Switching/Switcher.cs`

Decides *when*, and performs. Owns Runway, Impact Point, and Tail. Selects nothing — every decision is
asked of the Director.

| ADR | What it governs |
| --- | --- |
| [0021](0021-fire-math-follows-the-selected-transition.md) | **Current.** Fire math follows the selected Transition (peek before due-beat arithmetic, so Impact always lands on the mark; a late override waits for the following mark). The run-time ceiling measures **stillness**, anchored at cue start. |
| [0020](0020-the-switcher-executes-a-handed-over-cue-sheet.md) | *Superseded by 0021,* but the record of what survives: permanent check-offs, the Off-Plan Cue, commands down and questions up, Show Now as a performed move. Its amendments reverse its own body — read it as history. |
| [0019](0019-track-scoped-cue-sheets.md) | The handover itself and fire-and-forget. |
| [0004](0004-director-switcher-sequencing-dual-mode.md) | Beat-denominated Runway/Tail, hard cuts, last-command-wins. |

Terms: Runway, Impact Point, Tail, check-off, Off-Plan Cue, Grid Boundary count
(`boundariesSinceCue` — stillness, run-time), Show Now, Hold, override mask.

Note the two distinct gap rules: the Director's **plan-time** `TrackCueSheet.MaximumGapBeats` (64) bounds
gaps while a sheet is built; the Switcher's **run-time** stillness ceiling is the loop backstop for what
the plan cannot answer. They are not the same rule.

### Cue Log — `Assets/core/Runtime/CueLog.cs`

No ADR governs it. It is the narration surface implied by
[0011](0011-wire-change-director.md)'s "no decision memory" ruling — the Director records no verdicts,
so the trace log is where what happened is written down. As an observer it is bound by
[0016](0016-editor-surfaces-never-originate-runtime-state.md): it follows the runtime and never shapes it.

### Effects, Transitions, Repertoire — `Assets/core/Effects/`, `Assets/core/Transitions/`, `Assets/effects/`, `Assets/transitions/`

The performers. Everything above exists for them.

| ADR | What it governs |
| --- | --- |
| [0017](0017-performers-own-artistic-decisions.md) | **Performers own artistic decisions.** Modules provide facts and tools; Effects and Transitions decide what to do with them. A Mixer is one Effect outward and owns its children inward. |
| [0004](0004-director-switcher-sequencing-dual-mode.md) | Repertoire as a `[Flags]` declaration discovered by `Factory<T>` reflection; the core/editor two-layer rule. |
| [0011](0011-wire-change-director.md) | Energy affinity flags are **declared, not consumed** — no planner infers casting from them. |
| [0016](0016-editor-surfaces-never-originate-runtime-state.md) | Runtime → editor, never editor → runtime. |
| [0001](0001-waveform-rhythm-model.md) | Waveform acquisition and response live performer-side. |

Terms: Performer, Effect, Transition, Mixer, Blender, Repertoire, Energy affinity, Hold.

### OSC boundary — `Assets/OSC/`, `Assets/core/IO/RaveOscReceiver.cs`

| ADR | What it governs |
| --- | --- |
| [0003](0003-vendored-ravesystem-osc-boundary.md) | `Assets/OSC/*.cs` is a vendored generic `RaveSystem.Osc` library. Penrose policy goes in `Assets/OSC/Rave/` or core consumers — never into the vendored files. State which of the three change categories applies before editing, or stop and ask. |
| [0009](0009-cyclic-grid-vs-song-phrase-naming.md) | Wire field names are carried verbatim to the surface. |
| [0013](0013-single-lane-wire-facts-served-once.md) | The `/rave/onair` lanes are the served scope; `/rave/system/*` is not. |

### Hardware output — `Assets/core/Hardware/`, `Assets/core/IO/`

No ADR governs it. Authority is the root `AGENTS.md` (serial is the active compiled output path; ask
before changing any hardware or control path) and `Assets/core/Hardware/S2_MINI_PROTOCOL.md` for the
wire protocol itself.

## Cross-cutting rules

| ADR | What it governs |
| --- | --- |
| [0014](0014-document-what-you-touch.md) | Any symbol you touch or create gets XML doc comments. Symbol-scoped, no retroactive sweeps. |
| [0016](0016-editor-surfaces-never-originate-runtime-state.md) | An editor-surface need is never a design driver for the core. |
| [0012](0012-data-sources-serve-immutable-nullable-data.md) | Published data is immutable — but this governs *published* data only, not working state two tightly coupled collaborators share. |

## Status at a glance

Accepted: 0001, 0002, 0003, 0004, 0007, 0009, 0010, 0011, 0012, 0013, 0014, 0016, 0017, 0018, 0019, 0021.

Superseded: 0005 (by 0019), 0006 (by 0011), 0008 (by 0010), 0015 (by 0018), 0020 (by 0021).
