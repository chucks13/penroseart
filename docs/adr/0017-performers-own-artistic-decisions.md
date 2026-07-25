# Performers own artistic decisions

Status: accepted

BeatManager and Waveforms provide shared read-only musical facts and tools; Effects and Transitions decide how to use them and expose their artistic configuration as ordinary public object state. Ownership means the Performer chooses acquisition and artistic endpoints, not that every mechanical playback step must be repeated at every call site: a held Waveform or Routine may expose its current `Envelope` and apply caller-supplied `Lerp(from, to)` endpoints without choosing what the response means. Authoring bases may hold neutral shared configuration but perform no automatic acquisition or replacement. A Mixer is one Effect to the rest of the runtime, while internally it owns its child instances and may configure, suppress, synchronize, or combine them directly; the runtime standardizes no child behavior policy.
