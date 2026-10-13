# The evaluation Excel uses for PMT, PV, FV, NPV and XNPV

The seventeenth Windows run (`verification/2026-10-11-windows-17/`) read Excel's double for every
financial case of the corpus, and the two area passes of 2026-10-12 (`verification/2026-10-12-windows-area/`
and `verification/2026-10-12-windows-area-2/`) read the cases each round of corrections left `uncertain`.
ADR-0047 admits a function only where it answers as Excel, and ADR-0202 corrects or refuses a
disagreement before the tag. This experiment searches for the evaluation that gives Excel's double, bit
for bit, on **every** observed double, the agreeing cases included, so that a correction breaks none, and
checks the survivors against every Error Value Excel was observed to give. Nothing here ships.

Two programs, each run over every `results-*.json` under `ExcelOracle/`:

```sh
# The first search, in Python, exact arithmetic (fractions.Fraction) rounded at 53 or 64 bits after each step
python3 -B spikes/exsheet-financial-evaluation/search.py                  # the table (about 3 minutes)
python3 -B spikes/exsheet-financial-evaluation/search.py --detail         # every candidate, with its distance in units in the last place
python3 -B spikes/exsheet-financial-evaluation/search.py --json           # rewrites results.json
python3 -B spikes/exsheet-financial-evaluation/search.py --vectors FILE   # the engine's PV, FV and PMT at 400 generated inputs
python3 -B spikes/exsheet-financial-evaluation/search.py --spread "=PMT(1/10000000,12,1000)"   # the doubles PMT's survivors give for one formula

# The second and third searches, in C# under .NET (Release: the exact arithmetic is slow in Debug)
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs                 # the table (half a minute)
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --only PMT,NPV   # some functions only
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --json       # rewrites search-cs.json
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --refusals   # the engine against the survivors (exits 1 if it differs)
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --discriminate   # the inputs that tell PMT's and NPV's nearest apart (discriminate.txt)
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --discriminate-xnpv   # the same for XNPV (discriminate-xnpv.txt)
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --predict "=FV(0.05/12,12,-150,-1000,1)"
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --only PMT --pmt-spread   # how far each PMT candidate lies from Excel
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --only PMT --pmt-show "=PMT(1,1020,1000)"   # every answer PMT's readings give there
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --engine IN OUT   # the engine's answer to each Formula of IN
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --only PMT --pmt-readings IN   # PMT's readings wherever the engine answers a Formula of IN

# PMT against Python's decimal at 1200 digits (the third and fourth rounds): draw inputs, run the engine on them, compare
python3 -B spikes/exsheet-financial-evaluation/pmt_exact.py --generate 20000 7 > pmt.txt
nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --engine pmt.txt engine.txt
python3 -B spikes/exsheet-financial-evaluation/pmt_exact.py --check pmt.txt engine.txt
```

`Search.cs` references `src/ExSheet.Engine` (`#:project`) for `--refusals` and `--engine`, which enter each
input as a Formula in a `Sheet` through the engine's public surface; everything else in it is the standard
library and, for the C library's `log1p` and `expm1`, `libm.so.6`. `pmt_exact.py` is the standard library's
`decimal`, and computes nothing the engine computes: it re-implements PMT's rules from their comments over exact
values.

`search.py` reads each case's cells from the corpus; Excel's arguments are its own doubles (`0.06/12` and
`8%/12` are divided in doubles, as Excel's operators do). Its logarithms and exponentials come from
`decimal` at 80 digits, which models a correctly rounded library function. `Search.cs` computes in native
doubles where a candidate is in doubles, so `Math.*` is the platform's (glibc's here, as Python's `math`
is), and in an exact dyadic number rounded at 64 bits where a candidate models an x87 register; its
correctly rounded `ln`, `exp`, `log1p`, `expm1`, FYL2XP1 and F2XM1 are computed in 256-bit fixed point.

## The observations

