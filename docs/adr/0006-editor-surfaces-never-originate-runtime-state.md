# Editor surfaces never originate runtime state

Dashboards, custom inspectors, property drawers, and every other Unity Editor surface are
downstream mirrors: they display what the runtime core is and are rewritten to follow whatever it
becomes. Mirroring is unrestricted — the editor side may combine and derive from runtime reads
however it likes, and a debug view contrived only in the editor, to watch how the runtime
behaves, is exactly what these surfaces are for. What is rejected is the reverse flow: runtime
state, members, or features created, preserved, or shaped because an editor surface wants them —
a new operator control starts as a runtime design conversation, not as a widget needing a backing
field, and the Effect Hold (`Controller.heldEffect`, honored by the Director) is a sanctioned
observation affordance, not precedent for more.
