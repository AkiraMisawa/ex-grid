Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0050

KeyTips can be typed at once: the letters typed before the KeyTips have taken the keyboard are held and handed to them in order, so on a Blazor Server circuit Alt, H, 1 typed straight off sets bold instead of opening an edit holding "h1" (a Tab among them is dropped with the keys after it). An Alt typed while keys are still held — straight after Enter on an edit, a second Alt, or in a list as it closes — has the outcome it has when typed slowly: Enter, Alt, H, 1 commits the edit and sets bold below it, Alt, Alt, H, 1 turns the KeyTips on and off and types "h1", and Escape three times in a list a KeyTip opened, then Alt, H, H, opens the Fill's list. Alt released before the ↓ of Alt+↓ no longer shows the KeyTips.
