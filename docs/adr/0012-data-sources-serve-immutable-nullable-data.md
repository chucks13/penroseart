# Data sources serve immutable, availability-honest data

Status: accepted

A **data source** — a system whose job is to publish observations for any consumer to read, as BeatManager publishes musical truth — serves them **immutable** (reads cannot write), **availability-honest**, and **unrestricted** (consumers may read and combine them freely). `null` is appropriate when absence is a real, ordinary state the caller may need to distinguish, as with optional wire facts. A usable signal may instead expose a total response with a documented rest/default, while required configuration fails visibly when absent. This corrects the 2026-07-10 decision's overgeneralization of ADR-0002: its nullable-query pattern belongs to optional musical facts, not every value or API in the application.

## Scope (2026-07-25)

This governs *published* data, not every object one system hands to another. Two collaborators may share working state: the Cue Sheet the Director hands the Switcher carries a mutable fired flag on each Cue Mark, because the mark is the natural place to record that it fired and the two systems are deliberately tightly coupled — the Switcher exists to execute the Director's plan. Read as originally written, this ADR forbade that, which it was never meant to. BeatManager is the data source the rule is for: its consumers are arbitrary and must not be able to write back.

## Considered Options

- Mutable shared state and sentinel defaults (the public `beatData` field with `-1` sentinels) — rejected: write-back and sentinels leaking into effect math were the observed diseases.
- Universal nullability — rejected after the Waveform migration showed that it forced every caller to repeat fallback syntax for a value that was required and already had meaningful response defaults.
