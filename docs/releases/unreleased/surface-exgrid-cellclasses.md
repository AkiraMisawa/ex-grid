Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Components.CellClasses` is no longer public: it composes the classes a cell wears. The class names themselves stay the presentation contract a stylesheet selects by (ADR-0029), and nothing replaces the type.
