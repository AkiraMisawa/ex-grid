Product: ExGrid
Kind: Fixed
ADR: 0141

`GridSource.From` with a Row Key now moves the Row Sequence Version when a row that was changed in place (which ADR-0003 forbids, and the source cannot see) is then changed out of the result by a Change Batch. The Window already dropped the row, but the version stayed, so a Selection could survive on rows that had shifted under it.
