# 180: A structured reference is written in its table's and column's declared case

Status: ready-for-agent
Roadmap: ExSheet — A Formula that names a Linked Table in another case is written back in the table's own case, as Excel writes it.

**What to build:** A Formula's stored text writes a structured reference in the Linked Table's
declared case, both the table's name and the column's. `=sum(limit[line])`, typed over a table
declared as `LIMIT` with a column `Line`, is stored and shown as `=SUM(LIMIT[Line])`.

**Today:** the table and column are matched without regard to case (ordinally, so the culture plays
no part, and SH-6 holds under tr-TR), but the text keeps the case it was typed in. `=sum(limit[line])`
is stored as `=SUM(limit[line])`, and the function name is the only part written in upper case.
`FormulaText.WriteStructuredReference` writes the token's own text. A Sheet qualifier, by contrast,
is already written in the Sheet's own case: `=risk!i3` is stored as `=RISK!I3`.

**Excel:** writes a structured reference back in the table's and column's own case, as it writes a
function name in upper case. This is how the user described it when deciding, on 2026-10-13. No Windows run has
observed it yet. The run that implements this ticket asks Excel for the exact spelling, including for
a column whose name holds a space or one of `[ ] # '`.

**Decision:** decided with the user on 2026-10-13: align with Excel, but **not for 0.1.0**. ADR-0047
already says each token is written in the engine's invariant spelling. The ticket brings
structured references under that rule.

**Blocked by:** None (can start after 0.1.0)

- [x] **The stored text uses the declared case** for the table's name and the column's, at entry
      and when a Formula is written again (an insertion, a deletion, a copy, a fill).
      Written at entry; a rewrite replaces only References, so it keeps that case. A Formula whose text
      predates the declaration is the next box's case.
- [ ] **A table declared after the Formula was entered.** A reader of an undeclared table is
      `#NAME?` (ADR-0049). Decide, with Excel's answer, whether declaring the table rewrites the
      text or leaves it as typed until it is next written.
- [x] **The SH-6 test changes its expectation.**
      `dotnet/tests/ExSheet.Engine.Tests/ReferenceGrammarCultureTests.cs` pins today's behaviour in the
      form `(Sheet.DefaultName, "=sum(limit[line])", "=SUM(limit[line])", "=SUM(limit[line])", 7)`.
      Its written and after-insert texts become the declared case. Declare the table in that test
      with a column whose case differs from its name's (for example `LIMIT` with `Line`), so that
      the table's name and the column's are both checked.
- [x] **Layer 1** for the Sheet Document round trip: a reopened Sheet's text is the declared case.
- [ ] **Excel's exact spelling**, asked in a Windows run, including for a column whose name holds a space
      or one of `[ ] # '`.

## Comments

2026-10-13, claude/dod-gaps-sheet-pivot-tests. Found while closing SH-6: the culture round-trip
test showed that the typed case is kept.

2026-10-22, claude/project-thread-3klogz. The case is written when a Formula is typed or pasted as text
(`Sheet.InDeclaredCase`, beside `InOwnName`): a table and a column the Workbook declares are written as
declared, and one it does not is left as typed. A rewrite on an insertion, a deletion, a copy or a fill
replaces only References, so it keeps the case written at entry; a Sheet Document is opened as it was saved.
The second box waits on Excel's answer: until then a Formula typed before its table is declared keeps the case
it was typed in. `LinkedTableWriterTests` now expects `Positions[Desk]` for a Formula typed `positions[desk]`.
