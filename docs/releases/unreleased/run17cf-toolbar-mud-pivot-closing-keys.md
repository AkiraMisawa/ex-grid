Product: ExPivot
Kind: Fixed
Docs: expivot/values#show-details
ADR: 0061, 0070

Keys typed as Show Details' dialog closes, or as a field's menu or panel closes from inside, go where the keyboard lands, in order, under either Chrome: on a Blazor Server circuit ↓ typed straight after Escape in the dialog was lost and the report's Focus stayed put, and Enter typed straight after Escape in a field's menu ran the menu's first command. Now ↓ moves the report's Focus, and Enter is the field's button's, opening its menu again. A menu or panel stays on the page until the keyboard has left it, and its commands and OK do nothing meanwhile.
