Product: ExGrid
Kind: Added
Docs: exgrid/data#pushing-a-window--a-million-rows
ADR: 0140

A Row Key: `RowKey` names a row across versions, so a row that comes back as a new instance under the same key keeps its row component and only the text that changed is written. A Window in which a key repeats is refused by name, and `VouchesDistinctRows` lets the grid read only the keys it paints (#64).
