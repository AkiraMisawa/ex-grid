// What ExBoard's Blazor bridge takes from ExPivot's (ADR-0370 section 1): the bridge to a C# Pivot Source, which a
// pivot over a source asks through, and the kinds of the pivot's Razor slots, which a Board's pivots are dressed with
// as ExPivot's own are (ADR-0410). Bundled into ExBoard's module from these sources; nothing else of ExPivot's bridge.

export { DotNetPivotSource, type WireSource } from './dotnet-pivot-source';
export { isPivotRazorSlotName, pivotRazorSlotKinds, pivotSlotHostOptions, type PivotGrids, type PivotRazorSlotName } from './razor-pivot-slots';
