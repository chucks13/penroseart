---
status: superseded by ADR-0013
---

# An Effect's Standalone look is fixed; its mechanism is not

Years of tuning went into how every Effect looks with no music, and the work now starting adds
musical response across the whole catalog, so each Effect carries its authored values as
**Standalone Defaults** and **Sync Defaults** at the top of its own file, where only editing that
file changes them, while its **Sync Settings** are a saved copy tweakable live on the wall and
resettable to the Sync Defaults at any moment. What is fixed is the Standalone *look*, not the code
behind it: an Effect's standalone mechanism may be restructured, renamed, or rewritten freely so
long as the wall renders the same. Effects and Mixers are hand-built first-class citizens and each
is fitted one at a time — a shape shared by several Effects is welcome wherever they genuinely want
the same one, and what is forbidden is bending an Effect to fit a shape that does not suit it.

## Rejected

- **A frame-capture check proving the Standalone look is unchanged.** Effects are heavily
  randomized and `effectTime` carries a large random offset, so a repeatable comparison would need
  a fixed seed and a driven clock reaching into `EffectBase`. The look is judged by watching the
  wall instead. This concerns that one check and says nothing about tests anywhere else.
- **Editable Standalone values on an editor surface.** A value anyone can drift from a control
  panel is not a value that has been locked. Displaying them read-only is permitted and is nobody's
  obligation.
