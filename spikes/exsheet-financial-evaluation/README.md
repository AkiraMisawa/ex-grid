# The evaluation Excel uses for PMT, PV, FV, NPV and XNPV

The seventeenth Windows run (`verification/2026-10-11-windows-17/`) read Excel's double for every
financial case of the corpus, and the area pass of 2026-10-12 (`verification/2026-10-12-windows-area/`)
read the cases that run left `uncertain`: PMT at small rates, a fractional `type`, a fractional and a
negative `nper`, and NPV's documented and open cases. ADR-0047 admits a function only where it answers
as Excel, and ADR-0202 corrects or refuses a disagreement before the tag. This experiment searches for
the evaluation that gives Excel's double, bit for bit, on **every** observed double, the agreeing cases
included, so that a correction breaks none. Nothing here ships.

Two searches, each run over every `results-*.json` under `ExcelOracle/`:

```sh
# The first, in Python, exact arithmetic (fractions.Fraction) rounded at 53 or 64 bits after each step
python3 -B spikes/exsheet-financial-evaluation/search.py                  # the table (about 3 minutes)
python3 -B spikes/exsheet-financial-evaluation/search.py --detail         # every candidate, with its distance in units in the last place
python3 -B spikes/exsheet-financial-evaluation/search.py --json           # rewrites results.json
python3 -B spikes/exsheet-financial-evaluation/search.py --vectors FILE   # the engine's PV, FV and PMT at 400 generated inputs
python3 -B spikes/exsheet-financial-evaluation/search.py --spread "=PMT(1/10000000,12,1000)"   # the doubles PMT's survivors give for one formula

# The second, in C# under .NET, where Math.Pow, Math.Exp and Math.Log are the engine's own (2026-10-12)
nix develop -c dotnet run spikes/exsheet-financial-evaluation/Search.cs                 # the table (seconds)
nix develop -c dotnet run spikes/exsheet-financial-evaluation/Search.cs -- --json       # rewrites search-cs.json
nix develop -c dotnet run spikes/exsheet-financial-evaluation/Search.cs -- --discriminate   # the inputs that tell the nearest apart (discriminate.txt)
nix develop -c dotnet run spikes/exsheet-financial-evaluation/Search.cs -- --predict "=FV(0.05/12,12,-150,-1000,1)"
nix develop -c dotnet run spikes/exsheet-financial-evaluation/Search.cs -- --refusals   # the engine against where the survivors part (exits 1 if not)
nix develop -c dotnet run spikes/exsheet-financial-evaluation/Search.cs -- --discriminate-xnpv   # the inputs that tell XNPV's survivors apart (discriminate-xnpv.txt)
```

`Search.cs` references `src/ExSheet.Engine` (`#:project`) for `--refusals`, which enters each input as a
Formula in a `Sheet` through the engine's public surface; everything else in it is the standard library.

`search.py` reads each case's cells from the corpus; Excel's arguments are its own doubles (`0.06/12` and
`8%/12` are divided in doubles, as Excel's operators do). Its logarithms and exponentials come from
`decimal` at 80 digits, which models a correctly rounded library function. `Search.cs` computes in native
doubles where a candidate is in doubles, so `Math.*` is the platform's (glibc's here, as Python's `math`
is), and in an exact dyadic number rounded at 64 bits where a candidate models an x87 register; its
correctly rounded `ln`, `exp`, `log1p`, `expm1`, FYL2XP1 and F2XM1 are computed in 256-bit fixed point.

## The observations

41 doubles (the seventeenth run's 16, each read twice the same, and the area pass's): FV-001 to 003, 005
to 007, 009 and 010; PV-001 and 003 to 007; PMT-001 to 003 and 008 to 018; NPV-001 to 003 and 005 to 008;
XNPV-001 and 004. A rate of 0 raises no power and is no search case (FV-004, FV-008, PV-002, PMT-004).
NPV's seven are three distinct numbers: NPV-001, 007 and 008 are one (the same values typed, in a range,
and with the rate in a cell), NPV-005 and 006 another (typed or in a range), NPV-002 is NPV-006's less
40000 exactly, and NPV-003 the third.

