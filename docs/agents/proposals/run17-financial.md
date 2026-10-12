# Run 17, financial area: what the orchestrator applies, and the one question

## The question for the user (ADR-0202: correct, or refuse the arguments, before the tag)

*(Written 2026-10-12 after review; it gathers section 4's NPV question and the PMT finding below into one.)*

Two of the financial answers rest on a choice the seventeenth run's observations do not make:

- **NPV disagrees.** No evaluation the search tried gives all three observed doubles. The engine's NPV differs from
  Excel's by 4 units in the last place on NPV-001 and 32 on NPV-002, in ordinary cash flows (section 4).
- **PMT at small rates is unobserved, and the candidates part there.** The engine takes the exact payment rounded once.
  It gives all four observed payments (rates of 0.5% to 10% a period), and so do 2504 of the evaluations the search
  tried. The 304 of those in an x87's 64 bits part from it as the rate shrinks: one unit in the last place at PMT-016, seven at a daily rate over a
  year (PMT-018, `=PMT(0.0365/365,365,10000)`), 3,390 from the 13th significant digit at a rate of 10^-7 (PMT-017,
  `=PMT(1/10000000,12,1000)`). No observation disagrees with the engine, and none tells which candidate Excel is.

ADR-0202: "A function that disagrees is corrected before the tag, or the arguments where it disagrees are refused with
an Error Value." Which of these?

1. **Ask Excel first** (recommended). `oracle.ps1 -Area pmt,npv` asks NPV-005 to 008 and PMT-013 to 018 through COM, no
   keys, in minutes. The spike reruns on every observed double (`search.py`, `--spread`). NPV follows if one evaluation
   gives all its doubles, and PMT's choice is kept or corrected on what Excel answers at small rates. Whatever the run
   leaves unsettled comes back as option 2 or 3, before the tag.
2. **Refuse now, until a run settles each.** `NPV` at a rate other than 0 is `#VALUE!` (a rate of 0 sums the values, and
   a sum is Excel's). `PMT` at a rate below the smallest observed, say a magnitude under 0.001 a period, is `#VALUE!`.
   Nothing is quietly wrong, but a common function steps back, and the PMT bound is a line drawn between observations.
3. **Refuse NPV, keep PMT.** NPV disagrees, so ADR-0202 requires option 1 or 2 for it. PMT has no observed
   disagreement. Its exact model is the payment correctly rounded, and it shows the same to the cent and well beyond.
   PMT-016 to 018 record the open question.
4. **For NPV only: take the split by argument kind, or undeclare `NPV`** (`#NAME?`, as `IRR` is). See section 4.

**Recommendation:** 1. If no run can be made before the tag, then 3. NPV is a known disagreement and ADR-0202 settles it
by refusal. PMT is not one, and refusing small rates would refuse an answer no run has shown to be wrong. The engine
does not change until the user decides.

Branch `claude/run17-financial`. The cases: FV-001, FV-003, NPV-001, NPV-002, PMT-001 to 003, PV-001,
PV-003 (each a double that differed from Excel's in the 13th to 15th significant digit) and PMT-008
(`type` 2, refused). The search is `spikes/exsheet-financial-evaluation/` (README, `search.py`,
`results.json`).

**Done on the branch:** PV and FV, PMT and the reading of `type` follow Excel (below), and their cases
are `observed` with Excel's double, compared exactly. **Stopped:** NPV. No evaluation the search tried
gives all three of its observed doubles, so the engine's NPV is as it was, NPV-001 and NPV-002 still
differ from Excel, and the question below is the user's.

## 1. ADR-0047: a note to add

At the end of ADR-0047, after the note of 2026-10-11:

> *(2026-10-12)* **PV and FV give Excel's double, and PMT every double Excel was seen to give.** The
> seventeenth Windows run read Excel's double for every financial case. Seven of PMT's, PV's and FV's
> differed from the engine's in the 13th to 15th significant digit, and PMT-008 was refused: the
> engine evaluated Microsoft's identity in doubles with a correctly rounded power, and Excel does
> neither. `spikes/exsheet-financial-evaluation/` searched for the evaluation that gives every
> observed double, the agreeing cases included, among some 13,000 candidates:
>
> - **PV and FV** are evaluated in doubles, in the identity's order, with 1+rate raised to `nper` by
>   squaring from the exponent's lowest bit. Excel's FV-001 lies 39 units in the last place from the
>   correctly rounded power's evaluation; squaring gives FV-001 to 003, PV-001 and PV-003 to the bit.
> - **PMT** is not. Excel's payments equal the payment at the end of each period carried beyond a
>   double and rounded once, and, at the start, that double divided by 1+rate, unrounded, and rounded
>   once (PMT-001 to 003 and 008). The engine takes the exact model: the payment computed exactly over
>   Excel's doubles and rounded once. It gives all four observed doubles, and so do 2504 of the
>   evaluations the search tried, so the observations do not choose it. 304 of those are in an x87's
>   64 bits. They agree with the exact model at the rates observed, 0.5% to 10% a period, and part
>   from it as the rate shrinks, because an x87's 1+rate keeps only the rate's bits above 2^-64. At PMT-016, 38 of them are one unit in the
>   last place away. At a daily rate over a year they give four doubles across 7 units (PMT-018). At a
>   rate of 10^-7 they give nine across 3,390 units, from the 13th significant digit (PMT-017). PMT-013
>   to 018 ask Excel.
> - **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
>   (PMT-008). A fractional one stays `#VALUE!` until Excel is asked.
> - Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
>   not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
>   Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
>   `nper` is answered.
>
> The cases the search could not settle — which of two orders of the payment's factors, and which of
> the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
> double, for the next Windows run.

If the user decides NPV (section 4), its outcome is a further bullet of the same note.

## 2. The engine's README (`src/ExSheet.Engine/README.md`)

In "Asked of Excel before they are answered", replace

> `PMT`, `PV` and `FV` with a `type` other than 0 or 1 (`#VALUE!`);

with

> `PMT`, `PV` and `FV` with a fractional `type`, and, at a rate other than 0, with an `nper` that is
> not a whole number from 0 to 2^53 (`#VALUE!`);

and after that list add a bullet:

> - **`PMT`, `PV` and `FV` give every double Excel was observed to give, to the bit** (ADR-0047, the
>   seventeenth run). `PV` and `FV` evaluate Microsoft's identity in doubles, raising 1+rate by squaring.
>   `PMT` computes the payment at the end exactly and rounds it once, and the payment at the start
>   divides that double by 1+rate, rounded once. For `PMT` that is one of several evaluations that fit
>   the four payments observed, at rates of 0.5% to 10% a period. At smaller rates they part, from the
>   13th significant digit at a rate of 10^-7, and PMT-016 to 018 ask Excel. A `type` that is a whole
>   number other than 0 is payment at the start, as 1 is.

## 3. The Docs Site

`/exsheet/functions` says nothing about precision; nothing to change there. On `/exsheet/formulas`,
section "Supported functions", the list of what the engine refuses gains an item (after `RANDBETWEEN`'s):

> <li>
>     <b><code>PMT</code>, <code>PV</code> and <code>FV</code></b> give, to the last digit, every result Excel
>     was seen to give for them. Which result that is depends on how Excel raises 1+rate to the number of
>     periods. For <code>PMT</code> it rests on payments at rates of 0.5% to 10% a period. At much smaller
>     rates Excel has not been asked, and its payment may differ from ExSheet's from the 13th significant
>     digit. At a rate other than 0, a number of periods that is not a whole number, or is negative, is
>     <code>#VALUE!</code> until Excel has been asked how it raises to one; so is a <code>type</code> that is
>     not a whole number. Any other <code>type</code> but 0, 2 as well as 1, is payment at the start of each
>     period, as Excel reads it.
> </li>

The Change Note (`docs/releases/unreleased/run17-financial-annuity-doubles.md`) points there.

## 4. NPV: the question for the user

**What was found.** NPV-001 (`=NPV(10%,-10000,3000,4200,6800)`, Excel 1188.4434123352207, nine units in
the last place below the exact value) is given only by the running product in doubles —
`d *= 1+rate; total += value/d` (or `value*(1/d)`), summed forward — of the 753 NPV candidates. That
evaluation gives NPV-002 (`=NPV(A1,A3:A7)+A2`, Excel 1922.0615549323702) one unit in the last place of
41922 too low (1922.061554932363): Excel's NPV-002 is the exact value correctly rounded, which every
64-bit evaluation, or a sum from the last value, gives, and NPV-001 rules those out. NPV-003 is given by
both. The best single evaluations reproduce two of the three. A split by argument kind (values typed as
arguments by the running product in doubles, values in a range carried beyond a double) gives all three,
but it is two choices fitted to three numbers, and the search was told not to guess.

The engine's NPV (`value / Math.Pow(1+rate, i)`, in doubles) gives NPV-003 and differs from Excel by
4 units in the last place on NPV-001 and 32 on NPV-002 (one of 41922's). The corpus keeps NPV-001 and
NPV-002 `documented`, with the engine's double at 12 decimals: they will disagree again.

**The refusal domain ADR-0202 would take.** The disagreement is not in a corner of the arguments: it is
in the last place of ordinary cash flows, typed or in a range. No domain except a rate of 0 can be shown
to agree. So the refusal would be: `NPV` at a rate other than 0 is `#VALUE!` until a Windows run settles
its evaluation (a rate of 0 sums the values, and a sum is Excel's).

**The question.** ADR-0202 corrects or refuses a disagreeing function before the tag. Which of these?

1. **Ask Excel first** (recommended): `oracle.ps1 -Area npv` asks NPV-005 to 008 (below) in minutes;
   the spike then reruns on every observed double, and NPV follows if one evaluation gives them all.
   Cheap, and the next run asks this branch's other uncertain cases anyway.
2. **Refuse now**: `NPV` at a rate other than 0 is `#VALUE!` until that run (the domain above). A
   visible step back for a common function, but nothing quietly wrong.
3. **Take the split by argument kind**: all three observed doubles, at the risk of being wrong on
   inputs no run has asked.
4. **Undeclare `NPV`** (`#NAME?`), as `IRR` is.

## 5. What to ask Excel next (in the corpus, `uncertain`, with the engine's double)

`oracle.ps1 -Area fv,pv,pmt,npv,xnpv` asks all of them through COM (no keys needed):

| Case | Formula | The engine | What it decides |
|---|---|---|---|
| FV-005 | `=FV(0.06/12,10,-200,-500,2)` | 2581.4033740601185 | `type` 2 in FV reads as 1, as in PMT |
| FV-006 | `=FV(0.06/12,10.5,-200)` | `#VALUE!` | a fractional `nper`: Excel's answer, for an evaluation to be searched |
| FV-007 | `=FV(0.06/12,-10,-200)` | `#VALUE!` | a negative `nper` |
| FV-008 | `=FV(0,10.5,-100)` | 1050 | at a rate of 0 a fractional `nper` is answered |
| FV-009 | `=FV(0.05/12,12,-150,0,1)` | 1849.502608424663 | the payment times 1+rate first (the engine), or times (g−1)/rate first: 1849.5026084246633 |
| FV-010 | `=FV(0.05/12,360,-150)` | 124838.79530422161 | squaring over nine squarings |
| PV-004 | `=PV(0.05,10,-100,1000,2)` | 196.8689140236461 | `type` 2 in PV |
| PV-005 | `=PV(0.05,10.5,-100)` | `#VALUE!` | a fractional `nper` |
| PV-006 | `=PV(0.05/12,37,2500,1000,0)` | -86415.16626124813 | dividing by g (the engine), multiplying by 1/g (…815), or another order of the payment's factors (…812) |
| PV-007 | `=PV(0.05/12,12,-150,0,1)` | 1759.4840643974248 | the payment's factors' order at the start (…425 otherwise) |
| PMT-009 | `=PMT(0.1,10,1000,0,0.5)` | `#VALUE!` | a fractional `type` |
| PMT-010 | `=PMT(0.1,10,1000,0,-1)` | -147.95035898410148 | a negative `type` is payment at the start |
| PMT-011 | `=PMT(8%/12,10.5,10000)` | `#VALUE!` | a fractional `nper` |
| PMT-012 | `=PMT(8%/12,-10,10000)` | `#VALUE!` | a negative `nper` |
| PMT-013 | `=PMT(0.05/12,36,250000,-5000)` | -7363.703123976373 | the accurate kind (the exact model and all 304 at 64 bits give this) against the excess forms (…375, …374, …372) |
| PMT-014 | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725492 | the same at the start (…49, …485) |
| PMT-015 | `=PMT(0.05/12,12,10000,0,1)` | -852.5226402170998 | the same (…2171002, …2171, …2170999) |
| PMT-016 | `=PMT(0.045/12,12,250000,-5000)` | -20936.48778866115 | splits the 64-bit kind itself: 266 of the 304 give the exact model's double, 38 give …154 |
| PMT-017 | `=PMT(1/10000000,12,1000)` | -83.33338750000993 | a small rate: the 304 give nine doubles across 3,390 units in the last place, to …5811 |
| PMT-018 | `=PMT(0.0365/365,365,10000)` | -27.90167156122446 | a daily rate over a year: the 304 give four doubles across 7 units, all 2504 nine across 12 |
| NPV-005 | `=NPV(0.08,8000,9200,10000,12000,14500)` | 41922.06155493236 | NPV-002's values typed as arguments: the running product gives …236, the exact value …237 |
| NPV-006 | `=NPV(A1,A3:A7)`, NPV-002's cells | 41922.06155493236 | NPV-002 without `+A2`: whether the 1 in the last place is NPV's or the addition's |
| NPV-007 | `=NPV(0.1,A1:A4)`, NPV-001's values in A1:A4 | 1188.4434123352216 | NPV-001's values in a range: the split by argument kind predicts …228, the running product …207 |
| NPV-008 | `=NPV(A1,-10000,3000,4200,6800)`, A1 0.1 | 1188.4434123352216 | NPV-001 with its rate in a cell |
| XNPV-004 | `=XNPV(0.12,A1:A5,B1:B5)` | 342.04308747657205 | a correctly rounded power (the engine's `Math.Pow`) or e^(x ln(1+rate)), the `^` operator's (342.0430874765725) |

NPV and XNPV cases compare at 12 and 6 decimals, as their files do; what decides is the double the
oracle records (`excel.number`), which `search.py` reads from every results file.

Outside this area's files, for the arithmetic area if it wants it: `=1.005^240` and `=(1+0.08/12)^10`
would say whether Excel's `^` operator also raises a whole exponent by squaring (the engine's
`Arithmetic.Power` takes `Math.Pow`; the observed `^` cases are all exact, so they cannot tell).

## 6. Every candidate tried, with its match count

Each candidate is counted on every observed double of its function (rate 0 excluded: no power is
raised). Full lists: `results.json`; per-candidate distances: `search.py --detail`.

| Function | Candidates | Reproduce all | Match counts |
|---|---:|---:|---|
| FV (3 doubles) | 1008 | 4 | 3/3: 4 · 2/3: 6 · 1/3: 64 · 0/3: 934 |
| PV (2) | 1008 | 6 | 2/2: 6 · 0/2: 1002 |
| PMT (4) | 11088 | 2504 | 4/4: 2504 · 3/4: 3389 · 2/4: 1699 · 1/4: 1580 · 0/4: 1916 |
| NPV (3), schemes | 297 | 0 | 2/3: 8 · 1/3: 225 · 0/3: 64 |
| NPV (3), discount × order × precision | 456 | 0 | 2/3: 32 · 1/3: 346 · 0/3: 78 |
| XNPV (1) | 12 | 6 | 1/1: 6 · 0/1: 6 |

- **Ways of raising 1+rate to nper**: a correctly rounded power; squaring from the lowest bit; from the
  highest bit; repeated multiplication; e^(n ln(1+rate)); e^(n log1p(rate)); the x87's FYL2X, F2XM1,
  FSCALE; the C library's `pow`; e^(n log1p) beside expm1(n log1p); 21 excess forms carrying
  (1+rate)^n − 1 without forming 1+rate. Each at 53 and 64 bits, the rest at 53 and 64.
- **Forms**: FV 7, PV 7, PMT 20 orders of Microsoft's identity (with 1/g, with (g−1)/rate grouped or
  not, the payment's factors in each order, LibreOffice's expm1((n+1)·log1p) form at the start), and
  for PMT the payment at the start inline, or the payment at the end stored to a double and divided by
  1+rate in a double, at 64 bits, or exactly.
- **Survivors**: FV — squaring from the lowest bit, doubles, F1 and its equivalents (the engine) and F4
  (FV-009 decides). PV — the same raising, V1 (the engine), V2, V4, V5, V7 (PV-006 and 007 decide).
  PMT — 304 at 64 bits in every form and way of raising, 44 from log1p and expm1, 2156 excess forms
  (100 of them in doubles throughout), all with the payment at the start divided after the payment at
  the end is stored, or inline for 68 excess forms. The engine takes the exact model (the payment
  computed exactly and rounded once), which also gives all four but is none of the 2504. It agrees with
  every 64-bit survivor on PMT-013 to 015, with 266 of the 304 on PMT-016, and with fewer as the rate
  shrinks (PMT-017, 018; `--spread`). *(Corrected after review: this said "the engine takes the 64-bit
  kind (PMT-013 to 016 decide)". PMT-013 to 015 separate only the excess forms, and PMT-016 splits the
  64-bit kind. `--discriminate` keeps one form per family and searches rates of 0.0037 and above, so it
  could not find the small rates where the candidates part.)* NPV — none; best: the running product in doubles summed forward (NPV-002 −1 of 41922's last
  place), and 64-bit or summed-from-the-last evaluations (NPV-001 −2 to +9). XNPV — `pow`, the
  correctly rounded power and e^(x ln b), each in doubles (XNPV-004 decides).
- **The C# port** was checked against the search's model at 400 generated inputs, all three functions:
  1200 doubles, all equal. For PMT the model is the exact one, so the check says nothing of the 64-bit
  evaluations. *(The branch first said, in `Wide`'s doc comment, in the spike's README and in section 1,
  that about 106 bits give an x87 64-bit evaluation's double "except within about 2^-60 of a rounding
  boundary". That was false. The 64-bit forms lose the rate's bits below 2^-64 in 1+rate, so they part
  from the exact model, and from one another, at small rates, not only near a boundary. All four places
  are corrected.)*
