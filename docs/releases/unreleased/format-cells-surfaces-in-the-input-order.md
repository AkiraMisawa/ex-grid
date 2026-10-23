Product: ExSheet
Kind: Breaking
ADR: 0240
Docs: exsheet/formatting#format-cells

`FormatCellsContext` no longer has `ReturnKeyboardFrom` or `HoldKeysForPopup`: a Chrome's own Format Cells no longer holds the keys typed on the way itself. It uses `InOrder` for a claimed control's handler, `JoinSurface` for a dialog or popup drawn outside the Sheet, `HandKeyboardToFrame` and `TakeKeyboard` to bring the keyboard to it, and `ReturnKeyboard` to give it back; the Sheet holds the keys meanwhile. Under ExSheet.MudBlazor, the Format Cells dialog and its More Colours popup are in the Sheet's order — Escape, OK, Cancel, × and More Colours' opener are handled in their place, and what is typed after them goes where they leave the keyboard — and `MudFormatCellsDialog` loses its `KeysArrive` and `KeyWhileClosing` parameters, which only the Wrapper set.
