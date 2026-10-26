Product: ExGrid
Kind: Fixed
ADR: 0141

When the read a `FetchingGridSource.NotifyChanged` asked for fails, the next question asked, a scroll's or the next notice's, now carries the change that read could not show. Before, it was answered as a plain scroll: a server that sends no order token left the Row Sequence Version where it was, so a Selection taken against the old order stood.
