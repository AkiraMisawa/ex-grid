Product: ExPivot
Kind: Fixed
ADR: 0350

The expand and collapse buttons of a report's label cells are heard on the report grid's root, and a row's key is made
once; each painted button carried three listeners of its own, and every pass made each painted row's key again.
