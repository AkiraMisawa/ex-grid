# The area pass's corrections: LARGE, SMALL, AGGREGATE's array form, MOD — proposals for the orchestrator

Branch `claude/area-kth-and-mod`. The engine (`Functions.Math.cs`, `Functions.Subtotal.cs`), the corpus
(`small.json`, `large.json`, `aggregate.json`, `mod.json`), the tests (`KthAndModTests`, `SubtotalTests`),
ticket 16, the MOD search (`spikes/exsheet-financial-evaluation/mod-search.cs`) and the Change Notes are on
the branch. What follows is the text this area would put into files a background agent does not edit, or
that another area owns: ADR-0047, the engine's README, the function catalogue (`spec.md`), tickets 04 and
01, and the Docs Site. Each edit quotes the text it replaces.

Evidence: `verification/2026-10-12-windows-area/report.md` ("Every disagreement (29)") and
`tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-12-area-com-en-US.json` (E1), with the seventeenth
run's `results-2026-10-11-run17*.json`.

| Case | Formula | Excel (E1) | Before | Now |
|---|---|---|---|---|
| SMALL-004 | `=SMALL(A1:A9,1.5)` | 2 | `#VALUE!` | 2, observed |
| SMALL-005 | `=SMALL(A1:A9,1.2)` | 2 | `#VALUE!` | 2, observed |
| AGGREGATE-060 | `=AGGREGATE(15,6,A1:A11,1.2)` | 30 | `#VALUE!` | 30, observed |
| LARGE-011 | `=LARGE(A1:B5,0.5)` | `#NUM!` | `#VALUE!` | `#NUM!`, observed |
| AGGREGATE-061 | `=AGGREGATE(14,6,"5",1)` | `#NUM!` | `#VALUE!` | `#NUM!`, observed |
| MOD-017 | `=MOD(123456789012,0.7)` | 0.5000078322611261 | 0.5000152587890625 | 0.5000078322611261, observed |
| MOD-020 | `=MOD(1,0.1)` | not asked | 0 | `#VALUE!`, where the evaluations that fit part (section 0) |
| MOD-030 | `=MOD(1.1,0.1)` | not asked | 0 | `#VALUE!`, where the evaluations that fit part (section 0) |

---

## 0. MOD: seven evaluations fit Excel's answers, and the engine refuses where they part

*(Rewritten 2026-10-12, in the second round. The first version of this section counted three classes and
asked the user whether `MOD(1,0.1)` should move from 0 to 0.09999999999999995 or be refused. A verifier
found a class it had missed, a fused multiply-add with a wrong sign taken as 0, which gives 0 there. So the
observations do not choose, ADR-0047 and ADR-0202 leave no question, and the engine refuses there.)*

