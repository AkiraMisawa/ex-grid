# The area pass, sort and unique: what the orchestrator writes

The area pass of 2026-10-12 (`verification/2026-10-12-windows-area/report.md`, "Recorded (9)";
`tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-12-area-com-en-US.json`) read Excel's answer for the five cases
ADR-0125's note of 2026-10-12 named. This file holds the text this area would put into the ADRs, the specification, the
engine's README and the Docs Site, which a background agent does not change. Each block says where it goes, as an exact
old -> new edit; the text is in the repository's voice, ready to paste.

## What was done, per case

The `lookupData` fixture has Q1 = 7 and Q2 blank.

| Case | Formula | Excel | Before | Now | Why |
|---|---|---|---|---|---|
| SORT-011 | `=SORT(Q1:Q2)`, at ZZ1000 | 7 | `#VALUE!` by decision, `excelExpect` 7 | 7, `observed` | follows the rule the pass settled (below) |
| SORT-012 | the same, at ZZ1001 | 0 | an empty cell (the Anchor was `#VALUE!`), `uncertain`, recorded | 0, `observed` | the blank sorts after the 7, and its spilled cell shows 0 |
| SORT-013 | `=SORT(Q1:Q2,1,-1)` | 7 | `#VALUE!`, `uncertain`, recorded | 7, `observed` | in a descending sort the blank is still last |
| SORT-014 | `=SORT(A1:A3)` over b, blank, a | `a` | `#VALUE!`, `uncertain`, recorded | `a`, `observed` | the blank sorts neither as empty text nor as 0, either of which would come first |
| UNIQUE-009 | `=UNIQUE(Q1:Q2)`, at ZZ1000 | 7 | `#VALUE!` by decision, `excelExpect` 7 | 7, `observed` | follows the rule |
| UNIQUE-010 | the same, at ZZ1001 | 0 | an empty cell, `uncertain`, recorded | 0, `observed` | a blank spills as 0 |
| UNIQUE-011 | `=ROWS(UNIQUE(A1:A2))` over 0 and a blank | 2 | `#VALUE!`, `uncertain`, recorded | 2, `observed` | a blank is not one of a kind with 0 |

**The rule, and why the answers settle it rather than one point of it.** ADR-0125's note kept the refusal for four
unknowns: where the blank goes, what its spilled cell shows, how a descending sort places it, and whether `UNIQUE`
counts it one of a kind with 0. The pass answered each. A blank key sorts after every other key, in either order
(SORT-011, 013, 014: after a number ascending and descending, and after text, where 0 and empty text would have come
first); a blank spills as 0 (SORT-012, UNIQUE-010, as TRANSPOSE-004 already observed for any spilled array); in `UNIQUE`
a blank is not one of a kind with 0 (UNIQUE-011). Blank keys keep their order, as equal keys do (SORT-004). Nothing the
note waited for is left open, so the engine follows; the forms the pass did not ask are the rule's consequences, written
as `uncertain` cases with the engine's new answer for the next pass (below).

**The code** (`src/ExSheet.Engine/Formulas/Functions.Arrays.cs`):

- `TryOrderLines` refuses only an Error Value among the keys. A blank key compares after any Value, whatever the
  direction, and two blanks compare equal, so the stable order and the next key decide between them. `SORTBY` shares
  `TryOrderLines`, so its blank keys follow the same rule, key by key.
- `UNIQUE` no longer refuses a blank. `Same` compares item by item, and a blank is the same only as a blank; it no longer
  dereferences a null (`Item(...)!.Value`). The blank is kept in the result as a blank, and `Sheet.Spill.cs` (shared,
  unchanged) spills it as 0.
- The Error Value refusal stands under ADR-0047's admitting paragraph. No corpus case asked it; SORT-015 does now.

**Tests:** `tests/ExSheet.Engine.Tests/SortAndUniqueBlankTests.cs` (new, 30 tests, each named with ADR-0125, or ADR-0047
for the Error Value refusal). `ExSheet.Engine.Tests`, the whole project: 4806 passed, 0 failed. `ExSheet.Components`:
743 passed, 0 failed.

## Questions for the next pass (new `uncertain` cases, the engine's answer as `expect`)

