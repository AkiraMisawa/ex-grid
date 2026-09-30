# 40: Tests that watch for array operations and column ranges

Status: done

**What to build:** ADR-0058, "Not in the first version, and what watches for it". Two layer 1 tests
that pass today because the engine refuses two things, and fail the day it stops refusing either.

**Blocked by:** None (can start immediately)

- [x] A table column compared with a value, over two rows, is `#VALUE!`: `(Cds[Entity]="ACME")` with
      two rows (`Evaluator.cs`, `OperandKind.Column` in `ScalarOf`) (SH-37)
- [x] A range of columns is refused by the grammar: `Xva[[A]:[B]]` (`Lexer.cs`,
      `ReadStructuredColumn`) (SH-37)
- [x] Each test's name cites ADR-0058. Its failure message says: this refusal has gone; open ADR-0058,
      "Not in the first version"; decide with the user how a key of several columns (or a range of
      columns) is pointed at; do not make this test pass by changing its expectation (SH-37)

## Comments

2026-09-30, done (branch `agent/pointing-scope-36`), in `tests/ExSheet.Engine.Tests/PointingScopeWatchTests.cs`.

- `ADR0058_a_table_column_compared_with_a_value_over_two_rows_is_VALUE` pushes two rows of `Cds` and
  expects `#VALUE!` from `=(Cds[Entity]="ACME")`, and also from the pair a Scope would write,
  `=XLOOKUP(1, (Cds[Entity]="ACME")*(Cds[Tenor]="5Y"), Cds[Spread])`. The second is there because
  array operations could arrive while a cell still cannot hold an array: the comparison alone would
  then stay `#VALUE!` and the pair would start to answer, unnoticed.
- `ADR0058_a_range_of_columns_is_refused_by_the_grammar` expects `FormulaSyntaxException` from both
  `Entry.FromFormula` and `Sheet.Enter`, for `=Xva[[A]:[B]]` and for
  `=SUM(Xva[[CVA Before]:[CVA Diff]])`, the Header Group case the ADR names.
- The ADR number is in each method's name, since the ticket asks for it there, besides the comment
  the repository puts beside `[Fact]`. The assertions are `Assert.True(…, message)`, so a failure
  prints: "This refusal has gone: … Open ADR-0058, "Not in the first version, and what watches for
  it", and decide with the user how a key of several columns (a range of columns) is pointed at
  through a Pointing Scope. Do not make this test pass by changing its expectation."
- Checked by breaking each refusal on purpose (`ScalarOf` returning a column's first Value, and the
  lexer reading `[[A]:[B]]`): all three cases failed with that message, and the change was reverted.
