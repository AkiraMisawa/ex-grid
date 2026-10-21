Product: ExGrid
Kind: Added
ADR: 0331

An edit open as `ShowViewWith` shows other rows stays open with the rows it was opened among: its editor keeps the keyboard unseen, the Formula Bar shows its text, Point points at the rows shown, and its commit raises `OnEdit` with the row it was opened on; a view read of its rows, handed back, brings the editor back over its cell. `ReadEditText()` reads the open edit's text from a handler.
