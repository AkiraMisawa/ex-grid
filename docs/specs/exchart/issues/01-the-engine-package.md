# 01: The `ExChart.Engine` package exists, depends on nothing, and is tested

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0250](../../../adr/0250-exchart-is-a-chart-of-the-familys-data-in-three-packages-drawn-as-its-own-svg.md), "The packages".

- [ ] `src/ExChart.Engine` targeting `net10.0`, with no package or project reference (ADR-0022, ADR-0250)
- [ ] `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` and XML doc comments enforced as for every shipped project (ADR-0201)
- [ ] `tests/ExChart.Tests` (xUnit, layer 1) in `ExGrid.slnx`
- [ ] The package named where the family's packages are listed (the Docs generator's `Packages`, `release.yml`, the package smoke) — or each place named in the PR as left for M6

**Blocked by:** nothing.
