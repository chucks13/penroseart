# Sequencing gains the Standalone/Synced dual-mode: a Director directs, a Switcher executes

Status: accepted

Effect *rendering* already chooses between Standalone behavior (self-running when OSC data is absent, or when a specific query is null) and Synced Mode (driven by live musical structure through the nullable rhythm queries, ADR-0002); *sequencing* did not — it ran off a single wall-clock timer in `Controller.OnTimerFinished`, which mashed the decision of *what and when* to change together with the *mechanism* of changing. We lift the same duality up to sequencing: a **Director**, polled every frame, owns the cadence and decides what plays and when, and a **Switcher** extracted from `Controller` executes those decisions as pure mechanism. In Synced Mode the Director reads live OSC timing and lands changes on the one; in Standalone Mode, when no OSC data is available, it free-runs its own cadence. This is a preference between two fully intentional modes, not a fallback to a degraded path. The `OnTimerFinished` state machine leaves `Controller`: its decide-half becomes the Director, its execute-half the Switcher.

## Considered options

- **Effect-first with a Director fallback** — the on-screen effect drives its own changes and the Director steps in only when an effect goes quiet. Rejected: it splits "who decides" and reinstates the decide/execute mashup. The Director always decides; an effect's Repertoire and live musical structure are *inputs* to that decision, never overrides.

## Consequences

- **The Mechanical Switcher is execution-only.** It exposes switching commands such as `ShowNow`, `StartTransition`, `RenderAtTime`, and read-only presentation state. It owns active A-to-B progress, Tail completion, and promotion to B after a move starts; it owns no selection or musical interpretation. The Director decides what and when to start from musical timing; it does not supply transition progress, call normal completion, or branch on Switcher busy state. If another start command arrives while a Transition is rendering, the Switcher treats the latest command as the move to render.
- **Timing is beat-denominated, not wall-clock.** Beats count from one (no beat zero) in powers of four — four beats to a bar. A transition's Runway and Tail are beats; both are non-negative, their sum is capped at 12 beats, and zero/zero is a valid hard cut. The minimum cadence between changing Performers is 16 beats / 4 bars. The Director reads the incoming OSC musical timing/structure and starts a move so the Transition-local Impact Point lands on the intended beat; Standalone Mode still uses wall-clock `effectTime` for free-running cadence while the selected Transition's own default duration controls rendering. Landing a transition on a Drop is a beat-counted scheduling decision — fire when the incoming event's beat countdown equals the selected transition's Runway, or on the boundary when Runway is zero — with no sub-beat math because the incoming musical state already carries the runway.
- **The Director directs; effects express.** A Cue selects which of a target effect's Repertoire capabilities to engage and when; the effect pulls the live event data itself and owns *how* it responds. Repertoire is a `[Flags]` virtual property on `EffectBase` (`HandlesFill` / `HandlesDrop`, default `None`), discovered per-class through the existing `Factory<T>` reflection rather than a registry or ScriptableObject; it also biases which Performer the Director casts.
- **Hold is an inspection freeze.** Holding an effect suspends the Director entirely — no rotation, reaction, or transitions — so it can be tweaked live. This replaces the `ApplyHeldEffect()` re-assert-every-frame patch, which existed only because the deck could rotate away from a held effect.
- **Core/editor two-layer rule.** Director, Switcher, Repertoire, Cue, and Hold are plain core C# (`Assets/core`): UnityEngine types are fine, no `UnityEditor` dependency, state held as serializable fields. Inspecting and live-tweaking them belongs in the editor layer (`Assets/Editor`), never by coupling core to the inspector.

## Amendment 2026-06-19

We refined the Switcher consequence from Director-supplied progress/completion to fire-and-forget execution: the Director still owns musical timing and start decisions, but the Mechanical Switcher owns transition progress, Tail completion, B promotion, and last-command-wins replacement after `StartTransition`. We also clarified the Transition timing contract as non-negative Runway/Tail with `Runway + Tail <= 12`, including zero/zero hard cuts. This keeps the original Director/Switcher split while removing the shallow completion/progress seam that let mechanical execution leak into musical planning.

## Amendment 2026-07-05 — cadence details updated by ADR-0011

The split this ADR made is unchanged and remains the governing shape. Two Synced Mode details are superseded: the Director now wakes once per new beat rather than being "polled every frame", and it reads musical truth only from BeatManager, never incoming OSC directly. The 16-beat minimum between Performer changes survives as a Cue Sheet construction constraint (minimum Cue Mark gap) rather than a runtime cadence check.

## Amendment 2026-07-24 — "when" belongs to the Switcher; this ADR's firing rule is what it runs

ADR-0020 moved the *when* of a change from the Director to the Switcher, which leaves this ADR's "a Director,
polled every frame, owns the cadence and decides what plays and when" false in its second half: the Director
decides *what*, and the Switcher decides when to fire it. Recorded here because ADR-0020 declared it superseded
only ADR-0019, leaving that sentence reading as though it were still in force.

The beat-denominated timing rule in the consequences above is unchanged, and is now literally what the Switcher
runs: fire when the beat countdown equals the selected Transition's Runway, or on the boundary when Runway is
zero. The firing decision is beat-counted, exactly as this ADR required — sub-beat position only places the
start instant within the beat that fires, and never decides whether to fire. See ADR-0020's 2026-07-24 and
2026-07-25 amendments for waiting on that beat rather than testing whether it has gone by, and for the Grid
Boundary count that keeps the wall moving when the plan has nothing left to fire.
