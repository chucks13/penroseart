# Data sources serve immutable, nullable data

Any system in this application that makes data available to other systems serves it
**immutable** (reads cannot write — data flows one way, from source to consumer),
**nullable** (`null` is the ordinary spelling of "not there right now"; every consumer
chooses its own response to a missing value), and **unrestricted** (any consumer may read
anything and combine it freely). Decided with Hunter during the beat-data-interface
effort (2026-07-10): BeatManager is the first instance — all wire facts and all contrived
values pulled through the one gateway under this contract — the Waveform Synthesizer is
its sibling under the same contract, and future surfaces (the palette, and whatever
comes after) land on it instead of re-litigating the shape. This generalizes ADR-0002's
nullable-query pattern from one surface to an application-wide contract.

## Considered Options

- Mutable shared state and sentinel defaults (the public `beatData` field with `-1`
  sentinels) — rejected: write-back through the query surface and sentinels leaking into
  effect math were the observed diseases; immutable one-way flow with nulls makes
  availability explicit at every read.
