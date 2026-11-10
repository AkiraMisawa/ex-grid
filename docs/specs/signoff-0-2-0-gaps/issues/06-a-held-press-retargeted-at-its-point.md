# 06: Does ADR-0021's rejection of replay-time target discovery reach a held press?

Status: needs-triage
Roadmap: internal

**Why:** a press held while the grid is busy, whose control was painted again meanwhile, is retargeted by
`elementFromPoint` at the press's point when it is replayed (`packages/core/src/dom/grid-view.ts`). #539 tried
landing it on nothing and took that back, since CI's `row-command-order.spec.mjs` showed what the retarget carries.
ADR-0021 rejects replay-time target discovery, and CLAUDE.md's rule 2 repeats it for the touch additions; whether
that reaches this press is not written anywhere. The 0.2.0 sign-off left it for an ADR (the [sign-off](../../../../verification/2026-11-09-linux-signoff/results.md), "For
after 0.2.0").

**What:** an ADR, decided with the user, that says whether a held press may find its target at replay, and how a
press carries what it was taken against otherwise (the spine's sixth principle). The code and its tests follow the
decision.
