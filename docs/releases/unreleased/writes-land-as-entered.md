Product: ExGrid
Kind: Added
Docs: exgrid/editing#editing-while-values-change
ADR: 0142

Under live data a write lands as the user entered it, on the row it was aimed at. A commit over a cell that changed while the editor covered it raises `OnOverwriteNotice` with the text the user saw and the text it replaced, the Edit Intent carries both, and `GridEditIntent.Refuse(message)` keeps the editor open with the typing (#64, #70).
