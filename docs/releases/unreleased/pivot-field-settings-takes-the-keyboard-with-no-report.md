Product: ExPivot
Kind: Fixed
ADR: 0360

Under ExPivot.MudBlazor, Field Settings… opened while no report was drawn — its first answer still on its way, or failed — left the keyboard where it was, because the box around its sort order's select cannot take focus. With no report grid, the pivot now hands the keyboard to a box's first control through the grid's script, as it does while a report stands; a Pivot Chrome of the Consumer's own may name a box to `TakeKeyboard` in every state of the report.

With no report grid, the pivot's inputs now also take their turns one after another, in the order they arrived, instead of each being handled at once: a menu command or a panel opened while an earlier one was still handing the keyboard on no longer loses the keyboard.
