# Penrose Wall Context

The shared glossary for the Penrose Wall project — rhythm, visuals, sequencing, hardware, control, and tooling. One agreed meaning per term, so everyone is on the same page. Definitions describe what each concept *is*, never how it is implemented; runtime shape, call order, and file-level detail live in [`docs/runtime-architecture.md`](docs/runtime-architecture.md) and the ADRs under [`docs/adr/`](docs/adr/). Add a term here the moment it needs a canonical meaning.

## Language

### The wall

**Wall**:
The Penrose Wall itself — the physical LED installation and, by extension, whatever is showing on it right now. "The wall changes" means a new Performer came on; "the wall holds still" means none has.
_Avoid_: using "wall" for the Unity preview mesh specifically; treating the preview and the installation as different subjects.

**On the Wall**:
The Effect the wall is showing right now — the **A** side while a Transition is in flight, and the current Effect otherwise. It is what a viewer sees, never where the wall is headed.
_Avoid_: reading "on the wall" as a Transition's destination; the questions that ask "are we already going there?" — self-blend and Held Effect — compare against **B**, the Effect the wall is moving toward, which is a different question with a different answer mid-move.

**Buffer**:
One frame of the wall as color values, one per logical tile. Every Effect, Transition, Mixer, and overlay reads and writes buffers; the buffer is the only thing the runtime hands to hardware.
_Avoid_: treating a buffer as a screen or an image — the tile layout is irregular; bypassing the buffer to drive tiles directly.

**Tile**:
One logical Penrose rhomb and the wall's smallest distinct visual area.
_Avoid_: treating either of the two triangles inside a rhomb as a separate Tile; treating a nearby Tile as interchangeable with it.

**Section**:
One connected physical build panel containing 50 Tiles. The wall's 18 Sections form three spatial rows of six; a Section is wall geometry, never a musical Phrase.
_Avoid_: a radial wedge; using "section" for song structure — that is a Phrase.

**Edge-and-Seam Distance**:
The number of Neighbor steps from a Tile to the nearest outer wall edge or boundary between Sections. Tiles on either seed boundary have distance zero.
_Avoid_: Ring — the values are not concentric; Radius — that is geometric distance from the wall's origin.

**Rhomb Type**:
The wall's two Penrose Tile geometries: fat rhombs with angles near 72/108 degrees and thin rhombs with angles near 36/144 degrees.
_Avoid_: reversing fat and thin; treating Rhomb Type as a color or Section.

**Neighbor**:
A Tile that shares one complete edge with another Tile. Touching only at a corner does not make two Tiles Neighbors.
_Avoid_: a Tile that only touches at a corner; a Tile that is merely nearby in the layout.

**Shape List**:
A named collection of Tile groups that traces repeated geometric motifs or symmetry groups across the wall.
_Avoid_: a Buffer; assuming every Shape List group is a closed path or has the same number of Tiles.

**Motif**:
One Shape List group considered as a single figure on the wall — a Star, a Starball, a Lotusball, a Line Ribbon. The word names the figure a viewer sees, whether or not anything is currently lighting it.
_Avoid_: the Shape List itself, which is the whole collection; assuming a Motif is closed, or that every Motif in one Shape List holds the same number of Tiles.

**Part**:
A subdivision of a Motif whose Tiles an Effect treats as one region — a Starball's five-Tile Star core and its five-Tile surround are two Parts. A Motif with no internal division, such as a Star, is a single Part.
_Avoid_: a Tile; a Shape List group; assuming every Motif has more than one Part.

**Contour**:
The Tiles that border a Motif without belonging to it, lit so the Motif reads as a shape with an edge. A Tile that belongs to any Motif is never that Motif's Contour, and where two Motifs compete for a bordering Tile only one may claim it.
_Avoid_: an outline drawn in darkness — a Contour is a colour against a colour; the Motif's own Tiles.

**Star**:
A closed cycle of five neighboring fat Tiles.
_Avoid_: Starball — that is the larger compound motif around a Star.

**Starball**:
A ten-Tile compound motif in which five fat Tiles form a Star and five thin Tiles surround it.
_Avoid_: Star; Lotusball — it uses a different fat/thin adjacency pattern.

**Lotusball**:
A connected ten-Tile motif of five fat and five thin Tiles built around one fat Tile that touches four of the others. Its fat Tiles never form a Star, which is what separates it from a Starball.
_Avoid_: Starball; assuming its groups must have the Star's closed-cycle topology.

**Line Ribbon**:
An ordered open chain of neighboring Tiles that runs across the wall, possibly shortened where it meets the wall edge.
_Avoid_: Loop — that term is musical; a closed Star or other Shape List motif.

**Performer**:
The umbrella for anything that can be put on the wall — an Effect, Transition, or Mixer — seen as something called on stage rather than as a class. The Director casts Performers; the Switcher moves them on and off. **Everything else in the system exists to serve Performers**: modules inform them, and never restrict or command them.
_Avoid_: "dancer" (the early metaphor); using "Performer" when you specifically mean an Effect vs a Transition vs a Mixer; a module that decides artistic response on a Performer's behalf.

**Repertoire**:
What a Performer advertises about itself so the Director can cast it knowingly. Two different kinds of declaration sit here: **capability** — handles Fills, handles Drops, or neither — which casting reads, and **character** — the Low/Mid/High energy sections the Performer says it suits, which is advertised and currently unconsumed. Repertoire never says what musical data a Performer reads while rendering.
_Avoid_: "profile" / "capabilities" (earlier names); treating it as configuration the Director sets — it is the Performer's own declaration; inferring a Performer's character from its class name; casting on the energy affinities today, or removing them because nothing reads them yet.

**Mixer**:
An Effect that owns and combines child Effects. To everything outside it a Mixer is one ordinary Effect; inside it may directly configure any public child state, while suppress/unison/passive behavior remains the Mixer's choice rather than a system-wide policy.
_Avoid_: special-casing Mixers in casting or switching; treating independent child behavior as an isolation boundary; prescribing one child policy for every Mixer.

### Counting and cycles

**Beat Counting**:
Musical counting on the wall is **1-based: there is no beat zero.** The first beat of a Bar is "the one", a Grid Beat runs from one, a player number runs from one, and a countdown reads "4, 3, 2, 1, change". Normalized `0..1` progress values (Bar Phase, Grid progress) are math for shaping, not counts, and do not make the counting zero-based.
_Avoid_: zero-based beat, bar, or grid indexes anywhere in musical language; reading a `0..1` progress value as a count; an off-by-one that turns "the one" into beat 0.

