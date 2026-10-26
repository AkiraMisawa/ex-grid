Product: ExBoard
Kind: Fixed
ADR: 0340

A Board whose pivot view writes a layout of a version the reader does not read is now refused, naming the layout's path, rather than failing with a `NotSupportedException` that named nothing.