89 doubles, each read the same by every run that read it: FV-001 to 017, 019 and 022; PV-001 to 010;
PMT-001 to 004 and 008 to 033; NPV-001 to 003 and 005 to 022; XNPV-001, 004 to 006, 011 and 013 to 016. A
rate of 0 raises no power and is no search case (FV-004, FV-008, PV-002, PMT-004). NPV's 21 are 18
distinct numbers (NPV-001, 007 and 008 are one; NPV-005 and 006 another; NPV-002 is NPV-006's less 40000).

15 Error Values with numbers for arguments, which `Search.cs` checks every survivor against (2026-10-12,
the second area pass): FV-018 `#NUM!`, FV-020 and 021 `#DIV/0!`; PV-011 `#NUM!`, PV-012 `#DIV/0!`; PMT-005
`#NUM!`; NPV-004 `#DIV/0!`; XNPV-002, 003, 007 to 010, 012 and 017 `#NUM!`.

## What was tried, and how many observed doubles each reproduces

The third search is `Search.cs` as the second area pass left it (2026-10-12, later); its counts are its last
`--json` run, `search-cs.json`.

| Function | Observed doubles (rate ≠ 0) | `search.py` | `Search.cs`, second search | `Search.cs`, third search | Reproduce all | Best other |
|---|---:|---:|---:|---:|---|---|
| FV | 17 | 1022 | 440 | 480 | **one power**, three orders of the same products (below) | 16 of 17 |
| PV | 9 | 1022 | 440 | 480 | that power and others FV rules out (below) | 8 of 9 |
| PMT | 29 | 11242 | 7344 | 39602 | **none** | **16 of 29** |
| NPV | 21 | 301, then 456 | 3876 | 5796 | **none** | **7 of 21** |
| XNPV | 9 | 12 | 1496 | 2592 | **two evaluations** (below) | 8 of 9 |

`search-cs.json` holds every candidate with its match count, the legend for its short labels, each
function's 40 best that do not reproduce all, the Error Values each survivor misses, and, per family, the
observed doubles no candidate of the family gives: the cases that rule the whole family out.

### The candidates

`search.py`'s: a way of raising 1+rate to `nper` (a correctly rounded power; squaring from the lowest or the
highest bit; repeated multiplication; e^(n ln(1+rate)); e^(n log1p(rate)); the x87's FYL2X, F2XM1 and
FSCALE; the C library's `pow`; 21 ways of carrying (1+rate)^n − 1 without forming 1+rate), a precision
for it and for the rest, and an algebraic form: 7 for FV, 7 for PV, 20 for PMT, and for PMT how a payment
at the start is reached from the one at the end. NPV: 9 schemes, then 16 ways of discounting × 6 orders of
summing × both precisions.

`Search.cs`, the second search, added: (1+rate)^n − 1 carried accurately (expm1(n log1p(rate)) correctly
rounded; Goldberg's log1p and Kahan's expm1 from the platform's `Math.Log` and `Math.Exp`; the x87's
FYL2XP1 and F2XM1; squaring in a 64-bit register; the exact power); 10 forms for FV and 10 for PV, 34 for
PMT (fused multiply-adds, 1/g formed once); six ways to the payment at the start; four readings of a
fractional or negative `nper` for PV and FV (g18 to g21); for NPV 3 ways of forming 1+rate × 16 ways of
discounting × the discount, each term and the sum at 53 or 64 bits × 11 orders of summing, and Horner's
rule from either end; for XNPV the days over 365 or times 1/365 × 8 discounts × each value divided, times
the reciprocal or times e^(-x ln b) × terms and sum at 53 or 64 bits × the orders of summing.

`Search.cs`, the third search (2026-10-12, after the second area pass), adds:

- **PV and FV**: g22, *the financial power* (below), and g23, the same with the platform's `Math.Log` and
  `Math.Exp`.
- **PMT**: in doubles, (1+rate)^n − 1 carried accurately by every pairing of a log1p (correctly rounded,
  the C library's, Goldberg's from a correctly rounded or the platform's log, or log(1+r) corrected by
  (r−((1+r)−1))/(1+r)), the product n·L rounded or exact, an exp (correctly rounded, the platform's, or
  1+e) and an expm1 (correctly rounded, the C library's, Kahan's from either pair, or g−1): 96; (1+rate)^n
  − 1 squared without forming 1+rate, 4 ways of squaring × 6 of combining; the period-by-period
  recurrence e += r(1+e) in 5 orders; the financial power, g22 and g23; 6 more forms (M36 to M41); and
  LibreOffice's `ScGetPMT`, which reaches the payment at the start through expm1((n+1) log1p(r)) − r.
- **NPV**: e^(i log1p r) and e^(−i log1p r) correctly rounded, (1/b)^i by squaring 1/b, b^i as
  e^(i ln b) with the log taken once; summed by halving and by magnitude from the largest.
- **XNPV**: whole years by squaring or by `Math.Pow`, else e^(x ln b) with ln and exp correctly rounded;
  whole years by squaring, else 1/e^(−x ln b) correctly rounded, b^x correctly rounded, or the x87's.

### PV and FV: settled, the financial power

Of FV's 480 candidates, 9 give all 17 doubles: g20, g22 and g23, each with F4, F7 or F8, which are the same
products in another order. Of those, g20 misses FV-020 (`#NUM!` for `#DIV/0!`: it raises 0 to −0.5 as 1 over
0^0.5); g22 and g23 give every observed FV Error Value too. They are one evaluation but for the exponential
and the logarithm, and XNPV-013, below, tells the exponentials apart: Excel's is the correctly rounded one. No
observed double tells the logarithms apart (FV-023 and PV-016 ask), and the engine takes the correctly rounded
one with it. So **FV is
−(pv·g + pmt·((1+rate·type)·((g−1)/rate)))** in doubles with g *the financial power* (`Pow.Financial`,
g22):

- a whole `nper` from 0 by squaring from the exponent's lowest bit (FV-001 to 003, 005, 009, 010, 014, 015,
  019; PV-001, 003, 004, 006, 007);
