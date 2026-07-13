# Data sources serve immutable, availability-honest data

Any system that makes data available to other systems serves it **immutable** (reads cannot write), **availability-honest**, and **unrestricted** (consumers may read and combine it freely). `null` is appropriate when absence is a real, ordinary state the caller may need to distinguish, as with optional wire facts. A usable signal may instead expose a total response with a documented rest/default, while required configuration fails visibly when absent. This corrects the 2026-07-10 decision's overgeneralization of ADR-0002: its nullable-query pattern belongs to optional musical facts, not every value or API in the application.

## Considered Options

- Mutable shared state and sentinel defaults (the public `beatData` field with `-1` sentinels) — rejected: write-back and sentinels leaking into effect math were the observed diseases.
- Universal nullability — rejected after the Waveform migration showed that it forced every caller to repeat fallback syntax for a value that was required and already had meaningful response defaults.
