# Cue Marks are spread by a rising-hazard walk over the whole track

Status: accepted

Cue Marks clustered — four changes a Grid apart, then a long hold — because the builder walked
**each Phrase independently and had to land exactly on the Phrase end**, which truncated the gap
draw as the walk ran out of Phrase, and because that draw was **uniform over one to four Grids**, so
a quarter of all gaps were the 16-beat minimum with nothing resisting a run of them. For a 64-beat
Phrase the truncation alone forced a 16-beat final gap half the time. We decide that Cue Marks are
placed by **one walk over the whole track's Grid Boundaries, counted in beats**, where each candidate
boundary is taken with a probability that rises with the gap behind it — and that a mark is **never
forced** onto a boundary to make the walk land somewhere. Phrase ends lose all special status; they
are ordinary candidates, which is what ADR-0019 already said they were.

## The rule

Grid Boundaries are the candidate set — derived from the Phrase map, since the Grid count is
phrase-relative. Everything else counts beats, so no Phrase length can distort the cadence:

| Gap behind the candidate | Probability it is taken |
| --- | --- |
| < 16 beats | 0 (the floor — this is what stops clustering) |
| 16 beats | 0.08 |
| 32 beats | 0.35 |
| 48 beats | 0.65 |
| ≥ 64 beats | 1.00 (the ceiling — this is what stops drift) |

Mean spacing lands near 43 beats. `MinimumGapBeats` and `MaximumGapBeats` are unchanged at 16 and 64,
so the plan-time bounds the Switcher relies on still hold; only the distribution between them changes.

Drop and Fill landing beats are **pinned** — they are marks regardless of the walk, because they are
the moments a capable performer exists to show off. At a pinned Anchor, Ride-through is preferred at
**0.75** rather than the fair coin it was, so the incumbent usually plays the moment through.

## Considered options

- **Keep the uniform draw and add a repeat penalty** — rejected: it treats the symptom. The dominant
  cause was the per-Phrase walk forcing a mark onto the Phrase end, and no penalty on the draw can
  undo a gap the geometry made mandatory.
- **Stratified / jittered placement** (divide the track into equal slots, jitter one mark inside each)
  — rejected: it fixes the mark count up front, which fights the Anchor pins and makes the 16/64
  bounds emergent rather than enforced. The hazard enforces both bounds by construction.
- **Weighting the draw toward the middle without a hazard** — rejected: equivalent in distribution but
  it cannot express the ceiling, which has to be certainty at 64 beats, not a high probability.

## Consequences

- Determinism is unchanged: same seed pair, same single roll stream, same byte-identical sheet per
  (structure generation, player number). This ADR changes the shape of the walk, not its seeding.
- Deleted with their tests: the per-Phrase walk, the mandatory Phrase-end mark, the irregular-tail
  run-out Grid, and the shorter-than-one-Grid special case that admitted its own "unavoidably short
  run-in".
- The same rising-hazard shape now governs both the plan-time walk and the Off-Plan deal
  (`DealOffPlanCueAt`), which previously carried its own uniform `1/boundariesLeft` rule. One cadence
  rule, two callers.
- No Phrase length is assumed anywhere — not 32 beats, not a multiple of 16, not at least one Grid.
  Short Phrases simply mean more candidate boundaries get skipped by the floor.
- `CONTEXT.md`'s **Cue Mark** entry drops "plus the final beat of a Phrase": marks now sit on Grid
  Boundaries and nowhere else.
