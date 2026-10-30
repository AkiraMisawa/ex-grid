# SH-19's variant: a whole-column `SUM` on every row

SH-19 (the Definition of Done, §27) records what ExSheet's recalculation costs at a large Sheet. Its
case, decided with the user, is a synthetic Sheet from a fixed seed: 10,000 rows by 20 columns with
about 50,000 Formulas, and **twenty whole-column totals beside the block**, `=SUM(A:A)` to
`=SUM(T:T)` in V1 to V20. `dotnet/tests/ExSheet.Engine.Tests/Measurements.cs` measures it.

This console project measures **the variant**: the same Sheet, from the same seed and in the same
shape, with column T written as each row's share of R's total, `=R1/SUM(R:R)`, where SH-19's case has
`=R1*H1/100-I1`. So 10,020 Formulas read a whole column where SH-19's case has 20. It times SH-19's
three measurements on it: a full recalculation (`Sheet.Open`), one edit's propagation (to A1, to
A10000 and to O5000), and a row inserted at row 2 with its undo.

## Why it is here and not in the suite

The variant was measured beside SH-19's case on 2026-10-11, as a fourth `[Fact(Explicit = true)]` in
`Measurements.cs` (`Every_row_reading_a_whole_column`). Its cost is the one
`docs/specs/exsheet/formula-dependencies.md` §11 names as "cost to know", and the case as first
written did not say where its whole-column `SUM`s stand: read the variant's way, an edit takes about
45 s where SH-19's takes under a tenth of a second (`verification/2026-10-11-linux-sh19/`).

**On 2026-10-12 the user decided** that SH-19 is the case with its totals beside the block, and that
the variant's cost is a **Known limitation of `0.1.0`** ([ADR-0202](../../docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)'s
note): a Sheet where a whole-column `SUM` sits on every row holds an edit, and on the Server host the
circuit, for tens of seconds. So the variant left the test suite, where it added a quarter of an
hour to SH-19's minute under `-explicit only`, and lives here, where it can be run again when the
engine's cost of a whole-column range changes (`formula-dependencies.md`'s open question 11).

Why it costs what it does is in that record's README, under "What the numbers say": each of the 10,000
`=R…/SUM(R:R)` visits every held cell, and every lookup of a cell's dependents scans every Formula
that reads a range.

## How to run it

From the repository root, in a Release build (it is not in `ExGrid.slnx`, so the solution's build
and tests never touch it):

```sh
nix develop -c dotnet run -c Release --project spikes/exsheet-sh19-whole-column             # 10,000 rows: about a quarter of an hour
nix develop -c dotnet run -c Release --project spikes/exsheet-sh19-whole-column -- 1000     # a smaller block, to see that it works: seconds
```

The argument is the block's height, 10,000 by default. A smaller block is the same layout from the
same seed, cut short; its times say nothing about SH-19's size, since the variant's cost grows with
the square of the rows (a tenth of the rows, about a hundredth of the time).

- Each time is taken **three times, with no run to warm up**, each from a collected heap, as the test
  took them: a run takes tens of seconds at 10,000 rows. The median, best and worst are printed, with
  the collections each run caused.
- **Every timed run is checked after it, outside the clock**, against the same arithmetic done here,
  as `Measurements.cs` checks SH-19's case: every Formula's text in P to T and in V, every Value of P
  to T, all twenty totals, and how many Formulas an edit or an insertion recalculated (20,009, 10,010
  and 1 for the three edits, 50,016 for the insertion, at 10,000 rows). A row inserted is checked
  before its undo: row 2 holds nothing, every number of the block and of its totals is one row
  further down and unchanged, and every Formula reads where its cells went. The undone Sheet is
  checked as it was before. A check that fails prints `CHECK FAILED` and ends the run with exit
  code 1, and no time is printed for what failed.

## The numbers recorded

From `verification/2026-10-11-linux-sh19/` (its README and `raw/`), taken by the test this project
replaces, on a shared 4-vCPU cloud container (KVM, Intel Xeon @ 2.10 GHz, .NET 10.0.7, workstation
GC, Release). The third pass ran at a load average of 4 to 6 (2026-10-11, 22:30 to 22:44 UTC); the
fifth, with every run checked as this project checks it, at 13 to 17 (2026-10-12, 00:07 to 00:23 UTC).

| Measurement | Formulas recalculated | Third pass: median (best–worst) | Fifth pass: median |
|---|---:|---:|---:|
| Built by one `SetEntries` | 50,020 | 85.4 s (one run) | 100.1 s |
| A full recalculation (`Sheet.Open`) | 50,020 | **66.3 s** (65.9–70.9) | 76.5 s |
| An edit to A1 | 20,009 | **48.0 s** (46.6–50.4) | 42.5 s |
| An edit to A10000 | 10,010 | **44.8 s** (44.5–45.0) | 39.0 s |
| An edit to O5000 | 1 | **26 ms** (22–28) | 22 ms |
| A row inserted at row 2 | 50,016 | **47.2 s** (45.8–48.8) | 47.6 s |
| That insertion undone | — | **49.4 s** (49.4–50.3) | 52.5 s |

