Product: ExPivot
Kind: Added
ADR: 0370

`@ex-grid/pivot-engine` lays a report out from a server's Level Aggregates: its cube takes each group's cells as answered and checks every set of groups shown below an expanded one against its parent, the records exactly and the parts within their rounding, refusing the whole answer by the group's name when one does not add up. The report is the one the leaves make, to the last bit. A `LocalPivotReportSource` over a source that declares `answersLevels` asks with the groups its layout shows, and asks again when an expand shows groups it does not hold; `PivotCube.isLevels` says a cube holds only those groups, and a collapse of them is laid out from it. `ExPivot.Engine` still refuses an answer of cells.
