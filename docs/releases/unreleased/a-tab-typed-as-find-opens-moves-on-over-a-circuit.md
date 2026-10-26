Product: ExGrid
Kind: Fixed
ADR: 0240

On the Server host, a Tab typed the moment Find's field took the keyboard after Ctrl+F, before the grid had finished answering Ctrl+F, was refused, and the field kept the keyboard. It now moves on in the panel, as it does when typed a moment later. The same holds for every popover a key opens: once every input before the key is answered and the popover holds the keyboard, the next key goes to it.
