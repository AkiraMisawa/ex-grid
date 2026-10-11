Product: ExGrid
Kind: Breaking
Docs: exgrid/editing#editable-columns-and-the-edit-intent
ADR: 0142

A change of the row order no longer discards an open edit, and `EditDiscardReason.OrderChanged` is gone. With a Row Key the editor moves with its row; without one, Enter is refused through the new `OnCommitRefused` as `OrderMoved`, and the typing is kept. Code that handled `OrderChanged` in `OnEditDiscarded` handles `OnCommitRefused` instead (#70).
