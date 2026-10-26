Product: ExChart
Kind: Added
ADR: 0320, 0370

`@ex-grid/chart` answers a Chart Spec as the tool an AI calls answers it: `answerChartSpec(text, tables, metrics?)` gives the refusal with its path, or the chart said in words with how many pixels were thinned and how many points are missing, as C#'s `ChartSpec.Answer` does, on the same shared cases. It is published with 0.3.0.
