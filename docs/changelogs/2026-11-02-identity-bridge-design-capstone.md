# Changelog 2026-11-02: the identity bridge design (M1 capstone)

## Summary

ROADMAP.md's M1 ("Harden the identity bridge") was built across roughly twenty ADRs, each deciding one
narrow piece in isolation. Its own exit criterion was never "ship one more feature" — it was "the ADR is
merged," a capstone document describing the finished bridge as a whole, in one place. Written today: ADR
0047. No code changed; this is a synthesis of decisions already made; see each for its own rationale and
alternatives.

## Added

- **ADR 0047**, "The identity bridge: AuthFacade's design, end to end" — describes the shape (browser →
  gateway → AuthFacade as BFF → OpenEMR), the login flow, what a session actually is now (a server-side
  record, not a self-contained cookie), how it's reached, revoked, and refreshed, the defense-in-depth
  layers around all of it, and why the password grant's removal mattered beyond just deleting code. Closes
  with a full, explicit list of accepted risks — things this project chose not to fix, and why — rather
  than leaving them scattered across individual ADRs' own Consequences sections.

## Changed

- `ROADMAP.md`: M1 marked **Done**. Its "Remaining" section is retitled "Accepted risks": the two items
  left there (OpenEMR has no server-to-server revocation endpoint; running more than one AuthFacade
  replica is untested) were never actionable work items, they were always risk statements — the heading
  now says what they are.
- `README.md`: the AuthFacade feature-table row, the "Security review of the identity bridge" evaluation
  line, and the Documentation section's ADR pointer all updated to point at ADR 0047 as the starting point
  for understanding the design, rather than "in progress."

## Why now

The user asked to understand what "the identity bridge design" item on M1's remaining list actually was,
since it reads like a feature name but is really a documentation task. Answering that question *was* the
work: once what the capstone needed to contain was clear, writing it was mostly rereading ADRs 0024
through 0046 and stating what they add up to, not deciding anything new.
