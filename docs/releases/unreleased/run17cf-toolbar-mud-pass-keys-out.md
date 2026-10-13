Product: ExGrid
Kind: Added
ADR: 0070, 0050

`ExGrid.PassKeysOutAsync()`, called from your `OnLeave` handler before its first `await`, passes the keys typed on the grid after the leaving Escape out to your element around it, in order, until the keyboard has left the grid, so a dialog of yours that closes on that Escape can hold them and hand them on with the keyboard (`ReturnKeyboardAsync(from, typedOnTheWay)` of another grid). While you listen to `OnLeave`, the keys typed after an Escape on the grid's root wait for the grid's answer to it, as the keys after a declared key do.
