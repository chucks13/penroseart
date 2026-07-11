# Document what you touch

Agents read code and its doc comments every session but never write them unprompted, so
documentation only decays. RaveSystem fixes this by making a missing doc comment a compile
error; PenroseArt deliberately does not adopt that gate, because the other developer on
this project doesn't work that way. Decided with Hunter (2026-07-11): any symbol touched
or created — public or non-public, production or test — must be documented in the
language's standard form: C# XML doc comments, per the global documentation rules. Agents
get lost precisely in private helpers, internal seams, and test code, so visibility does
not limit the obligation. The obligation is scoped to the **symbol, not the file**:
changing one method in an undocumented file obligates documenting that method only.
Creating a file counts as touching it, so every new file also gets a `//` file-purpose
comment at the top, readable before any type is opened. No retroactive sweeps — coverage
grows along the paths that actually change.

## Considered Options

- Compiler-enforced documentation everywhere (RaveSystem's rule) — rejected: it imposes
  the gate on a collaborator whose workflow doesn't include it.
- No rule (status quo) — rejected: documentation is consumed constantly and produced
  never, a pure loss over time.
- Public surface and production code only — rejected: private helpers and the test corpus
  are most of what an agent reads when orienting, and a test name alone doesn't state the
  scenario under test or the asserted outcome.

## Consequences

- Touched tests are documented with the scenario under test and the asserted outcome.
  `<inheritdoc/>` satisfies overrides and interface implementations but is not a stand-in
  for real test documentation.
- Documentation is repaired, never deleted, to silence a doc warning (bad cref, misplaced
  comment) — fix the cref, placement, or param docs instead.
- Standing references reach code without dedicated tickets: when the implement effort
  touches the BeatManager surface or the OSC parser, the lane definitions in
  `docs/osc-client-contract.md` / `docs/rave-onair-value-reference.md` must land as doc
  comments on the touched symbols as part of that work.