SH-19's own case, for comparison (the same record's second pass): 621 ms, 87 ms, 41 ms, 17 ms,
1,798 ms and 2,479 ms.

## This project's runs

On the same container, 2026-10-12, with this project as first committed.

**1,000 rows**, to see that it works:

```text
2026-10-12T09:32:55Z
4 logical CPUs, .NET 10.0.7, Ubuntu 24.04.5 LTS, server GC False, Release build, load average 4.47 7.49 4.85
every row's share of a whole-column SUM: built by one SetEntries in 1466 ms
every row's share of a whole-column SUM: 1,000 rows by 20 columns, and 20 totals beside them; 5,020 Formulas, 1,020 of them reading a whole column, and 15,000 constants (seed 20261011)
every row's share of a whole-column SUM: Sheet.Open, every Value computed: median 522.2 ms, best 474.8 ms, worst 526.1 ms (n=3); collections by run: g0+2/g2+1 g0+1/g2+0 g0+1/g2+0
every row's share of a whole-column SUM: an edit to A1, which the whole running total reads: median 337.9 ms, best 287.2 ms, worst 412.2 ms (n=3); 2,009 recalculated, 2,010 Values changed; collections by run: g0+0/g2+0 g0+0/g2+0 g0+0/g2+0
every row's share of a whole-column SUM: an edit to A1000, which its own row and the totals read: median 346.2 ms, best 258.2 ms, worst 351.2 ms (n=3); 1,010 recalculated, 1,011 Values changed; collections by run: g0+0/g2+0 g0+0/g2+0 g0+0/g2+0
every row's share of a whole-column SUM: an edit to O500, which only its column's total reads: median 1.3 ms, best 1.0 ms, worst 3.3 ms (n=3); 1 recalculated, 2 Values changed; collections by run: g0+0/g2+0 g0+0/g2+0 g0+0/g2+0
every row's share of a whole-column SUM: a row inserted at row 2: median 499.9 ms, best 442.6 ms, worst 749.4 ms (n=3); 5,016 recalculated, 1,000 rows changed; collections by run: g0+1/g2+0 g0+1/g2+0 g0+1/g2+0
every row's share of a whole-column SUM: that insertion undone: median 601.4 ms, best 525.3 ms, worst 681.1 ms (n=3); collections by run: g0+2/g2+1 g0+2/g2+1 g0+2/g2+1
2026-10-12T09:33:04Z, 0.1 min in all; load average 4.74 7.45 4.87
```

**10,000 rows**, SH-19's size, in 16.4 minutes. Every check held: 20,009, 10,010 and 1 Formulas
recalculated by the three edits and 50,016 by the insertion, as recorded. The machine was shared:
other agents' browser suites started during the run, and the load average rose from 4.6 to 13. That
is the likely reason, not one shown, that the edits took 59 to 68 s here where the record's third
pass, at 4 to 6, took 45 to 48 s; the insertion and its undo, at about 50 s, are near the record's
47 to 52 s.

```text
2026-10-12T09:33:15Z
4 logical CPUs, .NET 10.0.7, Ubuntu 24.04.5 LTS, server GC False, Release build, load average 4.57 7.32 4.85
every row's share of a whole-column SUM: built by one SetEntries in 80098 ms
every row's share of a whole-column SUM: 10,000 rows by 20 columns, and 20 totals beside them; 50,020 Formulas, 10,020 of them reading a whole column, and 150,000 constants (seed 20261011)
every row's share of a whole-column SUM: Sheet.Open, every Value computed: median 73524.6 ms, best 67587.4 ms, worst 74275.9 ms (n=3); collections by run: g0+62/g2+14 g0+60/g2+12 g0+60/g2+12
every row's share of a whole-column SUM: an edit to A1, which the whole running total reads: median 68024.6 ms, best 64573.9 ms, worst 68545.7 ms (n=3); 20,009 recalculated, 20,010 Values changed; collections by run: g0+37/g2+19 g0+36/g2+18 g0+36/g2+18
every row's share of a whole-column SUM: an edit to A10000, which its own row and the totals read: median 59365.9 ms, best 55211.4 ms, worst 62787.4 ms (n=3); 10,010 recalculated, 10,011 Values changed; collections by run: g0+36/g2+18 g0+36/g2+18 g0+36/g2+18
every row's share of a whole-column SUM: an edit to O5000, which only its column's total reads: median 18.5 ms, best 13.9 ms, worst 20.7 ms (n=3); 1 recalculated, 2 Values changed; collections by run: g0+0/g2+0 g0+0/g2+0 g0+0/g2+0
every row's share of a whole-column SUM: a row inserted at row 2: median 50231.6 ms, best 49102.2 ms, worst 56861.1 ms (n=3); 50,016 recalculated, 10,000 rows changed; collections by run: g0+64/g2+11 g0+65/g2+12 g0+64/g2+12
every row's share of a whole-column SUM: that insertion undone: median 49959.9 ms, best 49370.8 ms, worst 50209.4 ms (n=3); collections by run: g0+63/g2+9 g0+61/g2+7 g0+62/g2+9
2026-10-12T09:49:38Z, 16.4 min in all; load average 13.34 11.47 9.38
```
