# 16: Structured references under fill and copy

Status: needs-triage

**What to find out:** what Excel does to a structured reference such as `Cds[Spread]` when the
Formula is filled right and when it is copied and pasted. It is reported that a fill shifts the
column and a paste does not; that has not been observed. ExSheet shifts neither. Code reading says
`ReferenceShift.Shift` rewrites only Reference tokens.

Found in the grilling for the sample, which avoids the question by reading tenors through the
heading (`"…|"&C$1`). It stands on its own as an ADR-0047 matter: if Excel shifts on a fill,
ExSheet disagrees with it today.

This is an ExSheet ticket. When it is picked up, it moves to `docs/specs/exsheet/issues/` under a
number from a block reserved in `docs/agents/numbering.md`.

**Blocked by:** None. A Windows run is needed.

- [ ] The cases in the corpus: fill right, fill down, copy and paste, `Table[[Col]]`
- [ ] ExSheet changed to Excel's answer, or the difference recorded in ADR-0047
