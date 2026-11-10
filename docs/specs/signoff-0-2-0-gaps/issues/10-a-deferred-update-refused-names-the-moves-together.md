# 10: A refused Update under Defer Layout Update names the moves together

Status: ready-for-agent
Roadmap: ExPivot — When Update is refused under Defer Layout Update, the reason says the deferred moves together make too many combinations, not that each one does.

**Why:** a refused Update under Defer Layout Update names each deferred move that adds fields with ADR-0370 section
3's sentence for one drop: "Currency in Columns makes more than 200,000 combinations, … Trade date in Rows makes more
than 200,000 combinations, … Quantity in Rows makes more than 200,000 combinations"
(`verification/2026-11-07-linux-signoff/measure/pv21/run-cap.log`, in the run at rc.3), which no one of them makes
alone. The refusal is right; its sentence reads as if each field were over the cap (the [sign-off](../../../../verification/2026-11-09-linux-signoff/results.md), "For
after 0.2.0").

**What:** one sentence for a deferred Update, naming the moves and the count they make together. Its words are
`CommandLabel`'s and a substituted Chrome's (ADR-0203), so a new command ID may be needed; read ADR-0370 section 3
first, and stop for an ADR if the sentence is a decision it does not hold. Layer 2 holds the wording; a Change Note
says it changed.
