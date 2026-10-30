Product: ExGrid
Kind: Added
ADR: 0051, 0410

A Razor Chrome can draw the completion list and the argument hint of `CompleteEditorText` through the new `IGridChrome.EditorCompletion(GridEditorCompletionView)`, `ScriptSlots.EditorCompletion`, and the marks `ScriptSlotAttributes.CompletionList` and `CompletionCandidate`; `MudGridChrome` draws it, and `MudSheetChrome` gives it to `ExSheet.Components.ExSheet`. The chosen candidate is marked `aria-selected` in the browser, so ↑ and ↓ never wait on a Blazor Server circuit.
