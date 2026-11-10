# 03: One definition of the Aggregations, in `@ex-grid/data` (DA-18)

Status: ready-for-agent
Roadmap: ExGrid, ExPivot, Data — The status bar's figures and a pivot's totals are computed by one definition of each Aggregation, so the two can never disagree.

**Why:** DA-18 asks that the Selection Summary and ExPivot answer through one definition of the Aggregations. The
Script Core defines them twice: `packages/core/src/summary/summary-accumulator.ts` for the Selection Summary and
`packages/pivot-engine/src/aggregates/parts.ts` for ExPivot. Only the C# side has a test that the definition is one
(`ExPivot.Components/OneAggregationDefinitionTests.cs`). The user made it a Known limitation of 0.2.0 on 2026-11-07
(0.2.0's Release Notes; the [sign-off](../../../../verification/2026-11-09-linux-signoff/results.md), DA-18).

**What:** one definition of the eleven Aggregations in `@ex-grid/data`, which `@ex-grid/core`'s Selection Summary
and `@ex-grid/pivot-engine` both answer through, and a test that fails if either defines one of its own again
(DA-18's Check). The figures do not change: `tests/cases/summary/summary.json` and `tests/cases/pivot/aggregation.json`, which hold
the two today, each pass through the one, and a case that holds both side by side is added.
Then DA-18 passes, the Known limitation goes from the Docs Site's Status-bar figures section, and a Change Note says
so.
