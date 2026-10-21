Product: ExSheet
Kind: Behaviour
ADR: 0047

A Formula with functions nested more than 64 levels deep, Excel's limit, or nested more than 256 levels deep (parentheses, signs, functions, operations within operations) is now refused as it is typed, with the reason; so is such text typed after a sign without `=`, which stays a refused Formula rather than becoming text. A Consumer will see a Sheet Document or an `.xlsx` workbook holding such a Formula refused on open, whole, where it opened before.