- a negative `nper` as 1 over the power to its absolute value: FV-007 and PV-009 at −10 (squaring
  1/(1+rate) is 19 units in the last place away), FV-022 at −2.5 (raising to −2.5 directly is 78 away);
- 0.5 by the square root (FV-016; e^(0.5 ln(1+rate)) is 8 away), as the `^` operator takes it (ARITH-133);
- any other `nper` as e^(n ln(1+rate)), ln and exp each correctly rounded (FV-006, 012, 013; PV-005);
- a negative 1+rate to a fraction whose reciprocal is an odd whole number as its odd root, −e^(n ln(−b))
  (FV-017, as the `^` operator's `=(-8)^(1/3)`, ARITH-043), and to any other fraction `#NUM!` (PV-011);
- 1+rate of 0 to the power 0 `#NUM!` (FV-018; squaring gave 1, and FV 500) and to a negative power
  `#DIV/0!` (FV-020, FV-021, PV-012), as the `^` operator's `=0^0` and `=0^-1` (ARITH-041, ARITH-042).

FV-014 and FV-015, a present value and a payment at the start, are the sum as written: fusing pv·g, or the
payment's product, into a multiply-add (F9, F10) is one unit in the last place away from each. FV-019 is it
at the end.

**PV is −(fv + pmt·((1+rate·type)·((g−1)/rate)))/g** with the same g. PV's own 9 doubles leave 14
candidates (g0 and g18 to g23, each with V5 or V9, the same products), and its two Error Values leave
g18, g20, g22 and g23; FV's settle the rest (FV-022 rules g18 out, FV-020 g20), as PV raises 1+rate as FV
does: every PV double and Error Value is the financial power's. PV-014 and PV-015 ask PV at half a period and
at −2.5 directly, where the readings PV's own observations leave part. At a rate of −1 with an `nper` above
0 the growth is 0 and PV divides by it; which Error Value Excel gives there is not observed, and the engine
refuses it (PV-013 asks).

*(The second search had found three FV forms and every reading of a fractional `nper`, and the engine
refused where they parted; FV-014 to 022 and PV-011 and 012, which the second area pass read, settle them as
above. The second search's note that a correctly rounded `exp` and `log` part from the platform's at no PV
or FV input drawn still holds for the `--refusals` grid; FV-023 and PV-016 are inputs where they part, at
11.25 periods of 0.5%, and ask.)*

### XNPV: settled

Of the 2,592 candidates, 4 give all nine doubles, and they are two evaluations: the days over 365, whole
years by squaring and e^(x ln(1+rate)) with ln and exp correctly rounded otherwise, each value divided by
its discount, summed from the first (`forward`; the fused sum and the 64-bit terms are the same here, each
value being divided), and the same with Excel's last-addition adjustment (`forward, the last addition as
Excel's`). They part only where the last addition nearly cancels: the engine computes the first and
refuses there (XNPV-018 asks). The nine settle each step:

- XNPV-005 and XNPV-006, whole years: squaring, where e^(x ln b) gives 2023.0065393622895 and
  2277.5848519247415, and `Math.Pow` the second of those;
- XNPV-011, 101 days: the days over 365, where times 1/365 gives −7286.877809009439;
- XNPV-013: the correctly rounded exponential. At 797 days glibc's `exp` gives the discount
  1.2313555711418422, one unit in the last place below the correctly rounded 1.2313555711418425 (the exact
  value lies 0.0008 of a unit above the midpoint between them), and the sum 700.2269770028581, 4 units from Excel's.

No candidate models Excel's Error Value at a negative rate: Excel gave `#NUM!` at −0.5 (XNPV-012, 017), −1
(XNPV-007, 008), −1.5 and −2 (XNPV-009, 010), where the survivors give numbers at −0.5, at every date the
first (−7250) and at whole years below −1 (400). The engine checks the rate before it evaluates: `#NUM!` below
0, which is Excel's answer at the four rates asked and the engine's at any other (XNPV-020 asks −0.01), and a
rate of 0 refused where "below 0" and "0 or below" part, after the counts and the dates, which give `#NUM!`
under both (XNPV-019 asks).

### PMT: no evaluation gives every observed payment

**0 of 39,602** (the third search; 11,242 in `search.py` and 7,344 in the second). The best give **16 of
29**: in doubles, log1p corrected from log(1+r), the product n·L exact, exp and expm1 correctly rounded,
and the payment as −fma(pv, r, (pv+fv)·r/e)/(1+r·t) (M31) or −(pv·r + (pv+fv)·r/e)/(1+r·t) (M19), the
payment at the start as the end's less end·r/(1+r). They miss PMT-020 by 19, PMT-026 by 15 and PMT-028 by
13 units in the last place.

Excel's payments against the exact payment over Excel's doubles, in units in the last place: PMT-001 to
003, 008 to 010, 016 to 019, 021, 022, 027, 031 and 033 lie within one unit; PMT-013 −2.5, PMT-014 +1.4,
PMT-015 −1.3, PMT-020 +1.1, PMT-023 −1.4, PMT-024 +2.3, PMT-025 −2.2, PMT-029 +2.1, PMT-030 −1.2, PMT-032
+1.3, **PMT-026 +10.7** and **PMT-028 −7.0**. The cases that rule out each family (`reproducedByNone` in
`search-cs.json`):

- **Every evaluation that forms 1+rate in a double** (1,920 + 1,920, 1 of 29 at best): PMT-017, at 10^-7,
  is the exact payment rounded once, and those are thousands of units away; of the 29 they give only
  PMT-021 and PMT-033, each by some candidate.
- **Every evaluation with (1+rate)^n accurate** (exact, x87, accurate e: 11 to 16 of 29): **PMT-024,
  PMT-026 and PMT-028**, which no candidate of these families gives. PMT-026 and 028 are 240 periods with
  a future value that cancels most of pv·g, where Excel is 10.7 and 7.0 units from the exact payment and
  those evaluations within about 3; the third search's 23,040 accurate-e candidates never give PMT-023
  either.
- **Every evaluation with (1+rate)^n − 1 squared or accumulated without forming 1+rate** (5,760 and 1,200;
  14 and 12 of 29): PMT-013, which none gives, and PMT-011 and 012, which they cannot raise; the
  period-by-period recurrence never gives PMT-018, 020, 024, 026, 027, 028 or 032 either.
- **PMT-020** (=PMT(0.15,12,1000,-5000,1)): pv·g + fv cancels by 14 to 1, and Excel's payment is 1.1 units
  from the exact one, so its growth is accurate to about a tenth of a unit, which no evaluation that rounds
  g to a double and then forms pv·g + fv reaches (the best accurate-e candidates miss it by 19 and 22);
  at PMT-026, read the same way, Excel's growth is about one unit off. No single precision for g fits both.

So PMT's evaluation is not settled. Its likely cause is a library log1p or expm1 that is not correctly
rounded, which no portable evaluation reproduces; that is a reading of the data, not a finding. **Decided
with the user (2026-10-12): PMT is kept, with an exception to ADR-0047 for PMT alone**: its result may differ
from Excel's in its last digits, the engine giving the exact payment rounded once, and 0.1.0 lists it under
Known limitations. PMT-001 to 003, 017, 022 and 033 are the engine's doubles, and so were 008 to 010 and 019
until the payment at the start was decided (below). *(The third
and fourth rounds, 2026-10-12, put to the user: at PMT-013 and PMT-026, at the end, Excel's double and the exact
payment rounded once differ in the 15th significant digit, which General shows. At the start the engine divided
the payment at the end's double by 1+rate and rounded it a second time, the branch's rule then: that was Excel's double at PMT-008,
009, 010 and 019, where the exact payment at the start rounded once is a unit away, and a unit from the exact
payment rounded once at PMT-014, 015, 025 and 029, where Excel's lies on its other side; at PMT-025 it was the
engine's double that showed another 15th digit. Decided with the user on 2026-10-13: the payment at the start
is the exact payment at the start rounded once, so the engine's double at PMT-025 agrees with Excel's at 15
digits, and Excel's differs from the engine's at 23 of the 30 observed payments, a unit at PMT-008, 009, 010 and
019. Decided with the user the same day: the exception holds where the difference shows in General's 15th
digit, PMT-013 and PMT-026, and the bound of 2^6 below is kept.)* PMT-011 and 012, an `nper` of 10.5 and −10, are answered by the
same exact payment: Excel's doubles lie −1.03 and −1.23 units from it, each one unit from the engine's.

`--pmt-spread` (the third round) says how far each family lies from Excel at its farthest observed payment, in
units in the last place: the accurate-e families in doubles 8 at best, the x87's FYL2XP1 and F2XM1 10, the exact
growth 10, LibreOffice's `ScGetPMT` 21; every evaluation that rounds 1+rate to a double some 3,000,000. PMT's
readings are taken as the 18,144 + 1,338 + 1,338 + 432 + 432 + 216 + 216 + 2 candidates within 64 units of
Excel's at every observed payment (the branch's choice of bound): every family but those that round 1+rate,
those that square 1+rate in a register, and those without a fractional `nper`, and of the forms every one but
M6, M10, M33 and M34, which form 1 - 1/g.

