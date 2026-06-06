# Architecture reviews

Durable home for architecture-review reports — the self-contained HTML artifacts
produced by the `improve-codebase-architecture` review process. Each report
surfaces deepening opportunities (turning shallow modules into deep ones) for one
target area of the codebase, captured at a point in time.

Reports are **decision records**, not living docs: a report reflects the codebase
as it stood when the review ran. Keep old reports rather than overwriting them, so
a subject's review history reads in order.

## Naming convention

When you move a generated report into this directory, rename it to:

```
architecture-review-<target>-<YYYY-MM-DD>T<HHMM>.html
```

- **`<target>`** — the area reviewed, in kebab-case, matching the project/subsystem
  under review (e.g. `penroseart`, `penroseart-effects`, `penroseart-beat`,
  `penroseart-output`). This is the *subject* axis: reviews of different targets
  never collide.
- **`<YYYY-MM-DD>T<HHMM>`** — the local date and 24-hour time the review was run.
  This is the *time* axis: every run gets a unique chronological point, including
  multiple reviews of the same target on the same day. ISO-style dates plus fixed
  width time sort lexically = chronologically.

Target-first ordering groups every review of one subject together when the
directory is listed, so a subject's reviews sit adjacent and in run order.

### Choosing the timestamp

Use the best timestamp evidence available, in this order:

1. Read the generated HTML report and use the report's own run timestamp if it is
   present.
2. If the report only includes a date, combine that date with the local time the
   review command started or finished, whichever is documented in the temp output.
3. If the report has no usable timestamp, use the file's modification time.
4. If none of the above is trustworthy, use the current local time and note that
   choice in the commit or handoff summary.

If two reports for the same target are generated in the same minute, include
seconds:

```
architecture-review-<target>-<YYYY-MM-DD>T<HHMMSS>.html
```

Never overwrite an existing report. A same-day re-run is a new decision record,
not a replacement.

### Example

```
architecture-review-penroseart-2026-06-06T1042.html
architecture-review-penroseart-2026-06-06T1630.html   # later same-day re-review, no overwrite
architecture-review-penroseart-effects-2026-07-12T0915.html
```

## Why rename on move

The review process writes reports to a temporary directory as
`architecture-review-<timestamp>.html` specifically so repo files never collide.
Committing a report into this directory opts into durable storage, which means the
repo's naming space — not the temp-dir timestamp — now owns uniqueness. The
`<target>-<date-time>` convention reintroduces that guarantee here.
