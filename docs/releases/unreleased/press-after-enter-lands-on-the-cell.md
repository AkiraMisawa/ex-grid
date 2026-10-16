Product: ExGrid
Kind: Fixed
ADR: 0010, 0028, 0220

On Blazor Server, a press made straight after the Enter that commits an edit, on the cell that was being edited, was lost: it landed on the Cell Editor the Enter was removing, and the Focus stayed on the cell below. Such a press now keeps its place behind the Enter, and lands on the cell it was made over once the edit has ended, as a slower press does, on ExSheet as on any editing ExGrid (ticket 201).
