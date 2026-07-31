# OSC Client Contract

> **Scope** — The client-visible meaning of RaveSystem's OSC state feed (OSC schema 4): every broadcast address, argument shape, unit, and unavailable sentinel a receiving client must handle, across the on-air surface (`/rave/onair/…`) and the keyed per-player surface (`/rave/player/{N}/…`).
> **Code anchors** — `RaveSystem.OscContract/` (`RaveOscAddresses`, `RaveOscFields`); `Devices/Models/Player{Clock,Transport,Structure,StructureState}.cs`; `Services/Osc/` (`OscBroadcasterService`, `OscServiceBundleWriter`); `RaveSystem.Player/Playback/PlayerDecksState.cs` (reference client)
> **Glossary** — On air, On-air focus, Loaded track, Timing grid, The One, Grid state, Irregular phrase

This document defines the client-visible meaning of the OSC state feed. Clients must match messages by OSC address and must not depend on message or bundle order. The OSC schema version follows the recording format version: any change to OSC addresses, argument shapes, types, or emitted values requires a new recording format version.

## Transport and delivery

RaveSystem sends OSC 1.0 over UDP to the configured network broadcast address. The default broadcast port is `7000`.

The schema is not negotiated or announced on the wire. Client and server releases must agree on the version out of band; this document defines schema 4.

