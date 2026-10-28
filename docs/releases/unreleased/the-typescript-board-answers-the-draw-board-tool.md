Product: ExBoard
Kind: Added
ADR: 0340, 0320

`@ex-grid/board` answers the `draw_board` tool as `BoardSpec` does, in the same words: `boardSpecTool` is the tool definition, `boardDeclarationOf(shape)` says a table's shape as a Board declares an input, `answerBoardSpec(call, tables)` reads a call, binds it and says the Board or its refusal by path, and `sayBoard` says a bound Board. `boardOfChart(spec, shapes)` makes a `draw_chart` spec a Board of one view, `boardChartViewId`. The Docs Site's ExBoard Examples all run under React.