| Case | Formula | The engine | What it asks |
|---|---|---|---|
| SORT-015 | `=SORT(A1:A2)` over 7 and `=NA()` | `#VALUE!` (refused) | where Excel sorts an Error Value key, or whether it is the result |
| SORT-016 | `=SORT(A1:A3,1,-1)` over b, blank, a, at ZZ1000 | `b` | a blank is last in a descending sort of text too |
| SORT-017 | the same, at ZZ1002 | 0 | and its cell shows 0 |
| SORT-018 | `=SORT(A1:A3)` over 1, blank, TRUE, at ZZ1001 | TRUE | a blank sorts after a boolean |
| SORT-019 | the same, at ZZ1002 | 0 | and is last |
| SORT-020 | `=SORT(A1:C1,1,1,TRUE)` over 2, blank, 1, at AAB1000 | 0 | by_col, a blank key sorts its column last |
| SORTBY-007 | `=SORTBY(H1:H2,Z1:Z2)`, Z1 blank, Z2 7 | 20 first | `SORTBY`'s blank key sorts last as `SORT`'s |
| SORTBY-008 | `=SORTBY(H1:H2,Z1:Z2,1,AA1:AA2,1)`, Z x, x; AA blank, 5 | 20 first | among equal first keys, a blank second key is last |
| SORTBY-009 | `=SORTBY(H1:H3,Z1:Z3,1,AA1:AA3,-1)`, Z blank, blank, 1; AA 1, 2, blank; at ZZ1001 | 20 | two blank first keys are equal, so the second key orders them |
| UNIQUE-012 | `=ROWS(UNIQUE(A1:A3))` over blank, blank, 1 | 2 | two blanks are one of a kind |
| UNIQUE-013 | `=ROWS(UNIQUE(A1:A2))` over a blank and `=""` | 2 | whether a blank is one of a kind with empty text |
| UNIQUE-014 | `=UNIQUE(A1:A3,,TRUE)` over blank, blank, 1 | 1 | `exactly_once` leaves out repeated blanks |
| UNIQUE-015 | `=ROWS(UNIQUE(A1:B2))` over (a, blank) and (a, 0) | 2 | a row with a blank differs from the same row with 0 |
| UNIQUE-016 | `=UNIQUE(Q2)`, one blank cell | 0 | one blank cell spills as 0 |

`sortby.json` was not among the pass's 30 files; the next `-Area` pass names `sort`, `sortby` and `unique`.

## ADR-0125: a dated note after the note of 2026-10-12

Old (the end of the note of 2026-10-12, "What becomes Excel's answer"):

```
guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*
```

New:

