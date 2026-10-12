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

---

## 0. One question for the user: MOD where the double quotient rounds up to a whole number

The engine now computes `MOD` as the exact remainder, rounded once (`%` on doubles, then the divisor added
where the remainder's sign is the number's), as the triage said. `mod-search.cs` tried 18 evaluations,
each written once, against all 17 MOD cases Excel answered (35 reads, 12 numbers): 9 fit every one, and
they fall into three classes that no other input parts (a grid of 432 inputs and 2,000,000 random ones):

- **the exact remainder, rounded once** — the engine now (B), and C# `%`, IEEE remainder, a fused
  multiply-add with the sign corrected;
- **the same, rounded first to an x87 register's 64 bits** (B4, B5) — parts from B by double rounding only,
  8 inputs in 2,000,000, the first `MOD(-0.087626,6630)`, at the last bit;
- **a fused multiply-add over the double quotient, its sign not corrected** (C, C3) — parts from B wherever
  the double quotient rounds up to a whole number the exact one is below, 8,857 inputs in 2,000,000, by a
  visible amount.

The identity in doubles, which the engine computed before, fits none: it misses MOD-017. But in the
inputs where the classes part visibly, it gave an answer too, and the change moves it:
`MOD(1,0.1)` was 0 and is now 0.09999999999999995, which a cell shows as 0.1 (C gives
-5.551115123125783E-17). `MOD(5,0.1)`, `MOD(3.3,0.01)` and `MOD(0.7,0.01)` move the same way. No Excel
answer is known for any of them; MOD-020 to MOD-028 ask. So the branch changes an unasked answer a user
can see, on inputs as common as `MOD(x,0.1)`, from one guess (0) to another (about 0.1) — the better one
by the evidence, since it is Microsoft's identity taken exactly and is the one class that fits MOD-017
and keeps the documented sign, but a guess.

The alternative, in the spirit of ADR-0047's "or not at all": keep the exact remainder, and refuse with
`#VALUE!` where the double quotient is a whole number the exact quotient is below (detectable in a line:
`Math.Floor(n/d)` differs from the quotient `%` implies), until MOD-020 to MOD-028 are asked. That turns
`MOD(1,0.1)` from 0 into `#VALUE!` for the while. **The branch does not do this**; it is the user's call. The
Change Note `area-kth-and-mod-mod-remainder.md` says the visible move as it is.

Found while searching (not a corpus case): Microsoft's Q&A thread "Problem with the MOD function in
excel?" (2011) reads `MOD(15*(1.4-1),6)`, where `15*(1.4-1)` is 6−2^-49, as −1.776E-15 in Excel 2003 and
as 6−2^-49 in Excel 2010. The 2003 answer is the identity with the quotient read at 15 digits (candidate
A4, which misses MOD-009 today); the 2010 one is what every candidate that fits gives. So `MOD` was
rewritten in Excel 2010, as the change of limit already said.

## 1. ADR-0047, the note of 2026-10-12 under "The catalogue's last Decide rows"

Dated additions, the sentences they answer kept.

**The array-form bullet.** Old:

> - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
>   (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
>   and stay `#VALUE!` (AGGREGATE-061).

New:

> - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
>   (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
>   and stay `#VALUE!` (AGGREGATE-061). *(2026-10-12, the area pass: text given directly is such an array
>   too, and holds no number, since it is not coerced: `AGGREGATE(14,6,"5",1)` is `#NUM!`, neither 5 nor
>   `#VALUE!` (AGGREGATE-061). The engine takes text and a boolean given directly so, and `LARGE` and
>   `SMALL` find no number in them. AGGREGATE-064 and 065 ask a boolean and text that spells no number, and
>   AGGREGATE-067 an Error Value given directly, which the engine gives back as an Error Value argument's
>   result, where option 6 might pass over it.)*

**The `LARGE` bullet.** Old:

> - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
>   are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
>   (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
>   (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
>   down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
>   asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
>   implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006).

New: the same text, then:

>   *(2026-10-12, the area pass: Excel truncates `SMALL`'s k, as LibreOffice and ODF do:
>   `SMALL(A1:A9,1.5)` and `SMALL(A1:A9,1.2)` are each the smallest (SMALL-004, 005), and
>   `AGGREGATE(15,6,A1:A11,1.2)` is 30 (AGGREGATE-060). `LARGE`'s 1.2 is the 2nd largest (LARGE-008,
>   AGGREGATE-059), so `LARGE` rounds up, not to nearest. A k below 1 is `#NUM!`, whole or not:
>   `LARGE(A1:B5,0.5)` is `#NUM!` (LARGE-011), not rounded up to 1. The two are one rule — the k-th largest
>   with k rounded up is the (n+1-k)-th smallest with that index truncated — and past the count once so
>   rounded, k is `#NUM!` (LARGE-010). The engine follows it, and no k is `#VALUE!` any more. SMALL-007 to
>   009, LARGE-013 and AGGREGATE-062, 063 and 066 ask where the rule reaches next: a k below 1 for `SMALL`,
>   and a k near the count.)*

**The `MOD` paragraph.** Old:

> `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
> case read (MOD-009's 0.09999999999999998 among them).

New: the same text, then, at the paragraph's end:

> *(2026-10-12, the area pass: not on every case. Excel's `MOD(123456789012,0.7)` is 0.5000078322611261
> (MOD-017): the identity computed exactly and rounded once, where in doubles it is 0.5000152587890625. The
> engine now takes the exact remainder, C#'s `%` on doubles, and adds the divisor where its sign is the
> number's, one rounding. Of 18 evaluations tried against the 17 cases Excel answered
> (`spikes/exsheet-financial-evaluation/mod-search.cs`), 9 fit every one, in three classes: this one; the
> same rounded first to an x87 register; and a fused multiply-add over the double quotient with no
> correction of the sign. They part where the double quotient rounds up to a whole number the exact one is
> below — `MOD(1,0.1)` is 0.09999999999999995 exactly, -5.551115123125783E-17 by the multiply-add, and 0 by
> the identity in doubles, which fits no case — and MOD-018 to MOD-029 ask Excel. MOD-010 to 016 agreed:
> the limit holds on both sides.)*

## 2. The engine's README (`src/ExSheet.Engine/README.md`)

- In "**Asked of Excel before they are answered**", old: "`NETWORKDAYS` and `WORKDAY` with a boolean typed as
  a date (`#VALUE!`); `SMALL` with a `k` that is not a whole number, and `LARGE` with one between 0 and 1;
  `PMT`, `PV` and `FV` with a" → new: "`NETWORKDAYS` and `WORKDAY` with a boolean typed as a date
  (`#VALUE!`); `PMT`, `PV` and `FV` with a". (The financial and dates areas edit the same bullet.)
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
  >   computed exactly and rounded once: `MOD(123456789012,0.7)` is 0.5000078322611261, as Excel's. Where the
  >   double quotient rounds up to a whole number the exact one is below, as in `MOD(1,0.1)`, that gives
  >   about the divisor, which Excel has not been asked (MOD-020 to 028).

- In "**`SUBTOTAL` and `AGGREGATE` while no row can be hidden**", old: "and `AGGREGATE`'s
  array form takes a number given directly." → new: "and `AGGREGATE`'s array form takes a Value given
  directly as an array of that one Value: a number is that number, and text or a boolean holds none
  (`#NUM!`)."

## 3. The function catalogue (`docs/specs/exsheet-functions/spec.md`)

- `LARGE`, old: "A `k` that is not whole is rounded up, as Excel's (ADR-0047, 2026-10-12)" → new: "A `k`
  that is not whole is rounded up, and one below 1 is `#NUM!`, as Excel's (ADR-0047, 2026-10-12)"
- `SMALL`, old: "A `k` that is not whole is `#VALUE!` until observed" → new: "A `k` that is not whole is
  truncated, and one below 1 is `#NUM!`, as Excel's (ADR-0047, 2026-10-12)"
- `MOD`, old: "Excel's sign follows the divisor; `#NUM!` from a quotient of 1,125,900,000,000" → new: "The
  exact remainder, rounded once, with the divisor's sign, as Excel's (MOD-017); `#NUM!` from a quotient of
  1,125,900,000,000"
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
> doubles gave 0.5000152587890625. The engine now computes the exact remainder (`%` on doubles) and adds the
> divisor where its sign is the number's, which gives every MOD value Excel has given; three evaluations
> fit them all and part elsewhere (`spikes/exsheet-financial-evaluation/mod-search.cs`), so MOD-018 to 029
> are new `uncertain` cases chosen where they part — `MOD(1,0.1)` among them, which moves from 0 to
> 0.09999999999999995 (ADR-0047's note of that date).

The same ADR-0208 question as ticket 04's: a box `oracle.ps1 -Area mod` for MOD-018 to 029, the Status
`ready-for-human`, and a `Roadmap:` line ("ExSheet — `MOD`'s last digits checked against Excel where the
quotient lies a hair below a whole number, as in `MOD(1,0.1)`").

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
                <code>=MOD(123456789012,0.7)</code> is 0.5000078322611261. It is <code>#NUM!</code> from a quotient
                of 1,125,900,000,000, as Excel 365's.
```

In the `SUBTOTAL` and `AGGREGATE` list item, after "…options 4 to 7 count its value.", a sentence:

```html
                Given text or a boolean in place of its array, <code>AGGREGATE</code>'s <code>LARGE</code> and
                <code>SMALL</code> find no number, as Excel's do: <code>=AGGREGATE(14,6,"5",1)</code> is
                <code>#NUM!</code>.
```

No Example shows these functions; nothing else on the site moves.

## 7. Found while writing

- **ADR-0202's example is out of date.** "refused with an Error Value**, as `MOD` refuses a quotient of
  2^27 or more (ADR-0047)" — since the seventeenth run `MOD` answers from 2^27, and its `#NUM!` from
  1,125,900,000,000 is Excel's own answer, not a refusal. A dated addition after "(ADR-0047)": *"(2026-10-12:
  Excel answers there, and so does `MOD` now; ADR-0047's notes of that date. `SMALL`'s k that is not whole
  was such a refusal until the area pass.)"*
- **SMALL-009 asks 3.9, not the triage's 2.9.** Over A1:A9 the 2nd and 3rd smallest are both 3, so 2.9
  could not tell truncation from rounding; the 3rd and 4th are 3 and 4.
- **Observed cases still described as "Excel to be asked" or "until Excel is asked"** (SMALL-006, LARGE-005,
  006, 008 to 010, 012, MOD-006, 007, 009 to 016, AGGREGATE-007, 008, 011, 022 to 024, 032 to 037, 041, 055 to
  059):
  `-Update` rewrites `source` only. Left as they are here, to keep this branch to the cases it decides; a
  sweep over the corpus would fix them in every file at once.
