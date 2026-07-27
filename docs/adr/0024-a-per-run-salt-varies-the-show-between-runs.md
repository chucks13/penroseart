# A per-run salt varies the show between runs

Status: accepted

The sheet seed was exactly (structure generation, player number), and both counters are RaveSystem's:
they restart when it does. So the first track loaded on a player dealt the byte-identical show every
session — same first effect, same first transition, same marks — which read as canned. We decide the
Director draws one random **salt** per run and folds it into every sheet's roll stream (plan walk and
Off-Plan deals alike). Within a run nothing changes: the salt is constant, so rebuilds are still
deterministic and the Switcher's (generation, player) handover identity is untouched. Across runs,
every show is fresh.

This deliberately gives up cross-run byte-identity, which ADR-0019's "the same load always rebuilds
the identical sheet" implied. That property's only real value was debugging reproducibility, so the
salt is traced on every `SHEET_BUILT` line — a run can be reproduced by rebuilding with the salt from
its log. The alternative — folding the track id into the seed — was rejected: it would keep each
track's show identical forever, which is the complaint, not the cure, and ADR-0011/ADR-0019 keep
Track ID out of identity and seeding.
