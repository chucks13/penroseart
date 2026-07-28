# Timing truth comes from the wire

The wire is the source of truth for musical timing: a fact the wire carries — grid, phrase,
position — is read from the wire, never rebuilt locally, because a local reconstruction agrees with
the source only until it drifts. Deriving values from what the wire says, or tracking something the
wire does not carry, is ordinary work; what is forbidden is keeping a second copy of a wire fact
beside the wire. Standalone Mode is not a fallback under this rule but the other intentional mode,
for when no usable clock exists (ADR-0007).
