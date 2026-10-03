# Financial solver admission experiment

IRR, XIRR and RATE remain undeclared. None of these generic candidates reproduces the
143 recorded Windows Excel cases at the catalogue's 15-significant-digit criterion.
This is a failed acceptance experiment, not an implementation of Excel's iteration.

Run from any directory with Python 3 (standard library only):

```sh
python3 spikes/exsheet-financial-solvers/compare.py
python3 spikes/exsheet-financial-solvers/compare.py --all
```

The script reads the immutable October 3 observation inputs and results. Expected numbers
come from `roundTrip`, checked against `bits`. Equality rounds the exact binary64 value to
15 significant digits, half away from zero; Error Values must match exactly. `results.json`
records the complete run and its Python/platform metadata. Last-bit results may vary with
Python's math library; a successful experiment would still need to validate a C# port.

The three candidates use the standard cash-flow equations and the observed input coercions:

- Newton, with an analytic derivative, starting at the given/default guess; at most 20
  steps for IRR/RATE or 100 for XIRR; absolute step tolerance 1e-7 or 1e-8 respectively.
- Secant, initialized at the guess and guess + 0.0001, with the same caps/tolerances.
- Newton with 100 steps and absolute step tolerance 1e-14, to check whether simply
  computing a more accurate mathematical root remedies the mismatch.

All refuse a candidate step at or below -1. There is no bracketing fallback, no per-case
root selection, and no table of expected answers. The tolerance and step-count choices
are candidate parameters; the observations do not establish Excel's recurrence or its
stopping test. All 143 cases were exercised. The simple input reader handles the recorded
financial calls; it is not an Excel expression parser.

| Candidate | IRR mismatches / 60 | XIRR mismatches / 67 | RATE mismatches / 16 |
|---|---:|---:|---:|
| Newton | 24 | 32 | 8 |
| Secant | 22 | 31 | 8 |
| Newton, tighter tolerance | 25 | 32 | 8 |

Representative Newton mismatches:

| Case | Excel's recorded result | Candidate result | Gap |
|---|---|---|---|
| IRR-018 | 0.20000000000000306 | 0.10000000000000138 | Different root for guess 2 |
| IRR-055 | -0.76550207031155 | #NUM! | Refuses a case Excel solves |
| XIRR-001 | 0.0997135818004608 | 0.099713585934141216 | Different stopping result |
| XIRR-013 | #NUM! | 0.10339792770065954 | Solves a case Excel refuses |
| RATE-001 | 0.02922854076913774 | 0.029228540769158224 | Different at 15 significant digits |
| RATE-009 | 8.4459342326353958E-14 | -9.8193843999238653E-17 | A mathematically near-zero answer is not Excel's answer |

A solver still needs a defensible recurrence, initialization, root-selection and stopping
policy that reproduces both numbers and failures. These experiments neither establish
that policy nor prove that no such algorithm exists. Looser comparisons or recorded-case
lookups would conceal the gap and are not admission strategies (ADR-0047).
