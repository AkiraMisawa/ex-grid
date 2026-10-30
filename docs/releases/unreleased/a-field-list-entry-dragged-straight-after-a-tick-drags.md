Product: ExPivot
Kind: Fixed
ADR: 0410

On the Server host, an entry of the MudBlazor Wrapper's PivotTable Fields pane pressed and dragged within a round trip of a field being ticked now drags. Until .NET drew the pane again, the drag was refused because the Areas had changed, even though the entry pressed still held the same field. A drag is now refused only when the place pressed holds another field.
