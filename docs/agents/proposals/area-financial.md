# Area: financial — proposals after the second area pass of 2026-10-12

Branch `claude/area2-financial`, on `claude/area-corrections` (which holds the first round, `claude/area-financial`).
The cases: FV-011 to 024, PV-008 to 016, PMT-013 to 033, NPV-001 and 009 to 022, XNPV-005 to 020. The
evidence: `verification/2026-10-12-windows-area-2/` (`results-2026-10-12-area-2-com-en-US.json`, Excel
16.0.20430.20146, each double Excel's `number`, G17), the first pass `verification/2026-10-12-windows-area/`, the
seventeenth run's results, and the third search in `spikes/exsheet-financial-evaluation/` (`Search.cs`,
`search-cs.json`; the README says what each family reproduces and which observed doubles rule each out).

What the branch did, in brief:

- **Settled and followed: `PV` and `FV`.** One evaluation of the 480 searched gives all 17 FV and 9 PV
  doubles and all five Error Values: Microsoft's identity in doubles, the payment times
  (1+rate·type)·((g−1)/rate), the sum as written (FV-014, FV-015: a fused multiply-add is one unit away), and
  g *the financial power*: squaring to a whole `nper`, 1 over the power to a negative one (FV-022), the square
  root at 0.5 (FV-016), e^(n ln(1+rate)) with ln and exp correctly rounded otherwise, the odd root of a
  negative 1+rate where 1/n is odd (FV-017) and `#NUM!` for any other fraction of one (PV-011), and at 1+rate
  = 0 the `^` operator's `#NUM!` for 0^0 (FV-018, where FV gave 500) and `#DIV/0!` for a negative power
  (FV-020, FV-021, PV-012). Every refusal the first round made where readings parted is gone. One stays: PV
  at a rate of −1 with an `nper` above 0 divides by a growth of 0, whose Error Value is unobserved (PV-013 asks).
- **Settled and followed: `XNPV`.** Of 2,592 evaluations, two give all nine doubles: the days over 365, each
  value divided by the financial power of its years, summed from the first, and the same with Excel's
  last-addition adjustment, which parts only where the last addition nearly cancels. XNPV-013 is the
  correctly rounded exponential's double, where glibc's `exp`, which .NET takes on Linux, is a unit low. The
  engine computes the first and refuses where the two part (XNPV-018 asks). Excel gave `#NUM!` at every
  negative rate it was asked (−0.5 to −2): XNPV is `#NUM!` below 0, and refuses exactly 0, where "below 0"
  and "0 or below" part (XNPV-019 asks; XNPV-020 asks −0.01).
- **New in the engine: `CorrectlyRounded.Exp` and `.Log`**, e^x and ln x rounded once, the same on every
  platform (double-double, and 256-bit fixed point near a rounding boundary). 40,010 values checked against
  Python's `decimal`; `Math.Exp` and `Math.Log` differ from them at 18.
- **Not settled: `PMT`** (section 1, for the user). 0 of 39,602 evaluations give all 29 observed payments;
  the best give 16. 17 of the 29 differ from the engine's by 1 to 11 units in the last place, and two
  (PMT-011, 012) are refused by decision.
- **Not settled: `NPV`**, which stays undeclared (`#NAME?`) for 0.1.0 as ADR-0202's note decided: 0 of 5,796
  evaluations give all 21 observed doubles; the best give 7. The undeclaring is **not** reversed.
