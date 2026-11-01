Product: ExGrid
Kind: Added
ADR: 0381

A grid given `countErrors` (Blazor: `OnCountErrors`) says the Error Count in its status line: every cell that wears Error, and apart, those the error step cannot reach because a Filter hides their row or their column is hidden ("Errors: 3 (1 hidden)"). `ruleset.countErrors(request, rowsInOrder)` (`GridRuleset.CountErrors` in .NET) answers it; it is asked again after each judgement, Source, order or change of visible columns, and pressing it steps as F8 does. It is given only beside `findError` (`OnFindError`), and refused at mount without it.
