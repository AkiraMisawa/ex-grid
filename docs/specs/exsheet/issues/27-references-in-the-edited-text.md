# 27: The References in the text being edited

Status: ready-for-agent

**What to build:** ExSheet's half of ADR-0057, "Who decides what". A pure function over the editor's
text that answers every Reference in it, with its span, and either the cells it names or, for a
structured reference, a key naming the table and the column. It is what ExSheet will hand the core
as the References function (ticket 28), and what the Linked Table notification reads (ticket 30).

**Blocked by:** None (can start immediately). Build on F4's `Lexer.MatchReference` and the tolerant
scan in `FormulaEntry`. The strict `Lexer.Tokenize` throws on an unfinished Formula, and a Formula
is unfinished for as long as it is typed.

- [ ] A Reference's span and cells: `A1`, `$A$1`, `A1:B2`, `B2:A1` (as A1:B2), `A:A`, `1:1`, and
      each with this Sheet's own qualifier (`Sheet1!A1`, `'Sheet 1'!A1`) (SH-30)
- [ ] A Reference qualified with another Sheet's name is not answered: it names no cells (SH-30)
- [ ] A structured reference (`Positions[PV]`, `Positions[[PV]]`) is answered with its span and a
      key naming the table and the column, in a form that compares equal however the name was cased
      (SH-30)
- [ ] An unfinished Formula is answered as far as it goes: `=SUM(A1,`, `=A1+`, `=SUM(A1:B2`, and one
      with an unclosed string, whose References before the string are answered (SH-30)
- [ ] Nothing inside a string, and no function name, even where the name reads as a cell
      (`LOG10(`) (SH-30)
- [ ] Text that is not a Formula is answered with nothing (SH-30)
- [ ] Layer 1 covers each reading of ADR-0057 by name, so a reading the eighth Windows run
      contradicts fails a test that says which (SH-30)

## Comments

The readings are asked of Excel in [verify-on-windows-8.md](../verify-on-windows-8.md), Part A. A
reading that Excel contradicts is fixed after the run, with the ADR paragraph.