## What was tried, and how many observed doubles each reproduces

| Function | Observed doubles (rate ≠ 0) | `search.py` candidates | `Search.cs` candidates | Reproduce all | Best other |
|---|---:|---:|---:|---|---|
| FV | 8 | 1022 | 440 | **three evaluations** (below), each with every reading of a fractional `nper` | 7 of 8 |
| PV | 6 | 1022 | 440 | **one evaluation** (below), with every reading of a fractional `nper` | 5 of 6 |
| PMT | 14 | 11242 | 7344 | **none** | 10 of 14 |
| NPV | 7 | 301, then 456 | 3876 | **none** | 6 of 7 |
| XNPV | 2 | 12 | 1496 | 2 in `search.py`, **72** in `Search.cs` (below) | 1 of 2 |

*(The first search, 2026-10-12 before the area pass, had 3 FV, 2 PV, 4 PMT and 3 NPV doubles; its
conclusions that the area pass changed are corrected below, each where it was. Later the same day
`Search.cs` gained four readings of a fractional or negative `nper` for PV and FV, and a search of
XNPV; its counts above are that run's.)*

`search.py`'s candidates: a way of raising 1+rate to `nper` (a correctly rounded power; squaring from
the lowest or the highest bit; repeated multiplication; e^(n ln(1+rate)); e^(n log1p(rate)); the x87's
FYL2X, F2XM1 and FSCALE; the C library's `pow`; e^(n log1p(rate)) beside expm1(n log1p(rate)); 21 ways of
carrying (1+rate)^n − 1 without forming 1+rate; and the PV/FV model below), a precision for it and for the
rest, and an algebraic form: 7 for FV, 7 for PV, 20 for PMT, and for PMT how a payment at the start is
reached from the one at the end. A method that raises only to a whole number from 0 does not match a
fractional or negative `nper`. NPV: 9 schemes, then 16 ways of discounting × 6 orders of summing × both
precisions.

`Search.cs` adds what `search.py` did not try (`search-cs.json` holds every candidate with its match
count, and the legend for its short labels):

- **(1+rate)^n − 1 carried accurately, any `nper`**: expm1(n log1p(rate)) correctly rounded; Goldberg's
  log1p and Kahan's expm1 from the platform's `Math.Log` and `Math.Exp`; mixed (g by squaring, e by
  expm1); the x87's FYL2XP1 and F2XM1 at 64 bits (unsplit below 1, split by FRNDINT and FSCALE, e as g − 1
  or from the parts); squaring in a 64-bit register; the exact power.
- **Forms**: 10 for FV and 10 for PV (the groupings `search.py` did not try, and fused multiply-adds), 34
  for PMT (fused multiply-adds, 1/g formed once); six ways to the payment at the start.
