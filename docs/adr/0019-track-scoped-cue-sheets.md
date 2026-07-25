# Track-scoped Cue Sheets with baked-in assignments drive Synced Mode

Status: accepted

The track-scoped Cue Sheet, its deterministic builder, and selection-behind-the-builder all stand. The Director/Switcher split below does not: "Position is wire-only" and "Decide-at-cast at the last responsible moment" describe a Director that times casts from Runway arithmetic, which ADR-0020 replaces with a handed-over sheet the Switcher executes.

The per-player OSC surface (schema 4) delivers a track's complete song structure the moment it loads on any player, so the Director no longer has to plan reactively from the current and announced-next Phrase. We decide that Synced Mode is driven end to end by a track-scoped **Cue Sheet**: on each track load, `TrackCueSheet.Build` produces one complete, full-length show plan — every Cue Mark placed against the real Phrase map with its Effect and Transition assignment baked in — as a pure, deterministic function of (structure, seed, catalogs), seeded by (structure generation, player number). The Director holds one sheet per physical player (six slots, no cache), follows the on-air focus player's sheet by pure wire position, and Casts its Cue Marks to the Switcher fire-and-forget. This redefines "Cue Sheet" from the phrase-scoped index of empty Cue Marks (ADR-0005, ADR-0011) to a track-scoped plan with assignments baked in at build time; the reactive phrase machinery is deleted, not kept as a fallback.

## Position is wire-only; there is no self-ticked count

The Director reads position solely from the wire: the live-order lane for the on-air focus, that player's absolute beat, and the on-air Grid. A Cue Mark is Cast when the focus position resolves to a mark different from the last Cast — so focus handover, needle-drop, and loop exit are one mechanism (the next lookup lands in a different segment or sheet and a normal Cast takes over), and a loop pinned inside one segment simply never changes the target. The old Director drifted precisely because it counted beats itself; this design keeps no beat count anywhere. Worst case a stale effect plays for up to one Grid (16 beats) after a jump — accepted.

## Decide-at-cast at the last responsible moment

From the focus position the Director knows the next Cue Mark and its Transition's Runway, hence the exact beat the Runway must begin. When the focus beat reaches that beat it reads the then-current focus sheet and Casts. There is no pending cue, no lock protocol, and no revocation window: the Switcher's contract narrows to one sentence — take a cast cue and execute it at its beats (Runway start, Impact on the mark, Tail after), unconditionally. A late cast fires as a compressed Runway with Impact still on the mark.

## Selection lives entirely behind the sheet builder

The single new creative seam is `TrackCueSheet.Build`. Grid walk, the two seeded shuffle bags (one over the Effect catalog, one over the Transition catalog), every Anchor flip, energy-fit, drop/fill Anchor ownership, and post-drop hold all live behind it. The Director builds descriptor lists from the same Factory/deck catalogs and indices the Switcher uses, so a baked mark index means the same performer everywhere. Determinism replaces caching: rebuilds are a microsecond pure walk, so a slot rebuilds whenever that player's structure generation changes (inequality only, never ordering); Track ID plays no role in identity or seeding.

## Considered options

- **Keep the reactive current/next Cue Sheet machinery and layer track scope on top** — rejected: the whole reactive posture (repair-by-invariant, the cast-early/lock protocol, cast-time selection) existed only because the Director could not see the whole track. With the complete structure in hand it is dead weight, and the spec's simplicity mandate is to prefer the decision that deletes code.
- **Transition at every Phrase change** — rejected: it puts a crossfade exactly where the fill peaks and the drop lands, undercutting the drop/fill support built into Effects. Phrase boundaries are now preferred mark positions, not mandates, and drops/fills are first-class planning Anchors owned by a capable performer.
- **A pending-cue lock protocol on the Switcher** — rejected: fire-and-forget with decide-at-cast needs no revocation. If a future effect ever needs warm-up frames the fix is "cast one beat early," never a lock protocol.

## Consequences

- Deleted with their tests: the Director's reactive phrase machinery (phrase/next-phrase lane consumption, current/next sheet slots, repair-by-invariant, the cast-early/stage-next and preferred-repertoire cast-time paths) and the Switcher's loaded-cue lifecycle (`UpsertLoadedCue`, lock latching, `CanCommitCue`, `CueUpsertResult`, `SwitcherCueStatus`, the loaded/active cue surface). The phrase-scoped `CueSheet` builder is deleted once its walk is absorbed by `TrackCueSheet`.
- The Switcher's public contract is `Cast` (plus the Standalone seconds path). It holds no cue lifecycle, so callers never mirror or guess commitment.
- Standalone Mode (timer-driven, no wire) is untouched, and Effects' own live drop/fill reactions via the Data Surface are untouched.
- Editor surfaces that visualized the phrase-scoped world and the loaded-cue window (Live Timeline, Tuning window) render a degraded view; new track-sheet visualization is a later feature.
- Supersedes the phrase-scoped empty-Cue-Sheet halves of ADR-0005 and ADR-0011; narrows ADR-0008 (its Synced-mode synthetic-phrase role is gone — Standalone is the only timer-driven fallback). ADR-0017 (performers own artistic decisions) is unchanged — Effects and Transitions still own how they respond to the musical facts they read. That an override **masks** the plan rather than mutating it is this ADR's own rule, not ADR-0017's: it follows from the sheet being a pure function of (structure, seed), so nothing an operator does can rewrite a built plan. ADR-0020 refines where the mask is applied (when the Director answers, not when the sheet is built).
