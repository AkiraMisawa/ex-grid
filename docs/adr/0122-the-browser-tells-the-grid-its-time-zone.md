# The browser tells the grid its time zone: ADR-0021's ninth entry

*(Decided with the user on 2026-10-03, for [ADR-0121](./0121-today-is-the-sheet-day-and-exsheet-keeps-it.md).)*

ADR-0121 makes the browser's time zone the default for `TODAY()`, because Excel answers with the
day on the user's own device. On WebAssembly, .NET runs in the browser and `TimeZoneInfo.Local` is
the browser's zone. On Blazor Server, .NET runs on the server, and `TimeZoneInfo.Local` is the
server's zone. No Blazor API carries the browser's zone to the server.

## The decision

**A ninth allowlisted use: at attach, the grid's script module reads
`Intl.DateTimeFormat().resolvedOptions().timeZone` once and reports it to C#.** The grid passes it to
its Consumer as `OnBrowserTimeZone`, an IANA name such as `Asia/Tokyo`. The grid itself does nothing
with it. ExSheet is the Consumer that does (ADR-0121).

- **It is reported only when a Consumer asks.** The report is armed only when `OnBrowserTimeZone`
  has a delegate. A grid that no one asks costs nothing.
- **Both hosts take the same path.** WebAssembly could read `TimeZoneInfo.Local` without JavaScript,
  but one path for both hosts keeps the answer from depending on the host.
- **It is told, never measured.** It is read once, it reads no layout, and it never runs per render.
  It is the same kind of report as the Device Pixel's (the eighth entry): the browser already knows
  the answer, and the grid is told it.
- **It is not told again when the zone changes.** Browsers raise no event when the device's zone
  changes. A device carried across zones keeps the zone it had when the page was opened, until the
  page is opened again. A Consumer that must follow a traveller sets `TimeZone` (ADR-0121).
- **What crosses the boundary is checked.** C# takes the name only if it is non-empty and at most
  100 characters long. Whether .NET knows the zone is ExSheet's question, and an unknown zone keeps
  `TODAY()` at `#GETTING_DATA` (ADR-0121).

## Considered options

- **The Consumer reads the zone with its own interop.** Rejected: every Consumer would need the
  same JavaScript, and ADR-0121's default would no longer work without it.
- **A JavaScript module of ExSheet's own.** Rejected: ExSheet would ship a second script and attach
  it to the same root. The grid already attaches once per instance (ADR-0018).
- **Read the zone in C# from the request.** Rejected: HTTP carries no time zone. Guessing it from
  the language or the IP address would be the plausible wrong answer this project refuses.

## Consequences

- ADR-0021's table gains this entry, and `AGENTS.md` counts nine uses.
- Layer 3 sets the browser's zone with Playwright's `timezoneId`, and checks that `TODAY()` follows
  it, on both hosts.
