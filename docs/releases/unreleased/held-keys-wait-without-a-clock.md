Product: ExGrid
Kind: Behaviour
ADR: 0240
Docs: exgrid/selection

Keys held while the grid answers an earlier key or click no longer give up after two seconds: they wait for the editor, a popover or your frame to take the keyboard, however long the round trip, and a reveal's scroll, an edit's close and a field's text no longer stop waiting on a clock either. When the grid has answered everything before them and owes the keyboard to nothing, keys whose keyboard never arrived are refused, every one, and the live region says so in the words of the new `KeyRefusalLabelIds.KeyboardDidNotArrive`, which a Consumer replaces or silences through `CommandLabel` (ADR-0240). Before, they were replayed against whatever held the keyboard, or dropped silently. One wait keeps its two seconds for now: behind a key a popover answers by itself (Enter on a menu item, Tab into a filter, Enter in a filter's field).
