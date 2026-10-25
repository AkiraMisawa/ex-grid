Product: ExGrid
Kind: Fixed
ADR: 0361

A press on the cell you are typing into, made before its Cell Editor appears (at once after the first key, on a slow Blazor Server connection), no longer commits the edit: the edit goes on, as it does when the press lands in the editor, and the caret stays where typing left it.
