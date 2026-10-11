Product: ExGrid
Kind: Fixed
ADR: 0012

A move that reveals rows, such as Ctrl+↓, Ctrl+↑ or PageDown, is followed by a repaint over the next two frames, so a browser that renders in software, as on a Windows PC under Citrix, no longer leaves the Viewport white until the next scroll (VZ-18, #69).
