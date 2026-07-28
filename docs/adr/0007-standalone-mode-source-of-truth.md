# Standalone vs Synced mode keys off the running 4-count

Whether the wall is Synced or Standalone is decided by exactly one signal: BeatManager's
`IsSynced`, true only while the wire's beat-in-bar reads a real 1–4. Tempo values and OSC
connectivity are not mode signals — OSC connected with no usable clock, because no track is
playing or the track is not yet analysed, is Standalone: the other intentional self-running mode,
not a hold. Entering Standalone clears the planning state — the sheet slots and the in-force
sheet — so no stale plan ever crosses a Standalone gap.
