# How EDATE and EOMONTH read their start: serial 0, and a start between -1 and 0

The area passes of 2026-10-12 (`verification/2026-10-12-windows-area/` and `-area-2/`) asked Excel
`EDATE` and `EOMONTH` from serial 0 and 60, and from a start of -0.5, and asked `YEAR`, `MONTH`, `DAY`,
`WEEKDAY`, `DAYS`, `NETWORKDAYS` and `WORKDAY` a serial of -0.5. ADR-0047 admits a function only where
it answers as Excel, and ADR-0202 refuses where the readings Excel's answers leave part. This
experiment writes out each whole reading of the two functions, counts the observed cases it
reproduces, and prints where the readings that reproduce all of them part. It is the evidence behind
`MonthsAway` in `Functions.Dates.cs`. Nothing here ships.

```sh
python3 spikes/exsheet-date-readings/readings.py    # the table; results.txt is its output
```

Standard library only. The cases are written into the script from the corpus (`edate.json`,
`eomonth.json` and the date functions' files), Excel's answers for EDATE-016, EDATE-017, EOMONTH-016
and EOMONTH-017 included.

## The readings

Five choices, each with its alternatives, 112 readings in all:

- **The start made whole**: truncated; cut down to its day (`Math.Floor`, as the engine's `YEAR`,
  `MONTH` and `DAY` cut a serial); below 0 `#NUM!`, then truncated; or refused between -1 and 0 (the
  engine before this pass).
- **Serial 0**: 0 January 1900 under both functions (as `YEAR`, `MONTH` and `DAY` read it); 31
  December 1899 under both; or 31 December 1899 under `EDATE` and 0 January 1900 under `EOMONTH`.
- **`EDATE` from a start whose day is 0**: refused; the day before the 1st of the month it reaches, by
  `DATE`'s arithmetic, which counts 29 February 1900; the month before's last day on the real
  calendar; day 1; or `#NUM!`.
- **31 December 1899 as a result**, under both functions alike, as one conversion serves both: `#NUM!`, or
  serial 0.
- **The months' lengths**: the real calendar (February 1900 has 28 days), or Excel's (29).

## What the observations settle

| Observed cases | Readings that reproduce them all |
|---|---|
| 32 of `EDATE` and `EOMONTH` (EDATE-009 and EOMONTH-009, a boolean typed, are no question of reading) | **6** of 112 |
| 16 of a serial of -1, -0.5 or 0 under the functions that read a date | 2 of 4 start readings |

- **A start is cut down to its day**, as `YEAR` reads a serial: truncation answers 31 for
  `EOMONTH(-0.5,0)`, where Excel answers `#NUM!`. "Below 0 `#NUM!`, then truncated" gives the same
  answer for every start, so the two are one rule.
- **Serial 0 is in January 1900 under `EOMONTH`** (EOMONTH-010, EOMONTH-014), and **a month on from it
  under `EDATE` is 31** (EDATE-016). That rules out refusing `EDATE` from serial 0, reading day 0 as day
  1 (32) and as `#NUM!`.
- **The months are counted on the real calendar** (EDATE-011, EDATE-013, EOMONTH-008, EOMONTH-010 to
  012), and a result of 31 December 1899 is `#NUM!` (EOMONTH-015), so `EDATE(31,-1)` is `#NUM!` under all six.

## Where the six part

They agree on every `EDATE` from serial 0 but three, each a month whose day 0 falls on an edge:

| Formula | The answers | Why |
|---|---|---|
| `=EDATE(0,0)` | 0, or `#NUM!` | the day before 1 January 1900 is serial 0 by `DATE`'s arithmetic, and 31 December 1899 otherwise |
| `=EDATE(0,2)` | 60, or 59 | the day before 1 March 1900 is the day that never existed by `DATE`'s arithmetic, and 28 February on the real calendar |
| `=EDATE(0,97200)` | `#NUM!`, or 2958465 | January 10000 is past the last year, and 31 December 9999 is the last day |

The engine refuses those three with `#VALUE!` (and the same from a start between 0 and 1), and answers
the rest: `EDATE(0,1)` is 31, `EDATE(0,3)` is 91 and `EDATE(0,12)` is 366. EDATE-018 to 021 ask them.
