Product: ExGrid
Kind: Added
Docs: getting-started#when-a-page-holds-another-version-s-files
ADR: 0123

A page that still holds another version's files says so. A grid whose script is not the one it was built with, or did not load, attaches nothing, says "This page holds another version of the grid's script. Reload the page." in place of its rows and once to a screen reader, and still asks for its rows; until now it stayed dimmed, busy and silent. A grid, a Sheet or a pivot shows a notice when its stylesheet is another version's, by the stylesheet's own rules. Both are the built-in Chrome's words, replaced through the label function by the ids in `ReloadNoticeLabelIds`.
