Product: ExPivot
Kind: Fixed
ADR: 0350

Under ExPivot.MudBlazor, Field Settings… opened while no report was drawn — its first answer still on its way, or failed — left the keyboard where it was, because the box around its sort order's select cannot take focus. With no report grid, the pivot now hands the keyboard to a box's first control through the grid's script, as it does while a report stands; a Pivot Chrome of the Consumer's own may name a box to `TakeKeyboard` in every state of the report.
