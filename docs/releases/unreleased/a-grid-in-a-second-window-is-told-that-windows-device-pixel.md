Product: ExGrid
Kind: Fixed
Docs: getting-started/react#a-grid-in-a-window-your-page-opens
ADR: 0090

A grid drawn in a second window the page opened (`window.open`, through a portal, as a docking layout pops a panel out) is
told that window's Device Pixel, as it is created and whenever that window's display scale or zoom changes. It was told the
opener's. With the two windows on displays of different scale, the popped-out grid placed its column edges and drew its
lines on the opener's device pixels, so they were blurred, and it heard nothing when its own window moved to another
display. The grids ExSheet and ExPivot draw are told the same way. A grid reads its window once, as it is created: one
whose element is moved into another document afterwards keeps the first window's Device Pixel, so create it again in the
window it moved to (issue #565).