### NPV: no evaluation gives every observed double

**0 of 5,796**; the best give **7 of 21**: Horner's rule from the first value with a fused multiply-add,
then divided by the running product (b^n), which misses NPV-009 by 43, NPV-014 by 65, NPV-015 by 26 and
NPV-021 by 16 units in the last place, among others. NPV-009, 014, 015, 021 and 022 are the cases most
families never give: each is a sum whose largest term is 16 to 150 times the result, where Excel lies
within about a unit in the last place of that term from the exact sum over the double 1+rate. NPV-014 is
given by 6 candidates, NPV-021 by 3, NPV-022 by none. NPV stays undeclared for 0.1.0 (ADR-0202, note of 2026-10-12).

## The refusals (`--refusals`)

ADR-0047 admits an answer only where it follows from Excel's observed answers, and where the evaluations
those leave part, the engine refuses. `--refusals` runs the survivors over a grid, and the engine through its
public `Sheet`, and requires the engine's answer where they agree and `#VALUE!` where they part. FV, PV and
PMT: 12 rates (−9, −1.5, −1 and −0.5 among them), 19 numbers of periods (whole, negative, 0.5, −0.5, 10.5,
−10.5, 1/3 and others), four payments, four present or future values and both types, 7,296 inputs each.
XNPV: the corpus's inputs and 4,000 drawn (up to seven values, dates whole years apart, the days at which
/365 and *(1/365) part, and any).

