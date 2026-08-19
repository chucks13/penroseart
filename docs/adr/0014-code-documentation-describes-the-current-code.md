# Code documentation describes the current code

Code comments and XML docs document the code: what a symbol does right now, and the technical or
artistic reason it works that way. They carry no rulings and no history — no "maintainer-approved",
no "the wall settled on", no record of who decided a value, that it was decided, or what the code
used to do. An authority stamp is not a reason: it tells the reader a decision happened while saying
nothing about what holds the code in its shape, and it rots the moment the next decision moves the
code. State the why in domain terms ("four wall units per beat keeps the pan readable at wall
scale"), never as provenance ("preserves the wall-approved look"). Decisions live in ADRs,
discussion lives on the tickets, history lives in git; a comment may point at an ADR when the reason
is recorded there.

## Consequences

- An authority or history stamp in a doc comment is doc rot, not an authored WHY clause. The rule
  that carries WHY clauses onto replacement symbols does not preserve these: rewrite the stamp as
  the reason it stood for, or drop it when no reason stands behind it.
