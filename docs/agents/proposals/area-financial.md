# Area: financial — proposals after the third round of 2026-10-12

Branch `claude/area2-financial`, on `claude/area-corrections` (which holds the first round, `claude/area-financial`).
The cases: FV-011 to 024, PV-008 to 016, PMT-011 to 046, NPV-001 and 009 to 022, XNPV-005 to 026. The
evidence: `verification/2026-10-12-windows-area-2/` (`results-2026-10-12-area-2-com-en-US.json`, Excel
16.0.20430.20146, each double Excel's `number`, G17), the first pass `verification/2026-10-12-windows-area/`, the
seventeenth run's results, and `spikes/exsheet-financial-evaluation/` (`Search.cs`, `search-cs.json`,
`pmt_exact.py`; the README says what each family reproduces, which observed doubles rule each out, and what the
third round's checks found).

## Needs the user's decision (2026-10-12, the third round)

The branch does not decide these. PMT answers as below meanwhile, as it did after the decision.

**1. PMT's exception says "digits no format displays", and at three observed payments a format displays them.**
The 15th significant digit of Excel's double and of the exact payment rounded once differ at PMT-013, PMT-025
and PMT-026, and ExSheet's General shows 15 significant digits (`NumberText.General`, G15), so a wide enough
General cell, or a format with that many decimals, shows the difference:

| Case | Excel's double | at 15 digits | ExSheet's | at 15 digits |
|---|---|---|---|---|
| PMT-013 | -7363.7031239763755 | -7363.70312397638 | -7363.703123976373 | -7363.70312397637 |
| PMT-025 | 50.90552130119933 | 50.9055213011993 | 50.90552130119935 | 50.9055213011994 |
| PMT-026 | 1.8452907580883169 | 1.84529075808832 | 1.8452907580883144 | 1.84529075808831 |

The other 16 of the 19 that differ agree at 15 digits. Options: (a) keep the exception, its scope restated as
1 to 11 units in the last place, which at some payments reaches the 15th significant digit; (b) narrow it, so
that PMT refuses where its readings could differ from the exact payment at 15 digits (the engine knows the
exact payment, but not on which side of a 15-digit step Excel's double falls, and the steps lie some 5 to 90
units in the last place apart, so that within 11 units of one is a large share of all payments); (c) refuse
PMT for 0.1.0, `#NAME?` as `NPV`, a Known limitation. The branch's texts state the facts without "no format
displays", and quote it only as the decision's words.

**2. PMT's readings part beyond the last digits by cancellation, and PMT answers there.** Over the 16,896
inputs `Search.cs --refusals` runs, where the engine gives the exact payment a reading lies up to
21,345,331,117 units in the last place from it: `=PMT(-0.5,36.25,-150,0,0)`, where M39's pv + (pv+fv)/(g-1)
nearly cancels, so that its payment keeps 5 or so significant digits. The cancellation is the payment's own:
pv·g + fv, or pv·r against (pv+fv)·r/(g-1), where the payment is far smaller than its terms (fv near -pv·g, or
g far below 1). Options: (a) answer as now, the exception covering it; (b) refuse where a reading's terms
exceed the payment by more than a bound (2^6, say, so that a reading stays within some 64 units in the last
place), which the engine can compute from the exact payment and the forms' terms; (c) as 1(c).

## What the branch did, in brief

- **Decided with the user on 2026-10-12, and applied: `PMT` is kept, under an exception to ADR-0047 for
  `PMT` alone.** Its result may differ from Excel's in digits no format displays (observed up to 11 units in
  the last place, PMT-026, a relative error of about 1e-15; but see question 1), because the engine gives the
  exact payment rounded once; it is listed under 0.1.0's Known limitations. The 19 observed payments that
  differ (PMT-011 to 016, 018, 020, 021, 023 to 032) are recorded as differences by that decision, `source:
  observed`, `expect` the engine's double and `excelExpect` Excel's; the 11 that agree are observed. `NPV` is not
  part of the exception and stays undeclared.
