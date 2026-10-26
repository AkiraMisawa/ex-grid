Product: ExGrid
Kind: Fixed
ADR: 0040

In `@ex-grid/core`, an arrow key or PageUp/PageDown now moves the Focus while a stretched box is too small to hold a row, as when the box grows back before the browser has reported its new size. The Focus it moved is brought into view once the box can show it. Before, the key was dropped. Also, Shift+→ on whole columns now brings back the Settled Mark (ADR-0220). Before, it waited for ever on a page follow to the last row, which the view never scrolls to (ADR-0052).
