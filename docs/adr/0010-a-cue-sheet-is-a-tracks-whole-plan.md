# A Cue Sheet is a track's whole plan, built fresh and baked

A Cue Sheet belongs to a track: when a player's structure generation changes, `TrackCueSheet.Build`
deals that track's whole plan at once — every Cue Mark with its Effect and Transition already
chosen — deterministically from the structure, the seed, and the performer catalogs, and the fresh
sheet replaces the player's old one whole. A mark's baked assignments are never rewritten; an
override masks what a mark deals rather than editing the sheet. There is no reactive per-Phrase
planner.
