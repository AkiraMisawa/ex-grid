# 40: Tests that watch for array operations and column ranges

Status: ready-for-agent

**What to build:** ADR-0058, "Not in the first version, and what watches for it". Two layer 1 tests
that pass today because the engine refuses two things, and fail the day it stops refusing either.

**Blocked by:** None (can start immediately)

- [ ] A table column compared with a value, over two rows, is `#VALUE!`: `(Cds[Entity]="ACME")` with
      two rows (`Evaluator.cs`, `OperandKind.Column` in `ScalarOf`) (SH-37)
- [ ] A range of columns is refused by the grammar: `Xva[[A]:[B]]` (`Lexer.cs`,
      `ReadStructuredColumn`) (SH-37)
- [ ] Each test's name cites ADR-0058. Its failure message says: this refusal has gone; open ADR-0058,
      "Not in the first version"; decide with the user how a key of several columns (or a range of
      columns) is pointed at; do not make this test pass by changing its expectation (SH-37)
