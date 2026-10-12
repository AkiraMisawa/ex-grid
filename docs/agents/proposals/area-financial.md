# Area: financial — proposals after the area pass of 2026-10-12

Branch `claude/area-financial`. The cases: PMT-009, 011 to 016 and 018, FV-006, 007 and 009, PV-005 and
007, NPV-001, 002 and 005 to 008. The evidence: `verification/2026-10-12-windows-area/` and the second
search in `spikes/exsheet-financial-evaluation/` (`Search.cs`, `search-cs.json`, `discriminate.txt`; the
README says what each candidate family reproduces).

What the branch did, in brief:

- **Followed**: `type` is a truth value (PMT-009). PV and FV answer any `nper` (FV-006, FV-007, PV-005),
  raising 1+rate to a fractional one as the `^` operator does (section 2). PV and FV group the payment
  as Excel does (FV-009, PV-007). XNPV discounts by e^(x ln(1+rate)) (XNPV-004, found by the search; it
  had agreed only at 6 decimals).
- **Refused by decision**: PMT with an `nper` that is not a whole number from 0 (PMT-011, PMT-012).
  `NPV` is undeclared, as ADR-0202's note of 2026-10-12 decides when no evaluation fits (NPV-001 to 008).
- **Refused until Excel is asked**, where the evaluations Excel's doubles leave part: PV and FV with an
  `nper` that is not a whole number at a rate below -1 (FV-017, PV-011 ask), and XNPV at a rate of -1
  or below (XNPV-007 to 010 ask), where `Math.Pow`, the engine's evaluation before, gave a number for
  some arguments.
- **Stopped, for the user**: PMT's last digits (PMT-013 to 016 and 018). Section 1.

Each edit below is exact: the old text, then the new. Line breaks are as the files have them.

---

## 1. The question for the user: PMT's last digits

**What was found.** Excel's PMT-013 to 016 and 018 differ from the engine's by one to three units in
the last place:

| Case | Formula | Excel | Engine |
|---|---|---|---|
| PMT-013 | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | -7363.703123976373 |
| PMT-014 | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | -21530.430018725492 |
| PMT-015 | `=PMT(0.05/12,12,10000,0,1)` | -852.5226402171 | -852.5226402170998 |
| PMT-016 | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | -20936.48778866115 |
| PMT-018 | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | -27.90167156122446 |

Excel's PMT-001 to 003, 008 to 010 and 017 (a rate of 10^-7) are the engine's: the exact payment over
Excel's doubles, rounded once. No evaluation searched gives all 14 observed payments:

- `search.py`: 11,242 candidates, 0 give all 14, the best give 10. The 2504 that gave the first four
  all fail: none survives PMT-009 to 018 (`--spread` on PMT-013 and 018: "every survivor: none").
- `Search.cs`: 7,344 candidates, 0 give all 14, the best give 10. No candidate of either search gives
  PMT-013 except some of `search.py`'s excess forms, which miss others.
- The nearest: e = expm1(n log1p(rate)) correctly rounded and the rest in doubles (forms M5, M16), 10 of
  14, two units off PMT-012, 013 and 016 and one off PMT-017.

ADR-0202's rule is "corrected before the tag, or the arguments where it disagrees are refused with an
Error Value". The disagreements are at ordinary arguments (monthly rates with a future value or with
payment at the start), so no domain of arguments can be refused without refusing PMT where users use
it. The options:

1. **Ask more, then search again before the tag** (recommended if a pass can be made). PMT-019 to 033 are
   in `pmt.json`, `uncertain` with the engine's double. PMT-020 to 033 are the inputs that split the 92
   nearest candidates into 45 groups (`discriminate.txt` lists which gives what). One
   `oracle.ps1 -Area pmt` call asks them through COM in a minute. The search reruns on every observed
   double.
2. **Refuse PMT for 0.1.0**, `#NAME?` as `NPV` and `IRR` are, and list it under Known limitations. This
   follows ADR-0202 as NPV followed it, but PMT is the most used of the four.
