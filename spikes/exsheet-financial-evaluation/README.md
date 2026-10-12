# The evaluation Excel uses for PMT, PV, FV, NPV and XNPV

The seventeenth Windows run (`verification/2026-10-11-windows-17/`) read Excel's double for every
financial case of the corpus. Nine disagreed with the engine at the 13th to 15th significant digit
(FV-001, FV-003, NPV-001, NPV-002, PMT-001 to 003, PV-001, PV-003) and one was refused by the engine
(PMT-008). ADR-0047 admits a function only where it answers as Excel, and ADR-0202 corrects or refuses
a disagreement before the tag. This experiment searches for the evaluation that gives Excel's double,
bit for bit, on **every** observed double, the agreeing cases included, so that a correction breaks
none. Nothing here ships.

```sh
python3 -B spikes/exsheet-financial-evaluation/search.py                  # the table (about 40 s)
python3 -B spikes/exsheet-financial-evaluation/search.py --detail         # every candidate, with its distance in units in the last place
python3 -B spikes/exsheet-financial-evaluation/search.py --json           # rewrites results.json
python3 -B spikes/exsheet-financial-evaluation/search.py --discriminate   # inputs that tell the survivors apart (several minutes)
python3 -B spikes/exsheet-financial-evaluation/search.py --vectors FILE   # the engine's evaluation at 400 generated inputs
```

Standard library only. The script reads every `results-*.json` under `ExcelOracle/` (only the
seventeenth run holds financial cases: 16 numbers, each read twice, the same each time) and the
corpus for each case's cells. Arithmetic is exact (`fractions.Fraction`) and rounded to nearest-even
at a chosen significand width after every operation: 53 bits is a double, 64 bits an x87 register.
Logarithms and exponentials come from `decimal` at 80 digits and are then rounded, which models a
correctly rounded library function. The arguments are Excel's own doubles: `0.06/12` and `8%/12` are
divided in doubles, as Excel's operators do.

## What was tried, and how many observed doubles each reproduces

A candidate is a way of raising 1+rate to nper, a precision for that and for the rest, and an
algebraic form. Raising: a correctly rounded power; squaring from the lowest bit; squaring from the
highest bit; repeated multiplication; e^(n ln(1+rate)); e^(n log1p(rate)); the x87's FYL2X, F2XM1 and
FSCALE; the C library's `pow`; e^(n log1p(rate)) beside expm1(n log1p(rate)); and 21 ways of carrying
(1+rate)^n − 1 without forming 1+rate (the excess forms (1+a)(1+b)−1 = a+b+ab, by squaring from either
end or by repeated multiplication, with each order of the sums). Forms: 7 for FV, 7 for PV, 20 for
PMT, each order of operations Microsoft's identity allows, plus, for PMT, the payment at the start
computed as the payment at the end, stored to a double, divided by 1+rate in a double, in an x87
register, or exactly. NPV: 9 schemes, then 16 ways of discounting each value × 6 orders of summing ×
both precisions.

| Function | Observed doubles (rate ≠ 0) | Candidates | Reproduce all | Next best |
|---|---:|---:|---:|---|
| FV | 3 (FV-001 to 003) | 1008 | 4 | 2 of 3 |
| PV | 2 (PV-001, PV-003) | 1008 | 6 | none of 2 |
| PMT | 4 (PMT-001 to 003, 008) | 11088 | 2504 | 3 of 4 |
| NPV | 3 (NPV-001 to 003) | 297, then 456 | **0** | 2 of 3 |
| XNPV | 1 (XNPV-001) | 12 | 6 | none of 1 |

The rate-0 cases (FV-004, PV-002, PMT-004) are no search case: no power is raised.

### FV and PV: the doubles' evaluation, 1+rate raised by squaring from the lowest bit

Every candidate that reproduces FV-001 to 003 and PV-001 and PV-003 raises 1+rate (a double) by
squaring from the exponent's lowest bit, every product a double, and evaluates the rest in doubles:

