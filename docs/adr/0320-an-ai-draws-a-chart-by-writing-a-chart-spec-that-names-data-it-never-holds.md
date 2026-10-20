# An AI draws a chart by writing a Chart Spec that names data it never holds

*Decided with the user, 2026-10-20, in the grilling of a declarative entry for AIs (AI-Q1 to AI-Q5 and
AI-Q8 to AI-Q10). It began from the user's question of how an AI, given a declarative spec, could
draw ExChart: "first the data, made by calling existing capabilities, then a grid, then the chart?"*

Until now a chart is made in C#. `ChartDefinition` and `ChartSeries` are records, `SeriesSampler`
holds a `Func`, and the selection bridge (`GridChartDeclarations<TRow>`, [ADR-0258](0258-the-selection-bridge-follows-the-rows-it-was-made-from.md)) is all functions:
`RowKey`, `RowOf`, `KindOf`, `PositionOf`. None of it has a form an AI can write. The binomial lattice
Showcase (PR #199) also showed what a chart could not say for itself: a per-point colour scale, its
colour bar, a marker's size, and a legend hidden by the page's stylesheet.

## Decisions

- **The data is a Snapshot; the chart never goes through a grid.** The AI calls the host's
  capabilities, which make a Snapshot ([ADR-0064](0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)) on the host and return only its **Data Handle**, a
  name. The **Chart Spec** names that handle and its columns. A grid showing the same Snapshot beside
  the chart is the page's choice, not a step on the way. Selecting a range and charting it stays the
  entry for people (ADR-0258).
- **A Chart Spec is JSON, read by ExChart.Engine against a published JSON Schema** into a
  `ChartDefinition`. It uses System.Text.Json only; the Engine still references no package. Not a C#
  builder for the AI to run, and not an existing grammar such as Vega-Lite, which has no Series Kind
  and no Notes: a translation would be quietly wrong where the two disagree.
- **A spec that cannot be drawn is refused with one sentence naming the path and the reason**
  (`series[1].kind is missing`, `data "lattice" is not known`). The host returns it as the tool's
  result, and the AI corrects its spec. This is spine rule 1, said to an AI.
- **A spec holds no numbers.** Values are only ever read from the Snapshot, so an AI copying a
  million values cannot drop a row or a digit, and the numbers cost no tokens. A small example still
  goes through a capability.
- **The AI writes every series' `kind`, with no default** ([ADR-0253](0253-every-series-declares-its-series-kind-and-its-drawing-follows.md)). *The user chose this over the
  recommendation that the capability put the meaning on the Snapshot's columns:* existing
  capabilities do not know ExChart's vocabulary, and the selection bridge already has the chart's
  side declare the kind (`KindOf`), so the Snapshot takes nothing of ExChart's. The accepted cost: a
  wrong `kind` is not caught by refusal.
- **A column may give a point its colour or its size** (`"colour": {"column": "value", "scale":
  "sequential"}`, `"size": {"column": "exercise"}`): a column and the kind of scale, never a colour
  value, which stays a Visual Token ([ADR-0027](0027-appearance-travels-in-css-geometry-travels-in-csharp.md)). A colour or a size always draws its key, a Colour Bar
  as [ADR-0310](0310-a-surface-shows-its-colour-bar-and-its-colours-may-diverge-from-a-centre.md) draws one, or a size legend, and the key cannot be hidden: a colour that cannot be read
  back to a number is a picture that looks right and says nothing.
- **A spec carries a required `version`.** An unknown version is refused, never read as the nearest.
- **A Data Handle the host's lookup does not know is refused by name.** Which Snapshots an AI may
  name is the host's lookup, not ExChart's.
- **ExChart ships no LLM.** It ships the tool definition (name, description, JSON Schema) and the
  reader; the host supplies the lookup from a handle to its Snapshot and connects them to tool use or
  MCP. No MCP package now; a thin one may come later as a package of its own.
- **An accepted spec is answered with the chart said in words**: the summary of [ADR-0275](0275-a-chart-is-read-aloud-as-a-summary-and-as-a-table.md), extended
  with how many pixels were thinned and how many points are missing, so the AI can check the chart
  against its intent. It is made from the definition, on the server, without a browser. Not a PNG,
  which only the browser paints ([ADR-0282](0282-a-thirteenth-javascript-entry-paints-a-chart-to-png-and-saves-its-files.md)) and which costs the AI far more to read.
- **The first version covers everything ExChart draws**: 2D series with candles and scatter,
  colour and size, nodes and edges ([ADR-0321](0321-nodes-and-edges-are-two-snapshots-and-exchart-lays-out-nothing.md)), the 3D surface and Notes. Laying a grid beside the
  chart and linking the two is the page's; a declarative grid spec, if one is wanted, is ExGrid's
  own decision.

## Consequences

- The Chart Spec is a public surface with a version of its own; a change to its schema is a Change
  Note ([ADR-0190](0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
- How the chart reads the Snapshot, on which host, and what it does when the Snapshot moves on is
  [ADR-0322](0322-a-chart-reads-its-data-through-a-chart-source-that-thins-where-the-data-lives.md).
- The grilling's record is `exchart/grilling-ai-spec.md` in the project's shared files.
