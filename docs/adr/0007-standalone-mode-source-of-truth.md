# Standalone vs Synced mode keys off the running 4-count, owned by BeatManager

Status: accepted

The wall runs in one of two intentional modes — Standalone (self-running) or Synced (locked to live musical timing). That mode was re-derived independently at five sites from three different signals: effects gated on `IsActive` (usable tempo, `bpm > 0`), the Director gated on `IsLiveSource` (OSC transport liveness), and `PhaseLock` on its own `beat_in_bar < 1` floor. They agreed until the on-air-timing work made the Director act on transport liveness: with OSC connected but no track playing (sentinels `bpm = -1`, `beat_in_bar = -1`), the Director entered Synced, found no beat, and returned without reaching `TickStandaloneMode` — freezing sequencing while the effects correctly ran Standalone. We make the running 4-count the single mode authority, owned by `BeatManager` and read by every consumer. Neighbour to ADR-0025 (the Director/Switcher split).

## Mode authority is the running 4-count, not transport or tempo

`BeatManager.IsSynced => beatData != null && beat_in_bar >= 1` is the one authority. The 4-count is bedrock — always-on, given by the wire, never derived from the beat — so it is the truest "is a usable clock running" signal (`beat_in_bar` is `1..4`, or `-1`/absent; never a real `0`). `bpm > 0` (the old `IsActive`) was a simulator-era proxy and retires as a mode signal; `IsActive` is redefined to `=> IsSynced`. `IsLiveSource` survives only as OSC-connectivity diagnostics, never for mode. Because the wire clears `bpm` and `beat_in_bar` as a set (`ClearToNoBeat`), this is behaviour-preserving on the rig — a consolidation, not a behaviour change. Gating tempo-derived queries on the 4-count means trusting that a running 4-count implies a usable `bpm`; that coupling is true because the wire clears them together and is documented at `IsSynced`.

## Standalone means "no usable clock", including OSC-connected-but-idle

OSC connected with no track playing — or playing but not yet analysed — is Standalone, not a hold: the wall keeps its self-running rotation, matching what the effects already do via the per-datum nullable queries (ADR-0012). "Some OSC data present, but not the data we need" is Standalone. A deliberate between-tracks hold (wall waits, frozen, for the next track) would be a different feature and is explicitly not what this is. Per-datum query nullability is unchanged and is a separate concern from mode: the queries still return `null` for any specific value that is absent.

## The mode boundary owns cue teardown

Entering Standalone resets the cue planner and clears the Switcher's loaded cue. A beat-domain cue is loaded with a Unity-time start and fires from Unity time independent of the clock, so a cue loaded while Synced must be aborted when the clock drops or it fires into a dead clock. `Director.TickStandaloneMode` — now actually reached — calls a public `Switcher.AbortLoadedCue()` that clears even a locked cue. This is a fire-and-forget command, not lifecycle observation, so it respects the Director → Switcher seam.

## Rejected: a mode enum with an entry/exit event

Exposing `SourceMode { Standalone, Synced }` and raising an event on change — to host entry/exit work — was considered. A single `bool` plus reaching `TickStandaloneMode` fixes both the freeze and the cue teardown with one consumer of the boundary. The enum-and-event earns its keep only when a second consumer needs the transition; until then it is speculation.

### Amendment (2026-07-11, Hunter, beat-data-interface effort)

`IsSynced` is the flag's **only** spelling. The `IsActive` alias — this ADR's
consolidation bridge (`=> IsSynced`) — is retired: an exact alias is a duplicate
spelling, and the Data Surface serves every datum exactly once (ADR-0013). The public
`IsLiveSource` member is retired too: connectivity never earns a Data Surface offering.
The transport-liveness timeout survives as the private mechanism that clears wire state
when packets stop — which is what turns `IsSynced` false — and debug views mirror
internals downstream instead of keeping a public member alive (ADR-0016). Everything
else here stands: the running 4-count is the one mode authority, owned by BeatManager,
read by every consumer.
