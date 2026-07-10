# Bar Phase is offered on the Data Surface; consumers are never constrained

An earlier beat-data-interface ruling kept Bar Phase and Beat Fraction synthesizer-internal
("effects pull evaluated waveforms, never the clock") so all shaped motion would come from
one clock — but effects already hand-roll private metronomes when the surface withholds
position (CrystalGrowth's `selfBeatPhase`, Lightning's local gates), and the effort's
standing rule is that the Data Surface offers, it never restricts. We decided (retro
audit, 2026-07-10): Bar Phase and Beat Fraction are ordinary contrived offerings in the
clock group; the Waveform Synthesizer still owns and turns the clock — it is the clock's
main customer, not its jailer. The exposure is deliberate: do not "fix" it by re-hiding
the clock.

## Considered Options

- Synthesizer-private clock (one-clock discipline via restricted readers) — rejected:
  restricting readers is the one thing the surface never does, and it invites the exact
  hand-rolled-metronome drift the effort exists to remove.
