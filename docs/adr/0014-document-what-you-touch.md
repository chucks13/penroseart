# Document what you touch

Agents read code and its doc comments every session but never write them unprompted, so
documentation only decays. RaveSystem fixes this by making a missing doc comment a compile
error; PenroseArt deliberately does not adopt that gate, because the other developer on
this project doesn't work that way. Decided with Hunter (2026-07-11): any symbol touched
or created must be documented in the language's standard form — C# XML doc comments on
the file/module, type, and public members, per the global documentation rules. The
obligation is scoped to the **symbol, not the file**: changing one method in an
undocumented file obligates documenting that method only. No retroactive sweeps — coverage
grows along the paths that actually change.

## Considered Options

- Compiler-enforced documentation everywhere (RaveSystem's rule) — rejected: it imposes
  the gate on a collaborator whose workflow doesn't include it.
- No rule (status quo) — rejected: documentation is consumed constantly and produced
  never, a pure loss over time.

## Consequences

- Standing references reach code without dedicated tickets: when the implement effort
  touches the BeatManager surface or the OSC parser, the lane definitions in
  `docs/osc-client-contract.md` / `docs/rave-onair-value-reference.md` must land as doc
  comments on the touched symbols as part of that work.