**Phase vs Phrase** (disambiguation):
Two words one letter apart naming unrelated things. **Phase** is position inside a repeating cycle — Bar Phase, the Grid's phase — normalized, cyclic, and answering "where are we in this cycle". **Phrase** is a named section of song structure with a beginning and an end. RaveSystem's **Track Phase** is the one place the two collide: despite its name it describes a **Phrase**.
_Avoid_: reading "phase" as a structural section; reading "phrase" as a cyclic position; letting the Track Phase name imply cyclic data.

**The One**:
The first beat of a Bar — the downbeat. **Every change lands on the one**, never mid-bar. A countdown to a change runs "4, 3, 2, 1, change", with the change landing on the *next* one.
_Avoid_: landing a change mid-bar; conflating the musical count with the `0..1` Bar Phase value.

**Bar** (a.k.a. **Measure**):
Four beats — "4-on-the-floor". The rhythmic structure runs on **powers of four**: 4 beats to a Bar, and Phrase lengths are usually multiples of four Bars, but a valid Phrase can end off that lattice. A Bar boundary falls every 4 beats, but plan-time mark placement works in Grids, not bars.
_Avoid_: assuming arbitrary bar lengths — the wall assumes a 4/4, powers-of-four structure; treating every bar downbeat as a place the wall may change.

**Bar Phase**:
The normalized position within the current measure (0 on the downbeat, 1 at the next downbeat), exposed as `BeatManager.Timing.BarProgress`. Every Waveform is evaluated against it. `Timing.BeatProgress` is the corresponding position within the current beat.
_Avoid_: "beat phase" when the whole measure is meant.

**Grid** (a.k.a. **16-Beat Grid**):
The wall's phase-keeping timing mechanism: a repeating count of where the wall sits in the music's short cycle, **nominally four bars of four**. It is **phrase-relative** — a Phrase boundary restarts the count, so the Grid a Phrase ends on is itself short. **Never assume a Grid is 16 beats**: read the count and watch it return to one. The Grid is the wall's main timing source — Phrase events (Fills, Drops) may drift off Grid, but the wall always follows the Grid.
_Avoid_: hardcoding 16 beats as a Grid's length or deriving boundaries by dividing beats; using "grid" when the whole Phrase is meant; assuming every Grid Boundary must trigger a change; conflating the wall's Grid with RaveSystem's **Beat Grid** (the analyzed per-beat → time map, which the wall does not use under this name).

**Grid Boundary**:
The crossing between two Grids — not a beat, but the seam between the last beat of one Grid and the first beat of the next, observed as the Grid count returning to one and labeled by the beat that opens the new Grid. Because the count is phrase-relative, a Phrase boundary always begins a Grid, and consecutive Grid Boundaries are therefore not always the same distance apart.
_Avoid_: calling a Grid Boundary a beat — the opening beat labels the crossing, it is not the crossing; calling every bar downbeat a Grid Boundary; assuming consecutive Grid Boundaries are always 16 beats apart; assuming a Phrase divides evenly into Grids.

**Grid Beat**:
The wall's 1-based beat within the current Grid (the wire's `beat`). A 4-beat Runway begins at grid beat 13 of a nominal Grid so the Impact Point lands on the next Grid Boundary: `13, 14, 15, 16, X`.
_Avoid_: zero-based beat-zero language.

