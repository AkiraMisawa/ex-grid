Product: ExSheet
Kind: Added
ADR: 0058

The TypeScript Sheet takes `pointingScope` and `onPointingRefused`: while a Formula is typed, a press on a grid registered in the Scope writes `XLOOKUP` of the pressed cell by its row's key, or the pressed column, and the arrows then move inside that grid. The Linked Tables page runs it under React.