- **The third round (the verifier's findings; the branch's choices, not the user's).** PMT's exact payment is
  now held between bounds that decide its double (`Enclosure`, `ExactPayment`), with (1+rate)^nper - 1 raised
  from the rate itself: the previous round's double-double gave visibly wrong payments at rates below about
  1e-290 (=PMT(1E-320,0.5,1000) gave -2007.94 for -2000) and was 1 or 2 units off at 874 of 3,167 payments at
  rates below 1e-15. `pmt_exact.py` checked 20,000 inputs against Python's decimal at 1200 digits: the engine
  gives decimal's answer at every one. PMT's readings, the evaluations within 64 units in the last place of
  Excel's at every observed payment, are run with a double's range (`Search.cs --refusals`, 16,896 inputs), and
  where they part in kind PMT refuses: at a rate of -1 with periods and below -1 with a whole `nper` or an odd
  root (PMT-034 to 037; PMT-036 and 037 were the engine's answers, -6 and `#DIV/0!`, and are now refusals), where
  a quantity they compute passes a double's range or falls below its normal numbers (PMT-038 to 044: overflow
  where the growth is finite, a growth below the normal numbers, a subnormal rate, a subnormal pv·rate), at a
  growth below 2^-52 (PMT-045), and with no periods at the start (PMT-046). **XNPV** refuses where the order of
  its checks would decide the answer (XNPV-021 to 026).
- **Settled and followed: `PV` and `FV`** (unchanged this round): one of the 480 evaluations searched for each,
  the financial power, gives all 17 FV and 9 PV doubles and all five Error Values. PV-014, PV-015, FV-023 and
  FV-024 ask where the power is the engine's reading; PV at a rate of −1 with an `nper` above 0 is refused
  (PV-013 asks).
- **`XNPV` follows Excel at every observed double** (unchanged): 4 of 2,592 candidates give all nine, two
  evaluations; the engine is `#NUM!` below 0, Excel's answer at the four rates asked and the engine's at any other
  (XNPV-020 asks −0.01), and refuses a rate of 0 (XNPV-019) and a last addition that nearly cancels (XNPV-018).
- **Not settled: `NPV`**, which stays undeclared (`#NAME?`) for 0.1.0 as ADR-0202's note decided.
- **New cases this round:** PMT-039 to PMT-046 and XNPV-021 to XNPV-026, all `uncertain`, each the engine's
  `#VALUE!`; PMT-034 to 038's descriptions say what the readings give.

The branch edits itself what is not an ADR, the glossary, the Definition of Done, the Docs Site or the shared
engine README: the engine, the corpus, the spike, the financial Change Notes, the catalogue's PMT, PV, FV, NPV
and XNPV rows and its notes 15 and 16 (`docs/specs/exsheet-functions/spec.md`), and ticket 13's comments.

## PMT: the decision and the cases it covers (applied in the branch)

**The decision** (with the user, 2026-10-12): PMT is kept, with an exception to ADR-0047 for PMT alone: its
result may differ from Excel's in digits no format displays (observed up to 11 units in the last place, a
relative error of about 1e-15), because the engine gives the exact payment rounded once and the difference is
Excel's own rounding. It is listed under 0.1.0's Known limitations. NPV stays undeclared, and is not part of
the exception. (Question 1 above: at three of the payments below, a format displays the difference.)

**What it covers.** The 30 payments Excel was asked (PMT-001 to 004, 008 to 033; PMT-004 at a rate of 0), of
which these 19 differ from the engine's:

| Case | Formula | Excel | Engine | Units |
|---|---|---|---|---:|
| PMT-011 | `=PMT(8%/12,10.5,10000)` | -989.2729401451879 | -989.2729401451878 | 1 |
| PMT-012 | `=PMT(8%/12,-10,10000)` | 970.3654226924854 | 970.3654226924855 | 1 |
| PMT-013 | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | -7363.703123976373 | 3 |
| PMT-014 | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | -21530.430018725492 | 2 |
| PMT-015 | `=PMT(0.05/12,12,10000,0,1)` | -852.5226402171 | -852.5226402170998 | 2 |
| PMT-016 | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | -20936.48778866115 | 1 |
| PMT-018 | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | -27.90167156122446 | 1 |
| PMT-020 | `=PMT(0.15,12,1000,-5000,1)` | -10.501648240558216 | -10.501648240558218 | 1 |
| PMT-021 | `=PMT(0.15,60,250000,-5000,1)` | -32615.986669943602 | -32615.986669943606 | 1 |
| PMT-023 | `=PMT(0.0365/365,24,250000,20000,1)` | -11260.941796175353 | -11260.941796175352 | 1 |
| PMT-024 | `=PMT(0.0525/12,24,250000,-5000,1)` | -10750.7823801456 | -10750.782380145603 | 2 |
| PMT-025 | `=PMT(0.065/12,60,1000,-5000,1)` | 50.90552130119933 | 50.90552130119935 | 3 |
| PMT-026 | `=PMT(0.07/12,240,1000,-5000)` | 1.8452907580883169 | 1.8452907580883144 | 11 |
| PMT-027 | `=PMT(0.00001,360,10000,-5000,1)` | -13.963833694802407 | -13.963833694802405 | 1 |
| PMT-028 | `=PMT(0.025/4,240,1000,-5000)` | 0.9737277420722876 | 0.9737277420722884 | 7 |
| PMT-029 | `=PMT(0.06/52,60,1000,-5000,1)` | 63.197272167756296 | 63.197272167756275 | 3 |
| PMT-030 | `=PMT(0.045/12,12,10000,-5000)` | -445.6426079318603 | -445.64260793186025 | 1 |
| PMT-031 | `=PMT(0.001,12,250000,20000,1)` | -22603.914076303507 | -22603.91407630351 | 1 |
| PMT-032 | `=PMT(0.03/12,12,250000,20000)` | -22817.29866479201 | -22817.298664792015 | 1 |

