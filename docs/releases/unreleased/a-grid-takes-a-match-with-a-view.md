Product: ExGrid
Kind: Added
ADR: 0332

`ShowViewWithMatch(view, match)` places a single cell as `ShowViewWith(view, range, focus)` does, under the view's Row Sequence Version. If the find panel is open when the version is taken, that cell becomes the current match, marked apart, and Replace writes it. A Consumer that finds a match among rows it shows next, as ExSheet does on another Sheet, hands it this way.
