Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0050

KeyTips can be typed at once: the letters typed after Alt is released, or after F10, before the KeyTips have taken the keyboard are held and handed to them in order. On a Blazor Server circuit, Alt, H, 1 typed straight off opened an edit in the cell holding "h1" under the KeyTips; it now sets bold. A Tab among them is dropped with the keys after it, as anywhere a key cannot be handed on.
