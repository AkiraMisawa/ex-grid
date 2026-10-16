Product: ExPivot
Kind: Breaking
Docs: expivot/values#aggregations
ADR: 0060, 0201

Value Field Settings compares a typed Custom Name with the other Value Fields' captions in the report's own words: under `PivotWords.Japanese`, `合計 / Amount` beside a Sum of Amount is refused instead of renaming the Sum `合計 / Amount2`, and `Sum of Amount` is no longer refused there. `PivotLayoutEdits.SetValueField` now takes the report's `PivotOptions` as an optional fifth parameter, replacing the four-parameter signature: rebuild calling assemblies, and replace a four-argument method-group conversion with a lambda or a five-argument delegate; direct calls can omit the new argument.
