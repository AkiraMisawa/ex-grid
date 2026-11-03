Product: ExGrid
Kind: Behaviour
ADR: 0451

A Find or error step taken while the one before it is still being answered now waits for it, and what is typed or
clicked meanwhile waits too: Find next pressed twice goes to the second match however slowly the Source answers, where
the second press used to call off the first and start from the same Focus. Esc cancels a step still out, and moves nothing.
ExSheet's and ExPivot's find panels, which are the grid's, behave the same.
