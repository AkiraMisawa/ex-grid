Product: ExPivot
Kind: Added
ADR: 0059, 0370

In `@ex-grid/pivot`, a `PivotShowDetailsEvent` carries the `source` its records are asked of, and the pivot's handle gains `openDetails(event)`, which opens the records of an event it raised as `detailsView` says: an `onShowDetails` handler that heard the records and does not take them hands them back.
