Product: ExGrid
Kind: Behaviour
ADR: 0440

On Blazor, a Sort or Filter the user applies is the grid's at once: the header shows it and the grid is loading until the server's rows arrive, as under a `FetchingSource`. A filter panel opened straight after OK now shows the filter just applied, where on Blazor Server it could show the one before and offer no "Add current selection". A click on the rows before they arrive is taken against the order before, and the new order drops it.
