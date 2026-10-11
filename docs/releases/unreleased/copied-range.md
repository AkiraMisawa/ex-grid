Product: ExGrid
Kind: Added
Docs: exgrid/clipboard#copy
ADR: 0170

What was copied stays outlined with standing dashes while the clipboard still holds it, as Excel's moving border does. The outline goes when anything else is copied, on Escape, when an edit opens, and when a copied cell comes to show another value; Chrome and Edge before 143 show none (#68).
