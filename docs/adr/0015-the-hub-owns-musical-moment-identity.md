# The hub owns musical moment identity

Status: superseded by ADR-0018

OSC delivers musical events as incrementally updated state, so every effect hand-rolled its
own edge detection and response curves — the drop latch existed five times (only one handling
mid-event activation correctly), the fill envelope constants three times. Decided with Hunter
(2026-07-11): BeatManager owns moment identity — **Edges** (Span started/ended, labeled state
changed, new Grid, gate openings) served as polled, frame-coherent, payload-free reads, true
during exactly the frame the hub observed the moment — and serves **Stock Envelopes** (Build
and Decay over any Span, duration in beats defaulting to the Span's length). The hub runs
continuously across effect swaps, so it genuinely witnesses each onset; per-effect latches and
their arming bugs stop existing, while state stays pullable and hand-rolled artistic response
stays fully free.

## Considered Options

- **C# events / subscriptions** — rejected: the surface's only push seam, with handler
  lifecycle landing in effects exactly where they are simplest today.
- **Monotonic event counters** — rejected: they solve "did I miss it while off stage," which
  is not an artistic need; an onset matters only to whoever is reading when it fires.
- **Status quo (per-effect edge detection)** — rejected: five duplicate drop latches, four of
  which false-flash when activated mid-drop.

## Consequences

- Two-kind nullability refines ADR-0012 without changing it: facts stay nullable; Edges are
  never null (`false` is the complete "nothing this frame" answer); envelopes are never null
  (`0` is the resting curve). Effects wire signals straight into rendering math and branch
  only where a real default choice exists.
- Edges evaluate once per hub update, ahead of effect `Draw()`, so every reader within a
  frame gets the same answer; boundaries are not queued — at most one edge per frame.
- `EffectBase`'s private grid latch and `OnNewGrid()` hook and the Director's own grid
  watching re-point onto the hub's edges during the implement effort.
