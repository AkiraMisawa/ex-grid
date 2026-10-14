Product: ExSheet
Kind: Fixed
Docs: exsheet/formatting#the-sheet-toolbar
ADR: 0100, 0039

In the built-in Chrome's Sheet Toolbar lists, arrow keys typed at once land where they lead: on a Blazor Server circuit, ↓ then → in the Fill palette could leave the keyboard on the first swatch, because the late arrival of the first move's focus put the place back. Enter and Space make the choice the keys have reached, from the list's own key handling rather than the browser's press of the focused button: Enter typed straight after ↓ chose the choice ↓ had left, and Enter held while the list opened (Alt, H, H, ↓, →, Enter at once) chose nothing.
