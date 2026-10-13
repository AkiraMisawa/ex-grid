Product: ExPivot
Kind: Fixed
Docs: expivot/values#aggregations
ADR: 0060

Value Field Settings compares a typed Custom Name with the other Value Fields' captions in the report's own words. Under `PivotWords.Japanese`, `合計 / Amount` typed beside a Sum of Amount is refused, where it was kept and the Sum renamed `合計 / Amount2`, and `Sum of Amount` is no longer refused there. `PivotLayoutEdits.SetValueField` takes the report's `PivotOptions` for this, as an optional last argument.