`mod-search.cs` tries 33 evaluations, each written once, against all 17 MOD cases Excel answered (35 reads,
12 numbers), and 15 give every one. Every evaluation of the identity in doubles misses MOD-017: the one the
engine computed before, `n - d*FLOOR(n/d)`, and its variants with a wrong sign taken as 0, with Excel's
cancellation of a final addition, or written as a Formula whose last subtraction cancels. So do x87
registers (from MOD-017's 9th digit), decimal, and a multiply-add over a quotient read at 15 digits
(MOD-009). The 15 fall into seven classes, within which none of the 3,116,066 inputs searched parts them:

1. **the exact remainder, rounded once** (C# `%` with the divisor added, IEEE remainder, a fused
   multiply-add with the sign corrected);
2. **the same, rounded first to an x87 register's 64 bits**;
3. **a fused multiply-add over the double quotient**, its sign not corrected;
4. **the same, a wrong sign taken as 0**;
5. **that, and Excel's cancellation**: a remainder within 2^-49 of the number is 0, the tolerance the
   engine observed of a final addition (`Arithmetic.FinalAdd`);
6. **the exact remainder, and that cancellation**;
7. **the exact remainder, 0 where it rounds to the divisor itself**.

They part in four places. The counts are of 1,915,648 random inputs of up to five digits:

- **The double quotient rounds up onto a whole number the exact one is below.** `MOD(1,0.1)` is
  0.09999999999999995 by classes 1, 2, 6 and 7, -5.551115123125783E-17 by 3, and 0 by 4 and 5. 8,857
  inputs. MOD-020 to 028.
- **The remainder is not 0 but within 2^-49 of the number.** `MOD(1.1,0.1)` is 2.7755575615628914E-17 by
  classes 1 to 4 and 7, and 0 by 5 and 6. 10,768 inputs. MOD-030 and 031.
- **The remainder plus the divisor rounds to the divisor.** `MOD(-1E-20,1)` is 1 by classes 1 to 6, and 0 by
  7. No random input; a number far smaller than the divisor, of the other sign. MOD-032.
- **Adding the divisor in 64 bits first rounds twice.** `MOD(-0.087626,6630)` is 6629.912374 by every class
  but 2, which gives 6629.9123739999995. 8 inputs. MOD-029.

**The engine answers where all seven give one double, and is `#VALUE!` where they part**, until the next
`oracle.ps1 -Area mod` pass asks MOD-020 to MOD-032 (ADR-0047, "a function answers as Excel, or not at
all"; ADR-0202, "the arguments where it disagrees are refused"). Its answer is class 1's. It tests the four
places directly: the multiply-add against the exact remainder, the remainder against 2^-49 of the number,
the remainder against the divisor, and the double rounding by the exact error of the sum.
`mod-search.cs --discriminate` checks, over every input searched, that it refuses exactly where the classes
part. It refuses 19,633 of the random inputs, about one in a hundred, everyday ones among them:
`MOD(1,0.1)`, `MOD(0.5,0.1)`, `MOD(1.1,0.1)`.

Before, on `main` (the identity in doubles): `MOD(1,0.1)` and `MOD(5,0.1)` were 0, `MOD(3.3,0.01)`
-4.440892098500626E-16, `MOD(0.7,0.01)` -1.1102230246251565E-16, and `MOD(1.1,0.1)` 0. Each is `#VALUE!`
now. Where the classes agree, the answer can move from the old one, as `MOD(10,0.3)` moves from
0.09999999999999964 to 0.10000000000000037: that follows from MOD-017 by a rule every fitting evaluation
shares, and MOD-018 and 019 ask two such answers. The Change Note `area-kth-and-mod-mod-remainder.md` says
both. Each pair of classes parts at one new case at least, which the spike prints. When the pass answers,
the cases leave one class standing or none: the engine then answers as that class, or the search starts
again.

Found while searching (not a corpus case): Microsoft's Q&A thread "Problem with the MOD function in
excel?" (2011) reads `MOD(15*(1.4-1),6)`, where `15*(1.4-1)` is 6−2^-49, as −1.776E-15 in Excel 2003 and
as 6−2^-49 in Excel 2010. The 2003 answer is the identity with the quotient read at 15 digits (candidate
A4, which misses MOD-009 today); the 2010 one is what every candidate that fits gives, and the engine
answers it. So `MOD` was rewritten in Excel 2010, as the change of limit already said.

## 1. ADR-0047, the note of 2026-10-12 under "The catalogue's last Decide rows"

Dated additions, the sentences they answer kept. Each edit is whole lines, with the ADR's own indentation.

### 1a. The array-form bullet

Old:

```markdown
  - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
    (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
    and stay `#VALUE!` (AGGREGATE-061).
```

New:

```markdown
  - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
    (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
    and stay `#VALUE!` (AGGREGATE-061). *(2026-10-12, the area pass: text given directly is such an array
    too, and holds no number, since it is not coerced: `AGGREGATE(14,6,"5",1)` is `#NUM!`, neither 5 nor
    `#VALUE!` (AGGREGATE-061). The engine takes text and a boolean given directly so, and `LARGE` and
    `SMALL` find no number in them. AGGREGATE-064 and 065 ask a boolean and text that spells no number,
    and AGGREGATE-067 an Error Value given directly, which the engine gives back as an Error Value
    argument's result, where option 6 might pass over it.)*
```

### 1b. The `LARGE` bullet

Old:

```markdown
  - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
    are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
    (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
    (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
    down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
    asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
    implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006).
```

New:

```markdown
  - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
    are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
    (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
    (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
    down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
    asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
    implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006). *(2026-10-12, the area pass: Excel
    truncates `SMALL`'s k, as LibreOffice and ODF do: `SMALL(A1:A9,1.5)` and `SMALL(A1:A9,1.2)` are each
    the smallest (SMALL-004, 005), and `AGGREGATE(15,6,A1:A11,1.2)` is 30 (AGGREGATE-060). `LARGE`'s 1.2
    is the 2nd largest (LARGE-008, AGGREGATE-059), so `LARGE` rounds up, not to nearest. A k below 1 is
    `#NUM!`, whole or not: `LARGE(A1:B5,0.5)` is `#NUM!` (LARGE-011), not rounded up to 1. The two are one
    rule — the k-th largest with k rounded up is the (n+1-k)-th smallest with that index truncated — and
    past the count once so rounded, k is `#NUM!` (LARGE-010). The engine follows it, and no k is
    `#VALUE!` any more. SMALL-007 to 009, LARGE-013 and AGGREGATE-062, 063 and 066 ask where the rule
    reaches next: a k below 1 for `SMALL`, and a k near the count.)*
```

### 1c. The `MOD` paragraph

Old:

```markdown
  The `MOD` refusal of "admitted with some argument values refused" above is replaced: Excel 365 answers
  `MOD(2^27,1)` with 0 (MOD-008). Microsoft documents no limit, and names
  `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
  case read (MOD-009's 0.09999999999999998 among them). A Microsoft 365 user reported on Microsoft's Q&A
  (2021) that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, a limit Excel 2010 brought
  in; the engine answers a quotient below 1,125,900,000,000 and is `#NUM!` from it, and MOD-010 to MOD-017
  ask Excel on both sides of it.
```

New:

```markdown
  The `MOD` refusal of "admitted with some argument values refused" above is replaced: Excel 365 answers
  `MOD(2^27,1)` with 0 (MOD-008). Microsoft documents no limit, and names
  `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
  case read (MOD-009's 0.09999999999999998 among them). A Microsoft 365 user reported on Microsoft's Q&A
  (2021) that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, a limit Excel 2010 brought
  in; the engine answers a quotient below 1,125,900,000,000 and is `#NUM!` from it, and MOD-010 to MOD-017
  ask Excel on both sides of it. *(2026-10-12, the area pass: the limit holds on both sides (MOD-010 to
  016), but the doubles are not Excel's on every case. Excel's `MOD(123456789012,0.7)` is
  0.5000078322611261 (MOD-017): the identity computed exactly and rounded once, where in doubles it is
  0.5000152587890625. Of 33 evaluations tried against the 17 cases Excel answered
  (`spikes/exsheet-financial-evaluation/mod-search.cs`), 15 give every one, and none of them evaluates the
  identity in doubles. They fall into seven classes: the exact remainder rounded once; the same rounded
  first to an x87 register; a fused multiply-add over the double quotient, with a wrong sign kept, taken as
  0, or taken as 0 with Excel's cancellation of a final addition; the exact remainder with that
  cancellation; and the exact remainder taken as 0 where it rounds to the divisor. They part where the
  double quotient rounds up onto a whole number the exact one is below (`MOD(1,0.1)` is
  0.09999999999999995, -5.551115123125783E-17 or 0), where the remainder is not 0 but within 2^-49 of the
  number (`MOD(1.1,0.1)` is 2.7755575615628914E-17 or 0), where the remainder plus the divisor rounds to
  the divisor (`MOD(-1E-20,1)` is 1 or 0), and at the last bit where adding the divisor in 64 bits first
  rounds twice (`MOD(-0.087626,6630)`). So, as "admitted with some argument values refused" and ADR-0202
  require, the engine answers the exact remainder rounded once where all seven agree, and is `#VALUE!`
  where they part, about one random input of up to five digits in a hundred, until a Windows run asks
  MOD-020 to MOD-032.)*
```

## 2. The engine's README (`src/ExSheet.Engine/README.md`)

- In "**Asked of Excel before they are answered**", old: "`NETWORKDAYS` and `WORKDAY` with a boolean typed as
  a date (`#VALUE!`); `SMALL` with a `k` that is not a whole number, and `LARGE` with one between 0 and 1;
  `PMT`, `PV` and `FV` with a" → new: "`NETWORKDAYS` and `WORKDAY` with a boolean typed as a date
  (`#VALUE!`); `MOD` where the evaluations that give every answer Excel gave part, as at `MOD(1,0.1)` and
  `MOD(1.1,0.1)` (`#VALUE!`; MOD-020 to 032); `PMT`, `PV` and `FV` with a". (The financial and dates areas
  edit the same bullet, so the edit is given as a phrase, not as whole lines.)
- In "**Answered as the seventeenth Windows run read Excel.**", old: "`MOD` answers a quotient of 2^27 or
  more, and is `#NUM!` from 1,125,900,000,000, the limit a Microsoft 365 user reported (MOD-010 to 017 ask
  Excel)." → new: "`MOD` answers a quotient of 2^27 or more, and is `#NUM!` from 1,125,900,000,000, the limit
  a Microsoft 365 user reported, as the area pass of 2026-10-12 read Excel on both sides of it (MOD-010 to
  016)."
- A bullet to add after it:

  > - **Answered as the area pass of 2026-10-12 read Excel.** `SMALL`, and `AGGREGATE`'s 15, truncate a `k`
  >   that is not whole: `SMALL(A1:A9,1.5)` is the smallest. A `k` below 1 is `#NUM!` for `LARGE` and
  >   `SMALL` alike, whole or not. `AGGREGATE`'s array form takes text or a boolean given directly as an
  >   array holding no number: `AGGREGATE(14,6,"5",1)` is `#NUM!`. `MOD` is `number - divisor*INT(number/divisor)`
  >   computed exactly and rounded once, where every evaluation that gives Excel's answers agrees:
  >   `MOD(123456789012,0.7)` is 0.5000078322611261, as Excel's. Where they part it is `#VALUE!` until
  >   Excel is asked (above).

- In "**`SUBTOTAL` and `AGGREGATE` while no row can be hidden**", old: "and `AGGREGATE`'s
  array form takes a number given directly." → new: "and `AGGREGATE`'s array form takes a Value given
  directly as an array of that one Value: a number is that number, and text or a boolean holds none
  (`#NUM!`)."

## 3. The function catalogue (`docs/specs/exsheet-functions/spec.md`)

- `LARGE`, old: "A `k` that is not whole is rounded up, as Excel's (ADR-0047, 2026-10-12)" → new: "A `k`
  that is not whole is rounded up, and one below 1 is `#NUM!`, as Excel's (ADR-0047, 2026-10-12)"
- `SMALL`, old: "A `k` that is not whole is `#VALUE!` until observed" → new: "A `k` that is not whole is
  truncated, and one below 1 is `#NUM!`, as Excel's (ADR-0047, 2026-10-12)"
- `MOD`, the whole row. Old:

  ```markdown
  | `MOD` | Supported | — | Excel's sign follows the divisor; `#NUM!` from a quotient of 1,125,900,000,000 |
  ```

  New:

  ```markdown
  | `MOD` | Supported | — | The exact remainder, rounded once, with the divisor's sign, as Excel's (MOD-017), where every evaluation that gives Excel's answers agrees; `#VALUE!` where they part, as at `MOD(1,0.1)` and `MOD(1.1,0.1)`, until Excel is asked (MOD-020 to 032, ADR-0047, 2026-10-12); `#NUM!` from a quotient of 1,125,900,000,000 |
  ```
- `AGGREGATE`, old: "As `SUBTOTAL`; options 0 to 3 pass over a nested cell and 4 to 7 count it, and a
  function number for a function the engine does not support (13, 16 to 19) is `#VALUE!`. Ticket 16" → new:
  "As `SUBTOTAL`; options 0 to 3 pass over a nested cell and 4 to 7 count it, and a function number for a
  function the engine does not support (13, 16 to 19) is `#VALUE!`; text or a boolean given as the array
  holds no number. Ticket 16"

## 4. Ticket 04 (`04-p2-ready-functions.md`, the financial area's)

A comment to append:

> 2026-10-12, with the area pass (`verification/2026-10-12-windows-area/report.md`): Excel truncates
> `SMALL`'s k that is not whole (SMALL-004, 005: 1.5 and 1.2 are each the smallest), and a k below 1 is
> `#NUM!` (LARGE-011: 0.5, which `LARGE` does not round up to 1). LARGE-008 to 010 and 012 and SMALL-006
> agreed. The engine reads k so for `LARGE`, `SMALL` and `AGGREGATE`'s 14 and 15 (`KthOf`), and no k is
> `#VALUE!` any more (ADR-0047's note of that date; `KthAndModTests`). SMALL-007 to 010 and LARGE-013 and 014
> are new `uncertain` cases: a k below 1 or near the count for `SMALL`, a k just below 1 for `LARGE`, and
> text or a boolean given directly as the array, which `LARGE` and `SMALL` coerce where `AGGREGATE`'s array
> form takes it as no number (AGGREGATE-061).

The ticket is `done`, and ADR-0208 does not close one on "the next run asks". Either a box —

> - [ ] `oracle.ps1 -Area small,large` before the tag (ADR-0208): SMALL-007 to 010, LARGE-013 and 014

— with the Status `ready-for-human` and a `Roadmap:` line ("ExSheet — `LARGE` and `SMALL` checked against
Excel for a k below 1 or near the count, and for text or a boolean in place of their array"), or the
orchestrator's word that they belong to ticket 16's box, which already holds AGGREGATE-062 to 067.

## 5. Ticket 01 (`01-p1-ready-functions.md`, the dates-and-text area's)

A comment to append:

> 2026-10-12, with the area pass: MOD-010 to 016 agreed, so the limit at a quotient of 1,125,900,000,000
> holds on both sides. MOD-017 did not: Excel's `MOD(123456789012,0.7)` is 0.5000078322611261, the
> identity `number - divisor*INT(number/divisor)` computed exactly and rounded once, where the engine's
> doubles gave 0.5000152587890625. Seven classes of evaluation give every MOD value Excel has given, and
> none evaluates the identity in doubles (`spikes/exsheet-financial-evaluation/mod-search.cs`). The engine
> answers the exact remainder rounded once (`%` on doubles, the divisor added where its sign is the
> number's) where all seven agree, and `#VALUE!` where they part: where the double quotient rounds up onto
> a whole number, as in `MOD(1,0.1)`, which was 0; where the remainder is within 2^-49 of the number, as in
> `MOD(1.1,0.1)`, which was 0; and rarely at the divisor or the last bit. MOD-018 to 032 are `uncertain`
> cases, MOD-020 to 032 chosen so that each pair of classes parts at one of them (ADR-0047's note of that
> date; `KthAndModTests`).

The same ADR-0208 question as ticket 04's: a box `oracle.ps1 -Area mod` for MOD-018 to 032, the Status
`ready-for-human`, and a `Roadmap:` line ("ExSheet — `MOD` checked against Excel where the quotient lies a
hair from a whole number, as in `MOD(1,0.1)` and `MOD(1.1,0.1)`, which are refused until then").

## 6. The Docs Site

### `/exsheet/formulas`, "Supported functions" (`SheetFormulas.razor`)

Old:

```html
                <b><code>LARGE</code> and <code>SMALL</code>'s k.</b> <code>LARGE</code> rounds a k that is not whole
                up, as Excel does: <code>=LARGE(A1:B5,1.5)</code> is the 2nd largest. <code>SMALL</code>'s, and a k
                between 0 and 1, are <code>#VALUE!</code> until Excel has been asked. <b><code>MOD</code></b> is
                <code>#NUM!</code> from a quotient of 1,125,900,000,000, the limit reported of Excel 365.
```

New:

```html
                <b><code>LARGE</code> and <code>SMALL</code>'s k.</b> A k that is not whole is rounded up by
                <code>LARGE</code> and truncated by <code>SMALL</code>, as Excel does: <code>=LARGE(A1:B5,1.5)</code>
                is the 2nd largest, and <code>=SMALL(A1:A9,1.5)</code> the smallest. A k below 1 is
                <code>#NUM!</code>, whole or not. <b><code>MOD</code></b> is
                <code>number - divisor*INT(number/divisor)</code> computed exactly and rounded once, as Excel's is:
                <code>=MOD(123456789012,0.7)</code> is 0.5000078322611261. Where the ways of computing it that give
                every answer Excel was seen to give part, it is <code>#VALUE!</code> until Excel has been asked:
                where the quotient lies a hair below a whole number, as in <code>=MOD(1,0.1)</code>, where the number
                lies a hair past a multiple of the divisor, as in <code>=MOD(1.1,0.1)</code>, and rarely elsewhere.
                It is <code>#NUM!</code> from a quotient of 1,125,900,000,000, as Excel 365's.
```

In the `SUBTOTAL` and `AGGREGATE` list item, after "…options 4 to 7 count its value.", a sentence:

```html
                Given text or a boolean in place of its array, <code>AGGREGATE</code>'s <code>LARGE</code> and
                <code>SMALL</code> find no number, as Excel's do: <code>=AGGREGATE(14,6,"5",1)</code> is
                <code>#NUM!</code>.
```

No Example shows these functions; nothing else on the site moves.

## 7. Found while writing

- **ADR-0202's example is out of date.** It says `MOD` refuses a quotient of 2^27 or more, but since the
  seventeenth run `MOD` answers from 2^27, and its `#NUM!` from 1,125,900,000,000 is Excel's own answer,
  not a refusal. `MOD`'s refusal where the evaluations that fit part (section 0) is such an example now.
  The edit, in "The run ExGrid's own by-hand rows need anyway", follows the list.
- **SMALL-009 asks 3.9, not the triage's 2.9.** Over A1:A9 the 2nd and 3rd smallest are both 3, so 2.9
  could not tell truncation from rounding; the 3rd and 4th are 3 and 4.
- **Observed cases still described as "Excel to be asked" or "until Excel is asked"** (SMALL-006, LARGE-005,
  006, 008 to 010, 012, MOD-006, 007, 009 to 016, AGGREGATE-007, 008, 011, 022 to 024, 032 to 037, 041, 055 to
  059):
  `-Update` rewrites `source` only. Left as they are here, to keep this branch to the cases it decides; a
  sweep over the corpus would fix them in every file at once.

ADR-0202, old:

```markdown
  - **A function that disagrees is corrected before the tag, or the arguments where it disagrees are
    refused with an Error Value**, as `MOD` refuses a quotient of 2^27 or more (ADR-0047). A width, a
    format or an undo that disagrees is corrected.
```

New:

```markdown
  - **A function that disagrees is corrected before the tag, or the arguments where it disagrees are
    refused with an Error Value**, as `MOD` refuses a quotient of 2^27 or more (ADR-0047). A width, a
    format or an undo that disagrees is corrected. *(2026-10-12: Excel answers a quotient of 2^27 or
    more, and so does `MOD` now; its `#NUM!` from 1,125,900,000,000 is Excel's own answer. `MOD` is an
    example still: where the evaluations that give every answer Excel gave part, as at `MOD(1,0.1)`, it
    is `#VALUE!` until a Windows run asks (ADR-0047's note of that date).)*
```
