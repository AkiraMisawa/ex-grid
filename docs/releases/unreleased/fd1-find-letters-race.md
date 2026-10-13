Product: ExGrid
Kind: Fixed
Docs: exgrid/sorting-filtering#with-gridsource-from
ADR: 0055, 0039

On Blazor Server, letters typed straight after a Ctrl+F pressed in a column's menu, its filter or the Context Menu could go missing from the find field — `Osei` arrived as `O`, `Os` or `Ose` — when they fell in the round trip between the menu closing and the field taking the keyboard; now every one of them lands in the field, in order, however long the round trip. ExSheet's Find, which is ExGrid's, gets the same fix.
