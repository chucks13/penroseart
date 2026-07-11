# Wire facts are never dropped and never duplicated

BeatManager's surface had grown five spellings of some facts ("next beat" ×4, "on beat"
×6) as each interface generation re-wrapped the same wire data under new names — while
other wire facts faced the opposite failure: dropped because "nobody is using it."
Decided with Hunter (beat-data-interface effort, tickets "Beat position, countdowns, and
on-beat offerings" and "Pulse, off-beat, and subdivision offerings", 2026-07-10):

**The floor: never drop anything that comes over the wire.** Every lane the wire carries
is served — arrival is what earns a fact its place, and no lane is dropped for having no
current consumer. Unused lanes are inventory for future effects, not junk.

*Scope (Hunter, 2026-07-10):* the floor covers the musical `/rave/onair` lanes. The
deprecated `/rave/system/*` transport metadata is not served. The exclusion costs nothing
because absence is already first-class on the surface: any value that isn't there is
`null`, and effects supply their own defaults. The wire schema itself is defined by
`docs/osc-client-contract.md` (a synced copy owned by RaveSystem — never edited here).

**The ceiling: every datum is served exactly once.** The test: a value is **contrived**
when it is built from more than a single piece of wire data — a wire value plus anything
else (another lane, local state, time). A member that re-serves one wire value under a
second name is a duplication, not an offering, and duplications are cut. Translating wire
sentinels (`-1`) to the surface's uniform `null` is serving, not contriving.

**Two kinds of data, never two structures.** Raw and contrived describe where a value
comes from, not where a consumer finds it. The surface is organized by musical concept
only; each datum lives once, in the group its meaning belongs to, raw and contrived side
by side, indistinguishable at the point of use. Provenance is the provider's bookkeeping —
no consumer should care, or can tell, whether a value arrived on the wire or was
contrived.

*Amended 2026-07-10: the original wording placed single-lane facts "on the raw wire
view," a separate first-class consumer-facing structure. That separation was an agent
assumption, not Hunter's decision, and is retracted. Direct by-name access to every wire
fact survives — as ordinary members of their concept groups, not a distinct raw shelf.*

## Considered Options

- Per-fact convenience wrappers (`Bpm` alongside the wire's `bpm`) — rejected: they
  recreate exactly the many-spellings drift this effort exists to remove.
- A separate consumer-facing "raw wire view" — rejected: effects don't care where data
  comes from, and a provenance split leaks the provider's bookkeeping into every
  consumer's head.

## Consequences

- This ADR supplies the tests, not the verdicts: whether any particular member survives,
  merges, or is cut is decided per cluster with Hunter, recorded in the effort's tickets
  with original names and locations for git recoverability.
