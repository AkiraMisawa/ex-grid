Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100

On the Sheet Toolbar, Escape goes back one level as Excel's does: in a list a KeyTip opened (Alt, H, F, C) it closes the list and shows the row's KeyTips again with the keyboard on the toolbar, rather than giving the keyboard to the Sheet, and after letters that narrow a level (Alt, H, O) it shows that level's every letter again; a second Escape shows the rows' letters and a third leaves none. Escape typed before such a list has taken the keyboard does the same, under either Chrome.
