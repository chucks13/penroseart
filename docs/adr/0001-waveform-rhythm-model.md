# Waveform rhythm model

Three properties of the Waveform are deliberate and meant to be hard to change. **Humps
concatenate in time, never sum**: a Waveform is an ordered run of Humps laid end-to-end, which
keeps it an envelope — always `[0..1]`, trough at 0 — rather than a signal, and makes evaluation
"which slot is the playhead in"; the web designer app implied an additive model and we rejected
it. **Sixteenth is the fastest Hump width**, a safety limit rather than a notation one, because a
32nd note at typical tempo strobes the entire wall at roughly 17 Hz. **Nothing silently
substitutes for broken notation**: the parser reports defects and stays evaluable so authoring
surfaces can show what is wrong, while runtime Pool acquisition refuses a malformed entry outright
instead of falling back to the Beat Pulse — a silent fallback would hide the authoring mistake it
covers.

The model, notation, file format, and the split between tolerant parsing and strict runtime
acquisition are documented in `docs/waveform-system.md`.