3. **Keep PMT and say so under Known limitations**: "PMT may differ from Excel's in the last of its
   seventeen digits". It relaxes ADR-0047's admission rule for one function, which is an ADR change
   (ADR-0202 considered and rejected the like for IRR: "a financial function that answers close to Excel
   is a plausible wrong number").

If 1 cannot be made before the tag, 2 is what ADR-0202 says. The branch keeps PMT as it is: the user
decides.

## 2. The fractional power: the `^` operator's own evaluation (chosen)

PV's and FV's fractional `nper` is raised by `Arithmetic.Power`, the evaluation the `^` operator (and
`POWER`) takes for a fractional exponent: e^(nper ln(1+rate)) by the platform's `Math.Exp` and
`Math.Log`, but at an exponent of exactly 0.5 the square root correctly rounded, as Excel's `=2^0.5` is
(ARITH-133, the third run). One evaluation serves both, so `=(1+r)^n` and the same power inside FV
cannot differ. On glibc it gives FV-006 and PV-005 to the bit, and every other observed PV and FV
double is unchanged (the corpus, and `search.py`'s PV/FV model, F4 and V5, still give all 8 and all 6):
none has an `nper` of 0.5, the one exponent where the square root and e^(0.5 ln(1+rate)) part. FV-016
asks it: `=FV(0.42,0.5,-100)` is 45.627983043166275 by the square root, the engine's answer, and
45.62798304316633 by e^(0.5 ln(1+rate)). A portable, correctly rounded `exp` and `log`, as the
trigonometry's reduction is portable, was not taken:

- the engine's `^`, `EXP`, `LN`, `LOG`, the hyperbolic functions and now XNPV already rest on the
  platform's functions, and making one of them portable would let `=(1+r)^n` and the same power inside
  FV differ on some platform;
- glibc's `exp` and `log` are correctly rounded for all but rare arguments, so a portable correctly
  rounded pair would give the same doubles here, and the evidence (two doubles) cannot tell them apart;
- on Windows, .NET calls the UCRT's `exp` and `log`, which is the library Excel itself is most likely to
  call there.

At a rate below -1, 1+rate is negative. There the `^` operator gives an odd root (Excel's `=(-8)^(1/3)`
is -1.9999999999999998, ARITH-043), while e^(nper ln(1+rate)) has no answer for any fractional `nper`,
so the observed doubles leave candidates that part: a fractional `nper` there is refused with `#VALUE!`,
as it was before this branch, and FV-017 (`=FV(-9,1/3,-1)`, where `^` would give 0.3333333333333333)
and PV-011 ask. A whole `nper` there is still raised by squaring, which does not depend on the sign.

If a Windows run shows .NET on Windows differing from Excel on a fractional `nper`, the remedy is the
trigonometry's: compute both portably. FV-012, 013 and 016, PV-010 and XNPV-005 and 006 ask the
neighbouring cases.

## 3. ADR-0047, the note of 2026-10-12 "PV and FV give Excel's double, and PMT every double Excel was seen to give"

### 3a. The `type` bullet

Old:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked.
```

New:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked. *(Answered by the area pass of
  2026-10-12: `type` is a truth value. Excel gave the payment at the start for 0.5 and for -1 (PMT-009,
  PMT-010), so 0.5 is not truncated to 0, and any `type` other than 0 is payment at the start, in PMT,
  PV and FV.)*
```

### 3b. The `nper` bullet

Old:

```
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered.
```

New:

```
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered. *(Answered by the area pass of 2026-10-12 for PV and FV: Excel raises 1+rate to a
  negative whole number as 1 over the power by squaring (FV-007; squaring 1/(1+rate) is 19 units in the
  last place away), and to any other as e^(nper ln(1+rate)) by the platform's `exp` and `log` (FV-006,
  PV-005; a correctly rounded power is two units from PV-005). The engine takes the `^` operator's own
  evaluation of a fractional exponent, which is that but for the square root at exactly 0.5 (ARITH-133),
  so that `=(1+r)^n` and the same power inside FV cannot differ; no observed double has an `nper` of
  0.5, and FV-016 asks it. PV and FV now answer any `nper` but one that is not a whole number at a rate
  below -1, where the `^` operator's odd roots and e^(nper ln(1+rate)) part (FV-017 and PV-011 ask).
  PMT still refuses an `nper` that is not a whole number from 0: Excel answers PMT-011 and PMT-012, but
  no evaluation searched gives those payments with every other payment Excel gave. Past 2^53, where
  every double is a whole number, all three still refuse.)*
```

### 3c. A dated note after the paragraph that ends the note

Old:

```
The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.
```

New:

```
The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.

*(2026-10-12, the area pass, `verification/2026-10-12-windows-area/`.)* **What those cases answered.**
The order of the payment's factors: Excel multiplies the payment by the product of 1+rate·type and
((g−1)/rate), and FV-009 and PV-007 are that evaluation's doubles to the bit, where the engine's order
was one unit in the last place below; the engine now takes it. FV-014 and 015 ask whether pv·g is fused
into it, which no observed double tells. PMT's evaluation is not settled: Excel's PMT-017, at 10^-7, is
the exact model's double, but its PMT-013 to 016 and 018 are one to three units in the last place from
it, and none of the 18,586 evaluations the two searches tried (`spikes/exsheet-financial-evaluation/`)
gives all 14 of Excel's payments. The engine keeps the exact model until the user decides, under
ADR-0202, whether PMT is corrected or refused; PMT-019 to 033 ask Excel more. And XNPV, which the
search covered: XNPV-004, asked to tell a correctly rounded power from e^(x ln(1+rate)), is the second
to the bit, where `Math.Pow` was 8 units away. The corpus had compared XNPV at 6 decimals, so the pass
counted it as agreeing; XNPV now takes e^(x ln(1+rate)) and its cases compare every bit. At a rate of
-1 or below, where 1+rate has no logarithm and `Math.Pow` gave a number for some arguments, XNPV is
refused with `#VALUE!` until Excel is asked (XNPV-007 to 010).
```

*(If the user takes option 2 of section 1, the sentence "The engine keeps the exact model ... ask Excel
more." becomes: "`PMT` is undeclared for 0.1.0, `#NAME?` as `NPV` and `IRR` are (ADR-0202).")*

## 4. ADR-0202

### 4a. A dated note on the note of 2026-10-12 (on `main`)

Old:

```
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
```

New:

```
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
  *(2026-10-12, what the answers decided. The pass was made, `verification/2026-10-12-windows-area/`.
  **`NPV` is refused for `0.1.0`, as `IRR` is** (`#NAME?`), and listed under Known limitations: Excel's
  NPV-005 to 008 answer the same values alike typed or in a range, which leaves three distinct observed
  doubles, and none of the 4,633 evaluations searched gives all three. **`PMT` at small rates agrees**:
  PMT-017, at 10^-7, is the engine's double. **`PMT` at ordinary rates does not**: PMT-013 to 016 and
  018 are one to three units in the last place from the engine's, and none of the 18,586 evaluations
  searched gives every payment Excel gave. Correcting it is not possible on what is known, and its
  disagreements lie where it is used, so no domain of arguments can be refused; whether `PMT` is
  refused, or asked more before the tag, is decided with the user. `spikes/exsheet-financial-evaluation/`
  holds both searches.)*
```

*(The last sentence is replaced by the user's decision when it is made.)*

### 4b. Known limitations (the Consequences)

Old:

```
- `0.1.0`'s Known limitations will hold at least: `IRR`, `XIRR` and `RATE`; dragging a Reference Outline;
  ExPivot's speed near its cap; and whatever of ExPivot's layout and wording the run finds different.
```

New:

```
- `0.1.0`'s Known limitations will hold at least: `IRR`, `XIRR` and `RATE`; dragging a Reference Outline;
  ExPivot's speed near its cap; and whatever of ExPivot's layout and wording the run finds different.
  *(2026-10-12: also `NPV`, refused by the note of that date above.)*
```

### 4c. The Known limitations line for `0.1.0`'s Release Notes

```
- `NPV` is not supported and is `#NAME?`, as `IRR`, `XIRR` and `RATE` are: its last digits differ from
  Excel's, and no evaluation found gives every result Excel was seen to give
  ([Formulas](exsheet/formulas#supported-functions)).
```

## 5. The Docs Site (`samples/ExGrid.Docs`, not edited by this branch)

### 5a. The Formulas page, `Pages/Sheet/SheetFormulas.razor`

Old:

```
            <li>
                <b><code>PMT</code>, <code>PV</code> and <code>FV</code></b> give, to the last digit, every result Excel
                was seen to give for them. Which result that is depends on how Excel raises 1+rate to the number of
                periods. For <code>PMT</code> it rests on payments at rates of 0.5% to 10% a period. At much smaller
                rates Excel has not been asked, and its payment may differ from ExSheet's from the 13th significant
                digit. At a rate other than 0, a number of periods that is not a whole number, or is negative, is
                <code>#VALUE!</code> until Excel has been asked how it raises to one; so is a <code>type</code> that is
                not a whole number. Any other <code>type</code> but 0, 2 as well as 1, is payment at the start of each
                period, as Excel reads it.
            </li>
```

New:

```
            <li>
                <b><code>PV</code>, <code>FV</code> and <code>XNPV</code></b> give, to the last digit, every result
                Excel was seen to give for them. Which result that is depends on how Excel raises 1+rate to the
                number of periods: by squaring to a whole number, 1 over that to a negative one, and as the
                <code>^</code> operator raises it to any other, so <code>PV</code> and <code>FV</code> answer any
                number of periods, as Excel does. At a rate below -1, a number of periods that is not a whole
                number is <code>#VALUE!</code> in <code>PV</code> and <code>FV</code>, and a rate of -1 or below is
                <code>#VALUE!</code> in <code>XNPV</code>, until Excel has been asked. <code>type</code> is read as
                a truth value: any number but 0, 0.5 and 2 as well as 1, is payment at the start of each period, as
                Excel reads it.
                <b><code>PMT</code></b> reads <code>type</code> the same way, and gives Excel's payment at the rates
                asked, from 10<sup>-7</sup> to 10% a period, but for a few monthly and daily payments whose last
                digit differs from Excel's by one to three units. At a rate other than 0, a number of periods that
                is not a whole number, or is negative, is <code>#VALUE!</code> in <code>PMT</code>, though Excel
                answers it. <b><code>NPV</code></b> is <code>#NAME?</code>, as <code>IRR</code> is: its last digits
                differ from Excel's, and no evaluation found gives every result Excel was seen to give.
            </li>
```

*(If the user takes option 2 of section 1, the `PMT` sentences become: "<b><code>PMT</code></b> is
<code>#NAME?</code> for now, as <code>NPV</code> is: some of its payments differ from Excel's in their last
digit, and no evaluation found gives every payment Excel was seen to give.")*

### 5b. The Roadmap, `Pages/RoadmapByHand.cs`

Old:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

New:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: NPV, IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

The Functions page needs no edit: it reads the catalogue, where `NPV` is now Blocked under ticket 13, so
it lists `NPV` among the functions that are `#NAME?` with ticket 13's Roadmap entry (whose `Roadmap:`
line the branch updated).

## 6. The engine's README (`src/ExSheet.Engine/README.md`, shared)

### 6a. The table of functions

Old:

```
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `NPV` | `rate, value1, [value2], ...` |
| `XNPV` | `rate, values, dates` |
```

New:

```
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `XNPV` | `rate, values, dates` |
```

### 6b. "Asked of Excel before they are answered"

Old:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PMT`, `PV` and `FV` with a
  fractional `type`, and, at a rate other than 0, with an `nper` that is not a whole number from 0 to
  2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
```

New:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PV` and `FV` with an `nper`
  that is not a whole number at a rate below -1, `PMT`, `PV` and `FV` with an `nper` past 2^53 at a
  rate other than 0, and `XNPV` at a rate of -1 or below (`#VALUE!`); `UNIQUE` with a blank in its
  array, and `SORT` and `SORTBY` with a blank or an
```

*(Other areas may rewrite the same bullet for `EOMONTH`, `EDATE`, `SMALL` and `LARGE`; only the `PMT`,
`PV`, `FV` and `XNPV` clause is this area's.)*

### 6c. The financial bullet

Old:

```
- **`PMT`, `PV` and `FV` give every double Excel was observed to give, to the bit** (ADR-0047, the
  seventeenth run). `PV` and `FV` evaluate Microsoft's identity in doubles, raising 1+rate by squaring.
  `PMT` computes the payment at the end exactly and rounds it once, and the payment at the start
  divides that double by 1+rate, rounded once. For `PMT` that is one of several evaluations that fit
  the four payments observed, at rates of 0.5% to 10% a period. At smaller rates they part, from the
  13th significant digit at a rate of 10^-7, and PMT-016 to 018 ask Excel. A `type` that is a whole
  number other than 0 is payment at the start, as 1 is.
```

New:

```
- **`PV`, `FV` and `XNPV` give every double Excel was observed to give, to the bit** (ADR-0047, the
  seventeenth run and the area pass of 2026-10-12). `PV` and `FV` evaluate Microsoft's identity in
  doubles, the payment times (1+rate·type)·((g−1)/rate), raising 1+rate by squaring to a whole `nper`,
  1 over that to a negative one, and to any other as the `^` operator raises it (`Arithmetic.Power`:
  e^(nper ln(1+rate)) by the platform's `exp` and `log`, but the square root at exactly 0.5). At a rate
  below -1 an `nper` that is not a whole number is `#VALUE!` until Excel is asked. `XNPV` discounts each
  value by e^(years ln(1+rate)), and is `#VALUE!` at a rate of -1 or below until Excel is asked. `type`
  is a truth value: any number other than 0, 0.5 included, is payment at the start. **`PMT` is not
  settled**: it computes the payment at the
  end exactly and rounds it once, and the payment at the start divides that double by 1+rate, rounded
  once. That gives Excel's PMT-001 to 003, 008 to 010 and 017 (a rate of 10^-7), but PMT-013 to 016
  and 018 differ from Excel's by one to three units in the last place, and no evaluation searched gives
  all (ADR-0202: the user decides). At a rate other than 0, an `nper` that is not a whole number from 0
  is `#VALUE!` in `PMT`, though Excel answers it.
```

### 6d. The functions that stay undeclared

Old:

```
`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. The October 3 observation run did not resolve existing Supported functions' other
`uncertain` corpus cases.
```

New:

```
`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. `NPV` is undeclared too (ADR-0202, note of 2026-10-12): no evaluation in
`spikes/exsheet-financial-evaluation/` gives every double Excel gave for it. The October 3
observation run did not resolve existing Supported functions' other `uncertain` corpus cases.
```

## 7. Change Notes this branch's change makes false (written by the run 17 branch, not this area's)

`docs/releases/unreleased/run17-financial-periods-refused.md`, old:

```
At a rate other than 0, `PMT`, `PV` and `FV` with a number of periods that is not a whole number, or is negative, are now `#VALUE!` where they gave a number, until Excel has been asked how it raises 1+rate to one; at a rate of 0 every number of periods is still answered. A Formula that relied on a fractional `nper` shows `#VALUE!`.
```

New:

```
At a rate other than 0, `PMT` with a number of periods that is not a whole number, or is negative, is now `#VALUE!` where it gave a number, though Excel answers it, and so are `PV` and `FV` with a number of periods that is not a whole number at a rate below -1, where they were `#NUM!`, until Excel has been asked; at a rate of 0 every number of periods is still answered. A Formula that relied on a fractional `nper` in `PMT` shows `#VALUE!`.
```

`docs/releases/unreleased/run17-financial-annuity-doubles.md`, old:

```
`PMT`, `PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit: `PV` and `FV` raise 1+rate by squaring, and `PMT` computes the payment exactly before rounding it once. For `PMT` that rests on payments at rates of 0.5% to 10% a period; at much smaller rates Excel has not been asked, and its payment may differ from the 13th significant digit. A `type` that is a whole number other than 0, such as 2, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`.
```

New:

```
`PMT`, `PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit: `PV` and `FV` raise 1+rate by squaring, and `PMT` computes the payment exactly before rounding it once. For `PMT` that holds at the rates Excel was asked, from 10^-7 to 10% a period, but for a few monthly and daily payments whose last digit differs from Excel's (see the notes of the area pass). A `type` other than 0, such as 2 or 0.5, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`.
```

## 8. Tickets 04 and 05 (applied by the orchestrator with the other areas' comments)

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/04-p2-ready-functions.md`:

```
2026-10-12, the area pass: `NPV` is refused for 0.1.0 (ADR-0202's note of that date) and moves to
ticket 13 with `PMT`'s last digits; `PV` and `FV` answer any `nper` and `type` as Excel does, but an
`nper` that is not a whole number at a rate below -1, refused until Excel is asked (FV-017, PV-011)
(`spikes/exsheet-financial-evaluation/`).
```

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/05-p3-ready-functions.md`:

```
2026-10-12, the area pass: `XNPV` discounts by e^(years ln(1+rate)), Excel's XNPV-004 to the bit,
where `Math.Pow` was 8 units in the last place away, and its cases compare every bit. A rate of -1 or
below is refused with `#VALUE!` until Excel is asked (XNPV-007 to 010): there 1+rate has no logarithm,
and `Math.Pow` gave a number for some arguments (`spikes/exsheet-financial-evaluation/`).
```

## 9. Found on the way, for the orchestrator

- **XNPV disagreed, hidden by `"digits": 6`.** The branch follows Excel in its own commit (`634ad2f8`),
  which touches `xnpv.json`, outside this area's files: two expectations and two new cases. Drop the
  commit if XNPV is to stay as it was.
- **`oracle.ps1` writes Excel's double with `ToString('R')`.** Section 10 proposes `G17`.
- **Excel gives a financial Formula's General cell its currency format on entry.** The area pass read
  `$#,##0.00_);[Red]($#,##0.00)` on every PMT, PV, FV and NPV cell; ExSheet gives none. The catalogue's
  PMT note said "until that is observed" and now says it was. Following it is a cell-format question
  for another ticket.
- **Python 3.12 and later sum floats with compensation** (`sum()`), so a spike that sums doubles with
  `sum()` models no evaluation Excel performs. Neither search uses it for a modelled sum.

## 10. `oracle.ps1`: write Excel's doubles with `"G17"`, not `"R"` (for the orchestrator)

**The finding.** `oracle.ps1` records each number Excel answers as `$v.ToString('R', $Invariant)`.
Windows PowerShell 5.1 runs on .NET Framework, where Microsoft documents that `"R"` does not always
round-trip a `Double` (on 64-bit .NET Framework, some values formatted with `"R"` parse back as a
neighbouring double) and recommends `"G17"` instead; `"R"` was only made to round-trip in .NET Core
3.0. So a string the oracle wrote with 15 or fewer significant digits may name a neighbour of Excel's
double, and the corpus, the area searches and this proposal read those strings as Excel's. Of this
area's, PMT-015 (`-852.5226402171`) and NPV-003 (`5.56724267468069`) are so written; neither string
lies near enough to a midpoint for the failure to apply, so neither changes a conclusion here. The
corpus's other short ones are unchecked.

What is not affected: the oracle's agree or disagree compares the double in memory with the corpus's
expectation parsed on the same .NET Framework (`$expected -eq [double]$Answer.value2`), so the written
string plays no part in a case's status. (Whether .NET Framework parses every 17-digit expectation to
its nearest double is a separate question, unchecked here.) What is at risk is what is written:
`number`, and `value2`, which `ConvertTo-Json` formats on Windows PowerShell 5.1 by its own means, so the
string to trust is `number` once it is `"G17"`.

`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1`, old:

```
    elseif ($v -is [double]) { $answer.value2 = $v; $answer.kind = 'number'; $answer.number = $v.ToString('R', $Invariant) }
```

New:

```
    elseif ($v -is [double]) { $answer.value2 = $v; $answer.kind = 'number'; $answer.number = $v.ToString('G17', $Invariant) }
```

`"G17"` always writes 17 significant digits, so `-852.5226402171` would read `-852.52264021710005`: the
same double, written so that any parser gets it back. The next `oracle.ps1 -Area` pass (PMT-019 to 033,
NPV-009 to 022, FV-016, FV-017, PV-011, XNPV-005 to 010) should run with it, and an importer that takes
Excel's double from a result file should read `number`.
