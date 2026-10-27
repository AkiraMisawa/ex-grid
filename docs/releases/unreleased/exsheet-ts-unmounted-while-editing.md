Product: ExSheet
Kind: Fixed
ADR: 0048

ExSheet in TypeScript and React no longer throws when it is destroyed, or unmounted, while a cell is being edited with the Sheet Toolbar shown: the edit the grid discards is not told, since the Sheet is gone.
