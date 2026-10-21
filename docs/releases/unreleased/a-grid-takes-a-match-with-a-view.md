Product: ExGrid
Kind: Added
ADR: 0332

`ShowViewWithMatch(view, match)` places a single cell as `ShowViewWith(view, range, focus)` does, under the view's Row Sequence Version. If the find panel is open when the version is taken, that cell becomes the current match, marked apart, and Replace writes it. A Consumer that finds a match among rows it shows next, as ExSheet does on another Sheet, hands it this way, and gives the step up by throwing `OperationCanceledException` from its `OnFind` answer. `ForgetCurrentMatch()` drops the current match, for a Consumer that writes cells behind the grid's back, so the next Replace finds before it writes.
