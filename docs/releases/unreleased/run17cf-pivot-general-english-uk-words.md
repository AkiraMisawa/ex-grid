Product: ExPivot
Kind: Behaviour
Docs: expivot/values#aggregations
ADR: 0060

Under an `en-GB` UI culture, ExPivot's English words are Excel's English (UK) edition's where that edition writes them differently: a Max or Min Value Field is captioned `Max. of Amount` and `Min. of Amount`, where it read `Max of Amount` and `Min of Amount`, and Value Field Settings lists `Max.` and `Min.` under `Summarise value field by`. Under any other UI culture, the other English cultures (`en-AU`, `en-IE`, `en-IN`, …) included, they stay the English (US) edition's. A Consumer's `Label` still wins: `PivotWords.EnglishFor` keeps the US words under `en-GB`, and the new `PivotWords.EnglishUnitedKingdom` gives the UK words under any culture. A report a server computes receives the words in its settings, never from the server's culture.
