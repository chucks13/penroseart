# A per-run salt varies the show between runs

The Director draws one salt and folds it into every sheet's roll stream, because the rest of the
seed is RaveSystem's structure generation and player number — counters that restart when it does,
so without the salt a player's first track deals the identical show every session. The salt does
not change while the Director lives, so rebuilds stay deterministic and the (generation, player)
handover identity is untouched; it is traced on every `SHEET_BUILT` line, so a run can be
reproduced from its log. Folding the track id in instead was rejected: that would keep each track's
show identical forever.
