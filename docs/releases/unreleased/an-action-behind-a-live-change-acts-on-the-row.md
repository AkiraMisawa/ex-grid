Product: ExGrid
Kind: Fixed
ADR: 0142

A row Action clicked while an earlier input was still being answered, on a grid without a Row Key, now acts on its row's new version when a live change replaced that row where it stood. Before, it was refused as if the row had left the Window.
