# How Excel reduces an angle: SIN, COS, TAN, COT, CSC and SEC

The seventeenth Windows run (2026-10-11) asked Excel `SIN(PI())`, `COS(PI()/2)` and `TAN(PI()/2)`, which the
corpus held as the engine's answers (SIN-004, COS-004, TAN-004). Excel answered **1.22514845490862E-16**,
**6.1257422745431E-17** and **16324552277619072**, where the double `PI()` gives has the sine
1.2246467991473532E-16. This experiment finds the computation that gives Excel's doubles, and is the
evidence behind `Functions.Trigonometry.cs` (ADR-0047).

Run from any directory with Python 3, standard library only:

```sh
python3 spikes/exsheet-trig-reduction/compare.py            # the tallies below; 20000 seeded arguments
python3 spikes/exsheet-trig-reduction/compare.py 100000     # more seeded arguments
python3 spikes/exsheet-trig-reduction/compare.py --known    # the known answers tests/cases/sheet/trigonometry-reduction.json pins
```

`results.txt` is the output of the first, on the platform it names.

## What Excel does

**Excel reduces an angle by pi rounded to a 64-bit significand**, the x87's `FLDPI`:
`0xC90FDAA22168C235 × 2^-62`, pi + 5.0166E-20. `PI()`'s double is that number less exactly
`565 × 2^-62` = 1.22514845490862E-16, which is Excel's `SIN(PI())` to the last bit. With
`k` the nearest whole number of the angle over half that pi and `r` the exact remainder, Excel's

- **SIN and COS** are the doubles nearest sin(r) and cos(r), by the quadrant `k` names;
- **TAN** is consistent with the double nearest sin/cos of the reduced angle (and with two other
  constructions; below);
- **CSC** is 1/SIN divided as doubles: the double nearest 1/sin is one unit off CSC-001;
- **COT** and **SEC** are consistent with 1/TAN and 1/COS divided as doubles (and with others; below).

The reduction is exact: the remainder of a double below 2^27 by that pi/2 needs at most 63 bits. So the
answer does not depend on how `k` is rounded at a quadrant's edge, nor on any platform's `sin`.

## The candidates, against every number Excel returned for the six (19, all runs' results files)

Every candidate is evaluated in exact arithmetic (Python's `Fraction`, and series to 120 digits), and
rounded once to a double, except those that say "libm" (glibc's here, as .NET's `Math.Sin` on Linux).

| Candidate | Reproduces |
|---|---:|
| libm of the angle (the engine before) | 15 / 19 |
| The true pi, correctly rounded | 14 / 19 |
| x87 pi, exact reduction, every function correctly rounded | 18 / 19 (CSC-001) |
| **x87 pi, exact reduction, SIN/COS/TAN correctly rounded, COT/CSC/SEC 1 over them (the engine now)** | **19 / 19** |
| x87 pi, exact reduction, SIN/COS as doubles, the rest from them in double arithmetic | 19 / 19 |
| x87 pi, exact reduction, TAN from tan(r) as a double, −1 over it in odd quadrants | 19 / 19 |
| x87 pi, exact reduction, the remainder rounded to a double, libm | 17 / 19 (COS-001, CSC-001) |
| x87 pi, exact reduction, the remainder as a double-double, libm | 17 / 19 (COS-001, CSC-001) |
| x87 pi, each reduction step rounded to 64 bits, correctly rounded | 17 / 19 (SIN-006, CSC-001) |
| x87 pi, each reduction step rounded to 64 bits, SIN/COS doubles | 18 / 19 (SIN-006) |
| FSIN's own 66-bit pi (Intel's reduction), correctly rounded | 14 / 19 |
| `PI()`'s double, exact reduction, correctly rounded | 8 / 19 |
| `PI()`'s double, double arithmetic, libm | 10 / 19 |

The engine's computation (a double-double remainder, sine and cosine by series in double-double, then
one rounding) was ported line for line to Python and agrees with the exact one on the 19 and on 20000
seeded arguments for every function. A scratch run of the C# engine against the exact values on 15000
random formulas (`=SIN(…)` with 15-digit arguments up to 2^27) found no difference.

## What the observations do not settle

Three ways to build TAN, four to build COT, and two to build SEC reproduce every observation. They part
here, and the corpus now asks each (`uncertain` cases, the engine's answer):

| Case | Formula | The engine (TAN correctly rounded; COT, CSC, SEC 1 over) | The others |
|---|---|---|---|
| TAN-006 | `=TAN(1)` | 1.5574077246549023 | 1.557407724654902 (SIN/COS doubles divided; tan(r) then −1 over it) |
| TAN-007 | `=TAN(9)` | -0.45231565944180985 | -0.4523156594418099 (SIN/COS doubles divided) |
| COT-006 | `=COT(1)` | 0.6420926159343306 | 0.6420926159343308 (cot rounded once; 1/tan(r); COS/SIN doubles) |
| COT-007 | `=COT(4)` | 0.8636911544506167 | 0.8636911544506166 (cot rounded once) |
| COT-008 | `=COT(9)` | -2.210845410999195 | -2.2108454109991946 (COS/SIN doubles) |
| SEC-005 | `=SEC(1)` | 1.8508157176809255 | 1.8508157176809257 (sec rounded once) |

`SIN(2*PI())` (SIN-008) and `COS(100000000)` (COS-006) are asked as well, to see the reduction at
another quadrant and at a large `k`.
