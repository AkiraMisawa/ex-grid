Product: ExPivot
Kind: Added
ADR: 0412

`ExPivot.Components.ExPivot`, and ExBoard's pivots drawn in the browser, take a field's `OrderKey`, which they refused by name: .NET calls it for each Item an answer names and sends the keys with the Items, and the browser orders the Items by them. A key crosses in its .NET type's order, exactly, as a text, a number, a Boolean or a whole number (integers, decimals, dates and times, a `Guid`, an enum); text keys are compared ordinally, where `ExPivot.Components.ExPivot` compares them in the current culture. A key of another type, two types in one field and a function that throws are refused by name, field and Item included, when the report orders by them.
