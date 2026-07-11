# OSC Client Contract

> **Schema version:** 3  
> **Document date:** 2026-07-10  
> The OSC schema version follows the recording format version. Any change to OSC addresses, argument shapes, types, or emitted values requires a new recording format version.

This document defines the client-visible meaning of the OSC state feed. Addresses appear in the same order as the published value display; clients must match messages by OSC address and must not depend on this display order or bundle order.

## Transport and delivery

RaveSystem sends OSC 1.0 over UDP to the configured network broadcast address. The default broadcast port is `7000`.

Schema version 3 does not negotiate or announce its schema version on the wire. Client and server releases must agree on the version out of band; this document defines version 3.

UDP delivery is not guaranteed. Clients must tolerate dropped, duplicated, delayed, and out-of-order datagrams. Every broadcast state message represents current state rather than a once-only event, so clients should replace the previously stored value for that address. Values that count down or move with playback may also move backward after seeking, scratching, or looping.

Messages are divided into two delivery lanes:

| Lane | Default delivery | Addresses |
| --- | --- | --- |
| Continuous | Every broadcast tick, normally `60 Hz`. | `/bpm`, `/beat`, `/bar`, `/next_bar_ms`, `/beat_in_bar`, `/beats_count_ms`, `/on_beats`, `/beat_avg_ms`, `/beat_pulse`, `/levels` |
| Discrete | When any discrete value changes, repeated three times; also sent as a complete heartbeat normally twice per second. | `/players_live`, `/track`, `/track_id`, `/total_beats`, `/phrase_state`, `/next_phrase_state`, `/drop_state`, `/fill_state`, `/energy_state`, `/next_energy_state`, `/loop_state`, `/timing_grid` |

Every abbreviated address in this table uses the `/rave/onair` prefix. Messages sent from the same captured state share one OSC bundle timetag. A client joining mid-session can wait for the next heartbeat or request an immediate snapshot.

### Snapshot request

When query/reply is enabled, RaveSystem listens on UDP port `7001`. Send this argument-free message to request the current continuous and discrete on-air bundles:

```text
/rave/snapshot/onair
```

The two reply bundles are sent back to the request's source address and port with a shared timetag.

### Registration acknowledgement

Schema version 3 also accepts:

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

The identifier is meaningful only within the source media and library that assigned it. It is not a portable or globally stable track identity. Existing schema-version-3 clients may use it as a source-local change signal, but its planned removal means new clients should not take a dependency on it.

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
| `total_beats` | int32 | Total number of musical beats in the track, or `-1` when unavailable. |

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

Schema version 3 can briefly publish `/beat` and `/bar` from different instants. Clients that require a coherent coordinate should calculate `bar` from an available `/beat` using the formula above.

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

After a seek, scratch, reverse movement, or loop jump, schema version 3 can briefly continue counting toward the previous boundary.

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

Schema version 3 can briefly publish `/beat` and `/beat_in_bar` from different instants. Clients that require a coherent coordinate should calculate `beat_in_bar` from an available `/beat` using the formula above.

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

After a seek, scratch, reverse movement, or loop jump, schema version 3 can briefly identify the previous four-count.

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

After a seek, scratch, reverse movement, or loop jump, schema version 3 can briefly identify the previous beat label.

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

Despite the schema-version-3 field description calling this a moving average, it does not average samples over a historical time window.

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
| `irregular` | int32 | Schema version 3 emits `1` when `length_beats` is not divisible by 16, `0` when it is divisible by 16, or `-1` when unavailable. |

For a phrase of length `N`, `count_beats` is `N` on its first beat and `1` on its final beat. Phrase length is independent of the repeating 16-position timing grid: during a 25-beat phrase, the timing grid reads `1..16`, then `1..9`, and the following phrase resets it to `1`. Phrase names use the exact case-sensitive vocabulary defined below.

Example:

```text
/rave/onair/phrase_state "Up" 6 16 0
```

Valid phrases can have lengths that are not divisible by 16. Clients must not use schema version 3's `irregular` argument to reject, repair, or reinterpret phrase boundaries.

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

These six names are the canonical vocabulary. Schema version 3 can also emit a legacy non-empty phrase label when canonical phrase state is not yet available. Clients must accept an unrecognized non-empty label as an opaque phrase name rather than failing or mapping it by string pattern. Numbered or unfamiliar labels must not be treated as additions to the canonical vocabulary. An empty string is the unavailable sentinel and is not a phrase name.

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
| `remaining` | int32 | Schema-version-3 count of drop occurrences whose designated drop point has not yet passed; `-1` when unavailable. |

Examples:

```text
/rave/onair/drop_state 0 6 16 1
/rave/onair/drop_state 1 16 16 1
/rave/onair/drop_state -1 -1 -1 -1
```

In the first example, the next 16-beat drop begins in 6 beats and is the final remaining drop. In the second, that drop is active and has 16 beats remaining.

In schema version 3, `remaining` can become `0` while `active` is still `1` after playback passes the active drop's marker. Clients must use `active`—not `remaining > 0`—to determine whether a drop is currently running.

### `/rave/onair/fill_state`

Reports one musical fill selected from across all live players. A fill is a transition section at the tail of a phrase and ends with that phrase.

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

Schema version 3 selects the event as follows:

1. Any active fill outranks every upcoming fill.
2. Among events with the same `active` value, the smaller `count_beats` wins.
3. If the beat counts are equal, the first player in live order wins.

The four arguments always describe that one selected player's event; values from multiple players are never averaged or added together. If no live player reports an active or upcoming fill, the complete unavailable shape is emitted.

When live players differ in tempo or phase, schema version 3 may not select the fill that occurs first in real time. Clients must interpret this value as RaveSystem's selected fill rather than an absolute chronological ordering of every live player's fills.

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

Schema version 3 has two client-visible limitations:

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

## System values

> **Deprecated transport metadata:** The `/rave/system/*` values are not part of the musical state contract and are planned for removal in a future schema version. New clients should not depend on them.

Schema version 3 places these four messages in one system bundle at broadcaster startup and repeats that bundle with the discrete heartbeat, normally twice per second.

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

Reports the lifecycle-event label attached to the schema-version-3 system bundle.

```text
Type tag: ,s
Arguments: event
```

| Argument | Type | Meaning |
| --- | --- | --- |
| `event` | string | Always the literal `session_started` in schema version 3, including on heartbeat retransmissions. It is not a stream of different lifecycle events. |
