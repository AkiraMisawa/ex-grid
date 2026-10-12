# Run 17, area let-names: proposals for the orchestrator

Branch `claude/run17-let-names`, from `claude/run17-corrections`. Cases LET-101, 103, 106, 107, 111 and 112,
all FOLLOW under ADR-0181's own rule ("a name that could mean something else is refused, until a Windows run
shows that Excel accepts it"). Evidence: `results-2026-10-11-run17-update-keys-en-US.json` (typed with real
keys under en-US), and `results-2026-10-11-run17-update-keys-alone-LET-102/104/105.json` for the refusals
that agreed.

What the branch changed, so the texts below can be checked against it:

- `Parser.Let.cs`: the refusal of a function's name and of an R1C1 shape is gone, and so is the refusal of
  a value left empty. A missing calculation, a missing name, an even number of arguments, a name bound
  twice, a 127th pair, `TRUE`/`FALSE` and a name that reads as an A1 Reference (`A1`, `R2`, `C3`, `RC1`,
  `LOG10`) are still refused, each where it stands.
- `ExcelFunctionNames.cs` refused nothing any more and is removed, with `FunctionCatalogueTests`' check
  that it equalled the catalogue's names. `FunctionCatalogueTests.ExcelFunctions()` stays: `LetTests` now
  binds every catalogued name that is a name in shape (no `.`, not `TRUE`/`FALSE`, not an A1 Reference).
- A value left empty binds the name to the empty argument (`MissingNode`, evaluated as
  `Operand.Missing`), so `=LET(x,,x)` shows 0 as `=IF(TRUE,)` does. So that the name reads as that empty
  argument would read in its place, `FunctionCall.Has` now sees through a name bound to a value left
  empty (`MissingNode.IsEmpty`): `=LET(x,,LOG(100,x))` is `=LOG(100,)`, and
  `=LET(x,,IF(FALSE,1,x))&"a"` is `=IF(FALSE,1,)&"a"`. **This touches
  `src/ExSheet.Engine/Formulas/FunctionDefinition.cs`, outside the area's file list**: one line, `Has`.
- The six cases are `observed` with Excel's answer (3, 3, 3, 0, 1, 1), `oracleSkip` "ask by keys" kept.

## 1. ADR-0181: a dated note, and the body brought in line

A note at the end of "What differs from Excel, by decision", after the note of 2026-10-11:

> *(2026-10-11, the seventeenth Windows run, typed with real keys under en-US: Excel's keyboard entered a
> function's name as a `LET` name, whether ExSheet declares it or not — `LET(SUM,2,SUM+1)` is 3,
> `LET(IRR,1,IRR)` 1 and `LET(N,1,N)` 1 (LET-101, 111, 112) — the R1C1 shapes `R1C1` and `RC` (LET-103,
> 106), and a value left empty: `LET(x,,x)` is 0 (LET-107). It refused `TRUE`, `R2` and `C3` (LET-102, 104,
> 105). Under this ADR's own rule, each shape it entered is now a name: the name of any function, and an
> R1C1 shape that does not read as an A1 Reference. A name followed by `(` is still the function's call,
> so `LET(SUM,2,SUM(SUM,1))` is 3. `R2` and `C3` stay refused because they read as A1 References, and
> `TRUE` and `FALSE` because they read as booleans. A value left empty binds the name to that empty
> argument, and where the name stands it reads as the empty argument would read there; only
> `LET(x,,x)` has been observed. **The note above is superseded**: its reason, that a Sheet saved with
> `LET(IRR, …)` would stop opening on the day `IRR` is declared, disappears once no function's name is
> refused.)*

The body, so that it no longer states the refusals as the decision (each change pointing at the note):

- "What Excel was observed to do", **Names**: after "Accepted from the keyboard: `_x`, `c`, `r`, `名前` and
  `é`." add "and, in the seventeenth run, `SUM`, `IRR`, `N`, `R1C1` and `RC`"; after "Refused: `A1`, …"
  add "`TRUE`, `R2` and `C3` (the seventeenth run)". A new bullet under **Values**: "A value may be left
  empty: `LET(x,,x)` is 0 (the seventeenth run)."
- "The decision", the bullet **A name that could mean something else is refused** becomes:
  - **A name that reads as something else is refused**, as Excel's keyboard refuses it: one that reads
    as an A1 Reference (`A1`, `XFD1048576`, `R2`, `C3`), and `TRUE` and `FALSE`.
  - The name of a function, declared or not, is a name, and so is an R1C1 shape that reads as no A1
    Reference (`R1C1`, `RC`); a name followed by `(` is the function's call (the note of 2026-10-11, run
    17). `c` and `r` alone are names, as observed.
