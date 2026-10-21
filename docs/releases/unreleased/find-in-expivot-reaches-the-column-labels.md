Product: ExPivot
Kind: Added
ADR: 0333

Ctrl+F in the report now finds the column labels and value captions too, which stand in the report grid's headers: a step that passes the last row, or finds no row, stops on the label that matches, marks it as the current match and says "Found in a header", the Focus staying where it was, and the next step goes on from it to the rows. Every label on screen that matches is marked as it is typed.
