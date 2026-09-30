# 19: The six demo pages

Status: ready-for-agent

**What to build:** the six pages ADR-0068 lists.

- `/pivot`, updated;
- `/pivot-csv`;
- `/pivot-db`;
- `/pivot-live`;
- `/pivot-risk`;
- `/grid-live`.

Each page states its use case and the API it uses, in code a developer can copy. Each runs on both
hosts and, where it has a pivot, under both Chromes. The pages are added to the page list and to
`navigation.spec`.

**Blocked by:** 13, 14, 15, 16, 17, 18, change-highlight 01

- [ ] PV-20, in layer 3
- [ ] change-highlight 02 (DC-54, DC-55) on `/grid-live`
- [ ] The browser's `HttpClient` turns response streaming off for the Arrow request (ADR-0064)

## Comments