The largest relative difference is PMT-026's, 1.3e-15. PMT-001 to 004, 008 to 010, 017, 019, 022 and 033 are
the engine's doubles.

**The search, for the record.** `Search.cs`, the third search, tried 39,602 candidates (11,242 in `search.py`
and 7,344 in the second search before it). None gives all 29 payments at a rate other than 0; the best give 16.
`--pmt-spread` (this round) gives how far each family lies from Excel at its farthest observed payment: the
accurate-e families 8 units at best, the x87's FYL2XP1 and F2XM1 10, the exact growth 10, LibreOffice's
`ScGetPMT` 21, every family that rounds 1+rate to a double some 3,000,000.

**How the branch applies it (its choices, not the user's).** The 19 cases above are `observed` with
`engineDiffersByDecision` citing the exception and `excelExpect` Excel's double. The engine computes the
payment at the end in `Enclosure`s, interval arithmetic over exact dyadic numbers, each operation rounding its
bounds outward: (1+rate)^nper − 1 by (1+a)(1+b) − 1 = a + b + ab from the rate at a whole `nper`, and as
e^x − 1 of x = nper·ln(1+rate) by series that keep their relative precision otherwise; the double is given only
where both bounds round to it, with twice the bits while they do not, up to 1536, and at the start that double
is divided by 1+rate exactly and rounded once. The readings of PMT are taken as the 22,118 candidates within
64 units in the last place of Excel's at every observed payment, by their form, their way to the start,
their growth's class and the precision of the rest; `Search.cs --refusals` runs each with a double's range, and
the engine refuses wherever they part in kind. Those refusals, each asked by a case:

| Case | Formula | Where | What the readings give (`--pmt-show`) |
|---|---|---|---|
| PMT-034 | `=PMT(-1,10,1000,500)` | a rate of −1, periods above 0 | −500, that is −fv, where a form multiplies by a growth of 0 (219), and `#NUM!` (12) |
| PMT-035 | `=PMT(-2,2,1000)` | below −1, a growth of exactly 1 | `#NUM!` from every reading run (231); which Error Value Excel gives has not been observed |
| PMT-036 | `=PMT(-9,1/3,-1)` | below −1, an odd root | `#NUM!` from every reading run; −6 from an evaluation that raises 1+rate as PV and FV do (FV-017) |
| PMT-037 | `=PMT(-1,-10,1000)` | a rate of −1, periods below 0 | `#NUM!` (138) and 1000 (21) |
| PMT-038 | `=PMT(1,1100,1000)` | the growth past the range | `#NUM!` (167) and −1000 (64) |
| PMT-039 | `=PMT(1,1020,1000)` | pv·g past the range | −1000 (244) and `#NUM!` (131) |
| PMT-040 | `=PMT(0.5,1740,1000)` | as PMT-039 | −500 (244) and `#NUM!` (131) |
| PMT-041 | `=PMT(1E10,30,1)` | rate·pv·g past the range | −1E10 (317), −9999999999.999998 (2) and `#NUM!` (56) |
| PMT-042 | `=PMT(-0.99,160,1000,500)` | the growth below the normal numbers | −495 (313), −494.99999999999994 (50) and `#NUM!` (12) |
| PMT-043 | `=PMT(1E-320,0.5,1000)` | a subnormal rate | −2000 (146), `#NUM!` (11) and 0 (2) |
| PMT-044 | `=PMT(-1.64435447903749E-307,283.5,1E-05,0)` | pv·rate below the normal numbers | the exact payment (128), and one that differs from it in the 12th significant digit (31) |
| PMT-045 | `=PMT(-0.5,360,1000,500)` | a growth below 2^−52 | −250 (371) and `#NUM!` (4), a reading that takes g as 1 + (g − 1) holding it as 0 |
| PMT-046 | `=PMT(0.42,0,1000,0,1)` | no periods at the start | `#NUM!` (2,244), and from LibreOffice's reading 7.566047373982433E+18, over the remainder of a rounding |

The quantities the range rule checks (`ExactPayment.Quantities`) are every product of powers of the rate, pv,
fv, pv+fv, g, g−1, 1+rate and 1+rate·type that a form of the identity computes, each between 2^−1021 and
2^1022; the rule refuses 170 of the grid's inputs where the readings agree in kind (it does not model which
reading reaches which quantity). Below −1, any other fractional `nper` is `#NUM!`, which every reading gives.

## XNPV's evaluation (the first round's question 1b), answered as far as the doubles go

The first round found 72 evaluations that gave XNPV-001 and 004, and the engine refused where the six it
computed parted. The second pass read XNPV-005, 006 and 011 to 017:

