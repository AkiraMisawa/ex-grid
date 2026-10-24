Product: ExSheet
Kind: Added
ADR: 0331

A 3D Reference, `=SUM(Jan:Mar!B2)`, reads the same range on every Sheet from one to another in tab order, as Excel's does: `SUM`, `AVERAGE`, `COUNT`, `COUNTA`, `MAX`, `MIN`, `PRODUCT`, `MEDIAN`, `STDEV.S`, `STDEV.P`, `VAR.S`, `VAR.P`, `AND`, `OR`, `XOR`, `CONCAT`, `TEXTJOIN`, `LARGE`, `SMALL` and `RANK.EQ` read it as one range, `IF`, `CHOOSE`, `LET`, `IFERROR` and `IFNA` hand it on, and anywhere else it is the Error Value Excel gives there. A Sheet inserted, copied or moved between the ends is read; an end moved past the other, or deleted, hands its end to the Sheet next to it, as Excel does, and each is undone exactly. Rows and columns inserted or deleted on a Sheet of the span leave it as written. A rename rewrites the end it names, and an `.xlsx` stores it as written.