```
guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*

*(2026-10-12, after the area pass, `verification/2026-10-12-windows-area/`: **a blank among `SORT`'s and `SORTBY`'s
keys, and in `UNIQUE`'s array, is answered as Excel answers it**, and the refusal of the note above is withdrawn. The
pass answered all four things it waited for. Over 7 and a blank, `=SORT(Q1:Q2)` spills 7 and then 0 (SORT-011, 012):
the blank sorts after the 7, and its cell shows 0, as a blank in any spilled array does (TRANSPOSE-004).
`=SORT(Q1:Q2,1,-1)` is 7 too (SORT-013): a descending sort leaves the blank last. Over b, a blank and a, `=SORT(A1:A3)`
is a first (SORT-014): the blank sorts neither as empty text nor as 0, either of which would have come first.
`=UNIQUE(Q1:Q2)` spills 7 and 0 (UNIQUE-009, 010), and over 0 and a blank `=ROWS(UNIQUE(A1:A2))` is 2 (UNIQUE-011): a
blank is not one of a kind with 0. So a blank key sorts after every other key, whichever the order, blanks keep their
order as equal keys do, and a blank spills as 0; `SORTBY` sorts its blank keys by the same rule, key by key; in `UNIQUE`
a blank is one of a kind only with another blank. The answers settle the rule, not one point of it, so the engine
follows; the forms the pass did not ask are the rule's answer, `uncertain` for the next pass: a blank after a boolean
(SORT-018, 019), in a descending sort of text (SORT-016, 017), by `by_col` (SORT-020), in `SORTBY` (SORTBY-007 to 009),
and two blanks, a blank beside empty text, `exactly_once`, a whole row and one blank cell in `UNIQUE` (UNIQUE-012 to
016). An Error Value among `SORT`'s and `SORTBY`'s keys stays refused with `#VALUE!`; SORT-015 asks it.)*
```

## ADR-0047: a dated note on the paragraph admitting the array functions

Old (under "The list grows", the paragraph that admits `FILTER` to `SUMPRODUCT` under ADR-0125):

```
answer, and their corpus cases changed from a difference by decision to it. `UNIQUE` refuses a blank
in its array, and `SORT` and `SORTBY` a blank or an Error Value among their keys, with `#VALUE!`
until Excel is asked how it orders them.
```

New:

```
answer, and their corpus cases changed from a difference by decision to it. `UNIQUE` refuses a blank
in its array, and `SORT` and `SORTBY` a blank or an Error Value among their keys, with `#VALUE!`
until Excel is asked how it orders them. *(2026-10-12: the area pass asked Excel about the blank, and
the blank is answered as Excel answers it: among `SORT`'s and `SORTBY`'s keys it sorts after every
other key, in either order, in `UNIQUE` it is one of a kind only with another blank, and it spills
as 0 (ADR-0125's note of that day; SORT-011 to 014, UNIQUE-009 to 011). An Error Value among
`SORT`'s and `SORTBY`'s keys is still refused with `#VALUE!` until a run asks it (SORT-015).)*
```

## `docs/specs/exsheet-functions/spec.md`: the catalogue rows

Old:

```
| `SORT` | Supported | — | Spills (ADR-0125); a blank or Error Value among the keys is refused until Excel is asked; Excel's Anchor recorded (SORT-011), the refusal kept until a run reads the rest |
| `SORTBY` | Supported | — | Spills (ADR-0125); keys refused as `SORT`'s; Excel's Anchor recorded (SORT-011), the refusal kept until a run reads the rest |
| `UNIQUE` | Supported | — | Spills (ADR-0125); a blank in the array is refused until Excel is asked; Excel's Anchor recorded (UNIQUE-009), the refusal kept until a run reads the rest |
```

New:

```
| `SORT` | Supported | — | Spills (ADR-0125); a blank key sorts after every other, in either order, and spills as 0, as Excel's does (the area pass, SORT-011 to 014); an Error Value among the keys is refused until Excel is asked (SORT-015) |
| `SORTBY` | Supported | — | Spills (ADR-0125); keys sort and are refused as `SORT`'s, a blank last key by key (SORTBY-007 to 009 ask it) |
| `UNIQUE` | Supported | — | Spills (ADR-0125); a blank is one of a kind only with another blank, never with 0, and spills as 0, as Excel's is (the area pass, UNIQUE-009 to 011) |
```

## `src/ExSheet.Engine/README.md`

1. "Asked of Excel before they are answered": the fragment on `UNIQUE`, `SORT` and `SORTBY` (other areas edit the
   same bullet; this edit touches only this fragment).

   Old:

   ```
     2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
     Error Value among their keys (`#VALUE!`; the seventeenth run read only the Anchor of a blank's
     sort).
   ```

   New:

   ```
     2^53 (`#VALUE!`); `SORT` and `SORTBY` with an Error Value among their keys (`#VALUE!`).
   ```

2. The paragraph on spilling, after "takes an array whole.".

   Old:

   ```
   spill. Operators work element by element over ranges, and a function that reduces (`SUM`,
   `SUMPRODUCT`) takes an array whole.
   ```

   New:

   ```
   spill. Operators work element by element over ranges, and a function that reduces (`SUM`,
   `SUMPRODUCT`) takes an array whole. A blank in a spilled array shows 0. Among `SORT`'s and
   `SORTBY`'s keys a blank sorts after every other key, in either order, blanks keeping their order,
   and in `UNIQUE` it is one of a kind only with another blank, never with 0, as Excel's are.
   ```

## Docs Site: `samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`, "Supported functions"

The bullet under "Where the engine cannot give Excel's answer, it gives an Error Value instead of a different answer".
The blank is Excel's answer now; the Error Value refusal stays, and was not on the page.

Old:

```
            <li>
                <b>A blank among <code>SORT</code>'s or <code>SORTBY</code>'s keys, or in <code>UNIQUE</code>'s
                array,</b> is <code>#VALUE!</code>. Excel sorts it, but where it goes and what its cell shows have not
                been read yet.
            </li>
```

New:

```
            <li>
                <b>An Error Value among <code>SORT</code>'s or <code>SORTBY</code>'s keys</b> is <code>#VALUE!</code>
                until Excel has been asked where it sorts one. A blank among them sorts last, in either order, and
                shows 0, as in Excel; in <code>UNIQUE</code> a blank is one of a kind only with another blank.
            </li>
```

No Docs Site Example uses `SORT`, `SORTBY` or `UNIQUE` (`samples/` searched), so no Example is now false.
`/exsheet/functions` needs nothing: it is generated from the engine's function list, whose entries did not change.

## Change Note

`docs/releases/unreleased/area-sort-and-unique-blanks.md` (ExSheet, Behaviour, `exsheet/formulas#supported-functions`,
ADR-0125).

## Nothing else

- `docs/definition-of-done.md`: SH-64 says "each to its corpus cases" and names no blank, so no edit.
- `CONTEXT.md`: no new term.
- No ticket under `docs/specs/` names these cases; `docs/specs/exsheet/` has no text on them.
