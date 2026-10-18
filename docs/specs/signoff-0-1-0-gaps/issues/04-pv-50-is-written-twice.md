# 04: PV-50 is written twice in the Definition of Done

Status: ready-for-agent
Roadmap: internal

**Why:** the 0.1.0 sign-off parsed every row of `docs/definition-of-done.md` and found PV-50 on two
adjacent lines with the same text. A reader cannot tell whether a second criterion was meant.

**What:** read PV-50's history (git log of the two lines) and either delete the duplicate or, if a second
criterion was meant, give it its own ID from the branch's block (`docs/agents/numbering.md`) with the
user's word, since the Definition of Done is changed only deliberately.
