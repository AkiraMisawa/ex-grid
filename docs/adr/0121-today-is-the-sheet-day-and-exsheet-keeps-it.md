# TODAY answers the Sheet Day, and ExSheet keeps it

*(Decided with the user on 2026-10-03, from the function catalogue's P1 Decide list,
`docs/specs/exsheet-functions/spec.md`. The JavaScript it needs is
[ADR-0122](./0122-the-browser-tells-the-grid-its-time-zone.md).)*

Excel's `TODAY()` returns the date on the user's own computer. A served sheet has no such computer:
on Blazor Server the code runs on the server, and a server in London tells a user in Tokyo at 8:00
that it is still yesterday. A plausible date that is wrong is the answer this project refuses.

[ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md) also had
no recalculation driven by a clock. [ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)
records Entries and never Values, so a saved Sheet recomputes `TODAY()` whenever it is opened, as
Excel's does.

## The decision

**`TODAY()` answers the Sheet Day: the calendar day it is now in the user's time zone, unless the
Consumer names another.** Which day that is resolves in this order:

1. **A fixed day the Consumer gives** (`Today`). For a report that must show the same day whenever
   it is opened, such as a month-end as of 30 September.
2. **A time zone the Consumer gives** (`TimeZone`). For a site that keeps one business day for every
   user, such as head office's.
3. **The browser's time zone**, which the browser tells the grid (ADR-0122). This is the default,
   and it is Excel's answer: the day on the user's own device.

**Until one of them is known, `TODAY()` is `#GETTING_DATA`.** That covers the moment before the
browser has told its zone, and a zone that .NET does not know. `#GETTING_DATA` already means "the
data this needs is on its way" ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)).
`IFERROR` does not catch it, and a Formula that calls `TODAY` waits with it, whether or not the
call's branch is taken, as a Formula that reads a waiting Linked Table does. The server's own clock
and zone are never used in its place.

### The engine never reads a clock

`ExSheet.Engine` takes the Sheet Day as data: `Sheet.SetToday(DateOnly?)`. It recalculates only the
Formulas that call `TODAY`, as a Linked Table's snapshot recalculates only its readers. A Sheet
whose day was never set answers `#GETTING_DATA`. A server that validates a saved sheet sets the
day it means. Nothing in the engine depends on when or where it runs.

### ExSheet keeps the day, and moves it at midnight

The component holds the clock, so a Consumer does not write the same timer in every application:

- **It reads the time from .NET's `TimeProvider`.** It is taken from the services when one is
  registered, and is `TimeProvider.System` otherwise. A test registers a provider it controls and
  sets the time to 23:59, so no outcome depends on when a test runs (principle 6).
- **It re-reads the day when the day can have changed.** That is at the next midnight in the zone,
  and at most an hour after its last reading. The hour bounds what a clock change, a daylight-saving
  shift or a sleeping device can cost: a day is never late by more than an hour. A reading that
  finds the same day changes nothing, so nothing renders and nothing recalculates.
- **A change of `Today` or `TimeZone`** sets the new day at once.

### Formats

`TODAY` gives a General cell the short date format, as Microsoft documents. It is the same rule as
`DATE` (ADR-0047's additions, FF-036).

## What differs from Excel, by decision

- **Excel's `TODAY` is volatile.** It is recalculated with every recalculation of the workbook. Here
  it is recalculated when the day changes, which is the only time its answer can change.
- **Excel uses the device's date. ExSheet uses the date in the device's time zone**, as the browser
  reports that zone. The two differ only on a device whose clock is wrong.

## Considered options

- **The server's clock.** Rejected: on a server in another time zone, every user east or west of it
  is given another day for part of each day.
- **The Consumer passes the day, and keeps it current.** This was the first proposal. It was
  rejected because every Consumer would write the same midnight timer, and a Consumer who forgot the
  timer would show yesterday on a page left open overnight. The Consumer still decides whose day it
  is (rules 1 and 2). ExSheet only counts it.
- **Unsupported: `#NAME?`.** Rejected: budgets and reports age their figures by today's date.

## Consequences

- `TODAY` joins the declared set (ADR-0047's dated additions). `NOW`, which needs a time of day as
  well as the day, stays in the catalogue for its own decision.
- Glossary: **Sheet Day**.