- FV = −(pv·g + pmt·(1+rate·type)·((g−1)/rate)) — or with `(1+rate·type)·((g−1)/rate)` multiplied
  first, which differs only at the start of each period (FV-009 tells the two apart);
- PV = −(fv + pmt·(1+rate·type)·((g−1)/rate))/g — or one of four other orders (PV-006 and PV-007).

The evidence is strong: Excel's FV-001 lies 39 units in the last place from the correctly rounded
power's evaluation and 20 from repeated multiplication's, and squaring gives it to the bit, with
FV-003, PV-001 and PV-003 likewise. Nothing evaluated at 64 bits comes near.

### PMT: carried beyond a double, rounded once; at the start, divided by 1+rate unrounded

No evaluation that forms 1+rate as a double comes within 36 units in the last place of PMT-001: 1+rate
loses the rate's low bits. Excel's PMT-001 and 003 are the exact payment over Excel's doubles,
correctly rounded. PMT-002 and 008 (at the start) are the exactly-rounded payment at the end divided
by 1+rate, carried unrounded, and rounded once. PMT-008 decides this: the exact payment at the
start, rounded once, is one unit in the last place from Excel's, and so is the payment at the end
divided by the double 1.1.

The 2504 survivors are of two kinds:

- **348 compute the payment at the end accurately** — at 64 bits, by any way of raising and in any of
  the 19 forms (304), or in doubles from correctly rounded log1p and expm1 (44) — store it to a
  double, and divide by 1+rate at 64 bits or exactly. That the 304 hold whatever the form or the way
  of raising is why the engine takes this kind: it does not rest on a choice the observations cannot
  make.
- 2156 carry (1+rate)^n − 1 by an excess form; of those, 100 stay in doubles throughout (squaring
  from the highest bit, 2+a or a+a for the square). They are a narrow fit to four doubles.

PMT-013 to 016 tell the two kinds apart (and the excess forms among themselves); the engine's answer
is the first kind's in each.

### NPV: no single evaluation reproduces all three

NPV-001 (`=NPV(10%,-10000,3000,4200,6800)`, 9 units in the last place below the exact value) is
reproduced only by the running product in doubles, `d *= 1+rate; total += value/d`, summed forward.
That evaluation gives NPV-002 (`=NPV(A1,A3:A7)+A2`) one unit in the last place of 41922 too low:
Excel's NPV-002 is the exact value correctly rounded, which the evaluations at 64 bits, or summing
from the last, give, and NPV-001 rules those out. A split by argument kind (scalars as the running
product, a range otherwise) would fit all three, but it is two choices fitted to three numbers, and
nothing else suggests it. The engine keeps its NPV; NPV-005 to 008 ask the questions that would
decide it (the same values as arguments or as a range, the rate from a cell, NPV-002 without the
`+A2`).

### XNPV

XNPV-001 is reproduced by the C library's `pow`, a correctly rounded power, and e^(x ln(1+rate)), each
in doubles: the engine's `Math.Pow` among them. One observation does not tell them apart.

## The C# port

`--vectors` writes the engine's evaluation (FV and PV in doubles as above; PMT exact, then rounded
once) at 400 generated inputs: rates typed as `k/d/100`, 1 to 1000 periods, types 0, 1 and 2,
negative rates among them, each PMT at least 0.0007 of a unit in the last place from a rounding
boundary. A throwaway layer-1 test entered each as a Formula and compared the engine's double:
all 1200 equal (2026-10-12). PMT's port carries double-double numbers (about 106 bits), so it gives
the 64-bit evaluation's double except within about 2^-60 of a boundary, where which one Excel's
register gives cannot be known from outside anyway.

`results.json` is the last `--json` run: each function's match counts, the candidates that reproduce
every observed double (grouped by evaluation for PMT), and the 40 best that do not, with their
distance from each observed double in units in the last place.