- "The decision", a new bullet after **Names**: "**A value left empty is bound.** The name reads as that
  empty argument would read where it stands, so `LET(x,,x)` shows 0, as `IF(TRUE,)` does. A missing
  calculation is still refused."
- "What differs from Excel, by decision", the bullet **Unobserved names are refused** becomes:
  "*(Until 2026-10-11: unobserved names were refused — a function's name, `TRUE`, an R1C1-shaped name.
  The seventeenth run observed each; see the note.)*" No name of letters, digits and `_` is refused for
  being unobserved any more. (If the orchestrator would rather keep the decision's text as it stood and
  carry the change in the note alone, the note says enough on its own.)

## 2. `src/ExSheet.Engine/README.md`, the `LET` paragraph

Replace the bullet that begins "**`LET` takes the names Excel's keyboard accepts**" with:

> - **`LET` takes the names Excel's keyboard accepts** (ADR-0181). A name is bound for the values
>   after it and its own calculation, sequentially and lexically, and compared without regard to
>   case; a name read before its binding, or inside its own value, is `#NAME?`. A name starts with a
>   letter of any script or `_` and goes on with letters, digits and `_`, at most 249 characters, and
>   a `LET` binds at most 126. The name of a function is a name, whether the engine declares it or not
>   (`SUM`, `IRR`, `N`), and so is an R1C1 shape that does not read as an A1 Reference (`R1C1`, `RC`),
>   as Excel's keyboard took them in the seventeenth Windows run; a name followed by `(` is still the
>   function's call, so `LET(SUM,2,SUM(SUM,1))` is 3. A value may be left empty: `LET(x,,x)` shows 0,
>   as `IF(TRUE,)` does, and elsewhere the name reads as that empty argument would read in its place
>   (only `LET(x,,x)` has been observed). Refused on entry, as a Formula that cannot be read is, and as
>   Excel's keyboard refuses each: an even number of arguments, a missing calculation, a name bound
>   twice, a name that reads as an A1 Reference (`A1`, `R2`, `C3`; `c` and `r` alone are names), `TRUE`
>   and `FALSE`. Each binding is computed at most once, when first read, so an unused binding's Error
>   Value never reaches the result; but a `LET` depends on everything its text names, so an unused
>   binding over a cycle is `#CIRC!`, and one over a waiting Linked Table waits. `Table[Column]` always
>   reads the Linked Table, whatever is bound. Two bound names with a space between them, Excel's
>   intersection, are refused by name.

## 3. The Docs Site, `/exsheet/formulas`, "Supported functions"

`samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`: replace the `<li>` that begins
"<b><code>LET</code></b> takes the names Excel's keyboard accepts" with:

```razor
<li>
    <b><code>LET</code></b> takes the names Excel's keyboard accepts: a letter of any script or
    <code>_</code>, then letters, digits and <code>_</code>, at most 249 characters, and at most 126
    of them. The name of a function is a name too, whether this engine has the function or not
    (<code>sum</code>, <code>irr</code>, <code>n</code>), and so are <code>R1C1</code> and <code>RC</code>;
    a name followed by <code>(</code> is still the function's call. A value may be left empty:
    <code>=LET(x,,x)</code> shows 0, as it does in Excel. A name that reads as a cell, such as
    <code>A1</code> or <code>R2</code>, and <code>TRUE</code> are refused as the Formula is typed, as a
    Formula that cannot be read is, because Excel's keyboard refuses them; so are a name bound twice and
    a missing calculation. A binding is computed only when its name is read, but the <code>LET</code>
    still depends on everything its text names, so an unused binding over a cycle shows
    <code>#CIRC!</code> and one over a Linked Table still on its way waits.
</li>
```

The Change Note this branch adds links `exsheet/formulas#supported-functions` (the route the triage
called `/exsheet/functions` is `/exsheet/formulas` on the site).

## 4. `docs/specs/exsheet-functions/spec.md`

The catalogue says in four places that the names it lists are the ones `LET` refuses. None is refused now.