UDP delivery is not guaranteed. Clients must tolerate dropped, duplicated, delayed, and out-of-order datagrams. Every broadcast state message represents current state rather than a once-only event, so clients should replace the previously stored value for that address. Values that count down or move with playback may also move backward after seeking, scratching, or looping. One address is the exception to store-by-address: `/rave/player/{N}/structure` is chunked and assembled by generation, not replaced datagram-for-datagram — see [Per-player structure](#per-player-values).

RaveSystem publishes two address surfaces on the same broadcast, both derived from one capture per tick:

- The **on-air surface** (`/rave/onair/…`) — the single canonical answer to what the audience hears now, resolving one on-air focus across the live set.
- The **per-player surface** (`/rave/player/{N}/…`) — one keyed address group per physical player, where `N` is the player's ProLink device number in the range `1..6`. Every physical player is reported independently, live or not; the VirtualCdj never appears. The per-player surface is broadcast-only and is not returned by any snapshot query.

Messages are divided into delivery lanes:

| Surface | Lane | Default delivery | Addresses |
| --- | --- | --- | --- |
| On-air | Continuous | Every broadcast tick, normally `60 Hz`. | `/bpm`, `/beat`, `/bar`, `/next_bar_ms`, `/beat_in_bar`, `/beats_count_ms`, `/on_beats`, `/beat_avg_ms`, `/beat_pulse`, `/levels` |
| On-air | Discrete | On change, repeated three times; also a complete heartbeat normally twice per second. | `/players_live`, `/track`, `/track_id`, `/total_beats`, `/phrase_state`, `/next_phrase_state`, `/drop_state`, `/fill_state`, `/energy_state`, `/next_energy_state`, `/loop_state`, `/timing_grid` |
| Per-player | Continuous | Every broadcast tick, normally `60 Hz`. | `/rave/player/{N}/clock`, `/rave/player/{N}/structure_state` |
| Per-player | Discrete | On change, repeated three times; also a complete heartbeat normally twice per second. | `/rave/player/{N}/transport`, `/rave/player/{N}/loop_state`, `/rave/player/{N}/timing_grid` |
| Per-player | Structure | Bare datagrams: a three-send burst on each new generation, one send per changed beat while the deck plays, and a ~2/s heartbeat while it is silent. | `/rave/player/{N}/structure` |

`60 Hz` is nominal: the broadcaster's timer has whole-millisecond resolution, so continuous ticks actually arrive every 16 ms (62.5 Hz). Treat the tick rate as ≈60 Hz — a client rendering at 60 fps will occasionally receive two updates within one frame.

On-air abbreviated addresses in this table use the `/rave/onair` prefix. Messages captured together share one OSC bundle timetag. `/rave/player/{N}/structure` is the exception: it is a bare OSC message, never a bundle member, and carries no timetag. A client joining mid-session can wait for the next heartbeat or request an immediate on-air snapshot.

### Snapshot request

When query/reply is enabled, RaveSystem listens on UDP port `7001`. Send this argument-free message to request the current continuous and discrete on-air bundles:

```text
/rave/snapshot/onair
```

The two reply bundles are sent back to the request's source address and port with a shared timetag. The reply is **on-air-only**: it returns exactly the two on-air bundles and never any per-player `/clock`, `/structure_state`, `/transport`, `/loop_state`, `/timing_grid`, or `/structure` message. There is no per-player snapshot query; the per-player surface converges through its own broadcast heartbeats instead.

### Registration acknowledgement

The schema also accepts:

```text
/rave/register
```

It replies to the request's source address and port with:

```text
/rave/registered "ok"
```

This is only an acknowledgement. It does not create a subscription, change broadcast delivery, or need to be sent before receiving broadcasts.

## Shared concepts

### Live player

A live player is a player that is both playing and routed on air. A player that is merely playing off air, or routed on air while paused, is not live.

### Live order and focus

The live order is ordered by when each player most recently entered the live set, newest first. Repeated updates from an already-live player do not change its position. The first player is the **on-air focus** used by fields that describe one player rather than the whole live set.

Player numbers are decimal device numbers in the range `1..6`.

### Unavailable values

Unless a field says otherwise:

- An empty string (`""`) means text is unavailable.
- `-1` means a numeric value is unavailable.
- For a tri-state integer, `1` means true, `0` means false, and `-1` means unavailable.

`-1` never means "no". Comparing a tri-state with `!= 0` misreads "unavailable" as "true"; compare against the specific value.

### Physical-player scope

The per-player surface addresses each physical player by its ProLink device number `N` in `1..6`: `/rave/player/{N}/clock`, `/transport`, `/loop_state`, `/timing_grid`, `/structure`, and `/structure_state`. These report the specific player `N` unconditionally, independent of the live set and the on-air focus. A player that leaves the network simply stops appearing in the per-player bundles — there is no departure message; whether to retain or expire a silent deck's last-known state is client policy. Where a player-scoped message shares a shape with an on-air message (`loop_state`, `timing_grid`), the argument meanings and sentinels are identical — only the scope differs. For the current on-air focus player, its `/rave/player/{N}/…` values equal the corresponding `/rave/onair/…` values in the same capture.

### Structure generation and phrase ordinal

The per-player `/structure` message carries a per-player `structure_generation` — a monotonic change detector — and its live cursor `/structure_state` carries the generation it belongs to. Two rules govern them and are stated in full under [Per-player structure](#per-player-values):

- **Generation is the change detector; `track_id` is only a recognition hint.** A client decides "did the structure change" from the generation, never from `track_id`.
- **Phrase identity is ordinal occurrence.** A phrase is identified by its position in the reconstructed phrase list, never by its `type` or name; identical adjacent types are legal and distinct.

Because a large structure is split into chunk datagrams, `/structure` is the one address a client assembles rather than stores whole; details are in its section.

## On-air values

### `/rave/onair/players_live`

Reports every live player as one comma-separated string in live order.

```text
Type tag: ,s
Arguments: players_live
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `players_live` | string | Comma-separated decimal player numbers, newest live-set entrant first. |

Examples:

```text
/rave/onair/players_live ""
/rave/onair/players_live "2"
/rave/onair/players_live "4,2"
```

An empty string means no players are currently live. In `"4,2"`, players 4 and 2 are live, and player 4 is the on-air focus.

### `/rave/onair/track`

Reports a display label for the on-air focus player's current track.

```text
Type tag: ,s
Arguments: track
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `track` | string | Track title followed by the artist, formatted as `Title - Artist`; if the artist is unavailable, contains only the title. |

Examples:

```text
/rave/onair/track "Midnight Runner - System Rave"
/rave/onair/track "Untitled Demo"
/rave/onair/track ""
```

An empty string means the display metadata is unavailable. It does not prove that no track is loaded: identity and display metadata can arrive independently. Clients must not infer durable track identity by comparing this display string.

### `/rave/onair/track_id`

> **Deprecated:** This address is planned for removal in a future schema version. New clients should not depend on it.

Reports the rekordbox track identifier carried by the on-air focus player's loaded-track identity.

```text
Type tag: ,i
Arguments: track_id
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `track_id` | int32 | Source-media-specific rekordbox track identifier, or `-1` when unavailable. |

The identifier is meaningful only within the source media and library that assigned it. It is not a portable or globally stable track identity. Existing clients may use it as a source-local change signal, but its planned removal means new clients should not take a dependency on it.

### `/rave/onair/bpm`

Reports the effective tempo of the on-air focus player after applying its current pitch adjustment.

```text
Type tag: ,f
Arguments: bpm
```

| Argument | Type | Unit | Meaning |
| --- | --- | --- | --- |
| `bpm` | float32 | beats per minute | Current pitch-adjusted playback tempo, or `-1.0` when unavailable. |

Examples:

```text
/rave/onair/bpm 128.0
/rave/onair/bpm 129.28
/rave/onair/bpm -1.0
```

This is the tempo at which the track is currently playing, not necessarily the track's analyzed/base BPM.

### `/rave/onair/beat`

Reports the on-air focus player's absolute beat position from the start of the loaded track.

```text
Type tag: ,i
Arguments: beat
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `beat` | int32 | One-based absolute track beat, or `-1` when unavailable. |

The value normally advances by one on each beat. It is a position, not an event counter: seeking, scratching, or looping can make it jump forward or backward. Use `/rave/onair/beat_in_bar` for the repeating `1..4` musical beat label and `/rave/onair/timing_grid` for the repeating `1..16` phrase-aligned grid.

### `/rave/onair/total_beats`

Reports the total musical beat count of the on-air focus player's loaded track.

```text
Type tag: ,i
Arguments: total_beats
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `total_beats` | int32 | Total number of musical beats in the track per the beat grid, or `-1` when unavailable. |

This value normally remains constant for the loaded track. It may become available later than other live fields.

### `/rave/onair/bar`

Reports the on-air focus player's one-based absolute bar position from the start of the loaded track. Each bar contains four beats.

```text
Type tag: ,i
Arguments: bar
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `bar` | int32 | One-based absolute four-beat bar, or `-1` when unavailable. |

For an available absolute `/rave/onair/beat` value `N`, the corresponding bar is:

```text
bar = ((N - 1) / 4) + 1
```

Integer division is used. Beats `1..4` are bar 1, beats `5..8` are bar 2, and so on. Like `/rave/onair/beat`, this is a track position rather than a monotonic event counter: seeking, scratching, or looping can move it forward or backward.

RaveSystem can briefly publish `/beat` and `/bar` from different instants. Clients that require a coherent coordinate should calculate `bar` from an available `/beat` using the formula above.

### `/rave/onair/next_bar_ms`

Reports the time remaining until the on-air focus player reaches the next future bar boundary. A bar boundary is beat 1 of a bar.

```text
Type tag: ,i
Arguments: next_bar_ms
```

| Argument | Type | Unit | Meaning |
| --- | --- | --- | --- |
| `next_bar_ms` | int32 | milliseconds | Time until the next future beat-1 boundary, or `-1` when unavailable. |

The value counts down toward zero. At the instant a new bar begins, "next" means the following bar boundary rather than the boundary that has just been reached; at a constant 120 BPM, the value at beat 1 is therefore approximately `2000` milliseconds.

After a seek, scratch, reverse movement, or loop jump, RaveSystem can briefly continue counting toward the previous boundary.

### `/rave/onair/beat_in_bar`

Reports which of the four beats in the current bar the on-air focus player occupies.

```text
Type tag: ,i
Arguments: beat_in_bar
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `beat_in_bar` | int32 | Musical beat label `1`, `2`, `3`, or `4`; `-1` when unavailable. |

For an available absolute `/rave/onair/beat` value `N`, the corresponding beat in bar is:

```text
beat_in_bar = ((N - 1) % 4) + 1
```

Music has no beat zero: this value cycles `1, 2, 3, 4, 1, ...`. It is a position rather than a trigger. Seeking, scratching, or looping can move it in either direction or repeat a value.

RaveSystem can briefly publish `/beat` and `/beat_in_bar` from different instants. Clients that require a coherent coordinate should calculate `beat_in_bar` from an available `/beat` using the formula above.

### `/rave/onair/beats_count_ms`

Reports four countdown lanes: the time until the next occurrence of musical beat labels 1, 2, 3, and 4 for the on-air focus player.

```text
Type tag: ,iiii
Arguments: beat1, beat2, beat3, beat4
```

| Argument | Type | Unit | Meaning |
| --- | --- | --- | --- |
| `beat1` | int32 | milliseconds | Countdown to the next beat labeled 1, or `-1` when unavailable. |
| `beat2` | int32 | milliseconds | Countdown to the next beat labeled 2, or `-1` when unavailable. |
| `beat3` | int32 | milliseconds | Countdown to the next beat labeled 3, or `-1` when unavailable. |
| `beat4` | int32 | milliseconds | Countdown to the next beat labeled 4, or `-1` when unavailable. |

During the first quarter of the current beat interval, the lane matching `/rave/onair/beat_in_bar` reads `0`. Once that gate closes, the same lane counts down to that beat label in the following bar. Every other lane counts down to its next future occurrence. Countdown values do not become negative.

At the start of beat 1 at a constant 120 BPM, an example value is:

```text
/rave/onair/beats_count_ms 0 500 1000 1500
```

If no trustworthy timing is available, all four arguments are `-1`.

After a seek, scratch, reverse movement, or loop jump, RaveSystem can briefly identify the previous four-count.

### `/rave/onair/on_beats`

Reports a short trigger gate for each musical beat label of the on-air focus player.

```text
Type tag: ,iiii
Arguments: beat1, beat2, beat3, beat4
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `beat1` | int32 | `1` during the beat-1 gate, otherwise `0`; `-1` when unavailable. |
| `beat2` | int32 | `1` during the beat-2 gate, otherwise `0`; `-1` when unavailable. |
| `beat3` | int32 | `1` during the beat-3 gate, otherwise `0`; `-1` when unavailable. |
| `beat4` | int32 | `1` during the beat-4 gate, otherwise `0`; `-1` when unavailable. |

For the first quarter of each beat interval, exactly one lane reads `1`: the lane matching `/rave/onair/beat_in_bar`. Outside that window, all four lanes read `0`.

Examples:

```text
/rave/onair/on_beats 1 0 0 0
/rave/onair/on_beats 0 1 0 0
/rave/onair/on_beats 0 0 0 0
/rave/onair/on_beats -1 -1 -1 -1
```

After a seek, scratch, reverse movement, or loop jump, RaveSystem can briefly identify the previous beat label.

### `/rave/onair/beat_avg_ms`

Reports the rounded arithmetic mean of the current beat intervals contributed by live players.

```text
Type tag: ,i
Arguments: beat_avg_ms
```

| Argument | Type | Unit | Meaning |
| --- | --- | --- | --- |
| `beat_avg_ms` | int32 | milliseconds | Equal-weight arithmetic mean of usable live-player beat intervals, rounded to the nearest millisecond; `-1` when no live player can contribute. |

This is a live-set value, not an on-air-focus value. Each live player with trustworthy timing contributes one interval with equal weight; a live player without usable timing is omitted. For example, intervals of `500` and `1000` milliseconds produce:

```text
/rave/onair/beat_avg_ms 750
```

Despite the field description calling this a moving average, it does not average samples over a historical time window.

### `/rave/onair/beat_pulse`

Reports the on-air focus player's position between consecutive beats as a normalized triangle wave.

```text
Type tag: ,f
Arguments: beat_pulse
```

| Argument | Type | Range | Meaning |
| --- | --- | --- | --- |
| `beat_pulse` | float32 | `0.0..1.0` | Triangle wave that is `1.0` on each beat and `0.0` halfway between beats. |

Across one beat interval, the waveform is:

| Position | Value |
| --- | --- |
| On the current beat | `1.0` |
| One quarter of the way to the next beat | `0.5` |
| Halfway between beats | `0.0` |
| Three quarters of the way to the next beat | `0.5` |
| On the next beat | `1.0` |

This is a continuous position signal, not a one-shot trigger. Use `/rave/onair/on_beats` when a short labeled trigger gate is required.

When no usable timing exists, the value is `0.0`. Because `0.0` is also the normal midpoint trough, clients cannot distinguish unavailable timing from that musical position using this value alone.

### `/rave/onair/levels`

Reports normalized low-, mid-, and high-frequency energy across the live set.

```text
Type tag: ,fff
Arguments: low, mid, high
```

| Argument | Type | Range | Meaning |
| --- | --- | --- | --- |
| `low` | float32 | `0.0..1.0` | Mean normalized low-frequency energy across contributing live players; `-1.0` when unavailable. |
| `mid` | float32 | `0.0..1.0` | Mean normalized mid-frequency energy across contributing live players; `-1.0` when unavailable. |
| `high` | float32 | `0.0..1.0` | Mean normalized high-frequency energy across contributing live players; `-1.0` when unavailable. |

This is a live-set value, not an on-air-focus value. Each live player with usable energy data contributes one equally weighted three-band value. A player without usable energy data is omitted. If no live player can contribute, all three arguments are `-1.0`.

These are normalized, track-relative spectral-energy values. They are not microphone, mixer-output, or real-time audio level meters, and values from different tracks should not be treated as absolute loudness measurements.

### `/rave/onair/phrase_state`

Reports the current musical phrase of the on-air focus player.

```text
Type tag: ,siii
Arguments: name, count_beats, length_beats, irregular
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `name` | string | Current phrase name, such as `Up` or `Drop`; empty when unavailable. |
| `count_beats` | int32 | Beats remaining in the current phrase, including the current beat; `-1` when unavailable. |
| `length_beats` | int32 | Total length of the current phrase in beats; `-1` when unavailable. |
| `irregular` | int32 | `1` when `length_beats` is not divisible by 16, `0` when it is divisible by 16, or `-1` when unavailable. |

For a phrase of length `N`, `count_beats` is `N` on its first beat and `1` on its final beat. Phrase length is independent of the repeating 16-position timing grid: during a 25-beat phrase, the timing grid reads `1..16`, then `1..9`, and the following phrase resets it to `1`. Phrase names use the exact case-sensitive vocabulary defined below.

Example:

```text
/rave/onair/phrase_state "Up" 6 16 0
```

Valid phrases can have lengths that are not divisible by 16. Clients must not use the `irregular` argument to reject, repair, or reinterpret phrase boundaries.

When no current phrase is available, the complete unavailable shape is:

```text
/rave/onair/phrase_state "" -1 -1 -1
```

#### Phrase name vocabulary

The canonical `name` values for `/rave/onair/phrase_state` and `/rave/onair/next_phrase_state` are:

| Name | Meaning |
| --- | --- |
| `Intro` | Opening section before the track reaches its main rhythmic body. |
| `Up` | Rising or building section leading toward a higher-energy section. |
| `Chorus` | Sustained full-rhythm, high-energy section. |
| `Drop` | Peak-impact entry into a high-energy section. |
| `Down` | Lower-energy section, break, or transition toward the outro. |
| `Outro` | Closing section shaped for mixing out of the track. |

These six names are the canonical vocabulary. RaveSystem can also emit a legacy non-empty phrase label when canonical phrase state is not yet available. Clients must accept an unrecognized non-empty label as an opaque phrase name rather than failing or mapping it by string pattern. Numbered or unfamiliar labels must not be treated as additions to the canonical vocabulary. An empty string is the unavailable sentinel and is not a phrase name.

### `/rave/onair/next_phrase_state`

Reports the next musical phrase of the on-air focus player.

```text
Type tag: ,sii
Arguments: name, count_beats, length_beats
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `name` | string | Name of the next phrase; empty when unavailable. |
| `count_beats` | int32 | Beats remaining until the next phrase begins, including the current beat; `-1` when unavailable. |
| `length_beats` | int32 | Total length of the next phrase itself in beats; `-1` when unavailable. |

`count_beats` describes the boundary countdown; `length_beats` describes the upcoming phrase after that boundary. For example:

```text
/rave/onair/next_phrase_state "Drop" 6 16
```

When there is no next phrase, including while the final phrase is playing, the complete unavailable shape is:

```text
/rave/onair/next_phrase_state "" -1 -1
```

### `/rave/onair/drop_state`

Reports the current or next musical drop for the on-air focus player.

```text
Type tag: ,iiii
Arguments: active, count_beats, length_beats, remaining
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `active` | int32 | `1` when currently inside a drop, `0` when counting down to the next drop, or `-1` when drop state is unavailable. |
| `count_beats` | int32 | When active, beats remaining in the current drop including the current beat; when inactive, beat advances until the next drop begins (`1` on the beat immediately before it starts). `-1` when unavailable. |
| `length_beats` | int32 | Total length of the current or upcoming drop in beats; `-1` when unavailable. |
| `remaining` | int32 | Count of drop occurrences whose designated drop point has not yet passed; `-1` when unavailable. |

Examples:

```text
/rave/onair/drop_state 0 6 16 1
/rave/onair/drop_state 1 16 16 1
/rave/onair/drop_state -1 -1 -1 -1
```

In the first example, the next 16-beat drop begins in 6 beats and is the final remaining drop. In the second, that drop is active and has 16 beats remaining.

`remaining` can become `0` while `active` is still `1` after playback passes the active drop's marker. Clients must use `active`—not `remaining > 0`—to determine whether a drop is currently running.

### `/rave/onair/fill_state`

Reports one musical fill selected from across all live players. A fill is a transition section in the tail of a phrase. It runs from its start beat to the last beat of that phrase and never stops before the phrase ends.

```text
Type tag: ,iiii
Arguments: active, count_beats, length_beats, remaining
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `active` | int32 | `1` when the selected fill is active, `0` when counting down to the selected upcoming fill, or `-1` when fill state is unavailable. |
| `count_beats` | int32 | When active, beats remaining in the selected fill including the current beat; when inactive, beat advances until the selected fill begins (`1` on the beat immediately before it starts). `-1` when unavailable. |
| `length_beats` | int32 | Total length of the selected fill in beats; `-1` when unavailable. |
| `remaining` | int32 | Number of fills remaining on the selected player's track, including the selected fill when it is active; `-1` when unavailable. This is not a total across the live set. |

Examples:

```text
/rave/onair/fill_state 0 6 8 2
/rave/onair/fill_state 1 4 4 1
/rave/onair/fill_state -1 -1 -1 -1
```

RaveSystem selects the event as follows:

1. Any active fill outranks every upcoming fill.
2. Among events with the same `active` value, the smaller `count_beats` wins.
3. If the beat counts are equal, the first player in live order wins.

The four arguments always describe that one selected player's event; values from multiple players are never averaged or added together. If no live player reports an active or upcoming fill, the complete unavailable shape is emitted.

When live players differ in tempo or phase, RaveSystem may not select the fill that occurs first in real time. Clients must interpret this value as RaveSystem's selected fill rather than an absolute chronological ordering of every live player's fills.

### `/rave/onair/energy_state`

Reports the current measured energy run of the on-air focus player's track. An energy run is one or more consecutive phrases with the same energy classification.

```text
Type tag: ,sii
Arguments: level, count_beats, length_beats
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `level` | string | Current energy level: exactly `Low`, `Mid`, or `High`; empty when unavailable. |
| `count_beats` | int32 | Beats remaining in the current energy run, including the current beat; `-1` when unavailable. |
| `length_beats` | int32 | Total length of the complete current energy run, including any earlier same-level phrases already played; `-1` when unavailable. |

Example:

```text
/rave/onair/energy_state "Mid" 22 48
```

`Low`, `Mid`, and `High` describe relative musical energy within the loaded track. They are not absolute loudness or mixer levels, and a level from one track must not be assumed equivalent to the same level from another track. Clients must not infer an energy level from the phrase name.

Consecutive phrases with the same classification form one run. If no energy classification is available for the current phrase, the complete unavailable shape is:

```text
/rave/onair/energy_state "" -1 -1
```

### `/rave/onair/next_energy_state`

Reports the next energy run whose measured level differs from the on-air focus player's current energy run.

```text
Type tag: ,sii
Arguments: level, count_beats, length_beats
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `level` | string | Upcoming different energy level: exactly `Low`, `Mid`, or `High`; empty when unavailable. |
| `count_beats` | int32 | Beat advances until the upcoming different energy run begins (`1` on the beat immediately before the change); `-1` when unavailable. |
| `length_beats` | int32 | Total length of that complete upcoming same-level run in beats; `-1` when unavailable. |

Example:

```text
/rave/onair/next_energy_state "High" 6 32
```

This skips phrase boundaries that do not change energy. For example, if the current `Mid` run spans three consecutive phrases, `count_beats` targets the first beat of the later phrase where the level actually changes. If no different classified run is known ahead, the complete unavailable shape is:

```text
/rave/onair/next_energy_state "" -1 -1
```

### `/rave/onair/loop_state`

Reports the loop state of the on-air focus player. This is not combined across the live set: if several live players are looping, only the focus player's loop appears here.

```text
Type tag: ,iifiii
Arguments: active, set, length_beats, length_ms, size_numerator, size_denominator
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `active` | int32 | `1` when the focus player reports looping audio as rolling, `0` otherwise, or `-1` when unavailable. |
| `set` | int32 | `1` when a loop region exists on the focus player, `0` when no region exists, or `-1` when unavailable. A set region can persist while playback is paused. |
| `length_beats` | float32 | Measured loop-region length in beats; `0.0` when no measurable region exists, or `-1.0` when the complete loop state is unavailable. |
| `length_ms` | int32 | Measured loop-region duration in whole milliseconds; `0` when no measurable region exists, or `-1` when the complete loop state is unavailable. |
| `size_numerator` | int32 | Numerator of the nominal loop size reported by the player. |
| `size_denominator` | int32 | Denominator of the nominal loop size reported by the player. A value greater than zero means a nominal quantized size is available. |

Nominal loop size is:

```text
size_beats = size_numerator / size_denominator
```

Examples:

```text
/rave/onair/loop_state 1 1 4.0 1875 4 1
/rave/onair/loop_state 0 1 0.5 234 1 2
/rave/onair/loop_state 0 1 4.0 1875 4 1
/rave/onair/loop_state 0 0 0.0 0 0 0
/rave/onair/loop_state -1 -1 -1.0 -1 -1 -1
```

`active` and `set` answer different questions. A paused player can retain its loop region, producing `active=0, set=1`. The measured `length_*` values describe the actual loop region. The nominal size fraction describes the requested loop size and can differ from the measured region. A `0/0` fraction means no nominal size was reported; it must not be interpreted as a fractional zero-beat loop.

The loop state has two client-visible limitations:

1. `active` can read `0` during some audibly cycling sub-beat or track-end-clamped loops. In that state, `set` and the size fields remain independently meaningful.
2. With non-zero pitch adjustment, `length_beats` can drift from the loop's musical length and `length_ms` can differ from the audible cycle duration.

### `/rave/onair/timing_grid`

Reports the phrase-relative 16-beat timing grid of the on-air focus player.

```text
Type tag: ,iis
Arguments: beat, bar, state
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `beat` | int32 | One-based position `1..16` within the repeating timing-grid cycle; `-1` when no beat can be placed. |
| `bar` | int32 | One-based four-beat subdivision `1..4` within that cycle; `-1` when no beat can be placed. |
| `state` | string | Confidence state: exactly `locked`, `coasting`, or `disputed`; empty only when the complete focus-player timing grid is unavailable. |

The first beat of every phrase is timing-grid beat `1`, called **the One**. The grid then advances through `1..16` and repeats for as long as that phrase continues. Phrase length does not need to be divisible by 16.

For a 25-beat phrase:

```text
Phrase beat:  1 ... 16  17 ... 25
Grid beat:    1 ... 16   1 ...  9
Grid bar:     1 ...  4   1 ...  3
```

The following phrase starts again at grid beat `1` and grid bar `1`. There is no timing-grid beat `0` or `17`.

The `bar` argument is derived from `beat`:

```text
bar = ((beat - 1) / 4) + 1
```

Thus grid beats `1..4` are bar 1, `5..8` are bar 2, `9..12` are bar 3, and `13..16` are bar 4. This bar is local to the timing-grid cycle; it is not the absolute track bar published by `/rave/onair/bar`.

#### Timing-grid state vocabulary

| State | Meaning |
| --- | --- |
| `locked` | The current phrase anchors the One and the grid position is trusted. Phrases of every length can lock. |
| `coasting` | No current phrase anchor is available, but RaveSystem can still provide an estimated grid position. |
| `disputed` | Available timing information conflicts. The numeric grid remains RaveSystem's best usable placement but should be treated with caution. |

Seeking, scratching, looping, and phrase changes can move or reset the grid. Clients must treat it as a position, not a monotonic counter.

When a focus player exists but no positive beat can be placed, the shape is:

```text
/rave/onair/timing_grid -1 -1 "coasting"
```

When no focus-player grid exists at all, the complete unavailable shape is:

```text
/rave/onair/timing_grid -1 -1 ""
```

## Per-player values

These addresses report each physical player `N` (device number `1..6`) independently of the on-air focus. A player's messages appear only while that player is tracked. For the on-air focus player, its per-player values match the corresponding `/rave/onair` values in the same capture.

### `/rave/player/{N}/clock`

Reports the 60 Hz clock of physical player `N`.

```text
Type tag: ,fiiif
Arguments: bpm, beat, bar, beat_in_bar, beat_pulse
```

| Argument | Type | Unit | Meaning |
| --- | --- | --- | --- |
| `bpm` | float32 | beats per minute | Pitch-adjusted effective tempo; `-1.0` when unavailable. |
| `beat` | int32 | | Absolute one-based track beat from track start; `-1` when unavailable. |
| `bar` | int32 | | Absolute one-based four-beat bar from track start; `-1` when unavailable. |
| `beat_in_bar` | int32 | | Musical beat label `1`, `2`, `3`, or `4`; `-1` when unavailable. |
| `beat_pulse` | float32 | `0.0..1.0` | Triangle wave that is `1.0` on each beat and `0.0` halfway between beats. |

Examples:

```text
/rave/player/2/clock 128.0 641 161 1 1.0
/rave/player/2/clock -1.0 -1 -1 -1 0.0
```

`beat`, `bar`, `beat_in_bar`, and `beat_pulse` follow the same meaning and formulas as the on-air `/rave/onair/beat`, `/bar`, `/beat_in_bar`, and `/beat_pulse` fields. Like `beat_pulse` on-air, `0.0` is both the mid-beat trough and the at-rest/unavailable value, so it alone cannot distinguish a missing clock from that musical position.

### `/rave/player/{N}/transport`

Reports the five capture-proven transport predicates of physical player `N`, each a tri-state.

```text
Type tag: ,iiiii
Arguments: playing, cued, onair, master, synced
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `playing` | int32 | `1` when audio is rolling, `0` when not, `-1` when unavailable. |
| `cued` | int32 | `1` when engaged with a cue point, `0` when not, `-1` when unavailable. |
| `onair` | int32 | `1` when the player's mixer channel is on air, `0` when not, `-1` when unavailable. |
| `master` | int32 | `1` when this player is tempo master, `0` when not, `-1` when unavailable. |
| `synced` | int32 | `1` when fully beat-synced, `0` when not, `-1` when unavailable. |

Examples:

```text
/rave/player/2/transport 1 0 1 1 1
/rave/player/2/transport 0 1 0 0 0
/rave/player/2/transport -1 -1 -1 -1 -1
```

**"Live" is not transmitted.** A player is live — actually heard by the audience — when it is both playing and on air. Clients derive it as `playing == 1 && onair == 1`. The wire carries the two raw predicates; the client composes the conclusion. Do not treat `master` or `synced` as liveness.

### `/rave/player/{N}/loop_state`

Reports the loop state of physical player `N`. Argument shape, per-argument meaning, and unavailable sentinels are **identical** to [`/rave/onair/loop_state`](#raveonairloop_state); only the scope differs (this is player `N`, not the on-air focus).

```text
Type tag: ,iifiii
Arguments: active, set, length_beats, length_ms, size_numerator, size_denominator
```

See the on-air `loop_state` section for the full semantics of `active`, `set`, the measured `length_beats`/`length_ms`, and the nominal `size_numerator`/`size_denominator` fraction (including the `0/0` "no nominal size" case). The complete unavailable shape is:

```text
/rave/player/2/loop_state -1 -1 -1.0 -1 -1 -1
```

### `/rave/player/{N}/timing_grid`

Reports the phrase-relative 16-beat timing grid of physical player `N`. Argument shape, per-argument meaning, and unavailable sentinels are **identical** to [`/rave/onair/timing_grid`](#raveonairtiming_grid); only the scope differs.

```text
Type tag: ,iis
Arguments: beat, bar, state
```

`beat` is the one-based `1..16` position (`1` is **the One**), `bar` is the local `1..4` subdivision, and `state` is exactly `locked`, `coasting`, or `disputed`. See the on-air `timing_grid` section for the full grid model and state vocabulary. Placement and unavailable shapes match on-air:

```text
/rave/player/2/timing_grid 5 2 "locked"
/rave/player/2/timing_grid -1 -1 "coasting"
/rave/player/2/timing_grid -1 -1 ""
```

### `/rave/player/{N}/structure`

Reports the complete song structure of physical player `N`: a header describing the loaded track and one tuple per analyzed phrase, in track order. This is the only OSC message a client **assembles** rather than storing whole, and the only one that is a **bare datagram** — never a bundle member.

Because the phrase list is variable-length and large structures are split across datagrams, `/rave/player/{N}/structure` has no single fixed type tag. Its type tag is a seven-argument header followed by one six-argument tuple per phrase carried by that datagram:

```text
Type tag: ,sisiiii  then  iisiii  repeated once per phrase in this datagram
Header arguments: track_id, structure_generation, source, total_beats, phrase_count, chunk_index, chunk_count
Tuple arguments (repeated): start_beat, end_beat, type, variant, fill_start_beat, drop_landing_beat
```

Header:

| Argument | Type | Meaning |
| --- | --- | --- |
| `track_id` | string | Opaque decimal string of the loaded track's full uint32 rekordbox id; a recognition/cache hint only. Empty (`""`) when no track is loaded. |
| `structure_generation` | int32 | Per-player monotonic change detector. Authoritative for whether the structure changed. Never `0` on the wire (`0` is the never-loaded sentinel). |
| `source` | string | Structure source: exactly `analyzed`, `synthesized`, `fused`, or `unavailable`. |
| `total_beats` | int32 | Total musical beats in the loaded track per the beat grid. `-1` when unavailable. |
| `phrase_count` | int32 | Full-track phrase count across **all** chunks of this generation; `0` when there are no phrases. |
| `chunk_index` | int32 | Zero-based index of this datagram within the generation's chunk sequence; `0` when unchunked. |
| `chunk_count` | int32 | Total chunks carrying this generation; `1` when unchunked. |

Repeating phrase tuple:

| Argument | Type | Meaning |
| --- | --- | --- |
| `start_beat` | int32 | Inclusive one-based phrase start beat. |
| `end_beat` | int32 | Inclusive phrase end beat. |
| `type` | string | Lowercase phrase kind (vocabulary below); `unknown` when the kind is not in the published set. |
| `variant` | int32 | Rekordbox phrase variant; `0` when variantless. |
| `fill_start_beat` | int32 | One-based fill start beat within the phrase, or `0` when the phrase has no fill. A fill is a tail: it runs from `fill_start_beat` to `end_beat`. For the `synthesized` and `fused` sources the audio-detected fill is canonical. A `fused` phrase with no audio-detected fill adopts a rekordbox fill only when that fill ends at `end_beat`. An `analyzed` structure carries only rekordbox fills. |
| `drop_landing_beat` | int32 | Pinned drop landing beat; `0` when none. For the `synthesized` and `fused` sources it always equals `start_beat`. Only an `analyzed` structure can carry a landing a few beats away from `start_beat`. There the landing stays a sidecar of the rekordbox boundary. |

Example — a two-phrase unchunked structure for track id `328123`, generation `7`:

```text
/rave/player/2/structure "328123" 7 "fused" 512 2 0 1  1 128 "intro" 0 0 0  129 256 "drop" 1 249 129
```

The cleared/unavailable shape after an eject is a header-only message under a fresh generation:

```text
/rave/player/2/structure "" 9 "unavailable" -1 0 0 1
```

#### Phrase type vocabulary

The `type` slot uses this stable lowercase set:

| Type | Meaning |
| --- | --- |
| `unknown` | Phrase kind not in the published vocabulary (also the fallback for an unrecognized value). |
| `intro` | Opening section before the main body. |
| `up` | Rising or building section. |
| `down` | Lower-energy section, break, or transition. |
| `verse` | Verse section. |
| `bridge` | Bridge section. |
| `chorus` | Sustained full-rhythm section. |
| `outro` | Closing section shaped for mixing out. |
| `drop` | Peak-impact entry into a high-energy section. |

This is a **different set and a different case** from the capitalized `/rave/onair/phrase_state` names — the two surfaces do not share a phrase vocabulary. Clients must accept `unknown` as an opaque kind rather than failing, and must not map the string by pattern.

#### Generation is the change detector; `track_id` is only a recognition hint

`structure_generation` and `track_id` answer different questions and must not be swapped:

- **`track_id`** is an opaque decimal **string**, not an OSC integer, specifically so it preserves the full uint32 rekordbox id (which overflows a signed OSC int32). Use it only to recognize a track or key a cache. It is **not** a change signal: loading the same track again can reappear under an identical `track_id` but a new generation.
- **`structure_generation`** is authoritative for change. It advances on track load, on eject/clear, and on any applied-structure or analysis change — including a content-identical re-application and, notably, a **synthesized→fused refinement** (the same track's structure improving from waveform-synthesized to PSSI-fused advances the generation even though the track never changed).
- An **eject** publishes the cleared shape — empty `track_id`, `source` `unavailable`, `total_beats` `-1`, `phrase_count` `0` — under a **fresh** generation, so a client sees the clear as an ordinary generation edge.
- `structure_generation` `0` is the never-loaded sentinel and is **never emitted**; a player's first real structure has a positive generation.
- **`total_beats`** counts the beats in the track per the beat grid. An `analyzed` phrase list can end before `total_beats` because rekordbox stops phrase analysis before the end of the track. `fused` and `synthesized` phrase lists end exactly at `total_beats`.

A client replaces its entire held structure and chunk buffer whenever it sees a new generation — "new" means *different from the held generation*, compared for inequality, never for ordering (do not require the number to increase) — and clears its held structure and cursor when it receives a zero-phrase `chunk_index 0 / chunk_count 1` message.

#### Phrase identity is ordinal occurrence

**A phrase's identity is its position, not its type or name.** This is the load-bearing parsing rule for `/structure` and for the `/structure_state` cursor:

- Preserve the complete tuple order exactly as received, and concatenate chunks in ascending `chunk_index`.
- A phrase's one-based ordinal is its position in the fully reconstructed sequence **+ 1** (the first tuple is ordinal `1`). This is the ordinal `/structure_state` reports as `current_phrase`.
- Repeated types and **immediately adjacent identical types are legal** and remain separate phrases — two consecutive `chorus` tuples are two phrases, not one. Nothing is deduplicated by type.
- **Never** store phrases in a dictionary keyed by `type` or name. Keying by type collapses legal repeats and destroys the ordinal contract. Use an ordered list indexed by position.

#### Chunk assembly

A structure that fits in one datagram is sent as a single message with `chunk_index 0`, `chunk_count 1`. A larger structure is split into a contiguous sequence of chunk datagrams for one generation. There is exactly one envelope shape; there is no older header variant. To assemble:

- `phrase_count` is the **full-track** count on **every** chunk; it does not shrink per chunk.
- Each chunk carries one contiguous phrase slice; concatenate slices in ascending `chunk_index`.
- The tuple count in one datagram is `(argument_count − 7) / 6`.
- Assembly is complete when the concatenated tuple count equals `phrase_count`.
- Every chunk of a generation shares one `chunk_count`; require `0 ≤ chunk_index < chunk_count` and `chunk_count ≥ 1`.
- At most `32` phrase tuples ride one datagram (`MaxStructurePhrasesPerDatagram`); a 33rd would cross the ~1200-byte structure sizing target. Every datagram must also clear the `1400`-byte transport guard.

### `/rave/player/{N}/structure_state`

Reports the live cursor of physical player `N` into its held `/structure` snapshot. Rides the continuous lane at `60 Hz` beside `/clock`.

```text
Type tag: ,iiii
Arguments: structure_generation, current_phrase, beat_in_phrase, beats_to_next_phrase
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `structure_generation` | int32 | Generation of the structure this cursor belongs to; binds the cursor to the held snapshot. `0` when no structure is held. |
| `current_phrase` | int32 | One-based ordinal (position + 1) of the phrase covering the current beat, within the held structure; `-1` when no phrase covers the current beat. |
| `beat_in_phrase` | int32 | One-based beat position within that phrase; `-1` in the same no-coverage case. |
| `beats_to_next_phrase` | int32 | Beats remaining to the next phrase boundary; `-1` in the same no-coverage case. |

Examples:

```text
/rave/player/2/structure_state 7 2 1 128
/rave/player/2/structure_state 7 -1 -1 -1
```

**Cursor generation binding (mandatory).** A client MUST discard any `structure_state` whose `structure_generation` does not equal the generation of the `/structure` snapshot it currently holds. `current_phrase` is an ordinal into the held phrase list and is meaningless against a different structure; the generation gate stops a late or lossy cursor from landing on the wrong grid.

## System values

> **Deprecated transport metadata:** The `/rave/system/*` values are not part of the musical state contract and are planned for removal in a future schema version. New clients should not depend on them.

RaveSystem places these four messages in one system bundle at broadcaster startup and repeats that bundle with the discrete heartbeat, normally twice per second.

### `/rave/system/session_id`

Reports the identifier for the current OSC transmission session.

```text
Type tag: ,s
Arguments: session_id
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `session_id` | string | A randomly generated GUID formatted as 32 hexadecimal digits without separators. It changes for each transmission session. |

Example:

```text
/rave/system/session_id "10b276310cf14f77936781f006a781d5"
```

### `/rave/system/session_started_iso`

Reports when the current OSC transmission session began.

```text
Type tag: ,s
Arguments: session_started_iso
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `session_started_iso` | string | UTC timestamp in round-trip ISO-8601 format. |

Example:

```text
/rave/system/session_started_iso "2026-07-10T22:14:35.1234567+00:00"
```

### `/rave/system/frame_rate`

Reports the configured display frame rate.

```text
Type tag: ,i
Arguments: frame_rate
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `frame_rate` | int32 | Configured display frame rate. The default is `60`. This is not a measurement of received packet cadence and is distinct from the broadcaster tick-rate setting. |

### `/rave/system/event`

Reports the lifecycle-event label attached to the system bundle.

```text
Type tag: ,s
Arguments: event
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `event` | string | Always the literal `session_started`, including on heartbeat retransmissions. It is not a stream of different lifecycle events. |
