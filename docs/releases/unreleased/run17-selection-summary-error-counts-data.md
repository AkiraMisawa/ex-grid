Product: Data
Kind: Behaviour
ADR: 0130

`AggregateAccumulator.Read(Aggregation.CountNumbers)` after `AddError` now answers how many values were numbers, as `Count` answers how many were not Blank, where it answered `AggregateError.ErrorAmongValues`; every Aggregation that reads numbers still answers that error. Code that took the error from Count Numbers as "an error was among the values" reads it from another Aggregation.
