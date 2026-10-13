Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Cells.EditorTextRules` is no longer public: it is how the grid's editor splices a completion into its text, which the completion a Consumer answers with (`EditorCompletion`, `CompletionCandidate`) already describes. Nothing replaces it.
