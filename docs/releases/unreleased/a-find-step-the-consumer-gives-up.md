Product: ExGrid
Kind: Behaviour
ADR: 0055

An `OperationCanceledException` thrown by `OnFind` (or a Source's find) for a step the grid did not cancel is now taken as a step the Consumer gave up: the Focus does not move, the find panel stays open with no outcome, and nothing is raised on `OnFindRefused`. Before, the exception escaped the grid. A Consumer that cannot read what it searches throws it and says why itself, rather than answering `NotFound` for cells it never read.
