Product: ExPivot
Kind: Breaking
ADR: 0201

`ExPivot.Engine.PivotValue` is no longer public: the value cell a Consumer reads is `PivotDisplayValue`, from `PivotDisplayRow.ValueAt`, with the same number, exact `decimal`, error and shown text, and the same raw form. `PivotValue.DivideByZeroText` goes with it: a division by zero still shows Excel's `#DIV/0!`.
