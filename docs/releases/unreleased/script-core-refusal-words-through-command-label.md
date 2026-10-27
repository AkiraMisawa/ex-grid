Product: ExGrid
Kind: Fixed
ADR: 0203, 0350
Docs: exgrid/clipboard

In `@ex-grid/core`, a refused copy, paste, fill or Delete is now said in the words `commandLabel` answers for the reason's id, as the Blazor grid's `CommandLabel` already was: the ids are exported as `copyRefusalLabelIds`, `pasteRefusalLabelIds` and `shapeMismatchMaySpillLabelId`, with `refusalColumnPlaceholder` where the refusing column is written in. Before, the Script Core always said them in English. `undefined` keeps the English, the empty string silences the announcement while `onRefused` still receives the reason, and `japaneseGridWords` answers every one of them, so a grid handed the Japanese words now says its refusals in Japanese, and ExPivot's details grid does too. An empty answer for any other sentence the grid announces (a row command whose row is gone, an edit cancelled while checked, the error step) now says nothing too, rather than emptying the live region.
