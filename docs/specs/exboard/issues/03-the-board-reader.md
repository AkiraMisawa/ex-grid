# 03: The Board reader

Status: ready-for-agent
Roadmap: internal

ADR-0340: a new package `ExBoard` reads a Board's JSON — a required `version`, an unknown one refused;
`inputs` (ADR-0341's declarations); `views`, each an `id` and one of `grid` (ADR-0342), `pivot`
(ExPivot.Engine's layout JSON) or `chart` (a Chart Spec, ticket 08); `layout`, rows of view ids with
their spans, never free placement. Refusal is one sentence naming the path, as the Chart Spec's.