- **New cases:** FV-023 and PV-016 (where the correctly rounded exp and log part from glibc's), FV-024 (half a
  period before), PV-013 (rate −1), PV-014 and PV-015 (PV at 0.5 and −2.5, where PV's own observations leave
  readings FV's settle), XNPV-018 (the last addition), XNPV-019 (rate 0), XNPV-020 (rate −0.01).

The branch edits itself what is not an ADR, the glossary, the Definition of Done or the Docs Site: the
engine, the corpus, the spike, the area's Change Notes, the catalogue's PMT, PV, FV, NPV and XNPV rows and its
note 15 (`docs/specs/exsheet-functions/spec.md`), and a comment on ticket 13.

Each edit below is exact: the old text, then the new, whole lines with the target's indentation. They replace
every edit the first round's version of this file proposed; none of those has been applied.

---

## 1. The question for the user: PMT's last digits

**What was found.** The second area pass read PMT-019 to 033, the inputs chosen to tell the nearest of the
first searches' candidates apart. PMT-019, 022 and 033 are the engine's doubles; twelve are not:

| Case | Formula | Excel | Engine | Units |
|---|---|---|---|---:|
| PMT-013 | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | -7363.703123976373 | 3 |
| PMT-014 | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | -21530.430018725492 | 2 |
| PMT-015 | `=PMT(0.05/12,12,10000,0,1)` | -852.52264021710005 | -852.5226402170998 | 2 |
| PMT-016 | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | -20936.48778866115 | 1 |
| PMT-018 | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | -27.90167156122446 | 1 |
| PMT-020 | `=PMT(0.15,12,1000,-5000,1)` | -10.501648240558216 | -10.501648240558218 | 1 |
| PMT-021 | `=PMT(0.15,60,250000,-5000,1)` | -32615.986669943602 | -32615.986669943606 | 1 |
| PMT-023 | `=PMT(0.0365/365,24,250000,20000,1)` | -11260.941796175353 | -11260.941796175352 | 1 |
| PMT-024 | `=PMT(0.0525/12,24,250000,-5000,1)` | -10750.7823801456 | -10750.782380145603 | 2 |
| PMT-025 | `=PMT(0.065/12,60,1000,-5000,1)` | 50.905521301199329 | 50.90552130119935 | 3 |
| PMT-026 | `=PMT(0.07/12,240,1000,-5000)` | 1.8452907580883169 | 1.8452907580883144 | 11 |
| PMT-027 | `=PMT(0.00001,360,10000,-5000,1)` | -13.963833694802407 | -13.963833694802405 | 1 |
| PMT-028 | `=PMT(0.025/4,240,1000,-5000)` | 0.97372774207228763 | 0.9737277420722884 | 7 |
| PMT-029 | `=PMT(0.06/52,60,1000,-5000,1)` | 63.197272167756296 | 63.197272167756275 | 3 |
| PMT-030 | `=PMT(0.045/12,12,10000,-5000)` | -445.64260793186031 | -445.64260793186025 | 1 |
| PMT-031 | `=PMT(0.001,12,250000,20000,1)` | -22603.914076303507 | -22603.91407630351 | 1 |
| PMT-032 | `=PMT(0.03/12,12,250000,20000)` | -22817.298664792012 | -22817.298664792015 | 1 |

PMT-011 and PMT-012 (an `nper` of 10.5 and −10) are refused by decision. PMT-001 to 003, 008 to 010, 017, 019,
022 and 033 are the engine's doubles.

**The search.** `Search.cs`, the third search: 39,602 candidates (11,242 in `search.py` and 7,344 in the second
search before it), every family of the earlier searches and, new: (1+rate)^n − 1 carried accurately by every
pairing of a log1p (correctly rounded, the C library's, Goldberg's, log(1+r) corrected), the product n·log1p
rounded or exact, an exp and an expm1 (correctly rounded, the C library's, Kahan's, g−1): 96 growths;
(1+rate)^n − 1 squared without forming 1+rate, 24 ways; the period-by-period recurrence, 5 orders; the
financial power that settles PV and FV; LibreOffice's `ScGetPMT`; 6 more forms. **None gives all 29; the best
give 16** (log1p corrected from log(1+r), the product exact, exp and expm1 correctly rounded,
−fma(pv, r, (pv+fv)·r/e)/(1+r·t), the payment at the start as the end's less end·r/(1+r)); they miss PMT-020
by 19, PMT-026 by 15 and PMT-028 by 13 units. What rules each family out (no candidate of the family gives
these):

| Family | Candidates | Best | Never given |
|---|---:|---:|---|
| 1+rate formed in a double | 3,840 | 1 | all but PMT-021 and 033 (PMT-017, at 10^-7, is thousands of units away) |
| (1+rate)^n accurate: exact, x87, accurate e | 27,960 | 16 | PMT-024, PMT-026, PMT-028 |
| (1+rate)^n − 1 squared without 1+rate | 5,760 | 14 | PMT-013 (and PMT-011, 012, which it cannot raise) |
| period by period | 1,200 | 12 | PMT-013, 018, 020, 024, 026, 027, 028, 032 |
| LibreOffice's `ScGetPMT` | 2 | 4 | 25 of the 29 |

The two that resist most pull opposite ways: at PMT-020, where pv·g + fv cancels 14 to 1, Excel's payment is
within 1.1 units of the exact one, so its growth is accurate to about a tenth of a unit; at PMT-026, read the
same way, its growth is about a unit off. A library `log1p` or `expm1` that is not correctly rounded would do
that, and no portable evaluation reproduces one. That is a reading of the data, not a finding.

**The options** (ADR-0202: "corrected before the tag, or the arguments where it disagrees are refused with an
Error Value"; the disagreements lie at ordinary arguments, so no domain can be refused without refusing PMT
where it is used):

1. **Refuse PMT for 0.1.0**, `#NAME?` as `NPV` and `IRR` are, and list it under Known limitations. This is
   what ADR-0202 and the spine's first principle say once correction has failed, and two passes and some
   58,000 evaluations have failed it.
2. **Keep PMT and say so under Known limitations**: "PMT may differ from Excel's in the last of its seventeen
   digits". It relaxes ADR-0047's admission rule for one function, an ADR change ADR-0202 considered and
   rejected for IRR ("a financial function that answers close to Excel is a plausible wrong number").
3. **Ask more**: only if a new family of evaluations is proposed first; more points of the families searched
   would not tell anything the 29 have not.

The branch keeps PMT as it is (the exact model) for the user to decide. Sections 4, 6 and 7 are written for
the branch as it is, with the sentences option 1 changes given after each.

## 2. Answered: XNPV's evaluation (the first round's question 1b)

The first round found 72 evaluations that gave XNPV-001 and 004, and the engine refused where the six it
computed parted. The second pass read XNPV-005, 006 and 011 to 017, and they settle it:

- XNPV-005 and 006, whole years: squaring (2023.0065393622904, 2277.5848519247406), where e^(x ln b) gives
  2023.0065393622895 and 2277.5848519247415, and `Math.Pow` the second of those;
- XNPV-011, 101 days: the days over 365 (−7286.877809009438), where times 1/365 gives −7286.877809009439;
- XNPV-013: the discount e^(x ln(1+rate)) with the exponential correctly rounded (700.22697700285767). At 797
  days glibc's `exp` gives 1.2313555711418422, a unit below the correctly rounded 1.2313555711418425, and the
  sum 700.2269770028581, 4 units from Excel's;
- XNPV-014 and 015: values in no order of their dates, summed in the order given;
- XNPV-007 to 010, 012 and 017: `#NUM!` at −1, −1.5, −2 and −0.5, where the engine refused with `#VALUE!`
  and the evaluations give numbers at −0.5, at every date the first (−7250) and whole years below −1 (400).

Of the 2,592 evaluations the third search tries (the second search's 1,496 and the financial power with a
correctly rounded or platform exponential, at whole years by squaring or `Math.Pow`), 4 give all nine, and
they are two evaluations: summed from the first, and the same with Excel's last-addition adjustment. The
engine takes the first, refuses where they part (XNPV-018 asks), gives `#NUM!` below 0 and refuses 0
(XNPV-019, XNPV-020 ask). `Search.cs --refusals` finds the engine giving their answer at all 4,004 inputs it
draws, but the 506 at a negative rate, where it gives `#NUM!` as Excel did.

## 3. PV and FV: settled

The financial power (above; `Search.cs` g22) is the only one of FV's 480 candidates that gives all 17 doubles
and all three Error Values with the platform-independent functions: g20 gives the doubles but `#NUM!` for
FV-020's `#DIV/0!`, and g23, the same power with the platform's `Math.Exp` and `Math.Log`, gives everything FV
was observed to give and is ruled out by XNPV-013, which raises 1+rate by the same power (squaring at whole
numbers, e^(x ln b) otherwise). PV's own doubles and Error Values leave g18, g20, g22 and g23; FV-022 rules out
g18 and FV-020 g20, PV raising 1+rate as FV does (every PV double and Error Value is the financial power's).
PV-014 and PV-015 ask PV where its own observations leave those readings, and FV-023 and PV-016 where the
correctly rounded functions part from glibc's. `Search.cs --refusals` finds the engine giving g22's answer at
all 7,296 inputs of each grid, but PV's 384 at a rate of −1 with `nper` above 0, which it refuses (PV-013).

So the first round's refusals where readings parted (half a period, a negative fraction, a rate of −1 with a
negative `nper`, FV's fused sums, a fraction at a rate below −1) are all answered as Excel answered them, and
its two kept exceptions (FV-018 answered 500, FV-019's unfused sum) are settled: FV-018 is `#NUM!`, FV-019 the
unfused sum.

## 4. ADR-0047, the note of 2026-10-12 "PV and FV give Excel's double, and PMT every double Excel was seen to give"

### 4a. The `type` bullet

Old:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked.
```

New:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked. *(Answered by the area passes of
  2026-10-12: `type` is a truth value. Excel gave the payment at the start for 0.5, −1 and 0.1 (PMT-009,
  PMT-010, PMT-019), so a fraction is not truncated or rounded, and any `type` other than 0 is payment at
  the start, in PMT, PV and FV.)*
```

### 4b. The `nper` bullet

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
  `nper` is answered. *(Answered by the area passes of 2026-10-12 for PV and FV: Excel raises 1+rate to
  a negative `nper` as 1 over the power to its absolute value (FV-007, FV-022), to 0.5 by the square
  root (FV-016), to any other fraction as e^(nper ln(1+rate)) with the exponential and the logarithm
  correctly rounded (FV-006, PV-005; XNPV-013 tells the correctly rounded exponential from the
  platform's), a negative 1+rate to a fraction whose reciprocal is odd as its odd root (FV-017) and to
  any other fraction `#NUM!` (PV-011), and 1+rate of 0 to the power 0 as `#NUM!` (FV-018) and to a
  negative power as `#DIV/0!` (FV-020, FV-021, PV-012). PV and FV now answer every `nper` so, but PV at
  a rate of −1 with an `nper` above 0, which divides by a growth of 0 and is refused until Excel is asked
  (PV-013). PMT still refuses an `nper` that is not a whole number from 0: Excel answers PMT-011 and
  PMT-012, but no evaluation searched gives those payments with every other payment Excel gave. Past
  2^53, where every double is a whole number, all three still refuse.)*
```

### 4c. A dated note after the paragraph that ends the note

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

*(2026-10-12, the two area passes, `verification/2026-10-12-windows-area/` and `-area-2/`.)* **What
those cases answered.** The order of the payment's factors: Excel multiplies the payment by the
product of 1+rate·type and ((g−1)/rate) (FV-009, PV-007, where the engine's order was one unit in the
last place below), and with a present value as well it takes the sum as written, not a fused
multiply-add (FV-014, FV-015). PV, FV and XNPV raise 1+rate by one power, the note's squaring at a
whole number and the bullet above otherwise, with the exponential and the logarithm correctly rounded:
`spikes/exsheet-financial-evaluation/` searched 480 evaluations of FV, 480 of PV and 2,592 of XNPV, and
this is the one that gives all 17, 9 and 9 of their observed doubles and their Error Values (XNPV adds
only the days over 365, summed from the first value; Excel's last-addition adjustment, which parts from
that sum only where the last addition nearly cancels, is refused there until asked). The engine now
computes e^x and ln x correctly rounded on every platform; the platform's `Math.Exp` is a unit in the
last place from Excel's discount at XNPV-013. XNPV is `#NUM!` at a rate below 0, as Excel was at −0.5,
−1, −1.5 and −2, and refuses a rate of 0 until asked. **PMT's evaluation is not settled**: of 39,602
evaluations searched, none gives all 29 observed payments and the best give 16; PMT-013 to 016, 018,
020, 021 and 023 to 032 differ from the engine's exact model by 1 to 11 units in the last place, and
whether PMT is corrected or refused is decided with the user under ADR-0202.
```

*(If the user takes option 1 of section 1, the last sentence becomes: "**PMT's evaluation is not settled**:
of 39,602 evaluations searched, none gives all 29 observed payments and the best give 16, so `PMT` is
undeclared for 0.1.0, `#NAME?` as `NPV` and `IRR` are (ADR-0202).")*

## 5. ADR-0202

### 5a. A dated note on the note of 2026-10-12

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
  *(2026-10-12, what the answers decided. Two passes were made, `verification/2026-10-12-windows-area/`
  and `-area-2/`. **`NPV` is refused for `0.1.0`, as `IRR` is** (`#NAME?`), and listed under Known
  limitations: Excel answers the same values alike typed or in a range, which leaves 18 distinct observed
  doubles, and none of the 5,796 evaluations searched gives them all; the best give 7 of 21. **`PMT` at
  small rates agrees** (PMT-017, at 10^-7). **`PMT` at ordinary rates does not**: 17 of its 29 observed
  payments are 1 to 11 units in the last place from the engine's, and none of the 39,602 evaluations
  searched gives every payment Excel gave. Correcting it is not possible on what is known, and its
  disagreements lie where it is used, so no domain of arguments can be refused; whether `PMT` is
  refused for `0.1.0` is decided with the user. **`XNPV` agrees**: one evaluation gives all nine
  observed doubles, and the engine takes it. `spikes/exsheet-financial-evaluation/` holds the searches.)*
```

*(If the user takes option 1 of section 1, the sentence on `PMT` at ordinary rates ends: "...so no domain of
arguments can be refused, and **`PMT` is refused for `0.1.0` too**, `#NAME?`, and listed under Known
limitations.")*

### 5b. Known limitations (the Consequences)

Old:

```
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
```

New:

```
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12: also `NPV`, refused by the note of that date above.)*
```

*(Option 1: "also `NPV` and `PMT`, refused by the note of that date above.")*

### 5c. The Known limitations line for `0.1.0`'s Release Notes

```
- `NPV` is not supported and is `#NAME?`, as `IRR`, `XIRR` and `RATE` are: its last digits differ from
  Excel's, and no evaluation found gives every result Excel was seen to give
  ([Formulas](exsheet/formulas#supported-functions)).
```

*(Option 1: "`NPV` and `PMT` are not supported...".)*

## 6. The Docs Site (`samples/ExGrid.Docs`, not edited by this branch)

### 6a. The Formulas page, `Pages/Sheet/SheetFormulas.razor`

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
                <b><code>PV</code>, <code>FV</code> and <code>XNPV</code></b> give, to the last digit, every result Excel
                was seen to give for them, Error Values included. Which result that is depends on how Excel raises
                1+rate to the number of periods: by squaring to a whole number, as 1 over the power to a negative one,
                by the square root at half a period, and otherwise as e<sup>n·ln(1+rate)</sup> with the exponential
                and the logarithm correctly rounded, which ExSheet computes the same on every platform. A negative
                1+rate has an odd root, as <code>(-8)^(1/3)</code> does, and no other fractional power
                (<code>#NUM!</code>); at a rate of -1 no periods is <code>#NUM!</code> and a negative number of
                periods <code>#DIV/0!</code>, as in Excel. <code>PV</code> at a rate of -1 with periods is
                <code>#VALUE!</code> until Excel has been asked. <code>type</code> is read as a truth value: any number
                but 0, 0.5 and 2 as well as 1, is payment at the start of each period, as Excel reads it.
                <code>XNPV</code> counts the years as the days over 365 and sums from the first value; a rate below 0
                is <code>#NUM!</code>, as Excel's is, and a rate of exactly 0, or values whose last addition nearly
                cancels, are <code>#VALUE!</code> until Excel has been asked.
                <b><code>PMT</code></b> reads <code>type</code> the same way, and gives Excel's payment at many of the
                rates asked, 10<sup>-7</sup> among them, but most of the payments Excel was asked differ from
                ExSheet's in their last digit, by one to eleven units, and no evaluation found gives them all. At a
                rate other than 0, a number of periods that is not a whole number, or is negative, is
                <code>#VALUE!</code> in <code>PMT</code>, though Excel answers it. <b><code>NPV</code></b> is
                <code>#NAME?</code>, as <code>IRR</code> is: its last digits differ from Excel's, and no evaluation
                found gives every result Excel was seen to give.
            </li>
```

*(Option 1: the `PMT` sentences become "<b><code>PMT</code></b> is <code>#NAME?</code> for now, as
<code>NPV</code> is: most of the payments Excel was asked differ from ExSheet's in their last digit, and no
evaluation found gives every payment Excel was seen to give.")*

### 6b. The Roadmap, `Pages/RoadmapByHand.cs`

Old:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

New:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: NPV, IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

*(Option 1: "Among them: PMT, NPV, IRR, XIRR and RATE, ...".)* The Functions page needs no edit: it reads the
catalogue, where `NPV` is Blocked under ticket 13.

## 7. The engine's README (`src/ExSheet.Engine/README.md`, shared)

### 7a. The table of functions

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

### 7b. "Asked of Excel before they are answered"

Only the `PMT`, `PV` and `FV` clause is this area's; other areas rewrite the clauses beside it in the same
lines. Old:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PMT`, `PV` and `FV` with a
  fractional `type`, and, at a rate other than 0, with an `nper` that is not a whole number from 0 to
  2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
```

New:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PV` at a rate of -1 with an
  `nper` above 0, `PMT`, `PV` and `FV` with an `nper` past 2^53 at a rate other than 0, and `XNPV` at a
  rate of 0 and where its last addition nearly cancels (`#VALUE!`); `UNIQUE` with a blank in its array,
  and `SORT` and `SORTBY` with a blank or an
```

### 7c. The financial bullet

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
- **`PV`, `FV` and `XNPV` give every double and Error Value Excel was observed to give, to the bit**
  (ADR-0047, the seventeenth run and the area passes of 2026-10-12). `PV` and `FV` evaluate Microsoft's
  identity in doubles, the payment times (1+rate·type)·((g−1)/rate), with g the financial power:
  squaring to a whole `nper`, 1 over the power to a negative one, the square root at 0.5, and
  e^(nper ln(1+rate)) otherwise, the exponential and the logarithm correctly rounded
  (`CorrectlyRounded`, the same on every platform); a negative 1+rate has an odd root where 1/nper is
  odd and is `#NUM!` otherwise, and 1+rate of 0 is `#NUM!` to the power 0 and `#DIV/0!` to a negative
  one. `XNPV` divides each value by that power of its days over 365 and sums from the first; it is
  `#NUM!` at a rate below 0. `type` is a truth value: any number other than 0, 0.5 included, is payment
  at the start. **`PMT` is not settled**: it computes the payment at the end exactly and rounds it once,
  and the payment at the start divides that double by 1+rate, rounded once. That gives 10 of the 29
  payments Excel was observed to give, 10^-7 among them, and 17 others differ by 1 to 11 units in the
  last place; no evaluation searched gives all (ADR-0202: the user decides). At a rate other than 0, an
  `nper` that is not a whole number from 0 is `#VALUE!` in `PMT`, though Excel answers it (PMT-011 and
  PMT-012).
```

*(Option 1: the `PMT` sentences become "`PMT` is undeclared (`#NAME?`), as `NPV` is: no evaluation searched
gives every payment Excel was observed to give (ADR-0202).")*

### 7d. The functions that stay undeclared

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

## 8. Change Notes this branch's change makes false (written by the run 17 branch, not this area's)

`docs/releases/unreleased/run17-financial-periods-refused.md`, old:

```
At a rate other than 0, `PMT`, `PV` and `FV` with a number of periods that is not a whole number, or is negative, are now `#VALUE!` where they gave a number, until Excel has been asked how it raises 1+rate to one; at a rate of 0 every number of periods is still answered. A Formula that relied on a fractional `nper` shows `#VALUE!`.
```

New:

```
At a rate other than 0, `PMT` with a number of periods that is not a whole number, or is negative, is now `#VALUE!` where it gave a number, though Excel answers it; at a rate of 0 every number of periods is still answered. A Formula that relied on such an `nper` in `PMT` shows `#VALUE!`. `PV` and `FV` answer every such number of periods as Excel does (see the notes of the area passes).
```

`docs/releases/unreleased/run17-financial-annuity-doubles.md`, old:

```
`PMT`, `PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit: `PV` and `FV` raise 1+rate by squaring, and `PMT` computes the payment exactly before rounding it once. For `PMT` that rests on payments at rates of 0.5% to 10% a period; at much smaller rates Excel has not been asked, and its payment may differ from the 13th significant digit. A `type` that is a whole number other than 0, such as 2, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`.
```

New:

```
`PMT`, `PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit: `PV` and `FV` raise 1+rate by squaring, and `PMT` computes the payment exactly before rounding it once. For `PMT` that holds at the rates of the seventeenth run and at 10^-7, but most of the payments the area passes asked differ from Excel's in their last digit (see the notes of the area passes). A `type` other than 0, such as 2 or 0.5, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`.
```

## 9. Tickets 04 and 05 (for the orchestrator, with the other areas' comments)

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/04-p2-ready-functions.md`:

```
2026-10-12, the area passes: `NPV` is refused for 0.1.0 (ADR-0202's note of that date) and moves to
ticket 13 with `PMT`'s last digits, which no evaluation searched settles. `PV` and `FV` give every
double and Error Value Excel was observed to give: they read any `type` as Excel does and raise 1+rate
to any `nper` by the financial power, with the exponential and the logarithm correctly rounded
(`spikes/exsheet-financial-evaluation/`, `Search.cs --refusals`). PV at a rate of -1 with an `nper`
above 0 is refused until asked (PV-013).
```

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/05-p3-ready-functions.md`:

```
2026-10-12, the area passes: `XNPV` gives every double and Error Value Excel was observed to give: the
days over 365, whole years by squaring and e^(years ln(1+rate)) correctly rounded otherwise, each value
divided, summed from the first (XNPV-001, 004 to 006, 011, 013 to 016), and `#NUM!` at a rate below 0
(XNPV-007 to 010, 012, 017). It refuses a rate of 0 and a last addition that nearly cancels until Excel
is asked (XNPV-018 to 020; `spikes/exsheet-financial-evaluation/`).
```

Ticket 13 carries this round's comment itself (the branch added it).

## 10. Found on the way, for the orchestrator

- **Excel's exponential is not glibc's.** At XNPV-013's 797 days glibc's `exp` (which .NET's `Math.Exp` calls
  on Linux) rounds the other way from the correctly rounded value, and Excel's double is the correctly
  rounded one's. The engine's `^` operator, `EXP`, `LN`, `LOG`, `POWER` and the hyperbolic functions still
  take the platform's `Math.Exp` and `Math.Log`, which differ between platforms and from the correctly
  rounded ones at about 1 argument in 2,000 (18 of the 40,010 checked). `CorrectlyRounded` is now in the
  engine; whether the arithmetic area takes it is that area's question (ARITH-098's and ARITH-134's doubles
  are the correctly rounded functions' too). PV's and FV's power and the `^` operator now differ at such
  arguments.
- **`Search.cs` hung in Debug** after the second pass: FV-017's rate of −9 made Goldberg's log1p NaN, and
  reading a NaN's bits as a dyadic number fed the fixed-point `exp` an argument of 2^1000. `X.Of` now refuses a
  non-finite double. The spike's runs are `-c Release` (half a minute; Debug took over 15 minutes).
- **Excel gives a financial Formula's General cell its currency format on entry** (still true: the second pass
  read `$#,##0.00_);[Red]($#,##0.00)` on every PMT, PV and FV cell). Following it is a cell-format question for
  another ticket.
- The first round's `oracle.ps1` proposal (G17 for Excel's doubles) is applied (`afd70b15`).
