# 02: A source held on a server answers its shape

Status: ready-for-agent
Roadmap: ExGrid, ExPivot, Data — A source that keeps its data on a server says its columns and their types before any row, so a saved view can be checked before it is drawn.

ADR-0341: the bundled fetching grid source, the pivot's report source and ADR-0322's Chart Source each
gain a call that answers a `DataShape` before any row; an Arrow schema answers one
(`ExGrid.Data.Arrow`). Binding checks it against the Board Input as a Snapshot's own shape is checked.
A Shaped Grid over a fetching source follows.