- The introduction (the paragraph on `FunctionCatalogueTests`), replace "Every name the file lists, its
  rows and its list of [Excel's functions not in the queue](#excels-functions-not-in-the-queue), must be
  exactly the names the engine refuses as a `LET` name for being a function's (ADR-0181)." with:
  "It also reads the file's list of [Excel's functions not in the queue](#excels-functions-not-in-the-queue):
  each name is listed once, under its letter, and has no row; and `LetTests` binds every name the rows
  and the list hold that is a name in shape as a `LET` name, as Excel's keyboard took `SUM`, `IRR` and
  `N` (ADR-0181, the seventeenth run)."
- The `LET` row's note becomes: "Names bound inside one Formula, as Excel's keyboard accepts them; a
  function's name, declared or not, and an R1C1 shape that is no A1 Reference are names, a value may be
  left empty, and `TRUE`, `FALSE` and a name that reads as an A1 Reference are refused on entry
  (ADR-0181, the seventeenth run). Ticket 14".
- "Excel's functions not in the queue", the paragraphs under the heading become:

  > The rest of Excel's worksheet functions, as Microsoft documents them. None of them is queued: each
  > is `#NAME?` (ADR-0047), and none has a status or a priority until a row above catalogues it.
  > *(They were listed because a `LET` name was refused if it was the name of any of Excel's functions
  > (ADR-0181's note of 2026-10-11). The seventeenth Windows run saw Excel's keyboard take `SUM`, `IRR`
  > and `N` as `LET` names, so none is refused now. The list stays as the catalogue's account of what
  > Excel has beyond its rows, and `LetTests` binds each name-shaped one as a `LET` name.)*
  >
  > `FunctionCatalogueTests` reads this list with the rows above: each name is listed once, under its own
  > letter, in order, and has no row. Cataloguing a function moves its name from here to a row; a
  > function Excel adds is listed here.

  (Removing the list altogether is the other choice: nothing in the engine reads it now. It is kept
  above because `LetTests`' rule, every function name of a name's shape binds, is the stronger for
  covering all of Excel's.)
- Status item 13 gains: "*(2026-10-11, the seventeenth run: Excel's keyboard took a function's name as a
  `LET` name, so the list is no longer what the engine refuses; see the list's paragraph.)*"

## 5. `docs/definition-of-done.md`, SH-67

Replace "Refused on entry: a name that reads as a Reference in A1 or R1C1 shape, a function's name,
`TRUE`, `FALSE`, a name bound twice, an even number of arguments and a missing calculation." with:
"Refused on entry: a name that reads as an A1 Reference, `TRUE`, `FALSE`, a name bound twice, an even
number of arguments and a missing calculation. A function's name, declared or not, and an R1C1 shape
that reads as no A1 Reference are names, and a value left empty is bound (`LET(x,,x)` is 0), as Excel's
keyboard was observed to take them (the seventeenth run)."

## 6. Further inputs a later Windows run could ask (not added to the corpus)

The general rule goes past the shapes observed: the run typed `R1C1` and `RC` but not `R1C` or `R10C20`;
`SUM`, `IRR` and `N` but not `LET` itself; and of an empty value only `LET(x,,x)`. These are **not** added
to `let.json`, because ADR-0202 meets SH-7 as "every case observed" before the tag, and a new `uncertain`
case would undo that. Written as corpus cases, for whichever run comes after the release candidate, each
with the engine's answer now (every one typed by keys, `oracleSkip` "ask by keys"):

| Proposed id | Formula in A10 | Engine | What it asks |
|---|---|---|---|
| LET-113 | `=LET(R1C,2,R1C+1)` | 3 | an R1C1 shape with no column number |
| LET-114 | `=LET(R10C20,2,R10C20+1)` | 3 | an R1C1 shape of other numbers |
| LET-115 | `=LET(RC1,2,RC1+1)` | refused (an A1 Reference: column RC, row 1) | an R1C1 shape that is also a cell |
| LET-116 | `=LET(LOG10,2,LOG10+1)` | refused (an A1 Reference) | a function's name that is also a cell |
| LET-117 | `=LET(LET,2,LET+1)` | 3 | `LET` itself as a name |
| LET-118 | `=LET(SUM,2,SUM(SUM,1))` | 3 | a bound name followed by `(` is the call |
| LET-119 | `=LET(T,"a",T&T)` | `aa` | a one-letter text function's name |
| LET-120 | `=LET(x,,x+1)` | 1 | an empty value under an operator |
| LET-121 | `=LET(x,,x&"a")` | `a` | an empty value joined to text |
| LET-122 | `=LET(x,,IF(FALSE,1,x)&"a")` | `0a` | an empty value passed on as an argument, as `IF(FALSE,1,)&"a"` |
| LET-123 | `=LET(x,,LOG(100,x))` | 2 | an empty value where an omitted argument has a default (`LOG(100,)` is 2 in the engine) |
| LET-124 | `=LET(x,,ISBLANK(x))` | TRUE | an empty value as a blank |
| LET-125 | `=LET(x,,COUNTA(x,1))` | 2 | an empty value counted, as `COUNTA(,1)` |

Should Excel disagree with any of LET-120 to 125, the narrower rule is to refuse a value left empty
except where the calculation is the name alone, which is the one shape observed.

## 7. The Change Note's kind

The triage asked for `Kind: Changed`; ADR-0190 and the Docs Site generator know Added, Fixed, Behaviour
and Breaking, so the note says `Behaviour`: Formulas refused before are entered now, which a Consumer
will see.