- XNPV-005 and 006, whole years: squaring (2023.0065393622904, 2277.5848519247406), where e^(x ln b) gives
  2023.0065393622895 and 2277.5848519247415, and `Math.Pow` the second of those;
- XNPV-011, 101 days: the days over 365 (−7286.877809009438), where times 1/365 gives −7286.877809009439;
- XNPV-013: the discount e^(x ln(1+rate)) with the exponential correctly rounded (700.2269770028577). At 797
  days glibc's `exp` gives 1.2313555711418422, a unit below the correctly rounded 1.2313555711418425, and the
  sum 700.2269770028581, 4 units from Excel's;
- XNPV-014 and 015: values in no order of their dates, summed in the order given;
- XNPV-007 to 010, 012 and 017: `#NUM!` at −1, −1.5, −2 and −0.5.

Of the 2,592 candidates the third search tries, 4 give all nine doubles, and they are two evaluations: summed
from the first, and the same with Excel's last-addition adjustment. **None of the 4 gives the six `#NUM!`**:
each gives numbers at −0.5, at every date the first (−7250) or at whole years below −1 (400). So the engine
computes the first evaluation, refuses where the two part (XNPV-018 asks), and checks the rate before it
evaluates: `#NUM!` below 0, which is Excel's answer at −0.5, −1, −1.5 and −2 and the engine's at any other
negative rate (XNPV-020 asks −0.01), and `#VALUE!` at exactly 0, where "below 0" and "0 or below" part, but
where the counts or a date before the first give `#NUM!` under both readings (XNPV-019 asks).
`Search.cs --refusals` finds the engine giving the two evaluations' answer at all 4,004 inputs it draws, but
the 506 at a negative rate, where it gives `#NUM!`.

**The order of XNPV's checks (the third round).** Whether Excel checks the rate before or after an Error Value
or a cell that is not a number among its ranges has not been observed, nor its counts (XNPV-003) or a date
before the first (XNPV-002) against them, nor the values against the dates. Where more than one of those fails
and they give different answers, the answer would be the order's, and XNPV now refuses with `#VALUE!`, where it
gave the first Error Value: `=XNPV(-0.5,A1:A2,B1:B2)` with `#N/A` in A1 gave `#N/A`, and checking the rate first
gives `#NUM!`. XNPV-021 to XNPV-026 ask: an Error Value at a rate below 0, text there, an Error Value at a rate of
0, beside counts that differ, beside a date before the first, and in each range. Where every check that fails
gives the same Error Value, that is the answer. The doc comment no longer says Excel gave `#NUM!` at a negative
rate "whatever the values and dates": its six cases had numbers in order.

## PV and FV: settled

