Product: ExGrid
Kind: Added
ADR: 0057

ExGrid in TypeScript outlines References, as the Blazor grid's `ReferencesIn`, `OnReferenceKeyColoursChanged` and `OutlinedColumns` do: `referencesIn` answers the References in the editor's text, each range named is outlined in its colour while the edit is open, `onReferenceKeyColoursChanged` tells the colour each key was given, and `outlinedColumns` outlines whole columns with or without an edit.
