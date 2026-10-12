Product: ExGrid
Kind: Behaviour
Docs: exgrid/selection#the-keyboard
ADR: 0044, 0039

Keys typed while one of the grid's popovers closes — Escape in it, a command or choice pressed in it, or a panel's OK, Apply or Cancel — wait until it has gone and the keyboard has landed, and then go there in order: on a Blazor Server circuit ↓ typed straight after Escape in a column menu moved in the closing menu and was lost, and now moves the Focus.