The financial power (above; `Search.cs` g22) is the only one of FV's 480 candidates that gives all 17 doubles
and all three Error Values with the platform-independent functions: g20 gives the doubles but `#NUM!` for
FV-020's `#DIV/0!`, and g23, the same power with the platform's `Math.Exp` and `Math.Log`, gives everything FV
was observed to give and is ruled out by XNPV-013, which raises 1+rate by the same power and tells the
exponentials apart. No observed double tells the logarithms apart: the engine takes the correctly rounded one
with the exponential, and FV-023 and PV-016 ask where it parts from glibc's. PV's own doubles and Error Values
leave g18, g20, g22 and g23; FV-022 rules out g18 and FV-020 g20, PV raising 1+rate as FV does (every PV double
and Error Value is the financial power's). PV-014 and PV-015 ask PV where its own observations leave those
readings, and FV-024 asks half a period before. `Search.cs --refusals` finds the engine giving g22's answer at
all 7,296 inputs of each grid, but PV's 384 at a rate of −1 with `nper` above 0, which it refuses (PV-013).

So the first round's refusals where readings parted (half a period, a negative fraction, a rate of −1 with a
negative `nper`, FV's fused sums, a fraction at a rate below −1) are all answered as Excel answered them, and
its two kept exceptions (FV-018 answered 500, FV-019's unfused sum) are settled: FV-018 is `#NUM!`, FV-019 the
unfused sum.

## Edits for the orchestrator to apply

Each old text below is whole lines of its target, with the target's indentation; each occurs once, and the
list applies in order (checked by script, 2026-10-12). They replace every edit this file proposed before; none
of those has been applied. Where an edit is unchanged from the last round, its title says so.

### 1. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The admission rule: a pointer to the exception (unchanged from the last round).

Old:

```
**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.
```

New:

```
**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.
*(2026-10-12, decided with the user: one exception, `PMT`, whose result may differ from Excel's in
digits no format displays. The note of that date, "An exception for `PMT` alone", says why and how
far it goes.)*
```

### 2. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The `type` bullet of the note of 2026-10-12 (unchanged).

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

### 3. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The `nper` bullet: PMT at rates of −1 and below.

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
  `nper` is answered. *(Answered by the area passes of 2026-10-12 for PV and FV. Excel's doubles are
  those of raising 1+rate to a negative `nper` as 1 over the power to its absolute value (FV-007,
  FV-022), to 0.5 by the square root (FV-016), and to any other fraction as e^(nper ln(1+rate)) with
  the exponential correctly rounded (FV-006, PV-005; XNPV-013 tells it from glibc's) and the logarithm
  correctly rounded too, which no observed double tells from glibc's (FV-023 and PV-016 ask). Excel
  gave a negative 1+rate to a fraction whose reciprocal is odd its odd root (FV-017) and to another
  fraction `#NUM!` (PV-011), and 1+rate of 0 to the power 0 `#NUM!` (FV-018) and to a negative power
  `#DIV/0!` (FV-020, FV-021, PV-012). PV and FV now answer every `nper` so, but PV at a rate of −1 with
  an `nper` above 0, which divides by a growth of 0 and is refused until Excel is asked (PV-013). PMT
  answers every `nper` at a rate above −1 by its exact payment, under the exception for PMT below
  (PMT-011, PMT-012); below −1 a fraction whose reciprocal is not odd is `#NUM!`, and the rest at −1
  and below is refused until Excel is asked. Past 2^53, where every double is a whole number, all three
  still refuse.)*
```

### 4. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

Two dated notes after the paragraph that ends the note: what the area passes answered, and the exception for PMT with how the engine applies it.

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
those cases answered.** The order of the payment's factors: Excel's doubles are the payment times
the product of 1+rate·type and ((g−1)/rate) (FV-009, PV-007, where the engine's order was one unit in
the last place below), and with a present value as well the sum as written, not a fused multiply-add
(FV-014, FV-015). PV and FV raise 1+rate by one power, the note's squaring at a whole number and the
bullet above otherwise: `spikes/exsheet-financial-evaluation/` searched 480 evaluations of FV and 480
of PV, and this power is the one that gives all 17 and 9 of their observed doubles and their Error
Values with the exponential XNPV-013 tells apart. XNPV divides each value by that power of its days
over 365 and sums from the first value: of 2,592 candidates, 4 give its nine observed doubles, two
evaluations, this one and the same with Excel's last-addition adjustment, which parts from it only
where the last addition nearly cancels and is refused there until asked. None of the four gives
XNPV's `#NUM!` at a negative rate: the engine checks the rate before it evaluates and is `#NUM!`
below 0, as Excel was at −0.5, −1, −1.5 and −2, its own answer at other negative rates until
XNPV-020 is read, and refuses a rate of 0 until asked. The engine now computes e^x and ln x correctly
rounded on every platform; the platform's `Math.Exp` is a unit in the last place from Excel's
discount at XNPV-013, and no observed double tells the logarithms apart. **PMT's evaluation is not
settled**: of 39,602 evaluations searched, none gives all 29 observed payments at a rate other than
0 and the best give 16; 19 of the 30 observed payments are 1 to 11 units in the last place from the
engine's exact model, which PMT keeps by the exception that follows.

*(2026-10-12, decided with the user)* **An exception for `PMT` alone: its result may differ from
Excel's in digits no format displays.** Two area passes asked Excel 29 payments at a rate other than
0, and some 58,000 evaluations were searched; none gives them all. The engine gives the exact payment
over Excel's doubles, rounded once, and Excel's payment differs from it at 19 of the 30 observed, by
1 to 11 units in the last place (PMT-026, a relative error of about 1e-15), which is taken to be
Excel's own rounding. `PMT` is kept with that difference. Its corpus cases record each as a difference
by this decision, with Excel's double beside the engine's. `NPV` is not part of it, and stays
undeclared (ADR-0202's note of this date). `0.1.0` lists it under Known limitations (ADR-0202).
*(2026-10-12, later: at PMT-013, PMT-025 and PMT-026 the two doubles differ in the 15th significant
digit, which General shows, so that "no format displays" does not hold there; put to the user.)*

*(2026-10-12, how the engine applies the exception: the financial area's choices, not the user's.)*
The payment at the end of each period is held between two bounds, interval arithmetic over exact
dyadic numbers with (1+rate)^nper − 1 raised from the rate itself, and its double is given only where
both bounds round to it; the payment at the start is that double divided by 1+rate exactly, rounded
once. So it is the exact payment rounded once at any rate above −1 and any `nper` (PMT-011, PMT-012,
each one unit in the last place from Excel's): `spikes/exsheet-financial-evaluation/pmt_exact.py`
checked 20,000 inputs against Python's decimal at 1200 digits. PMT's readings are taken as the
evaluations within 64 units in the last place of Excel's at every observed payment, and where they
part in kind, a number against an Error Value or one Error Value against another, `PMT` is refused
with `#VALUE!` until Excel is asked (PMT-034 to PMT-046; `Search.cs --refusals` runs them with a
double's range):

- at a rate of −1 with an `nper` other than 0, and below −1 with a whole `nper` or a fraction whose
  reciprocal is odd; any other fraction there is `#NUM!`, which every reading gives;
- where a quantity a reading computes passes a double's range or falls below its normal numbers, or
  the growth lies below 2^−52, which a reading that takes it as 1 + (g − 1) holds as 0;
- with no periods at the start, where LibreOffice's reading divides by a rounding's remainder.

Where the readings part beyond the last digits by cancellation, `PMT` answers; that is put to the
user with the 15th digit above.
```

### 5. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

A dated note on the note of 2026-10-12 (unchanged).

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
  small rates agrees** (PMT-017, at 10^-7). **`PMT` at ordinary rates does not**: 19 of its 30 observed
  payments are 1 to 11 units in the last place from the engine's, and none of the 39,602 evaluations
  searched gives every payment Excel gave. Decided with the user the same day: **`PMT` is kept, under
  ADR-0047's exception for `PMT` alone** (its note of this date), and its last digits are listed under
  Known limitations. **`XNPV` agrees at every observed double**: 4 of the 2,592 candidates searched give
  all nine, two evaluations that part only where a last addition nearly cancels, where the engine
  refuses; none of them gives Excel's `#NUM!` at a negative rate, which the engine gives by a check of
  the rate. `spikes/exsheet-financial-evaluation/` holds the searches.)*
```

### 6. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

Known limitations (the Consequences): PMT's last digits without "no format displays".

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
  *(2026-10-12: also `NPV`, refused by the note of that date above, and `PMT`'s last digits, which may
  differ from Excel's, under ADR-0047's exception of that date.)*
```

### 7. docs/definition-of-done.md

§27's note of 2026-10-11, the first bullet (unchanged; the decision's words).

Old:

```
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
```

New:

```
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
  *(2026-10-12, decided with the user: one exception, `PMT`, whose result may differ from Excel's in
  digits no format displays, by ADR-0047's note of that date. Its cases that differ are recorded as
  differences by that decision, with Excel's double beside the engine's, and it is a known limitation.)*
```

### 8. docs/definition-of-done.md

The note's third bullet (unchanged).

Old:

```
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations.
```

New:

```
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations. *(2026-10-12: so is `NPV`, refused
  by ADR-0202's note of that date, and so are `PMT`'s last digits, kept by ADR-0047's.)*
```

### 9. docs/definition-of-done.md

SH-7's row (unchanged; the decision's words).

Old:

```
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047) | Layer 1 | every row of every table |
```

New:

```
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047). *(2026-10-12, decided with the user: `PMT` but in digits no format displays, ADR-0047's exception for `PMT` alone; its rows that differ are recorded as differences by that decision.)* | Layer 1 | every row of every table |
```

### 10. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The Formulas page: PMT's refusals and last digits, XNPV's order.

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
                was seen to give for them, Error Values included. 1+rate is raised to the number of periods by
                squaring to a whole number, as 1 over the power to a negative one, by the square root at half a
                period, and otherwise as e<sup>n·ln(1+rate)</sup> with the exponential and the logarithm correctly
                rounded, which ExSheet computes the same on every platform. A negative 1+rate has an odd root, as
                <code>(-8)^(1/3)</code> does, and no other fractional power (<code>#NUM!</code>); at a rate of -1 no
                periods is <code>#NUM!</code> and a negative number of periods <code>#DIV/0!</code>, as Excel gave
                them. <code>PV</code> at a rate of -1 with periods is <code>#VALUE!</code> until Excel has
                been asked. <code>type</code> is read as a truth value: any number but 0, 0.5 and 2 as well as 1, is
                payment at the start of each period, as Excel reads it. <code>XNPV</code> counts the years as the days
                over 365 and sums from the first value. A rate below 0 is <code>#NUM!</code>, which is Excel's answer
                at every negative rate it was asked, -0.5 to -2, and ExSheet's own between -0.5 and 0 until Excel has
                been asked; a rate of exactly 0, or values whose last addition nearly cancels, are
                <code>#VALUE!</code> until Excel has been asked. An Error Value among the values or the dates is
                that Error Value, but <code>#VALUE!</code> where another check would give another answer (a rate below
                0, counts that differ, a date before the first, or another Error Value), since the order Excel checks
                them in has not been observed.
                <b><code>PMT</code></b> gives the exact payment, rounded once, at any rate above -1 and any number of
                periods, and reads <code>type</code> as <code>PV</code> and <code>FV</code> do. Excel's own payment can
                differ from it in its last digits: of the 30 payments Excel was asked, 19 differ by one to eleven units
                in the last of a double's digits, a relative difference of at most about 1.3·10<sup>-15</sup>, which at
                three of them shows in the 15th significant digit. No evaluation found gives every payment Excel gave,
                and this is a known limitation of 0.1.0. Where the evaluations Excel's payments leave give different
                kinds of answer, <code>PMT</code> is <code>#VALUE!</code> until Excel has been asked: at a rate of -1
                or below (there a number of periods that is a fraction whose reciprocal is not odd is
                <code>#NUM!</code>), where a number it works with would pass a double's range or fall below its normal
                numbers, as in <code>=PMT(1,1020,1000)</code>, where 1+rate raised lies below 2<sup>-52</sup>, and with
                no periods at the start of each period.
                <b><code>NPV</code></b> is <code>#NAME?</code>, as <code>IRR</code> is: its last digits differ from
                Excel's, and no evaluation found gives every result Excel was seen to give.
            </li>
```

### 11. samples/ExGrid.Docs/Pages/RoadmapByHand.cs

The Roadmap: PMT's last digits.

Old:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

New:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: NPV, IRR, XIRR and RATE, which it does not support, PMT's last digits, which may differ from Excel's, and a Reference Outline, which cannot be dragged.",
```

### 12. src/ExSheet.Engine/README.md

The table of functions (unchanged).

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

### 13. src/ExSheet.Engine/README.md

"Asked of Excel before they are answered": PMT's and XNPV's refusals; other areas rewrite the clauses beside it in the same lines.

Old:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PMT`, `PV` and `FV` with a
  fractional `type`, and, at a rate other than 0, with an `nper` that is not a whole number from 0 to
  2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
```

New:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PV` at a rate of -1 with an
  `nper` above 0; `PMT` at a rate of -1 with periods, below -1 with a whole `nper` or a fraction whose
  reciprocal is odd, where a quantity the evaluations Excel's payments leave compute passes a double's
  range or falls below its normal numbers, at a growth below 2^-52, and with no periods at the start;
  `PMT`, `PV` and `FV` with an `nper` past 2^53 at a rate other than 0; and `XNPV` at a rate of 0, where
  its last addition nearly cancels, and where an Error Value among its ranges fails beside another
  check that gives another answer (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and
  `SORTBY` with a blank or an
```

### 14. src/ExSheet.Engine/README.md

The financial bullet.

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
  (ADR-0047, the seventeenth run and the area passes of 2026-10-12). `PV` and `FV`
  evaluate Microsoft's identity in doubles, the payment times (1+rate·type)·((g−1)/rate), with g the
  financial power: squaring to a whole `nper`, 1 over the power to a negative one, the square root at
  0.5, and e^(nper ln(1+rate)) otherwise, the exponential and the logarithm correctly rounded
  (`CorrectlyRounded`, the same on every platform; Excel's exponential is the correctly rounded one at
  XNPV-013, and no observed double tells logarithms apart); a negative 1+rate has an odd root where
  1/nper is odd and is `#NUM!` otherwise, and 1+rate of 0 is `#NUM!` to the power 0 and `#DIV/0!` to a
  negative one. `XNPV` divides each value by that power of its days over 365 and sums from the first;
  it checks the rate before it evaluates and is `#NUM!` below 0, Excel's answer at the rates asked
  (-0.5 to -2) and the engine's at others, and refuses an Error Value in its ranges beside another check
  that would give another answer. `type` is a truth value: any number other than 0, 0.5 included, is
  payment at the start. **`PMT` is the exact payment rounded once, under ADR-0047's exception for `PMT`
  alone**: the payment at the end held between two bounds that decide its double (`Enclosure`, interval
  arithmetic over exact dyadic numbers, (1+rate)^nper - 1 raised from the rate itself), and the payment
  at the start that double divided by 1+rate, rounded once. Excel's payment differs from it at 19 of
  the 30 observed, by 1 to 11 units in the last place, and no evaluation searched gives every one (a
  Known limitation of 0.1.0). Where the evaluations Excel's payments leave part in kind, `PMT` refuses
  (`ExactPayment`, `Functions.Financial.cs`).
```

### 15. src/ExSheet.Engine/README.md

The functions that stay undeclared (unchanged).

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

## The Known limitations lines for `0.1.0`'s Release Notes (not an edit)

```
- `NPV` is not supported and is `#NAME?`, as `IRR`, `XIRR` and `RATE` are: its last digits differ from
  Excel's, and no evaluation found gives every result Excel was seen to give
  ([Formulas](exsheet/formulas#supported-functions)).
- `PMT` gives the exact payment rounded once, and Excel's payment can differ from it in its last digits:
  19 of the 30 payments Excel was asked differ by 1 to 11 units in the last place, a relative difference
  of at most about 1.3e-15, which at three of them shows in the 15th significant digit
  ([Formulas](exsheet/formulas#supported-functions)).
```

## Change Notes (applied in the branch; nothing for the orchestrator to apply)

This round: `run17-financial-annuity-doubles.md` says `PMT` gives the exact payment rounded once at any rate
above -1, and that Excel's differs by at most about 1.3e-15, reaching the 15th significant digit at three
payments, where it said "no format displays"; `area-financial-type-and-periods.md` no longer gives PMT the odd
root and the Error Values at a rate of -1, which are PV's and FV's; `area-financial-refused-where-readings-part.md`
lists PMT's refusals of this round and XNPV's where the order of its checks would decide. The rest are as the
last round left them: `area-financial-last-digits.md`, `area-financial-xnpv-rate.md`,
`area-financial-xnpv-last-digits.md`, `area-financial-npv.md`, and `run17-financial-periods-refused.md` deleted.

## Comments for tickets 04 and 05 (for the orchestrator, with the other areas' comments; not edits)

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/04-p2-ready-functions.md`:

```
2026-10-12, the area passes: `NPV` is refused for 0.1.0 (ADR-0202's note of that date) and moves to
ticket 13. `PV` and `FV` give every double and Error Value Excel was observed to give: they read any
`type` as Excel does and raise 1+rate to any `nper` by the financial power, with the exponential and
the logarithm correctly rounded (`spikes/exsheet-financial-evaluation/`, `Search.cs --refusals`); PV at
a rate of -1 with an `nper` above 0 is refused until asked (PV-013). `PMT` is kept under ADR-0047's
exception for `PMT` alone (decided with the user): the exact payment rounded once, which Excel's
differs from in its last digits. The branch answers it at any rate above -1 and any `nper`, and
refuses it where the evaluations its observed payments leave part in kind, until asked (PMT-034 to
046).
```

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/05-p3-ready-functions.md`:

```
2026-10-12, the area passes: `XNPV` gives every double Excel was observed to give: the days over 365,
whole years by squaring and e^(years ln(1+rate)) correctly rounded otherwise, each value divided,
summed from the first (XNPV-001, 004 to 006, 011, 013 to 016), one of the two evaluations that give
them. It checks the rate before it evaluates: `#NUM!` below 0, Excel's answer at -0.5 to -2 (XNPV-007
to 010, 012, 017), which no evaluation gives, and the engine's at other negative rates (XNPV-020
asks). It refuses a rate of 0, a last addition that nearly cancels, and an Error Value in its ranges
beside another check that would give another answer, until Excel is asked (XNPV-018, XNPV-019,
XNPV-021 to 026; `spikes/exsheet-financial-evaluation/`).
```

Ticket 13 carries this round's comments itself: the decision's comment holds what the user decided, and a
third-round comment what the branch applied and the two questions at the top of this file.

## Found on the way, for the orchestrator

- **Excel's exponential is not glibc's.** At XNPV-013's 797 days glibc's `exp` (which .NET's `Math.Exp` calls
  on Linux) rounds the other way from the correctly rounded value, and Excel's double is the correctly
  rounded one's. That is the one argument observed where they part; no observed double tells logarithms
  apart. The engine's `^` operator, `EXP`, `LN`, `LOG`, `POWER` and the hyperbolic functions still take the
  platform's `Math.Exp` and `Math.Log`, which differ between platforms and from the correctly rounded ones at
  about 1 argument in 2,000 (18 of the 40,010 checked). `CorrectlyRounded` is in the engine; whether the
  arithmetic area takes it is that area's question (ARITH-098's and ARITH-134's doubles are the correctly
  rounded functions' too). PV's and FV's power and the `^` operator now differ at such arguments.
- **The order of a domain check and an Error Value in a range is unobserved.** `XNPV` now refuses where its
  order would decide the answer (XNPV-021 to 026). The same question stands for every function with a check
  of an argument beside a range; outside XNPV it is left as it was.
- **PMT's tests and texts used a double-double that was not exact.** The previous round's `ExpWide` and
  `LogWide` lost the rate below about 1e-15 and at subnormal parts; they are removed, and PMT computes in
  `Enclosure`s. `CorrectlyRounded.Exp` and `.Log` (PV, FV and XNPV) round their double-double with a bound and
  fall back to 256-bit fixed point, and are unchanged.
- **`Search.cs` hung in Debug** after the second pass: FV-017's rate of −9 made Goldberg's log1p NaN, and
  reading a NaN's bits as a dyadic number fed the fixed-point `exp` an argument of 2^1000. `X.Of` now refuses a
  non-finite double. The spike's runs are `-c Release` (half a minute; Debug took over 15 minutes).
- **Excel gives a financial Formula's General cell its currency format on entry** (the second pass read
  `$#,##0.00_);[Red]($#,##0.00)` on every PMT, PV and FV cell). Following it is a cell-format question for
  another ticket.
- The first round's `oracle.ps1` proposal (G17 for Excel's doubles) is applied (`afd70b15`).
