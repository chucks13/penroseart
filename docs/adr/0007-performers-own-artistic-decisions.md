# Performers own artistic decisions

BeatManager and Waveforms serve shared read-only musical facts and tools; Effects and Transitions
decide what their art makes of them, and nothing else in the application tells a Performer what to
do. The one channel running the other way is the Repertoire: a Performer advertises what it
supports, the rest of the application selects around those advertisements, and a declared
capability may precede its first consumer — it is a standing offer, not dead code. Authoring bases
hold neutral shared configuration and perform no automatic acquisition or replacement; a Mixer is
one Effect to the rest of the runtime and owns its children internally.