- **NPV**: 1+rate as a double, at 64 bits or exact × 16 ways of discounting each value (running
  products, quotients and factors; b^i by squaring, correctly rounded, by `Math.Pow`, by
  `Math.Exp(i Math.Log(b))`, correctly rounded e^(i ln b), the x87's FYL2X/F2XM1/FSCALE; dividing or
  multiplying by a reciprocal) × the discount, each term and the sum each at 53 or 64 bits (an x87
  register stored to a double at each step among them) × 11 orders of summing (forward, from the last,
  pairwise, Kahan, Neumaier, positives and negatives apart either way, by magnitude, exact, forward with
  Excel's last-addition adjustment, forward fused); and Horner's rule from either end, plain or fused, at
  53 or 64 bits.

### FV and PV: the doubles' evaluation, any `nper`, the payment grouped with its factor

These evaluations give all 8 FV and all 6 PV doubles, in both searches:

- **1+rate raised to `nper`** by squaring from the exponent's lowest bit when `nper` is a whole number,
  1 over that when it is a negative whole number, and e^(nper ln(1+rate)) by the platform's `exp` and
  `log` otherwise, each step a double. Squaring 1/(1+rate) for FV-007's −10 is 19 units in the last place
  away; a correctly rounded power is two units from PV-005's 10.5; `Math.Pow` for both misses both.
  *(2026-10-12, later: no observed double has an `nper` of 0.5, nor a negative one that is not a whole
  number, so the observations leave more than one reading of those. `search.py`'s `g_pv_fv` is the `^`
  operator's evaluation, `Arithmetic.Power`, the correctly rounded square root at exactly 0.5 (ARITH-133)
  and e^(nper ln(1+rate)) otherwise; `Search.cs`'s g0 is e^(nper ln(1+rate)) at 0.5 too; and g19 and g20
  raise a negative fraction as 1 over the power to its absolute value, as FV-007's −10 is reached, by
  either. g18 is the `^` operator's with its errors where 1+rate is 0: =0^0 is `#NUM!` (ARITH-041) and 0 to
  a negative power `#DIV/0!` (ARITH-042), where squaring gives 0^0 as 1 and 1 over 0^n no finite number.
  g21 is g0 with a correctly rounded `ln` and `exp`, as `search.py` models a library; it parts from g0 at
  no input `--refusals` draws, so the platform's library makes no difference there.
  So `=(1+r)^n` and the same power inside FV can differ, at a rate of −1 and wherever FV's reading is not
  the operator's. All five give every observed double, and they part at 0.5 (FV-016), at some negative
  fractions (FV-022), and at a rate of −1 with a negative `nper` (FV-020, FV-021, PV-012): the engine
  computes each and refuses with `#VALUE!` where they part. At a rate below −1 it refuses a fractional
  `nper` as before: where 1/nper is an odd whole number, `^` gives a negative base an odd root and
  e^(nper ln(1+rate)) none; elsewhere neither has one.)*
- **FV = −(pv·g + pmt·((1+rate·type)·((g−1)/rate)))** and **PV = −(fv + pmt·((1+rate·type)·((g−1)/rate)))/g**:
  the payment multiplies the product of 1+rate·type and the annuity factor. Multiplying the payment by
  1+rate·type first, as the engine did, is one unit in the last place below Excel's FV-009 and PV-007,
  as this README said those two would tell.
- `type` is a truth value: 2, 0.5 and −1 all give the payment at the start (PMT-008 to 010), so 0.5 is
  not truncated.

`search.py` finds it as F4 (and F7, the same products in another order); `Search.cs` as F4, F7 and F8,
which are the same products, and also F9 and F10, which fuse pv·g, or the payment's product, into a
multiply-add. PV's V5 and V9 are one evaluation; its fused V10 misses. No observed double tells F4 from F9
and F10, which part where FV has both a present value and a payment: the engine refuses those with
`#VALUE!` where they part (FV-014 and FV-015 ask; a fused multiply-add gives 2900.6645063063966 and
123950.02507801352 for them), but at the end of each period with a whole `nper` from 0, where FV gave F4's
double before the area pass and still does until Excel is asked (FV-019, where F9 gives 483.06382918288557
and F10 483.0638291828854).

### PMT: no evaluation gives every observed payment

Excel's PMT-001 to 003, 008 to 010 and 017 are the exact payment over Excel's doubles rounded once (at
the start, that double divided by 1+rate unrounded and rounded once): the engine's model. PMT-013 to 016
and 018 are one to three units in the last place from it, and PMT-011 and 012 (`nper` 10.5 and −10) one
unit each.

*(Corrected 2026-10-12, after the area pass: this section said that the 304 evaluations in an x87's 64
bits agree with the exact model at the rates observed. They do, and Excel does not: at PMT-013 to 015
all 304 give the engine's double, which Excel's is not.)* No candidate of either search gives all 14:

- **None gives PMT-013**, `=PMT(0.05/12,36,250000,-5000)`, Excel's −7363.7031239763755, 2.5 units in
  the last place from the exact payment, in either search (the spike's excess forms give it, 456 of
  11242, but miss others).
- The nearest, 10 of 14 in both, carry e = expm1(n log1p(rate)) correctly rounded and the rest in
  doubles (form M5 or M16), and miss PMT-012, 013 and 016 by two units and PMT-017 by one.
- PMT-017, at 10^-7, rules out every evaluation in doubles that forms 1+rate and subtracts 1 from its
  power: it is the exact payment rounded once, and those are thousands of units away.

So the last digits of PMT are open. The engine keeps the exact model; PMT-011 and 012 are refused by
decision (ADR-0047, note of 2026-10-12); PMT-013 to 016 and 018 stay `uncertain` with Excel's double in
their description, for the user (ADR-0202). PMT-019 to 033 ask the next pass: PMT-020 to 033 are
`--discriminate`'s inputs, which split the 92 candidates nearest Excel's payments (9 or more of 14, and
each family's four best) into 45 groups; `discriminate.txt` lists which candidates give what.

### NPV: no evaluation gives all three

Excel's three NPV numbers: NPV-001 (`=NPV(10%,-10000,3000,4200,6800)`, 1188.4434123352207), NPV-005
(`=NPV(0.08,8000,9200,10000,12000,14500)`, 41922.06155493237) and NPV-003 (`=NPV(0.1,A1:A7)`, 1, 2 and
4, 5.56724267468069).

- NPV-001 needs 1+rate rounded to a double: the exact 1+rate is 9.5 units in the last place away, and
  1.1's own rounding explains 8 of them. Given that base, NPV-001 lies 1.5 units below the exact sum, a
  rounding of the forward sum's second partial sum (−6611.57), so it is given only by sums in doubles
  taken forward.
- NPV-005 lies 0.9 of a unit above the exact sum with 1+rate rounded. Every evaluation that gives
  NPV-001 by summing discounted values misses it; the exact sum, or one in 64 bits, or one from the
  last value, gives it.
- The nearest: the running product summed forward in doubles (`d *= 1+rate; total += value/d`) gives
  NPV-001 and NPV-003 and is one unit from NPV-005; Horner's rule from the first value with a fused
  multiply-add, then divided by the running product, gives NPV-001 and NPV-005 and is one unit from
  NPV-003.
- The area pass ruled out the split by the kind of argument that the first search had noticed: Excel
  answers NPV-001's values in a range (NPV-007) as typed, and NPV-005's typed values as in a range.

NPV is refused for 0.1.0, as IRR is (ADR-0202, note of 2026-10-12). NPV-009 to 022 are recorded cases,
`--discriminate`'s inputs for the 189 nearest candidates (37 groups), so that the next pass reads
Excel's doubles where they differ.

### XNPV

XNPV-001 and XNPV-004 are both given by e^(x ln(1+rate)) in doubles summed forward, with x the days over
365 (divided, or multiplied by 1/365). A correctly rounded power, and `Math.Pow`, so summed are 8 units in
the last place from XNPV-004, which was asked to tell them apart; the corpus compared XNPV at 6 decimals,
so the oracle counted it as agreeing. The engine now takes e^(x ln(1+rate)), and XNPV-001 and 004 compare
every bit. At a rate of −1 or below 1+rate has no logarithm, and `Math.Pow`, which the engine took before,
answers some arguments there (the first value, and whole years below −1): the engine refuses that rate
with `#VALUE!`, and XNPV-007 to 010 ask.

*(2026-10-12, later.)* `search.py` tried 12 evaluations. `Search.cs` tries 1,496: the days over 365 or
times 1/365; the discount by `Math.Exp(x*Math.Log(b))`, at whole years by squaring or by `Math.Pow`,
`Math.Pow` throughout, a correctly rounded power, e^(x ln b) correctly rounded, the x87's, or 1 over
e^(−x ln b); each value divided by it, multiplied by its reciprocal, or by e^(−x ln b); NPV's orders of
summing and pairwise by halving; terms and sum at 53 or 64 bits. **72 give both observed doubles.** The
engine computes six of them, the days over 365 or times 1/365 with e^(x ln b), or at whole years squaring
or `Math.Pow`, each value divided and summed forward, and refuses where those six part: whole years
(XNPV-005, 006) and the days at which the two years part (XNPV-011). The other 66 part from them at about
half the inputs `--refusals` draws, so XNPV's evaluation is open as PMT's is, and whether XNPV answers
there is the user's question (ADR-0202). `--discriminate-xnpv` finds six inputs that split the 72 into 38
groups, each of evaluations that agree on every input drawn (forward, forward fused and forward with
Excel's last addition, for instance); XNPV-012 to 017 ask them. A correctly rounded e^(x ln b) parts from
the platform's `Math.Exp` and `Math.Log` at one of them, so the platform's library is among the questions.

## The refusals (`--refusals`)

ADR-0047 admits an answer only where it follows from Excel's observed answers, and where the
evaluations those leave part, the engine refuses with `#VALUE!`. `--refusals` runs each survivor of the
search over a grid, and the engine through its public `Sheet`, and requires the engine's answer where they
agree and `#VALUE!` where they part. FV and PV: 12 rates (−9, −1.5, −1 and −0.5 among them), 19 numbers of
periods (whole, negative, 0.5, −0.5, 10.5, −10.5, 1/3 and others), four payments, four present or future
values and both types, 7,296 inputs each. XNPV: the corpus's inputs and 4,000 drawn (up to seven values,
dates whole years apart, the days at which /365 and *(1/365) part, and any). The last run (2026-10-12):

| | Survivors | Agree | Part | Exceptions, listed and asked |
|---|---:|---:|---:|---|
| FV | 25 | 5,792 | 1,504 | 144 answered at the end with a whole `nper` from 0 (FV-019); 32 answered at a rate of −1 with no periods (FV-018); 512 refused at a rate below −1 with a fractional `nper` though the survivors agree |
| PV | 10 | 6,682 | 614 | 32 answered at a rate of −1 with no periods; 512 refused below −1 as FV's |
| XNPV, the six | 6 | 3,749 | 255 | 604 refused at a rate of −1 or below though they agree |
| XNPV, all 72 | 72 | 2,081 | 1,923 | the six's 604; 1,668 answered where the 72 part, for the user (ADR-0202) |

The exceptions are answers the engine gave before the area pass, kept until Excel is asked, and refusals
kept from before it; every other input matches. The check exits 1 if any other input differs.

## The C# port

`--vectors` writes the engine's evaluation at 400 generated inputs: rates typed as `k/d/100`, `nper`
from 1 to 1000 and also 0.5, 10.5, 36.25, −1, −12 and −10.5, types 0, 1, 2, 0.5 and −1, negative rates
among them; FV and PV by the evaluation above, PMT by the exact model (or `#VALUE!` where it refuses an
`nper`), each PMT at least 0.0007 of a unit in the last place from a rounding boundary. A throwaway
layer-1 test entered each as a Formula and compared the engine's double: all 1200 equal (2026-10-12,
Linux). *(Later that day the engine began refusing PV and FV where their readings part, which `--vectors`
does not model; `--refusals` is the check of PV and FV now.)* PMT's port carries double-double numbers (about 106 bits), which gives the exact model's double
except very near a rounding boundary.

## Files

- `search.py`, `results.json`: the first search, and its last `--json` run.
- `Search.cs`, `search-cs.json`: the second, and its last `--json` run (every candidate's match count,
  short labels with their legend, each function's 40 best that do not reproduce all).
- `discriminate.txt`: the last `--discriminate` run, the inputs PMT-020 to 033 and NPV-009 to 022 ask.
- `discriminate-xnpv.txt`: the last `--discriminate-xnpv` run, the inputs XNPV-012 to 017 ask.
- `mod-search.cs`, `mod-search.txt`: the same search for `MOD` (its header says what it found after
  each area pass and after the review of the second, which added the widths no hardware holds), and its
  last `--discriminate` run, every candidate with its match count.
