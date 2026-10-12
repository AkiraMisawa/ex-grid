Product: ExGrid
Kind: Added
Docs: exgrid/appearance#built-in-chrome-or-mudblazor-s
ADR: 0203, 0204

For a Chrome of your own: `IGridChrome.Words` answers the words a refusal is announced in, by the ids in `RefusalLabelIds`, and the column menu's, the Context Menu's, the filter panel's and the find panel's contexts carry `TakeFocus`, which a popover's contents call with their control instead of its own `FocusAsync`, so that it does not take the keyboard back from another grid pressed during a Blazor Server round trip.
