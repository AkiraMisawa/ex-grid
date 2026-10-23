Product: ExPivot
Kind: Breaking
ADR: 0240

`PivotPopupFrame.OnFocusIn` and `OnFocusOut` are removed. The frame is ExPivot's own part, placed by ExPivot and hidden from a Consumer's editor; they carried the local queue of keys typed while a menu or a panel opened, which the pivot's Input Order replaces. Nothing replaces them.
