# Design: extract the Wall Variant Lock policy into BeatManager (deep, testable)

Status: Implemented — runtime core + tests landed; shared editor adapter deferred (decision 3)
Source: architecture review `docs/architecture-reviews/architecture-review-penroseart-editor-2026-06-17T2125.html`, Candidate 1

## Problem

The policy for *reading and changing what the wall is playing* lives as private statics
inside `Assets/Editor/Rhythm/BeatManagerDrawer.cs` (`TryGetLiveWall`, `ApplyWallSelection`,
`ResolveDisplayVariant`). It reaches into `Controller.Instance` / `Controller.HasInstance`,
`BeatManager.activeVariant`, and `Controller.CurrentBeatVariant`, and it carries real
invariants — don't *spawn* a Controller GameObject from an open Inspector, retarget the
on-screen effect immediately, log every change. Because it is trapped in a 1,286-line IMGUI
drawer behind the anti-spawn guard, none of it is testable, and the same "change the wall
safely" need is starting to recur (the Held Effect path, a future OSC/keyboard control).

This is not a *shallow* module — a `CustomPropertyDrawer`'s external interface is small. The
defect is **no internal seams**: invariant-bearing policy can only be exercised by driving the
whole drawer.

## Decisions (locked)

1. **Split, don't lump.** The genuinely runtime application policy (write `activeVariant`,
   retarget the live effect, log) moves to the runtime side and becomes testable. The
   editor-context guards (anti-spawn `HasInstance`, Edit-Mode preview) stay in the editor.

2. **Deep core on `BeatManager`, on-screen variant passed in.** `BeatManager` is a plain
   `[Serializable]` class (it owns `activeVariant` and `waveformPool`) and is already tested
   directly in EditMode, so it can hold the deep, testable core. Dependencies are *accepted,
   not created*: the on-screen variant is a parameter, never reached for via `Controller`.

3. **Defer the shared editor adapter.** A shared `LiveWall` editor class would have exactly
   one consumer today (`BeatManagerDrawer`; `EffectSelectorDrawer` does not touch the wall).
   One adapter = hypothetical seam — don't build it yet. The drawer keeps a small private
   instance-resolution helper, marked as the future extraction point.

4. **Pure refactor.** No observable behavior change. The log line relocates into `BeatManager`
   (using its own `waveformPoolNames`), wording preserved.

## Shape

### `BeatManager` (runtime, deep, testable)

- `void LockVariant(int poolIndex)` — clamps `poolIndex` to `[0, pool-1]`, sets `activeVariant`,
  logs `"[Waveform] Wall locked to '<name>' (variant N) — …"`.
- `void ReleaseToAuto()` — sets `activeVariant = -1`, logs `"[Waveform] Wall released to Auto — …"`.
- `int ResolveDisplayVariant(int onScreenVariant)` — pure resolution:
  locked (`activeVariant >= 0`) → `activeVariant`; else `onScreenVariant >= 0` → `onScreenVariant`;
  else `0`.

### `Controller` (runtime, one-line composition)

- When the wall is locked to `v`, also retarget the live effect: `CurrentBeatVariant = v`
  (i.e. `effects[currentEffect].beatVariant = v`). This is the only step needing `effects[]`,
  so it stays here. Trivial wiring; not unit-tested.

### `BeatManagerDrawer` (editor)

- `ApplyWallSelection` body delegates to the runtime API above instead of writing
  `activeVariant`/poking `CurrentBeatVariant`/logging inline.
- `ResolveDisplayVariant`/`TryGetLiveWall` collapse to: resolve the live instance safely
  (keep the `Application.isPlaying && Controller.HasInstance` guard as a private helper —
  one consumer), read `activeVariant` + `CurrentBeatVariant`, and pass the on-screen variant
  into `BeatManager.ResolveDisplayVariant`. In Edit Mode, pass `onScreenVariant = -1`
  (→ resolves to `0`), preserving today's behavior.
- Leave a one-line note: this private resolver is the extraction point for a shared `LiveWall`
  editor adapter once a second consumer is real.

## Test surface (new EditMode tests, beside `BeatManagerContrivedQueriesTests.cs`)

- `LockVariant(v)` → `activeVariant == clamp(v, 0, pool-1)`; out-of-range clamps; logs once.
- `ReleaseToAuto()` → `activeVariant == -1`.
- `ResolveDisplayVariant(onScreen)` truth table: locked → lock; Auto + onScreen≥0 → onScreen;
  Auto + onScreen<0 → 0.

Deliberately **not** tested: the `Controller` retarget one-liner and the editor instance guard
(both require the un-instantiable `Controller` MonoBehaviour; testing them would reach past the
interface — the smell being removed).

## Out of scope

- The shared `LiveWall` editor adapter (deferred — decision 3).
- Candidates 2 (shared inspector widgets) and 3 (rhythm-query presentation) from the review.

## Validation

`scripts/unity-compile.sh` (0 errors / 0 warnings) and `scripts/unity-tests.sh` for the new
EditMode tests. No serial/OSC/scene/hardware paths are touched.
