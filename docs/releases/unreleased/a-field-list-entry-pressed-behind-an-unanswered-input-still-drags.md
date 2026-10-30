Product: ExPivot
Kind: Fixed
ADR: 0362

A field or an entry of the PivotTable Fields pane pressed while an input before it is still being answered, such as the MudBlazor Wrapper's Show Details tab still on its way on the Server host, drags again: the Script Core held the press with its default prevented, and the browser starts no drag from such a press, so the field stayed where it was and the report never offered to remove it. The press now keeps its default, so the drag starts, and the pivot hears the press and the focus it made in their turn.
