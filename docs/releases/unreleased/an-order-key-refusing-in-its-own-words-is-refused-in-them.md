Product: ExPivot
Kind: Behaviour
ADR: 0412

In `@ex-grid/pivot-engine`, an Order Key that throws a `PivotOrderKeyError` naming its own field is refused in that error's words, where the report used to say "The Order Key of Region failed on 'East'." with the error as its cause. Any other error an Order Key throws is refused as before.
