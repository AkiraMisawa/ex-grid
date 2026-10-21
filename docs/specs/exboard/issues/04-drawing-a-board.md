# 04: Drawing a Board

Status: done

ADR-0340: an `ExBoard` component draws a read Board with each input bound to a source (in memory or
held where the data lives), refusing a mismatch by name before anything is drawn. `ExBoard.MudBlazor`
wraps it. Layer 3 covers the layout under both hosts.

Done without a Wrapper package: the `ExBoard` component takes the grid, pivot and chart Chromes as
parameters, so a MudBlazor application passes `MudGridChrome`, `MudPivotChrome` and `MudChartChrome`.
Whether `ExBoard.MudBlazor` is needed at all is ticket 10's to say. A layer-3 spec over a Board
joins ticket 05, the first that adds a gesture of the Board's own.
