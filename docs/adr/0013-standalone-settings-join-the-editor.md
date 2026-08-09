---
status: accepted
supersedes: ADR-0012
---

# Standalone Settings join the editor; Standalone Defaults stay the authored record

ADR-0012 rejected editable Standalone values on any editor surface so that nothing could drift the
Standalone look while the whole catalog was restructured under #111. That conversion is complete:
every Effect and Mixer carries its authored values as named Standalone Defaults and Sync Defaults
in its own source file. The guard has done its job, and the musicality work — tuning every Effect
against the moving wall — needs Standalone reachable the same way Sync is. Each Effect therefore
gains a Standalone Settings asset with the same contract as its Sync Settings asset: serialized,
live-tweakable in Play Mode from the Effects tab, persisting after the run, and restorable at any
moment to the Standalone Defaults, which remain in source as the one authored record of the look.

## Carried forward from ADR-0012

- An Effect's mechanism stays free. Effects and Mixers are hand-built first-class citizens, fitted
  one at a time; a shared shape is welcome where Effects genuinely want the same one, and bending
  an Effect to a shape that does not suit it is forbidden.
- The look is judged by watching the wall, never by a frame-capture check. Effects are heavily
  randomized, so a repeatable comparison would need a fixed seed and a driven clock; that check
  stays rejected, and this says nothing about tests anywhere else.
