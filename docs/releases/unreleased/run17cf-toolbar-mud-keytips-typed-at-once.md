Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0050

KeyTips can be typed at once after one Alt or F10: the letters typed before the KeyTips have taken the keyboard are held and handed to them in order, so on a Blazor Server circuit Alt, H, 1 typed straight off sets bold instead of opening an edit holding "h1" (a Tab among them is dropped with the keys after it). A second Alt typed while keys are still held for the toolbar or for the Sheet can still have an outcome that depends on the round trip (a known limitation, not yet decided).