PMT has no survivors. Its readings, the candidates within 64 units of Excel's at every observed payment, are
run instead (the third round, 2026-10-12, `PmtReadings`) with a double's range: in native doubles where a
candidate's rest is at 53 bits, with g and e by the class of its growth (exp(x), 1+e, or the power PV and FV
share, with the C library's log1p and expm1, which keep their precision to the subnormal numbers; or exact, or
the x87's), and in an x87's register where it is at 64. Over the annuity grid and an extreme grid of 9,600
(rates from 1E10 to 1E-320 and near and below −1; growths past 2^1000 and below 2^−1000; values from 1E−5 to
9E307), where the readings part in kind the engine must refuse; where all give an Error Value it must give
one; where all give a number it must give its payment, computed in the spike's exact arithmetic by the
engine's rules (the exact payment rounded once, at the end and at the start; where its 256-bit fixed point reaches it, `pmt_exact.py` checks the rest), or refuse for a reason the
check names; and where it answers, no reading may lie more than 128 units in the last place from its payment
(the fourth round, `PmtReadings.Apart`). The fourth round added tiny numbers of periods, either sign, to the
extreme grid, which now holds 12,800 inputs. The last run (2026-10-12, the fourth round):

| | Survivors or readings | Agree | Part | Exceptions, listed |
|---|---:|---:|---:|---|
| FV, the financial power (g22) | 1 | 7,296 | 0 | none |
| FV, g22 beside g23 (the platform's ln and exp) | 2 | 7,296 | 0 | none |
| PV, the financial power (g22) | 1 | 7,296 | 0 | 384 refused at a rate of −1 with `nper` above 0 (PV-013) |
| XNPV, its two evaluations | 2 | 4,004 | 0 | 506 `#NUM!` at a rate below 0, which no evaluation models |
| PMT, its readings, over 20,096 inputs | 2,245 (the 22,118 candidates by form, way to the start, growth's class and precision), and LibreOffice's | 9,136 numbers, 4,966 Error Values | 5,994 in kind | 790 refused where a reading magnifies its roundings beyond 2^6 (180 by (|pv|·(1+g) + |fv|) / |pv·g + fv|, 46 by the exponent, 114 by p − p·r/(1+r), 371 by LibreOffice's divisor, 79 by its power of pv), 173 where a quantity lies outside [2^−1021, 2^1022] though the readings agree in kind; 740 numbers left to `pmt_exact.py` |

The engine gives the survivors' answer, and for PMT its payment, at every other input, and refuses PMT wherever
its readings part in kind. (PV's 14 survivors of its own doubles part at 614 inputs, at half a period and
negative fractions, where FV's settle PV's power; FV's 9 at 96, where g20 gives 0 to a negative fraction as
`#NUM!`.) In the third round PMT answered where its readings part beyond the last digits by cancellation, up to
21,345,331,117 units in the last place from the exact payment at `=PMT(-0.5,36.25,-150,0,0)`. The fourth round
refuses wherever a reading magnifies its own roundings more than 2^6 times (`ExactPayment.Conditioned`, the
branch's bound, put to the user and kept by decision with the user on 2026-10-13), and now, where PMT answers, the farthest reading lies 48 units in the last
place from its payment (`=PMT(-0.99,-10,0,-5000,1)`, LibreOffice's `ScGetPMT`). `--pmt-readings` runs the
readings at `pmt_exact.py`'s 20,000 inputs: the engine gives a number at 12,101 of them, and every reading
gives a number there; of the 11,588 that are not 0, the farthest reading lies within 8 units in the last place
at 11,312, and within 93 at all (at `=PMT(0.00371655630722511,-1100,28486236.273849,44.6175619860253,0)`, where
(|pv|·(1+g) + |fv|) / |pv·g + fv| is 60; `--pmt-readings` requires 128, twice the bound). `--pmt-show` prints
what the readings give at one input; at the third and fourth rounds' new cases:

| Case | Formula | The readings give |
|---|---|---|
| PMT-036 | `=PMT(-9,1/3,-1)` | `#NUM!`, every admitted one (none takes the odd root, as the power PV and FV share does, FV-017) |
| PMT-037 | `=PMT(-1,-10,1000)` | `#NUM!` (138) and 1000 (21) |
| PMT-039 | `=PMT(1,1020,1000)` | −1000 (244) and `#NUM!` (131) |
| PMT-042 | `=PMT(-0.99,160,1000,500)` | −495 (313), −494.99999999999994 (50) and `#NUM!` (12) |
| PMT-043 | `=PMT(1E-320,0.5,1000)` | −2000 (146), `#NUM!` (11) and 0 (2) |
| PMT-044 | `=PMT(-1.64435447903749E-307,283.5,1E-05,0)` | −3.5273368606701946E-08 (128) and −3.527336860668532E-08 (31) |
| PMT-045 | `=PMT(-0.5,360,1000,500)` | −250 (371) and `#NUM!` (4) |
| PMT-047 | `=PMT(0.1,1E-17,1000,0,1)` | −9.5382351702337E+19 (1,651), −9.538235170233698E+19 (489), −9.538235170233701E+19 (104), and `#NUM!` from LibreOffice's reading |
| PMT-048 | `=PMT(-0.999999,1E-12,1000,0,1)` | −7.238234126569758E+19 (1,283) and doubles a few units from it, and `#NUM!` from LibreOffice's reading |
| PMT-049 | `=PMT(0.1,2,1000,-1210)` | 0 (139), 1.691768418476429E−14 (75), −5.8154539385127246E−15 (60, the exact payment) and 20 other doubles from −1.98E−14 to 1.69E−14 |
| PMT-050 | `=PMT(0.1,1E-17,1000,-1000)` | 0 (216), −104.83672617280074 (63), −100 (60, the exact payment), −102.37961540312571 (15), −104, −96, −102.4, −88, −101.92459489022293 |
| PMT-051 | `=PMT(-0.5,36.25,-150,0,0)` | 9.17749001306952E−10 (200, the exact payment), 9.177492099722465E−10 (58), 9.177512083624606E−10 (24) and others |
| PMT-052 | `=PMT(1,1000,0,1000)` | −9.332636185032189E−299 (216, the exact payment), −9.332636185032829E−299 (142, 603 units away), −9.33263618503283E−299 (17) |
| PMT-053 | `=PMT(1000,2,1000,0,1)` | −999.0019960079841 (1,825, the exact payment), −999.0019960080972 (366, 995 units away) and doubles beside the first |

## The C# port

`--vectors` (`search.py`) writes the engine's evaluation at 400 generated inputs; a throwaway layer-1 test
compared them all (2026-10-12, Linux). `--refusals` is the check of PV, FV, XNPV and PMT now. The engine's
`CorrectlyRounded` computes e^x and ln x in double-double and in 256-bit fixed point near a rounding
boundary; 40,010 of its values were compared with Python's `decimal` at 120 digits, all equal, of which
the platform's `Math.Exp` and `Math.Log` differ at 18 (2026-10-12). PMT's payment is computed in the engine's
`Enclosure`s (the third round): interval arithmetic over exact dyadic numbers, each operation rounding its
bounds outward, with (1+rate)^nper − 1 raised from the rate itself, by (1+a)(1+b) − 1 = a + b + ab at a whole
`nper` and as e^x − 1 of x = nper·ln(1+rate) otherwise, and the double given only where both bounds round to it.
The previous round's double-double (`ExpWide`, `LogWide`, since removed) lost the rate below about 1e−15 and at
subnormal parts: over `pmt_exact.py`'s 20,000 inputs it was 1 or 2 units in the last place from the exact
payment at 874 of the 3,167 payments at a rate below 1e−15 and at 53 others. After the third round the engine
gave decimal's answer at all 20,000 drawn then. The fourth round's draw (seed 20261012, tiny numbers of periods
and the new refusals among them) gives 12,101 numbers, 3,285 of them at a rate below 1e−15, 623 `#NUM!` and
7,276 refused, and the engine gives decimal's answer by its rules at every one. At the start the earlier rule,
before the decision of 2026-10-13, was the payment at the end's double over 1+rate, rounded a second time: at 809 of the 4,132 payments at the start other than 0
(20%) it is a unit in the last place from the exact payment at the start rounded once, which `--check` counts.

## Files

- `search.py`, `results.json`: the first search, and its last `--json` run.
- `Search.cs`, `search-cs.json`: the second and third searches, and the third's last `--json` run.
- `discriminate.txt`: the second search's `--discriminate` run, the inputs PMT-020 to 033 and NPV-009 to 022
  asked.
- `discriminate-xnpv.txt`: the second search's `--discriminate-xnpv` run, the inputs XNPV-012 to 017 asked.
- `pmt_exact.py`: PMT against Python's decimal at 1200 digits over a dense random set (the third and fourth
  rounds).
- `mod-search.cs`, `mod-search.txt`: the same search for `MOD` (its header says what it found after
  each area pass and after the review of the second, which added the widths no hardware holds), and its
  last `--discriminate` run, every candidate with its match count.
