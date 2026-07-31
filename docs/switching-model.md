# The Switching Model

The model of the switching product: how the wall changes, and who decides, while a
live DJ plays (sync mode). Written from Hunter's head on 2026-07-31 with the old
docs closed. This document is the single source of truth for switching behavior;
older text that disagrees must justify itself against this or die.

## The players

Think of it as a stage play.

- **Performers** are the effects and transitions. They are first-class citizens:
  each decides what it does and how, and advertises what it supports through its
  **repertoire**. No other code restricts a performer. Effects perform their own
  show — a drop-capable effect watches the music through the BeatManager and does
  its own show-off move when the drop hits; nobody cues it. Effects likewise decide
  their own standalone-mode behavior.
- The **Director** directs. It reads each track's structure, selects performers by
  repertoire, and writes the **cue sheet** — one per track load, per player. The
  Director always does the casting; nobody else ever picks an effect or transition.
- The **Switcher** switches. It takes the cue sheet for the on-air player and
  executes it, on time, by the rules below. It never casts.
- The **BeatManager** is the single, read-only musical source. Everything musical —
  beats, bars, grids, phrases, tempo, loop state, on-air state — is pre-computed on
  the OSC wire (see `docs/osc-client-contract.md`) and read through the BeatManager.
  No module tracks music on its own or reads the wire directly; that habit is how
  things drift out of sync.

## The timeline

Everything is counted in beats, musically: 1-2-3-4, no beat zero.

- 4 beats = a **bar**; 4 bars = a **grid** (16 beats).
- Every track has a total number of beats with **phrases** laid over it (intro, up,
  down, chorus, bridge, outro, …). The grid restarts at every new phrase. An
  irregular phrase means a short grid at its end — known at track load.
- Two phrase events matter today (more later): a **drop** — a key moment, the
  build-up to something great — and a **fill** — a short piece at the end of a
  phrase/grid that separates into the next part.
- The **on-air grid from the wire is the timing authority.** Anything the DJ does is
  represented in it. Beats are watched for one thing: a **loop** shows up as the
  beat counter snapping back (…190, 191, 160, 161…). The wire's loop flag
  corroborates but is not the signal.

## Marks and transitions

- A **cue mark** sits on a grid boundary. It means: something happens crossing this
  boundary.
- A transition is an A-to-B blend with a **runway** (beats before the boundary) and
  a **tail** (beats after). Runway + tail ≤ 12 beats. The blend runs from the start
  of the runway to the end of the tail, crossing the boundary.
- The Switcher starts the blend at boundary minus runway. Once started, a transition
  is **fire and forget**: it runs to completion no matter what — even if the cue
  sheet changes mid-flight.

## The Director's job

At track load, the Director sees the whole track's structure and writes the cue
sheet:

- Plant marks at sensible, irregular spacing — never clumped, never metronomic — and
  never leave more than 64 beats (4 grids) without a transition.
- Fill each cue from the sheet's own **shuffled bag** of effects, matched by
  repertoire. Show as much of the catalog as the track allows; repeats are
  tolerated, not habitual. Never write a transition from an effect into itself.
- Around a drop or fill: a repertoire-supporting effect must already be on the wall,
  and no transition's runway or tail may cross it. The effect shows off; the
  Director's whole contribution is casting and clearance.
- On a short grid, cast a transition whose runway fits. All of this is knowable at
  track load.

The Director keeps a cue sheet for every loaded player and hands the Switcher the
sheet of the **latest player to come on air** — the only sheet that matters. (The
wire lists live players most-recent-first, up to six.)

## The Switcher's job

The Switcher thinks once per grid, at the grid's start, from BeatManager state:

- If the next boundary carries an unfired mark, fire its transition at boundary
  minus runway. Mark it fired.
- A handover (a new sheet arrives) changes nothing on the wall by itself. The next
  change comes at a mark or at the stillness deadline.
- Marks skipped over by a forward jump simply lapse — no late firing.
- Anomalies go through **one doorway**: the Switcher tells the Director what it
  sees, and the Director decides — ride through, or here is a fresh cue
  (Director-cast, as always). The anomalies:
  - the mark at hand has already fired (looping),
  - the mark transitions into the effect already on the wall,
  - stillness is up.

## Stillness

**Stillness** is the time since the last fired cue — a property of the wall, not of
any sheet. It is measured in whole grids and checked at every grid start: three
grids since the last fire means the fourth grid must fire, short or not. A
Director-built sheet never violates stillness on its own; only sheet swaps and loops
can push the wall toward it, and the grid-start check catches both.

## Modes

- All of the above is **sync mode** (live DJ on the wire).
- **Standalone mode** is a separate, old, simple mechanism that works. This model
  does not describe it, and nothing here may reach into it or disturb it.
- Entering sync mode may change the effect instantly — mode flips are rare, and the
  change is acceptable.