**Grid State**:
How much to trust where the one sits on the Grid this frame, expressed as the three `GridState` values (the wire's `state`): **Locked** (the one is trusted), **Coasting** (the last good offset is held), and **Disputed** (a fresh offset disagrees with the held one). Effects read `BeatManager.Grid.State`; position may still be absent in the wire's partial coasting shape. Losing the clock makes the group's nullable facts read null; it is not a fourth state.
_Avoid_: describing Grid State as a five-level evidence ladder (retired); treating Coasting or Disputed as off-grid.

**Selected Grid Boundary**:
Retired phrasing for a Grid Boundary chosen as a change target. Prefer **Cue Mark**: the mark belongs to the Grid, and a Transition's local Impact Point hits it.
_Avoid_: using Selected Grid Boundary as the canonical name for Cue Sheet items.

**Offbeat** (a.k.a. **Half-Step**):
The moment exactly midway between two beats — the "&". Four beats to a Bar means four offbeats. The wire carries nothing about the "&", so BeatManager derives four matching lanes. `Offbeats.OffBeatMs(count)` returns milliseconds until the selected 1..4 offbeat; `Offbeats.OffBeat(count)` returns its tempo-based active window.
_Avoid_: confusing "half-step" with its pitch-theory meaning (a semitone); the nearest-upcoming gate pick (retired for On Beat — both gates answer "am I on the moment" with current-slot semantics); defining the offbeat as a Waveform — the position is the concept, the Waveform is one expression of it.

**On the Beat** (`OnBeat`):
Landing on the count — on the 1, 2, 3, or 4. The wire reports four triggers, each active for the first quarter of its beat interval. `Beats.OnBeat(count)` reads the selected trigger directly and `Beats.OnBeatMs(count)` reads that count's wire countdown.
_Avoid_: the nearest-upcoming-count pick (retired for indirection — the current count is already named, so its gate is read directly); confusing On Beat (a gate) with the Beat Pulse (a continuous wave).

**Beat Count Countdown**:
The four wire countdowns exposed as `Beats.OnBeatMs(1..4)`. For example, at count one with a 400 ms beat they may read `0, 400, 800, 1200`. `Timing.BeatAverageMilliseconds` is the beat length; the per-count values say when each named count next lands. A consumer needing the soonest or the next count derives that tiny view locally.
_Avoid_: a zero count; conflating the countdowns with the average interval; adding a second aggregate spelling to BeatManager.

### Song structure

**Track Phase**:
RaveSystem's name for the analyzed phrase signal: current/next phrase labels, active state, beats remaining to the phrase boundary or upcoming phrase start, phrase length, and phrase count. Despite the name, Track Phase describes a **Phrase** in song structure; it is not the wall's **Grid**. In the current OSC stream, `active=1` describes the current Phrase, `active=0` can describe an upcoming Phrase, and `active=-1` means unavailable.
_Avoid_: confusing Track Phase with **Bar Phase** or the wall's **Grid**; treating phrase labels as an enum; treating unavailable Track Phase as Standalone Mode while other live timing is present.

**Song Structure**:
The complete ordered Phrase list created for a track when it loads — e.g. intro, up, drop, chorus, down, up, drop, down, outro — with Fills marked anywhere within its Phrases. Each player broadcasts its own Song Structure, keyed by Structure Generation; the on-air drop and fill lanes are conveniences carved off it because those moments are used so much.
_Avoid_: treating the on-air drop/fill lanes as a separate musical source from the structure; "phrase map" or "structure phrase" as distinct concepts — the list is the Song Structure and its pieces are Phrases.

**Phrase**:
A named section of the Song Structure — intro, up, down, verse, bridge, chorus, outro, or drop. It starts and ends at phrase boundaries, contains one or more Grids, and is usually at least 8 bars / 32 beats while often doubling or extending from there; Track Phase is the on-air description of the current one. The track's Song Structure is what the Cue Sheet lays its marks against.
_Avoid_: treating a Phrase as a transition, a visual effect, or a clock source; choosing Cue Marks without reference to the current Phrase; "section" as a separate musical term — Section belongs to wall geometry.

**Phrase Ordinal**:
A structure phrase's identity: its one-based position in the assembled phrase list. Repeated and immediately adjacent identical phrase types are distinct phrases, and the structure cursor names its current phrase by ordinal.
_Avoid_: keying phrases by type or name; deduplicating adjacent identical types; zero-based phrase counting.

**Structure Generation**:
The per-player change detector for song structure: an identifier that differs whenever the held structure changes — track load, eject, or an analysis refinement of the same track. Compared for inequality only, never for ordering, and zero means never loaded. The track id is a recognition hint, never a change signal.
_Avoid_: using the track id to detect structure change; comparing generations with less-than/greater-than; treating an identical track as an unchanged structure.

**Fill**:
A short musical transition in the tail of a Phrase — from its marked start beat through the Phrase's final beat, commonly one to four beats but not bounded by four — described by `BeatManager.Fill`. The wire's one countdown lane changes meaning with `Active`; BeatManager serves it only under its readable names — `BeatsRemaining` while active, `BeatsUntil` while upcoming — beside `LengthBeats` and `Progress`, with the Stock Envelopes reached through its **Before** and **In** spans. The selected Effect or Transition owns how it responds.
_Avoid_: placing a Fill anywhere outside the Phrase tail; treating it as able to stop before the Phrase ends; expecting `Active` without the running 4-count — a Fill is a Synced Mode fact, and Standalone Mode never reports one.

**Drop**:
The climactic section of a track. A Drop is its own Phrase and starts on that Phrase's first beat. `BeatManager.Drop` has the same direct shape as Fill: `Active`, `LengthBeats`, readable `BeatsRemaining` or `BeatsUntil`, and `Progress`, with the Stock Envelopes reached through its **Before** and **In** spans. There is no separate "next drop" wire lane; the same lane describes the current or upcoming drop according to `Active`.
_Avoid_: expecting `Active` without the running 4-count — a Drop is a Synced Mode fact, and Standalone Mode never reports one.

**Energy**:
Intensity on one closed three-step ladder — Low, Mid, High. `BeatManager.Energy` exposes the current wire level, countdown, length, progress, derived `Trend`, and `Build()`/`Decay()`. The explicitly named next wire lane lives separately at `BeatManager.NextEnergy`. A **Waveform's** Energy is derived from its shape — how many peaks it has and how tightly they pack — computed from the notation itself.
_Avoid_: treating Energy labels as open text; confusing Energy (phrase-level intensity) with Levels (instantaneous audio bands); "Medium" (the middle tier is **Mid**); storing a Waveform's Energy in the Pool file or a per-entry label (it is a pure function of the notation); per-subject ladders or extra tiers.

**Loop**:
A live repeated section of the current music, surfaced as `BeatManager.Loop`. Loops are powers of four and usually preserve Grid, but they rewind or repeat beat numbers, so absolute beat progress goes stale and the same Cue Mark comes around again. The beat counter snapping back *is* the loop signal; the loop lane corroborates traces and diagnostics, never a decision.
_Avoid_: assuming a Loop means the wall is out of phase; assuming old absolute progress remains valid after a loop rewind; reading the loop lane as a decision signal; modeling a loop as its own scheduler or a Director cursor.

**Levels**:
The live low/mid/high audio band triple in three forms: **Normalized** (wire values), **Smoothed** (attack/release follower), and **Peak** (instant rise with tempo-based fall). `Levels` is never null. When the wire lane is unavailable, Normalized becomes zero immediately while Smoothed and Peak fall toward zero according to their algorithms. Every form has the same `Low`, `Mid`, `High`, `Average`, `Strongest`, `StrongestBand`, `Centroid`, and `Dominance` reads.
_Avoid_: nullable Levels; unequal capabilities between forms; treating track-relative levels as absolute loudness meters.

### The musical source

**BeatManager**:
The single musical source for the whole application. It reads the wire, contrives the derived values that follow from it, and serves both through one read-only Data Surface — which is why nothing else reads OSC directly and **no other module re-derives a musical fact**. It informs; it never restricts or commands. A module may keep domain state of its own on top of what it reads; it may not recompute the music.
_Avoid_: a second musical clock or a private re-derivation of a fact BeatManager already serves; BeatManager holding artistic response policy, consumer policy, or commands.

**Data Surface**:
The read-only face of BeatManager through which Effects, Transitions, Waveforms, and other systems pull musical data. It is deliberately shallow: `Timing`, `Track`, `Beats`, `Offbeats`, `Pulses`, `Phrase`, `NextPhrase`, `Drop`, `Fill`, `Energy`, `NextEnergy`, `Loop`, `Grid`, `Levels`, `Players`, `LiveOrder`, and the seven typed Phrase handles of the Song Structure — `Intro`, `Up`, `Down`, `Verse`, `Bridge`, `Chorus`, and `Outro`. Each group places related wire facts and derived values together. Captured structs and owned collections prevent write-back; wire sentinels never cross the boundary.
_Avoid_: `View`/`Facts`/`Span`/`Current`/`Run` navigation; a separate raw tree; duplicate aliases; hub-owned `Started`, `Ended`, `Changed`, `Wrapped`, or gate-opened flags; color policy; dropping wire lanes because nothing reads them yet.

**Contrived Value**:
A reusable value BeatManager derives from wire state, time, or multiple lanes: offbeats, progress, pulses, envelopes, energy trend, and level shaping. A **Wire Value** is passed through after sentinel translation. Both live side by side in the shallow musical group where a caller expects them; provenance never creates another navigation layer. Optional facts use `null`. Boolean questions (`Active`, `Rolling`, `On`, …) and pulse envelopes are total responses resting at false and zero — like `Levels`, whose silence and missing input share a useful zero while its followers fall according to their algorithms. Absence a caller must distinguish stays on the nullable sibling facts (counts, lengths, positions).
_Avoid_: "cooked"; effects reading the private wire snapshot directly; sentinel values crossing into effect math; separate raw/derived public trees.

**Wire Snapshot**:
The latest complete set of values decoded from the RaveSystem OSC broadcast — the On-Air Surface and the Per-Player Surface together — held privately as the source BeatManager translates into its Data Surface groups each frame. Wire sentinels live here and never cross onto the Data Surface.
_Avoid_: effects or the Director reading the Wire Snapshot directly; treating it as a public surface; letting sentinel values leak past it.

**On-Air Surface**:
The part of the RaveSystem broadcast describing the single on-air focus — the program the audience is hearing right now: beat clock, track, phrase, fill, drop, energy, levels, loop, and timing grid. This is the surface the wall has always synced to.
_Avoid_: calling it "the OSC data" as if it were the whole broadcast; assuming a value is on-air when it belongs to one specific player.

**Per-Player Surface**:
The part of the RaveSystem broadcast describing each of the six physical players (ProLink device numbers 1–6) independently of the on-air focus: its own clock, transport, loop, timing grid, song structure, and structure cursor. Exposed as the `Players` group — always six entries ordered by player number.
_Avoid_: confusing a player's values with the On-Air Surface; assuming a silent or absent player disappears from the group (it reads as unavailable, not missing).

**LiveOrder**:
The on-air deck history: the players currently in the live set, **most-recent-first**, surfaced as `BeatManager.LiveOrder`. Its first entry is the **Focus**.
_Avoid_: "Live Order" as two words in code-facing text; treating the lowest player number as the Focus; inferring the live set from clocks or levels.

**Focus**:
The first LiveOrder entry — the deck whose fader went up most recently, and the one the wall follows. Focus changing rapidly ("flapping") is the DJ riding faders through a blend: real behavior to follow, not a defect to damp out.
_Avoid_: debouncing, smoothing, or applying hysteresis to Focus so it "settles"; treating a focus change as an error; following any player other than the Focus.

**Standalone Mode / Synced Mode**:
The two intentional personalities for rhythm-aware behavior. The dividing line is a single authority — whether a usable musical clock (the running 4-count) is present. **Synced Mode** is active whenever that clock is present; the wall syncs to whatever musical timing the signal currently provides. **Standalone Mode** is the self-running art behavior whenever the clock is absent — no OSC connected at all, or OSC connected but no track playing or yet analysed — and it must look fully intentional on its own. One shared flag — spelled `IsSynced`, its only name — decides this for every consumer so they can never disagree. This is a preference, not a fallback: the wall prefers a live clock and works deliberately without one. Every musical fact stands or falls with that clock together: no track playing means no beats, which means no Synced Mode, and therefore no Fill, no Drop, no Energy, no Levels, and no measured beat interval. A consumer that has an active Fill or Drop in hand is already in Synced Mode and every beat-derived value it needs is present — reaching one of them behind such a check needs no further guard.
_Avoid_: deciding mode from transport connectivity or tempo instead of the running 4-count; multiple consumers each re-deriving the mode; `IsActive` (retired alias of `IsSynced`); effects that freeze, glitch, or go dark when the clock is absent; calling Standalone Mode a "fallback" or "default"; treating missing Track Phase (clock still running) as Standalone Mode.

**Span** (the **Before / In** pair):
A musical piece with a beginning — a Drop, a Fill, or a Phrase of the Song Structure — has exactly two named spans: **Before**, approaching the piece across a caller-named window of whole beats, and **In**, through the piece from its start to its end. Each span serves the Stock Envelopes and nothing else; both spans of a piece can be live in the same frame, and `After` was considered and deliberately dropped.
_Avoid_: `SpanView`, `.Span`, `.Current`, or forcing unlike wire shapes through one generic public type; "During", "Near"/"Far", "Approach"/"Distance" (rejected span names); a Before span with a default window.

**Edge**:
A consumer-local comparison when a system needs to know that a value changed or a moment began. BeatManager exposes current immutable state, not one-frame `Started`, `Ended`, `Changed`, `Wrapped`, or gate-opened booleans. The consumer retains the prior value whose change matters to its own behavior.
_Avoid_: manufacturing events in BeatManager merely because a caller could compare two values; confusing a frame flag with durable event delivery.

**Stock Envelope**:
The readable linear envelope pair wherever a musical duration exists: **Build** is the continuous normalized position from zero to one; **Decay** is one minus that position, falling from one to zero. Windows are counts of whole beats, but values move continuously within each beat — `Build(16)` completes during the first sixteen beats and then holds its endpoint; with no argument the window is the piece's full length, which is why a Before span (having no length of its own) always names its window. Served directly on the on-air Phrase group, Energy, and Grid, and through the Before/In spans of Drop, Fill, and the typed Phrase handles of the Song Structure. Each method rests at its nothing-happening value when its duration is unavailable or inactive: zero everywhere except a Before span's `Decay`, which rests at one — the piece reads as infinitely far, so a speed multiplier means "no response".
_Avoid_: a generic public envelope hierarchy; fractional-beat windows; naming curves after artistic gestures; treating the convenience as the only sanctioned response; a Before `Decay` that rests at zero (it would freeze multiplier consumers in Standalone Mode).

**Color Bank**:
Retired. Color mapping is artistic policy owned by the Effect or Transition using the level data. BeatManager exposes musical values only.
_Avoid_: `Rgb()`, `Hsv()`, palette reads, or configurable color-source abstractions on `LevelBands`.

### Waveforms

**Waveform**:
A one-bar rhythmic brightness envelope built by **merging humps end-to-end in time** — each hump occupies its own time slot and has a width (Duration) and a height (Amplitude). Humps are never summed or layered. Values are **unipolar `[0..1]`**: 1 at a peak (on the beat), 0 in the troughs between beats. It is an envelope, never a bipolar audio wave — there is no negative half and 0 is the trough, not a midpoint.
_Avoid_: "adding waves together" (they are concatenated in time, not summed); "true wave" / "−1 to 1" (it is unipolar); "signal", "curve".

**Hump**:
The single unit a Waveform is built from: one rise-and-fall occupying its own time slot, peaking once and returning to 0. A Waveform is an ordered run of Humps merged end-to-end. Each Hump carries a width (its Duration / note value) and a height (its Amplitude).
_Avoid_: "cycle", "wave", "pulse" for the unit — those name the whole signal, not the piece.

**Amplitude**:
The height of a single Hump, authored as a single digit `0–8` mapping linearly to `[0..1]` via digit ÷ 8 (`8` = full height, peak reaches 1; the ÷8 gives nine clean eighth-steps that land exactly on 1.0). One digit per Hump, read straight across in order, so the amplitude string sits directly beneath the sequence string as a stacked, equal-length pair. `0` makes the Hump silent — flat at 0 for its whole slot — which is how a beat is *skipped* (e.g. "measure start" = `8000`, "alternating beats" = `8080`). There is no separate gate; Amplitude `0` is the gate.

**Duration** (a.k.a. note value, the Hump's width):
How much musical time a note occupies, named by note value rather than a count. One shared ladder serves both sides of the musical vocabulary: a Hump's width **occupies** a Duration, and a notation pulse or gate **runs every** Duration. The authored range is `W` whole (the full bar), `H` half (2 beats), `Q` quarter (1 beat), `E` eighth (½ beat), `S` sixteenth (¼ beat). One token per Hump; the tokens of a Waveform, read left to right, are its widths. The sixteenth is the fastest allowed — finer rates are deliberately excluded (both musically unneeded and a full-wall flicker hazard).
_Avoid_: the retired name "Subdivision"; "frequency" or per-beat counts — these are note values, and a value slower than a quarter spans several beats, which a per-beat count cannot express.

**Duration Pulse / Duration Gate**:
The idealized-clock members of the pulse family: signals derived from the Bar Phase clock that run every **Duration** — "pulse me every eighth." A Duration Pulse peaks on each onset and decays smoothly to 0 across its cycle; a Duration Gate is its square on-off sibling, open for the first part of each cycle (strobes, ratchets). Deliberately distinct from the other three pulse offerings: the wire's `beat_pulse` (the sender's own analyzed hit), the Offbeat pulse (contrived from measured beat-time midpoints), and Waveforms (authored dance moves). Four offerings, four purposes — all valid options for effects.
_Avoid_: folding these into Waveforms (a Duration Pulse is parametric and instant, not an authored shape); the retired name "subdivision pulses/gates"; treating the four pulse offerings as duplicates of one datum.

**Beat Pulse**:
The standard rhythmic signal: a value in `[0..1]` that peaks on the quarter-note beat and falls back before the next. It is the default/canonical Waveform — the plain every-beat Preset (`QQQQ` / `8888`).
_Avoid_: equating it with the raw OSC scalar; "the one all others are generated from" (retired mental model — Pool Waveforms are authored, not derived from the Beat Pulse).

**Phase Offset**:
A per-Waveform shift, measured in beats, that slides the whole Waveform along the Bar Phase before it is evaluated. 0 leaves it on the beat; 0.5 lands it on the "&" (the Offbeat). Fractional values express swing/shuffle feel. It moves *when* the humps land without changing their shape or count.

**Rounding** (a.k.a. sharpness):
A per-waveform scalar in `[0..1]` controlling hump shape. At 0 the peak is sharp/pointed; rising first rounds the peak toward a cosine dome, then continues to grow a **flat top** — a plateau pinned at 1 around the beat. Higher rounding keeps the wall at full brightness for longer near the beat ("brighter longer"); the trough between beats still falls to 0 at every setting.
_Avoid_: "smoothing", "easing" (overloaded); treating it as a true low-pass filter.

**Waveforms** (the acquisition surface):
The shared Waveform acquisition surface beside BeatManager. Consumers draw immutable, clock-bound values from the required Pool, selecting by Energy; the held Waveform or Routine then reads its own current `Envelope` or applies caller-chosen endpoints through `Lerp(from, to)`. `None` is the explicit non-null value a Mixer uses to suppress a child's Waveform response.
_Avoid_: index addressing in any form (a Pool position may change at any time); nullable Waveform configuration; a provider-side `Evaluate` step; one-frame Waveform "Hit" state; reaching Waveforms through a BeatManager doorway (it is a sibling, not a child); runtime Waveform substitution.

**Preset**:
A named, saved Waveform spec in the Pool — a human/editor label for a `sequence + amplitude + rounding + offset` bundle. Runtime performers draw values by Energy rather than depending on a stable name or Pool position. The plain Beat Pulse (`QQQQ` / `8888`) is the canonical default.
_Avoid_: treating Preset names as runtime identity, as a fixed hardcoded enumeration, or as an exhaustive description of the (effectively unbounded) Waveform space.

**Pool** (the curated Preset set):
The hand-vetted collection of Presets that random selection draws from, so a random pick is always musically sensible. It is the **required runtime source of truth**, persisted as a **hand-editable text file in `StreamingAssets`** — named entries, plain notation, `//` comments, blank lines ignored, creatable by hand in any text editor. A missing/empty Pool, a parsed notation-invalid entry, or an unsatisfied Energy set is a configuration error and fails visibly rather than synthesizing or widening to a fallback.

**Routine**:
A four-bar choreography — exactly one nominal Grid — containing four resolved, clock-bound single-bar Waveforms composed with `Routine.Of(w1, w2, w3, w4)`. It reads exactly like a Waveform: `Envelope` is the current Grid bar's response and `Lerp(from, to)` maps that response. Without a placed Grid, `Envelope` rests at 0 and `Lerp` returns the caller's `to` endpoint. Routine retains no acquisition settings or replacement policy; a consumer wanting different values composes another Routine whenever it chooses.
_Avoid_: "Waveform Pattern" (retired name); any Routine length other than one Grid; using "pattern" for one bar's hump sequence; putting re-roll, refresh, wrap, or lifecycle policy on Routine, Waveforms, or a base hook; gating playback on Grid State.

**Visual Tool** (the waveform "designer" web app):
A standalone browser sketchpad for *seeing* what a Waveform's notation looks like before committing it. Purely a visualizer/design aid — it is not the authoring pipeline and the runtime does not depend on it or its exported JSON.

### Sequencing

**Director**:
The decision layer that owns *what* plays on the wall — which Performer comes on and which Transition moves between them. It answers the Switcher's two questions: what a planned Cue Mark plays, and what to do at a moment the plan cannot feed. It reads musical truth only from BeatManager and decides nothing about *when*.
_Avoid_: "choreographer" (retired name); using "Director" for whatever decides when a move starts — that is the Switcher's; describing the Director as something that draws buffers or owns transition mechanics.

**Mechanical Switcher** (a.k.a. **Switcher**):
The execution mechanism for the current Cue Sheet. At each Grid start, it gives an unfired, non-self-blend Cue Mark priority and owns all transition timing — Runway, Impact Point, Tail, start, progress, and completion. It reports every anomaly — a re-crossed fired mark, self-blend, or Stillness — through the single Off-Plan doorway; it chooses none of the content.
_Avoid_: putting musical or casting decisions in the Switcher; allowing Off-Plan recovery to preempt an unfired Cue Mark; treating the plan as a statement of what must be on the wall at a given beat; calling it "dumb" instead of execution-only; a second doorway or a Switcher-local fallback choice.

**Cue**:
The directive for a musical change. A Cue is not the change itself — it *triggers* one: a stage-directed Cue directs the Switcher to swap Performers at a Cue Mark, and an effect-directed Cue tells the on-screen effect to respond ("respond to this fill", "play at this energy"). A Cue carries intent, never pixel-level commands.
_Avoid_: "call" (collides with calling a Performer on stage); treating a Cue as the change itself; a Cue that micromanages a Performer's internal parameters.

**Cue Sheet**:
A track-scoped show plan built when a track's complete Song Structure arrives. Every Cue Mark is placed against the real Phrase map with its Effect and Transition assignment baked in; one sheet describes one track on one player from start to finish.
_Avoid_: the retired per-Phrase empty-marks sheet; treating it as a queue of pending cues; treating the sheet as something an override or an Off-Plan Cue edits.

**Cue Mark**:
A Grid Boundary in a Cue Sheet where a stage-directed Cue musically lands, carrying its baked Effect and Transition assignment, and labeled by the beat that opens the new Grid. Marks sit at Grid Boundaries and nowhere else because a cue needs the crossing: its Runway runs on the Grid before the boundary and its Tail on the Grid after. A Phrase boundary always begins a Grid, so a Phrase end is an ordinary candidate, never a mandate. Where a Drop or Fill owns a boundary, the Anchor rule applies: a capable Effect is already on the wall and the moment is cleared of Runways and Tails. Once a Cue Mark fires, its check-off remains set for the life of that Cue Sheet; re-crossing it never makes it unfired.
_Avoid_: calling a Cue Mark a beat position, an Impact Point, a Transition start, a Transition Completion, or a Selected Grid Boundary; empty marks awaiting cast-time selection (retired); replaying a fired Cue Mark after a Loop ends.

**Cast**:
The Director's act of handing the Switcher the Cue Sheet now in force — the cast list for the on-air track. An Off-Plan Cue is not a Cast: the Switcher already holds the plan, and the Director simply answers with one freshly dealt card.
_Avoid_: cast-time selection (retired — selection happens at sheet build); casting individual Cue Marks at their Runway start (retired — that put transition timing in the Director); a lock or revocation window after casting.

**Anchor**:
A moment owned by a Drop landing or live Fill window: a capable Effect is already on the wall, and no Transition's Runway or Tail crosses it.
_Avoid_: "Anchor treatment" (retired — there is one way to perform an Anchor, not a choice of two); scheduling any Transition whose Runway or Tail crosses the moment; treating every phrase boundary as an Anchor; resolving Anchors at cast time (retired); treating an Anchor as a plan for where a Fill begins or how long it runs.

**Ride-through**:
How an Anchor is performed, and one of the Director's two doorway answers: the Drop/Fill-capable Effect already on the wall simply plays through the moment, its own live Drop/Fill response carrying the hit rather than a new Performer arriving.
_Avoid_: cutting to a new Effect on the landing beat and calling it a ride-through; "Performed Transition" (retired — an Anchor is never performed by a Transition).

**Bag**:
The seeded fairness mechanism sheet building deals from: one shuffled bag per catalog (Effects, Transitions), dealt a card at a time so the whole catalog shows before anything repeats, and reshuffled once it empties. The bag *is* the fairness — no scoring, weighting, or preference sits behind it.
_Avoid_: weights or scoring; filtering the deal by energy affinity (energy is a Performer input, not a casting one); per-cast random picks; treating a one-card catalog's unavoidable repeat as a defect.

**Off-Plan Cue**:
The Director's answer through the single anomaly doorway when no unfired planned Cue has priority — for a re-crossed fired mark, self-blend, or Stillness. It supplies a fresh eligible move or a ride-through, uses the normal scheduler to land at the closing Grid Boundary, and leaves the Cue Sheet and its check-offs unchanged.
_Avoid_: "staleness cue" (retired name); calling it a Cast, an override, or a Missed Cue's replacement; treating it as something that spends a Cue Mark.

**Off-Plan Sighting** (a.k.a. **Sighting**):
The Switcher's report through the anomaly doorway: the anomaly, Grid Boundary, Stillness gap, ask number, and the Effects on the wall and in flight. The anomaly kind is diagnostic; it never changes the deal.
_Avoid_: reading the anomaly kind as a decision input; the Director reaching back into the Switcher for anything the Sighting should carry; a second doorway or a looser-argument variant of the question.

**Stillness**:
How long the wall has held still — whole Grids since the last fired Cue, checked at every Grid start. It is a property of the wall, not of any sheet: only a fired Cue resets it; a handover changes nothing on the wall and resets nothing.
_Avoid_: counting Stillness in beats or beat positions; treating Stillness as sheet state a handover restarts; gating it on a Loop or any other condition.

**Ceiling**:
The bound on Stillness: three Grids since the last fire means the fourth Grid must fire, short or not. A Director-built sheet never violates it on its own — only sheet swaps and loops push the wall toward it — and the Grid-start check catches both through the anomaly doorway.
_Avoid_: a ceiling counted in heard beats (retired); treating a Ceiling take as an immediate fire; taking a Ceiling Cue anywhere but a Grid start.

**Missed Cue**:
A Cue Mark the playhead went past without firing, because its Runway beat elapsed while the wall was somewhere else — a fresh Cast, a mid-track focus handover, a needle-drop, a late entry, or an inspection freeze. A cue *is* its Runway, Impact Point, and Tail, so one whose Runway is already behind the playhead cannot be performed as written and is therefore not performed at all. A missed mark is not spent: it is still plan.
_Avoid_: performing a missed mark on the current beat and still calling it that mark's cue; treating a passed mark as spent, or as a debt the next Off-Plan Cue pays off.

**Loaded Cue / Lock Point / Standby Cue**:
Retired vocabulary for holding a cue between deciding it and performing it. The Switcher holds none of them: what it holds is a plan and a beat to recognise, not a cue lifecycle and not a queue.
_Avoid_: reintroducing commitment verdicts, lock latching, or a revocation window as vocabulary; reading what the Switcher holds as a cue-lifecycle surface.

**Cue Log**:
The per-run diagnostic record of sequencing decisions — one timestamped line per event in a session file, written as things happen. It is an operator-facing trace, downstream of every decision and never an input to one.
_Avoid_: reading the Cue Log back into runtime behavior; treating a missing or failed log as a runtime fault (a broken log must never take the wall down).

### Overrides and inspection

The override and inspection surface below is debugging and verification tooling — for testing Performers and steering inspection, never show behavior. It must never degrade the show model.

**Fire-and-forget**:
The rule governing every performed move: **nothing changes a Transition once it is in flight.** A later pick or decision applies to a later move, not the current one.
_Avoid_: re-deciding, re-targeting, or re-timing an in-flight Transition; treating a staged pick as retroactive.

**Next Transition**:
A staged override for the Transition of the very next Cue Mark performed: a one-shot pick that replaces exactly the next dealt card, after which the plan resumes verbatim; with Hold Selected it trumps every deal until released. Overrides mask the Cue Sheet, never edit it.
_Avoid_: treating a staged pick as permission to bypass Runway, Tail, or Impact Point timing; mutating the sheet; expecting the displaced card to play later.

**Next Effect**:
A staged override for the destination Effect of the very next Cue Mark performed — the same one-shot mask semantics as Next Transition. It lets a person steer what the wall moves toward without disturbing the plan.
_Avoid_: confusing the Next Effect with the currently on-wall Effect; using an effect hold to freeze the wall when the goal is to steer the next destination; mutating the sheet.

**Show Now**:
The debug override that starts a move toward a selected Effect at once. It is not a cut, has no Cue Mark, and does not edit the Cue Sheet; the plan resumes at its next unfired Cue Mark.
_Avoid_: cutting instantly to the Effect (retired); confusing it with Next Effect, which changes what the *next Cue Mark* moves toward; expecting the pick to survive the next Cue Mark (that is a Held Effect).

**Held Effect** (a.k.a. **Hold**):
One operator selection with two states. **Random** permits normal switching. Choosing an Effect moves the wall to it once through an ordinary Transition, then stops stage-directed switching until Random is chosen again. The held Effect still reads the music and performs its own Drop and Fill responses. Cue Marks passed during Hold lapse instead of waiting for release. In Standalone Mode, Hold pauses the change cadence and resumes it from the same point.
_Avoid_: confusing Held Effect with Hold Selected; modeling Hold as a Director selection decision, or as a second sequencer that commands the Switcher around the Director; expecting the Cue Marks a hold covered to be deferred — they lapse like any other passed mark.

**Hold Selected**:
A Tuning Window mode where the selected Effect or Transition remains the Director's next choice after each move completes. Turning it off returns that choice to normal random selection.
_Avoid_: confusing this with Held Effect; Hold Selected keeps the Director/Switcher path running and the wall still changes, while Held Effect stops switching altogether.

### Transitions

**A-to-B Transition**:
A move from the current on-wall Effect (**A**) toward the destination Effect (**B**). Its visible position is described as progress from 0 to 1: 0 is fully A, 0.5 is exactly between, and 1 is fully B. Once started it is visual execution according to its Transition Settings.
_Avoid_: treating every Transition as if its only goal is to complete on a Cue Mark; treating transition progress, completion, or busy state as music-structure evidence.

**Transition Repertoire**:
The declaration of the A-to-B move a Transition offers: its Runway, Tail, Shape, and Intensity. This lets the Director cast a Transition that fits the space it is given while the Switcher uses the Transition's own timing shape to perform it at its Cue Mark.
_Avoid_: Fill/Drop tags as a casting input (retired with the Performed Transition — an Anchor's moment is cleared of Transitions, not served by one); treating Repertoire as per-cue instructions or as state the Director sets.

**Transition Settings**:
Saved authoring values for a Transition's Repertoire and human-tweakable creative knobs. Settings determine the Transition's Runway and Tail, which imply where its Impact Point falls; the Switcher uses that declaration to perform the move without compensating scheduling logic. Saved settings are part of the live Transition Repertoire, not just editor tuning notes.
_Avoid_: putting pure algorithm invariants into Settings; treating every numeric literal as a setting; using the Director to compensate for invalid Transition Settings.

**Code Defaults**:
The transition-authored baseline values used to create or restore Transition Settings. Code Defaults live with the Transition's source so changing a Transition and changing its intended defaults stay together.
_Avoid_: making saved Settings the only source of truth; forcing artists to hunt generated asset files just to return to the Transition's intended defaults.

**Transition Shape**:
The broad visual family of an A-to-B Transition — e.g. Blend, Channel Blend, Directional Wipe, Index Wipe, Dissolve, Iris, or Noise. Shape helps avoid treating two same-duration Transitions as equivalent when they read very differently on the wall.
_Avoid_: using Shape to describe musical timing; timing is Runway/Tail/Impact Point.

**Transition Intensity**:
How forcefully a Transition reads as a musical move: Subtle, Medium, or High. Intensity is a casting hint for ordinary phrase motion versus bigger events such as Drops or high-energy changes.
Its middle tier is **Medium** — deliberately not Energy's **Mid**, because the two are unrelated ladders and should not read as one.
_Avoid_: treating Intensity as brightness or audio level; it describes the visual force of the Transition itself; matching Intensity tiers to Energy tiers by name.

**Impact Point**:
Where a Transition's main visual hit falls inside its own A-to-B move. This is **transition-authoring vocabulary**: it is placed entirely by Runway and Tail rather than authored or set on its own, and it is **not runtime state** — nothing schedules by it, stores it, or steers it.
_Avoid_: a runtime field, parameter, or state named for the Impact Point; treating it as a phrase boundary, Cue Mark, Grid Boundary, or Transition Completion; assuming every Transition's main hit happens at progress 1.

**Runway**:
The lead-in before a Transition's Impact Point — how many beats before the Cue Mark the Switcher must start the Transition so the Impact Point reaches that mark on time. Runway is authored directly as part of the Transition Repertoire and may be zero, in which case the Transition hits immediately on the Cue Mark.
_Avoid_: using Runway to mean the whole Transition; requiring a fake one-beat Runway for hard cuts.

**Tail**:
The part of a Transition after the Impact Point. Tail is visual resolution to B after the Transition has hit its mark; it has no effect on Phrase, Cue Sheet, Cue Mark, or Grid Boundary decisions.
_Avoid_: treating post-impact motion as late or wrong; treating Tail completion as a musical scheduling event.

**Transition Duration**:
The full length of an A-to-B Transition from start to Completion, measured in beats. Duration is Runway plus Tail, and zero duration is a hard cut.
_Avoid_: using Duration when only the pre-impact lead time is meant; that lead time is the Runway.

**Transition Completion**:
The moment an A-to-B Transition has fully reached B. Completion may happen on the same beat as the Impact Point or after it, depending on the Transition Repertoire.
_Avoid_: assuming Completion is the timed musical target; the timed target is the Impact Point.

### Effect configuration

**Effect Settings**:
The umbrella for everything an Effect is tuned by — its Standalone Defaults, Sync Defaults, Standalone Settings, and Sync Settings taken together. Every Effect has all four, Mixers included, since a Mixer is an Effect like any other.
_Avoid_: using it for any single one of the four; reading it as one storage location; assuming every Effect expresses them the same way — each Effect is hand-built and takes the shape that suits it.

**Standalone Defaults**:
An Effect's authored values for how it looks with no music. They sit at the top of that Effect's own source file and change only by editing that file; they are the one authored record of the look, and what Standalone Settings restore to.
_Avoid_: making them editable on an editor surface (displaying them read-only is fine); **Code Defaults**, which is the Transition-side term for a different arrangement.

**Sync Defaults**:
An Effect's authored values for its musical response, sitting at the top of the same file beside the Standalone Defaults and likewise changed only by editing that file. They are what Sync Settings reset back to.
_Avoid_: keeping them in the saved settings instead of the file; classing a value that shapes the Standalone look as a Sync Default.

**Standalone Settings**:
What an Effect reads in Standalone Mode: a saved copy that can be tweaked while the wall runs and restored to the Standalone Defaults at any moment.
_Avoid_: treating a live tweak as finished authoring before it has been written back into the file; changing an Effect's intended look by editing the saved copy rather than the Standalone Defaults.

**Sync Settings**:
What an Effect reads in Synced Mode: a saved copy that can be tweaked while the wall runs and reset back to the Sync Defaults at any moment.
_Avoid_: treating a live tweak as finished authoring before it has been written back into the file; changing an Effect's intended values by editing the saved copy rather than the Sync Defaults.

**Roll**:
The moment at activation when an Effect determines every value it randomizes and discards every piece of carried motion state. A re-roll is the same determination run again on reactivation; after any Roll, nothing from the previous activation shows on the wall.
_Avoid_: partial rolls that redraw random values but keep stale orientation or progress; confusing the Roll with camera roll, the aviation-sense rotation some Effects animate.

**Rail**:
The lowest or highest value a range's tuning slider spans — per-range calibration for live tweaking, carried by the range itself and saved with the settings. Authored defaults seed Rails from the range's endpoints; a Rail stretched during tuning lives only in the saved asset.
_Avoid_: treating Rails as the Roll bounds (the endpoints are); writing Rails back into Standalone or Sync Defaults, which carry no constants for them.

### Authoring surfaces

**Play Mode**:
Unity Editor state where the wall runtime is actually running: effects render, the Director makes choices, transitions execute, inputs may be live, and authoring tweaks are judged by watching the moving wall. Tuning changes made in Play Mode should persist when the run stops unless explicitly discarded.
_Avoid_: treating Play Mode edits as throwaway previews; confusing Play Mode with a built standalone Player.

**Edit Mode**:
Unity Editor state where the wall runtime is not running. Authoring changes made here configure the next run but are not being judged against a live moving wall.
_Avoid_: assuming Edit Mode authoring is the only durable authoring path; forcing creative tuning to happen without seeing the wall in motion.

**Tuning Window**:
The canonical Unity workspace for detailed wall observation and Transition tuning. Its focused Live, Rhythm, and Transitions tabs show live sequencing state — including the Cue Sheet tracker, which lays the in-force plan out by Grid — alongside musical state and saved Transition Settings; the compact Controller Inspector opens it directly. It may steer the Director's Next Effect and Next Transition without taking timing ownership away from the Director. Effect authoring has no dedicated window surface yet.
_Avoid_: burying detailed observability in the narrow Controller Inspector; presenting unfinished Effect authoring as a real workspace; making a fake preview path that does not exercise the Director and Switcher.

**Phrase Event View**:
The editor-side display model of a phrase event (a **Fill** or a **Drop**): its status chip, meter fill, one-line readout, and a Now/Soon/Idle state, derived from the direct Fill or Drop values. It never feeds state back into BeatManager.
_Avoid_: duplicating "how a Fill/Drop reads" per surface; folding the chip *color* (an editor concern) into the view — the view only classifies the state.

**Rhythm Text**:
The shared text vocabulary for nullable beat/count facts on the Data Surface — a beat count reads as "16b", a plain count as its number, and **`null` reads as "—"**. One vocabulary so every rhythm readout speaks the same way, keeping the **Contrived Value** rule that "null means not-available" visible in the UI.
_Avoid_: re-deriving the "—"-for-null formatting per row; treating "—" as an error rather than the ordinary absent state.
