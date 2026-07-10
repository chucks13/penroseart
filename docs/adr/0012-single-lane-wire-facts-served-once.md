# Single-lane wire facts are served once, on the raw wire view

BeatManager's surface had grown five spellings of some facts ("next beat" ×4, "on beat"
×6) because each interface generation re-wrapped the same wire data under new names. We
decided (beat-data-interface effort, ticket "Beat position, countdowns, and on-beat
offerings", 2026-07-10) that a value is **contrived** only when it is built from more
than a single piece of wire data — a wire value plus anything else: another lane, local
state, time — and that a fact arriving as a single wire value is served exactly once, on
the Data Surface's raw wire view, never re-wrapped as a named query; translating wire
sentinels (`-1`) to the surface's uniform `null` is serving, not contriving. This ends
the many-spellings drift at the rule level: named offerings in the concept groups are
contrived by definition, and consumers wanting per-label detail read the wire's arrays
directly.

## Considered Options

- Per-fact convenience wrappers (`Bpm` alongside the wire's `bpm`) — rejected: they
  recreate exactly the many-spellings drift this effort exists to remove.

## Consequences

- This ADR supplies the test, not the verdicts: whether any particular member survives,
  merges, or is cut is decided per cluster with Hunter, recorded in the effort's tickets
  with original names and locations for git recoverability.
