# The /rave/onair lanes: value to effects

The wire schema — addresses, argument shapes, types, sentinels, delivery — is defined by
[the OSC client contract](osc-client-contract.md), a synced copy owned by RaveSystem.
**Never edit that file here**; schema updates flow from RaveSystem. This document holds
the Penrose half: why each lane has value to effects and transitions, and the
client-side caveats Penrose must respect. Serving rules are
[ADR-0012](adr/0012-data-sources-serve-immutable-nullable-data.md) (immutable, uniformly
nullable) and [ADR-0013](adr/0013-single-lane-wire-facts-served-once.md) (nothing on
`/rave/onair` dropped, each datum served once). The deprecated `/rave/system/*` bundle is
out of the serving floor per ADR-0013's scope note.

Ground-truth captures of the live wire (gate/countdown behavior at two instants) live in
the beat-data-interface effort's map assets.

## Deck and track identity

- **`players_live`** — which decks are audible and which one is the *on-air focus* (first
  in the list). Focus changes explain sudden jumps in every focus-anchored lane below;
  effects that hold state across a changeover can use this to reset gracefully.
- **`track`** — human-readable "what's playing" for overlays and debug surfaces. Not an
  identity: never compare display strings to detect track changes.
- **`track_id`** — deprecated on the wire; per-source rekordbox id. Served (floor), but
  nothing new should depend on it.

## Tempo and position

- **`bpm`** — the playing tempo (pitch-adjusted). Scales any motion that should ride the
  music's speed rather than wall-clock time.
- **`beat`** — absolute track beat, one-based. "How far into the track" for long-arc
  sequencing; pairs with `total_beats` for track progress.
- **`total_beats`** — track length in beats; with `beat` gives a 0..1 track-progress
  fraction. May arrive later than the live fields.
- **`bar`** — absolute four-beat bar counter, for bar-scale sequencing.
- **`beat_in_bar`** — the 1-2-3-4 count. The lane for triggering on set counts with no
  tempo aspect needed ("hit on every 2 and 4").

All position lanes are positions, not event counters: seeks, scratches, and loops move
them backward. Edge/trigger logic derived from them must tolerate reversals. The wire can
briefly publish `beat`, `bar`, and `beat_in_bar` from different instants; when a coherent
coordinate matters, derive bar and beat-in-bar from `beat` with the contract's formulas.

## The countdown/gate cluster

- **`beats_count_ms`** — per-count millisecond countdowns, true track timing (follows
  tempo changes mid-track). The material off-beats are contrived from, and the ms-domain
  scheduler for count-specific hits. A landed count holds at 0 while its gate is open.
- **`on_beats`** — ready-made per-count trigger gates: the current count reads open for
  the first quarter of the beat interval, so the on-time follows the tempo.
- **`beat_avg_ms`** — the beat interval yardstick in milliseconds (equal-weight mean
  across live players, not focus-only). Sizes anything duration-relative: gate windows,
  decay times, anticipation leads.
- **`next_bar_ms`** — ms-domain anticipation of the next bar line, for pre-arming a hit
  on the One. Unlike `beats_count_ms`, it never holds at 0 — at the instant a bar starts
  it already points at the following boundary.
- **`beat_pulse`** — the sender's beat-position signal: a triangle wave, 1.0 on the hit,
  0.0 midway between beats. A free 0..1 animation ramp that needs no local timing math.

## Audio levels

- **`levels`** — low/mid/high band energy, normalized per band to the track's own maxima
  and averaged across live players. The audio-reactive material for brightness and
  motion; track-relative, so every track exercises the full 0..1 range.

## Musical structure

- **`phrase_state`** — what section the music is in (canonical names: Intro, Up, Chorus,
  Drop, Down, Outro; tolerate unknown labels as opaque) and a beat countdown through it.
  Look selection per section; the countdown ends looks cleanly at the boundary.
- **`next_phrase_state`** — what's coming and in how many beats. The pre-arm lane:
  build toward the boundary before it lands.
- **`drop_state`** — the hit-hard moment. Drops open phrases: anticipation while
  inactive (countdown to the slam), full-commitment visuals while active. Use `active`,
  never `remaining > 0`, to detect a running drop.
- **`fill_state`** — the transition garnish at phrase tails: short accents that ride a
  build into the next phrase. Selected across all live players (soonest wins), so it is
  the live set's next fill, not necessarily the focus player's.
- **`energy_state` / `next_energy_state`** — how hard the wall should go now, and what
  intensity is coming (Low/Mid/High, track-relative, measured per *run* of same-level
  phrases). The macro intensity control above per-beat reactivity.

## Deck state

- **`loop_state`** — the focus deck's loop: rolling flag, region-set flag, measured
  length (beats and ms), and nominal size fraction. Loop-aware visuals: hold or cycle a
  look for the loop's musical length while track position freezes. Fractional loops are
  real (a 1/2-beat loop is 0.5).
- **`timing_grid`** — the phrase-anchored 1..16 count with 1 as the One, plus a
  confidence word (`locked` / `coasting` / `disputed`). The alignment clock for
  choreography-scale moves; the state gates how much to trust it.

## Penrose-side caveats (implementation record)

- **`on_beats` unavailable parse bug**: `RegisterFourBools` reads each lane as `!= 0`
  (`Assets/OSC/Rave/RaveOscPacketParser.cs`), so the contract's `-1 -1 -1 -1` unavailable
  shape parses as all four gates *open*. Fix in the implement effort.
- **`beat_pulse` has no unavailable sentinel**: `0.0` doubles as the triangle's trough
  and "no usable timing." The one lane where the surface cannot manufacture `null` from
  the wire value alone.
- Countdowns and positions can move backward (seek/scratch/loop) and can briefly point at
  the previous boundary after a jump; contrived edge detection must not assume
  monotonicity.
