# 08: Measure aggregation and re-layout

Status: ready-for-agent

**What to do:** PV-21 is observational and nothing has been measured. Record, in `metrics.json`,
how long `PivotEngine.Aggregate` takes over a large record set (100,000 and 1,000,000 records,
three row fields, one column field, two Value Fields) and how long `PivotEngine.Report` takes to
lay out a changed layout from a held cube. Never gate on it (AGENTS.md). Until it is measured,
nothing is claimed about ExPivot's speed — including whether Defer Layout Update is needed, which
ADR-0058 leaves for later on exactly that ground.

**Blocked by:** None

- [ ] A measurement mode, in `spikes/render-bench` or beside the engine's tests, that writes the numbers
- [ ] The numbers recorded, with the machine they came from

## Comments
