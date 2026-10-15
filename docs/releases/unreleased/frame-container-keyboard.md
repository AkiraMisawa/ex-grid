Product: ExGrid
Kind: Added
Docs: exgrid/appearance#built-in-chrome-or-mudblazor-s
ADR: 0039, 0050, 0210

HandKeyboardToAsync accepts a control container and gives its first enabled text field the keyboard with its value selected, while preserving a control the user already chose. Held text in a change-bound field commits on held Enter or when the field is left, without duplicating a normal change.
